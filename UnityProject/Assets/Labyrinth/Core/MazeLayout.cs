// The maze as data. Authored on a grid of cells (x to the right, y away from the player, cell (0,0) at the near-left
// corner); ToBoard converts to board space: metres, origin at the board centre, x right, z away from the player, the
// floor's top surface at y = 0. Walls are axis-aligned segments on grid lines (the outer rim is added by the builder);
// holes, start and goal are points in cell units. Route is the intended line through the maze (drawn on the board
// like the line of a wooden labyrinth, and followed by the autopilot test that proves the maze can be completed).
using System.Collections.Generic;
using UnityEngine;

namespace Labyrinth.Core
{
    public readonly struct WallSegment
    {
        public readonly Vector2 A, B; // cell units, axis-aligned
        public WallSegment(float ax, float ay, float bx, float by) { A = new Vector2(ax, ay); B = new Vector2(bx, by); }
    }

    public sealed class MazeLayout
    {
        public int Columns = 10;
        public int Rows = 8;
        public float Cell = 0.11f;            // metres per cell: 10 x 8 cells = 1.10 x 0.88 m
        public float BallRadius = 0.028f;     // a 56 mm steel ball
        public float HoleRadius = 0.044f;     // must exceed BallRadius, or the ball cannot drop through
        public float WallThickness = 0.016f;
        public float WallHeight = 0.035f;
        public float GoalRadius = 0.05f;      // the ball's centre within this distance of Goal = success

        public readonly List<WallSegment> Walls = new();
        public readonly List<Vector2> Holes = new();
        public readonly List<Vector2> Route = new();
        public Vector2 Start;
        public Vector2 Goal;

        public float Width => Columns * Cell;
        public float Depth => Rows * Cell;

        public Vector3 ToBoard(Vector2 cell) => new((cell.x - Columns * 0.5f) * Cell, 0f, (cell.y - Rows * 0.5f) * Cell);
        public Vector2 ToCell(Vector3 board) => new(board.x / Cell + Columns * 0.5f, board.z / Cell + Rows * 0.5f);

        // Interior walls plus the four rim walls, all in cell units.
        public IEnumerable<WallSegment> AllWalls()
        {
            foreach (var w in Walls) yield return w;
            yield return new WallSegment(0, 0, Columns, 0);
            yield return new WallSegment(0, Rows, Columns, Rows);
            yield return new WallSegment(0, 0, 0, Rows);
            yield return new WallSegment(Columns, 0, Columns, Rows);
        }

        // Shortest distance (metres) from a board-space point (x, z) to a wall's surface (not its centre line).
        public float DistanceToWalls(Vector2 cellPoint)
        {
            var best = float.MaxValue;
            foreach (var w in AllWalls())
                best = Mathf.Min(best, SegmentDistance(cellPoint, w.A, w.B) * Cell - WallThickness * 0.5f);
            return best;
        }

        // Shortest distance (metres) from a point to the nearest hole's edge.
        public float DistanceToHoles(Vector2 cellPoint)
        {
            var best = float.MaxValue;
            foreach (var h in Holes) best = Mathf.Min(best, Vector2.Distance(cellPoint, h) * Cell - HoleRadius);
            return best;
        }

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + t * ab);
        }

        // The first level: a four-lane serpentine. Lanes are two cells deep, so each hole sits on one side of its lane
        // and the ball must weave past it; a hole in each turn punishes overshooting.
        public static MazeLayout First()
        {
            var m = new MazeLayout { Start = new Vector2(0.6f, 1.0f), Goal = new Vector2(0.75f, 7.0f) };
            // Lane separators with alternating gaps (right, left, right).
            m.Walls.Add(new WallSegment(0, 2, 8, 2));
            m.Walls.Add(new WallSegment(2, 4, 10, 4));
            m.Walls.Add(new WallSegment(0, 6, 8, 6));
            // Short baffles that narrow the lanes and make the turns deliberate.
            m.Walls.Add(new WallSegment(6, 0, 6, 0.7f));
            m.Walls.Add(new WallSegment(3, 4, 3, 4.7f));
            m.Walls.Add(new WallSegment(5, 8, 5, 7.3f));

            // Lane 0 (near, left to right)
            m.Holes.Add(new Vector2(3.5f, 0.5f));
            m.Holes.Add(new Vector2(5.0f, 1.5f));
            m.Holes.Add(new Vector2(9.5f, 0.5f));
            // Lane 1 (right to left)
            m.Holes.Add(new Vector2(7.5f, 3.5f));
            m.Holes.Add(new Vector2(4.5f, 2.5f));
            m.Holes.Add(new Vector2(0.5f, 2.5f));
            // Lane 2 (left to right)
            m.Holes.Add(new Vector2(5.0f, 5.5f));
            m.Holes.Add(new Vector2(7.0f, 4.5f));
            // Lane 3 (right to left, to the goal)
            m.Holes.Add(new Vector2(3.5f, 6.5f));

            m.Route.AddRange(new[]
            {
                m.Start,
                new Vector2(2.2f, 1.45f), new Vector2(4.2f, 1.4f), new Vector2(5.0f, 0.55f), new Vector2(6.0f, 1.25f),
                new Vector2(7.3f, 1.0f), new Vector2(8.6f, 1.2f), new Vector2(8.8f, 2.5f),
                new Vector2(6.5f, 2.55f), new Vector2(5.5f, 3.45f), new Vector2(2.5f, 3.45f), new Vector2(1.3f, 3.5f),
                new Vector2(1.3f, 4.5f), new Vector2(3.0f, 5.35f), new Vector2(4.2f, 4.55f), new Vector2(5.2f, 4.55f),
                new Vector2(6.2f, 5.35f), new Vector2(7.8f, 5.45f), new Vector2(8.9f, 5.6f), new Vector2(8.9f, 6.7f),
                new Vector2(6.0f, 6.8f), new Vector2(4.8f, 6.75f), new Vector2(3.5f, 7.45f), new Vector2(2.2f, 7.1f),
                m.Goal,
            });
            return m;
        }
    }
}
