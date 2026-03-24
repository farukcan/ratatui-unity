using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// High-level C# API for the ratatui native library.
    /// Wraps lifecycle, layout, style, and widget commands.
    /// Must be disposed when no longer needed.
    /// </summary>
    public sealed class RatatuiTerminal : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        // Scratch arrays reused across Split() calls to avoid per-frame allocations.
        private byte[]   _splitTypes  = new byte[8];
        private ushort[] _splitValues = new ushort[8];
        private uint[]   _splitOutIds = new uint[8];

        /// <summary>Width of the rendered texture in pixels.</summary>
        public int PixelWidth  { get; private set; }

        /// <summary>Height of the rendered texture in pixels.</summary>
        public int PixelHeight { get; private set; }

        /// <summary>Terminal width in character columns.</summary>
        public int Cols { get; private set; }

        /// <summary>Terminal height in character rows.</summary>
        public int Rows { get; private set; }

        /// <summary>Width of a single character cell in pixels (PixelWidth / Cols).</summary>
        public int CellWidth  => Cols  > 0 ? PixelWidth  / Cols  : 0;

        /// <summary>Height of a single character cell in pixels (PixelHeight / Rows).</summary>
        public int CellHeight => Rows > 0 ? PixelHeight / Rows : 0;

        /// <summary>Root area ID (always 0).</summary>
        public uint RootArea => RatatuiNative.ratatui_root_area(_handle);

        /// <param name="cols">Terminal width in character columns.</param>
        /// <param name="rows">Terminal height in character rows.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        public RatatuiTerminal(int cols, int rows, float fontSize = 16f)
        {
            Cols  = cols;
            Rows  = rows;
            _handle    = RatatuiNative.ratatui_create((ushort)cols, (ushort)rows, fontSize);
            PixelWidth  = (int)RatatuiNative.ratatui_pixel_width(_handle);
            PixelHeight = (int)RatatuiNative.ratatui_pixel_height(_handle);
        }

        // ── Font ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Replace the embedded JetBrains Mono font with a custom TTF font.
        /// </summary>
        /// <returns>True if the font was loaded successfully.</returns>
        public bool SetCustomFont(byte[] ttfBytes)
        {
            ThrowIfDisposed();
            if (ttfBytes == null || ttfBytes.Length == 0) return false;
            return RatatuiNative.ratatui_set_custom_font(
                _handle, ttfBytes, (uint)ttfBytes.Length) != 0;
        }

        // ── Frame ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Start a new frame. Clears all queued widget commands.
        /// Call before adding widgets and before <see cref="EndFrame"/>.
        /// </summary>
        public void BeginFrame()
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_begin_frame(_handle);
        }

        /// <summary>
        /// Execute all queued widget commands and return a direct pointer to the
        /// native RGBA32 pixel buffer.  The pointer is valid until the next
        /// <see cref="BeginFrame"/> call.  Byte count is <c>PixelWidth * PixelHeight * 4</c>.
        /// Prefer this over <see cref="EndFrame"/> to avoid a managed byte[] allocation
        /// every frame — pass the pointer directly to
        /// <c>Texture2D.LoadRawTextureData(IntPtr, int)</c>.
        /// </summary>
        public IntPtr EndFrameRaw()
        {
            ThrowIfDisposed();
            return RatatuiNative.ratatui_end_frame(_handle);
        }

        /// <summary>
        /// Execute all queued widget commands and copy the RGBA32 pixel data into
        /// a newly allocated <c>byte[]</c>.  Prefer <see cref="EndFrameRaw"/> to
        /// avoid a ~1-2 MB GC allocation every frame.
        /// </summary>
        public byte[] EndFrame()
        {
            IntPtr ptr = EndFrameRaw();
            if (ptr == IntPtr.Zero) return Array.Empty<byte>();
            int byteCount = PixelWidth * PixelHeight * 4;
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(ptr, pixels, 0, byteCount);
            return pixels;
        }

        // ── Layout ────────────────────────────────────────────────────────────

        /// <summary>
        /// Split an area into children according to the provided constraints.
        /// Returns the array of child area IDs.
        /// </summary>
        public uint[] Split(uint areaId, Direction direction, params Constraint[] constraints)
        {
            ThrowIfDisposed();
            if (constraints == null || constraints.Length == 0)
                return Array.Empty<uint>();

            int n = constraints.Length;

            // Grow scratch arrays only when a larger split is encountered (rare).
            if (n > _splitTypes.Length)
            {
                _splitTypes  = new byte[n];
                _splitValues = new ushort[n];
                _splitOutIds = new uint[n];
            }

            for (int i = 0; i < n; i++)
            {
                _splitTypes[i]  = (byte)constraints[i].Type;
                _splitValues[i] = constraints[i].Value;
            }

            uint produced = RatatuiNative.ratatui_split(
                _handle, areaId, (byte)direction,
                _splitTypes, _splitValues, (uint)n, _splitOutIds);

            var result = new uint[produced];
            Array.Copy(_splitOutIds, result, produced);
            return result;
        }

        // ── Margin / Inner ────────────────────────────────────────────────────

        /// <summary>
        /// Returns a new area ID that represents the inside of <paramref name="areaId"/>
        /// shrunk by the given margin on each side.
        /// <para>
        /// Typical usage: call after <see cref="Block"/> to get the area inside the border:
        /// <code>
        /// term.Block(area, "Title", Borders.All);
        /// uint inner = term.Inner(area);          // 1 char inside borders
        /// term.Paragraph(inner, "Hello");
        /// </code>
        /// Use <paramref name="horizontal"/> = 2 for one extra character of text padding on
        /// left/right in addition to the border.
        /// </para>
        /// </summary>
        public uint Inner(uint areaId, ushort horizontal = 1, ushort vertical = 1)
        {
            ThrowIfDisposed();
            return RatatuiNative.ratatui_inner(_handle, areaId, horizontal, vertical);
        }

        // ── Style ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Set a style that will be applied to the next widget command.
        /// Use <c>Color.clear</c> to keep the terminal default for fg or bg.
        /// </summary>
        /// <param name="modifiers">
        /// Bitmask: 0x01=Bold, 0x02=Italic, 0x04=Underlined, 0x08=Dim
        /// </param>
        public void SetStyle(Color fg, Color bg, byte modifiers = 0)
        {
            ThrowIfDisposed();
            bool defaultFg = (fg.a < 0.01f);
            bool defaultBg = (bg.a < 0.01f);

            RatatuiNative.ratatui_set_style(
                _handle,
                (byte)(fg.r * 255), (byte)(fg.g * 255), (byte)(fg.b * 255),
                (byte)(defaultFg ? 1 : 0),
                (byte)(bg.r * 255), (byte)(bg.g * 255), (byte)(bg.b * 255),
                (byte)(defaultBg ? 1 : 0),
                modifiers);
        }

        // ── Widgets ───────────────────────────────────────────────────────────

        /// <summary>
        /// Render a bordered block with an optional title into <paramref name="areaId"/>.
        /// </summary>
        public void Block(uint areaId, string title = "", Borders borders = Borders.All)
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_block(_handle, areaId, title ?? string.Empty, (byte)borders);
        }

        /// <summary>
        /// Render a paragraph of text into <paramref name="areaId"/>.
        /// </summary>
        public void Paragraph(uint areaId, string text,
            Alignment alignment = Alignment.Left, bool wrap = true)
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_paragraph(
                _handle, areaId, text ?? string.Empty,
                (byte)alignment, (byte)(wrap ? 1 : 0));
        }

        /// <summary>
        /// Render a list widget. <paramref name="items"/> is a newline-separated string.
        /// Pass <paramref name="selected"/> = -1 for no selection.
        /// </summary>
        public void List(uint areaId, string items, int selected = -1)
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_list(_handle, areaId, items ?? string.Empty, selected);
        }

        /// <summary>
        /// Render a progress gauge. <paramref name="ratio"/> is clamped to [0, 1].
        /// </summary>
        public void Gauge(uint areaId, float ratio, string label = "")
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_gauge(_handle, areaId, ratio, label ?? string.Empty);
        }

        /// <summary>
        /// Render a tab bar. <paramref name="titles"/> is a newline-separated string.
        /// </summary>
        public void Tabs(uint areaId, string titles, uint selected = 0)
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_tabs(_handle, areaId, titles ?? string.Empty, selected);
        }

        /// <summary>
        /// Render a sparkline from an array of data values.
        /// </summary>
        public void Sparkline(uint areaId, ulong[] data)
        {
            ThrowIfDisposed();
            if (data == null || data.Length == 0) return;
            RatatuiNative.ratatui_sparkline(_handle, areaId, data, (uint)data.Length);
        }

        /// <summary>
        /// Render a table. <paramref name="data"/> format: first line = tab-separated headers;
        /// subsequent lines = tab-separated row cells.
        /// </summary>
        public void Table(uint areaId, string data)
        {
            ThrowIfDisposed();
            RatatuiNative.ratatui_table(_handle, areaId, data ?? string.Empty);
        }

        // ── Utility ───────────────────────────────────────────────────────────

        /// <summary>Returns the native library version string.</summary>
        public static string Version
        {
            get
            {
                // ratatui_version() returns a pointer to a static C string — never freed.
                IntPtr ptr = RatatuiNative.ratatui_version();
                return ptr == IntPtr.Zero
                    ? "unknown"
                    : Marshal.PtrToStringAnsi(ptr) ?? "unknown";
            }
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    RatatuiNative.ratatui_destroy(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RatatuiTerminal));
        }
    }
}
