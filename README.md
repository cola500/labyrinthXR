# LabyrinthXR

A cozy VR wooden tilt-maze for Meta Quest 3. Tilt the board with two handles and guide the steel ball past the holes
to the green goal, through five levels: First Steps, Finding Balance, Steady Hands, The Gauntlet, Master of the Maze.

## Play on the Quest

1. Build: `scripts/labyrinth.sh build` (close the Unity Editor first) → `UnityProject/Builds/LabyrinthXR-quest.apk`.
2. Connect the headset over USB (developer mode, allow USB debugging), then `scripts/labyrinth.sh install` and
   `scripts/labyrinth.sh launch`.
3. Stand up; the labyrinth appears ~0.8 m in front of you at table height. Hold the Meta button to recenter (the
   labyrinth moves in front of you again).

Controls:
- **Left hand** – reach for the left red knob and squeeze **grip**: push away / pull back tilts the board forward / back.
- **Right hand** – the right knob: push right / left tilts the board right / left.
- A released handle stays where you left it. Both hands work at the same time.
- Reaching the goal completes the level; after ~2.5 s the next one loads. Falling restarts the current level.
- **A** or **X** – restart the current level (after Level 5: play again from Level 1).
- **Hold B or Y** (1.5 s) – back to Level 1.
- The current level is shown on the walnut plaque on the front of the frame.

## Play in the Unity Editor (no headset)

Open `UnityProject` in Unity 6000.4.7f1, open `Assets/Scenes/Labyrinth.unity`, press Play.
**W/S** (or Up/Down) tilt forward/back, **A/D** (or Left/Right) tilt left/right while held, **R** restarts the level
(after Level 5: from Level 1), **1** or **Backspace** goes back to Level 1.

## Development

See `CLAUDE.md` for commands (tests, snapshots, scene generation) and architecture, and `docs/EPIC-001.md` for the
scope, verification evidence and known limitations; `docs/FEATURE-levels.md` for the five levels.
