using System;
using System.Runtime.InteropServices;

namespace RatatuiUnity
{
    /// <summary>
    /// Low-level P/Invoke declarations for the ratatui_unity native library.
    /// Use <see cref="RatatuiTerminal"/> for a higher-level API.
    /// </summary>
    internal static class RatatuiNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Lib = "__Internal";
#elif UNITY_WEBGL && !UNITY_EDITOR
        private const string Lib = "__Internal";
#else
        private const string Lib = "ratatui_unity";
#endif

        // ── Lifecycle ──────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ratatui_create(ushort cols, ushort rows, float fontSize);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_destroy(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte ratatui_set_custom_font(
            IntPtr handle, byte[] fontData, uint fontLen);

        // ── Frame ─────────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_begin_frame(IntPtr handle);

        /// <summary>
        /// Renders queued commands and returns a pointer to the RGBA32 pixel buffer.
        /// The pointer is valid until the next call on this handle.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ratatui_end_frame(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_pixel_width(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_pixel_height(IntPtr handle);

        // ── Layout ────────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_root_area(IntPtr handle);

        /// <summary>
        /// Splits <paramref name="areaId"/> and writes child area IDs into
        /// <paramref name="outIds"/>. Returns the number of children produced.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_split(
            IntPtr handle,
            uint areaId,
            byte direction,
            byte[]  constraintTypes,
            ushort[] constraintValues,
            uint count,
            uint[] outIds);

        // ── Style ─────────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_set_style(
            IntPtr handle,
            byte fgR, byte fgG, byte fgB, byte useDefaultFg,
            byte bgR, byte bgG, byte bgB, byte useDefaultBg,
            byte modifiers);

        // ── Widgets ───────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_block(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string title,
            byte borders);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_paragraph(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string text,
            byte alignment, byte wrap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_list(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string items,
            int selected);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_gauge(
            IntPtr handle, uint areaId,
            float ratio,
            [MarshalAs(UnmanagedType.LPStr)] string label);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_tabs(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string titles,
            uint selected);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_sparkline(
            IntPtr handle, uint areaId,
            ulong[] data, uint len);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_table(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string data);

        /// Returns a new area ID representing the inside of <paramref name="areaId"/> shrunk by
        /// the given margin on each side.  Typical usage: horizontal=1, vertical=1 to get the
        /// area inside a Block with Borders.All.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_inner(
            IntPtr handle, uint areaId,
            ushort horizontal, ushort vertical);

        // Returns a pointer to a static string in the native lib — must NOT be freed.
        // Use Marshal.PtrToStringAnsi() without calling free.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ratatui_version();
    }
}
