using System;

namespace RatatuiUnity
{
    /// <summary>
    /// Marker attribute that identifies a class as a terminal app.
    /// This attribute does NOT trigger automatic discovery — each app must explicitly
    /// call <see cref="RatatuiTerminalApps.Register{T}"/> from a
    /// <c>[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]</c> static method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RatatuiTerminalAppAttribute : Attribute
    {
        /// <summary>Unique app identifier used by the static open/close API.</summary>
        public string Id { get; }

        /// <summary>Human-readable label shown in app lists.</summary>
        public string DisplayName { get; set; }

        /// <summary>Sort order in the app registry. Lower values appear first.</summary>
        public int Order { get; set; }

        public RatatuiTerminalAppAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("App id cannot be empty.", nameof(id));

            Id = id;
            DisplayName = id;
        }
    }
}
