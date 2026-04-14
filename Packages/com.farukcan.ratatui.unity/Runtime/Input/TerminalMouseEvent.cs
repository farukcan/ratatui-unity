namespace RatatuiUnity
{
    public enum MouseButton : byte
    {
        Left   = 0,
        Right  = 1,
        Middle = 2,
    }

    public enum MouseEventType : byte
    {
        /// <summary>Mouse moved to a new cell.</summary>
        Move,
        /// <summary>Mouse button pressed down.</summary>
        Down,
        /// <summary>Mouse button released.</summary>
        Up,
        /// <summary>Mouse button pressed and released on the same cell.</summary>
        Click,
        /// <summary>Scroll wheel moved.</summary>
        Scroll,
    }

    /// <summary>
    /// A mouse event in terminal cell coordinates.
    /// </summary>
    public readonly struct TerminalMouseEvent
    {
        /// <summary>Type of mouse event.</summary>
        public readonly MouseEventType Type;

        /// <summary>Terminal column (0-based, left to right).</summary>
        public readonly int Col;

        /// <summary>Terminal row (0-based, top to bottom).</summary>
        public readonly int Row;

        /// <summary>
        /// Area ID at the mouse position (from Rust hit_test).
        /// 0 = root area, meaning no specific widget area was hit.
        /// </summary>
        public readonly uint AreaId;

        /// <summary>Which mouse button (for Down/Up/Click events).</summary>
        public readonly MouseButton Button;

        /// <summary>Scroll delta (for Scroll events). Positive = up.</summary>
        public readonly float ScrollDelta;

        /// <summary>Active modifier keys.</summary>
        public readonly KeyModifiers Modifiers;

        public TerminalMouseEvent(
            MouseEventType type, int col, int row, uint areaId,
            MouseButton button, float scrollDelta, KeyModifiers modifiers)
        {
            Type        = type;
            Col         = col;
            Row         = row;
            AreaId      = areaId;
            Button      = button;
            ScrollDelta = scrollDelta;
            Modifiers   = modifiers;
        }

        public override string ToString()
            => $"Mouse({Type}, cell=({Col},{Row}), area={AreaId}, btn={Button})";
    }
}
