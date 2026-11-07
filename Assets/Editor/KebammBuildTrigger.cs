using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kebamm.EditorTools
{
    /// <summary>
    /// Remote build trigger: while the Editor is open, creating Builds/.build_request runs the same build as the
    /// Kebamm/Build Android APK menu. Progress/result goes to Builds/.build_status. Waits while compiling or in Play mode.
    /// </summary>
    [InitializeOnLoad]
    static class KebammBuildTrigger
    {
        const string RequestPath = "Builds/.build_request";
        const string StatusPath = "Builds/.build_status";
        const string ApkPath = "Builds/Android/Kebammm.apk";
        static double s_next;
        static bool s_busy;
        static bool s_waitNoted;

        static KebammBuildTrigger()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (s_busy || EditorApplication.timeSinceStartup < s_next)
                return;
            s_next = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(RequestPath))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!s_waitNoted)
                {
                    Status("waiting: in Play mode, the build starts when Play stops");
                    s_waitNoted = true;
                }
                return;
            }
            s_waitNoted = false;
            File.Delete(RequestPath);
            s_busy = true;
            Status("started");
            // Run immediately. delayCall can be lost on a domain reload, or never fire if the
            // Editor throttles update ticks while it is in the background.
            Run();
        }

        static void Run()
        {
            try
            {
                DateTime buildStart = DateTime.Now;
                KebammBuild.BuildAndroidApk();
                var fi = new FileInfo(ApkPath);
                bool fresh = fi.Exists && fi.LastWriteTime >= buildStart; // an APK from an earlier build must not count
                Status(fresh ? $"succeeded size={fi.Length} apk={fi.FullName} apkTime={fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}"
                             : "failed: no fresh APK (see Editor.log [KebammBuild] Result=)");
            }
            catch (Exception e)
            {
                Status("failed: " + e.Message);
            }
            finally
            {
                s_busy = false;
            }
        }

        static void Status(string s)
        {
            Directory.CreateDirectory("Builds");
            File.WriteAllText(StatusPath, $"{s}\ntime={DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}\n");
            Debug.Log("[KebammBuildTrigger] " + s);
        }
    }
}
