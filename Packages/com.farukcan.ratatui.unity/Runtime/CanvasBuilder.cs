using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Fluent builder for canvas widgets: world map, geometric shapes, text, and point clouds.
    /// </summary>
    /// <example>
    /// <code>
    /// term.BeginCanvas(area, -180, 180, -90, 90, Marker.Braille)
    ///     .Map(MapResolution.High)
    ///     .Layer()
    ///     .Points(serverCoords, Color.yellow)
    ///     .Render();
    /// </code>
    /// </example>
    public sealed class CanvasBuilder
    {
        private readonly RatatuiTerminal _term;

        internal CanvasBuilder(
            RatatuiTerminal term,
            uint areaId,
            double xMin, double xMax,
            double yMin, double yMax,
            Marker marker)
        {
            _term = term;
            RatatuiNative.ratatui_canvas_begin(
                term.Handle, areaId, xMin, xMax, yMin, yMax, (byte)marker);
        }

        /// <summary>Draw the world map.</summary>
        public CanvasBuilder Map(MapResolution resolution = MapResolution.High)
        {
            RatatuiNative.ratatui_canvas_map(_term.Handle, (byte)resolution);
            return this;
        }

        /// <summary>
        /// Flush the current drawing layer so subsequent shapes render on top of
        /// previously drawn shapes.
        /// </summary>
        public CanvasBuilder Layer()
        {
            RatatuiNative.ratatui_canvas_layer(_term.Handle);
            return this;
        }

        /// <summary>Draw a line segment.</summary>
        public CanvasBuilder Line(double x1, double y1, double x2, double y2, Color color)
        {
            RatatuiNative.ratatui_canvas_line(
                _term.Handle, x1, y1, x2, y2,
                (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255));
            return this;
        }

        /// <summary>Draw a circle outline.</summary>
        public CanvasBuilder Circle(double x, double y, double radius, Color color)
        {
            RatatuiNative.ratatui_canvas_circle(
                _term.Handle, x, y, radius,
                (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255));
            return this;
        }

        /// <summary>Draw a rectangle outline.</summary>
        public CanvasBuilder Rectangle(double x, double y, double width, double height, Color color)
        {
            RatatuiNative.ratatui_canvas_rectangle(
                _term.Handle, x, y, width, height,
                (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255));
            return this;
        }

        /// <summary>Print a text label at the given canvas coordinates.</summary>
        public CanvasBuilder Text(double x, double y, string text, Color color)
        {
            RatatuiNative.ratatui_canvas_text(
                _term.Handle, x, y, text ?? string.Empty,
                (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255));
            return this;
        }

        /// <summary>
        /// Draw a scatter of points. <paramref name="coords"/> is interleaved
        /// (x, y) doubles: [x0, y0, x1, y1, ...].
        /// </summary>
        public CanvasBuilder Points(double[] coords, Color color)
        {
            if (coords == null || coords.Length < 2) return this;
            RatatuiNative.ratatui_canvas_points(
                _term.Handle, coords, (uint)(coords.Length / 2),
                (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255));
            return this;
        }

        /// <summary>
        /// Finalize and enqueue the canvas widget command.
        /// Must be called exactly once; the builder is unusable afterward.
        /// </summary>
        public void Render()
        {
            RatatuiNative.ratatui_canvas_end(_term.Handle);
        }
    }
}
