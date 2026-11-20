#!/usr/bin/env bash
#
# scripts/labyrinth.sh — tests, off-headset renders, scene generation and the Quest build for LabyrinthXR.
# Modelled on Lilla fruktståndet's scripts/quest.sh. The Unity project must NOT be open in the Editor while these run.
#
# Usage:
#   scripts/labyrinth.sh test          # EditMode + PlayMode tests (batchmode, -nographics), watched; TEST_FILTER=<regex>
#   scripts/labyrinth.sh snapshots     # the explicit Snapshots renders (needs graphics); PNGs in UnityProject/Logs/shots/
#   scripts/labyrinth.sh scene         # regenerate Assets/Scenes/Labyrinth.unity and its generated assets (SceneSetup)
#   scripts/labyrinth.sh build         # Quest APK -> UnityProject/Builds/LabyrinthXR-quest.apk
#   scripts/labyrinth.sh install       # adb install -r of that APK on the one connected headset (never uninstalls)
#   scripts/labyrinth.sh launch | log | devices

set -euo pipefail

UNITY_VERSION="${UNITY_VERSION:-6000.4.7f1}"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
ADB="${ADB:-/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="${ROOT}/UnityProject"
APK="${PROJECT}/Builds/LabyrinthXR-quest.apk"
PACKAGE_ID="com.labyrinthxr.prototype"

editor_open() {
    pgrep -f "Unity -projectPath ${PROJECT}( |$)" >/dev/null 2>&1 || pgrep -f "Unity.*-projectpath ${PROJECT}( |$)" >/dev/null 2>&1
}

guard() {
    if [[ -f "${PROJECT}/Temp/UnityLockfile" ]] && editor_open; then
        echo "[labyrinth] The project is open in the Unity Editor; close it first." >&2
        exit 2
    fi
}

# One watched Unity test run (see Lilla fruktståndet docs/TESTING.md): shows "[test-progress]" lines, stops the run when
# no test has finished for TEST_STALL s (120) or after TEST_TIMEOUT s (600), and saves a stack sample first.
unity_tests() {
    local platform="$1" name="$2"; shift 2
    local limit="${TEST_TIMEOUT:-600}" stall="${TEST_STALL:-120}" log="${PROJECT}/Logs/${name}.log" results="${PROJECT}/Logs/${name}-results.xml"
    mkdir -p "${PROJECT}/Logs"
    rm -f "$results" "$log" "${PROJECT}/Logs/${name}-hang.txt"
    "$UNITY" -batchmode "$@" -projectPath "$PROJECT" -runTests -testPlatform "$platform" -testResults "$results" -logFile "$log" &
    local pid=$! waited=0 quiet=0 seen=0 current="" hung=""
    while kill -0 "$pid" 2>/dev/null; do
        sleep 1; waited=$((waited + 1)); quiet=$((quiet + 1))
        local lines n l
        lines=$(grep -a '^\[test-progress\]' "$log" 2>/dev/null | uniq || true)
        n=$(grep -c . <<<"$lines" || true)
        if (( n > seen )); then
            while IFS= read -r l; do
                case "$l" in
                    "[test-progress] start "*) current="${l#\[test-progress\] start }" ;;
                    "[test-progress] run "*) echo "[test] ${name}: ${l#\[test-progress\] run } tests" ;;
                    "[test-progress] Failed "*) printf '\n[test] %s: FAILED %s\n' "$name" "${l#\[test-progress\] Failed }"; quiet=0 ;;
                    *) quiet=0 ;;
                esac
            done < <(tail -n +$((seen + 1)) <<<"$lines")
            seen=$n
        fi
        (( seen == 0 )) && quiet=0
        if (( waited >= limit )); then hung="after ${limit}s (TEST_TIMEOUT)"; elif (( quiet >= stall )); then hung="no test finished for ${stall}s (TEST_STALL)"; fi
        if [[ -n "$hung" ]]; then
            sample "$pid" 3 -file "${PROJECT}/Logs/${name}-hang.txt" >/dev/null 2>&1 || true
            kill "$pid" 2>/dev/null || true
            printf '\n[test] %s: HUNG, %s; stopped%s. Log: %s\n' "$name" "$hung" "${current:+ in ${current}}" "${log#"${ROOT}/"}" >&2
            return 1
        fi
    done
    local status=0 line
    wait "$pid" || status=1
    line=$(grep -m1 -o '<test-run [^>]*' "$results" 2>/dev/null || true)
    if [[ -z "$line" ]]; then
        echo "[test] ${name}: no results (compile error?). $(grep -a -m3 'error CS' "$log" || true)" >&2
        return 1
    fi
    echo "[test] ${name}: $(sed -E 's/.*total="([0-9]+)".*passed="([0-9]+)".*failed="([0-9]+)".*skipped="([0-9]+)".*/\2 passed, \3 failed, \4 skipped (of \1)/' <<<"$line")"
    return $status
}

run_tests() {
    guard
    local status=0 filter=()
    [[ -n "${TEST_FILTER:-}" ]] && filter=(-testFilter "$TEST_FILTER")
    for platform in ${TEST_PLATFORMS:-EditMode PlayMode}; do
        unity_tests "$platform" "$platform" -nographics ${filter[@]+"${filter[@]}"} || status=1
    done
    return $status
}

snapshots() {
    guard
    rm -rf "${PROJECT}/Logs/shots"
    local status=0
    unity_tests PlayMode snapshots -testFilter "${TEST_FILTER:-Snapshots}" || status=1
    echo "[snapshots] $(ls "${PROJECT}"/Logs/shots/*.png 2>/dev/null | wc -l | tr -d ' ') images in UnityProject/Logs/shots/"
    return $status
}

scene() {
    guard
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -executeMethod Labyrinth.EditorTools.SceneSetup.CreateScene -quit \
        -logFile "${PROJECT}/Logs/scene.log" || { echo "[scene] failed, see UnityProject/Logs/scene.log" >&2; grep -a 'error CS\|Exception' "${PROJECT}/Logs/scene.log" | head -5 >&2; exit 3; }
    echo "[scene] OK"
}

# Engine classes the game creates at runtime (CreatePrimitive in MazeBuilder at a level change) but no scene keeps:
# IL2CPP stripping drops them unless Assets/link.xml preserves them. Checked in every built APK's metadata.
REQUIRED_TYPES=(CapsuleCollider BoxCollider SphereCollider MeshCollider)

check_apk() {
    local missing=() t meta
    meta="$(mktemp)"
    unzip -p "$1" 'assets/bin/Data/Managed/Metadata/global-metadata.dat' >"$meta" 2>/dev/null || true
    for t in "${REQUIRED_TYPES[@]}"; do
        LC_ALL=C grep -a -q "$t" "$meta" || missing+=("$t")
    done
    rm -f "$meta"
    if (( ${#missing[@]} )); then
        echo "[build] stripped from the APK: ${missing[*]} (runtime CreatePrimitive would fail; see Assets/link.xml)" >&2
        return 1
    fi
    echo "[build] runtime types present: ${REQUIRED_TYPES[*]}"
}

build() {
    guard
    echo "[build] Unity ${UNITY_VERSION} -> ${APK#"${ROOT}/"}"
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -buildTarget Android \
        -executeMethod Labyrinth.EditorTools.QuestBuild.Build -logFile "${PROJECT}/Logs/quest-build.log" \
        || { echo "[build] failed, see UnityProject/Logs/quest-build.log" >&2; exit 3; }
    [[ -f "$APK" ]] || { echo "[build] no APK produced" >&2; exit 3; }
    check_apk "$APK" || exit 3
    echo "[build] OK · $(du -h "$APK" | cut -f1)"
}

device() {
    local list n
    list="$("$ADB" devices | awk 'NR>1 && $2=="device" {print $1}')"
    [[ -n "${QUEST_DEVICE:-}" ]] && { echo "$QUEST_DEVICE"; return; }
    n=$(grep -c . <<<"$list" || true)
    if (( n != 1 )); then
        echo "[adb] Need exactly one authorised headset (found ${n}). USB: plug in and allow USB debugging; or QUEST_DEVICE=<serial>." >&2
        exit 4
    fi
    echo "$list"
}

case "${1:-}" in
    test) run_tests ;;
    snapshots) snapshots ;;
    scene) scene ;;
    build) build ;;
    install) d="$(device)"; "$ADB" -s "$d" install -r "$APK" ;;
    launch) d="$(device)"; "$ADB" -s "$d" shell monkey -p "$PACKAGE_ID" -c android.intent.category.LAUNCHER 1 >/dev/null ;;
    log) d="$(device)"; "$ADB" -s "$d" logcat -s Unity ;;
    devices) "$ADB" devices -l ;;
    check-apk) check_apk "${2:-$APK}" ;;
    *) sed -n '2,13p' "$0"; exit 1 ;;
esac
