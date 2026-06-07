using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RatatuiUnity;
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

            RatatuiConsole.RegisterCommand("prefs", "PlayerPrefs access: prefs get|set|del|clear|save [key] [value].", PrefsCmd);
            RatatuiConsole.RegisterCommand("scene_load", "Load a scene by name or build index: scene_load <name|index> [additive].", SceneLoadCmd);
            RatatuiConsole.RegisterCommand("scene_reload", "Reload the active scene.", SceneReloadCmd);
            RatatuiConsole.RegisterCommand("tree", "Print scene hierarchy: tree [path] [depth]. Default depth 3.", TreeCmd);
            RatatuiConsole.RegisterCommand("pwd", "Print current hierarchy path.", PwdCmd);
            RatatuiConsole.RegisterCommand("cd", "Change current hierarchy path: cd <path|..|/>. No arg → root.", CdCmd);
            RatatuiConsole.RegisterCommand("ls", "List children of current path or given path: ls [path].", LsCmd);
            RatatuiConsole.RegisterCommand("cat", "Print GameObject details: cat [path].", CatCmd);
            RatatuiConsole.RegisterCommand("rm", "Destroy a GameObject: rm <path>.", RmCmd);
            RatatuiConsole.RegisterCommand("mv", "Move/rename a GameObject: mv <src> <dest>. Existing dest → move into it; else rename.", MvCmd);
            RatatuiConsole.RegisterCommand("cp", "Clone a GameObject: cp <src> <dest>. Existing dest → clone into it; else rename clone.", CpCmd);
            RatatuiConsole.RegisterCommand("enable", "SetActive(true): enable <path>.", EnableCmd);
            RatatuiConsole.RegisterCommand("disable", "SetActive(false): disable <path>.", DisableCmd);
            RatatuiConsole.RegisterCommand("toggle", "Flip activeSelf: toggle <path>.", ToggleCmd);

            RegisterTerminalAppCommands();
        }

        private static void RegisterTerminalAppCommands()
        {
            var apps = RatatuiTerminalApps.Apps;
            for (int i = 0; i < apps.Count; i++)
            {
                var app = apps[i];
                string id = app.Id;
                string label = app.DisplayName;

                RatatuiConsole.RegisterCommand(
                    "open_" + id,
                    "Open the " + label + " terminal app.",
                    _ =>
                    {
                        RatatuiTerminalApps.Open(id);
                        Reply(label + " opened.");
                    });

                RatatuiConsole.RegisterCommand(
                    "close_" + id,
                    "Close the " + label + " terminal app.",
                    _ =>
                    {
                        RatatuiTerminalApps.Close(id);
                        Reply(label + " closed.");
                    });
            }
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
            var refreshRatio = Screen.currentResolution.refreshRateRatio;
            double hz = refreshRatio.denominator == 0
                ? 0.0
                : (double)refreshRatio.numerator / refreshRatio.denominator;
            sb.Append('@').Append(System.Math.Round(hz, 2)).Append("Hz");
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

        private static void PrefsCmd(string[] args)
        {
            if (args.Length == 0)
            {
                Debug.LogWarning("Usage: prefs get|set|del|clear|save <key> [value]");
                return;
            }

            string sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "get":
                    PrefsGet(args);
                    return;
                case "set":
                    PrefsSet(args);
                    return;
                case "del":
                case "delete":
                    PrefsDelete(args);
                    return;
                case "clear":
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    Reply("PlayerPrefs cleared.");
                    return;
                case "save":
                    PlayerPrefs.Save();
                    Reply("PlayerPrefs saved.");
                    return;
                default:
                    Debug.LogWarning("Unknown prefs subcommand: " + sub);
                    return;
            }
        }

        private static void PrefsGet(string[] args)
        {
            if (args.Length < 2)
            {
                Debug.LogWarning("Usage: prefs get <key>");
                return;
            }
            string key = args[1];
            if (!PlayerPrefs.HasKey(key))
            {
                Reply("(not set) " + key);
                return;
            }
            // PlayerPrefs is typed but untagged — probe in priority order.
            string asString = PlayerPrefs.GetString(key, null);
            if (!string.IsNullOrEmpty(asString))
            {
                Reply(key + " = \"" + asString + "\" (string)");
                return;
            }
            float asFloat = PlayerPrefs.GetFloat(key, float.NaN);
            if (!float.IsNaN(asFloat))
            {
                Reply(key + " = " + asFloat.ToString("G", CultureInfo.InvariantCulture) + " (float)");
                return;
            }
            int asInt = PlayerPrefs.GetInt(key, 0);
            Reply(key + " = " + asInt.ToString(CultureInfo.InvariantCulture) + " (int)");
        }

        private static void PrefsSet(string[] args)
        {
            if (args.Length < 3)
            {
                Debug.LogWarning("Usage: prefs set <key> <value>");
                return;
            }
            string key = args[1];
            string value = args[2];

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                PlayerPrefs.SetInt(key, i);
                PlayerPrefs.Save();
                Reply(key + " = " + i + " (int)");
                return;
            }
            if (TryParseFloat(value, out float f))
            {
                PlayerPrefs.SetFloat(key, f);
                PlayerPrefs.Save();
                Reply(key + " = " + f.ToString("G", CultureInfo.InvariantCulture) + " (float)");
                return;
            }
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            Reply(key + " = \"" + value + "\" (string)");
        }

        private static void PrefsDelete(string[] args)
        {
            if (args.Length < 2)
            {
                Debug.LogWarning("Usage: prefs del <key>");
                return;
            }
            string key = args[1];
            if (!PlayerPrefs.HasKey(key))
            {
                Reply("(not set) " + key);
                return;
            }
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Reply("Deleted: " + key);
        }

        private static void SceneLoadCmd(string[] args)
        {
            if (args.Length == 0)
            {
                Debug.LogWarning("Usage: scene_load <name|index> [additive]");
                return;
            }

            bool additive = args.Length >= 2 &&
                args[1].Equals("additive", StringComparison.OrdinalIgnoreCase);
            var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;

            if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int buildIndex))
            {
                if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.LogWarning("Build index out of range: " + buildIndex
                        + " (0.." + (SceneManager.sceneCountInBuildSettings - 1) + ")");
                    return;
                }
                SceneManager.LoadScene(buildIndex, mode);
                Reply("Loading scene #" + buildIndex + " (" + mode + ")");
                return;
            }

            string name = args[0];
            if (!Application.CanStreamedLevelBeLoaded(name))
            {
                Debug.LogWarning("Scene not found in build settings: " + name);
                return;
            }
            SceneManager.LoadScene(name, mode);
            Reply("Loading scene \"" + name + "\" (" + mode + ")");
        }

        private static void SceneReloadCmd(string[] args)
        {
            var active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex, LoadSceneMode.Single);
            Reply("Reloading scene: " + active.name);
        }

        private const int TreeDefaultDepth = 3;
        private const int TreeMaxNodes = 500;

        private static void TreeCmd(string[] args)
        {
            // Parse args: [path] [depth]. Either, both, or neither.
            string path = null;
            int depth = TreeDefaultDepth;

            if (args.Length == 1)
            {
                if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int d))
                    depth = d;
                else
                    path = args[0];
            }
            else if (args.Length >= 2)
            {
                path = args[0];
                if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out depth))
                {
                    Debug.LogWarning("Usage: tree [path] [depth]");
                    return;
                }
            }

            if (depth < 0) depth = 0;

            var sb = new StringBuilder(512);
            int nodeBudget = TreeMaxNodes;

            string startPath = string.IsNullOrEmpty(path) ? _cwd : ResolvePath(path);

            if (startPath == "/")
            {
                var active = SceneManager.GetActiveScene();
                var roots = active.GetRootGameObjects();
                sb.Append(active.name).Append(" (root, ").Append(roots.Length).Append(')');
                for (int i = 0; i < roots.Length; i++)
                {
                    bool isLast = i == roots.Length - 1;
                    AppendNode(sb, roots[i].transform, string.Empty, isLast, depth, ref nodeBudget);
                    if (nodeBudget <= 0) break;
                }
            }
            else
            {
                var root = FindTransformByAbsolutePath(startPath);
                if (root == null)
                {
                    Debug.LogWarning("GameObject not found: " + startPath);
                    return;
                }
                AppendNode(sb, root, string.Empty, true, depth, ref nodeBudget);
            }

            if (nodeBudget <= 0)
                sb.Append("\n… truncated (").Append(TreeMaxNodes).Append(" node limit)");

            Reply(sb.ToString());
        }

        private static void AppendNode(
            StringBuilder sb,
            Transform t,
            string prefix,
            bool isLast,
            int remainingDepth,
            ref int nodeBudget)
        {
            if (nodeBudget <= 0) return;
            nodeBudget--;

            sb.Append('\n').Append(prefix);
            sb.Append(isLast ? "└─ " : "├─ ");
            AppendNodeLabel(sb, t);

            if (remainingDepth <= 0)
            {
                if (t.childCount > 0)
                    sb.Append(" …(").Append(t.childCount).Append(')');
                return;
            }

            string childPrefix = prefix + (isLast ? "   " : "│  ");
            int count = t.childCount;
            for (int i = 0; i < count; i++)
            {
                if (nodeBudget <= 0) return;
                AppendNode(sb, t.GetChild(i), childPrefix, i == count - 1, remainingDepth - 1, ref nodeBudget);
            }
        }

        private static void AppendNodeLabel(StringBuilder sb, Transform t)
        {
            sb.Append(t.name);
            if (!t.gameObject.activeInHierarchy) sb.Append('*');

            var components = t.GetComponents<Component>();
            if (components.Length == 0) return;

            sb.Append(" (");
            bool first = true;
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue; // missing script
                if (!first) sb.Append(", ");
                sb.Append(c.GetType().Name);
                first = false;
            }
            sb.Append(')');
        }

        private static Transform FindTransformByPath(string path)
        {
            string abs = ResolvePath(path);
            return FindTransformByAbsolutePath(abs);
        }

        // Filesystem-style navigation state. Absolute path; "/" = virtual scene root.
        private static string _cwd = "/";

        public static string Cwd => _cwd;

        /// <summary>
        /// Tab-completion for the last whitespace-delimited token in <paramref name="buffer"/>.
        /// Quote-aware: an unmatched <c>"</c> opens a quoted span where whitespace is part of
        /// the token. Inserted paths quote any segment containing a space so the tokenizer
        /// reassembles the full name.
        /// </summary>
        public static List<ConsoleSuggestion> CompletePath(string buffer, int maxResults)
        {
            var results = new List<ConsoleSuggestion>();
            if (buffer == null) return results;

            int tokenStart = FindLastTokenStart(buffer);
            string raw = tokenStart >= buffer.Length ? string.Empty : buffer.Substring(tokenStart);
            string token = StripQuotes(raw);

            int lastSlash = token.LastIndexOf('/');
            string dirToken = lastSlash < 0 ? string.Empty : token.Substring(0, lastSlash);
            string namePrefix = lastSlash < 0 ? token : token.Substring(lastSlash + 1);
            string prefixPortion = lastSlash < 0 ? string.Empty : token.Substring(0, lastSlash + 1);

            string absDir;
            if (dirToken.Length == 0)
                absDir = lastSlash == 0 ? "/" : _cwd;
            else
                absDir = ResolvePath(dirToken);

            Transform parent = null;
            GameObject[] roots = null;
            int childCount;
            if (absDir == "/")
            {
                roots = SceneManager.GetActiveScene().GetRootGameObjects();
                childCount = roots.Length;
            }
            else
            {
                parent = FindTransformByAbsolutePath(absDir);
                if (parent == null) return results;
                childCount = parent.childCount;
            }

            for (int i = 0; i < childCount && results.Count < maxResults; i++)
            {
                Transform child = parent != null ? parent.GetChild(i) : roots[i].transform;
                if (namePrefix.Length > 0 &&
                    !child.name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool hasChildren = child.childCount > 0;
                string display = hasChildren ? child.name + "/" : child.name;
                string detail = ComponentSummary(child);
                string combined = prefixPortion + child.name;
                string insert = QuotePathSegments(combined);
                if (hasChildren) insert += "/";
                results.Add(new ConsoleSuggestion(display, detail, insert, tokenStart, !hasChildren));
            }
            return results;
        }

        // Walk the buffer with the same quote rules as ConsoleCommandRegistry.Tokenize
        // to find where the current (in-progress) token begins. Returns buffer.Length
        // when the buffer ends in unquoted whitespace (empty current token).
        private static int FindLastTokenStart(string buffer)
        {
            int start = buffer.Length;
            bool inQuotes = false;
            bool inToken = false;
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (c == '"')
                {
                    if (!inToken) { start = i; inToken = true; }
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    inToken = false;
                    continue;
                }
                if (!inToken) { start = i; inToken = true; }
            }
            return inToken ? start : buffer.Length;
        }

        private static string StripQuotes(string s)
        {
            if (s.IndexOf('"') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
                if (s[i] != '"') sb.Append(s[i]);
            return sb.ToString();
        }

        // Quote any path segment containing a space. Slashes stay outside the quotes
        // so the tokenizer can still see the segment boundaries when reassembling.
        private static string QuotePathSegments(string path)
        {
            if (path.IndexOf(' ') < 0) return path;
            var parts = path.Split('/');
            var sb = new StringBuilder(path.Length + 8);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('/');
                if (parts[i].IndexOf(' ') >= 0)
                    sb.Append('"').Append(parts[i]).Append('"');
                else
                    sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        private static string ComponentSummary(Transform t)
        {
            var components = t.GetComponents<Component>();
            if (components.Length == 0) return string.Empty;
            var sb = new StringBuilder(64);
            int shown = 0;
            for (int i = 0; i < components.Length && shown < 3; i++)
            {
                var c = components[i];
                if (c == null) continue;
                if (shown > 0) sb.Append(", ");
                sb.Append(c.GetType().Name);
                shown++;
            }
            if (components.Length > shown) sb.Append(", …");
            return sb.ToString();
        }

        private static void PwdCmd(string[] args)
        {
            Reply(_cwd);
        }

        private static void CdCmd(string[] args)
        {
            if (args.Length == 0)
            {
                _cwd = "/";
                Reply(_cwd);
                return;
            }

            string target = ResolvePath(args[0]);
            if (target != "/" && FindTransformByAbsolutePath(target) == null)
            {
                Debug.LogWarning("Path not found: " + target);
                return;
            }
            _cwd = target;
            Reply(_cwd);
        }

        private static void LsCmd(string[] args)
        {
            string target = args.Length == 0 ? _cwd : ResolvePath(args[0]);
            var sb = new StringBuilder(256);

            if (target == "/")
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                if (roots.Length == 0) { Reply("(empty)"); return; }
                for (int i = 0; i < roots.Length; i++)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    AppendNodeLabel(sb, roots[i].transform);
                }
                Reply(sb.ToString());
                return;
            }

            var t = FindTransformByAbsolutePath(target);
            if (t == null)
            {
                Debug.LogWarning("Path not found: " + target);
                return;
            }
            if (t.childCount == 0)
            {
                Reply("(no children)");
                return;
            }
            for (int i = 0; i < t.childCount; i++)
            {
                if (sb.Length > 0) sb.Append('\n');
                AppendNodeLabel(sb, t.GetChild(i));
            }
            Reply(sb.ToString());
        }

        // Resolve a path relative to _cwd into a normalized absolute path.
        // "/A/B" → absolute. "A/B" → joined with _cwd. ".." pops, "." skipped.
        private static string ResolvePath(string input)
        {
            bool absolute = input.StartsWith("/", StringComparison.Ordinal);
            var stack = new List<string>();

            if (!absolute && _cwd != "/")
            {
                var cwdParts = _cwd.Split('/');
                for (int i = 0; i < cwdParts.Length; i++)
                    if (cwdParts[i].Length > 0) stack.Add(cwdParts[i]);
            }

            var parts = input.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0 || p == ".") continue;
                if (p == "..")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                stack.Add(p);
            }

            if (stack.Count == 0) return "/";
            var sb = new StringBuilder(64);
            for (int i = 0; i < stack.Count; i++)
            {
                sb.Append('/').Append(stack[i]);
            }
            return sb.ToString();
        }

        private static Transform FindTransformByAbsolutePath(string absPath)
        {
            if (absPath == "/" || string.IsNullOrEmpty(absPath)) return null;

            // Strip leading slash, split.
            var parts = absPath.Substring(1).Split('/');
            if (parts.Length == 0) return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Transform current = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == parts[0])
                {
                    current = roots[i].transform;
                    break;
                }
            }
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                Transform next = null;
                for (int c = 0; c < current.childCount; c++)
                {
                    var child = current.GetChild(c);
                    if (child.name == parts[i])
                    {
                        next = child;
                        break;
                    }
                }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static void CatCmd(string[] args)
        {
            string target = args.Length == 0 ? _cwd : ResolvePath(args[0]);
            if (target == "/")
            {
                Debug.LogWarning("cat: cannot inspect virtual root. Use ls /.");
                return;
            }

            var t = FindTransformByAbsolutePath(target);
            if (t == null)
            {
                Debug.LogWarning("Path not found: " + target);
                return;
            }

            var go = t.gameObject;
            var sb = new StringBuilder(384);

            sb.Append(go.name);
            sb.Append("  (").Append(go.activeInHierarchy ? "active" : "inactive").Append(')');

            sb.Append("\nPath: ").Append(target);

            string layerName = LayerMask.LayerToName(go.layer);
            if (string.IsNullOrEmpty(layerName)) layerName = go.layer.ToString(CultureInfo.InvariantCulture);
            sb.Append("\nScene: ").Append(go.scene.name);
            sb.Append(" · layer: ").Append(layerName);
            sb.Append(" · tag: ").Append(go.tag);
            sb.Append(" · static: ").Append(go.isStatic ? "true" : "false");

            sb.Append("\nActive: self=").Append(go.activeSelf ? "true" : "false");
            sb.Append(" · hierarchy=").Append(go.activeInHierarchy ? "true" : "false");

            var p = t.localPosition;
            var r = t.localEulerAngles;
            var s = t.localScale;
            sb.Append("\nTransform: pos(")
                .Append(p.x.ToString("F2", CultureInfo.InvariantCulture)).Append(", ")
                .Append(p.y.ToString("F2", CultureInfo.InvariantCulture)).Append(", ")
                .Append(p.z.ToString("F2", CultureInfo.InvariantCulture)).Append(") rot(")
                .Append(r.x.ToString("F1", CultureInfo.InvariantCulture)).Append(", ")
                .Append(r.y.ToString("F1", CultureInfo.InvariantCulture)).Append(", ")
                .Append(r.z.ToString("F1", CultureInfo.InvariantCulture)).Append(") scale(")
                .Append(s.x.ToString("F2", CultureInfo.InvariantCulture)).Append(", ")
                .Append(s.y.ToString("F2", CultureInfo.InvariantCulture)).Append(", ")
                .Append(s.z.ToString("F2", CultureInfo.InvariantCulture)).Append(')');

            sb.Append("\nParent: ").Append(t.parent == null ? "/" : GetAbsolutePath(t.parent));

            sb.Append("\nChildren (").Append(t.childCount).Append(')');
            if (t.childCount > 0)
            {
                sb.Append(": ");
                for (int i = 0; i < t.childCount; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(t.GetChild(i).name);
                }
            }

            var components = t.GetComponents<Component>();
            sb.Append("\nComponents (").Append(components.Length).Append(')');
            if (components.Length > 0)
            {
                sb.Append(": ");
                bool first = true;
                for (int i = 0; i < components.Length; i++)
                {
                    var c = components[i];
                    if (c == null) continue;
                    if (!first) sb.Append(", ");
                    sb.Append(c.GetType().Name);
                    first = false;
                }
            }

            Reply(sb.ToString());
        }

        private static void RmCmd(string[] args)
        {
            if (args.Length == 0)
            {
                Debug.LogWarning("Usage: rm <path>");
                return;
            }
            string target = ResolvePath(args[0]);
            if (target == "/")
            {
                Debug.LogWarning("rm: refusing to destroy virtual root.");
                return;
            }
            var t = FindTransformByAbsolutePath(target);
            if (t == null)
            {
                Debug.LogWarning("Path not found: " + target);
                return;
            }

            string name = t.name;
            DestroyGameObject(t.gameObject);
            Reply("Destroyed: " + target + " (" + name + ")");
        }

        private static void MvCmd(string[] args)
        {
            if (args.Length < 2)
            {
                Debug.LogWarning("Usage: mv <src> <dest>");
                return;
            }
            string fromPath = ResolvePath(args[0]);
            string destPath = ResolvePath(args[1]);
            if (fromPath == "/")
            {
                Debug.LogWarning("mv: cannot move virtual root.");
                return;
            }

            var from = FindTransformByAbsolutePath(fromPath);
            if (from == null)
            {
                Debug.LogWarning("Path not found: " + fromPath);
                return;
            }

            if (!ResolveDest(destPath, from.name, out Transform newParent, out string finalName))
            {
                Debug.LogWarning("mv: destination parent not found: " + destPath);
                return;
            }

            // Cycle: newParent must not be from or any descendant of from.
            for (var p = newParent; p != null; p = p.parent)
            {
                if (p == from)
                {
                    Debug.LogWarning("mv: cannot reparent under self or own descendant.");
                    return;
                }
            }

            from.SetParent(newParent, true);
            from.name = finalName;
            Reply("Moved " + fromPath + " → " + FormatDestPath(newParent, finalName));
        }

        private static void CpCmd(string[] args)
        {
            if (args.Length < 2)
            {
                Debug.LogWarning("Usage: cp <src> <dest>");
                return;
            }
            string fromPath = ResolvePath(args[0]);
            string destPath = ResolvePath(args[1]);
            if (fromPath == "/")
            {
                Debug.LogWarning("cp: cannot clone virtual root.");
                return;
            }

            var from = FindTransformByAbsolutePath(fromPath);
            if (from == null)
            {
                Debug.LogWarning("Path not found: " + fromPath);
                return;
            }

            if (!ResolveDest(destPath, from.name, out Transform parent, out string finalName))
            {
                Debug.LogWarning("cp: destination parent not found: " + destPath);
                return;
            }

            var clone = UnityEngine.Object.Instantiate(from.gameObject, parent);
            clone.name = finalName;
            Reply("Cloned " + fromPath + " → " + FormatDestPath(parent, finalName));
        }

        // POSIX cp/mv destination semantics:
        //   - If destPath exists as a GameObject → treat as target directory, keep source name.
        //   - Else → split into parent path + new name; parent must exist.
        //   - destPath "/" → scene root with source name.
        private static bool ResolveDest(string destPath, string srcName, out Transform parent, out string finalName)
        {
            parent = null;
            finalName = null;

            if (destPath == "/")
            {
                finalName = srcName;
                return true;
            }

            var existing = FindTransformByAbsolutePath(destPath);
            if (existing != null)
            {
                parent = existing;
                finalName = srcName;
                return true;
            }

            int lastSlash = destPath.LastIndexOf('/');
            string parentPath = lastSlash <= 0 ? "/" : destPath.Substring(0, lastSlash);
            string newName = destPath.Substring(lastSlash + 1);
            if (string.IsNullOrEmpty(newName)) return false;

            if (parentPath == "/")
            {
                finalName = newName;
                return true;
            }

            parent = FindTransformByAbsolutePath(parentPath);
            if (parent == null) return false;
            finalName = newName;
            return true;
        }

        private static string FormatDestPath(Transform parent, string name)
        {
            if (parent == null) return "/" + name;
            return GetAbsolutePath(parent) + "/" + name;
        }

        private static void EnableCmd(string[] args) => SetActiveCmd(args, true, "enable");
        private static void DisableCmd(string[] args) => SetActiveCmd(args, false, "disable");

        private static void ToggleCmd(string[] args)
        {
            if (args.Length == 0)
            {
                Debug.LogWarning("Usage: toggle <path>");
                return;
            }
            string target = ResolvePath(args[0]);
            var t = FindTransformByAbsolutePath(target);
            if (t == null)
            {
                Debug.LogWarning("Path not found: " + target);
                return;
            }
            bool next = !t.gameObject.activeSelf;
            t.gameObject.SetActive(next);
            Reply(target + " activeSelf → " + (next ? "true" : "false"));
        }

        private static void SetActiveCmd(string[] args, bool value, string verb)
        {
            if (args.Length == 0)
            {
                Debug.LogWarning("Usage: " + verb + " <path>");
                return;
            }
            string target = ResolvePath(args[0]);
            var t = FindTransformByAbsolutePath(target);
            if (t == null)
            {
                Debug.LogWarning("Path not found: " + target);
                return;
            }
            t.gameObject.SetActive(value);
            Reply(target + " activeSelf → " + (value ? "true" : "false"));
        }

        private static void DestroyGameObject(GameObject go)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }

        private static string GetAbsolutePath(Transform t)
        {
            if (t == null) return "/";
            var sb = new StringBuilder(64);
            var stack = new List<string>();
            for (var c = t; c != null; c = c.parent)
                stack.Add(c.name);
            for (int i = stack.Count - 1; i >= 0; i--)
                sb.Append('/').Append(stack[i]);
            return sb.ToString();
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
