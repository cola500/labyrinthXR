// The floor mesh has every hole of the layout (a hole missing from the mesh is missing for the ball too: seen in the
// first visual review, when grid lines from neighbouring holes split a hole's tile and it was filled with plain floor).
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;

namespace Labyrinth.Tests
{
    public class FloorMeshTests
    {
        static readonly MazeLayout M = Levels.Create(1); // the classic maze (closely spaced holes)

        public static float TopArea(Mesh mesh, int submesh, out bool allUp)
        {
            var v = mesh.vertices;
            var t = mesh.GetTriangles(submesh);
            var area = 0f;
            allUp = true;
            for (var i = 0; i < t.Length; i += 3)
            {
                var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                area += n.magnitude / 2f;
                if (n.y <= 0f && n.magnitude > 1e-9f) allUp = false;
            }
            return area;
        }

        [Test]
        public void TopSurface_IsTheBoardMinusEveryHole_AndFacesUp()
        {
            var (visual, collider) = FloorMesh.Build(M);
            var polygon = 0.5f * FloorMesh.CircleSegments * M.HoleRadius * M.HoleRadius * Mathf.Sin(2f * Mathf.PI / FloorMesh.CircleSegments);
            var expected = M.Width * M.Depth - M.Holes.Count * polygon;
            foreach (var mesh in new[] { visual, collider })
            {
                var area = TopArea(mesh, 0, out var up);
                Assert.AreEqual(expected, area, 1e-4f, $"{mesh.name}: area (one missing hole = {polygon:0.0000} m²)");
                Assert.IsTrue(up, $"{mesh.name}: every top triangle faces up");
            }
        }

        [Test]
        public void EveryHole_HasItsWall()
        {
            var (visual, _) = FloorMesh.Build(M);
            Assert.AreEqual(M.Holes.Count * FloorMesh.CircleSegments * 2 * 3, visual.GetTriangles(1).Length);
        }

        // No point of the surface lies inside a hole's circle (sampled at each hole's centre region via triangle tests).
        [Test]
        public void NoTriangleCoversAHoleCentre()
        {
            var (_, collider) = FloorMesh.Build(M);
            var v = collider.vertices;
            var t = collider.triangles;
            foreach (var h in M.Holes)
            {
                var c = M.ToBoard(h);
                var p = new Vector2(c.x, c.z);
                for (var i = 0; i < t.Length; i += 3)
                    Assert.IsFalse(Inside(p, v[t[i]], v[t[i + 1]], v[t[i + 2]]), $"hole {h} is covered");
            }
        }

        static bool Inside(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float S(Vector2 p1, Vector3 p2, Vector3 p3) => (p1.x - p3.x) * (p2.z - p3.z) - (p2.x - p3.x) * (p1.y - p3.z);
            var d1 = S(p, a, b);
            var d2 = S(p, b, c);
            var d3 = S(p, c, a);
            var neg = d1 < 0 || d2 < 0 || d3 < 0;
            var pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
