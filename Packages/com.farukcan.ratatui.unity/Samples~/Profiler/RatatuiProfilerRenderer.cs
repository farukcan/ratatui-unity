using UnityEngine;

namespace RatatuiUnity.Samples.Profiler
{
    /// <summary>
    /// Read-only telemetry overlay. Created automatically by
    /// <see cref="RatatuiTerminalApps"/> — do not add to scenes manually.
    /// Toggle with the configured key (default F11), close with ESC.
    /// </summary>
    [RatatuiTerminalApp("profiler", DisplayName = "Profiler", Order = 30)]
    [DefaultExecutionOrder(-100)]
    public sealed class RatatuiProfilerRenderer : RatatuiTerminalApp
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterApp()
        {
            RatatuiTerminalApps.Register<RatatuiProfilerRenderer>("profiler", "Profiler", 30);
        }

        private RatatuiProfilerConfig _config;
        private readonly ProfilerMetrics _metrics = new ProfilerMetrics();

        // Cached widget buffers — a profiler must not allocate every frame
        // or it skews its own GC ALLOC/FRAME reading. Each buffer is sized
        // to (panel inner width + 1): the last slot is a ghost equal to
        // SparkScaleCeiling so ratatui's max-of-data normalizer is pinned
        // to the 1-minute max we computed in C#.
        private ulong[] _fpsSpark;
        private ulong[] _gcBars;
        private ulong[] _batchesSpark;
        private ulong[] _heapSpark;

        // Fixed normalizer ceiling. Values are pre-scaled to [0, ceiling];
        // ratatui then maps [0, ceiling] to [0, full bar height]. Higher
        // values give finer vertical resolution; 10000 is plenty for the
        // 1–8 row sparklines we draw.
        private const ulong SparkScaleCeiling = 10000UL;

        private static readonly Color ColorBorder = new Color(0.45f, 0.45f, 0.5f);
        private static readonly Color ColorText = new Color(0.85f, 0.85f, 0.9f);
        private static readonly Color ColorDim = new Color(0.5f, 0.5f, 0.55f);

        // Section accents matching the visual reference.
        private static readonly Color ColorPerformance = new Color(0.4f, 0.95f, 0.45f);  // green
        private static readonly Color ColorGc = new Color(0.95f, 0.6f, 0.25f);            // orange
        private static readonly Color ColorRendering = new Color(0.55f, 0.85f, 1.0f);     // cyan
        private static readonly Color ColorMemory = new Color(0.95f, 0.45f, 0.85f);       // magenta

        private static readonly Color ColorFpsGood = new Color(0.4f, 0.95f, 0.4f);
        private static readonly Color ColorFpsWarn = new Color(0.95f, 0.85f, 0.3f);
        private static readonly Color ColorFpsBad = new Color(0.95f, 0.4f, 0.4f);
        private static readonly Color ColorTitle = new Color(0.95f, 0.92f, 0.85f);

        protected override KeyCode ToggleKey => _config != null ? _config.toggleKey : KeyCode.F10;

        protected override void Awake()
        {
            RatatuiProfiler.EnsureServicesBooted();
            _config = RatatuiProfiler.Config;
            if (_config == null) _config = RatatuiProfilerConfig.CreateDefault();
            ApplyConfigToBase(_config);
            base.Awake();
        }

        protected override void BuildFrame(RatatuiTerminal term)
        {
            if (!IsOpen) return;

            _metrics.Sample(Time.unscaledDeltaTime);

            uint root = term.RootArea;
            var rows = term.Split(root, Direction.Vertical,
                Constraint.Length(3),
                Constraint.Fill(1));

            if (rows.Length < 2) return;

            BuildHeader(term, rows[0]);
            BuildBody(term, rows[1]);
        }

        private void BuildHeader(RatatuiTerminal term, uint area)
        {
            term.SetStyle(ColorBorder, Color.clear, Modifier.None);
            term.Block(area, " TELEMETRY MONITOR ", Borders.All);
            uint inner = term.Inner(area);

            var cols = term.Split(inner, Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));
            if (cols.Length < 2) return;

            term.BeginStyledParagraph(cols[0], Alignment.Left, wrap: false)
                .Span("BUILD ", ColorDim)
                .SpanLine(string.IsNullOrEmpty(Application.version) ? "0.0.0" : Application.version,
                          ColorTitle, modifiers: Modifier.Bold)
                .Render();

            string hint = ToggleKey == KeyCode.None
                ? "ESC close"
                : ToggleKey + " toggle  -  ESC close";
            term.SetStyle(ColorDim, Color.clear, Modifier.None);
            term.Paragraph(cols[1], hint + " ", Alignment.Right, wrap: false);
        }

        private void BuildBody(RatatuiTerminal term, uint area)
        {
            var bodyRows = term.Split(area, Direction.Vertical,
                Constraint.Fill(1),
                Constraint.Fill(1));
            if (bodyRows.Length < 2) return;

            var topCols = term.Split(bodyRows[0], Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));
            if (topCols.Length < 2) return;

            var bottomCols = term.Split(bodyRows[1], Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));
            if (bottomCols.Length < 2) return;

            BuildPerformancePanel(term, topCols[0]);
            BuildGcPanel(term, topCols[1]);
            BuildRenderingPanel(term, bottomCols[0]);
            BuildMemoryPanel(term, bottomCols[1]);
        }

        private void BuildPerformancePanel(RatatuiTerminal term, uint area)
        {
            term.SetStyle(ColorPerformance, Color.clear, Modifier.Bold);
            term.Block(area, " PERFORMANCE ", Borders.All);
            uint inner = term.Inner(area);

            var split = term.Split(inner, Direction.Vertical,
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Fill(1));
            if (split.Length < 4) return;

            Color fpsColor = FpsColor(_metrics.Fps);
            term.BeginStyledParagraph(split[0], Alignment.Left, wrap: false)
                .Span("FPS: ", ColorText)
                .SpanLine(_metrics.Fps.ToString("0.0"), fpsColor, modifiers: Modifier.Bold)
                .Render();

            term.BeginStyledParagraph(split[1], Alignment.Left, wrap: false)
                .Span("FRAME TIME: ", ColorText)
                .SpanLine(_metrics.FrameTimeMs.ToString("0.00") + " ms", ColorText)
                .Render();

            term.SetStyle(ColorDim, Color.clear, Modifier.None);
            term.Paragraph(split[2], "Goal: < " + _metrics.FrameTimeGoalMs.ToString("0.0") + " ms", Alignment.Left, wrap: false);

            term.SetStyle(ColorPerformance, Color.clear, Modifier.None);
            RenderFloatSparkline(term, split[3], ref _fpsSpark, _metrics.FpsHistory, clampMax: 200f);
        }

        private void BuildGcPanel(RatatuiTerminal term, uint area)
        {
            term.SetStyle(ColorGc, Color.clear, Modifier.Bold);
            term.Block(area, " GC ", Borders.All);
            uint inner = term.Inner(area);

            var split = term.Split(inner, Direction.Vertical,
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Fill(1));
            if (split.Length < 5) return;

            term.BeginStyledParagraph(split[0], Alignment.Left, wrap: false)
                .Span("ALLOC/FRAME: ", ColorText)
                .SpanLine(FormatBytes(_metrics.GcAllocThisFrame), _metrics.GcAllocThisFrame == 0 ? ColorPerformance : ColorGc, modifiers: Modifier.Bold)
                .Render();

            term.SetStyle(ColorDim, Color.clear, Modifier.None);
            term.Paragraph(split[1], "Goal: 0 Bytes", Alignment.Left, wrap: false);

            term.BeginStyledParagraph(split[2], Alignment.Left, wrap: false)
                .Span("FREQUENCY: ", ColorText)
                .SpanLine(_metrics.GcFrequencyHz.ToString("0.0") + " Hz", ColorText)
                .Render();

            term.BeginStyledParagraph(split[3], Alignment.Left, wrap: false)
                .Span("TOTAL: ", ColorText)
                .SpanLine(FormatBytes(_metrics.GcTotalBytes), ColorText)
                .Render();

            term.SetStyle(ColorGc, Color.clear, Modifier.None);
            RenderLongSparkline(term, split[4], ref _gcBars, _metrics.GcAllocHistory);
        }

        private void BuildRenderingPanel(RatatuiTerminal term, uint area)
        {
            term.SetStyle(ColorRendering, Color.clear, Modifier.Bold);
            term.Block(area, " RENDERING ", Borders.All);
            uint inner = term.Inner(area);

            var split = term.Split(inner, Direction.Vertical,
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Fill(1));
            if (split.Length < 5) return;

            bool available = _metrics.Batches >= 0;

            term.BeginStyledParagraph(split[0], Alignment.Left, wrap: false)
                .Span("BATCHES: ", ColorText)
                .SpanLine(available ? _metrics.Batches.ToString() : "—", available ? ColorRendering : ColorDim, modifiers: Modifier.Bold)
                .Render();

            term.SetStyle(ColorDim, Color.clear, Modifier.None);
            term.Paragraph(split[1], "Batching efficiency", Alignment.Left, wrap: false);

            term.BeginStyledParagraph(split[2], Alignment.Left, wrap: false)
                .Span("SETPASS: ", ColorText)
                .SpanLine(available ? _metrics.SetPassCalls.ToString() : "—", available ? ColorRendering : ColorDim, modifiers: Modifier.Bold)
                .Render();

            term.SetStyle(ColorDim, Color.clear, Modifier.None);
            term.Paragraph(split[3], "CPU-GPU communication load", Alignment.Left, wrap: false);

            if (available)
            {
                term.SetStyle(ColorRendering, Color.clear, Modifier.None);
                RenderIntSparkline(term, split[4], ref _batchesSpark, _metrics.BatchesHistory);
            }
            else
            {
                term.SetStyle(ColorDim, Color.clear, Modifier.None);
                term.Paragraph(split[4], "(Editor only)", Alignment.Center, wrap: false);
            }
        }

        private void BuildMemoryPanel(RatatuiTerminal term, uint area)
        {
            term.SetStyle(ColorMemory, Color.clear, Modifier.Bold);
            term.Block(area, " MEMORY ", Borders.All);
            uint inner = term.Inner(area);

            var split = term.Split(inner, Direction.Vertical,
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Length(1),
                Constraint.Fill(1));
            if (split.Length < 5) return;

            string heapLine = _metrics.RamUsedAvailable
                ? FormatBytes(_metrics.SystemRamUsedBytes) + " / " + FormatBytes(_metrics.SystemRamTotalBytes)
                : "— / " + FormatBytes(_metrics.SystemRamTotalBytes);
            term.BeginStyledParagraph(split[0], Alignment.Left, wrap: false)
                .Span("HEAP: ", ColorText)
                .SpanLine(heapLine, ColorText)
                .Render();

            float ramRatio = (_metrics.RamUsedAvailable && _metrics.SystemRamTotalBytes > 0)
                ? Mathf.Clamp01((float)((double)_metrics.SystemRamUsedBytes / _metrics.SystemRamTotalBytes))
                : 0f;
            term.SetStyle(ColorMemory, Color.clear, Modifier.None);
            term.LineGauge(split[1], ramRatio, (ramRatio * 100f).ToString("0") + "%");

            string vramLine = _metrics.VRamUsedAvailable
                ? FormatBytes(_metrics.VRamUsedBytes) + " / " + FormatBytes(_metrics.VRamTotalBytes)
                : "— / " + FormatBytes(_metrics.VRamTotalBytes);
            term.BeginStyledParagraph(split[2], Alignment.Left, wrap: false)
                .Span("VRAM: ", ColorText)
                .SpanLine(vramLine, ColorText)
                .Render();

            term.SetStyle(ColorMemory, Color.clear, Modifier.None);
            term.LineGauge(split[3], 0f, "n/a");

            if (_metrics.RamUsedAvailable)
            {
                term.SetStyle(ColorMemory, Color.clear, Modifier.None);
                RenderLongSparkline(term, split[4], ref _heapSpark, _metrics.HeapUsedHistory);
            }
        }

        protected override void OnTerminalKeyDown(TerminalKeyEvent e)
        {
            if (!IsOpen) return;
            if (ShouldSuppressToggleKeyEvent(e)) return;

            if (e.Key == KeyCode.Escape)
            {
                SetOpen(false);
            }
        }

        protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
        {
            // Read-only overlay — no in-frame mouse interaction.
            // Window chrome (drag, close) is handled by the base renderer.
        }

        // Sparkline render helpers. Each one:
        //   1. Queries the area's cell width.
        //   2. Resizes the cached buffer to (width + 1) — last slot is a
        //      ghost equal to SparkScaleCeiling so ratatui's max(data)
        //      normalizer is pinned to our 1-minute max, not the visible
        //      window max.
        //   3. Walks the metric ring buffer, scales values by the 1-minute
        //      max, and writes them into visible positions oldest→newest
        //      so the newest bar lands at the right edge. Empty slots
        //      (history not yet collected) stay 0 on the left.
        private void RenderFloatSparkline(RatatuiTerminal term, uint area, ref ulong[] buf,
                                          float[] history, float clampMax)
        {
            if (!term.TryGetAreaRect(area, out _, out _, out int w, out _) || w <= 0) return;
            EnsureSparkBuffer(ref buf, w);

            float max1min = 0f;
            int hsize = ProfilerMetrics.HistorySize;
            for (int j = 0; j < hsize; j++)
            {
                float v = history[j];
                if (v < 0f) v = 0f;
                if (v > clampMax) v = clampMax;
                if (v > max1min) max1min = v;
            }
            if (max1min <= 0f) max1min = 1f;

            int head = _metrics.Head;
            double scale = SparkScaleCeiling / (double)max1min;
            for (int i = 0; i < w; i++)
            {
                int src = (head + 1 + i + (hsize - w)) % hsize;
                float v = history[src];
                if (v < 0f) v = 0f;
                if (v > clampMax) v = clampMax;
                ulong s = (ulong)(v * scale);
                if (s > SparkScaleCeiling) s = SparkScaleCeiling;
                buf[i] = s;
            }
            buf[w] = SparkScaleCeiling;
            term.Sparkline(area, buf);
        }

        private void RenderLongSparkline(RatatuiTerminal term, uint area, ref ulong[] buf,
                                         long[] history)
        {
            if (!term.TryGetAreaRect(area, out _, out _, out int w, out _) || w <= 0) return;
            EnsureSparkBuffer(ref buf, w);

            ulong max1min = 0;
            int hsize = ProfilerMetrics.HistorySize;
            for (int j = 0; j < hsize; j++)
            {
                long v = history[j];
                if (v > 0 && (ulong)v > max1min) max1min = (ulong)v;
            }
            if (max1min == 0) max1min = 1;

            int head = _metrics.Head;
            for (int i = 0; i < w; i++)
            {
                int src = (head + 1 + i + (hsize - w)) % hsize;
                long v = history[src];
                if (v < 0) v = 0;
                ulong s = (ulong)((double)v * SparkScaleCeiling / max1min);
                if (s > SparkScaleCeiling) s = SparkScaleCeiling;
                buf[i] = s;
            }
            buf[w] = SparkScaleCeiling;
            term.Sparkline(area, buf);
        }

        private void RenderIntSparkline(RatatuiTerminal term, uint area, ref ulong[] buf,
                                        int[] history)
        {
            if (!term.TryGetAreaRect(area, out _, out _, out int w, out _) || w <= 0) return;
            EnsureSparkBuffer(ref buf, w);

            ulong max1min = 0;
            int hsize = ProfilerMetrics.HistorySize;
            for (int j = 0; j < hsize; j++)
            {
                int v = history[j];
                if (v > 0 && (ulong)v > max1min) max1min = (ulong)v;
            }
            if (max1min == 0) max1min = 1;

            int head = _metrics.Head;
            for (int i = 0; i < w; i++)
            {
                int src = (head + 1 + i + (hsize - w)) % hsize;
                int v = history[src];
                if (v < 0) v = 0;
                ulong s = (ulong)((double)v * SparkScaleCeiling / max1min);
                if (s > SparkScaleCeiling) s = SparkScaleCeiling;
                buf[i] = s;
            }
            buf[w] = SparkScaleCeiling;
            term.Sparkline(area, buf);
        }

        private static void EnsureSparkBuffer(ref ulong[] buf, int visibleWidth)
        {
            int needed = visibleWidth + 1;
            if (buf == null || buf.Length != needed) buf = new ulong[needed];
        }

        private static Color FpsColor(float fps)
        {
            if (fps >= 55f) return ColorFpsGood;
            if (fps >= 28f) return ColorFpsWarn;
            return ColorFpsBad;
        }

        private static string FormatBytes(long b)
        {
            if (b < 0) return "—";
            if (b < 1024L) return b + " B";
            if (b < 1024L * 1024L) return (b / 1024f).ToString("0.0") + " KB";
            if (b < 1024L * 1024L * 1024L) return (b / (1024f * 1024f)).ToString("0.0") + " MB";
            return (b / (1024f * 1024f * 1024f)).ToString("0.00") + " GB";
        }

        private void ApplyConfigToBase(RatatuiProfilerConfig cfg)
        {
            Cols = cfg.cols;
            Rows = cfg.rows;
            FontSize = cfg.fontSize;
            SizingMode = cfg.sizingMode;
            BackgroundColor = cfg.backgroundColor;
            OnGuiDisplayMode = cfg.displayMode;
            OnGuiHorizontalAlignment = cfg.horizontalAlign;
            OnGuiVerticalAlignment = cfg.verticalAlign;
            WindowStartMaximized = cfg.windowStartMaximized;
            WindowInitialWidth = cfg.windowInitialWidth;
            WindowInitialHeight = cfg.windowInitialHeight;
            WindowChromeFont = cfg.windowChromeFont;
            FitColsAndRows = true;
            EnableInput = true;
            EnableMouseInput = true;
            EnableKeyboardInput = true;
        }
    }
}
