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

        // ── New widgets ───────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_barchart(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string data,
            ushort barWidth, ushort barGap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_line_gauge(
            IntPtr handle, uint areaId,
            float ratio,
            [MarshalAs(UnmanagedType.LPStr)] string label);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_scrollbar(
            IntPtr handle, uint areaId,
            uint contentLength, uint position, uint viewportLength,
            byte orientation);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_calendar(
            IntPtr handle, uint areaId,
            int year, byte month, byte day);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_table_ex(
            IntPtr handle, uint areaId,
            [MarshalAs(UnmanagedType.LPStr)] string data,
            byte[] colTypes, ushort[] colValues, uint colCount,
            int selectedRow);

        // ── StyledParagraph builder ───────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_begin(
            IntPtr handle, uint areaId, byte alignment, byte wrap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_styled_para_span(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string text,
            byte fgR, byte fgG, byte fgB, byte useDefaultFg,
            byte bgR, byte bgG, byte bgB, byte useDefaultBg,
            byte modifiers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_newline(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_end(IntPtr handle);

        // ── Chart builder ─────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_begin(IntPtr handle, uint areaId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_chart_x_axis(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string title,
            double min, double max);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_chart_y_axis(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string title,
            double min, double max);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_chart_dataset(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            byte marker,
            byte r, byte g, byte b,
            double[] data, uint pointCount);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_end(IntPtr handle);

        // ── Canvas builder ────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_begin(
            IntPtr handle, uint areaId,
            double xMin, double xMax,
            double yMin, double yMax,
            byte marker);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_map(IntPtr handle, byte resolution);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_layer(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_line(
            IntPtr handle,
            double x1, double y1, double x2, double y2,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_circle(
            IntPtr handle,
            double x, double y, double radius,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_rectangle(
            IntPtr handle,
            double x, double y, double w, double h,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void ratatui_canvas_text(
            IntPtr handle,
            double x, double y,
            [MarshalAs(UnmanagedType.LPStr)] string text,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_points(
            IntPtr handle,
            double[] coords, uint count,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_end(IntPtr handle);

        // ── Input / Hit-Testing ───────────────────────────────────────────────

        /// <summary>
        /// Returns the most specific area ID at the given terminal cell.
        /// Returns 0 (root) if no specific area contains the cell.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_hit_test(IntPtr handle, ushort col, ushort row);

        /// <summary>
        /// Returns area rect as packed u64: x | (y &lt;&lt; 16) | (w &lt;&lt; 32) | (h &lt;&lt; 48).
        /// Returns 0 if area not found.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong ratatui_get_area_rect(IntPtr handle, uint areaId);
    }
}
