using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// Configuration for the developer console. Loaded at boot from
    /// <c>Resources/RatatuiConsoleConfig</c>; falls back to a default in-memory
    /// instance when no asset is present.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RatatuiConsoleConfig",
        menuName = "Ratatui/Console Config",
        order = 0)]
    public sealed class RatatuiConsoleConfig : ScriptableObject
    {
        [Header("Terminal Dimensions")]
        [Tooltip("Terminal width in character columns.")]
        public int cols = 120;

        [Tooltip("Terminal height in character rows.")]
        public int rows = 32;

        [Tooltip("Font size in pixels.")]
        public float fontSize = 14f;

        [Header("Display")]
        [Tooltip("Full: stretch to entire screen. Partial: native pixel size with alignment. " +
                 "Window: draggable macOS-style window with title bar.")]
        public OnGuiMode displayMode = OnGuiMode.Window;

        [Tooltip("Horizontal placement when displayMode is Partial.")]
        public OnGuiHorizontalAlign horizontalAlign = OnGuiHorizontalAlign.Center;

        [Tooltip("Vertical placement when displayMode is Partial.")]
        public OnGuiVerticalAlign verticalAlign = OnGuiVerticalAlign.Top;

        [Tooltip("When displayMode is Window, start maximized on first open.")]
        public bool windowStartMaximized = true;

        [Tooltip("Background color of the terminal (alpha ignored).")]
        public Color backgroundColor = new Color(0.07f, 0.07f, 0.11f);

        [Header("Input")]
        [Tooltip("Key that toggles the console open/closed.")]
        public KeyCode toggleKey = KeyCode.BackQuote;

        [Header("Buffers")]
        [Tooltip("Maximum number of log entries kept in memory.")]
        public int maxLogEntries = 2000;

        [Tooltip("Maximum number of command-history entries.")]
        public int maxHistoryEntries = 64;

        [Header("Display Options")]
        [Tooltip("Prepend each log line with [HH:mm:ss].")]
        public bool showTimestamp = true;

        /// <summary>
        /// Build a default in-memory instance used when no asset is present
        /// under <c>Resources/RatatuiConsoleConfig</c>.
        /// </summary>
        public static RatatuiConsoleConfig CreateDefault()
        {
            var cfg = CreateInstance<RatatuiConsoleConfig>();
            cfg.name = "RatatuiConsoleConfig (Default)";
            return cfg;
        }
    }
}
