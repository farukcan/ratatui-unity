using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Static bootstrap and registry for terminal apps. Each app registers itself via
    /// <see cref="Register{T}"/> in a <c>[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]</c>
    /// static method, then this manager instantiates them all at <c>BeforeSceneLoad</c>.
    /// </summary>
    public static class RatatuiTerminalApps
    {
        private static bool _booted;
        private static readonly List<TerminalAppHandle> _apps = new List<TerminalAppHandle>();
        private static readonly Dictionary<string, TerminalAppHandle> _byId =
            new Dictionary<string, TerminalAppHandle>(StringComparer.OrdinalIgnoreCase);

        /// <summary>All registered terminal apps, sorted by Order after bootstrap.</summary>
        public static IReadOnlyList<TerminalAppHandle> Apps => _apps;

        /// <summary>True when bootstrap has completed.</summary>
        public static bool IsBooted => _booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _booted = false;
            _apps.Clear();
            _byId.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_booted) return;

#if !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogWarning(
                "[RatatuiTerminalApps] Legacy Input Manager is disabled. Terminal apps " +
                "require Player Settings → Active Input Handling = 'Both' or " +
                "'Input Manager (Old)'. Apps will not start.");
            _booted = true;
            return;
#else
            _apps.Sort((a, b) =>
            {
                int order = a.Order.CompareTo(b.Order);
                if (order != 0) return order;
                return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });

            InstantiateApps();
            _booted = true;
#endif
        }

        /// <summary>
        /// Register a terminal app. Call this from a
        /// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]</c>
        /// static method inside the app class so it is registered before bootstrap runs.
        /// </summary>
        public static void Register<T>(string id, string displayName = null, int order = 0)
            where T : RatatuiTerminalApp
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[RatatuiTerminalApps] Cannot register app with empty id.");
                return;
            }

            if (_byId.ContainsKey(id))
            {
                Debug.LogWarning($"[RatatuiTerminalApps] Duplicate app id '{id}'. Skipping.");
                return;
            }

            var handle = new TerminalAppHandle(id, displayName ?? id, order,
                go => go.AddComponent<T>());
            _apps.Add(handle);
            _byId[id] = handle;
        }

        /// <summary>
        /// Open the app with the given id. If already open, brings it to
        /// <see cref="RatatuiFocusManager"/> focus (keyboard input + window z-order).
        /// </summary>
        public static void Open(string id)
        {
            EnsureBooted();
            if (TryGetHandle(id, out var handle) && handle.Instance != null)
                handle.Instance.SetOpen(true);
        }

        /// <summary>Close the app with the given id.</summary>
        public static void Close(string id)
        {
            EnsureBooted();
            if (TryGetHandle(id, out var handle) && handle.Instance != null)
                handle.Instance.SetOpen(false);
        }

        /// <summary>Toggle the app with the given id.</summary>
        public static void Toggle(string id)
        {
            EnsureBooted();
            if (TryGetHandle(id, out var handle) && handle.Instance != null)
                handle.Instance.Toggle();
        }

        /// <summary>Return whether the app with the given id is open.</summary>
        public static bool IsOpen(string id)
        {
            EnsureBooted();
            return TryGetHandle(id, out var handle) && handle.IsOpen;
        }

        /// <summary>Get the live app instance by id, or null if not found.</summary>
        public static RatatuiTerminalApp Get(string id)
        {
            EnsureBooted();
            return TryGetHandle(id, out var handle) ? handle.Instance : null;
        }

        /// <summary>Get the live app instance by type, or null if not found.</summary>
        public static T Get<T>() where T : RatatuiTerminalApp
        {
            EnsureBooted();
            for (int i = 0; i < _apps.Count; i++)
            {
                if (_apps[i].Instance is T typed)
                    return typed;
            }
            return null;
        }

        /// <summary>
        /// Open the first app whose instance is of type <typeparamref name="T"/>.
        /// If already open, brings it to focus.
        /// </summary>
        public static void Open<T>() where T : RatatuiTerminalApp
        {
            var app = Get<T>();
            if (app != null) app.SetOpen(true);
        }

        /// <summary>Close the first app whose instance is of type <typeparamref name="T"/>.</summary>
        public static void Close<T>() where T : RatatuiTerminalApp
        {
            var app = Get<T>();
            if (app != null) app.SetOpen(false);
        }

        /// <summary>Toggle the first app whose instance is of type <typeparamref name="T"/>.</summary>
        public static void Toggle<T>() where T : RatatuiTerminalApp
        {
            var app = Get<T>();
            if (app != null) app.Toggle();
        }

        private static void EnsureBooted()
        {
            if (_booted) return;
            Bootstrap();
        }

        private static bool TryGetHandle(string id, out TerminalAppHandle handle)
        {
            handle = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            return _byId.TryGetValue(id, out handle);
        }

        private static void InstantiateApps()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                var handle = _apps[i];
                var go = new GameObject(handle.DisplayName);
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                handle.Instance = handle.Factory(go);
            }
        }
    }
}
