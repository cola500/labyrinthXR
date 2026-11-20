// Builds one level's maze under the tilting board: the floor with its holes (one MeshCollider), the walls (rim walls
// included), and the markings (start, goal, route line). Used at edit time for the scene's Level 1 and at runtime on
// every level change. Everything a level owns hangs under the returned root, so a level change only has to drop that
// root (and the meshes it generated).
using System.Collections.Generic;
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    [System.Serializable]
    public sealed class MazeMaterials
    {
        public Material BoardWood, HoleDark, WallWood, Goal, GoalRing, StartMark, Line;
        public PhysicsMaterial Wood;
    }

    public sealed class MazeView
    {
        public GameObject Root;
        public Renderer Goal;
        public readonly List<Mesh> GeneratedMeshes = new();
    }

    public static class MazeBuilder
    {
        public static MazeView Build(Transform board, MazeLayout m, MazeMaterials mat)
        {
            var view = new MazeView { Root = new GameObject($"Maze {m.Number}: {m.Name}") };
            var root = view.Root.transform;
            root.SetParent(board, false);

            var (visual, collider) = FloorMesh.Build(m);
            view.GeneratedMeshes.Add(visual);
            view.GeneratedMeshes.Add(collider);
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root, false);
            floor.AddComponent<MeshFilter>().sharedMesh = visual;
            floor.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat.BoardWood, mat.HoleDark };
            var mc = floor.AddComponent<MeshCollider>();
            mc.sharedMesh = collider;
            mc.sharedMaterial = mat.Wood;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            var i = 0;
            foreach (var w in m.AllWalls())
            {
                var a = m.ToBoard(w.A);
                var b = m.ToBoard(w.B);
                var along = b - a;
                var len = along.magnitude + m.WallThickness;
                var horizontal = Mathf.Abs(along.x) > Mathf.Abs(along.z);
                var size = horizontal
                    ? new Vector3(len, m.WallHeight + 0.003f, m.WallThickness)
                    : new Vector3(m.WallThickness, m.WallHeight + 0.003f, len);
                BoardBuilder.Box($"Wall{i++}", walls, (a + b) / 2 + Vector3.up * (m.WallHeight / 2 - 0.0015f), size, mat.WallWood, mat.Wood);
            }

            // Markings: start, goal and the route line (no colliders; the ball rolls over them).
            var marks = new GameObject("Markings").transform;
            marks.SetParent(root, false);
            BoardBuilder.Disc("Start", marks, m.ToBoard(m.Start), 0.036f, 0.0007f, mat.StartMark);
            BoardBuilder.Disc("GoalRing", marks, m.ToBoard(m.Goal), m.GoalRadius + 0.018f, 0.0006f, mat.GoalRing);
            view.Goal = BoardBuilder.Disc("Goal", marks, m.ToBoard(m.Goal), m.GoalRadius, 0.0009f, mat.Goal).GetComponent<Renderer>();
            for (var k = 0; k + 1 < m.Route.Count; k++)
            {
                var a = m.ToBoard(m.Route[k]);
                var b = m.ToBoard(m.Route[k + 1]);
                var seg = BoardBuilder.Box($"Line{k}", marks, (a + b) / 2 + Vector3.up * 0.0003f, new Vector3(0.006f, 0.0006f, (b - a).magnitude), mat.Line, null);
                seg.localRotation = Quaternion.LookRotation(b - a);
                if (k > 0) BoardBuilder.Disc($"Joint{k}", marks, a, 0.003f, 0.0006f, mat.Line);
            }
            return view;
        }
    }
}
