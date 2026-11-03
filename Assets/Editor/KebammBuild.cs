#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kebamm.EditorTools
{
    public static class KebammBuild
    {
        const string ApkPath = "Builds/Android/Kebammm.apk";

        [MenuItem("Kebamm/Build Android APK")]
        public static void BuildAndroidApk()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !s.path.Contains("/Samples/"))
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
                scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            Debug.Log("[KebammBuild] Scenes: " + string.Join(", ", scenes));

            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[KebammBuild] Result={s.result} Size={s.totalSize} Time={s.totalTime} Errors={s.totalErrors} Warnings={s.totalWarnings}");
            if (Application.isBatchMode)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif