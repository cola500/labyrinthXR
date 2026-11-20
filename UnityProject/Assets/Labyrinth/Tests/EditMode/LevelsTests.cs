// The five level definitions: identity, geometry rules, a ball-sized path from start to goal, and the intended
// difficulty progression (by geometry; not a measure of how hard a person finds it).
using System.Collections.Generic;
using System.Linq;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;

namespace Labyrinth.Tests
{
    public class LevelsTests
    {
        static IEnumerable<int> All() => Enumerable.Range(0, Levels.Count);

        [Test]
        public void ExactlyFiveLevels_NumberedInOrder_WithUniqueNames()
        {
            Assert.AreEqual(5, Levels.Count);
            var names = new HashSet<string>();
            for (var i = 0; i < Levels.Count; i++)
            {
                var m = Levels.Create(i);
                Assert.AreEqual(i + 1, m.Number);
                Assert.IsTrue(names.Add(m.Name), $"duplicate name {m.Name}");
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Levels.Create(5));
            CollectionAssert.AreEqual(new[] { "First Steps", "Finding Balance", "Steady Hands", "The Gauntlet", "Master of the Maze" },
                All().Select(i => Levels.Create(i).Name).ToArray());
        }

        [Test]
        public void Layouts_AreUnique()
        {
            var signatures = All().Select(i =>
            {
                var m = Levels.Create(i);
                return string.Join(";", m.Walls.Select(w => $"{w.A}{w.B}")) + "|" + string.Join(";", m.Holes) + "|" + m.Start + m.Goal;
            }).ToList();
            Assert.AreEqual(signatures.Count, signatures.Distinct().Count());
        }

        // Board, ball, holes and walls are the same size on every level: difficulty comes from the geometry only.
        [Test]
        public void EveryLevel_SharesBoardBallAndHoleDimensions()
        {
            var a = Levels.Create(0);
            foreach (var i in All())
            {
                var m = Levels.Create(i);
                Assert.AreEqual((a.Columns, a.Rows, a.Cell, a.BallRadius, a.HoleRadius, a.WallThickness, a.WallHeight, a.GoalRadius),
                                (m.Columns, m.Rows, m.Cell, m.BallRadius, m.HoleRadius, m.WallThickness, m.WallHeight, m.GoalRadius), m.Name);
            }
        }

        [TestCaseSource(nameof(All))]
        public void HasEnoughHoles_AndWalls(int i)
        {
            var m = Levels.Create(i);
            Assert.GreaterOrEqual(m.Holes.Count, 3);
            Assert.GreaterOrEqual(m.Walls.Count, 1);
        }

        // A hole may sit right next to a wall (as on a wooden labyrinth) but never cut under it.
        [TestCaseSource(nameof(All))]
        public void HolesAreClearOfWalls(int i)
        {
            var m = Levels.Create(i);
            foreach (var h in m.Holes)
                Assert.Greater(m.DistanceToWalls(h) - m.HoleRadius, 0.002f, $"{m.Name}: hole {h} touches a wall");
        }

        // Each hole gets its own square floor tile; the tiles must not overlap or leave the board.
        [TestCaseSource(nameof(All))]
        public void HoleTiles_DoNotOverlap_AndStayOnTheBoard(int i)
        {
            var m = Levels.Create(i);
            var half = FloorMesh.TileSize(m) / 2f;
            for (var a = 0; a < m.Holes.Count; a++)
            {
                var p = m.ToBoard(m.Holes[a]);
                Assert.That(Mathf.Abs(p.x) + half, Is.LessThan(m.Width / 2), $"{m.Name}: hole {a}");
                Assert.That(Mathf.Abs(p.z) + half, Is.LessThan(m.Depth / 2), $"{m.Name}: hole {a}");
                for (var b = a + 1; b < m.Holes.Count; b++)
                {
                    var q = m.ToBoard(m.Holes[b]);
                    Assert.That(Mathf.Abs(p.x - q.x) >= 2 * half || Mathf.Abs(p.z - q.z) >= 2 * half, $"{m.Name}: holes {a} and {b}");
                }
            }
        }

        // The ball never spawns inside a wall or next to a hole; the goal is reachable without touching either.
        [TestCaseSource(nameof(All))]
        public void StartAndGoal_AreSafeAndInside(int i)
        {
            var m = Levels.Create(i);
            foreach (var p in new[] { m.Start, m.Goal })
            {
                Assert.Greater(m.DistanceToWalls(p), m.BallRadius, $"{m.Name}: {p} inside a wall");
                Assert.Greater(m.DistanceToHoles(p), m.BallRadius, $"{m.Name}: {p} next to a hole");
            }
            Assert.Greater(Vector2.Distance(m.Start, m.Goal) * m.Cell, 2 * m.GoalRadius, $"{m.Name}: start inside the goal");
        }

        // The drawn route keeps the ball's centre off every hole and the ball clear of every wall.
        [TestCaseSource(nameof(All))]
        public void Route_IsClearOfHolesAndWalls(int i)
        {
            var m = Levels.Create(i);
            Assert.AreEqual(m.Start, m.Route[0]);
            Assert.AreEqual(m.Goal, m.Route[m.Route.Count - 1]);
            for (var s = 0; s + 1 < m.Route.Count; s++)
                for (var t = 0f; t <= 1f; t += 0.02f)
                {
                    var p = Vector2.Lerp(m.Route[s], m.Route[s + 1], t);
                    Assert.Greater(m.DistanceToHoles(p), 0.01f, $"{m.Name}: segment {s} at {p} crosses a hole");
                    Assert.Greater(m.DistanceToWalls(p), m.BallRadius + 0.005f, $"{m.Name}: segment {s} at {p} hits a wall");
                }
        }

        // Independent of the drawn route: a grid search over positions of the ball's centre (5.5 mm steps) that keep
        // the ball off every wall and its centre off every hole finds a way from start to goal.
        [TestCaseSource(nameof(All))]
        public void BallSizedPath_ExistsFromStartToGoal(int i)
        {
            var m = Levels.Create(i);
            Assert.IsTrue(MazeAnalysis.HasBallPath(m), $"{m.Name}: no ball-sized path");
        }

        // The floor mesh of every level has exactly its holes (board area minus one hole polygon per hole).
        [TestCaseSource(nameof(All))]
        public void FloorMesh_HasEveryHole(int i)
        {
            var m = Levels.Create(i);
            var (visual, collider) = FloorMesh.Build(m);
            var polygon = 0.5f * FloorMesh.CircleSegments * m.HoleRadius * m.HoleRadius * Mathf.Sin(2f * Mathf.PI / FloorMesh.CircleSegments);
            Assert.AreEqual(m.Width * m.Depth - m.Holes.Count * polygon, FloorMeshTests.TopArea(collider, 0, out var up), 1e-4f, m.Name);
            Assert.IsTrue(up, $"{m.Name}: all top faces up");
            Assert.AreEqual(m.Holes.Count * FloorMesh.CircleSegments * 6, visual.GetTriangles(1).Length, m.Name);
            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(collider);
        }

        // The intended progression, by geometry: each level asks for more travel through narrow passages (less than
        // 25 mm to spare on a side) than the one before. A design check only; how hard a person finds each level needs
        // playtesting.
        [Test]
        public void NarrowPassages_IncreaseFromLevelToLevel()
        {
            var narrow = All().Select(i => MazeAnalysis.NarrowRouteLength(Levels.Create(i))).ToArray();
            Debug.Log("[levels] narrow route length per level (m): " + string.Join(", ", narrow.Select(n => n.ToString("0.00"))));
            for (var i = 1; i < narrow.Length; i++)
                Assert.Greater(narrow[i], narrow[i - 1], $"level {i + 1} vs {i}");
        }
    }

    public static class MazeAnalysis
    {
        public static bool HasBallPath(MazeLayout m, float step = 0.05f)
        {
            int nx = Mathf.FloorToInt(m.Columns / step), ny = Mathf.FloorToInt(m.Rows / step);
            var free = new sbyte[nx, ny]; // 0 unknown, 1 free, -1 blocked
            bool Free(int x, int y)
            {
                if (free[x, y] == 0)
                {
                    var p = new Vector2((x + 0.5f) * step, (y + 0.5f) * step);
                    free[x, y] = (sbyte)(m.DistanceToWalls(p) > m.BallRadius + 0.002f && m.DistanceToHoles(p) > 0.004f ? 1 : -1);
                }
                return free[x, y] > 0;
            }
            (int, int) Cell(Vector2 p) => (Mathf.Min(nx - 1, (int)(p.x / step)), Mathf.Min(ny - 1, (int)(p.y / step)));
            var start = Cell(m.Start);
            var goal = Cell(m.Goal);
            var seen = new HashSet<(int, int)> { start };
            var queue = new Queue<(int, int)>();
            queue.Enqueue(start);
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == goal) return true;
                foreach (var (dx, dy) in dirs)
                {
                    var n = (c.Item1 + dx, c.Item2 + dy);
                    if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= nx || n.Item2 >= ny || seen.Contains(n) || !Free(n.Item1, n.Item2)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        // Route length (m) where the ball has less than 25 mm to spare to the nearest wall.
        public static float NarrowRouteLength(MazeLayout m)
        {
            var total = 0f;
            for (var s = 0; s + 1 < m.Route.Count; s++)
            {
                const int n = 40;
                var d = Vector2.Distance(m.Route[s], m.Route[s + 1]) * m.Cell / n;
                for (var k = 0; k < n; k++)
                {
                    var p = Vector2.Lerp(m.Route[s], m.Route[s + 1], (k + 0.5f) / n);
                    if (m.DistanceToWalls(p) - m.BallRadius < 0.025f) total += d;
                }
            }
            return total;
        }
    }
}
