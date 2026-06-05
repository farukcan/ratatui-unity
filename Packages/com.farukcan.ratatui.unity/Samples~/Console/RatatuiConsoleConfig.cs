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

        [Tooltip("Font size — interpretation depends on sizingMode. " +
                 "Pixel: absolute pixels. Vh / Vw / Vmin / Vmax: percent of viewport.")]
        public float fontSize = 14f;

        [Tooltip("How fontSize is interpreted. " +
                 "Pixel: absolute pixels. Vmin: percent of the smaller viewport dimension.")]
        public SizingMode sizingMode = SizingMode.Pixel;

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

        [Tooltip("Font used for OnGUI window chrome (title bar + zoom / resize glyphs). " +
                 "Required for non-ASCII glyphs (◥, −) to render on WebGL, where Unity's " +
                 "default GUI font lacks them and no OS font fallback exists. " +
                 "Auto-populated with the bundled JetBrains Mono via OnValidate.")]
        public Font windowChromeFont;

        /// <summary>
        /// Build a default in-memory instance used when no asset is present
        /// under <c>Resources/RatatuiConsoleConfig</c>.
        /// </summary>
        public static RatatuiConsoleConfig CreateDefault()
        {
            var cfg = CreateInstance<RatatuiConsoleConfig>();
            cfg.name = "RatatuiConsoleConfig (Default)";
#if UNITY_EDITOR
            cfg.windowChromeFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(BundledChromeFontPath);
#endif
            return cfg;
        }

#if UNITY_EDITOR
        // Bundled JetBrains Mono inside the package — same TTF the Rust core embeds, so
        // chrome glyphs match the terminal font on platforms without OS font fallback.
        private const string BundledChromeFontPath =
            "Packages/com.farukcan.ratatui.unity/Runtime/Fonts/JetBrainsMono-Regular.ttf";

        private void OnValidate()
        {
            if (windowChromeFont == null)
                windowChromeFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(BundledChromeFontPath);
        }
#endif
    }
}
