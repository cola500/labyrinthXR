// Generates Assets/Scenes/Labyrinth.unity and everything it references under Assets/Labyrinth/Generated/: procedural
// wood textures, materials (built-in Standard shader), physics materials, a warm reflection cubemap (so the steel ball
// reflects a warm room instead of black), the floor meshes, the room, the lights and the cameras. Change the scene
// here, not by hand. Batch: -executeMethod Labyrinth.EditorTools.SceneSetup.CreateScene
using System.IO;
using Labyrinth.Core;
using Labyrinth.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Labyrinth.EditorTools
{
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Labyrinth.unity";
        const string Gen = "Assets/Labyrinth/Generated";

        [MenuItem("Labyrinth/Regenerate scene")]
        public static void CreateScene()
        {
            if (AssetDatabase.IsValidFolder(Gen)) AssetDatabase.DeleteAsset(Gen);
            Directory.CreateDirectory(Gen);
            AssetDatabase.Refresh();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Quality();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = Materials();

            // Light and atmosphere: warm trilight ambient, a warm custom reflection, no skybox.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.42f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.28f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.12f, 0.09f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = ReflectionCube();
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = false;

            Room(mat);

            var root = new GameObject("Labyrinth").transform;
            var game = BoardBuilder.Build(root, Levels.Create(0), mat);
            foreach (var mesh in BoardBuilder.GeneratedMeshes) AssetDatabase.CreateAsset(mesh, $"{Gen}/{mesh.name}.asset");

            // The lamp above the table: the key light, with soft shadows (one spot = one shadow map, cheap on Quest).
            var lamp = new GameObject("Lamp Light").AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.80f, 0.58f);
            lamp.intensity = 2.1f;
            lamp.range = 4.5f;
            lamp.spotAngle = 95f;
            lamp.innerSpotAngle = 40f;
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = 0.7f;
            lamp.shadowBias = 0.02f;
            lamp.shadowNormalBias = 0.2f;
            lamp.transform.SetPositionAndRotation(new Vector3(0.25f, 2.35f, -0.15f), Quaternion.Euler(84f, 0f, 0f));
            // Soft window fill from the left, no shadows.
            var fill = new GameObject("Window Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.95f, 0.85f, 0.72f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(40f, 65f, 0f);

            // Camera: a standing adult's eyes behind the handles (desktop). In VR, XrRig takes it over.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.09f, 0.07f);
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 30f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = DesktopEye;
            camGo.transform.LookAt(DesktopLookAt);
            var rig = new GameObject("XR Rig").AddComponent<XrRig>();
            // Controller hands: soft pale spheres, clearly different from the red knobs.
            rig.Wire(game, cam, Standard("Hand", null, 0.35f, Vector2.one, new Color(0.93f, 0.86f, 0.76f)));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] scene saved: " + ScenePath);
        }

        public static readonly Vector3 DesktopEye = new(0f, 1.62f, -0.95f);
        public static readonly Vector3 DesktopLookAt = new(0f, 0.92f, 0.05f);

        static void Quality()
        {
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowDistance = 4f;
                QualitySettings.shadowCascades = 1;
                QualitySettings.pixelLightCount = 2;
                QualitySettings.antiAliasing = 4;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void Room(BoardMaterials mat)
        {
            var room = new GameObject("Room").transform;
            var floorMat = Standard("RoomFloor", WoodTexture("RoomFloorWood", new Color(0.36f, 0.22f, 0.13f), new Color(0.22f, 0.12f, 0.07f), 11, planks: true), 0.25f, new Vector2(4, 4));
            var wallMat = Standard("RoomWall", null, 0.05f, Vector2.one, new Color(0.78f, 0.66f, 0.52f));
            var rugMat = Standard("Rug", null, 0f, Vector2.one, new Color(0.45f, 0.16f, 0.12f));
            var shelfMat = mat.FrameWood;
            BoardBuilder.Box("Floor", room, new Vector3(0, -0.05f, 0), new Vector3(7f, 0.1f, 7f), floorMat, null);
            BoardBuilder.Box("Ceiling", room, new Vector3(0, 2.75f, 0), new Vector3(7f, 0.1f, 7f), wallMat, null);
            BoardBuilder.Box("WallFar", room, new Vector3(0, 1.35f, 2.6f), new Vector3(7f, 2.8f, 0.1f), wallMat, null);
            BoardBuilder.Box("WallNear", room, new Vector3(0, 1.35f, -3.0f), new Vector3(7f, 2.8f, 0.1f), wallMat, null);
            BoardBuilder.Box("WallLeft", room, new Vector3(-3.0f, 1.35f, 0), new Vector3(0.1f, 2.8f, 7f), wallMat, null);
            BoardBuilder.Box("WallRight", room, new Vector3(3.0f, 1.35f, 0), new Vector3(0.1f, 2.8f, 7f), wallMat, null);
            BoardBuilder.Box("Skirting", room, new Vector3(0, 0.05f, 2.54f), new Vector3(7f, 0.1f, 0.03f), shelfMat, null);
            BoardBuilder.Box("Rug", room, new Vector3(0, 0.004f, 0), new Vector3(2.2f, 0.008f, 1.6f), rugMat, null);
            // A shelf with a few toy blocks on the far wall: something calm to look at.
            BoardBuilder.Box("Shelf", room, new Vector3(-0.6f, 1.45f, 2.45f), new Vector3(1.2f, 0.03f, 0.22f), shelfMat, null);
            var colours = new[] { new Color(0.75f, 0.25f, 0.15f), new Color(0.90f, 0.70f, 0.25f), new Color(0.25f, 0.45f, 0.60f) };
            for (var i = 0; i < 3; i++)
                BoardBuilder.Box($"Block{i}", room, new Vector3(-0.95f + i * 0.13f, 1.51f, 2.42f), Vector3.one * 0.09f,
                    Standard($"Block{i}", null, 0.2f, Vector2.one, colours[i]), null);
            // The lamp's shade above the table.
            var shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shade.name = "LampShade";
            Object.DestroyImmediate(shade.GetComponent<Collider>());
            shade.transform.SetParent(room, false);
            shade.transform.SetPositionAndRotation(new Vector3(0.25f, 2.45f, -0.15f), Quaternion.identity);
            shade.transform.localScale = new Vector3(0.32f, 0.06f, 0.32f);
            var shadeMat = Standard("LampShade", null, 0.1f, Vector2.one, new Color(0.85f, 0.70f, 0.45f));
            shadeMat.EnableKeyword("_EMISSION");
            shadeMat.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.45f) * 1.2f);
            shade.GetComponent<MeshRenderer>().sharedMaterial = shadeMat;
            shade.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            BoardBuilder.Box("LampCord", room, new Vector3(0.25f, 2.6f, -0.15f), new Vector3(0.008f, 0.3f, 0.008f), mat.HoleDark, null);
            foreach (var r in room.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        }

        static BoardMaterials Materials()
        {
            var birch = WoodTexture("BoardWood", new Color(0.86f, 0.70f, 0.50f), new Color(0.72f, 0.55f, 0.36f), 1, planks: false);
            var maple = WoodTexture("WallWood", new Color(0.70f, 0.48f, 0.28f), new Color(0.55f, 0.35f, 0.19f), 2, planks: false);
            var walnut = WoodTexture("FrameWood", new Color(0.42f, 0.26f, 0.15f), new Color(0.28f, 0.16f, 0.09f), 3, planks: false);
            var oak = WoodTexture("TableWood", new Color(0.55f, 0.37f, 0.22f), new Color(0.40f, 0.26f, 0.14f), 4, planks: true);
            var m = new BoardMaterials
            {
                BoardWood = Standard("BoardWood", birch, 0.30f, new Vector2(1, 1)),
                WallWood = Standard("WallWood", maple, 0.35f, new Vector2(4, 4)),
                FrameWood = Standard("FrameWood", walnut, 0.40f, new Vector2(3, 3)),
                TableWood = Standard("TableWood", oak, 0.30f, new Vector2(2, 2)),
                Felt = Standard("Felt", null, 0f, Vector2.one, new Color(0.10f, 0.13f, 0.08f)),
                HoleDark = Standard("HoleDark", null, 0f, Vector2.one, new Color(0.03f, 0.02f, 0.015f)),
                Steel = Standard("Steel", null, 0.88f, Vector2.one, new Color(0.92f, 0.92f, 0.95f), metallic: 1f),
                Goal = Standard("Goal", null, 0.3f, Vector2.one, new Color(0.20f, 0.45f, 0.22f)),
                GoalRing = Standard("GoalRing", null, 0.7f, Vector2.one, new Color(0.85f, 0.62f, 0.25f), metallic: 0.8f),
                StartMark = Standard("StartMark", null, 0.2f, Vector2.one, new Color(0.62f, 0.40f, 0.22f)),
                Line = Standard("Line", null, 0.1f, Vector2.one, new Color(0.16f, 0.10f, 0.06f)),
                Brass = Standard("Brass", null, 0.75f, Vector2.one, new Color(0.80f, 0.60f, 0.30f), metallic: 0.9f),
                Knob = Standard("Knob", null, 0.55f, Vector2.one, new Color(0.55f, 0.16f, 0.10f)),
                BallPhysics = Physics("Ball", 0.25f, 0.30f, 0.15f),
                BoardPhysics = Physics("Wood", 0.45f, 0.55f, 0.25f),
                Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
            };
            m.Goal.EnableKeyword("_EMISSION");
            m.Goal.SetColor("_EmissionColor", new Color(0.08f, 0.18f, 0.08f));
            return m;
        }

        static Material Standard(string name, Texture2D albedo, float smoothness, Vector2 tiling, Color? tint = null, float metallic = 0f)
        {
            var m = new Material(Shader.Find("Standard")) { name = name };
            m.color = tint ?? Color.white;
            if (albedo != null)
            {
                m.mainTexture = albedo;
                m.mainTextureScale = tiling;
            }
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(m, $"{Gen}/{name}.mat");
            return m;
        }

        static PhysicsMaterial Physics(string name, float dynamicFriction, float staticFriction, float bounce)
        {
            var p = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounce,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(p, $"{Gen}/{name}.physicMaterial");
            return p;
        }

        // Procedural wood: wavy growth rings along x with fine grain; planks: staggered boards with dark seams.
        static Texture2D WoodTexture(string name, Color light, Color dark, int seed, bool planks)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[size * size];
            var o = seed * 17.31f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    var board = planks ? Mathf.Floor(v * 6f) : 0f;
                    var shift = planks ? Mathf.Repeat(board * 0.37f, 1f) : 0f;
                    var uu = Mathf.Repeat(u + shift, 1f);
                    var warp = Mathf.PerlinNoise(uu * 3f + o, v * 6f + board * 3.1f) * 2.2f;
                    var rings = Mathf.Sin((v * 26f + warp * 3f + board * 1.7f) * Mathf.PI);
                    var ring = Mathf.SmoothStep(0f, 1f, rings * 0.5f + 0.5f);
                    var grain = Mathf.PerlinNoise(uu * 90f + o, v * 4f) * 0.35f + Mathf.PerlinNoise(uu * 8f, v * 40f + o) * 0.15f;
                    var t = Mathf.Clamp01(ring * 0.6f + grain);
                    var c = Color.Lerp(light, dark, t);
                    if (planks)
                    {
                        var edge = Mathf.Abs(Mathf.Repeat(v * 6f, 1f) - 0.5f) * 2f;
                        var end = Mathf.Abs(Mathf.Repeat(uu * 2f, 1f) - 0.5f) * 2f;
                        if (edge > 0.97f || end > 0.992f) c *= 0.55f;
                        c *= 0.92f + 0.16f * Mathf.PerlinNoise(board * 5.3f + o, 0.5f);
                    }
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, $"{Gen}/{name}.asset");
            return tex;
        }

        // A soft warm room for reflections: bright warm top (the lamp), warm walls, dark floor.
        static Cubemap ReflectionCube()
        {
            const int size = 64;
            var cube = new Cubemap(size, TextureFormat.RGB24, true) { name = "WarmReflection" };
            foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
            {
                if (face == CubemapFace.Unknown) continue;
                var px = new Color[size * size];
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var d = FaceDirection(face, (x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).normalized;
                        var up = d.y;
                        var c = up > 0
                            ? Color.Lerp(new Color(0.55f, 0.42f, 0.30f), new Color(1.0f, 0.86f, 0.62f), Mathf.Pow(up, 3f))
                            : Color.Lerp(new Color(0.55f, 0.42f, 0.30f), new Color(0.18f, 0.11f, 0.07f), Mathf.Pow(-up, 0.6f));
                        // a window glow on one side
                        c += new Color(0.35f, 0.30f, 0.22f) * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, new Vector3(-0.8f, 0.3f, 0.5f).normalized)), 12f);
                        px[(size - 1 - y) * size + x] = c;
                    }
                cube.SetPixels(px, face);
            }
            cube.Apply(true);
            AssetDatabase.CreateAsset(cube, $"{Gen}/WarmReflection.cubemap");
            return cube;
        }

        static Vector3 FaceDirection(CubemapFace f, float a, float b) => f switch
        {
            CubemapFace.PositiveX => new Vector3(1, -b, -a),
            CubemapFace.NegativeX => new Vector3(-1, -b, a),
            CubemapFace.PositiveY => new Vector3(a, 1, b),
            CubemapFace.NegativeY => new Vector3(a, -1, -b),
            CubemapFace.PositiveZ => new Vector3(a, -b, 1),
            _ => new Vector3(-a, -b, -1),
        };
    }
}
