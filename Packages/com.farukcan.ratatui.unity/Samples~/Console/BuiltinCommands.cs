using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// Registers the always-available console commands.
    /// </summary>
    internal static class BuiltinCommands
    {
        private static float _timeScaleBeforePause = 1f;

        public static void Register()
        {
            RatatuiConsole.RegisterCommand("help", "List all registered commands.", HelpCmd);
            RatatuiConsole.RegisterCommand("clear", "Clear the log buffer.", ClearCmd);
            RatatuiConsole.RegisterCommand("quit", "Exit the application.", QuitCmd);
            RatatuiConsole.RegisterCommand("echo", "Print arguments back to the console.", EchoCmd);
            RatatuiConsole.RegisterCommand("log_warning", "Emit a Unity warning log.", LogWarningCmd);
            RatatuiConsole.RegisterCommand("log_error", "Emit a Unity error log.", LogErrorCmd);
            RatatuiConsole.RegisterCommand("log_exception", "Emit a Unity exception log.", LogExceptionCmd);

            RatatuiConsole.RegisterCommand("version", "Print Unity and application version info.", VersionCmd);
            RatatuiConsole.RegisterCommand("fps", "Print current frame rate and timing stats.", FpsCmd);
            RatatuiConsole.RegisterCommand("scene", "Print active and loaded scene info.", SceneCmd);
            RatatuiConsole.RegisterCommand("time_scale", "Get or set Time.timeScale (e.g. time_scale 0.5).", TimeScaleCmd);
            RatatuiConsole.RegisterCommand("target_fps", "Get or set Application.targetFrameRate.", TargetFpsCmd);
            RatatuiConsole.RegisterCommand("sysinfo", "Print platform and device information.", SysInfoCmd);
            RatatuiConsole.RegisterCommand("pause", "Pause the game (time scale 0).", PauseCmd);
            RatatuiConsole.RegisterCommand("resume", "Resume the game (restore time scale).", ResumeCmd);
            RatatuiConsole.RegisterCommand("gc", "Force a garbage collection.", GcCmd);
        }

        private static void HelpCmd(string[] args)
        {
            var commands = RatatuiConsole.Registry.SortedAll();
            var sb = new StringBuilder(256);
            sb.Append("Registered commands (").Append(commands.Count).Append("):");
            foreach (var cmd in commands)
            {
                sb.Append('\n').Append("  ").Append(cmd.Name);
                if (!string.IsNullOrEmpty(cmd.Description))
                    sb.Append("  -  ").Append(cmd.Description);
            }
            Reply(sb.ToString());
        }

        private static void ClearCmd(string[] args)
        {
            RatatuiConsole.ClearLogs();
        }

        private static void QuitCmd(string[] args)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void EchoCmd(string[] args)
        {
            Reply(JoinArgs(args));
        }

        private static void LogWarningCmd(string[] args)
        {
            Debug.LogWarning(JoinArgs(args));
        }

        private static void LogErrorCmd(string[] args)
        {
            Debug.LogError(JoinArgs(args));
        }

        private static void LogExceptionCmd(string[] args)
        {
            Debug.LogException(new Exception(JoinArgs(args)));
        }

        private static void VersionCmd(string[] args)
        {
            var sb = new StringBuilder(128);
            sb.Append("Unity ").Append(Application.unityVersion);
            sb.Append('\n').Append(Application.productName);
            if (!string.IsNullOrEmpty(Application.version))
                sb.Append(" v").Append(Application.version);
            sb.Append('\n').Append(Application.platform);
            sb.Append(" · ").Append(Application.isEditor ? "Editor" : "Player");
            Reply(sb.ToString());
        }

        private static void FpsCmd(string[] args)
        {
            float unscaledDt = Time.unscaledDeltaTime;
            float fps = unscaledDt > 0f ? 1f / unscaledDt : 0f;
            var sb = new StringBuilder(128);
            sb.Append("FPS: ").Append(fps.ToString("F1", CultureInfo.InvariantCulture));
            sb.Append(" (frame ").Append(Time.frameCount).Append(')');
            sb.Append("\ndeltaTime: ").Append(Time.deltaTime.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(" · unscaled: ").Append(Time.unscaledDeltaTime.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append("\ntimeScale: ").Append(Time.timeScale.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(" · targetFrameRate: ").Append(FormatTargetFps(Application.targetFrameRate));
            Reply(sb.ToString());
        }

        private static void SceneCmd(string[] args)
        {
            var active = SceneManager.GetActiveScene();
            var sb = new StringBuilder(128);
            sb.Append("Active: ").Append(active.name);
            sb.Append(" (buildIndex ").Append(active.buildIndex).Append(')');
            sb.Append("\nLoaded scenes: ").Append(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                sb.Append("\n  [").Append(i).Append("] ").Append(scene.name);
                if (!scene.isLoaded) sb.Append(" (not loaded)");
            }
            Reply(sb.ToString());
        }

        private static void TimeScaleCmd(string[] args)
        {
            if (args.Length == 0)
            {
                Reply("timeScale: " + Time.timeScale.ToString("G", CultureInfo.InvariantCulture));
                return;
            }

            if (!TryParseFloat(args[0], out float scale))
            {
                Debug.LogWarning("Usage: time_scale [value]  (e.g. time_scale 0.5)");
                return;
            }

            Time.timeScale = Mathf.Max(0f, scale);
            Reply("timeScale set to " + Time.timeScale.ToString("G", CultureInfo.InvariantCulture));
        }

        private static void TargetFpsCmd(string[] args)
        {
            if (args.Length == 0)
            {
                Reply("targetFrameRate: " + FormatTargetFps(Application.targetFrameRate));
                return;
            }

            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
            {
                Debug.LogWarning("Usage: target_fps [value]  (e.g. target_fps 60, target_fps -1 for unlimited)");
                return;
            }

            Application.targetFrameRate = fps;
            Reply("targetFrameRate set to " + FormatTargetFps(Application.targetFrameRate));
        }

        private static void SysInfoCmd(string[] args)
        {
            var sb = new StringBuilder(384);
            sb.Append("Unity ").Append(Application.unityVersion);
            sb.Append('\n').Append(SystemInfo.operatingSystem);
            sb.Append("\nDevice: ").Append(SystemInfo.deviceModel);
            sb.Append(" (").Append(SystemInfo.deviceType).Append(')');
            sb.Append("\nCPU: ").Append(SystemInfo.processorType);
            sb.Append(" ×").Append(SystemInfo.processorCount);
            sb.Append("\nGPU: ").Append(SystemInfo.graphicsDeviceName);
            sb.Append(" (").Append(SystemInfo.graphicsDeviceType).Append(')');
            sb.Append("\nMemory: ").Append(FormatBytes(SystemInfo.systemMemorySize * 1024L * 1024L));
            sb.Append(" system · ").Append(FormatBytes(GC.GetTotalMemory(false)));
            sb.Append(" managed");
            sb.Append("\nScreen: ").Append(Screen.width).Append('×').Append(Screen.height);
            sb.Append('@').Append(Screen.currentResolution.refreshRate).Append("Hz");
            sb.Append("\nPlatform: ").Append(Application.platform);
            sb.Append(" · ").Append(Application.isMobilePlatform ? "mobile" : "desktop");
            sb.Append(" · ").Append(Application.internetReachability);
            Reply(sb.ToString());
        }

        private static void PauseCmd(string[] args)
        {
            if (Time.timeScale > 0f)
                _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            Reply("Paused (timeScale 0).");
        }

        private static void ResumeCmd(string[] args)
        {
            Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            Reply("Resumed (timeScale " + Time.timeScale.ToString("G", CultureInfo.InvariantCulture) + ").");
        }

        private static void GcCmd(string[] args)
        {
            long before = GC.GetTotalMemory(false);
            GC.Collect();
            long after = GC.GetTotalMemory(true);
            Reply("GC complete. Managed memory: " + FormatBytes(before) + " → " + FormatBytes(after));
        }

        private static void Reply(string message) => Debug.Log(message);

        private static string JoinArgs(string[] args) => string.Join(" ", args);

        private static bool TryParseFloat(string s, out float value) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static string FormatTargetFps(int fps) =>
            fps <= 0 ? "unlimited" : fps.ToString(CultureInfo.InvariantCulture);

        private static string FormatBytes(long bytes)
        {
            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;
            if (bytes >= gb) return (bytes / gb).ToString("F2", CultureInfo.InvariantCulture) + " GB";
            if (bytes >= mb) return (bytes / mb).ToString("F2", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= kb) return (bytes / kb).ToString("F2", CultureInfo.InvariantCulture) + " KB";
            return bytes + " B";
        }
    }
}
