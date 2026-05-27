namespace RatatuiUnity
{
    /// <summary>
    /// OnGUI fallback display mode when no RawImage or MeshRenderer target is assigned.
    /// </summary>
    public enum OnGuiMode
    {
        /// <summary>Stretch the terminal texture to fill the entire screen.</summary>
        Full,

        /// <summary>
        /// Draw at the terminal's native pixel size (cols × rows × cell size),
        /// positioned according to horizontal and vertical alignment.
        /// </summary>
        Partial,
    }
}
