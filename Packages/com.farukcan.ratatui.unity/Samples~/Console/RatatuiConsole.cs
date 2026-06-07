using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// Public facade and bootstrap entry point for the Ratatui developer console.
    /// Console services (logs, commands, history) bootstrap here; the renderer is
    /// created by <see cref="RatatuiTerminalApps"/> via <see cref="RatatuiTerminalAppAttribute"/>.
    /// </summary>
    public static class RatatuiConsole
    {
        private const string AppId = "console";

        private static bool _servicesBooted;
        private static RatatuiConsoleConfig _config;
        private static ConsoleLogCapture _logs;
        private static ConsoleCommandRegistry _registry;
        private static ConsoleHistory _history;

        public static RatatuiConsoleConfig Config => _config;
        public static ConsoleLogCapture Logs => _logs;
        public static ConsoleCommandRegistry Registry => _registry;
        public static ConsoleHistory History => _history;

        /// <summary>All registered terminal apps from <see cref="RatatuiTerminalApps"/>.</summary>
        public static IReadOnlyList<TerminalAppHandle> TerminalApps => RatatuiTerminalApps.Apps;

        public static bool IsOpen => RatatuiTerminalApps.IsOpen(AppId);

        // ── Bootstrap ────────────────────────────────────────────────────────

        // Reset static fields first — Unity's "Enter Play Mode → Reload Domain = Off"
        // keeps statics alive between play sessions but destroys the GameObject and
        // clears the Application.logMessageReceivedThreaded subscription. Without this
        // reset, Bootstrap would early-return and the console would be silently dead.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_logs != null) _logs.Uninstall();
            _servicesBooted = false;
            _config = null;
            _logs = null;
            _registry = null;
            _history = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureServicesBooted();
        }

        private static void OnApplicationQuitting()
        {
            // Symmetric uninstall: ensures we never leak handler subscriptions
            // when the editor shuts the play session down.
            if (_logs != null) _logs.Uninstall();
            Application.quitting -= OnApplicationQuitting;
        }

        /// <summary>
        /// Ensures console services (config, logs, registry, history) are initialized.
        /// Called automatically at boot and from the renderer Awake when needed.
        /// Services start regardless of the Legacy Input Manager setting — log capture
        /// is useful even when the renderer cannot open. The renderer is blocked by
        /// <see cref="RatatuiTerminalApps"/> when Legacy Input is disabled.
        /// </summary>
        internal static void EnsureServicesBooted()
        {
            if (_servicesBooted) return;
            _servicesBooted = true;

            _config = Resources.Load<RatatuiConsoleConfig>("RatatuiConsoleConfig");
            if (_config == null)
                _config = RatatuiConsoleConfig.CreateDefault();

            _logs = new ConsoleLogCapture(_config.maxLogEntries);
            _registry = new ConsoleCommandRegistry();
            _history = new ConsoleHistory(_config.maxHistoryEntries);

            _logs.Install();
            Application.quitting += OnApplicationQuitting;
            BuiltinCommands.Register();
        }

        // ── Command Registration ─────────────────────────────────────────────

        public static void RegisterCommand(string name, string description, Action<string[]> callback)
        {
            EnsureServicesBooted();
            if (_registry == null) return;
            _registry.Register(name, description, callback);
        }

        public static void UnregisterCommand(string name)
        {
            EnsureServicesBooted();
            if (_registry == null) return;
            _registry.Unregister(name);
        }

        public static void ExecuteCommand(string raw)
        {
            EnsureServicesBooted();
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
            EnsureServicesBooted();
            _logs?.Append(ConsoleLogKind.Log, message, string.Empty);
        }

        public static void ClearLogs()
        {
            EnsureServicesBooted();
            _logs?.Clear();
        }

        // ── Visibility ───────────────────────────────────────────────────────

        public static void Open() => RatatuiTerminalApps.Open(AppId);

        public static void Close() => RatatuiTerminalApps.Close(AppId);

        public static void Toggle() => RatatuiTerminalApps.Toggle(AppId);
    }
}
