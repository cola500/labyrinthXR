# EPIC-001 — VR Labyrinth: playable vertical slice

Status: implemented and verified off-headset; **awaiting headset acceptance test** (no Quest was connected during
development). Verification levels used below: **[T]** automated test, **[R]** off-headset render inspected by Claude,
**[B]** build, **[H]** headset (not yet done).

## Environment (Slice 0)

| Item | Value |
|---|---|
| Unity | 6000.4.7f1, built-in render pipeline, linear colour space |
| Packages | test-framework 1.6.0, xr.management 4.5.0, xr.oculus 4.5.0 (+ built-in modules: physics, audio, xr, vr, imageconversion, imgui, jsonserialize, androidjni) |
| Target | Meta Quest 3 (Quest 2/3S also enabled), Android IL2CPP ARM64, Vulkan, multiview |
| Input | `UnityEngine.XR.InputDevices` (grip, primary button, device poses) — as in Lilla fruktståndet / CampfireVR |
| Testing without headset | EditMode logic tests, PlayMode physics tests in the real scene (script-stepped), real frame-loop test, Snapshots renders of the real scene (PNG), physical autopilot playthrough, desktop keyboard adapter |

## Design decisions

- **Handles**: two joystick-like levers on brackets at the frame's front corners (36 cm left/right of centre). The
  left swings away/towards the player (pitch), the right swings left/right (roll); a slot in each bracket shows the
  direction. 16 cm shaft, ±30° swing = ±8 cm of hand travel for full tilt; the knob follows the hand 1:1.
- **Mapping**: push away = far edge down; push right = right edge down → the ball rolls the way you push.
- **Tilt**: ±6° per axis, 60 ms smoothing, ≤40°/s. Configurable on `LabyrinthGame` (max tilt, rate, smoothing,
  response exponent, lever swing, return-to-neutral on release, physics step, fall pause).
- **Release**: a released handle stays where it was (like the knobs of a wooden labyrinth). `returnToNeutralOnRelease`
  switches to easing back to neutral.
- **Grab**: a fresh grip press within 10 cm of the knob; the left hand can only take the left handle, the right hand
  the right one. Losing tracking or app focus releases.
- **Physics**: kinematic board turned with `MoveRotation` at 90 Hz; dynamic steel ball (Ø 56 mm, continuous
  speculative collision, raised max angular velocity, light damping); one MeshCollider floor with real round holes
  (Ø 88 mm); box-collider walls; the frame's tray catches fallen balls.
- **Level**: 1.10 × 0.88 m, four-lane serpentine, 9 holes, 3 short baffles, route line, start disc, green goal.

## Definition of Done — status

### Gameplay
| Criterion | Status | Evidence |
|---|---|---|
| Board tilts on both axes | ✅ [T][R] | `PitchForward_…`, `RollRight_…`, render 08/09 |
| Left and right controls independent | ✅ [T] | `TwoHands_OperateTheirHandlesIndependently`, `HandOnRollKnob_…` (other handle untouched) |
| Both simultaneously | ✅ [T] | `BothHandlesTogether_…` (diagonal roll, both axes at target) |
| Tilt responsive and smooth | ✅ [T] / ⏳ [H] | rate limit + smoothing tests; feel needs the headset |
| Ball rolls by tilt and gravity | ✅ [T] | direction tests; ball only moved by physics |
| Ball collides with walls | ✅ [T] | `Wall_BlocksTheBall`, `RimWall_BlocksTheBall` |
| Ball falls through holes | ✅ [T][R] | `EveryHole_SwallowsTheBall` (all 9), render 10/11 |
| Fall triggers reliable reset | ✅ [T] | `Hole_BallFallsThrough_AndIsResetToStart` |
| Goal detection | ✅ [T][R] | `Goal_Wins_…`, render 12/13 |
| Restart after success | ✅ [T] | `Goal_Wins_SettlesBall_AndRestartWorks`; A/X in VR ⏳ [H] |
| Maze physically navigable | ✅ [T] | `Autopilot_CompletesTheMaze_WithHandlesOnly`: 21.7 s, 0 falls, handles only |
| No known blocking defects | ✅ off-headset | |

### Engineering
| Criterion | Status |
|---|---|
| Project opens without blocking errors | ✅ batchmode loads/compiles cleanly (only offline licensing/CDN notices) |
| No new unexplained console errors | ✅ test and snapshot logs checked |
| Scene and assets saved | ✅ `Assets/Scenes/Labyrinth.unity`, `Assets/Labyrinth/Generated/` |
| Parameters configurable | ✅ serialized on `LabyrinthGame`, `XrRig`, `LabyrinthFeedback` |
| Automated tests pass | ✅ 29 EditMode + 15 PlayMode (+1 explicit Snapshots) |
| Build succeeds | ✅ [B] `scripts/labyrinth.sh build`: 23 MB APK; manifest has the Oculus VR category, head tracking, arm64, Quest 2/3/3S |
| No unnecessary dependencies | ✅ only XR Management + Oculus XR + test framework |

### Visual quality ([R], renders in `UnityProject/Logs/shots/`)
Board, walls, holes, ball and goal distinguishable; warm lamp light with soft shadows; steel ball reflects a warm
room; handles at the front corners; no floating or intersecting geometry found in the side/tilted views; VR eye view
(1.65 m, 0.8 m from centre, 95° FOV) frames the whole board with the handles in the lower corners.

## Acceptance scenarios
A ✅ [T] · B ✅ [T] · C ✅ [T] · D ✅ [T][R] · E ✅ [T][R] · F ✅ [R] (desktop/VR-eye renders) — all ⏳ [H] on the headset.

## Headset acceptance test (to do, [H])
1. Labyrinth appears in front at a comfortable height; both knobs reachable without leaning.
2. Knobs highlight when a hand is near; grip grabs; the knob follows the hand; a short haptic tick on grab.
3. Left hand away/back tilts the far edge down/up; right hand right/left tilts right/left edge down; both at once.
4. Tilt feels smooth and controllable (not too fast/slow; try `maxTiltDegrees` 4–8 if not).
5. Ball visible at all times while on the board; holes and goal readable.
6. Falling: "Oops!" + sound, ball back at start after ~1 s. Goal: chime, glowing goal, A/X restarts.
7. Meta-button recenter puts the labyrinth in front again. Frame rate stays smooth (`scripts/labyrinth.sh log`).
8. Text: check that the hint text is upright (see known limitations).

## Known limitations
- Nothing verified on a headset yet: controller bindings, grab ergonomics, comfort, frame rate, recenter.
- Hint text (TextMesh) shows a slight slant on part of the lines in some oblique renders, though it renders upright
  head-on (render 07b). Cause not found within the time box; check in the headset.
- The board is placed relative to the head at start/recenter; its height is fixed (board surface 0.95 m).
- Wall textures use default cube UVs (grain stretches on long walls). Placeholder room art.
- Win time is shown but there is no scoring (out of scope).

## Verification log

**2026-11-19 — Slices 1–3 (first pass).** 26 EditMode + 12 PlayMode passed on the first run; autopilot reached the
goal. Not trusted alone: the visual review followed.

**2026-11-19 — first visual review found two defects that all tests had missed.**

*1. Only 4 of 9 holes existed.* Why? The hole cells in the floor mesh were plain quads. Why? A hole tile was only
emitted when one grid cell equalled the tile, and grid lines from neighbouring holes split some tiles. Why not caught?
The tests checked the *layout* (holes clear of walls, route clear of holes), not the *mesh*, and the autopilot route
never touched the missing holes. Fix: tiles cover all cells inside them; the outline contains every grid point on its
edges and is zipped to the circle by angle; tile edges are cut in quarters by grid lines (corners alone gave chords that
cut into the hole: the area test then showed +0.0095 m²). New tests: floor area = board − 9 hole polygons, all faces
up, no triangle covers a hole centre, every hole swallows the ball physically.

*2. The ball was not on the board (flung to y = 33 m).* Why? The kinematic board's `MovePosition` jumped and launched
the ball. Why did the board jump? It follows the labyrinth root, and the root moved. Why? `LabyrinthFeedback` moved
its rolling-sound AudioSource to the ball every frame — and that AudioSource was on the root GameObject. Evidence:
logged positions showed the root displaced by exactly the ball's start position. Why not caught? PlayMode tests step
physics with `Advance()` inside one frame, so `Update` side effects never ran between steps, and positions were
compared in board space. Fix: the sound has its own child object. New test `RealFrameLoop_KeepsLabyrinthAndBallInPlace`
runs the real FixedUpdate/Update loop; CLAUDE.md warns against self-moving components on the root.

**Further render fixes:** light "spikes" round hole rims (rim vertices shared by top and wall → averaged normals; walls
now have their own rim vertices); 2 cm gap between frame walls and table; hint text enlarged for VR.

**Results after fixes:** 29 EditMode passed, 15 PlayMode passed (Snapshots explicit/skipped in the normal run),
14 renders inspected. Autopilot: 21.7 s, 0 falls.

**Build [B]:** Quest APK built in 2.5 min (IL2CPP/ARM64). Manifest checked with aapt2: `com.oculus.intent.category.VR`,
`android.hardware.vr.headtracking`, `vr_only`, focus-aware, supported devices quest2|eureka(Quest 3)|quest3s. Not
installed: no headset connected.

**Code review of the untested XR path:** `XrRig` found the feedback with `GetComponentInParent`, but the rig is not
under the labyrinth, so the grab click would never play (fixed). Hand spheres used the red knob material (now pale).
Tests, renders and the build were re-run afterwards: all green.
