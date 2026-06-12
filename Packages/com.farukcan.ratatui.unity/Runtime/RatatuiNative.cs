using System;
using System.Runtime.InteropServices;

namespace RatatuiUnity
{
    /// <summary>
    /// Owned native terminal handle. Using <see cref="SafeHandle"/> makes the
    /// runtime keep the handle alive (ref-counted) for the duration of every
    /// P/Invoke that receives it, closing the race where the GC finalizer
    /// could otherwise free the handle while a native call is still running.
    /// Release goes through <c>ratatui_destroy</c> exactly once.
    /// </summary>
    internal sealed class RatatuiHandle : SafeHandle
    {
        // Parameterless ctor required by the marshaler for P/Invoke returns.
        public RatatuiHandle() : base(IntPtr.Zero, ownsHandle: true) { }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            RatatuiNative.ratatui_destroy(handle);
            return true;
        }
    }

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
        internal static extern RatatuiHandle ratatui_create(ushort cols, ushort rows, float fontSize);

        // Takes a raw IntPtr (not RatatuiHandle): called from
        // RatatuiHandle.ReleaseHandle, which owns the lifetime.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_destroy(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte ratatui_set_custom_font(
            RatatuiHandle handle, byte[] fontData, uint fontLen);

        // ── Frame ─────────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_begin_frame(RatatuiHandle handle);

        /// <summary>
        /// Renders queued commands and returns a pointer to the RGB24 pixel buffer.
        /// The pointer is valid until the next call on this handle.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ratatui_end_frame(RatatuiHandle handle);

        /// <summary>
        /// Like <see cref="ratatui_end_frame"/>, but skips pixel rasterization when the
        /// cell buffer is unchanged (hash-based dirty check). Returns null when unchanged.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ratatui_end_frame_hashed(RatatuiHandle handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_set_background_color(
            RatatuiHandle handle, byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_pixel_width(RatatuiHandle handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_pixel_height(RatatuiHandle handle);

        // ── Layout ────────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_root_area(RatatuiHandle handle);

        /// <summary>
        /// Splits <paramref name="areaId"/> and writes child area IDs into
        /// <paramref name="outIds"/>. Returns the number of children produced.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_split(
            RatatuiHandle handle,
            uint areaId,
            byte direction,
            byte[]  constraintTypes,
            ushort[] constraintValues,
            uint count,
            uint[] outIds);

        // ── Style ─────────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_set_style(
            RatatuiHandle handle,
            byte fgR, byte fgG, byte fgB, byte useDefaultFg,
            byte bgR, byte bgG, byte bgB, byte useDefaultBg,
            byte modifiers);

        // ── Widgets ───────────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_block(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
            byte borders);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_paragraph(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            byte alignment, byte wrap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_list(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string items,
            int selected);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_gauge(
            RatatuiHandle handle, uint areaId,
            float ratio,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string label);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_tabs(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string titles,
            uint selected);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_sparkline(
            RatatuiHandle handle, uint areaId,
            ulong[] data, uint len);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_table(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

        /// Returns a new area ID representing the inside of <paramref name="areaId"/> shrunk by
        /// the given margin on each side.  Typical usage: horizontal=1, vertical=1 to get the
        /// area inside a Block with Borders.All.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_inner(
            RatatuiHandle handle, uint areaId,
            ushort horizontal, ushort vertical);

        // Returns a pointer to a static string in the native lib — must NOT be freed.
        // Use Marshal.PtrToStringAnsi() without calling free.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ratatui_version();

        // ── New widgets ───────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_barchart(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string data,
            ushort barWidth, ushort barGap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_line_gauge(
            RatatuiHandle handle, uint areaId,
            float ratio,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string label);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_scrollbar(
            RatatuiHandle handle, uint areaId,
            uint contentLength, uint position, uint viewportLength,
            byte orientation);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_calendar(
            RatatuiHandle handle, uint areaId,
            int year, byte month, byte day);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_table_ex(
            RatatuiHandle handle, uint areaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string data,
            byte[] colTypes, ushort[] colValues, uint colCount,
            int selectedRow);

        // ── StyledParagraph builder ───────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_begin(
            RatatuiHandle handle, uint areaId, byte alignment, byte wrap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_span(
            RatatuiHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            byte fgR, byte fgG, byte fgB, byte useDefaultFg,
            byte bgR, byte bgG, byte bgB, byte useDefaultBg,
            byte modifiers);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_newline(RatatuiHandle handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_styled_para_end(RatatuiHandle handle);

        // ── Chart builder ─────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_begin(RatatuiHandle handle, uint areaId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_x_axis(
            RatatuiHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
            double min, double max);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_y_axis(
            RatatuiHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
            double min, double max);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_dataset(
            RatatuiHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            byte marker,
            byte r, byte g, byte b,
            double[] data, uint pointCount);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_chart_end(RatatuiHandle handle);

        // ── Canvas builder ────────────────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_begin(
            RatatuiHandle handle, uint areaId,
            double xMin, double xMax,
            double yMin, double yMax,
            byte marker);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_map(RatatuiHandle handle, byte resolution);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_layer(RatatuiHandle handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_line(
            RatatuiHandle handle,
            double x1, double y1, double x2, double y2,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_circle(
            RatatuiHandle handle,
            double x, double y, double radius,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_rectangle(
            RatatuiHandle handle,
            double x, double y, double w, double h,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_text(
            RatatuiHandle handle,
            double x, double y,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_points(
            RatatuiHandle handle,
            double[] coords, uint count,
            byte r, byte g, byte b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ratatui_canvas_end(RatatuiHandle handle);

        // ── Input / Hit-Testing ───────────────────────────────────────────────

        /// <summary>
        /// Returns the most specific area ID at the given terminal cell.
        /// Returns 0 (root) if no specific area contains the cell.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ratatui_hit_test(RatatuiHandle handle, ushort col, ushort row);

        /// <summary>
        /// Returns area rect as packed u64: x | (y &lt;&lt; 16) | (w &lt;&lt; 32) | (h &lt;&lt; 48).
        /// Returns 0 if area not found.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong ratatui_get_area_rect(RatatuiHandle handle, uint areaId);
    }
}
