using UnityEngine;

namespace RatatuiUnity.Samples.Profiler
{
    /// <summary>
    /// Configuration for the Profiler terminal app. Loaded at boot from
    /// <c>Resources/RatatuiProfilerConfig</c>; falls back to a default in-memory
    /// instance when no asset is present.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RatatuiProfilerConfig",
        menuName = "Ratatui/Profiler Config",
        order = 2)]
    public sealed class RatatuiProfilerConfig : ScriptableObject
    {
        [Header("Terminal Dimensions")]
        public int cols = 60;
        public int rows = 30;
        public float fontSize = 14f;
        public SizingMode sizingMode = SizingMode.Pixel;

        [Header("Display")]
        public OnGuiMode displayMode = OnGuiMode.Window;
        public OnGuiHorizontalAlign horizontalAlign = OnGuiHorizontalAlign.Right;
        public OnGuiVerticalAlign verticalAlign = OnGuiVerticalAlign.Top;
        public bool windowStartMaximized = false;
        public Color backgroundColor = new Color(0.0196f, 0.0235f, 0.0314f, 1f);

        [Tooltip("Initial window width in pixels (Window mode). -1 = auto.")]
        public float windowInitialWidth = 520f;

        [Tooltip("Initial window height in pixels including title bar (Window mode). -1 = auto.")]
        public float windowInitialHeight = 420f;

        [Header("Input")]
        public KeyCode toggleKey = KeyCode.F10;

        public Font windowChromeFont;

        public static RatatuiProfilerConfig CreateDefault()
        {
            var cfg = CreateInstance<RatatuiProfilerConfig>();
            cfg.name = "RatatuiProfilerConfig (Default)";
#if UNITY_EDITOR
            cfg.windowChromeFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(BundledChromeFontPath);
#endif
            return cfg;
        }

#if UNITY_EDITOR
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
