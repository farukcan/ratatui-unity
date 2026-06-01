using System;
using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// Public facade and bootstrap entry point for the Ratatui developer console.
    /// All consumer-facing API surface lives here. The console GameObject is created
    /// automatically before the first scene loads.
    /// </summary>
    public static class RatatuiConsole
    {
        private static bool _booted;
        private static RatatuiConsoleConfig _config;
        private static ConsoleLogCapture _logs;
        private static ConsoleCommandRegistry _registry;
        private static ConsoleHistory _history;
        private static RatatuiConsoleRenderer _renderer;
        private static GameObject _go;

        public static RatatuiConsoleConfig Config => _config;
        public static ConsoleLogCapture Logs => _logs;
        public static ConsoleCommandRegistry Registry => _registry;
        public static ConsoleHistory History => _history;

        public static bool IsOpen => _renderer != null && _renderer.IsOpen;

        // ── Bootstrap ────────────────────────────────────────────────────────

        // Reset static fields first — Unity's "Enter Play Mode → Reload Domain = Off"
        // keeps statics alive between play sessions but destroys the GameObject and
        // clears the Application.logMessageReceivedThreaded subscription. Without this
        // reset, Bootstrap would early-return and the console would be silently dead.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_logs != null) _logs.Uninstall();
            _booted = false;
            _config = null;
            _logs = null;
            _registry = null;
            _history = null;
            _renderer = null;
            _go = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_booted) return;

#if !ENABLE_LEGACY_INPUT_MANAGER
            // Legacy Input Manager is disabled (project uses the new Input System only).
            // The base RatatuiRenderer and HandleToggleKey both rely on UnityEngine.Input,
            // which returns no events in this mode. Fail loudly instead of silently doing
            // nothing for the rest of the session.
            Debug.LogWarning(
                "[RatatuiConsole] Legacy Input Manager is disabled. The developer console " +
                "requires Player Settings → Active Input Handling = 'Both' or " +
                "'Input Manager (Old)'. Console will not start.");
            return;
#else
            _booted = true;

            _config = Resources.Load<RatatuiConsoleConfig>("RatatuiConsoleConfig");
            if (_config == null)
                _config = RatatuiConsoleConfig.CreateDefault();

            _logs = new ConsoleLogCapture(_config.maxLogEntries);
            _registry = new ConsoleCommandRegistry();
            _history = new ConsoleHistory(_config.maxHistoryEntries);

            _logs.Install();
            Application.quitting += OnApplicationQuitting;
            BuiltinCommands.Register();

            _go = new GameObject("Ratatui Unity Developer Console");
            _go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _renderer = _go.AddComponent<RatatuiConsoleRenderer>();
#endif
        }

        private static void OnApplicationQuitting()
        {
            // Symmetric uninstall: ensures we never leak handler subscriptions
            // when the editor shuts the play session down.
            if (_logs != null) _logs.Uninstall();
            Application.quitting -= OnApplicationQuitting;
        }

        // ── Command Registration ─────────────────────────────────────────────

        public static void RegisterCommand(string name, string description, Action<string[]> callback)
        {
            EnsureBooted();
            if (_registry == null) return;
            _registry.Register(name, description, callback);
        }

        public static void UnregisterCommand(string name)
        {
            EnsureBooted();
            if (_registry == null) return;
            _registry.Unregister(name);
        }

        public static void ExecuteCommand(string raw)
        {
            EnsureBooted();
            if (_registry == null) return;
            if (!ConsoleCommandRegistry.Parse(raw, out string name, out string[] args))
                return;

            Debug.Log("> " + raw);
            if (_registry.TryGet(name, out var cmd))
            {
                try { cmd.Callback(args); }
                catch (Exception ex)
                {
                    Debug.LogError($"Command '{name}' threw: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Debug.LogWarning($"Unknown command: '{name}'. Type 'help' for a list.");
            }
        }

        // ── Logs ─────────────────────────────────────────────────────────────

        public static void Log(string message)
        {
            EnsureBooted();
            _logs?.Append(ConsoleLogKind.Log, message, string.Empty);
        }

        public static void ClearLogs()
        {
            EnsureBooted();
            _logs?.Clear();
        }

        // ── Visibility ───────────────────────────────────────────────────────

        public static void Open()
        {
            if (_renderer != null) _renderer.SetOpen(true);
        }

        public static void Close()
        {
            if (_renderer != null) _renderer.SetOpen(false);
        }

        public static void Toggle()
        {
            if (_renderer != null) _renderer.SetOpen(!_renderer.IsOpen);
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private static void EnsureBooted()
        {
            if (_booted) return;
            Bootstrap();
        }
    }
}
