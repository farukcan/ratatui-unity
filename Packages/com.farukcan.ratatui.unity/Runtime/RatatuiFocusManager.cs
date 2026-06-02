using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Scene-wide focus arbitration for multiple <see cref="RatatuiRenderer"/> instances.
    /// Only the focused renderer receives keyboard input. Mouse hover/click is unaffected:
    /// raycast and rect hit-testing already route mouse events to the correct renderer.
    /// Focus changes on mouse-down inside a renderer (click-to-focus).
    /// </summary>
    public static class RatatuiFocusManager
    {
        private static readonly List<RatatuiRenderer> _registered = new List<RatatuiRenderer>();
        private static RatatuiRenderer _focused;

        /// <summary>The currently focused renderer, or null if none.</summary>
        public static RatatuiRenderer Focused => _focused;

        /// <summary>All renderers currently enabled in the scene. Used for input occlusion checks.</summary>
        internal static IReadOnlyList<RatatuiRenderer> Registered => _registered;

        /// <summary>Fires when focus changes. First arg: previous focused (may be null). Second: new focused (may be null).</summary>
        public static event Action<RatatuiRenderer, RatatuiRenderer> FocusChanged;

        // Reset static state on subsystem registration so domain-reload-disabled
        // play sessions don't carry stale references from a previous run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _registered.Clear();
            _focused = null;
            FocusChanged = null;
        }

        internal static void Register(RatatuiRenderer r)
        {
            if (r == null) return;
            if (_registered.Contains(r)) return;
            _registered.Add(r);

            // Newly enabled / instantiated renderers always take focus.
            // In multi-renderer scenes the last one to enable wins; users can call
            // RequestFocus() on a specific renderer to override.
            SetFocus(r);
        }

        internal static void Unregister(RatatuiRenderer r)
        {
            if (r == null) return;
            _registered.Remove(r);

            if (_focused == r)
            {
                _focused = null;
                r.InvokeFocusChanged(false);
                FocusChanged?.Invoke(r, null);
            }
        }

        /// <summary>
        /// Transfer focus to the given renderer. Pass null to clear focus.
        /// Reentrant-safe: if a callback re-enters <c>SetFocus</c>, this outer call
        /// suppresses its remaining notifications once the focus target has been superseded.
        /// </summary>
        public static void SetFocus(RatatuiRenderer r)
        {
            if (_focused == r) return;

            var prev = _focused;
            _focused = r;

            if (prev != null) prev.InvokeFocusChanged(false);
            // A reentrant SetFocus inside the previous callback already drove the next transition.
            if (_focused != r) return;

            if (r != null) r.InvokeFocusChanged(true);
            if (_focused != r) return;

            FocusChanged?.Invoke(prev, r);
        }

        /// <summary>Clear focus. No renderer will receive keyboard input until <see cref="SetFocus"/> is called.</summary>
        public static void ClearFocus() => SetFocus(null);
    }
}
