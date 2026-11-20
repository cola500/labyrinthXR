# FEATURE — Fit the labyrinth to the player, and both bare hands

Goal: make LabyrinthXR easy for a small player (Viola) to reach and play with hand tracking: lower the table, bring
the handles closer together, and let both bare hands work their handles at once.

## 1. Table height and handle spacing (fit)

### Pattern reused from Market VR
Market VR's counter height (`StandView.SetHeight`, `FruitStand.SetHeight`, `XrInputRouter.CounterSpeed`):
one clamped value with `MinHeight`/`MaxHeight`/`DefaultHeight`, applied to the whole assembly at once (counter top
and legs, then `Physics.SyncTransforms`), changed at a fixed speed (0.25 m/s) from the right stick's vertical axis
or `+`/`-`/PageUp/PageDown on the desktop, while the player is never moved. Here the same shape is `PlayerFit`
(Core) with `LabyrinthGame.ApplyFit`; the stick/keys use the same speed and keys.

Added on top (Market VR has no hand tracking and does not save the height):
- **Bare hands / direct manipulation:** pinch (or grip) one of the two brass grips on the table's front edge and move
  the hand up/down: the table follows the hand 1:1. Pinch a handle's wooden base and move the hand sideways: that
  handle follows the hand, the other moves the same amount (symmetric). Each fit value is a `Lever` whose travel is
  half its range, so the existing `HandGrip` → `Lever` path does it, including slip at the limits.
- **Reset:** hold a fingertip (or a controller) on the round button in the middle of the table's front edge for 1 s.
  Desktop: Home.
- **Saved** on the device (`PrefsFitStore`, PlayerPrefs) when an adjustment ends; reset forgets it.
- **Feedback:** the message above the board shows "Table 70 cm · handles 30 cm apart" while adjusting (2.5 s);
  grips and bases light up on hover/hold; the reset button turns green as the hold progresses; the grab click
  sound and, with a controller, a haptic pulse.

### Measures
| | Min | Default | Max |
|---|---|---|---|
| Board surface above the floor | 0.60 m | 0.95 m | 1.10 m |
| Each handle from the centre line | 0.15 m (30 cm apart) | 0.36 m (72 cm apart) | 0.36 m |

Change the limits in `Core/PlayerFit.cs` (`Min*/Max*/Default*`); `StickSpeed` is the stick/key speed. The narrow
limit keeps both knobs ≥12 cm apart at every end stop (`FitTests.LowNarrowFit_HandlesSwingClear`). Below 32.5 cm
spacing the level plaque no longer fits between the handle bases and moves onto the frame's front face.

### How the player changes it
| | Table height | Handle spacing | Reset |
|---|---|---|---|
| Bare hand / controller grip | brass grip on the table edge, move up/down | a handle's wooden base, move sideways | hold the round button 1 s |
| Controller sticks | right stick up/down | left stick up/down (up = wider) | — |
| Desktop | `+`/`-` or PageUp/PageDown | `]` wider, `[` narrower | Home |

### Behaviour
- Board, maze, frame, tray, table top, handles, plaque, message, ball and fit controls move together; the legs
  stretch to the floor. Board size, ball physics and the handles' travel directions are unchanged; knobs, grab zones
  (distance to the knob) and bases follow the handles.
- Any fit change releases both handles in place (their values, and so the board's tilt, stay). A hand that was
  holding must open before it can grab again (`HandGrip.Holding`; a grip only moves a lever it grabbed itself, so
  two hands can never drag the shared table-height control at once).
- The fit is kept through restart, level change and recentering (`Place` keeps the table height above the floor).

### Evidence
- **Automated:** EditMode 84/84 (8 new in `PlayerFitTests`: defaults, clamping, store/load/clear, 1:1 drag with slip,
  nearest-target gating, shared lever ownership, release from outside needs a fresh press). PlayMode 52 passed,
  4 skipped renders, of 56 (12 new in `FitTests`: default layout; lowering moves every part by the same amount and
  the legs still reach the floor, the ball still rolls the way the handle points; narrow spacing is symmetric with
  unchanged travel axes and board; at 0.60 m / 0.15 m both handles at every end stop stay clear of frame, plaque,
  bases and each other; grab zones follow; adjusting while held lets go with no board jolt and no regrab; kept through
  restart/level/recenter; save/load/reset; bare hand lowers the table, narrows the handles, reset needs a 1 s hold,
  two hands cannot both hold the table height).
- **Off-headset renders** (`Snapshots_Fit`): default from an adult's eyes (1.62 m) unchanged; 0.70 m table / 30 cm
  apart from a child's eyes (1.20 m): whole board and ball clearly visible, knobs low in view not covering the maze,
  both knobs above and clear of the frame, plaque on the frame face under the bases, right-hand markers on the moved
  knob, brass grips and reset button on the table edge, legs on the floor. A first pass had the low-table cameras
  25 cm too low (the helper added the root's height twice); fixed before judging the images.
- **Build:** OK, 25 MB, `UnityProject/Builds/reference/LabyrinthXR-fit.apk`.
- **Headset:** not yet.

## 2. Left bare hand

- The left hand reuses the right hand's path unchanged (`XrHands.TryGet(left)`, `HandSample.FromHand`, `HandPinch`,
  `HandDriver`/`HandGrip`): pinch near the left knob and move the hand forward/back for the pitch handle. Each hand
  only works its own handle; both work at once; a tracked controller wins per hand. Debug markers and `[Hands] left:`
  log lines like the right hand's. The hint now says "grip buttons or a pinch".
- One hand losing tracking releases only its handle (in place); the other keeps steering. A hand that returns pinched
  must open and pinch again (`HandGrip`, as for the right hand).

### Evidence
- **Automated:** EditMode 84/84; PlayMode 58 passed, 4 skipped renders, of 62 (6 new in `TwoHandTests`: left pinch
  pitches forward/back and leaves roll alone; each hand only its own handle and axis; both at once tilt both axes and
  the ball rolls the diagonal; left tracking lost while both hold → right keeps steering, left returns pinched → no
  grab, no jump, re-pinch grabs without a jump; both hands at the 0.70 m / 30 cm fit; Touch controllers grab and move
  both handles at the default and a custom fit).
- **Render** (`Snapshots_Fit`, both hands' markers at 0.70 m / 30 cm): thumb/index tips at each knob's player-side
  surface, palms behind and outside each knob; the knobs ~30 cm apart with room for both hands.
- **Build:** OK, 25 MB, `UnityProject/Builds/reference/LabyrinthXR-fit-twohands.apk`.
- **Headset:** not yet.

## Headset session 1 (Quest 3, 2026-11-20 11:12–11:14, wifi ADB; device log, player)
- Fit changed while controllers were active (spacing 72 → 30 cm, table 62–90 cm, saved 11:12:41–11:13:46; five
  saves in 4 s point to the stick); last fit 62 cm / 30 cm. Four resets in two pairs 2–3 s apart (not yet known
  whether deliberate). **Not tried: adjusting with bare hands** (no `[Fit]` line while hands were the source).
- Both bare hands held their handles together for 37 s at 62 cm / 30 cm, several re-pinches, no unintended drop;
  one still second: palm SD 1.8 / 2.0 mm; corrections 3–25 mm. Controllers grabbed both knobs at default and custom fit.
- Not shown: an adjustment while a handle is held, tracking loss while held, the fit after an app restart, Viola.

### Verification log: "class 'CapsuleCollider' doesn't exist" (92 error lines at a level change, 11:13:10)
1. Why the error? `MazeBuilder` builds the next level at runtime with `GameObject.CreatePrimitive(Cylinder)`, which
   adds a `CapsuleCollider`, and the class was not in the player.
2. Why not in the player? IL2CPP/engine stripping keeps only engine classes the scenes or code reference; the scene
   has no `CapsuleCollider` (`BoardBuilder` removes it from every cylinder at edit time).
3. Why did it not break the game? The primitive is still created without the collider, which the builder would have
   removed anyway; the level loaded and played.
4. When did it start? With the first Quest build: the Oculus reference APK (`0f59e8f`) already lacks the type in its
   IL2CPP metadata (as does every later APK), while Box/Sphere/MeshCollider are present.
5. Why not caught earlier? Editor and tests never strip; earlier device logs were captured without a level change
   (or only the last ~2 minutes from the device buffer), and the game kept working.
- **Fix:** `Assets/link.xml` preserves the four collider classes. **Guard:** `scripts/labyrinth.sh build` (and
  `check-apk`) fails when one of them is missing from the APK's metadata; it failed on the previous APK and passes now.
- **Logging:** `[Fit]` lines now name who grabs the table height or the handle spacing (hand and source), the release
  (and tracking loss), stick adjustments, reset (by which hand), and when an adjustment releases a held handle (with
  its value), so the next headset session can show these cases directly.
- Tests 84 / 58 (+4 skipped renders); APK `UnityProject/Builds/reference/LabyrinthXR-fit-twohands-2.apk`.

## Headset session 2 (Quest 3, 2026-11-20 11:26–11:28, wifi ADB; device log)
- **Saved fit after an app restart:** `[Fit] loaded: table 62 cm, handles 30 cm apart` at start.
- **Bare-hand fit:** the right hand dragged the table height 62 → 108, 95 → 110 and 110 → 61 cm (≈1.5–2 s per drag)
  and the handle spacing 30 → 53 and 72 → 43 cm apart; **reset** by holding the button 1 s with the right hand.
- **Adjustment while a handle is held:** 11:26:51 the left hand held the pitch handle when the right hand took a
  handle base: "Hand adjustment released the pitch handle at -1.00"; no regrab by the still-pinched left hand.
- **Tracking loss while held:** 11:27:54 the left controller lost tracking while holding (released, handle kept at
  -0.90); the next grab only came with a fresh press (11:28:04).
- Controllers worked both handles at the custom fit. Last fit 61 cm / 43 cm apart. No errors; no `CapsuleCollider`
  lines, but no level change is visible in this log (levels were not logged), so the stripping fix is confirmed only
  by the APK check so far.
- **Log fix:** at 11:26:44 the right hand, still pinching the roll handle (dragged to its end stop), held the reset
  button; the reset released the handle correctly but the hand log reported "grip opened, tips 1.3 cm". Hand logs now
  skip releases made by a fit change (LabyrinthGame logs those), and level loads are logged (`[Level] loaded n/5`).
- Tests 84 / 58 (+4 skipped renders); APK `UnityProject/Builds/reference/LabyrinthXR-fit-twohands-3.apk`.
- **Not yet:** Viola.
