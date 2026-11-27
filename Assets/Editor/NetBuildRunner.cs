using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Chạy build Windows từ Editor đang mở, ghi trạng thái ra file để shell theo dõi.
    /// Dùng Begin(exePath, development, statusPath); build chạy sau khi eval trả về.
    /// </summary>
    public static class NetBuildRunner
    {
        private static string s_Output;
        private static bool s_Development;
        private static string s_Status;
        private static bool s_Scheduled;

        public static string Begin(string output, bool development, string statusPath)
        {
            if (s_Scheduled) return "already-scheduled";
            s_Output = output;
            s_Development = development;
            s_Status = Path.GetFullPath(statusPath);
            s_Scheduled = true;
            Directory.CreateDirectory(Path.GetDirectoryName(s_Status));
            Directory.CreateDirectory(Path.GetDirectoryName(s_Output));
            WriteStatus("scheduled");
            EditorApplication.delayCall += RunBuild;
            return "scheduled";
        }

        /// <summary>Chạy build ngay trong lời gọi eval; eval có thể timeout nhưng build vẫn tiếp tục.</summary>
        public static string BuildNow(string output, bool development, string statusPath)
        {
            s_Output = output;
            s_Development = development;
            s_Status = Path.GetFullPath(statusPath);
            Directory.CreateDirectory(Path.GetDirectoryName(s_Status));
            Directory.CreateDirectory(Path.GetDirectoryName(s_Output));
            RunBuild();
            return "finished";
        }

        private static void RunBuild()
        {
            s_Scheduled = false;
            DateTime started = DateTime.UtcNow;
            WriteStatus("running");
            try
            {
                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();

                // Bản dev dùng cho probe: CoopLobby phải là scene đầu để -iw-host/-iw-join tự chạy.
                if (s_Development)
                {
                    const string lobby = "Assets/Scenes/CoopLobby.unity";
                    if (scenes.Contains(lobby))
                    {
                        scenes = new[] { lobby }.Concat(scenes.Where(scene => scene != lobby)).ToArray();
                    }
                }

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = s_Output,
                    target = BuildTarget.StandaloneWindows64,
                    options = s_Development ? BuildOptions.Development : BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                TimeSpan elapsed = DateTime.UtcNow - started;
                long size = Directory.Exists(Path.GetDirectoryName(s_Output))
                    ? Directory.GetFiles(Path.GetDirectoryName(s_Output), "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
                    : 0L;
                string first = scenes.Length > 0 ? scenes[0] : "<none>";
                string firstError = string.Empty;
                foreach (BuildStep step in report.steps)
                {
                    foreach (BuildStepMessage message in step.messages)
                    {
                        if (message.type != LogType.Error && message.type != LogType.Exception) continue;
                        firstError = (message.content ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
                        if (firstError.Length > 400) firstError = firstError.Substring(0, 400);
                        break;
                    }
                    if (!string.IsNullOrEmpty(firstError)) break;
                }

                WriteStatus(string.Format(
                    "done result={0} errors={1} warnings={2} size={3} time={4} scenes={5} first={6} output={7} firstError={8}",
                    report.summary.result,
                    report.summary.totalErrors,
                    report.summary.totalWarnings,
                    size,
                    elapsed,
                    scenes.Length,
                    first,
                    report.summary.outputPath,
                    firstError));
            }
            catch (Exception exception)
            {
                WriteStatus("done result=Exception errors=1 message=" + exception.GetType().Name + ": " + exception.Message);
                Debug.LogException(exception);
            }
        }

        private static void WriteStatus(string text)
        {
            File.WriteAllText(s_Status, text + Environment.NewLine);
        }
    }
}
