using UnityEngine;

namespace RatatuiUnity.Samples.Notepad
{
    /// <summary>
    /// Configuration for the Notepad terminal app. Loaded at boot from
    /// <c>Resources/RatatuiNotepadConfig</c>; falls back to a default in-memory
    /// instance when no asset is present.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RatatuiNotepadConfig",
        menuName = "Ratatui/Notepad Config",
        order = 1)]
    public sealed class RatatuiNotepadConfig : ScriptableObject
    {
        [Header("Terminal Dimensions")]
        public int cols = 100;
        public int rows = 28;
        public float fontSize = 14f;
        public SizingMode sizingMode = SizingMode.Pixel;

        [Header("Display")]
        public OnGuiMode displayMode = OnGuiMode.Window;
        public OnGuiHorizontalAlign horizontalAlign = OnGuiHorizontalAlign.Center;
        public OnGuiVerticalAlign verticalAlign = OnGuiVerticalAlign.Center;
        public bool windowStartMaximized = false;
        public Color backgroundColor = new Color(0.0196f, 0.0235f, 0.0314f, 1f);

        [Header("Input")]
        public KeyCode toggleKey = KeyCode.F12;

        public Font windowChromeFont;

        public static RatatuiNotepadConfig CreateDefault()
        {
            var cfg = CreateInstance<RatatuiNotepadConfig>();
            cfg.name = "RatatuiNotepadConfig (Default)";
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
