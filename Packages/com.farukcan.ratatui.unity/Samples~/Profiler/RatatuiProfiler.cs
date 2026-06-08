using UnityEngine;

namespace RatatuiUnity.Samples.Profiler
{
    /// <summary>
    /// Public facade and bootstrap entry point for the Ratatui Profiler terminal app.
    /// The renderer is created by <see cref="RatatuiTerminalApps"/> at boot.
    /// Read-only overlay — no state persistence, no editable input.
    /// Default toggle: F10.
    /// </summary>
    public static class RatatuiProfiler
    {
        private const string AppId = "profiler";

        private static bool _servicesBooted;
        private static RatatuiProfilerConfig _config;

        public static RatatuiProfilerConfig Config => _config;

        public static bool IsOpen => RatatuiTerminalApps.IsOpen(AppId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _servicesBooted = false;
            _config = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureServicesBooted();
        }

        internal static void EnsureServicesBooted()
        {
            if (_servicesBooted) return;
            _servicesBooted = true;

            _config = Resources.Load<RatatuiProfilerConfig>("RatatuiProfilerConfig");
            if (_config == null)
                _config = RatatuiProfilerConfig.CreateDefault();
        }

        public static void Open() => RatatuiTerminalApps.Open(AppId);

        public static void Close() => RatatuiTerminalApps.Close(AppId);

        public static void Toggle() => RatatuiTerminalApps.Toggle(AppId);
    }
}
