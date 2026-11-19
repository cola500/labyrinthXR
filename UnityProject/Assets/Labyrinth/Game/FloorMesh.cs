// The board's floor as one procedural mesh with round holes. The floor is cut into a rectilinear grid whose lines are
// the board edges and the edges (and quarter lines) of a square tile round every hole; plain cells are quads, each
// tile is a square with a circular cut-out whose outline holds every grid point on its edges, so neighbouring cells
// share vertices (no T-junctions: the ball rolls over the seams of the single MeshCollider without bumps).
// Visual mesh: submesh 0 = top surface, submesh 1 = the hole walls going down (dark). Collider mesh: the top surface.
using System.Collections.Generic;
using System.Linq;
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    public static class FloorMesh
    {
        public const int CircleSegments = 32;  // divisible by 4: one circle vertex faces each tile edge's middle
        public const float HoleDepth = 0.022f; // the board's thickness
        public const float UvScale = 0.5f;     // metres per texture repeat

        public static float TileSize(MazeLayout m) => 2f * m.HoleRadius + 0.018f;

        public static (Mesh visual, Mesh collider) Build(MazeLayout m)
        {
            var tile = TileSize(m);
            var holes = m.Holes.Select(h => m.ToBoard(h)).Select(p => new Vector2(p.x, p.z)).ToList();
            var xs = new SortedSet<float> { -m.Width / 2, m.Width / 2 };
            var zs = new SortedSet<float> { -m.Depth / 2, m.Depth / 2 };
            // Each tile edge is cut in quarters by grid lines: the outline then has a point every ~27 degrees round the
            // hole, so no triangle from the outline to the circle cuts into the hole (corners alone are too far).
            foreach (var h in holes)
                for (var k = -2; k <= 2; k++)
                {
                    xs.Add(R(h.x + k * tile / 4));
                    zs.Add(R(h.y + k * tile / 4));
                }
            var xl = xs.ToList();
            var zl = zs.ToList();

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var walls = new List<int>();

            // Grid lines of one hole may cut through another hole's tile: such cells are covered by that tile, whose
            // outline includes every grid point on its edges (so the neighbouring quads still share its vertices).
            for (var i = 0; i + 1 < xl.Count; i++)
                for (var j = 0; j + 1 < zl.Count; j++)
                {
                    var min = new Vector2(xl[i], zl[j]);
                    var max = new Vector2(xl[i + 1], zl[j + 1]);
                    var centre = (min + max) / 2;
                    var hole = holes.FindIndex(h => Mathf.Abs(h.x - centre.x) < tile / 2 && Mathf.Abs(h.y - centre.y) < tile / 2);
                    if (hole < 0) Quad(min, max, verts, uvs, top);
                }
            foreach (var h in holes) HoleTile(h, tile / 2, m.HoleRadius, xl, zl, verts, uvs, top, walls);

            var visual = new Mesh { name = "BoardFloor" };
            visual.SetVertices(verts);
            visual.SetUVs(0, uvs);
            visual.subMeshCount = 2;
            visual.SetTriangles(top, 0);
            visual.SetTriangles(walls, 1);
            visual.RecalculateNormals();
            visual.RecalculateBounds();

            var collider = new Mesh { name = "BoardFloorCollider" };
            collider.SetVertices(verts);
            collider.SetTriangles(top, 0);
            collider.RecalculateNormals();
            collider.RecalculateBounds();
            return (visual, collider);
        }

        static float R(float v) => Mathf.Round(v * 100000f) / 100000f;

        static int V(Vector3 p, List<Vector3> verts, List<Vector2> uvs)
        {
            verts.Add(p);
            uvs.Add(new Vector2(p.x, p.z) / UvScale);
            return verts.Count - 1;
        }

        static void Quad(Vector2 min, Vector2 max, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var a = V(new Vector3(min.x, 0, min.y), verts, uvs);
            var b = V(new Vector3(min.x, 0, max.y), verts, uvs);
            var c = V(new Vector3(max.x, 0, max.y), verts, uvs);
            var d = V(new Vector3(max.x, 0, min.y), verts, uvs);
            tris.AddRange(new[] { a, b, c, a, c, d }); // clockwise seen from above = facing up in Unity
        }

        // A square tile round a hole: its outline (corners plus every grid line crossing its edges) is zipped to the
        // hole's circle by angle round the centre, which works because both are convex round the same centre.
        static void HoleTile(Vector2 c, float half, float radius, List<float> xl, List<float> zl,
                             List<Vector3> verts, List<Vector2> uvs, List<int> top, List<int> walls)
        {
            // The tile's edges are grid lines: use the grid's exact (rounded) values so shared vertices coincide.
            float Nearest(List<float> lines, float v) => lines.OrderBy(l => Mathf.Abs(l - v)).First();
            float x0 = Nearest(xl, c.x - half), x1 = Nearest(xl, c.x + half), z0 = Nearest(zl, c.y - half), z1 = Nearest(zl, c.y + half);
            var outline = new List<Vector2> { new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1) };
            foreach (var x in xl)
                if (x > x0 + 1e-6f && x < x1 - 1e-6f)
                {
                    outline.Add(new Vector2(x, z0));
                    outline.Add(new Vector2(x, z1));
                }
            foreach (var z in zl)
                if (z > z0 + 1e-6f && z < z1 - 1e-6f)
                {
                    outline.Add(new Vector2(x0, z));
                    outline.Add(new Vector2(x1, z));
                }
            var outer = outline.Select(p => (angle: Angle(p - c), p)).OrderBy(t => t.angle).ToList();

            var ring = new int[CircleSegments];
            var ringHigh = new int[CircleSegments]; // the wall's own rim vertices: a sharp edge, not averaged normals
            var ringLow = new int[CircleSegments];
            var ringAngle = new float[CircleSegments];
            for (var i = 0; i < CircleSegments; i++)
            {
                var a = 2f * Mathf.PI * i / CircleSegments;
                var p = new Vector3(c.x + Mathf.Cos(a) * radius, 0, c.y + Mathf.Sin(a) * radius);
                ring[i] = V(p, verts, uvs);
                ringHigh[i] = V(p, verts, uvs);
                ringLow[i] = V(p + Vector3.down * HoleDepth, verts, uvs);
                ringAngle[i] = a;
            }
            var outerIdx = outer.Select(t => V(new Vector3(t.p.x, 0, t.p.y), verts, uvs)).ToArray();

            // Angles run counter-clockwise seen from above; Unity's front faces are clockwise seen from the viewer
            // (from above for the top, from the hole's axis for the walls).
            int n = CircleSegments, mOut = outer.Count, i0 = 0, j0 = 0;
            float InnerAngle(int i) => ringAngle[i % n] + (i >= n ? 2f * Mathf.PI : 0f);
            float OuterAngle(int j) => outer[j % mOut].angle + (j >= mOut ? 2f * Mathf.PI : 0f);
            while (i0 < n || j0 < mOut)
            {
                var advanceInner = j0 >= mOut || (i0 < n && InnerAngle(i0 + 1) <= OuterAngle(j0 + 1));
                if (advanceInner)
                {
                    top.AddRange(new[] { ring[i0 % n], ring[(i0 + 1) % n], outerIdx[j0 % mOut] });
                    i0++;
                }
                else
                {
                    top.AddRange(new[] { ring[i0 % n], outerIdx[(j0 + 1) % mOut], outerIdx[j0 % mOut] });
                    j0++;
                }
            }
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                walls.AddRange(new[] { ringHigh[i], ringLow[next], ringHigh[next], ringHigh[i], ringLow[i], ringLow[next] });
            }
        }

        static float Angle(Vector2 d) => Mathf.Repeat(Mathf.Atan2(d.y, d.x), 2f * Mathf.PI);
    }
}
