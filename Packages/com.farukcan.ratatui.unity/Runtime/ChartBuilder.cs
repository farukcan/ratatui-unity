using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Fluent builder for line/scatter charts with multiple datasets and labeled axes.
    /// </summary>
    /// <example>
    /// <code>
    /// term.BeginChart(area)
    ///     .XAxis("Time", 0.0, 20.0)
    ///     .YAxis("Value", -20.0, 20.0)
    ///     .Dataset("sin", Marker.Braille, Color.cyan, sinData)
    ///     .Render();
    /// </code>
    /// </example>
    public sealed class ChartBuilder
    {
        private readonly RatatuiTerminal _term;

        internal ChartBuilder(RatatuiTerminal term, uint areaId)
        {
            _term = term;
            RatatuiNative.ratatui_chart_begin(term.Handle, areaId);
        }

        /// <summary>Configure the horizontal axis label and data bounds.</summary>
        public ChartBuilder XAxis(string title, double min, double max)
        {
            RatatuiNative.ratatui_chart_x_axis(_term.Handle, title ?? string.Empty, min, max);
            return this;
        }

        /// <summary>Configure the vertical axis label and data bounds.</summary>
        public ChartBuilder YAxis(string title, double min, double max)
        {
            RatatuiNative.ratatui_chart_y_axis(_term.Handle, title ?? string.Empty, min, max);
            return this;
        }

        /// <summary>
        /// Add a dataset. <paramref name="data"/> contains interleaved (x, y) doubles:
        /// [x0, y0, x1, y1, ...].  The number of points is <c>data.Length / 2</c>.
        /// </summary>
        public ChartBuilder Dataset(
            string name,
            Marker marker,
            Color color,
            double[] data)
        {
            if (data == null || data.Length < 2) return this;
            RatatuiNative.ratatui_chart_dataset(
                _term.Handle,
                name ?? string.Empty,
                (byte)marker,
                (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255),
                data,
                (uint)(data.Length / 2));
            return this;
        }

        /// <summary>
        /// Finalize and enqueue the chart widget command.
        /// Must be called exactly once; the builder is unusable afterward.
        /// </summary>
        public void Render()
        {
            RatatuiNative.ratatui_chart_end(_term.Handle);
        }
    }
}
