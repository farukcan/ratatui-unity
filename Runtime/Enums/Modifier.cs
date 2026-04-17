namespace RatatuiUnity
{
    /// <summary>Text style modifiers; can be combined with the | operator.</summary>
    [System.Flags]
    public enum Modifier : byte
    {
        None       = 0x00,
        Bold       = 0x01,
        Italic     = 0x02,
        Underlined = 0x04,
        Dim        = 0x08,
    }
}
