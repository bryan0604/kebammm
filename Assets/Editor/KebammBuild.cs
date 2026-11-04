#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
                options = BuildOptions.Development // debug console (UNITY_EDITOR || DEVELOPMENT_BUILD) must exist on device
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[KebammBuild] Result={s.result} Size={s.totalSize} Time={s.totalTime} Errors={s.totalErrors} Warnings={s.totalWarnings}");
            NotifyTelegram(report);
            if (Application.isBatchMode)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        // Optional Telegram ping. Token and chat id come only from the Windows user environment variables
        // KEBAMM_TG_TOKEN / KEBAMM_TG_CHAT (never stored in the repo). Short timeout; never fails the build.
        static void NotifyTelegram(BuildReport report)
        {
            try
            {
                string token = EnvVar("KEBAMM_TG_TOKEN");
                string chat = EnvVar("KEBAMM_TG_CHAT");
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chat))
                {
                    Debug.Log("[KebammBuild] Telegram ping skipped (KEBAMM_TG_TOKEN / KEBAMM_TG_CHAT not set).");
                    return;
                }

                BuildSummary s = report.summary;
                string text;
                if (s.result == BuildResult.Succeeded)
                {
                    long bytes = File.Exists(ApkPath) ? new FileInfo(ApkPath).Length : (long)s.totalSize;
                    TimeSpan t = s.totalTime;
                    string duration = t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m {t.Seconds}s" : $"{t.Seconds}s";
                    text = $"\u2705 Kebammm APK built: {bytes / (1024.0 * 1024.0):0.0} MB in {duration}, {DateTime.Now:d MMM HH:mm}";
                }
                else
                {
                    string firstError = report.steps
                        .SelectMany(step => step.messages)
                        .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                        .Select(m => m.content)
                        .FirstOrDefault();
                    if (string.IsNullOrEmpty(firstError))
                        firstError = $"Result={s.result}, errors={s.totalErrors}";
                    firstError = firstError.Split('\n')[0].Trim();
                    if (firstError.Length > 300)
                        firstError = firstError.Substring(0, 300) + "...";
                    text = $"\u274C Kebammm APK build failed: {firstError}";
                }

                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var form = new FormUrlEncodedContent(new Dictionary<string, string> { { "chat_id", chat }, { "text", text } });
                    HttpResponseMessage resp = http.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", form)
                        .GetAwaiter().GetResult();
                    Debug.Log($"[KebammBuild] Telegram ping: HTTP {(int)resp.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KebammBuild] Telegram ping failed (build unaffected): " + e.GetType().Name + ": " + e.Message);
            }
        }

        static string EnvVar(string name)
        {
            string v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            return string.IsNullOrEmpty(v) ? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) : v;
        }
    }
}
#endif
