using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// MonoBehaviour that owns the console UI. Drives layout, log filtering,
    /// command prompt, autocomplete popup, and the per-log detail panel.
    /// Created automatically by <see cref="RatatuiTerminalApps"/> — do not
    /// add to scenes manually.
    /// </summary>
    [RatatuiTerminalApp("console", DisplayName = "Developer Console", Order = 0)]
    [DefaultExecutionOrder(-100)]
    public sealed class RatatuiConsoleRenderer : RatatuiTerminalApp
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterApp()
        {
            RatatuiTerminalApps.Register<RatatuiConsoleRenderer>("console", "Developer Console", 0);
        }

        public enum Filter : byte { All = 0, Logs = 1, Warnings = 2, Errors = 3, Exceptions = 4 }
        private enum InputFocus : byte { Prompt = 0, Search = 1 }

        // ── Persistent UI state ──────────────────────────────────────────────

        private RatatuiConsoleConfig _config;
        private InputFocus _focus = InputFocus.Prompt;

        // Editable buffers built on TerminalInput — give us selection, clipboard,
        // undo/redo, Cmd/Ctrl shortcuts, horizontal scroll, and mobile IME for free.
        // Console-specific keys (Enter/Tab/Esc/↑↓) are published as events that we
        // wire to history navigation, completion, and submit in InitInputWiring().
        private readonly TerminalCommandInput _prompt = new TerminalCommandInput();
        private readonly TerminalCommandInput _search = new TerminalCommandInput();

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
        private int _messageScroll;

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
        private uint _detailMessageArea;
        private uint _detailStackArea;
        private uint _detailCopyArea;
        private uint _detailEmailArea;
        private uint _detailCloseArea;
        private uint _autocompleteArea;
        private uint _promptArea;

        protected override KeyCode ToggleKey => _config != null ? _config.toggleKey : KeyCode.BackQuote;

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
            RatatuiConsole.EnsureServicesBooted();
            _config = RatatuiConsole.Config;
            if (_config == null) _config = RatatuiConsoleConfig.CreateDefault();
            ApplyConfigToBase(_config);
            base.Awake();
            InitInputWiring();
            OnCloseClicked += () => SetOpen(false);
        }

        private void InitInputWiring()
        {
            _prompt.Prefix      = "> ";
            // Console behavior: focusing the field (open, F7 swap, mouse click) must
            // preserve the cursor, not nuke the half-typed command. TerminalInput's
            // default (select-all on focus) is right for one-shot form fields, wrong
            // for a REPL prompt.
            _prompt.Input.SelectAllOnFocus = false;
            _search.Input.SelectAllOnFocus = false;

            _prompt.OnSubmit       += SubmitPromptOrToggleDetail;
            _prompt.OnEscape       += OnPromptEscape;
            _prompt.OnTab          += ApplyTopSuggestion;
            _prompt.OnHistoryStep  += OnPromptHistoryStep;
            _prompt.OnEdit         += () => RatatuiConsole.History.Reset();

            _search.Placeholder = "SEARCH LOGS...";
            _search.OnSubmit += () => SetFocus(InputFocus.Prompt);
            _search.OnEscape += () => SetFocus(InputFocus.Prompt);
        }

        private void OnPromptEscape()
        {
            if (_detailOpen) { _detailOpen = false; return; }
            SetOpen(false); // RatatuiTerminalApp
        }

        private void OnPromptHistoryStep(int dir)
        {
            if (dir < 0) NavigateUp(); else NavigateDown();
        }

        private void SetFocus(InputFocus next)
        {
            if (_focus == next) return;
            // Drive the underlying input's focus lifecycle so the mobile IME
            // opens/closes alongside the renderer's logical focus state.
            if (_focus == InputFocus.Prompt) _prompt.OnBlur(); else _search.OnBlur();
            _focus = next;
            if (next == InputFocus.Prompt) _prompt.OnFocus(); else _search.OnFocus();
        }

        protected override void Update()
        {
            // Always drain pending logs so the queue stays bounded even when the
            // console is closed for hours. The ring buffer applies its cap here.
            // Budget bounds the worst-case per-frame work so a log burst cannot
            // stall the renderer; the remainder drains over subsequent frames.
            RatatuiConsole.Logs?.DrainPending(ConsoleLogCapture.DefaultDrainBudget);
            base.Update();
        }

        protected override void OnOpened()
        {
            _focus = InputFocus.Prompt;
            _prompt.OnFocus();
            _followLogTail = true;
            _forceLogScrollToBottom = true;
        }

        protected override void OnClosed()
        {
            _prompt.OnBlur();
            _search.OnBlur();
            ClearSuggestions();
        }

        // ── Frame Building ───────────────────────────────────────────────────

        protected override void BuildFrame(RatatuiTerminal term)
        {
            if (!IsOpen) return;

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

        private int FallbackDetailMessageHeight() => 3;

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
            Color hintColor = hover ? ColorButtonHi : new Color(0.6f, 0.85f, 1.0f);
            Color bg = hover ? ColorHoverBg : Color.clear;

            _search.Prefix = " (F7) ";
            _search.PrefixFg = hintColor;
            _search.PrefixBg = bg;
            _search.Render(term, area,
                fg: ColorPromptText, bg: bg,
                cursorFg: Color.black, cursorBg: ColorPromptText,
                selectionFg: Color.white, selectionBg: ColorSelectionBg,
                placeholderFg: focused ? ColorPromptText : ColorPromptDim,
                focused: focused);
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

            BuildMessage(term, rows[0], entry);

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

        private void BuildMessage(RatatuiTerminal term, uint area, ConsoleLogEntry entry)
        {
            // Reserve right column for scrollbar.
            uint[] cols = term.Split(area, Direction.Horizontal,
                Constraint.Fill(1),
                Constraint.Length(1));
            uint content = cols[0];
            uint scroll = cols[1];
            _detailMessageArea = content;

            int areaHeight = FallbackDetailMessageHeight();
            int areaWidth = 40;
            if (term.TryGetAreaRect(content, out _, out _, out int w, out int h))
            {
                if (h > 0) areaHeight = h;
                if (w > 0) areaWidth = w;
            }

            string tag = KindTag(entry.Kind) + " ";
            var lines = WrapMessage(entry.Message ?? string.Empty, areaWidth, tag.Length);
            ClampMessageScroll(lines.Count, areaHeight);

            var sp = term.BeginStyledParagraph(content, Alignment.Left, wrap: false);
            int start = _messageScroll;
            int end = Mathf.Min(lines.Count, start + areaHeight);
            for (int i = start; i < end; i++)
            {
                if (i == 0)
                    sp.Span(tag, fg: KindColor(entry.Kind), modifiers: Modifier.Bold);
                sp.Span(lines[i], fg: KindForeground(entry.Kind), modifiers: Modifier.None);
                if (i < end - 1) sp.Line();
            }
            sp.Render();

            term.Scrollbar(scroll, lines.Count, _messageScroll, areaHeight,
                ScrollbarOrientation.VerticalRight, autoHide: true);
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
            Color bg = hover ? ColorHoverBg : Color.clear;
            Color prefixColor = focused || hover ? ColorButtonHi : ColorPromptDim;

            _prompt.PrefixFg = prefixColor;
            _prompt.PrefixBg = bg;
            _prompt.Render(term, inner,
                fg: ColorPromptText, bg: bg,
                cursorFg: Color.black, cursorBg: ColorPromptText,
                selectionFg: Color.white, selectionBg: ColorSelectionBg,
                placeholderFg: ColorPromptDim,
                focused: focused);
        }

        // ── Filter / Search Cache ────────────────────────────────────────────

        private void RebuildFilterCacheIfNeeded()
        {
            var capture = RatatuiConsole.Logs;
            int gen = capture?.Generation ?? 0;
            string search = _search.Text ?? string.Empty;

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
                _messageScroll = 0;
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
                    _messageScroll = 0;
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
            string text = _prompt.Text ?? string.Empty;
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
            if (!IsOpen) return;
            if (ShouldSuppressToggleKeyEvent(e)) return;

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
                    SetFocus(_focus == InputFocus.Search ? InputFocus.Prompt : InputFocus.Search);
                    return;
            }

            if (e.Key == KeyCode.PageUp) { MoveLogSelection(-1); return; }
            if (e.Key == KeyCode.PageDown) { MoveLogSelection(1); return; }

            if (_focus == InputFocus.Search) _search.HandleKeyEvent(e);
            else                              _prompt.HandleKeyEvent(e);
        }

        protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
        {
            if (!IsOpen) return;
            if (ShouldIgnoreMouseThisFrame()) return;

            if (e.Type == MouseEventType.Scroll)
            {
                HandleScroll(e);
                return;
            }

            // Route Down/Move/Up/Click on the input areas to the widgets so they
            // get click-to-position-cursor and drag-to-select. A Down also pulls
            // logical focus so the next keystroke goes to the same field.
            if (e.AreaId == _promptArea && e.Button == MouseButton.Left)
            {
                if (e.Type == MouseEventType.Down) SetFocus(InputFocus.Prompt);
                _prompt.HandleMouseEvent(e);
                return;
            }
            if (e.AreaId == _searchArea && e.Button == MouseButton.Left)
            {
                if (e.Type == MouseEventType.Down) SetFocus(InputFocus.Search);
                _search.HandleMouseEvent(e);
                return;
            }
            // Drag-selection continues after the cursor leaves the input area, so
            // forward Move/Up to the focused widget regardless of AreaId.
            if ((e.Type == MouseEventType.Move || e.Type == MouseEventType.Up)
                && e.Button == MouseButton.Left)
            {
                if (_focus == InputFocus.Prompt && _prompt.HandleMouseEvent(e)) return;
                if (_focus == InputFocus.Search && _search.HandleMouseEvent(e)) return;
            }

            if (e.Type != MouseEventType.Click || e.Button != MouseButton.Left) return;

            uint a = e.AreaId;
            if (a == 0) return;

            if (a == _tabAllArea) { _filter = Filter.All; _logScroll = 0; return; }
            if (a == _tabLogsArea) { _filter = Filter.Logs; _logScroll = 0; return; }
            if (a == _tabWarnsArea) { _filter = Filter.Warnings; _logScroll = 0; return; }
            if (a == _tabErrsArea) { _filter = Filter.Errors; _logScroll = 0; return; }
            if (a == _tabExcArea) { _filter = Filter.Exceptions; _logScroll = 0; return; }

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
        }

        private void HandleScroll(TerminalMouseEvent e)
        {
            int dir = e.ScrollDelta > 0 ? -1 : 1;
            if (_detailOpen && e.AreaId == _detailMessageArea)
            {
                _messageScroll = Mathf.Max(0, _messageScroll + dir);
            }
            else if (_detailOpen && (e.AreaId == _detailPanelArea || e.AreaId == _detailStackArea))
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
                    _messageScroll = 0;
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
            string raw = _prompt.Text ?? string.Empty;
            if (raw.Length == 0)
            {
                if (_selectedEntryIndex >= 0) _detailOpen = !_detailOpen;
                return;
            }
            SetPromptText(string.Empty);
            RatatuiConsole.History.Push(raw);
            RatatuiConsole.ExecuteCommand(raw);
            ClearSuggestions();
        }

        private void ApplyTopSuggestion()
        {
            if (_suggestions.Count == 0) return;
            int idx = Mathf.Clamp(_suggestionIndex, 0, _suggestions.Count - 1);
            var s = _suggestions[idx];

            string cur = _prompt.Text ?? string.Empty;
            string head = s.ReplaceFromIndex < 0
                ? string.Empty
                : cur.Substring(0, Mathf.Clamp(s.ReplaceFromIndex, 0, cur.Length));
            string next = head + s.Insert + (s.TrailingSpace ? " " : string.Empty);
            SetPromptText(next);
            // Direct Value writes bypass HandleKeyEvent's diff, so OnEdit doesn't
            // fire — invoke History.Reset() explicitly to match the "user just
            // edited the buffer" semantics.
            RatatuiConsole.History.Reset();
            ClearSuggestions();
        }

        private void NavigateUp()
        {
            if (_suggestions.Count > 0)
            {
                _suggestionIndex = (_suggestionIndex - 1 + _suggestions.Count) % _suggestions.Count;
                return;
            }
            if ((_prompt.Text ?? string.Empty).Length == 0)
            {
                MoveLogSelection(-1);
                return;
            }
            string hist = RatatuiConsole.History.MovePrev();
            if (hist == null) return;
            SetPromptText(hist);
        }

        private void NavigateDown()
        {
            if (_suggestions.Count > 0)
            {
                _suggestionIndex = (_suggestionIndex + 1) % _suggestions.Count;
                return;
            }
            if ((_prompt.Text ?? string.Empty).Length == 0)
            {
                MoveLogSelection(1);
                return;
            }
            string hist = RatatuiConsole.History.MoveNext();
            if (hist == null) return;
            SetPromptText(hist);
        }

        // Replaces the prompt buffer and parks the cursor at the end. Value setter
        // clears selection and resets undo history, which is what we want when an
        // external action (submit/history/completion) rewrites the input.
        private void SetPromptText(string text)
        {
            _prompt.Text = text ?? string.Empty;
            _prompt.Cursor = _prompt.Text.Length;
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
            _detailScroll = 0;
            _messageScroll = 0;
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

        private void ClampMessageScroll(int total, int viewportHeight)
        {
            int maxScroll = Mathf.Max(0, total - viewportHeight);
            _messageScroll = Mathf.Clamp(_messageScroll, 0, maxScroll);
        }

        // Word-wraps text to `width`. First visual line gets `firstOffset` reserved
        // (e.g. for an inline tag). Breaks on spaces, hard-splits long runs.
        private static List<string> WrapMessage(string text, int width, int firstOffset)
        {
            var lines = new List<string>(4);
            if (width <= 0) width = 1;
            string norm = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            int avail = Mathf.Max(1, width - firstOffset);
            foreach (string para in norm.Split('\n'))
            {
                int pos = 0;
                do
                {
                    int take = Mathf.Min(avail, para.Length - pos);
                    if (pos + take < para.Length && take > 0)
                    {
                        int br = para.LastIndexOf(' ', pos + take - 1, take);
                        if (br >= pos) take = br - pos;
                    }
                    lines.Add(para.Substring(pos, take).TrimEnd(' '));
                    pos += Mathf.Max(1, take);
                    while (pos < para.Length && para[pos] == ' ') pos++;
                    avail = width;
                } while (pos < para.Length);
            }
            return lines;
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
