using System;
using System.Text;
using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo1 "Dashboard" tab — gauges, sparkline, task/log lists, bar chart, sin chart.
    /// All signal data is generated in C# and pushed to Rust for rendering.
    /// </summary>
    public class DashboardTab : ITab
    {
        public string Title => "Dashboard";

        // ── Gauge / LineGauge progress ─────────────────────────────────────────
        private float _progress;

        // ── Sparkline: rolling random values ──────────────────────────────────
        private readonly ulong[] _sparkline = new ulong[300];
        private int _sparkTick;
        private readonly System.Random _rng = new System.Random(42);

        // ── Bar chart: rotating event data ────────────────────────────────────
        private static readonly (string Label, ulong Value)[] AllBars =
        {
            ("B1",9),("B2",12),("B3",5),("B4",8),("B5",2),("B6",4),
            ("B7",5),("B8",9),("B9",14),("B10",15),("B11",1),("B12",0),
            ("B13",4),("B14",6),("B15",4),("B16",6),("B17",4),("B18",7),
            ("B19",13),("B20",8),("B21",11),("B22",9),("B23",3),("B24",5),
        };
        private int _barOffset;

        // ── Chart: two sin waves ───────────────────────────────────────────────
        // 100 points for sin1 (interval 0.2), 200 points for sin2 (interval 0.1)
        private readonly double[] _sin1 = new double[200]; // 100 x (x,y)
        private readonly double[] _sin2 = new double[400]; // 200 x (x,y)
        private double _windowStart;

        // ── Task list ─────────────────────────────────────────────────────────
        private static readonly string[] TaskItems =
        {
            "Buy groceries", "Fix bug #1337", "Reply to emails", "Read Ratatui docs",
            "Water the plants", "Call dentist", "Update Unity", "Write unit tests",
            "Stretch break", "Commit and push", "Review PRs", "Plan next sprint",
        };
        private readonly StatefulList<string> _tasks = new StatefulList<string>(TaskItems);

        // ── Log list ──────────────────────────────────────────────────────────
        private static readonly (string Evt, string Level)[] AllLogs =
        {
            ("Connected", "INFO"), ("Auth OK", "INFO"), ("Disk low", "WARNING"),
            ("Null ref", "ERROR"), ("Panic!", "CRITICAL"), ("Retry 1", "WARNING"),
            ("Retry 2", "WARNING"), ("Recovered", "INFO"), ("Overflow", "ERROR"),
            ("Reconnect", "INFO"), ("Timeout", "WARNING"), ("OK", "INFO"),
        };
        private int _logOffset;

        // ── Timing ────────────────────────────────────────────────────────────
        private float _tickTimer;
        private const float TickInterval = 0.25f;

        public DashboardTab()
        {
            _tasks.SelectFirst();
            // Pre-fill with random values so the sparkline is visible immediately.
            for (int i = 0; i < _sparkline.Length; i++)
                _sparkline[i] = (ulong)_rng.Next(0, 100);
        }

        public void Update(float dt)
        {
            _progress = (_progress + dt * 0.05f) % 1.0f;
            _windowStart += dt * 2.0;

            _tickTimer += dt;
            if (_tickTimer >= TickInterval)
            {
                _tickTimer -= TickInterval;

                // Shift sparkline left, append new random value at end.
                Array.Copy(_sparkline, 1, _sparkline, 0, _sparkline.Length - 1);
                _sparkline[_sparkline.Length - 1] = (ulong)_rng.Next(0, 100);

                _barOffset = (_barOffset + 1) % AllBars.Length;
                _logOffset = (_logOffset + 1) % AllLogs.Length;
            }

            // Re-generate sin data every frame based on current window.
            double x = _windowStart;
            for (int i = 0; i < 100; i++)
            {
                _sin1[i * 2]     = x;
                _sin1[i * 2 + 1] = Math.Sin(x / 3.0) * 18.0;
                x += 0.2;
            }
            x = _windowStart;
            for (int i = 0; i < 200; i++)
            {
                _sin2[i * 2]     = x;
                _sin2[i * 2 + 1] = Math.Sin(x / 2.0) * 10.0;
                x += 0.1;
            }
        }

        public void OnInput(KeyCode key)
        {
            if (key == KeyCode.UpArrow || key == KeyCode.W)    _tasks.Previous();
            if (key == KeyCode.DownArrow || key == KeyCode.S)  _tasks.Next();
        }

        public void OnKeyEvent(TerminalKeyEvent e)
        {
            if (e.Key == KeyCode.UpArrow   || e.Character == 'w' || e.Character == 'W') _tasks.Previous();
            if (e.Key == KeyCode.DownArrow || e.Character == 's' || e.Character == 'S') _tasks.Next();
        }

        public void OnMouseEvent(TerminalMouseEvent e) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Length(9),
                Constraint.Min(8),
                Constraint.Length(7));

            if (rows.Length < 3) return;
            RenderGauges(term, rows[0]);
            RenderCharts(term, rows[1]);
            RenderFooter(term, rows[2]);
        }

        // ─────────────────────────────────────────────────────────────────────

        private void RenderGauges(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Graphs", Borders.All);
            uint inner = term.Inner(area);
            var rows = term.Split(inner, Direction.Vertical,
                Constraint.Length(2),
                Constraint.Length(3),
                Constraint.Length(2));

            if (rows.Length < 3) return;

            // Progress gauge
            string pctLabel = $"{_progress * 100:F0}%";
            term.SetStyle(Color.magenta, Color.clear, Modifier.Italic);
            term.Gauge(rows[0], _progress, pctLabel);

            // Sparkline
            term.SetStyle(new Color(0.2f, 0.8f, 0.2f), Color.clear);
            term.Sparkline(rows[1], _sparkline);

            // Line gauge
            term.SetStyle(Color.magenta, Color.clear);
            term.LineGauge(rows[2], _progress, $"{_progress * 100:F0}%");
        }

        private void RenderCharts(RatatuiTerminal term, uint area)
        {
            var cols = term.Split(area, Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));

            if (cols.Length < 2) return;

            RenderListsAndBarChart(term, cols[0]);
            RenderSinChart(term, cols[1]);
        }

        private void RenderListsAndBarChart(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Percentage(50),
                Constraint.Percentage(50));

            if (rows.Length < 2) return;

            // Top: tasks + logs side by side
            var topCols = term.Split(rows[0], Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));

            if (topCols.Length >= 2)
            {
                // Task list with selection
                term.SetStyle(Color.cyan, Color.clear);
                term.List(topCols[0], _tasks.ToItemsString(t => t), _tasks.Selected);

                // Log list (colored via StyledParagraph)
                term.Block(topCols[1], "Logs", Borders.All);
                uint logInner = term.Inner(topCols[1]);
                RenderLogs(term, logInner);
            }

            // Bottom: bar chart
            term.SetStyle(Color.green, Color.clear);
            RenderBarChart(term, rows[1]);
        }

        private void RenderLogs(RatatuiTerminal term, uint area)
        {
            var builder = term.BeginStyledParagraph(area, Alignment.Left, false);
            int count = Math.Min(10, AllLogs.Length);
            for (int i = 0; i < count; i++)
            {
                int idx = (_logOffset + i) % AllLogs.Length;
                var (evt, level) = AllLogs[idx];
                Color levelColor = level switch
                {
                    "CRITICAL" => Color.red,
                    "ERROR"    => new Color(1f, 0.4f, 0.4f),
                    "WARNING"  => Color.yellow,
                    _          => new Color(0.5f, 0.8f, 1f),
                };
                builder.Span($"[{level}] ", levelColor).Span(evt).Line();
            }
            builder.Render();
        }

        private void RenderBarChart(RatatuiTerminal term, uint area)
        {
            var sb = new StringBuilder();
            int count = Math.Min(12, AllBars.Length);
            for (int i = 0; i < count; i++)
            {
                var (label, value) = AllBars[(_barOffset + i) % AllBars.Length];
                sb.AppendLine($"{label}\t{value}");
            }
            term.BarChart(area, sb.ToString().TrimEnd(), barWidth: 3, barGap: 1);
        }

        private void RenderSinChart(RatatuiTerminal term, uint area)
        {
            double wEnd = _windowStart + 20.0;
            term.BeginChart(area)
                .XAxis("Time", _windowStart, wEnd)
                .YAxis("Value", -20.0, 20.0)
                .Dataset("sin(x/3)*18", Marker.Braille, Color.cyan,  _sin1)
                .Dataset("sin(x/2)*10", Marker.Braille, Color.yellow, _sin2)
                .Render();
        }

        private void RenderFooter(RatatuiTerminal term, uint area)
        {
            term.BeginStyledParagraph(area, Alignment.Left, true)
                .Span("This demo combines both ratatui demos in one Unity project. ")
                .Span("Tip: ", fg: Color.yellow, modifiers: Modifier.Bold)
                .Span("use ")
                .Span("A/D", fg: Color.cyan, modifiers: Modifier.Bold)
                .Span(" to switch tabs, ")
                .Span("W/S or arrows", fg: Color.cyan, modifiers: Modifier.Bold)
                .Span(" to navigate lists.")
                .Render();
        }
    }
}
