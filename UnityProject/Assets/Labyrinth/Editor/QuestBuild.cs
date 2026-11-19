// Quest configuration and APK build, adapted from Lilla fruktståndet (itself from Magic Tiles / CampfireVR): Oculus XR
// loader on Android only, IL2CPP/ARM64, min SDK 29, target SDK 34, Vulkan, multiview. Desktop/Standalone and the editor
// get no XR loader, so they stay non-VR (DesktopInput). Batch: -buildTarget Android -executeMethod
// Labyrinth.EditorTools.QuestBuild.Build
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.Oculus;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace Labyrinth.EditorTools
{
    public static class QuestBuild
    {
        public const string ApplicationId = "com.labyrinthxr.prototype";
        public const string ApkPath = "Builds/LabyrinthXR-quest.apk";
        const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        const string OculusSettingsPath = "Assets/XR/Settings/OculusSettings.asset";

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

            var oculus = OculusSettingsAsset();
            var manager = AndroidXrManager();
            var assigned = XRPackageMetadataStore.AssignLoader(manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);
            EnsurePreloadedXrAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Configured: id={ApplicationId}, Oculus loader assigned={assigned || manager.activeLoaders.Count > 0}, Quest 3={oculus.TargetQuest3}");
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

        static OculusSettings OculusSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<OculusSettings>(OculusSettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OculusSettingsPath));
                settings = ScriptableObject.CreateInstance<OculusSettings>();
                AssetDatabase.CreateAsset(settings, OculusSettingsPath);
            }
            settings.m_StereoRenderingModeAndroid = OculusSettings.StereoRenderingModeAndroid.Multiview;
            settings.TargetQuest2 = true;
            settings.TargetQuest3 = true;
            settings.TargetQuest3S = true;
            EditorUtility.SetDirty(settings);
            EditorBuildSettings.AddConfigObject("Unity.XR.Oculus.Settings", settings, true);
            return settings;
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

        // CampfireVR found that Unity 6 + XR Management can drop these from preloadedAssets during a build, which
        // ships an APK where XR never starts. Re-added before and after every build.
        internal static void EnsurePreloadedXrAssets()
        {
            var required = new List<Object>();
            var general = AssetDatabase.LoadAllAssetsAtPath(XrSettingsPath).OfType<XRGeneralSettings>().FirstOrDefault();
            if (general != null) required.Add(general);
            var oculus = AssetDatabase.LoadAssetAtPath<OculusSettings>(OculusSettingsPath);
            if (oculus != null) required.Add(oculus);

            var current = (PlayerSettings.GetPreloadedAssets() ?? new Object[0]).Where(o => o != null).ToList();
            var missing = required.Where(r => !current.Contains(r)).ToList();
            if (missing.Count == 0) return;
            PlayerSettings.SetPreloadedAssets(current.Concat(missing).ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Restored {missing.Count} XR entries in preloadedAssets.");
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
