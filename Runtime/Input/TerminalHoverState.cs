namespace RatatuiUnity
{
    /// <summary>
    /// Tracks the current mouse hover position in terminal coordinates.
    /// Updated every frame by RatatuiRenderer when mouse input is enabled.
    /// </summary>
    public readonly struct TerminalHoverState
    {
        /// <summary>Current terminal column under the mouse.</summary>
        public readonly int Col;

        /// <summary>Current terminal row under the mouse.</summary>
        public readonly int Row;

        /// <summary>Area ID under the mouse cursor (from Rust hit_test).</summary>
        public readonly uint AreaId;

        /// <summary>True if the mouse is currently over the terminal texture.</summary>
        public readonly bool IsInside;

        public TerminalHoverState(int col, int row, uint areaId, bool isInside)
        {
            Col      = col;
            Row      = row;
            AreaId   = areaId;
            IsInside = isInside;
        }

        public static readonly TerminalHoverState Outside = new TerminalHoverState(0, 0, 0, false);

        public override string ToString()
            => IsInside ? $"Hover(cell=({Col},{Row}), area={AreaId})" : "Hover(outside)";
    }
}
