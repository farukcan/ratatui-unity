namespace RatatuiUnity
{
    /// <summary>
    /// How <see cref="RatatuiRenderer"/> interprets the <c>fontSize</c> inspector value.
    /// Cols × rows are always preserved; only the per-cell pixel scale changes.
    /// Modeled after CSS viewport-relative length units.
    /// </summary>
    public enum SizingMode
    {
        /// <summary>
        /// <c>fontSize</c> is taken as absolute pixels. The terminal is created
        /// once at <c>Awake</c> and never refits.
        /// </summary>
        Pixel,

        /// <summary>
        /// <c>fontSize</c> is interpreted as percent of viewport height
        /// (<c>fontSize × viewportHeight / 100</c>). Refits on resize / DPI change.
        /// Mirrors CSS <c>vh</c>.
        /// </summary>
        Vh,

        /// <summary>
        /// <c>fontSize</c> is interpreted as percent of viewport width
        /// (<c>fontSize × viewportWidth / 100</c>). Refits on resize / DPI change.
        /// Mirrors CSS <c>vw</c>.
        /// </summary>
        Vw,

        /// <summary>
        /// <c>fontSize</c> is interpreted as percent of the smaller viewport dimension
        /// (<c>fontSize × min(viewportWidth, viewportHeight) / 100</c>). Best general
        /// choice for terminals that should stay readable in both portrait and landscape.
        /// Mirrors CSS <c>vmin</c>.
        /// </summary>
        Vmin,

        /// <summary>
        /// <c>fontSize</c> is interpreted as percent of the larger viewport dimension
        /// (<c>fontSize × max(viewportWidth, viewportHeight) / 100</c>). Mirrors CSS <c>vmax</c>.
        /// </summary>
        Vmax,
    }
}
