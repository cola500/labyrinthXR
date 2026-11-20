// Builds the labyrinth under a root transform: the tilting board with its rim and the first level's maze (MazeBuilder),
// the steel ball, the fixed frame with its catch tray, the table, the two handles, the level plaque and the fit
// controls (two brass table-height grips on the table's front edge, a hold-to-reset button between them; the handles'
// wooden bases are the spacing grips). LabyrinthGame.ApplyFit moves all of it for the player's table height and handle
// spacing; what is built here is the default fit (PlayerFit.Default*).
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

        public MazeMaterials Maze() => new()
        {
            BoardWood = BoardWood, HoleDark = HoleDark, WallWood = WallWood, Goal = Goal, GoalRing = GoalRing,
            StartMark = StartMark, Line = Line, Wood = BoardPhysics,
        };
    }

    public static class BoardBuilder
    {
        public const float SurfaceHeight = 0.95f;
        public const float FrameClearance = 0.035f;
        public const float FrameThickness = 0.045f;
        public const float FrameTop = 0.03f;      // above the board surface
        public const float TrayDepth = 0.17f;     // below the board surface
        public const float LeverSpacing = PlayerFit.DefaultLeverSpacing; // each handle's distance from the centre line (default fit)
        public const float ShaftLength = 0.16f;
        public const float BallMass = 0.7f;
        public const float PlaqueWidth = 0.48f;

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

            var mazeMat = mat.Maze();
            var maze = MazeBuilder.Build(board, m, mazeMat);
            GeneratedMeshes.AddRange(maze.GeneratedMeshes);
            var walls = new GameObject("Rim").transform; // the board's own rim: the same for every level
            walls.SetParent(board, false);
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
            var tableTopY = y0 - 0.04f;
            Box("TableTop", table, new Vector3(0, tableTopY, 0), new Vector3(2 * tx, 0.04f, 2 * tz), mat.TableWood, null);
            var legTop = y0 - 0.06f; // the legs reach from the floor (y = 0 at the default fit) to under the table top
            var legs = new List<Transform>();
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    legs.Add(Box("Leg", table, new Vector3(sx * (tx - 0.06f), legTop / 2, sz * (tz - 0.06f)), new Vector3(0.06f, legTop, 0.06f), mat.TableWood, null));

            // --- the two handles -------------------------------------------------------------------------------------
            var front = -(iz + t);
            var (pitch, pitchMount, pitchBracket) = Lever("PitchHandle", root, new Vector3(-LeverSpacing, h, front), Vector3.forward, mat);
            var (roll, rollMount, rollBracket) = Lever("RollHandle", root, new Vector3(LeverSpacing, h, front), Vector3.right, mat);

            // --- the fit controls: table-height grips and the reset button on the table's front edge --------------------
            var fit = new GameObject("FitControls").transform;
            fit.SetParent(root, false);
            var gripZ = -tz - 0.03f;
            var heightGrips = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1 : 1) * (tx - 0.13f);
                var grip = Cylinder(i == 0 ? "HeightGripLeft" : "HeightGripRight", fit, new Vector3(x, tableTopY, gripZ), 0.011f, 0.13f, Vector3.right, mat.Brass);
                grip.gameObject.AddComponent<GripHighlight>();
                foreach (var dx in new[] { -0.055f, 0.055f })
                    Cylinder("Post", grip.parent, new Vector3(x + dx, tableTopY, (gripZ - tz) / 2), 0.006f, 0.03f, Vector3.forward, mat.Brass);
                heightGrips[i] = grip;
            }
            var reset = Cylinder("ResetButton", fit, new Vector3(0, tableTopY, -tz - 0.004f), 0.022f, 0.008f, Vector3.forward, mat.Knob);
            reset.gameObject.AddComponent<GripHighlight>();
            pitchBracket.gameObject.AddComponent<GripHighlight>();
            rollBracket.gameObject.AddComponent<GripHighlight>();

            game.Wire(boardBody, ball, pitch, roll, h, mazeMat, maze.Root, maze.Goal);
            var plaque = Plaque(root, front, mat);
            game.WireFit(legs.ToArray(), legTop, pitchMount, rollMount, new[] { pitchBracket, rollBracket }, heightGrips, reset,
                plaque.transform.parent, front);
            var feedback = root.gameObject.AddComponent<LabyrinthFeedback>();
            feedback.Wire(game, Message(root, m, mat), plaque, root.gameObject.AddComponent<AudioSource>());
            root.gameObject.AddComponent<DesktopInput>();
            return game;
        }

        // A handle on a bracket fixed to the frame's front: the slot on top shows which way it moves. The bracket (its
        // wooden base) doubles as the grip for moving the handles closer together or further apart.
        static (LeverView view, Transform mount, Transform bracket) Lever(string name, Transform root, Vector3 frameFront, Vector3 travel, BoardMaterials mat)
        {
            var group = new GameObject(name + "Mount").transform;
            group.SetParent(root, false);
            var alongZ = Mathf.Abs(travel.z) > 0.5f;
            var bracketSize = alongZ ? new Vector3(0.07f, 0.04f, 0.15f) : new Vector3(0.15f, 0.04f, 0.07f);
            var centre = frameFront + new Vector3(0, -0.01f, -bracketSize.z / 2);
            var bracket = Box("Bracket", group, centre, bracketSize, mat.FrameWood, null);
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
            return (view, group, bracket);
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

        // A small walnut plaque on the frame's front edge, between the handles, tilted towards the player: the level.
        // When the handles are moved too close together for it, LabyrinthGame moves it down onto the frame's front face.
        static TextMesh Plaque(Transform root, float frontZ, BoardMaterials mat)
        {
            var plaque = new GameObject("LevelPlaque").transform;
            plaque.SetParent(root, false);
            plaque.localPosition = new Vector3(0, SurfaceHeight + FrameTop - 0.012f, frontZ - 0.012f);
            plaque.localRotation = Quaternion.Euler(35f, 0f, 0f); // faces up-and-back, towards a standing player
            Box("Board", plaque, Vector3.zero, new Vector3(PlaqueWidth, 0.055f, 0.012f), mat.FrameWood, null);
            var go = new GameObject("Text");
            go.transform.SetParent(plaque, false);
            go.transform.localPosition = new Vector3(0, 0, -0.0065f);
            var text = go.AddComponent<TextMesh>();
            text.font = mat.Font;
            text.fontSize = 64;
            text.characterSize = 0.0036f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1.0f, 0.88f, 0.62f); // cream lettering on dark walnut (dark text on light wood came out faint)
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

        public static Transform Disc(string name, Transform parent, Vector3 pos, float radius, float height, Material mat) =>
            Cylinder(name, parent, pos + Vector3.up * (height / 2), radius, height, Vector3.up, mat);

        public static Transform Cylinder(string name, Transform parent, Vector3 pos, float radius, float length, Vector3 axis, Material mat)
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
