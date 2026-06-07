using System;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Registry entry describing one registered terminal app and its live instance.
    /// </summary>
    public sealed class TerminalAppHandle
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public RatatuiTerminalApp Instance { get; internal set; }

        internal Func<GameObject, RatatuiTerminalApp> Factory { get; }

        /// <summary>True when the app instance exists and is currently open.</summary>
        public bool IsOpen => Instance != null && Instance.IsOpen;

        internal TerminalAppHandle(
            string id,
            string displayName,
            int order,
            Func<GameObject, RatatuiTerminalApp> factory)
        {
            Id = id;
            DisplayName = displayName ?? id;
            Order = order;
            Factory = factory;
        }
    }
}
