// The five levels, in order. Authored on the 10 x 8 grid of MazeLayout (cell units). Every level uses the same board,
// ball, holes and physics; difficulty comes from the geometry only (see docs/FEATURE-levels.md). The designs were
// checked for clearance and a ball-sized path before they were added; LevelsTests and the autopilot keep checking them.
using UnityEngine;

namespace Labyrinth.Core
{
    public static class Levels
    {
        public const int Count = 5;

        public static MazeLayout Create(int index) => index switch
        {
            0 => Level1(),
            1 => Level2(),
            2 => Level3(),
            3 => Level4(),
            4 => Level5(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(index)),
        };

        // Learn to tilt: two wide lanes joined by a wide turn; four holes keep to the edges, away from the line.
        static MazeLayout Level1()
        {
            var m = new MazeLayout { Number = 1, Name = "First Steps", Start = new Vector2(1f, 2f), Goal = new Vector2(1.2f, 6f) };
            m.Walls.Add(new WallSegment(0f, 4f, 7f, 4f));
            m.Holes.AddRange(new[] { new Vector2(5f, 0.5f), new Vector2(9.5f, 0.5f), new Vector2(9.5f, 7.5f), new Vector2(4.5f, 7.5f) });
            m.Route.AddRange(new[]
            {
                new Vector2(1f, 2f), new Vector2(4f, 2f), new Vector2(7.5f, 2.2f), new Vector2(8.5f, 3f),
                new Vector2(8.5f, 5f), new Vector2(7.5f, 6f), new Vector2(4f, 6f), new Vector2(1.2f, 6f),
            });
            return m;
        }

        // The original maze: four two-cell lanes, a hole on one side of each lane to weave past, holes in the turns.
        static MazeLayout Level2()
        {
            var m = new MazeLayout { Number = 2, Name = "Finding Balance", Start = new Vector2(0.6f, 1f), Goal = new Vector2(0.75f, 7f) };
            m.Walls.Add(new WallSegment(0f, 2f, 8f, 2f));
            m.Walls.Add(new WallSegment(2f, 4f, 10f, 4f));
            m.Walls.Add(new WallSegment(0f, 6f, 8f, 6f));
            m.Walls.Add(new WallSegment(6f, 0f, 6f, 0.7f));
            m.Walls.Add(new WallSegment(3f, 4f, 3f, 4.7f));
            m.Walls.Add(new WallSegment(5f, 8f, 5f, 7.3f));
            m.Holes.AddRange(new[] { new Vector2(3.5f, 0.5f), new Vector2(5f, 1.5f), new Vector2(9.5f, 0.5f), new Vector2(7.5f, 3.5f), new Vector2(4.5f, 2.5f), new Vector2(0.5f, 2.5f), new Vector2(5f, 5.5f), new Vector2(7f, 4.5f), new Vector2(3.5f, 6.5f) });
            m.Route.AddRange(new[]
            {
                new Vector2(0.6f, 1f), new Vector2(2.2f, 1.45f), new Vector2(4.2f, 1.4f), new Vector2(5f, 0.55f),
                new Vector2(6f, 1.25f), new Vector2(7.3f, 1f), new Vector2(8.6f, 1.2f), new Vector2(8.8f, 2.5f),
                new Vector2(6.5f, 2.55f), new Vector2(5.5f, 3.45f), new Vector2(2.5f, 3.45f), new Vector2(1.3f, 3.5f),
                new Vector2(1.3f, 4.5f), new Vector2(3f, 5.35f), new Vector2(4.2f, 4.55f), new Vector2(5.2f, 4.55f),
                new Vector2(6.2f, 5.35f), new Vector2(7.8f, 5.45f), new Vector2(8.9f, 5.6f), new Vector2(8.9f, 6.7f),
                new Vector2(6f, 6.8f), new Vector2(4.8f, 6.75f), new Vector2(3.5f, 7.45f), new Vector2(2.2f, 7.1f),
                new Vector2(0.75f, 7f),
            });
            return m;
        }

        // Narrow gates (one cell) between the lanes and one squeeze per lane, with a hole just past each squeeze.
        static MazeLayout Level3()
        {
            var m = new MazeLayout { Number = 3, Name = "Steady Hands", Start = new Vector2(0.6f, 1.5f), Goal = new Vector2(1.2f, 7.3f) };
            m.Walls.Add(new WallSegment(0f, 2f, 9f, 2f));
            m.Walls.Add(new WallSegment(1f, 4f, 10f, 4f));
            m.Walls.Add(new WallSegment(0f, 6f, 9f, 6f));
            m.Walls.Add(new WallSegment(3f, 1f, 3f, 2f));
            m.Walls.Add(new WallSegment(7f, 2f, 7f, 3f));
            m.Walls.Add(new WallSegment(3f, 4f, 3f, 5f));
            m.Walls.Add(new WallSegment(7f, 6f, 7f, 7f));
            m.Holes.AddRange(new[] { new Vector2(4.6f, 0.5f), new Vector2(7.5f, 1.5f), new Vector2(5.4f, 3.5f), new Vector2(0.5f, 2.5f), new Vector2(4.6f, 5.5f), new Vector2(7.5f, 4.5f), new Vector2(5.4f, 7.5f), new Vector2(2.5f, 6.5f) });
            m.Route.AddRange(new[]
            {
                new Vector2(0.6f, 1.5f), new Vector2(2f, 0.6f), new Vector2(3f, 0.5f), new Vector2(3.6f, 0.55f),
                new Vector2(4.6f, 1.45f), new Vector2(6f, 1.3f), new Vector2(7.5f, 0.55f), new Vector2(8.6f, 0.6f),
                new Vector2(9.5f, 1.2f), new Vector2(9.5f, 2.5f), new Vector2(9.5f, 3.4f), new Vector2(8f, 3.5f),
                new Vector2(7f, 3.5f), new Vector2(6.4f, 3.45f), new Vector2(5.4f, 2.55f), new Vector2(3.5f, 2.6f),
                new Vector2(1.6f, 3.4f), new Vector2(0.5f, 3.6f), new Vector2(0.5f, 4.6f), new Vector2(2f, 5.4f),
                new Vector2(3f, 5.5f), new Vector2(3.6f, 5.45f), new Vector2(4.6f, 4.55f), new Vector2(6f, 4.7f),
                new Vector2(7.5f, 5.45f), new Vector2(8.6f, 5.5f), new Vector2(9.5f, 5.6f), new Vector2(9.5f, 6.6f),
                new Vector2(8.5f, 7.4f), new Vector2(7f, 7.5f), new Vector2(6.4f, 7.45f), new Vector2(5.4f, 6.55f),
                new Vector2(4f, 6.6f), new Vector2(2.5f, 7.45f), new Vector2(1.2f, 7.3f),
            });
            return m;
        }

        // One-way spiral: outer ring with slaloms and holes on the outside of corners, a one-cell entrance above a pocket hole, a one-cell inner ring, the goal beside a hole.
        static MazeLayout Level4()
        {
            var m = new MazeLayout { Number = 4, Name = "The Gauntlet", Start = new Vector2(3f, 1f), Goal = new Vector2(6.3f, 4.1f) };
            m.Walls.Add(new WallSegment(2f, 0f, 2f, 2f));
            m.Walls.Add(new WallSegment(2f, 2f, 8f, 2f));
            m.Walls.Add(new WallSegment(8f, 2f, 8f, 6f));
            m.Walls.Add(new WallSegment(2f, 6f, 8f, 6f));
            m.Walls.Add(new WallSegment(2f, 3f, 2f, 6f));
            m.Walls.Add(new WallSegment(2f, 3f, 7f, 3f));
            m.Walls.Add(new WallSegment(7f, 3f, 7f, 5f));
            m.Walls.Add(new WallSegment(4f, 5f, 7f, 5f));
            m.Walls.Add(new WallSegment(3f, 3f, 3f, 5f));
            m.Holes.AddRange(new[] { new Vector2(1f, 1f), new Vector2(5f, 1.5f), new Vector2(7f, 0.5f), new Vector2(9.5f, 0.5f), new Vector2(8.5f, 4f), new Vector2(9.5f, 7.5f), new Vector2(6.5f, 6.5f), new Vector2(4.5f, 7.5f), new Vector2(0.5f, 4.5f), new Vector2(2.5f, 3.5f), new Vector2(5f, 3.6f) });
            m.Route.AddRange(new[]
            {
                new Vector2(3f, 1f), new Vector2(4.2f, 0.6f), new Vector2(5f, 0.55f), new Vector2(5.9f, 1f),
                new Vector2(7f, 1.45f), new Vector2(8.4f, 1.45f), new Vector2(9.1f, 2.2f), new Vector2(9.45f, 3f),
                new Vector2(9.45f, 5f), new Vector2(9f, 6.4f), new Vector2(8.4f, 7f), new Vector2(7.4f, 7.3f),
                new Vector2(6.5f, 7.45f), new Vector2(5.5f, 7f), new Vector2(4.5f, 6.55f), new Vector2(3.3f, 6.8f),
                new Vector2(1.6f, 7f), new Vector2(1.4f, 6f), new Vector2(1.45f, 4.5f), new Vector2(1.3f, 3f),
                new Vector2(1.6f, 2.5f), new Vector2(2.6f, 2.5f), new Vector2(7.5f, 2.5f), new Vector2(7.5f, 4f),
                new Vector2(7.5f, 5.5f), new Vector2(3.6f, 5.5f), new Vector2(3.55f, 4.5f), new Vector2(5f, 4.5f),
                new Vector2(6.3f, 4.1f),
            });
            return m;
        }

        // Five vertical lanes (mostly forward/back control) joined by one-cell gates; staggered hole pairs to weave through and two squeezes.
        static MazeLayout Level5()
        {
            var m = new MazeLayout { Number = 5, Name = "Master of the Maze", Start = new Vector2(1f, 0.8f), Goal = new Vector2(9f, 7.3f) };
            m.Walls.Add(new WallSegment(2f, 0f, 2f, 7f));
            m.Walls.Add(new WallSegment(4f, 1f, 4f, 8f));
            m.Walls.Add(new WallSegment(6f, 0f, 6f, 7f));
            m.Walls.Add(new WallSegment(8f, 1f, 8f, 8f));
            m.Walls.Add(new WallSegment(4f, 5f, 5f, 5f));
            m.Walls.Add(new WallSegment(7f, 3f, 8f, 3f));
            m.Holes.AddRange(new[] { new Vector2(0.5f, 2.5f), new Vector2(1.5f, 3.7f), new Vector2(3.5f, 6f), new Vector2(2.5f, 4.8f), new Vector2(3.5f, 3.4f), new Vector2(5.5f, 3.8f), new Vector2(4.5f, 7.5f), new Vector2(6.5f, 5.3f), new Vector2(6.5f, 2f), new Vector2(9.5f, 0.5f), new Vector2(8.5f, 2.9f), new Vector2(9.5f, 4.3f), new Vector2(8.5f, 5.7f) });
            m.Route.AddRange(new[]
            {
                new Vector2(1f, 0.8f), new Vector2(1.45f, 1.8f), new Vector2(1.4f, 2.7f), new Vector2(0.5f, 3.6f),
                new Vector2(0.6f, 4.4f), new Vector2(1.5f, 5f), new Vector2(1.5f, 6.3f), new Vector2(1.4f, 7.5f),
                new Vector2(2.5f, 7.5f), new Vector2(3.1f, 7.1f), new Vector2(2.5f, 6.1f), new Vector2(2.6f, 5.9f),
                new Vector2(3.5f, 5f), new Vector2(3.4f, 4.6f), new Vector2(2.5f, 3.7f), new Vector2(2.5f, 2.5f),
                new Vector2(3f, 1.2f), new Vector2(3.4f, 0.5f), new Vector2(4.5f, 0.5f), new Vector2(4.5f, 2.5f),
                new Vector2(4.5f, 4.3f), new Vector2(5.5f, 4.55f), new Vector2(5.55f, 5.5f), new Vector2(5.5f, 7f),
                new Vector2(5.6f, 7.5f), new Vector2(6.5f, 7.5f), new Vector2(6.9f, 6.8f), new Vector2(7.5f, 5.8f),
                new Vector2(7.5f, 4f), new Vector2(6.5f, 3.3f), new Vector2(6.5f, 2.9f), new Vector2(7.5f, 2f),
                new Vector2(7.5f, 1.2f), new Vector2(7.6f, 0.5f), new Vector2(8.6f, 0.6f), new Vector2(9.4f, 1.4f),
                new Vector2(9.45f, 2.4f), new Vector2(9.4f, 3.2f), new Vector2(8.6f, 3.9f), new Vector2(8.55f, 4.6f),
                new Vector2(9.4f, 5.4f), new Vector2(9.45f, 6.4f), new Vector2(9f, 7.3f),
            });
            return m;
        }
    }
}
