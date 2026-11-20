// One level's maze as data (the five levels are in Levels). Authored on a grid of cells (x to the right, y away from the player, cell (0,0) at the near-left
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

        public int Number;                    // 1-based position in the campaign
        public string Name = "";

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
    }
}
