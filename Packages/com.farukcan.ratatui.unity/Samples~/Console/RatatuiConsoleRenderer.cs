using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// MonoBehaviour that owns the console UI. Drives layout, log filtering,
    /// command prompt, autocomplete popup, and the per-log detail panel.
    /// Created automatically by <see cref="RatatuiConsole.Bootstrap"/> — do not
    /// add to scenes manually.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class RatatuiConsoleRenderer : RatatuiRenderer
    {
        public enum Filter : byte { All = 0, Logs = 1, Warnings = 2, Errors = 3, Exceptions = 4 }
        private enum InputFocus : byte { Prompt = 0, Search = 1 }

        // ── Persistent UI state ──────────────────────────────────────────────

        private RatatuiConsoleConfig _config;
        private bool _isOpen;
        private int _freshOpenFrames; // skip mouse input until BuildFrame populates area_map
        private InputFocus _focus = InputFocus.Prompt;

        // Hard-coded touch toggle: 4-finger tap. Tracks peak simultaneous touch
        // count across an active session; fires on full release so the toggle
        // happens AFTER the gesture ends — otherwise lingering touches in the
        // next frame would immediately cancel the open we just performed.
        private const int TouchToggleFingerCount = 4;
        private int _touchSessionMaxCount;

        private readonly StringBuilder _promptBuffer = new StringBuilder(128);
        private readonly StringBuilder _searchBuffer = new StringBuilder(64);

        private Filter _filter = Filter.All;
        private readonly List<int> _visibleIndices = new List<int>(256);
        private int _filterCacheGeneration = -1;
        private Filter _filterCacheFilter = Filter.All;
        private string _filterCacheSearch = string.Empty;
        // Incremental cache state: avoids re-scanning the entire ring on every
        // Generation bump when only new entries have arrived (the common case
        // under a log burst). _filterScanUpTo is the entry-count we've already
        // considered; _lastTotalEvicted lets us shift stored positional indices
        // down when the ring drops oldest entries between drains.
        private int _filterScanUpTo;
        private long _lastTotalEvicted;

        // Identity-stable selection: absolute index into Logs.Entries, not _visibleIndices.
        private int _selectedEntryIndex = -1;
        private bool _detailOpen;
        private int _detailScroll;

        // Log scroll is measured in DISPLAY ROWS (not vis indices), because each
        // log can occupy several rows when its message contains newlines.
        private int _logScroll;
        private int _lastLogGeneration = -1;
        private bool _followLogTail = true;
        private bool _forceLogScrollToBottom;
        // For each entry in _visibleIndices, the display row at which it starts.
        private readonly List<int> _logRowStarts = new List<int>(256);
        private int _logTotalRows;

        private readonly List<ConsoleSuggestion> _suggestions = new List<ConsoleSuggestion>(8);
        private int _suggestionIndex;

        // ── Per-frame area IDs (used by mouse hit-testing) ───────────────────

        private uint _tabAllArea, _tabLogsArea, _tabWarnsArea, _tabErrsArea, _tabExcArea;
        private uint _searchArea;
        private uint _logListArea;
        private uint _detailPanelArea;
        private uint _detailStackArea;
        private uint _detailCopyArea;
        private uint _detailEmailArea;
        private uint _detailCloseArea;
        private uint _autocompleteArea;
        private uint _promptArea;

        public bool IsOpen => _isOpen;

        // ── Style ────────────────────────────────────────────────────────────

        private static readonly Color ColorLog = new Color(0.85f, 0.85f, 0.9f);
        private static readonly Color ColorWarn = new Color(1.0f, 0.85f, 0.2f);
        private static readonly Color ColorError = new Color(1.0f, 0.35f, 0.35f);
        private static readonly Color ColorException = new Color(0.9f, 0.4f, 1.0f);
        private static readonly Color ColorTimestamp = new Color(0.5f, 0.5f, 0.55f);
        private static readonly Color ColorPromptText = new Color(0.95f, 0.95f, 1.0f);
        private static readonly Color ColorPromptDim = new Color(0.55f, 0.55f, 0.6f);
        private static readonly Color ColorTabIdle = new Color(0.55f, 0.55f, 0.6f);
        private static readonly Color ColorButton = new Color(0.85f, 0.85f, 0.9f);
        private static readonly Color ColorButtonHi = new Color(0.4f, 0.8f, 1.0f);
        private static readonly Color ColorSelectionBg = new Color(0.15f, 0.3f, 0.5f);
        private static readonly Color ColorHoverBg = new Color(0.12f, 0.2f, 0.32f);

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected override void Awake()
        {
            _config = RatatuiConsole.Config;
            if (_config == null) _config = RatatuiConsoleConfig.CreateDefault();
            ApplyConfigToBase(_config);
            base.Awake();
        }

        protected override void Update()
        {
            HandleToggleKey();

            // Always drain pending logs so the queue stays bounded even when the
            // console is closed for hours. The ring buffer applies its cap here.
            // Budget bounds the worst-case per-frame work so a log burst cannot
            // stall the renderer; the remainder drains over subsequent frames.
            RatatuiConsole.Logs?.DrainPending(ConsoleLogCapture.DefaultDrainBudget);

            if (_freshOpenFrames > 0) _freshOpenFrames--;

            if (!_isOpen) return; // skip render pipeline + input while closed
            base.Update();
        }

        protected override void OnGUI()
        {
            if (!_isOpen) return;
            base.OnGUI();
        }

        // ── Open / Close ─────────────────────────────────────────────────────

        public void SetOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;
            if (open)
            {
                // Keyboard is gated to the focused renderer; opening via toggle/API must
                // take scene focus immediately so typing works without a click first.
                RequestFocus();
                _focus = InputFocus.Prompt;

                // The area_map is stale or empty after a closed period; ignore mouse
                // events for one frame so the next BuildFrame can populate it before
                // ProcessMouse runs against it.
                _freshOpenFrames = 1;
                _followLogTail = true;
                _forceLogScrollToBottom = true;
            }
            else
            {
                ClearSuggestions();
            }
        }

        private void HandleToggleKey()
        {
            if (Input.GetKeyDown(_config.toggleKey))
                SetOpen(!_isOpen);

            HandleTouchToggle();
        }

        private void HandleTouchToggle()
        {
            int count = Input.touchCount;
            if (count > _touchSessionMaxCount) _touchSessionMaxCount = count;

            if (count == 0 && _touchSessionMaxCount > 0)
            {
                bool fire = _touchSessionMaxCount >= TouchToggleFingerCount;
                _touchSessionMaxCount = 0;
                if (fire) SetOpen(!_isOpen);
            }
        }

        // ── Frame Building ───────────────────────────────────────────────────

        protected override void BuildFrame(RatatuiTerminal term)
        {
            if (!_isOpen) return;

            RebuildFilterCacheIfNeeded();
            RebuildSuggestionsIfNeeded();

            uint root = term.RootArea;

            int popupHeight = ComputePopupHeight();
            uint[] outer = term.Split(root, Direction.Vertical,
                Constraint.Length(3),
                Constraint.Fill(1),
                Constraint.Length((ushort)popupHeight),
                Constraint.Length(3));

            BuildHeader(term, outer[0]);
            BuildBody(term, outer[1]);
            BuildAutocomplete(term, outer[2], popupHeight);
            BuildPrompt(term, outer[3]);
        }

        private int ComputePopupHeight()
        {
            if (_focus != InputFocus.Prompt) return 0;
            if (_suggestions.Count == 0) return 0;
            return Mathf.Min(_suggestions.Count + 2, 8);
        }

        // Fallback heights used when TryGetAreaRect cannot resolve a brand-new
        // current-frame area (the native area_map only contains last frame's IDs).
        private int FallbackBodyInnerHeight()
        {
            int popup = ComputePopupHeight();
            int body = Mathf.Max(1, _config.rows - 3 - popup - 3);
            return Mathf.Max(1, body - 2); // minus block borders
        }

        private int FallbackDetailStackHeight()
        {
            int bodyInner = FallbackBodyInnerHeight();
            // detail panel inner = bodyInner (same height as the log-list inner)
            // Internal split: message(5) + separator(1) + stack(?) + buttons(3)
            return Mathf.Max(1, bodyInner - 5 - 1 - 3);
        }

        // ── Header (Tabs + Search) ───────────────────────────────────────────

        private void BuildHeader(RatatuiTerminal term, uint area)
        {
            term.Block(area, " FILTERS ", Borders.All);
            uint inner = term.Inner(area);

            uint[] cols = term.Split(inner, Direction.Horizontal,
                Constraint.Length(16),
                Constraint.Length(17),
                Constraint.Length(21),
                Constraint.Length(19),
                Constraint.Length(23),
                Constraint.Fill(1));

            _tabAllArea = cols[0];
            _tabLogsArea = cols[1];
            _tabWarnsArea = cols[2];
            _tabErrsArea = cols[3];
            _tabExcArea = cols[4];
            _searchArea = cols[5];

            CountByKind(out int allCount, out int logCount, out int warnCount, out int errCount, out int excCount);

            DrawTab(term, cols[0], $"(F2) [{allCount} ALL]", Filter.All, new Color(0.85f, 0.85f, 0.85f));
            DrawTab(term, cols[1], $"(F3) [{logCount} LOGS]", Filter.Logs, new Color(0.45f, 0.85f, 0.9f));
            DrawTab(term, cols[2], $"(F4) [{warnCount} WARNINGS]", Filter.Warnings, ColorWarn);
            DrawTab(term, cols[3], $"(F5) [{errCount} ERRORS]", Filter.Errors, ColorError);
            DrawTab(term, cols[4], $"(F6) [{excCount} EXCEPTIONS]", Filter.Exceptions, ColorException);
            DrawSearch(term, cols[5]);
        }

        private void CountByKind(out int all, out int logs, out int warns, out int errs, out int excs)
        {
            // Incremental counters are maintained by ConsoleLogCapture on every
            // push / evict. Avoids an O(N) tally every header rebuild.
            var capture = RatatuiConsole.Logs;
            if (capture == null) { all = logs = warns = errs = excs = 0; return; }
            logs = capture.LogCount;
            warns = capture.WarningCount;
            errs = capture.ErrorAndAssertCount;
            excs = capture.ExceptionCount;
            all = logs + warns + errs + excs;
        }

        private void DrawTab(RatatuiTerminal term, uint area, string label, Filter filter, Color activeColor)
        {
            // Always render the tab in its own color so the legend is readable
            // even when the tab is not selected. Selection is signalled by Bold
            // + a contrasting background — not by color presence/absence.
            bool active = _filter == filter;
            bool hover = IsHovering(area);
            Color bg = active ? ColorSelectionBg : (hover ? ColorHoverBg : Color.clear);
            Color fg = hover && !active ? ColorButtonHi : activeColor;
            var mods = active || hover ? Modifier.Bold : Modifier.None;
            term.BeginStyledParagraph(area, Alignment.Center, false)
                .Span(label, fg: fg, bg: bg, modifiers: mods)
                .Render();
        }

        private void DrawSearch(RatatuiTerminal term, uint area)
        {
            bool focused = _focus == InputFocus.Search;
            bool hover = IsHovering(area);
            string text = _searchBuffer.Length > 0 ? _searchBuffer.ToString() : "";
            Color hintColor = hover ? ColorButtonHi : new Color(0.6f, 0.85f, 1.0f);
            Color bg = hover ? ColorHoverBg : Color.clear;

            var sp = term.BeginStyledParagraph(area, Alignment.Left, false)
                .Span(" (F7) ", fg: hintColor, bg: bg, modifiers: Modifier.Bold);

            if (text.Length == 0)
            {
                sp.Span("SEARCH LOGS...", fg: focused ? ColorPromptText : ColorPromptDim,
                    bg: bg, modifiers: focused ? Modifier.None : Modifier.Dim);
            }
            else
            {
                sp.Span(text, fg: ColorPromptText, bg: bg);
            }
            if (focused) sp.Span("_", fg: ColorPromptText, bg: bg, modifiers: Modifier.Bold);
            sp.Render();
        }

        // ── Body (Log list + optional Detail panel) ──────────────────────────

        private void BuildBody(RatatuiTerminal term, uint area)
        {
            if (_detailOpen && _selectedEntryIndex >= 0)
            {
                uint[] cols = term.Split(area, Direction.Horizontal,
                    Constraint.Percentage(40),
                    Constraint.Percentage(60));
                BuildLogList(term, cols[0]);
                BuildDetailPanel(term, cols[1]);
            }
            else
            {
                _detailPanelArea = 0;
                _detailStackArea = 0;
                _detailCopyArea = 0;
                _detailEmailArea = 0;
                _detailCloseArea = 0;
                BuildLogList(term, area);
            }
        }

        private void BuildLogList(RatatuiTerminal term, uint area)
        {
            term.Block(area, "", Borders.All);
            uint inner = term.Inner(area);

            uint[] cols = term.Split(inner, Direction.Horizontal,
                Constraint.Fill(1),
                Constraint.Length(1));
            uint content = cols[0];
            uint scroll = cols[1];
            _logListArea = content;

            int innerHeight = FallbackBodyInnerHeight();
            if (term.TryGetAreaRect(content, out _, out _, out _, out int h) && h > 0)
                innerHeight = h;

            var entries = RatatuiConsole.Logs?.Entries;
            RecomputeLogLayout(entries);

            int gen = RatatuiConsole.Logs?.Generation ?? 0;
            bool logsChanged = gen != _lastLogGeneration;
            _lastLogGeneration = gen;

            if (_forceLogScrollToBottom || (logsChanged && _followLogTail))
                ScrollLogToBottom(innerHeight);
            else
                ClampLogScroll(_logTotalRows, innerHeight);

            if (entries == null) return;

            int viewStart = _logScroll;
            int viewEnd = _logScroll + innerHeight;
            int rowsEmitted = 0;
            int hoveredVis = GetHoveredLogVisIndex();

            var sp = term.BeginStyledParagraph(content, Alignment.Left, false);

            for (int vis = 0; vis < _visibleIndices.Count; vis++)
            {
                int rowStart = _logRowStarts[vis];
                int rowEnd = GetLogRowEnd(vis); // exclusive
                if (rowEnd <= viewStart) continue;       // entirely above viewport
                if (rowStart >= viewEnd) break;          // entirely below viewport

                int idx = _visibleIndices[vis];
                if (idx < 0 || idx >= entries.Count) continue;
                var entry = entries[idx];
                bool selected = idx == _selectedEntryIndex;
                bool hover = vis == hoveredVis;
                Color rowBg = selected ? ColorSelectionBg : (hover ? ColorHoverBg : Color.clear);
                Color msgColor = KindForeground(entry.Kind);

                string normalized = (entry.Message ?? string.Empty)
                    .Replace("\r\n", "\n").Replace('\r', '\n');
                string[] lines = normalized.Length == 0
                    ? new[] { string.Empty }
                    : normalized.Split('\n');

                string ts = _config.showTimestamp
                    ? "[" + entry.Time.ToString("HH:mm:ss") + "] "
                    : string.Empty;
                string tag = KindTag(entry.Kind) + " ";
                Color tagColor = KindColor(entry.Kind);
                // Continuation lines of the same entry indent so they visually
                // align under the message column instead of under the timestamp.
                string contIndent = new string(' ', ts.Length + tag.Length);

                for (int li = 0; li < lines.Length; li++)
                {
                    int currentRow = rowStart + li;
                    if (currentRow < viewStart) continue;
                    if (currentRow >= viewEnd) break;

                    if (rowsEmitted > 0) sp.Line();

                    if (li == 0)
                    {
                        if (ts.Length > 0) sp.Span(ts, fg: ColorTimestamp, bg: rowBg);
                        sp.Span(tag, fg: tagColor, bg: rowBg, modifiers: Modifier.Bold);
                        sp.Span(lines[0], fg: msgColor, bg: rowBg);
                        if (entry.Repeat > 1)
                            sp.Span(" (×" + entry.Repeat + ")", fg: ColorPromptDim,
                                bg: rowBg, modifiers: Modifier.Bold);
                    }
                    else
                    {
                        sp.Span(contIndent, bg: rowBg);
                        sp.Span(lines[li], fg: msgColor, bg: rowBg);
                    }
                    rowsEmitted++;
                }
            }
            sp.Render();

            term.Scrollbar(scroll, _logTotalRows, _logScroll, innerHeight,
                ScrollbarOrientation.VerticalRight, autoHide: true);
        }

        private void RecomputeLogLayout(IReadOnlyList<ConsoleLogEntry> entries)
        {
            _logRowStarts.Clear();
            _logTotalRows = 0;
            if (entries == null) return;
            for (int vis = 0; vis < _visibleIndices.Count; vis++)
            {
                _logRowStarts.Add(_logTotalRows);
                int idx = _visibleIndices[vis];
                int lines = (idx >= 0 && idx < entries.Count)
                    ? CountVisualLines(entries[idx].Message)
                    : 1;
                _logTotalRows += lines;
            }
        }

        private int GetLogRowEnd(int vis)
        {
            if (vis + 1 < _logRowStarts.Count) return _logRowStarts[vis + 1];
            return _logTotalRows;
        }

        private void BuildDetailPanel(RatatuiTerminal term, uint area)
        {
            term.Block(area, " LOG DETAILS & STACKTRACE ", Borders.All);
            uint inner = term.Inner(area);
            _detailPanelArea = inner;

            var entries = RatatuiConsole.Logs?.Entries;
            if (entries == null) return;
            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= entries.Count) return;
            var entry = entries[_selectedEntryIndex];

            int messageLines = Mathf.Clamp(CountVisualLines(entry.Message) + 1, 3, 12);
            uint[] rows = term.Split(inner, Direction.Vertical,
                Constraint.Length((ushort)messageLines),
                Constraint.Length(1),
                Constraint.Fill(1),
                Constraint.Length(3));

            string tag = KindTag(entry.Kind);
            var msg = term.BeginStyledParagraph(rows[0], Alignment.Left, wrap: true);
            msg.Span(tag + " ", fg: KindColor(entry.Kind), modifiers: Modifier.Bold);
            RenderMultiline(msg, entry.Message ?? string.Empty,
                KindForeground(entry.Kind), Modifier.None);
            msg.Render();

            term.BeginStyledParagraph(rows[1], Alignment.Left, false)
                .Span("CALL STACK:", fg: ColorPromptDim, modifiers: Modifier.Bold)
                .Render();

            BuildStackTrace(term, rows[2], entry);

            uint[] btnCols = term.Split(rows[3], Direction.Horizontal,
                Constraint.Fill(1),
                Constraint.Fill(1),
                Constraint.Fill(1));

            _detailCopyArea = DrawButton(term, btnCols[0], "[ COPY ]");
            _detailEmailArea = DrawButton(term, btnCols[1], "[ EMAIL ]");
            _detailCloseArea = DrawButton(term, btnCols[2], "[ CLOSE ]");
        }

        private void BuildStackTrace(RatatuiTerminal term, uint area, ConsoleLogEntry entry)
        {
            // Reserve right column for scrollbar.
            uint[] cols = term.Split(area, Direction.Horizontal,
                Constraint.Fill(1),
                Constraint.Length(1));
            uint content = cols[0];
            uint scroll = cols[1];
            _detailStackArea = content;

            int areaHeight = FallbackDetailStackHeight();
            if (term.TryGetAreaRect(content, out _, out _, out _, out int h) && h > 0)
                areaHeight = h;

            string stack = string.IsNullOrEmpty(entry.StackTrace)
                ? "(no stack trace)"
                : entry.StackTrace;

            string[] lines = stack.Replace("\r", "").Split('\n');
            ClampDetailScroll(lines.Length, areaHeight);

            var sp = term.BeginStyledParagraph(content, Alignment.Left, true);
            int start = _detailScroll;
            int end = lines.Length;
            for (int i = start; i < end; i++)
            {
                sp.Span(lines[i], fg: ColorPromptText, modifiers: Modifier.Dim);
                if (i < end - 1) sp.Line();
            }
            sp.Render();

            term.Scrollbar(scroll, lines.Length, _detailScroll, areaHeight,
                ScrollbarOrientation.VerticalRight, autoHide: true);
        }

        private uint DrawButton(RatatuiTerminal term, uint area, string label)
        {
            term.Block(area, "", Borders.All);
            uint inner = term.Inner(area);
            bool hover = IsHovering(inner);
            var color = hover ? ColorButtonHi : ColorButton;
            term.BeginStyledParagraph(inner, Alignment.Center, false)
                .Span(label, fg: color, bg: hover ? ColorHoverBg : Color.clear,
                    modifiers: hover ? Modifier.Bold : Modifier.None)
                .Render();
            return inner;
        }

        private bool IsHovering(uint areaId)
        {
            return areaId != 0 && HoverState.IsInside && HoverState.AreaId == areaId;
        }

        private int GetHoveredLogVisIndex()
        {
            if (!IsHovering(_logListArea)) return -1;
            if (!Terminal.TryGetAreaRect(_logListArea, out _, out int areaY, out _, out int areaH))
                return -1;

            int rel = HoverState.Row - areaY;
            if (rel < 0 || rel >= areaH) return -1;

            int displayRow = _logScroll + rel;
            for (int vis = 0; vis < _logRowStarts.Count; vis++)
            {
                int start = _logRowStarts[vis];
                int end = GetLogRowEnd(vis);
                if (displayRow < start) break;
                if (displayRow >= start && displayRow < end)
                    return vis;
            }
            return -1;
        }

        private int GetHoveredSuggestionIndex()
        {
            if (!IsHovering(_autocompleteArea)) return -1;
            if (!Terminal.TryGetAreaRect(_autocompleteArea, out _, out int areaY, out _, out int areaH))
                return -1;

            int rel = HoverState.Row - areaY;
            if (rel < 0 || rel >= areaH || rel >= _suggestions.Count) return -1;
            return rel;
        }

        // ── Autocomplete Popup ───────────────────────────────────────────────

        private void BuildAutocomplete(RatatuiTerminal term, uint area, int popupHeight)
        {
            _autocompleteArea = 0;
            if (popupHeight == 0) return;

            term.Block(area, "", Borders.All);
            uint inner = term.Inner(area);

            uint[] cols = term.Split(inner, Direction.Horizontal,
                Constraint.Fill(1),
                Constraint.Length(1));
            uint content = cols[0];
            uint scroll = cols[1];
            _autocompleteArea = content;

            int hoveredSuggestion = GetHoveredSuggestionIndex();

            var sp = term.BeginStyledParagraph(content, Alignment.Left, false);
            for (int i = 0; i < _suggestions.Count; i++)
            {
                var s = _suggestions[i];
                bool selected = i == _suggestionIndex;
                bool hover = i == hoveredSuggestion;
                Color rowBg = selected ? ColorSelectionBg : (hover ? ColorHoverBg : Color.clear);
                Color fg = selected || hover ? ColorButtonHi : ColorPromptText;
                var mods = selected || hover ? Modifier.Bold : Modifier.None;

                sp.Span(" ", bg: rowBg);
                sp.Span(s.Display, fg: fg, bg: rowBg, modifiers: mods);

                if (!string.IsNullOrEmpty(s.Detail))
                {
                    sp.Span("  —  ", fg: ColorPromptDim, bg: rowBg);
                    sp.Span(s.Detail, fg: ColorPromptDim, bg: rowBg, modifiers: Modifier.Dim);
                }

                if (i < _suggestions.Count - 1) sp.Line();
            }
            sp.Render();

            term.Scrollbar(scroll, _suggestions.Count, _suggestionIndex,
                Mathf.Min(_suggestions.Count, popupHeight - 2),
                ScrollbarOrientation.VerticalRight, autoHide: true);
        }

        // ── Prompt ───────────────────────────────────────────────────────────

        private void BuildPrompt(RatatuiTerminal term, uint area)
        {
            term.Block(area,
                " Tab=complete · ↑↓=history · F2-F6=filter · F7=search · Enter=run · Esc=close ",
                Borders.All);
            uint inner = term.Inner(area);
            _promptArea = inner;

            bool focused = _focus == InputFocus.Prompt;
            bool hover = IsHovering(_promptArea);
            bool cursorOn = focused && ((int)(Time.unscaledTime * 2f) % 2 == 0);
            Color bg = hover ? ColorHoverBg : Color.clear;
            Color prefixColor = focused || hover ? ColorButtonHi : ColorPromptDim;

            var sp = term.BeginStyledParagraph(inner, Alignment.Left, false)
                .Span("> ", fg: prefixColor, bg: bg, modifiers: Modifier.Bold)
                .Span(_promptBuffer.ToString(), fg: ColorPromptText, bg: bg);
            if (cursorOn) sp.Span("_", fg: ColorPromptText, bg: bg, modifiers: Modifier.Bold);
            sp.Render();
        }

        // ── Filter / Search Cache ────────────────────────────────────────────

        private void RebuildFilterCacheIfNeeded()
        {
            var capture = RatatuiConsole.Logs;
            int gen = capture?.Generation ?? 0;
            string search = _searchBuffer.Length > 0 ? _searchBuffer.ToString() : string.Empty;

            bool filterChanged = _filter != _filterCacheFilter || search != _filterCacheSearch;
            if (gen == _filterCacheGeneration && !filterChanged) return;

            _filterCacheFilter = _filter;
            _filterCacheSearch = search;

            var entries = capture?.Entries;
            if (entries == null)
            {
                _visibleIndices.Clear();
                _filterScanUpTo = 0;
                _lastTotalEvicted = 0;
                _filterCacheGeneration = gen;
                _selectedEntryIndex = -1;
                _detailOpen = false;
                _detailScroll = 0;
                return;
            }

            long ev = capture.TotalEvicted;

            if (filterChanged)
            {
                // Full rebuild — the active filter/search predicate changed, so
                // previously-accepted indices may no longer qualify.
                _visibleIndices.Clear();
                bool selectedStillVisible = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!FilterAllows(_filter, entries[i].Kind)) continue;
                    if (search.Length > 0 &&
                        entries[i].Message.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    _visibleIndices.Add(i);
                    if (i == _selectedEntryIndex) selectedStillVisible = true;
                }
                _filterScanUpTo = entries.Count;
                if (!selectedStillVisible)
                {
                    _selectedEntryIndex = -1;
                    _detailOpen = false;
                    _detailScroll = 0;
                }
            }
            else
            {
                // Incremental: filter/search unchanged, generation bumped. Shift
                // stored positional indices for any evictions, then scan only the
                // newly-appended tail of the ring.
                int evictedDelta = (int)(ev - _lastTotalEvicted);
                if (evictedDelta > 0)
                {
                    int write = 0;
                    for (int r = 0; r < _visibleIndices.Count; r++)
                    {
                        int v = _visibleIndices[r] - evictedDelta;
                        if (v >= 0) _visibleIndices[write++] = v;
                    }
                    if (write < _visibleIndices.Count)
                        _visibleIndices.RemoveRange(write, _visibleIndices.Count - write);
                    _filterScanUpTo = Mathf.Max(0, _filterScanUpTo - evictedDelta);

                    if (_selectedEntryIndex >= 0)
                    {
                        _selectedEntryIndex -= evictedDelta;
                        if (_selectedEntryIndex < 0)
                        {
                            _selectedEntryIndex = -1;
                            _detailOpen = false;
                            _detailScroll = 0;
                        }
                    }
                }

                for (int i = _filterScanUpTo; i < entries.Count; i++)
                {
                    if (!FilterAllows(_filter, entries[i].Kind)) continue;
                    if (search.Length > 0 &&
                        entries[i].Message.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    _visibleIndices.Add(i);
                }
                _filterScanUpTo = entries.Count;
            }

            _lastTotalEvicted = ev;
            _filterCacheGeneration = gen;
        }

        private static bool FilterAllows(Filter filter, ConsoleLogKind kind)
        {
            switch (filter)
            {
                case Filter.All: return true;
                case Filter.Logs: return kind == ConsoleLogKind.Log;
                case Filter.Warnings: return kind == ConsoleLogKind.Warning;
                case Filter.Errors: return kind == ConsoleLogKind.Error || kind == ConsoleLogKind.Assert;
                case Filter.Exceptions: return kind == ConsoleLogKind.Exception;
                default: return true;
            }
        }

        private void RebuildSuggestionsIfNeeded()
        {
            string text = _promptBuffer.ToString();
            _suggestions.Clear();
            if (_focus == InputFocus.Prompt && text.Length > 0)
            {
                int firstSpace = text.IndexOf(' ');
                if (firstSpace < 0)
                {
                    var matches = RatatuiConsole.Registry?.Match(text, 6);
                    if (matches != null)
                    {
                        foreach (var cmd in matches)
                        {
                            _suggestions.Add(new ConsoleSuggestion(
                                cmd.Name, cmd.Description, cmd.Name, -1, true));
                        }
                    }
                }
                else
                {
                    var matches = BuiltinCommands.CompletePath(text, 6);
                    _suggestions.AddRange(matches);
                }
            }
            if (_suggestionIndex >= _suggestions.Count) _suggestionIndex = 0;
        }

        // ── Input ────────────────────────────────────────────────────────────

        protected override void OnTerminalKeyDown(TerminalKeyEvent e)
        {
            if (!_isOpen) return;

            // Drop any event originating from the held toggle key. This covers:
            //   (a) the special-key Down event for tracked toggle keys,
            //   (b) the character that the toggle key emits via Input.inputString,
            //   (c) OS auto-repeat characters while the key is still held.
            if (Input.GetKey(_config.toggleKey))
            {
                if (e.Key == _config.toggleKey) return;
                if (e.Character != '\0' && IsToggleKeyCharacter(_config.toggleKey, e.Character))
                    return;
            }

            if (e.Key == KeyCode.Tab && e.HasCtrl)
            {
                CycleFilter(e.HasShift ? -1 : 1);
                return;
            }

            // Function-key shortcuts work regardless of focus.
            switch (e.Key)
            {
                case KeyCode.F2: _filter = Filter.All; _logScroll = 0; return;
                case KeyCode.F3: _filter = Filter.Logs; _logScroll = 0; return;
                case KeyCode.F4: _filter = Filter.Warnings; _logScroll = 0; return;
                case KeyCode.F5: _filter = Filter.Errors; _logScroll = 0; return;
                case KeyCode.F6: _filter = Filter.Exceptions; _logScroll = 0; return;
                case KeyCode.F7:
                    _focus = _focus == InputFocus.Search ? InputFocus.Prompt : InputFocus.Search;
                    return;
            }

            if (e.Key == KeyCode.PageUp) { MoveLogSelection(-1); return; }
            if (e.Key == KeyCode.PageDown) { MoveLogSelection(1); return; }

            if (_focus == InputFocus.Search) HandleSearchKey(e);
            else HandlePromptKey(e);
        }

        private static bool IsToggleKeyCharacter(KeyCode key, char c)
        {
            switch (key)
            {
                case KeyCode.BackQuote: return c == '`' || c == '~';
                case KeyCode.Tilde: return c == '~';
                default: return false;
            }
        }

        private void HandlePromptKey(TerminalKeyEvent e)
        {
            switch (e.Key)
            {
                case KeyCode.Tab:
                    ApplyTopSuggestion();
                    return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SubmitPromptOrToggleDetail();
                    return;
                case KeyCode.Escape:
                    if (_detailOpen) { _detailOpen = false; return; }
                    SetOpen(false);
                    return;
                case KeyCode.Backspace:
                    if (_promptBuffer.Length > 0)
                        _promptBuffer.Length--;
                    RatatuiConsole.History.Reset();
                    return;
                case KeyCode.UpArrow:
                    NavigateUp();
                    return;
                case KeyCode.DownArrow:
                    NavigateDown();
                    return;
            }
            if (e.Character != '\0' && !char.IsControl(e.Character))
            {
                _promptBuffer.Append(e.Character);
                RatatuiConsole.History.Reset();
            }
        }

        private void HandleSearchKey(TerminalKeyEvent e)
        {
            switch (e.Key)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Escape:
                    _focus = InputFocus.Prompt;
                    return;
                case KeyCode.Backspace:
                    if (_searchBuffer.Length > 0)
                        _searchBuffer.Length--;
                    return;
            }
            if (e.Character != '\0' && !char.IsControl(e.Character))
                _searchBuffer.Append(e.Character);
        }

        protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
        {
            if (!_isOpen) return;
            // The frame after Open(), area_map is stale; drop mouse events for one frame.
            if (_freshOpenFrames > 0) return;

            if (e.Type == MouseEventType.Scroll)
            {
                HandleScroll(e);
                return;
            }
            if (e.Type != MouseEventType.Click || e.Button != MouseButton.Left) return;

            uint a = e.AreaId;
            if (a == 0) return;

            if (a == _tabAllArea) { _filter = Filter.All; _logScroll = 0; return; }
            if (a == _tabLogsArea) { _filter = Filter.Logs; _logScroll = 0; return; }
            if (a == _tabWarnsArea) { _filter = Filter.Warnings; _logScroll = 0; return; }
            if (a == _tabErrsArea) { _filter = Filter.Errors; _logScroll = 0; return; }
            if (a == _tabExcArea) { _filter = Filter.Exceptions; _logScroll = 0; return; }
            if (a == _searchArea) { _focus = InputFocus.Search; return; }

            if (a == _detailCopyArea) { CopySelectedStackTrace(); return; }
            if (a == _detailEmailArea) { EmailSelectedLog(); return; }
            if (a == _detailCloseArea) { _detailOpen = false; return; }

            if (a == _logListArea)
            {
                SelectLogAtScreenRow(e.Row);
                return;
            }

            if (a == _autocompleteArea)
            {
                SelectSuggestionAtScreenRow(e.Row);
                return;
            }

            if (a == _promptArea)
            {
                _focus = InputFocus.Prompt;
            }
        }

        private void HandleScroll(TerminalMouseEvent e)
        {
            int dir = e.ScrollDelta > 0 ? -1 : 1;
            if (_detailOpen && (e.AreaId == _detailPanelArea || e.AreaId == _detailStackArea))
            {
                _detailScroll = Mathf.Max(0, _detailScroll + dir);
            }
            else if (e.AreaId == _logListArea)
            {
                _logScroll = Mathf.Max(0, _logScroll + dir);
                int maxScroll = Mathf.Max(0, _logTotalRows - GetLogListViewportHeight());
                _logScroll = Mathf.Min(_logScroll, maxScroll);
                _followLogTail = _logScroll >= maxScroll;
            }
        }

        private void SelectLogAtScreenRow(int screenRow)
        {
            if (!Terminal.TryGetAreaRect(_logListArea, out _, out int areaY, out _, out int areaH))
                return;
            int rel = screenRow - areaY;
            if (rel < 0 || rel >= areaH) return;
            int displayRow = _logScroll + rel;

            // Find the entry whose display-row range contains this row.
            for (int vis = 0; vis < _logRowStarts.Count; vis++)
            {
                int start = _logRowStarts[vis];
                int end = GetLogRowEnd(vis);
                if (displayRow < start) break;
                if (displayRow >= start && displayRow < end)
                {
                    _selectedEntryIndex = _visibleIndices[vis];
                    _detailOpen = true;
                    _detailScroll = 0;
                    return;
                }
            }
        }

        private void SelectSuggestionAtScreenRow(int screenRow)
        {
            if (!Terminal.TryGetAreaRect(_autocompleteArea, out _, out int areaY, out _, out _))
                return;
            int rel = screenRow - areaY;
            if (rel < 0 || rel >= _suggestions.Count) return;
            _suggestionIndex = rel;
            ApplyTopSuggestion();
        }

        // ── Actions ──────────────────────────────────────────────────────────

        private void SubmitPromptOrToggleDetail()
        {
            if (_promptBuffer.Length == 0)
            {
                if (_selectedEntryIndex >= 0) _detailOpen = !_detailOpen;
                return;
            }
            string raw = _promptBuffer.ToString();
            _promptBuffer.Length = 0;
            RatatuiConsole.History.Push(raw);
            RatatuiConsole.ExecuteCommand(raw);
            ClearSuggestions();
        }

        private void ApplyTopSuggestion()
        {
            if (_suggestions.Count == 0) return;
            int idx = Mathf.Clamp(_suggestionIndex, 0, _suggestions.Count - 1);
            var s = _suggestions[idx];

            if (s.ReplaceFromIndex < 0)
                _promptBuffer.Length = 0;
            else
                _promptBuffer.Length = Mathf.Clamp(s.ReplaceFromIndex, 0, _promptBuffer.Length);

            _promptBuffer.Append(s.Insert);
            if (s.TrailingSpace) _promptBuffer.Append(' ');
            ClearSuggestions();
        }

        private void NavigateUp()
        {
            if (_suggestions.Count > 0)
            {
                _suggestionIndex = (_suggestionIndex - 1 + _suggestions.Count) % _suggestions.Count;
                return;
            }
            if (_promptBuffer.Length == 0)
            {
                MoveLogSelection(-1);
                return;
            }
            string hist = RatatuiConsole.History.MovePrev();
            if (hist == null) return;
            _promptBuffer.Length = 0;
            _promptBuffer.Append(hist);
        }

        private void NavigateDown()
        {
            if (_suggestions.Count > 0)
            {
                _suggestionIndex = (_suggestionIndex + 1) % _suggestions.Count;
                return;
            }
            if (_promptBuffer.Length == 0)
            {
                MoveLogSelection(1);
                return;
            }
            string hist = RatatuiConsole.History.MoveNext();
            if (hist == null) return;
            _promptBuffer.Length = 0;
            _promptBuffer.Append(hist);
        }

        private int GetSelectedVisIndex()
        {
            if (_selectedEntryIndex < 0) return -1;
            return _visibleIndices.IndexOf(_selectedEntryIndex);
        }

        private void MoveLogSelection(int delta)
        {
            if (_visibleIndices.Count == 0) return;
            int curVis = GetSelectedVisIndex();
            int nextVis = curVis < 0
                ? (delta > 0 ? 0 : _visibleIndices.Count - 1)
                : Mathf.Clamp(curVis + delta, 0, _visibleIndices.Count - 1);
            _selectedEntryIndex = _visibleIndices[nextVis];
            EnsureSelectedVisible();
        }

        private void EnsureSelectedVisible()
        {
            int curVis = GetSelectedVisIndex();
            if (curVis < 0 || curVis >= _logRowStarts.Count) return;
            int firstRow = _logRowStarts[curVis];
            int lastRow = GetLogRowEnd(curVis) - 1;
            int h = FallbackBodyInnerHeight();
            if (Terminal.TryGetAreaRect(_logListArea, out _, out _, out _, out int hRect) && hRect > 0)
                h = hRect;
            if (firstRow < _logScroll) _logScroll = firstRow;
            else if (lastRow >= _logScroll + h) _logScroll = lastRow - h + 1;
            _followLogTail = IsLogAtBottom(_logScroll, _logTotalRows, h);
        }

        private void CycleFilter(int dir)
        {
            int n = 5;
            int next = ((int)_filter + dir + n) % n;
            _filter = (Filter)next;
            _logScroll = 0;
        }

        private void CopySelectedStackTrace()
        {
            var entries = RatatuiConsole.Logs?.Entries;
            if (entries == null) return;
            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= entries.Count) return;
            var entry = entries[_selectedEntryIndex];
            string payload = string.IsNullOrEmpty(entry.StackTrace)
                ? entry.Message
                : entry.Message + "\n\n" + entry.StackTrace;
            GUIUtility.systemCopyBuffer = payload;
            Debug.Log("[RatatuiConsole] Log copied to clipboard.");
        }

        private void EmailSelectedLog()
        {
            var entries = RatatuiConsole.Logs?.Entries;
            if (entries == null) return;
            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= entries.Count) return;
            var entry = entries[_selectedEntryIndex];

            string body = string.IsNullOrEmpty(entry.StackTrace)
                ? entry.Message
                : entry.Message + "\n\n" + entry.StackTrace;

            string firstLine = entry.Message ?? string.Empty;
            int nl = firstLine.IndexOf('\n');
            if (nl >= 0) firstLine = firstLine.Substring(0, nl);
            if (firstLine.Length > 120) firstLine = firstLine.Substring(0, 120);

            string subject = "[" + Application.productName + "] " + KindTag(entry.Kind) + " " + firstLine;
            string url = "mailto:?subject=" + Uri.EscapeDataString(subject)
                       + "&body=" + Uri.EscapeDataString(body);
            Application.OpenURL(url);
        }

        private void ClearSuggestions()
        {
            _suggestions.Clear();
            _suggestionIndex = 0;
        }

        private void ScrollLogToBottom(int viewportHeight)
        {
            _forceLogScrollToBottom = false;
            int maxScroll = Mathf.Max(0, _logTotalRows - viewportHeight);
            _logScroll = maxScroll;
            _followLogTail = true;
        }

        private static bool IsLogAtBottom(int scroll, int totalRows, int viewportHeight)
        {
            int maxScroll = Mathf.Max(0, totalRows - viewportHeight);
            return scroll >= maxScroll;
        }

        private int GetLogListViewportHeight()
        {
            if (Terminal != null && Terminal.TryGetAreaRect(_logListArea, out _, out _, out _, out int h) && h > 0)
                return h;
            return FallbackBodyInnerHeight();
        }

        private void ClampLogScroll(int total, int viewportHeight)
        {
            int maxScroll = Mathf.Max(0, total - viewportHeight);
            _logScroll = Mathf.Clamp(_logScroll, 0, maxScroll);
        }

        private void ClampDetailScroll(int total, int viewportHeight)
        {
            int maxScroll = Mathf.Max(0, total - viewportHeight);
            _detailScroll = Mathf.Clamp(_detailScroll, 0, maxScroll);
        }

        // ── Multi-line helpers ───────────────────────────────────────────────

        private static int CountVisualLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int count = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') count++;
            return count;
        }

        private static void RenderMultiline(StyledText sp, string text, Color fg, Modifier modifiers)
        {
            if (string.IsNullOrEmpty(text)) return;
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            int start = 0;
            for (int i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] != '\n') continue;
                sp.Span(normalized.Substring(start, i - start), fg: fg, modifiers: modifiers);
                sp.Line();
                start = i + 1;
            }
            if (start < normalized.Length)
                sp.Span(normalized.Substring(start), fg: fg, modifiers: modifiers);
        }

        // ── Style helpers ────────────────────────────────────────────────────

        private static string KindTag(ConsoleLogKind kind)
        {
            switch (kind)
            {
                case ConsoleLogKind.Log: return "[LOG]";
                case ConsoleLogKind.Warning: return "[WARNING]";
                case ConsoleLogKind.Error: return "[ERROR]";
                case ConsoleLogKind.Exception: return "[EXCEPTION]";
                case ConsoleLogKind.Assert: return "[ASSERT]";
                default: return "[LOG]";
            }
        }

        private static Color KindColor(ConsoleLogKind kind)
        {
            switch (kind)
            {
                case ConsoleLogKind.Warning: return ColorWarn;
                case ConsoleLogKind.Error:
                case ConsoleLogKind.Assert: return ColorError;
                case ConsoleLogKind.Exception: return ColorException;
                default: return ColorLog;
            }
        }

        private static Color KindForeground(ConsoleLogKind kind)
        {
            switch (kind)
            {
                case ConsoleLogKind.Warning: return ColorWarn;
                case ConsoleLogKind.Error:
                case ConsoleLogKind.Assert: return ColorError;
                case ConsoleLogKind.Exception: return ColorException;
                default: return ColorPromptText;
            }
        }

        // ── Base-class configuration overrides ───────────────────────────────

        private void ApplyConfigToBase(RatatuiConsoleConfig cfg)
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
            // Push the chrome font from config to the base renderer before base.Awake.
            // The renderer is created via AddComponent at runtime, so the base's Reset /
            // OnValidate auto-populate path is skipped — without this hop, the chrome
            // glyphs (↗, −) render blank on WebGL.
            WindowChromeFont = cfg.windowChromeFont;
            FitColsAndRows = true;
            EnableInput = true;
            EnableMouseInput = true;
            EnableKeyboardInput = true;
        }
    }
}
