// Quest configuration and APK build: OpenXR loader on Android only with the Meta Quest Support feature and the Oculus
// Touch controller profile, IL2CPP/ARM64, min SDK 29, target SDK 34, Vulkan, single-pass instanced (multiview).
// Desktop/Standalone and the editor get no XR loader, so they stay non-VR (DesktopInput). Active input handling is
// "Both": OpenXR needs the Input System package, DesktopInput uses the legacy Input Manager.
// Batch: -buildTarget Android -executeMethod Labyrinth.EditorTools.QuestBuild.Build
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Labyrinth.EditorTools
{
    public static class QuestBuild
    {
        public const string ApplicationId = "com.labyrinthxr.prototype";
        public const string ApkPath = "Builds/LabyrinthXR-quest.apk";
        const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        const string OpenXrLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const int ActiveInputHandlingBoth = 2;

        [MenuItem("Labyrinth/Quest: configure project")]
        public static void Configure()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "LabyrinthXR";
            PlayerSettings.productName = "Labyrinth XR";
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EnsureActiveInputHandlingBoth();

            var manager = AndroidXrManager();
            foreach (var loader in manager.activeLoaders.Where(l => l == null || l.GetType().FullName != OpenXrLoader).ToList())
                manager.TryRemoveLoader(loader);
            var assigned = XRPackageMetadataStore.AssignLoader(manager, OpenXrLoader, BuildTargetGroup.Android);
            var features = ConfigureOpenXr();
            EnsurePreloadedXrAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Configured: id={ApplicationId}, loaders=[{string.Join(", ", manager.activeLoaders.Select(l => l.GetType().Name))}] (assigned now={assigned}), {features}");
        }

        // Meta Quest Support (Quest manifest entries, all Quest models as targets) and the Oculus Touch interaction
        // profile, which the Quest 3 runtime serves for its Touch Plus controllers. Everything else stays off.
        static string ConfigureOpenXr()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) throw new BuildFailedException("[QuestBuild] No OpenXR settings for Android.");
            var quest = settings.GetFeature<MetaQuestFeature>();
            var touch = settings.GetFeature<OculusTouchControllerProfile>();
            if (quest == null || touch == null) throw new BuildFailedException("[QuestBuild] OpenXR Meta Quest feature or Oculus Touch profile missing.");
            quest.enabled = true;
            touch.enabled = true;
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(quest);
            EditorUtility.SetDirty(touch);
            EditorUtility.SetDirty(settings);
            var on = settings.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name);
            return $"OpenXR features on=[{string.Join(", ", on)}], renderMode={settings.renderMode}";
        }

        static void EnsureActiveInputHandlingBoth()
        {
            var player = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (player == null) return;
            var so = new SerializedObject(player);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == ActiveInputHandlingBoth) return;
            prop.intValue = ActiveInputHandlingBoth;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[QuestBuild] Active input handling set to Both (takes effect after an editor restart).");
        }

        [MenuItem("Labyrinth/Quest: build APK")]
        public static void Build()
        {
            Configure();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            if (File.Exists(ApkPath)) File.Delete(ApkPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var ok = report.summary.result == BuildResult.Succeeded && File.Exists(ApkPath);
            Debug.Log(ok
                ? $"[QuestBuild] OK · {report.summary.totalSize / (1024 * 1024)} MB · {Path.GetFullPath(ApkPath)}"
                : $"[QuestBuild] FAILED · result={report.summary.result} · errors={report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static XRManagerSettings AndroidXrManager()
        {
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
            if (perTarget == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(XrSettingsPath));
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            EditorBuildSettings.RemoveConfigObject("Unity.XR.Oculus.Settings"); // left over from the Oculus XR plugin

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                general.name = "Android Settings";
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android Managers";
                general.Manager = manager;
                AssetDatabase.AddObjectToAsset(general, perTarget);
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            }
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);
            return general.Manager;
        }

        // CampfireVR found that Unity 6 + XR Management can drop XRGeneralSettings from preloadedAssets during a build,
        // which ships an APK where XR never starts. Re-added before and after every build; stale (deleted) entries are
        // dropped. OpenXR's own build processor adds its settings for the build itself.
        internal static void EnsurePreloadedXrAssets()
        {
            var required = new List<Object>();
            var general = AssetDatabase.LoadAllAssetsAtPath(XrSettingsPath).OfType<XRGeneralSettings>().FirstOrDefault();
            if (general != null) required.Add(general);

            var raw = PlayerSettings.GetPreloadedAssets() ?? new Object[0];
            var current = raw.Where(o => o != null).ToList();
            var missing = required.Where(r => !current.Contains(r)).ToList();
            if (missing.Count == 0 && current.Count == raw.Length) return;
            PlayerSettings.SetPreloadedAssets(current.Concat(missing).ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] preloadedAssets: restored {missing.Count} XR entries, dropped {raw.Length - current.Count} missing.");
        }
    }

    public sealed class PreloadedXrAssetsGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) QuestBuild.EnsurePreloadedXrAssets();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) QuestBuild.EnsurePreloadedXrAssets();
        }
    }
}
