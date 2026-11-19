# CLAUDE.md

Guidance for Claude Code in this repository.

## What this is

LabyrinthXR: a cozy VR adaptation of the wooden tilt-maze toy for Meta Quest 3 (Touch controllers). The player stands at
a 1.1 m wooden labyrinth on a table and tilts it with two handles: left hand = pitch (push away = far edge down), right
hand = roll (push right = right edge down). A steel ball rolls by Unity physics; holes reset it, the goal wins.
Spec: `docs/EPIC-001.md` (scope, DoR/DoD status, verification log). Keep "automated test" / "off-headset render" /
"build" / "headset" claims separate, and never claim headset verification from desktop runs.

Sibling projects `../lilla-fruktstandet-funplay-spike` and `../CampfireVR` are reference only (patterns were copied
from them): read-only, never modify them or kill their Unity processes.

## Commands

Unity 6000.4.7f1, built-in render pipeline. The project must not be open in the Editor while these run.

```sh
scripts/labyrinth.sh test        # EditMode + PlayMode (batchmode, -nographics), watched; TEST_FILTER=<regex>, TEST_PLATFORMS=EditMode
scripts/labyrinth.sh snapshots   # renders of the real scene (needs graphics) -> UnityProject/Logs/shots/*.png; inspect them
scripts/labyrinth.sh scene       # regenerate Assets/Scenes/Labyrinth.unity + Assets/Labyrinth/Generated/ (SceneSetup)
scripts/labyrinth.sh build       # Quest APK -> UnityProject/Builds/LabyrinthXR-quest.apk
scripts/labyrinth.sh install | launch | log | devices   # adb (install -r only, never uninstall)
```

Always run Unity tests through the script (progress, stall watchdog, stack sample on hang). PlayMode tests load the
scene with `SceneLoad.Fresh` (synchronous) and step physics deterministically with `LabyrinthGame.Advance` (script
simulation mode); `RealFrameLoop_*` covers the real FixedUpdate/Update loop, which `Advance` bypasses.

## Architecture

- `Labyrinth.Core` (pure, EditMode-tested): `Lever` (handle value -1..1 from hand travel, end stops, optional return to
  neutral), `TiltAxis` (handle -> board degrees: limit, exponential smoothing, rate limit), `HandGrip` (fresh grip press
  near the knob grabs; hysteresis; tracking loss releases), `Round` (Playing -> Falling -> reset -> Playing; Won until
  Restart), `MazeLayout` (walls/holes/start/goal/route in cell units, `ToBoard` -> metres; `First()` is the level).
- `Labyrinth.Game`: `LabyrinthGame` (on the root) is the single runtime entry: `Tick(dt)` steps handles -> axes ->
  `board.MoveRotation` (kinematic board), then fall/goal detection from the ball's board-space position. The ball is
  never moved by input. `BoardBuilder` builds all geometry from the layout (used by `SceneSetup` at edit time; the scene
  stores the result). `FloorMesh` = one mesh with round holes (non-uniform grid, hole tiles zipped to circles, no
  T-junctions; the MeshCollider uses its top surface). `LeverView` = the visible handle. `XrRig` reads controllers via
  `UnityEngine.XR.InputDevices` (no XRI / Input System) and places the labyrinth in front of the player on start and
  recenter. `DesktopInput` (WASD/arrows, R) works only without XR and outside batch mode. `LabyrinthFeedback` = message,
  goal glow, synthesised sounds (`SoundSynth`, no audio files).
- `Editor`: `SceneSetup` (scene, procedural wood textures, materials, warm reflection cubemap, room, lights) and
  `QuestBuild` (Oculus loader on Android only, IL2CPP/ARM64, Vulkan, preloaded-XR-assets guard from CampfireVR).
- Change the scene through `SceneSetup`/`BoardBuilder`, not by hand. Don't add packages without a concrete need.
- Never put a component that moves its own transform on the labyrinth root (all game objects hang off it; see the
  verification log in docs/EPIC-001.md).

## Working rules

- New logic and bug fixes are test-driven where practical; a bug fix starts with a test that reproduces it.
- Debug non-obvious failures from evidence (logs, instrumented asserts, renders), not guesses; write the 5-whys chain
  into the epic's verification log, including why it was not caught earlier.
- Visual changes are verified by rendering snapshots and looking at them, not by reading scene configuration.
