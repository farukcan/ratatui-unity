using System;
using System.Text;
using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo2 "Weather" tab — weekly bar chart, monthly calendar, download progress line gauges.
    /// All weather data is generated in C#.
    /// </summary>
    public class WeatherTab : ITab
    {
        public string Title => "Weather";

        // ── Weekly forecast (Celsius) ─────────────────────────────────────────
        private static readonly (string Day, ulong High, ulong Low)[] Forecast =
        {
            ("Mon", 22, 14), ("Tue", 19, 13), ("Wed", 17, 10), ("Thu", 21, 15),
            ("Fri", 25, 16), ("Sat", 28, 18), ("Sun", 26, 17),
        };

        // ── Download speeds (KB/s) — animated ─────────────────────────────────
        private struct Download
        {
            public string Name;
            public float  Progress; // [0, 1]
            public float  Speed;    // display value
        }

        private readonly Download[] _downloads =
        {
            new Download { Name="ubuntu-24.04.iso",     Progress=0.72f, Speed=812f },
            new Download { Name="ratatui-demo.mp4",     Progress=0.45f, Speed=203f },
            new Download { Name="unity-package.unityp", Progress=0.91f, Speed=1240f},
        };

        private float _elapsed;

        public void Update(float dt) { _elapsed += dt; }
        public void OnInput(KeyCode key) { }
        public void OnKeyEvent(TerminalKeyEvent e) { }
        public void OnMouseEvent(TerminalMouseEvent e) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Percentage(40),
                Constraint.Percentage(30),
                Constraint.Percentage(30));

            if (rows.Length < 3) return;

            RenderForecast(term, rows[0]);
            RenderCalendar(term, rows[1]);
            RenderDownloads(term, rows[2]);
        }

        private void RenderForecast(RatatuiTerminal term, uint area)
        {
            var cols = term.Split(area, Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));

            if (cols.Length < 2) return;

            // High temps bar chart
            term.Block(cols[0], "Highs °C", Borders.All);
            uint highInner = term.Inner(cols[0]);
            var highSb = new StringBuilder();
            foreach (var f in Forecast) highSb.AppendLine($"{f.Day}\t{f.High}");
            term.SetStyle(new Color(1f, 0.6f, 0f), Color.clear);
            term.BarChart(highInner, highSb.ToString().TrimEnd(), barWidth: 3, barGap: 1);

            // Low temps bar chart
            term.Block(cols[1], "Lows °C", Borders.All);
            uint lowInner = term.Inner(cols[1]);
            var lowSb = new StringBuilder();
            foreach (var f in Forecast) lowSb.AppendLine($"{f.Day}\t{f.Low}");
            term.SetStyle(new Color(0.4f, 0.7f, 1f), Color.clear);
            term.BarChart(lowInner, lowSb.ToString().TrimEnd(), barWidth: 3, barGap: 1);
        }

        private void RenderCalendar(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Calendar", Borders.All);
            uint inner = term.Inner(area);

            DateTime now = DateTime.Now;
            term.Calendar(inner, now.Year, now.Month, now.Day);
        }

        private void RenderDownloads(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Downloads", Borders.All);
            uint inner = term.Inner(area);

            var rows = term.Split(inner, Direction.Vertical,
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1));

            if (rows.Length < _downloads.Length) return;

            for (int i = 0; i < _downloads.Length; i++)
            {
                // Animate: oscillate progress slightly to show activity.
                float p = _downloads[i].Progress
                    + 0.03f * (float)Math.Sin(_elapsed * 1.5f + i * 2.0f);
                p = Math.Clamp(p, 0f, 1f);

                string label = $"{_downloads[i].Name} [{_downloads[i].Speed:F0} KB/s]";
                term.SetStyle(new Color(0.4f, 0.9f, 0.4f), Color.clear);
                term.LineGauge(rows[i], p, label);
            }
        }
    }
}
