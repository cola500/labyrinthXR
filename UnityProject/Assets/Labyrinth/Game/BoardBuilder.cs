// Builds the labyrinth under a root transform from a MazeLayout: the tilting board (floor with holes, walls, rim,
// start, goal, route line), the steel ball, the fixed frame with its catch tray, the table and the two handles.
// Root space: floor level y = 0, the player stands on -z looking towards +z, the board's top surface at SurfaceHeight.
// SceneSetup runs this in the editor and saves the result in the scene (the generated meshes become assets).
using System.Collections.Generic;
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    public sealed class BoardMaterials
    {
        public Material BoardWood, WallWood, FrameWood, TableWood, Felt, HoleDark, Steel, Goal, GoalRing, StartMark, Line, Brass, Knob;
        public PhysicsMaterial BallPhysics, BoardPhysics;
        public Font Font;
    }

    public static class BoardBuilder
    {
        public const float SurfaceHeight = 0.95f;
        public const float FrameClearance = 0.035f;
        public const float FrameThickness = 0.045f;
        public const float FrameTop = 0.03f;      // above the board surface
        public const float TrayDepth = 0.17f;     // below the board surface
        public const float LeverSpacing = 0.36f;  // each handle's distance from the centre line
        public const float ShaftLength = 0.16f;
        public const float BallMass = 0.7f;

        public static readonly List<Mesh> GeneratedMeshes = new();

        public static LabyrinthGame Build(Transform root, MazeLayout m, BoardMaterials mat)
        {
            GeneratedMeshes.Clear();
            var h = SurfaceHeight;
            var game = root.gameObject.AddComponent<LabyrinthGame>();

            // --- the tilting board -------------------------------------------------------------------------------
            var board = new GameObject("Board").transform;
            board.SetParent(root, false);
            board.localPosition = new Vector3(0, h, 0);
            var boardBody = board.gameObject.AddComponent<Rigidbody>();
            boardBody.isKinematic = true;
            boardBody.interpolation = RigidbodyInterpolation.Interpolate;

            var (visual, collider) = FloorMesh.Build(m);
            GeneratedMeshes.Add(visual);
            GeneratedMeshes.Add(collider);
            var floor = new GameObject("Floor");
            floor.transform.SetParent(board, false);
            floor.AddComponent<MeshFilter>().sharedMesh = visual;
            floor.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat.BoardWood, mat.HoleDark };
            var mc = floor.AddComponent<MeshCollider>();
            mc.sharedMesh = collider;
            mc.sharedMaterial = mat.BoardPhysics;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(board, false);
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
                Box($"Wall{i++}", walls, (a + b) / 2 + Vector3.up * (m.WallHeight / 2 - 0.0015f), size, mat.WallWood, mat.BoardPhysics);
            }
            // A sturdier outer rim that also hides the floor's edge.
            const float rim = 0.026f, rimTop = 0.045f, rimBottom = -0.03f;
            var rx = m.Width / 2 + m.WallThickness / 2 + rim / 2;
            var rz = m.Depth / 2 + m.WallThickness / 2 + rim / 2;
            var ry = (rimTop + rimBottom) / 2;
            var rh = rimTop - rimBottom;
            Box("RimNear", walls, new Vector3(0, ry, -rz), new Vector3(2 * rx + rim, rh, rim), mat.WallWood, mat.BoardPhysics);
            Box("RimFar", walls, new Vector3(0, ry, rz), new Vector3(2 * rx + rim, rh, rim), mat.WallWood, mat.BoardPhysics);
            Box("RimLeft", walls, new Vector3(-rx, ry, 0), new Vector3(rim, rh, 2 * rz - rim), mat.WallWood, mat.BoardPhysics);
            Box("RimRight", walls, new Vector3(rx, ry, 0), new Vector3(rim, rh, 2 * rz - rim), mat.WallWood, mat.BoardPhysics);

            // Markings: start, goal and the route line (no colliders; the ball rolls over them).
            var marks = new GameObject("Markings").transform;
            marks.SetParent(board, false);
            Disc("Start", marks, m.ToBoard(m.Start), 0.036f, 0.0007f, mat.StartMark);
            Disc("GoalRing", marks, m.ToBoard(m.Goal), m.GoalRadius + 0.018f, 0.0006f, mat.GoalRing);
            var goal = Disc("Goal", marks, m.ToBoard(m.Goal), m.GoalRadius, 0.0009f, mat.Goal);
            for (var k = 0; k + 1 < m.Route.Count; k++)
            {
                var a = m.ToBoard(m.Route[k]);
                var b = m.ToBoard(m.Route[k + 1]);
                var seg = Box($"Line{k}", marks, (a + b) / 2 + Vector3.up * 0.0003f, new Vector3(0.006f, 0.0006f, (b - a).magnitude), mat.Line, null);
                seg.localRotation = Quaternion.LookRotation(b - a);
                if (k > 0) Disc($"Joint{k}", marks, a, 0.003f, 0.0006f, mat.Line);
            }

            // --- the steel ball ----------------------------------------------------------------------------------
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.transform.SetParent(root, false);
            ballGo.transform.localScale = Vector3.one * (2 * m.BallRadius);
            ballGo.transform.localPosition = new Vector3(0, h, 0) + m.ToBoard(m.Start) + Vector3.up * m.BallRadius;
            ballGo.GetComponent<MeshRenderer>().sharedMaterial = mat.Steel;
            ballGo.GetComponent<SphereCollider>().sharedMaterial = mat.BallPhysics;
            var ball = ballGo.AddComponent<Rigidbody>();
            ball.mass = BallMass;
            ball.linearDamping = 0.06f;
            ball.angularDamping = 0.12f;
            ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // --- the fixed frame, catch tray and table -----------------------------------------------------------
            var frame = new GameObject("Frame").transform;
            frame.SetParent(root, false);
            var ix = m.Width / 2 + m.WallThickness / 2 + rim + FrameClearance;
            var iz = m.Depth / 2 + m.WallThickness / 2 + rim + FrameClearance;
            var t = FrameThickness;
            var y0 = h - TrayDepth;
            var y1 = h + FrameTop;
            var fy = (y0 - 0.02f + y1) / 2; // the walls stand on the table top, like the tray
            var fh = y1 - (y0 - 0.02f);
            Box("FrameNear", frame, new Vector3(0, fy, -iz - t / 2), new Vector3(2 * (ix + t), fh, t), mat.FrameWood, mat.BoardPhysics);
            Box("FrameFar", frame, new Vector3(0, fy, iz + t / 2), new Vector3(2 * (ix + t), fh, t), mat.FrameWood, mat.BoardPhysics);
            Box("FrameLeft", frame, new Vector3(-ix - t / 2, fy, 0), new Vector3(t, fh, 2 * iz), mat.FrameWood, mat.BoardPhysics);
            Box("FrameRight", frame, new Vector3(ix + t / 2, fy, 0), new Vector3(t, fh, 2 * iz), mat.FrameWood, mat.BoardPhysics);
            Box("Tray", frame, new Vector3(0, y0 - 0.01f, 0), new Vector3(2 * ix, 0.02f, 2 * iz), mat.Felt, mat.BoardPhysics);
            // Pivot pins of the gimbal (visual): the board hangs between the left and right frame sides.
            Cylinder("PinLeft", frame, new Vector3(-(m.Width / 2 + rim + FrameClearance / 2 + 0.01f), h - 0.01f, 0), 0.012f, FrameClearance + 0.03f, Vector3.right, mat.Brass);
            Cylinder("PinRight", frame, new Vector3(m.Width / 2 + rim + FrameClearance / 2 + 0.01f, h - 0.01f, 0), 0.012f, FrameClearance + 0.03f, Vector3.right, mat.Brass);

            var table = new GameObject("Table").transform;
            table.SetParent(root, false);
            var tx = ix + t + 0.06f;
            var tz = iz + t + 0.06f;
            Box("TableTop", table, new Vector3(0, y0 - 0.04f, 0), new Vector3(2 * tx, 0.04f, 2 * tz), mat.TableWood, null);
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    Box("Leg", table, new Vector3(sx * (tx - 0.06f), (y0 - 0.06f) / 2, sz * (tz - 0.06f)), new Vector3(0.06f, y0 - 0.06f, 0.06f), mat.TableWood, null);

            // --- the two handles -------------------------------------------------------------------------------------
            var front = -(iz + t);
            var pitch = Lever("PitchHandle", root, new Vector3(-LeverSpacing, h, front), Vector3.forward, mat);
            var roll = Lever("RollHandle", root, new Vector3(LeverSpacing, h, front), Vector3.right, mat);

            game.Wire(boardBody, ball, pitch, roll, h);
            var feedback = root.gameObject.AddComponent<LabyrinthFeedback>();
            feedback.Wire(game, Message(root, m, mat), goal.GetComponent<Renderer>(), root.gameObject.AddComponent<AudioSource>());
            root.gameObject.AddComponent<DesktopInput>();
            return game;
        }

        // A handle on a bracket fixed to the frame's front: the slot on top shows which way it moves.
        static LeverView Lever(string name, Transform root, Vector3 frameFront, Vector3 travel, BoardMaterials mat)
        {
            var group = new GameObject(name + "Mount").transform;
            group.SetParent(root, false);
            var alongZ = Mathf.Abs(travel.z) > 0.5f;
            var bracketSize = alongZ ? new Vector3(0.07f, 0.04f, 0.15f) : new Vector3(0.15f, 0.04f, 0.07f);
            var centre = frameFront + new Vector3(0, -0.01f, -bracketSize.z / 2);
            Box("Bracket", group, centre, bracketSize, mat.FrameWood, null);
            var slot = alongZ ? new Vector3(0.014f, 0.004f, 0.12f) : new Vector3(0.12f, 0.004f, 0.014f);
            Box("Slot", group, centre + Vector3.up * (bracketSize.y / 2), slot, mat.HoleDark, null);

            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = centre + Vector3.up * (bracketSize.y / 2);
            Sphere("Hub", pivot, Vector3.zero, 0.026f, mat.Brass);
            Cylinder("Shaft", pivot, Vector3.up * (ShaftLength / 2), 0.009f, ShaftLength, Vector3.up, mat.Brass);
            var knob = Sphere("Knob", pivot, Vector3.up * ShaftLength, 0.052f, mat.Knob);
            var view = pivot.gameObject.AddComponent<LeverView>();
            view.Wire(travel, ShaftLength, knob, knob.GetComponent<Renderer>());
            return view;
        }

        static TextMesh Message(Transform root, MazeLayout m, BoardMaterials mat)
        {
            var go = new GameObject("Message");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, SurfaceHeight + 0.30f, m.Depth / 2 + 0.12f);
            var text = go.AddComponent<TextMesh>();
            text.font = mat.Font;
            text.fontSize = 64;
            text.characterSize = 0.0062f; // ~2.5 cm capitals: readable in the headset from ~1.4 m
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.95f, 0.85f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat.Font != null ? mat.Font.material : null;
            return text;
        }

        public static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, PhysicsMaterial phys)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var c = go.GetComponent<BoxCollider>();
            if (phys == null) Object.DestroyImmediate(c);
            else c.sharedMaterial = phys;
            return go.transform;
        }

        static Transform Disc(string name, Transform parent, Vector3 pos, float radius, float height, Material mat) =>
            Cylinder(name, parent, pos + Vector3.up * (height / 2), radius, height, Vector3.up, mat);

        static Transform Cylinder(string name, Transform parent, Vector3 pos, float radius, float length, Vector3 axis, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            go.transform.localScale = new Vector3(2 * radius, length / 2, 2 * radius);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Transform Sphere(string name, Transform parent, Vector3 pos, float diameter, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
