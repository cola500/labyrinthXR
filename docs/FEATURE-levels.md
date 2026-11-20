# FEATURE — Five progressive labyrinth levels

Status: implemented and verified off-headset ([T] automated tests, [R] renders inspected by Claude, [B] build).
**Not yet played on the headset ([H]).** Builds on EPIC-001; physics, controls and presentation are unchanged.

## Design

All levels share the board (1.10 × 0.88 m, 10 × 8 cells of 11 cm), the 56 mm ball, 88 mm holes, wall size, tilt
limits, handle mapping and physics. Difficulty comes from geometry only.

| # | Name | Holes | Route | Narrow route* | Intended challenge |
|---|---|---|---|---|---|
| 1 | First Steps | 4 | 1.9 m | 0.00 m | Learn to tilt: two wide lanes, one wide turn; holes kept to the edges, away from the line. |
| 2 | Finding Balance | 9 | 4.0 m | 0.94 m | The original EPIC-001 maze: four two-cell lanes, one hole per lane side to weave past, holes in the turns. |
| 3 | Steady Hands | 8 | 4.7 m | 1.53 m | One-cell gates between lanes and one squeeze per lane, with a hole just past each squeeze: precise, slow movement. |
| 4 | The Gauntlet | 11 | 4.4 m | 2.02 m | One-way spiral: outer ring with slaloms and holes on the outside of corners, a one-cell entrance above a pocket hole, a one-cell inner ring, the goal beside a hole. Both axes, all four directions. |
| 5 | Master of the Maze | 13 | 4.9 m | 2.25 m | Five vertical lanes (mostly forward/back control, new after four mostly left/right levels) joined by one-cell gates; staggered hole pairs to weave through; two squeezes; 25 direction changes. |

\* route length where the ball has less than 25 mm to spare to the nearest wall (`MazeAnalysis.NarrowRouteLength`).
These are design measures; **no playtest has measured human difficulty yet.** Autopilot completion times (a perfect
handles-only controller, not a person): 9.7 / 21.7 / 25.8 / 24.0 / 28.0 s.

**Level 1 decision:** the existing maze (9 holes, slalom in every lane) became Level 2, unchanged, because it suits
"Finding Balance" better than "First Steps"; a gentler Level 1 was added.

## Architecture

- `Core/Levels.cs` — the five `MazeLayout`s as data (number, name, start, goal, walls, holes, route).
- `Core/Campaign.cs` — level index; a won level advances after `AdvanceSeconds` (2.5 s); Level 5 stays won (completion).
- `Game/MazeBuilder.cs` — builds one level's maze (floor with holes, walls, markings) under the board; `MazeMaterials`
  are serialized on `LabyrinthGame` so runtime builds have them in the APK.
- `LabyrinthGame.LoadLevel(i)` — deactivates the old maze (its colliders leave physics at once), destroys it and its
  generated meshes, builds the new one, resets the round and puts the ball on the start. Ball, board, handles and
  physics are untouched. The scene holds Level 1 (baked by `SceneSetup`).
- Feedback: walnut plaque on the frame's front, `Level 3 / 5 — Steady Hands`; messages for level complete (with the
  next level's name) and campaign complete.
- Input: A/X restarts the current level (after Level 5: from Level 1); hold B/Y 1.5 s → Level 1. Desktop: R, 1/Backspace.

## Verification

| Criterion | Evidence |
|---|---|
| AC-01 levels available | [T] `GameStarts_AtLevelOne`, `EachLevel_LoadsCleanly` ×5, `AllFiveLevels_ThroughNormalProgression`; [R] L1–L5 overview + player view |
| AC-02 increasing difficulty | [T] `NarrowPassages_IncreaseFromLevelToLevel` (design measure only) — **[H] playtest pending** |
| AC-03 progression | [T] `Goal_AdvancesToTheNextLevel_AfterThePause` (exactly one transition, ball on new start, plaque); [R] L1-c, L2-c |
| AC-04 retry on Level 4 | [T] `FallOnLevelFour_StaysOnLevelFour`, `Restart_DuringPlay_RetriesTheCurrentLevel` |
| AC-05 final completion | [T] `LevelFive_Completion_ThenRestartFromLevelOne`; [R] L5-c |
| AC-06 consistent controls | [T] `PhysicsAndControls_AreTheSameOnEveryLevel`, `EveryLevel_SharesBoardBallAndHoleDimensions` |
| AC-07 clean transitions | [T] `EachLevel_LoadsCleanly`: exact collider count, one floor, one maze, no old maze objects after the frame |
| Navigable | [T] per level: route clearance, ball-sized grid path search, every hole swallows the ball, autopilot completes it with handles only |
| Geometry | [T] per level: start/goal clear of walls and holes, holes clear of walls, hole tiles valid, floor mesh has exactly its holes |
| Build | [B] Quest APK 23 MB |

Totals: 67 EditMode + 35 PlayMode tests pass; 2 explicit Snapshots render tests pass; no errors in the logs.

### Visual review [R]
Rendered from the real scene through `LoadLevel`: each level from above and from the player's position, the plaque,
level complete, the transition, and campaign complete (`UnityProject/Logs/shots/L*.png`).
- Found: the level plaque's dark lettering on light wood rendered faint and unreadable → walnut plaque with cream
  lettering, widened to 48 cm for the longest name; re-rendered and readable.
- Checked, not a defect: Level 4's goal looks bright from straight above (brass ring's specular highlight under the
  lamp); from the player's position it looks like every other goal.
- No overlapping or floating geometry found; holes next to walls keep the same 3 mm lip as the original maze.

## Known limitations
- Nothing of this feature has been played on the headset; human difficulty and the 2.5 s pause length are untested.
- The hint text's slight slant in oblique renders (EPIC-001) is still there; check in the headset.
- No saved progress between sessions (out of scope).
