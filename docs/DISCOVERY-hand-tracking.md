# DISCOVERY — Hand tracking for LabyrinthXR

Status: **Spike 0 done and verified on Quest 3 (OpenXR, controllers only). Spike 1 implemented; headset verification pending.**
Discovery was Read-only investigation on 2026-11-20 at commit `0f59e8f`; no
packages installed, no project files changed. Implementation needs explicit approval (see §7).

## 1. Current state (verified in the project)

| Item | Finding | Evidence |
|---|---|---|
| Unity | 6000.4.7f1, built-in render pipeline | `UnityProject/ProjectSettings/ProjectVersion.txt` |
| XR provider | **Oculus XR Plugin 4.5.0** (Android loader only) + XR Management 4.5.0 | `UnityProject/Packages/packages-lock.json`, `Assets/XR/Loaders/OculusLoader.asset` |
| Hand support in that provider | **None**: no hand-tracking setting, no hand subsystem, no hand permission in the plugin. The only mention is a 2021 changelog fix for hand tracking provided by Meta's separate SDK on top. | `Library/PackageCache/com.unity.xr.oculus@…/Runtime/OculusSettings.cs`, `CHANGELOG.md:238` |
| Input | Legacy Input Manager only (`activeInputHandler: 0`); no Input System package, no XR Interaction Toolkit, no Meta SDK | `ProjectSettings/ProjectSettings.asset:779`, `Packages/manifest.json` |
| Controller code | Generic `UnityEngine.XR.InputDevices` only (grip, primary/secondary button, device poses, centre eye). Oculus-specific code exists only in the build script. | `Game/XrRig.cs:104-146`, `Editor/QuestBuild.cs` |
| Handle logic (reusable) | `HandGrip`: grab on a fresh grip press within 10 cm of the knob, hysteresis 0.55/0.35, release on tracking loss. `Lever`: value follows the hand along the handle's travel axis (8 cm = full tilt), end stops. `TiltAxis`: ≤6°, 60 ms smoothing, ≤40°/s → `board.MoveRotation`. | `Core/HandGrip.cs`, `Core/Lever.cs`, `Core/TiltAxis.cs`, `Game/LabyrinthGame.cs` |
| Hardware | Meta Quest 3 (`adb`: model `Quest_3`, product `eureka`) | `scripts/labyrinth.sh devices` |

`HandGrip` and `Lever` don't care where the position and grip value come from: a hand-tracking source can feed them
unchanged.

## 2. Technical options

Deciding facts from the official documentation:

- Unity XR Hands: *"Currently, the OpenXR package is the only plug-in provider that supports hand tracking."*
- Meta: *"The **OculusXR** plugin is deprecated and no longer receives updates or support for the latest Meta VR
  features."* New projects are told to choose OpenXR.
- Meta Interaction SDK: *"supports OpenXR via the Oculus OpenXR backend"* and depends on the Meta XR Core SDK.
- Unity OpenXR: *"Unity requires the use of the Input System package when using OpenXR."*

**Conclusion: every option requires switching the provider to OpenXR and adding the Input System package.**

Versions recommended by this editor's package manifest (not installed, compatibility with this project untested):
OpenXR 1.16.1, XR Hands 1.7.3, Unity OpenXR: Meta 2.4.1, Input System 1.19.0 (Oculus XR 4.5.4, XR Interaction
Toolkit 3.4.1 for reference).

| | A — XR Hands + Unity's grab (XRI) | B — Meta Interaction SDK | **C — small custom adapter on XR Hands** |
|---|---|---|---|
| New dependencies | OpenXR, Input System, XR Hands, XR Interaction Toolkit | OpenXR, Meta XR Core SDK, Interaction SDK (Asset Store, large) | OpenXR, Input System, XR Hands |
| Fit with the handles | XRI grabbing would replace `HandGrip`/`Lever` | Grab transformers would duplicate `Lever`/`TiltAxis` | **Feeds the existing `HandGrip` → `Lever` → `TiltAxis`** |
| Complexity | Medium | High (second interaction system) | Low (about two small classes) |
| Precision control | XRI's | Meta's | Ours (existing smoothing, plus a filter if needed) |
| Testability | Partial | Hard | **Grab detection and source choice as pure EditMode tests** |
| Risk to controllers | Medium | High (Meta's own controller input) | Low after the provider switch (controllers keep their code path) |
| Maintenance | Medium | Tied to Meta SDK releases | Small, our own code |

## 3. Recommended approach

**Option C on top of XR Hands, preceded by a separate switch to OpenXR.**

- **Grab:** pinch (thumb tip to index tip distance) with hysteresis; its strength replaces the controller's analogue
  grip in the existing `HandGrip`, including the "fresh press only" rule.
- **Movement (mapping 1: grab and move the hand):** the handles are levers that swing along a travel direction, so
  hand translation matches their appearance and reuses `Lever` unchanged. Mapping 2 (hand rotation) suits a twist
  knob, which these are not, and wrist rotation is expected to be the noisier signal (expectation, not measured).
- **Hand position:** the palm joint, not the fingertips (expected to be steadier while pinching; to be checked on the
  headset).
- **Controllers and hands:** one build, a source per hand: a tracked controller always wins, otherwise the tracked
  hand. The Quest itself switches between controllers and hands when they are put down or picked up, so the game adds
  no switching logic. Hand tracking optional in the manifest, so controller-only play is unaffected.

## 4. Risks and unknowns

**Evidence-based**
- The OpenXR switch is the largest change: XR settings, `QuestBuild.cs`, Android manifest, Input System. It must not
  break controllers.
- With the Input System added, active input handling must be "Both" (the desktop keys use `Input.GetKey`).
- The Oculus XR Plugin is deprecated, so the switch is likely needed eventually anyway.
- `HandGrip` already releases on tracking loss and needs a fresh grab afterwards, so the board does not jump when
  tracking returns; the handle stays where it was left (`LosingTracking_Releases`, `HoldingGripThenReaching_DoesNotGrab`).

**Hypotheses that need the headset**
- Palm jitter when holding still and during slow movement; whether the existing smoothing (60 ms, 40°/s) is enough.
- Extra latency of hands compared with controllers.
- False grabs or drops of the pinch while holding still.
- Both hands staying tracked at the handle positions while looking down at the board (field of view, occlusion).
- Whether `InputDevices` controller input keeps working unchanged under OpenXR (the OpenXR manual neither confirms
  nor rules it out).
- Comfort of holding a pinch for a 30–90 s level.

**Limitation:** Quest Link is Windows-only, so on this Mac every hand-tracking check must run on the device.

## 5. Proposed spikes (each one commit, rollback with `git revert`)

### Spike 0 — switch to OpenXR, controllers only (no hands)
- **Hypothesis:** the game plays identically on OpenXR.
- **Scope:** add OpenXR, Unity OpenXR: Meta and Input System; active input handling "Both"; enable Meta Quest support
  and the Oculus Touch controller profile; update `QuestBuild.cs`.
- **Acceptance:** all 102 tests pass; the APK builds and its manifest is a Quest VR app. On the headset: both
  handles, A/X, hold B/Y, recenter and all five levels work; steady 72 FPS.

### Spike 1 — one hand, one handle (right hand → roll handle)
- **Hypothesis:** a pinch-and-move hand can operate one existing handle reliably.
- **Scope:** add XR Hands; enable the Hand Tracking Subsystem feature; manifest "controllers and hands". New pure
  `HandPinch` (pinch strength from fingertip distance, hysteresis) and `XrHandSource` (right hand's palm position,
  pinch and tracked state from `XRHandSubsystem`, used only when the right controller is not tracked) feeding the
  existing `HandGrip`. Debug spheres on palm and fingertips.
- **Expected behaviour:** pinch near the right knob grabs it; moving the hand right/left tilts the board right/left;
  opening the pinch releases; losing the hand releases without a jump; picking up the controller takes over.
- **Automated:** pinch hysteresis tests; source-choice tests (controller wins, hand otherwise); a PlayMode test that
  feeds simulated hand poses and checks the roll handle and board follow; existing suites unchanged.
- **Visual:** rendered snapshot of the debug markers at the knob (layout only, not real tracking).
- **Headset:** the expected behaviour above, plus a log of palm jitter while holding still.
- Two-handed play follows in a later spike, only if this one passes.

### Spike 0 — results (2026-11-20)
- **Change:** `com.unity.xr.oculus` removed; `com.unity.xr.openxr` 1.16.1 + `com.unity.inputsystem` 1.19.0 added (both
  bundled with the editor; Unity OpenXR: Meta not added, no need yet). Android loader = `OpenXRLoader` only; OpenXR
  features on: Meta Quest Support, Oculus Touch controller profile; render mode single-pass instanced; active input
  handling Both. `QuestBuild.Configure` sets all of this and drops the stale Oculus config object / preloaded asset.
  Game code unchanged (`XrRig` still reads `UnityEngine.XR.InputDevices`).
- **Automated tests:** EditMode 67/67 passed; PlayMode 35 passed, 2 skipped (the `Snapshots_*` renders, which skip
  without graphics by design) of 37 — same as the 67 + 35 reference.
- **Build:** `scripts/labyrinth.sh build` OK, 24 MB. OpenXR build validation: three `[Optional]` hints only
  (PoseControl type, StickControl, latency optimization "Prioritize Input Polling"); none applied.
- **Manifest** (aapt2, vs. the Oculus reference APK): still `com.oculus.intent.category.VR` + LAUNCHER,
  `android.hardware.vr.headtracking` required, `com.oculus.vr.focusaware`; `supportedDevices` now
  `quest|quest2|cambria|eureka|quest3s` (was `quest2|eureka|quest3s`); added `org.khronos.openxr.permission.OPENXR(_SYSTEM)`,
  the OpenXR runtime-service queries and `android.permission.INTERNET` (added by the OpenXR package; can be stripped
  via Meta Quest Support's "force remove internet permission" if wanted); removed the `com.unity.xr.oculus.*` meta-data.
- **APKs (not in git):** reference `UnityProject/Builds/reference/LabyrinthXR-oculus-0f59e8f.apk` (Oculus XR, built
  from `0f59e8f`), Spike 0 `UnityProject/Builds/reference/LabyrinthXR-openxr-spike0.apk`.
- **Headset (Quest 3, 2026-11-20, player + device log):** player confirmed: app starts in VR with the labyrinth in
  front; left and right handles each alone and together; release keeps the handle's position; A/X restart; hold B/Y
  to Level 1 (short press does nothing); recenter re-places the labyrinth; Levels 1–4 played through, Level 5 loaded
  and played (not completed; it is the hardest); feel unchanged vs. the Oculus build (player's judgement, no
  side-by-side run). Log: OpenXR runtime "Oculus" 210.137.0, session reaches FOCUSED in ~0.3 s, Oculus Touch profile
  bound (17 actions), no errors or exceptions, same process for the whole session; VrApi 72–73/72 FPS, isolated
  stale frames only while a system process (Meta menu / recenter) was active.

### Spike 1 — results (2026-11-20)
- **Change:** `com.unity.xr.hands` 1.7.3 (bundled); OpenXR Hand Tracking feature on; manifest gets
  `com.oculus.permission.HAND_TRACKING`, `oculus.software.handtracking` (required=false) and
  `com.oculus.handtracking.frequency=HIGH` (post-generate Gradle hook in `QuestBuild.cs`; neither package adds them).
  Core: `HandGrip` takes an `InputSource` (None/Controller/Hand); losing tracking or switching source releases the
  handle (it stays where it is) and the grip must open before the next press counts. `SourceChoice` (controller wins),
  `HandPinch` (tip distance 1.5 cm → 1, 5.0 cm → 0; with HandGrip's 0.55/0.35 a pinch closes below 3.1 cm and opens
  above 3.8 cm). Game: `XrHands` reads the right hand's palm/thumb tip/index tip; `HandSample.FromHand` uses the pinch
  point (between the tips) for the grab distance and the **palm** for the handle's travel; `XrRig` uses it for the
  roll handle only when the right controller is not tracked (controller tracking now also honours `isTracked`);
  debug markers on palm/tips; `[Hands]` log lines (source changes, grabs/releases with cause, per-second palm spread
  while held).
- **Regression found by test first:** `TrackingReturnsWithGripClosed_DoesNotRegrab_UntilOpenedAndPressedAgain` failed on
  the old `HandGrip` (it cleared its grip memory on tracking loss, so a hand returning pinched counted as a fresh press).
- **Automated tests:** EditMode 76/76 (67 + 9 new: tracking return, source switch while held, hand lost then
  controller, first source, pinch endpoints, pinch thresholds through HandGrip, pinch jitter); PlayMode 40 passed,
  3 skipped renders, of 43 (35 + 5 new `HandTrackingTests`: simulated pinch at the roll knob moves the handle and
  tilts the board right, open keeps it; ±1 mm palm / 2.2–3.4 cm tip jitter never drops; tracking loss while held then
  a pinched return neither jumps nor regrabs; controller picked up while held releases and keeps the handle; pinch away
  from the knob does not grab).
- **Off-headset render** (`Snapshots_HandMarkers`, runtime `HandMarkers` with the rig's material, simulated pose):
  thumb/index tips meet at the knob's surface on the player's side with the pinch point between them, the palm about
  7 cm behind and above; after a 4 cm sideways move the handle has swung right and the tips are still on the knob.
  First render had the pinch point inside the knob (tips hidden): the simulated pose was wrong, fixed in the test helper.
  Layout only; real tracked poses exist only on the headset.
- **Build:** OK, 25 MB, `UnityProject/Builds/reference/LabyrinthXR-hands-spike1.apk`; manifest checked with aapt2.
- **Headset:** not yet verified.

## 6. Verification plan

| Check | Level |
|---|---|
| Pinch detection, source choice, handle mapping, no jump after tracking loss | Automated tests (Claude) |
| Simulated hand moves the handle and board | PlayMode test (Claude) |
| Debug markers placed at the knob | Rendered snapshot (Claude) |
| Build, manifest, app starts with XR | `scripts/labyrinth.sh build` + device startup log (Claude, headset connected) |
| Real tracking quality, jitter, latency, pinch reliability, field of view, comfort, OS controller/hand switching, permission prompt | **Headset only** (player; Claude reads the logs) |

## 7. Go / No-Go

- **No-Go** for hand-tracking gameplay directly: the current provider cannot supply hands.
- **Go (on approval)** for Spike 0, the switch to OpenXR with controllers only.
- Spike 1 only after Spike 0 passes on the Quest.

## Sources
- [Unity XR Hands manual](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.8/manual/index.html)
- [Unity OpenXR plugin — Input](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/manual/input.html)
- [Meta — Hand tracking overview (Unity)](https://developers.meta.com/horizon/documentation/unity/unity-handtracking-overview/)
- [Meta — Interaction SDK hand grab](https://developers.meta.com/horizon/documentation/unity/unity-isdk-hand-grab-interaction/)
- [Meta — Interaction SDK packages and requirements](https://developers.meta.com/horizon/documentation/unity/unity-isdk-packages-and-requirements/)
- [Meta — Unity project setup](https://developers.meta.com/horizon/documentation/unity/unity-project-setup)
- [Meta — Unity OpenXR](https://developers.meta.com/vr/documentation/unity/unity-openxr/)
