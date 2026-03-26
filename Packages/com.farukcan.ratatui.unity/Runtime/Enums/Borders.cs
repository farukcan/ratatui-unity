using System;

namespace RatatuiUnity
{
    /// <summary>
    /// Bitmask for block border sides, matching the Rust C API byte values.
    /// </summary>
    [Flags]
    public enum Borders : byte
    {
        None   = 0x00,
        Top    = 0x01,
        Bottom = 0x02,
        Left   = 0x04,
        Right  = 0x08,
        All    = 0x0F,
    }
}
