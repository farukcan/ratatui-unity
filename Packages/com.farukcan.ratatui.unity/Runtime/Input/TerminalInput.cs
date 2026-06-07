using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Single-line text input with selection, clipboard, undo/redo, unicode +
    /// CJK width-aware cursor, placeholder, password mask, max length, character
    /// filter, cursor blink, click-and-drag selection, double/triple-click, and
    /// optional select-all on focus. Pure C# state; renders via StyledParagraph.
    /// </summary>
    public class TerminalInput
    {
        // ── State ────────────────────────────────────────────────────────
        private string _value;
        private int _cursor;
        private int _anchor = -1;          // -1 = no selection; else != cursor
        private int _scrollIdx;            // first visible char index

        // Last-render geometry (for hit-testing)
        private uint _lastAreaId;
        private int _lastAreaX;

        // Blink
        private float _blinkAnchor;

        // Mouse drag + multi-click
        private bool _isDragging;
        private bool _downConsumed;     // Down was handled — suppress the trailing Click
        private float _lastClickTime = -10f;
        private int _lastClickCol = -1000;
        private int _clickStreak;

        // Undo
        private struct Snap { public string V; public int C; public int A; }
        private enum EditKind { Other, Insert, DeleteBack, DeleteForward }
        private readonly List<Snap> _history = new List<Snap>();
        private int _histIdx;
        private EditKind _lastEditKind = EditKind.Other;
        private float _lastEditTime = -10f;
        private const float CoalesceWindow = 0.6f;
        private const float MultiClickWindow = 0.4f;

        // ── Configuration ───────────────────────────────────────────────
        /// <summary>Text shown (dim) when Value is empty.</summary>
        public string Placeholder { get; set; } = "";
        /// <summary>If set, displays this character for every input codepoint (password mode).</summary>
        public char? MaskChar { get; set; }
        /// <summary>Maximum UTF-16 length. Excess pastes are truncated.</summary>
        public int MaxLength { get; set; } = int.MaxValue;
        /// <summary>Returns true if a character is allowed. Null = accept all.</summary>
        public Func<char, bool> CharFilter { get; set; }
        /// <summary>If true, editing keys and paste/cut are ignored. Selection + copy still work.</summary>
        public bool ReadOnly { get; set; }
        /// <summary>If true, OnFocus() will select all text.</summary>
        public bool SelectAllOnFocus { get; set; } = true;
        /// <summary>Cursor blink half-period in seconds. ≤ 0 disables blink.</summary>
        public float BlinkPeriod { get; set; } = 0.5f;
        /// <summary>Virtual keyboard layout requested on iOS / Android / mobile WebGL.</summary>
        public TouchScreenKeyboardType KeyboardType { get; set; } = TouchScreenKeyboardType.Default;
        /// <summary>Enable native autocorrection on the virtual keyboard.</summary>
        public bool AutoCorrection { get; set; } = false;

        // Mobile virtual keyboard bridge — no-op on platforms without TouchScreenKeyboard.
        private readonly MobileKeyboardBridge _mobileKb = new MobileKeyboardBridge();

        // ── Public read-only state ───────────────────────────────────────
        public string Value
        {
            get => _value;
            set
            {
                _value = value ?? string.Empty;
                if (_cursor > _value.Length) _cursor = _value.Length;
                if (_anchor > _value.Length) _anchor = -1;
                ResetHistory();
                ResetBlink();
                _mobileKb.PushText(_value);
            }
        }

        public int Cursor
        {
            get => _cursor;
            set => SetCursor(Mathf.Clamp(value, 0, _value.Length), extend: false);
        }

        public int ScrollOffset => _scrollIdx;

        public bool HasSelection => _anchor >= 0 && _anchor != _cursor;
        public int SelectionStart => HasSelection ? Math.Min(_anchor, _cursor) : _cursor;
        public int SelectionEnd => HasSelection ? Math.Max(_anchor, _cursor) : _cursor;
        public string SelectedText => HasSelection
            ? _value.Substring(SelectionStart, SelectionEnd - SelectionStart)
            : string.Empty;

        // ── Construction ─────────────────────────────────────────────────
        public TerminalInput(string initialValue = "")
        {
            _value = initialValue ?? string.Empty;
            _cursor = _value.Length;
            ResetHistory();
            // Do NOT call NowTime() here — Unity forbids Application.isPlaying access
            // from MonoBehaviour field initializers. Leave _blinkAnchor at 0; the
            // first frame computes a valid phase against Time.unscaledTime regardless.
        }

        // ── Focus hooks ──────────────────────────────────────────────────
        public void OnFocus()
        {
            if (SelectAllOnFocus) SelectAll();
            ResetBlink();
            OpenMobileKeyboard();
        }

        public void OnBlur()
        {
            ClearSelection();
            _mobileKb.Close();
        }

        private void OpenMobileKeyboard()
        {
            if (ReadOnly) return;
            _mobileKb.Open(
                initialText: _value,
                multiline: false,
                secure: MaskChar.HasValue,
                placeholder: Placeholder,
                characterLimit: MaxLength,
                type: KeyboardType,
                autocorrection: AutoCorrection);
        }

        // Pulls text from the native virtual keyboard into the widget. Called
        // automatically from Render() when focused; safe to call elsewhere.
        public void SyncMobileKeyboard()
        {
            if (!_mobileKb.Poll(out string text, out int caret, out bool closed)) return;
            if (text != null) ApplyMobileKeyboardText(text, caret);
            if (closed) ClearSelection();
        }

        private void ApplyMobileKeyboardText(string text, int caret)
        {
            if (ReadOnly) return;
            // Single-line: collapse any newline that snuck through the IME.
            text = text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

            if (CharFilter != null)
            {
                var sb = new System.Text.StringBuilder(text.Length);
                foreach (char c in text) if (CharFilter(c)) sb.Append(c);
                // Filtering can shorten the buffer — clamp the IME caret too.
                if (sb.Length != text.Length && caret > sb.Length) caret = sb.Length;
                text = sb.ToString();
            }
            if (text.Length > MaxLength)
            {
                text = text.Substring(0, MaxLength);
                if (caret > text.Length) caret = text.Length;
            }

            if (text == _value && caret == _cursor) return;
            // Coalesce contiguous IME edits into a single undo step.
            var kind = text.Length >= _value.Length ? EditKind.Insert : EditKind.DeleteBack;
            _value = text;
            _cursor = Mathf.Clamp(caret, 0, _value.Length);
            _anchor = -1;
            ResetBlink();
            PushHistory(kind);
            // If filtering/clamping rewrote the buffer, push the canonical
            // version back so the IME stays in sync.
            _mobileKb.PushText(text);
        }

        // ── Selection ────────────────────────────────────────────────────
        public void SelectAll()
        {
            if (_value.Length == 0) { ClearSelection(); return; }
            _anchor = 0;
            _cursor = _value.Length;
            ResetBlink();
        }

        public void ClearSelection() => _anchor = -1;

        public void Select(int start, int end)
        {
            start = Mathf.Clamp(start, 0, _value.Length);
            end = Mathf.Clamp(end, 0, _value.Length);
            _anchor = start;
            _cursor = end;
            ResetBlink();
        }

        // ── Movement ─────────────────────────────────────────────────────
        public void MoveLeft(bool extend)
        {
            int p = (HasSelection && !extend)
                ? SelectionStart
                : TextUtils.PrevBoundary(_value, _cursor);
            SetCursor(p, extend);
        }

        public void MoveRight(bool extend)
        {
            int p = (HasSelection && !extend)
                ? SelectionEnd
                : TextUtils.NextBoundary(_value, _cursor);
            SetCursor(p, extend);
        }

        public void MoveToStart(bool extend) => SetCursor(0, extend);
        public void MoveToEnd(bool extend) => SetCursor(_value.Length, extend);

        public void MoveWordLeft(bool extend) => SetCursor(ComputeWordLeftTarget(), extend);
        public void MoveWordRight(bool extend) => SetCursor(ComputeWordRightTarget(), extend);

        private void SetCursor(int pos, bool extend)
        {
            pos = Mathf.Clamp(pos, 0, _value.Length);
            if (extend)
            {
                if (_anchor < 0) _anchor = _cursor;
            }
            else
            {
                _anchor = -1;
            }
            _cursor = pos;
            ResetBlink();
        }

        private int ComputeWordLeftTarget()
        {
            if (_cursor == 0) return 0;
            int i = TextUtils.PrevBoundary(_value, _cursor);
            while (i > 0 && !TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, i)))
                i = TextUtils.PrevBoundary(_value, i);
            while (i > 0)
            {
                int prev = TextUtils.PrevBoundary(_value, i);
                if (!TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, prev))) break;
                i = prev;
            }
            return i;
        }

        private int ComputeWordRightTarget()
        {
            int i = _cursor;
            while (i < _value.Length && TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, i)))
                i = TextUtils.NextBoundary(_value, i);
            while (i < _value.Length && !TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, i)))
                i = TextUtils.NextBoundary(_value, i);
            return i;
        }

        // ── Editing ──────────────────────────────────────────────────────
        public void InsertChar(char c)
        {
            if (ReadOnly) return;
            if (CharFilter != null && !CharFilter(c)) return;
            if (HasSelection) DeleteSelectionInternal();
            if (_value.Length >= MaxLength) return;
            _value = _value.Insert(_cursor, c.ToString());
            _cursor++;
            ResetBlink();
            PushHistory(EditKind.Insert);
        }

        public void InsertString(string s)
        {
            if (ReadOnly || string.IsNullOrEmpty(s)) return;
            if (HasSelection) DeleteSelectionInternal();

            if (CharFilter != null)
            {
                var sb = new System.Text.StringBuilder(s.Length);
                foreach (char c in s) if (CharFilter(c)) sb.Append(c);
                s = sb.ToString();
                if (s.Length == 0) return;
            }
            int available = MaxLength - _value.Length;
            if (available <= 0) return;
            if (s.Length > available) s = s.Substring(0, available);

            _value = _value.Insert(_cursor, s);
            _cursor += s.Length;
            ResetBlink();
            PushHistory(EditKind.Other);
        }

        public void DeleteBack()
        {
            if (ReadOnly) return;
            if (HasSelection) { DeleteSelectionInternal(); ResetBlink(); PushHistory(EditKind.Other); return; }
            if (_cursor == 0) return;
            int prev = TextUtils.PrevBoundary(_value, _cursor);
            _value = _value.Remove(prev, _cursor - prev);
            _cursor = prev;
            ResetBlink();
            PushHistory(EditKind.DeleteBack);
        }

        public void DeleteForward()
        {
            if (ReadOnly) return;
            if (HasSelection) { DeleteSelectionInternal(); ResetBlink(); PushHistory(EditKind.Other); return; }
            if (_cursor >= _value.Length) return;
            int next = TextUtils.NextBoundary(_value, _cursor);
            _value = _value.Remove(_cursor, next - _cursor);
            ResetBlink();
            PushHistory(EditKind.DeleteForward);
        }

        public void DeleteWordBack()
        {
            if (ReadOnly) return;
            if (HasSelection) { DeleteSelectionInternal(); ResetBlink(); PushHistory(EditKind.Other); return; }
            if (_cursor == 0) return;
            int target = ComputeWordLeftTarget();
            _value = _value.Remove(target, _cursor - target);
            _cursor = target;
            ResetBlink();
            PushHistory(EditKind.Other);
        }

        public void DeleteWordForward()
        {
            if (ReadOnly) return;
            if (HasSelection) { DeleteSelectionInternal(); ResetBlink(); PushHistory(EditKind.Other); return; }
            if (_cursor >= _value.Length) return;
            int target = ComputeWordRightTarget();
            _value = _value.Remove(_cursor, target - _cursor);
            ResetBlink();
            PushHistory(EditKind.Other);
        }

        private void DeleteSelectionInternal()
        {
            int s = SelectionStart, e = SelectionEnd;
            _value = _value.Remove(s, e - s);
            _cursor = s;
            _anchor = -1;
        }

        // ── Clipboard ────────────────────────────────────────────────────
        public bool Copy()
        {
            if (!HasSelection) return false;
            // Never copy masked content.
            if (MaskChar.HasValue) return false;
            GUIUtility.systemCopyBuffer = SelectedText;
            return true;
        }

        public bool Cut()
        {
            if (ReadOnly) return Copy();
            if (!HasSelection) return false;
            if (MaskChar.HasValue) return false;
            GUIUtility.systemCopyBuffer = SelectedText;
            DeleteSelectionInternal();
            ResetBlink();
            PushHistory(EditKind.Other);
            return true;
        }

        public bool Paste()
        {
            if (ReadOnly) return false;
            string txt = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(txt)) return false;
            // Single-line: collapse newlines into spaces.
            txt = txt.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            InsertString(txt);
            return true;
        }

        // ── Undo/Redo ────────────────────────────────────────────────────
        public bool Undo()
        {
            if (_histIdx <= 0) return false;
            _histIdx--;
            ApplySnap(_history[_histIdx]);
            _lastEditKind = EditKind.Other;
            return true;
        }

        public bool Redo()
        {
            if (_histIdx + 1 >= _history.Count) return false;
            _histIdx++;
            ApplySnap(_history[_histIdx]);
            _lastEditKind = EditKind.Other;
            return true;
        }

        private void ResetHistory()
        {
            _history.Clear();
            _history.Add(new Snap { V = _value, C = _cursor, A = _anchor });
            _histIdx = 0;
            _lastEditKind = EditKind.Other;
        }

        private void PushHistory(EditKind kind)
        {
            float now = NowTime();
            bool coalesce = _histIdx > 0
                         && kind != EditKind.Other
                         && kind == _lastEditKind
                         && (now - _lastEditTime) < CoalesceWindow;
            var snap = new Snap { V = _value, C = _cursor, A = _anchor };
            if (coalesce)
            {
                _history[_histIdx] = snap;
            }
            else
            {
                if (_history.Count > _histIdx + 1)
                    _history.RemoveRange(_histIdx + 1, _history.Count - _histIdx - 1);
                _history.Add(snap);
                _histIdx = _history.Count - 1;
            }
            _lastEditTime = now;
            _lastEditKind = kind;
        }

        private void ApplySnap(Snap s)
        {
            _value = s.V;
            _cursor = s.C;
            _anchor = s.A;
            ResetBlink();
        }

        // ── Blink ────────────────────────────────────────────────────────
        private void ResetBlink() => _blinkAnchor = NowTime();

        private bool CursorVisible()
        {
            if (BlinkPeriod <= 0f) return true;
            float t = NowTime() - _blinkAnchor;
            int phase = (int)(t / BlinkPeriod);
            return (phase & 1) == 0;
        }

        private static float NowTime() => Application.isPlaying ? Time.unscaledTime : 0f;

        // ── Key handling ─────────────────────────────────────────────────
        public bool HandleKeyEvent(TerminalKeyEvent e)
        {
            // Cmd/Ctrl-shortcuts first (so Cmd+Z / Ctrl+Z doesn't reach the
            // printable-char path). Cmd is the native modifier on macOS; Ctrl on
            // Windows/Linux. We accept either so shortcuts feel right everywhere.
            // Always consume the key — success of Copy/Paste/Undo is independent
            // of whether the keystroke belongs to this widget.
            if (e.HasCmdOrCtrl && !e.HasAlt)
            {
                switch (e.Key)
                {
                    case KeyCode.A: SelectAll(); return true;
                    case KeyCode.C: Copy(); return true;
                    case KeyCode.X: Cut();  return true;
                    case KeyCode.V: Paste(); return true;
                    case KeyCode.Z: if (e.HasShift) Redo(); else Undo(); return true;
                    case KeyCode.Y: Redo(); return true;
                }
            }

            // Printable character. Suppress when Cmd or Ctrl is held so macOS
            // Cmd+C does not also insert a literal 'c'.
            if (e.Character != '\0' && !char.IsControl(e.Character) && !e.HasCtrl && !e.HasCmd)
            {
                InsertChar(e.Character);
                return true;
            }

            bool shift = e.HasShift;
            switch (e.Key)
            {
                case KeyCode.Backspace:
                    if (e.HasCtrl) DeleteWordBack(); else DeleteBack();
                    return true;
                case KeyCode.Delete:
                    if (e.HasCtrl) DeleteWordForward(); else DeleteForward();
                    return true;
                case KeyCode.LeftArrow:
                    if (e.HasCtrl) MoveWordLeft(shift); else MoveLeft(shift);
                    return true;
                case KeyCode.RightArrow:
                    if (e.HasCtrl) MoveWordRight(shift); else MoveRight(shift);
                    return true;
                case KeyCode.Home: MoveToStart(shift); return true;
                case KeyCode.End:  MoveToEnd(shift);   return true;
                default: return false;
            }
        }

        // ── Mouse handling ───────────────────────────────────────────────
        public bool HandleMouseEvent(TerminalMouseEvent e)
        {
            if (e.Button != MouseButton.Left) return false;
            switch (e.Type)
            {
                case MouseEventType.Down:
                    _downConsumed = true;
                    return OnMouseDown(e);
                case MouseEventType.Move:  return OnMouseMove(e);
                case MouseEventType.Up:    return OnMouseUp(e);
                case MouseEventType.Click:
                    // Down/Up already processed in this widget — Click is redundant.
                    if (_downConsumed) { _downConsumed = false; return true; }
                    // Click forwarded directly (no preceding Down) — position cursor.
                    return OnMouseClick(e);
                default: return false;
            }
        }

        private bool OnMouseClick(TerminalMouseEvent e)
        {
            _cursor = ColToIndex(e.Col);
            _anchor = -1;
            ResetBlink();
            return true;
        }

        private bool OnMouseDown(TerminalMouseEvent e)
        {
            int idx = ColToIndex(e.Col);
            float now = NowTime();
            bool sameSpot = Mathf.Abs(e.Col - _lastClickCol) <= 1;
            bool inTime = (now - _lastClickTime) < MultiClickWindow;
            _clickStreak = (sameSpot && inTime) ? _clickStreak + 1 : 1;
            _lastClickTime = now;
            _lastClickCol = e.Col;

            if (_clickStreak >= 3)
            {
                SelectAll();
                _isDragging = false;
                _clickStreak = 0;
                return true;
            }
            if (_clickStreak == 2)
            {
                SelectWordAt(idx);
                _isDragging = false;
                return true;
            }
            _cursor = idx;
            _anchor = idx;
            _isDragging = true;
            ResetBlink();
            return true;
        }

        private bool OnMouseMove(TerminalMouseEvent e)
        {
            if (!_isDragging) return false;
            int idx = ColToIndex(e.Col);
            if (idx == _cursor) return false;
            _cursor = idx;
            ResetBlink();
            return true;
        }

        private bool OnMouseUp(TerminalMouseEvent e)
        {
            if (!_isDragging) return false;
            _isDragging = false;
            if (_anchor == _cursor) _anchor = -1;
            return true;
        }

        private int ColToIndex(int absCol)
        {
            int localCol = absCol - _lastAreaX;
            if (localCol < 0) localCol = 0;
            int i = _scrollIdx;
            int col = 0;
            while (i < _value.Length)
            {
                int w = WidthAt(i);
                if (col + w > localCol)
                {
                    // Snap to nearer boundary of this codepoint.
                    return (localCol - col) * 2 < w ? i : TextUtils.NextBoundary(_value, i);
                }
                col += w;
                i = TextUtils.NextBoundary(_value, i);
            }
            return _value.Length;
        }

        private void SelectWordAt(int idx)
        {
            int s = idx, e = idx;
            while (s > 0)
            {
                int prev = TextUtils.PrevBoundary(_value, s);
                if (!TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, prev))) break;
                s = prev;
            }
            while (e < _value.Length && TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, e)))
                e = TextUtils.NextBoundary(_value, e);
            if (s == e) { _cursor = idx; _anchor = -1; }
            else        { _anchor = s; _cursor = e; }
            ResetBlink();
        }

        // ── Rendering ────────────────────────────────────────────────────
        /// <summary>
        /// Render the input field. Caller controls focus styling externally.
        /// </summary>
        public void Render(
            RatatuiTerminal term, uint areaId,
            Color fg = default, Color bg = default,
            Color cursorFg = default, Color cursorBg = default,
            Color selectionFg = default, Color selectionBg = default,
            Color placeholderFg = default,
            bool focused = true)
        {
            if (focused) SyncMobileKeyboard();
            _lastAreaId = areaId;
            if (!term.TryGetAreaRect(areaId, out int ax, out int ay, out int aw, out int ah) || aw <= 0)
            {
                _lastAreaX = 0;
                return;
            }
            _lastAreaX = ax;
            int width = aw;

            if (fg.a < 0.01f)            fg            = Color.white;
            if (cursorFg.a < 0.01f)      cursorFg      = Color.black;
            if (cursorBg.a < 0.01f)      cursorBg      = Color.white;
            if (selectionFg.a < 0.01f)   selectionFg   = Color.white;
            if (selectionBg.a < 0.01f)   selectionBg   = new Color(0.2f, 0.4f, 0.8f);
            if (placeholderFg.a < 0.01f) placeholderFg = new Color(0.45f, 0.45f, 0.45f);

            // Placeholder branch: empty value
            if (_value.Length == 0)
            {
                RenderPlaceholder(term, areaId, width, fg, bg, cursorFg, cursorBg, placeholderFg, focused);
                return;
            }

            EnsureCursorVisible(width);
            int visEnd = ComputeVisibleEnd(width);

            int selS = Mathf.Clamp(SelectionStart, _scrollIdx, visEnd);
            int selE = Mathf.Clamp(SelectionEnd,   _scrollIdx, visEnd);
            bool hasSelInView = HasSelection && selE > selS;

            var b = term.BeginStyledParagraph(areaId, Alignment.Left, wrap: false);

            if (hasSelInView)
            {
                // Selection takes precedence over cursor block.
                AppendRange(b, _scrollIdx, selS, fg, bg);
                AppendRange(b, selS, selE, selectionFg, selectionBg);
                AppendRange(b, selE, visEnd, fg, bg);
            }
            else if (focused && CursorVisible())
            {
                int curIdx = Mathf.Clamp(_cursor, _scrollIdx, visEnd);
                AppendRange(b, _scrollIdx, curIdx, fg, bg);
                if (curIdx < visEnd)
                {
                    int cursorEnd = TextUtils.NextBoundary(_value, curIdx);
                    string cursorChar = MaskChar.HasValue
                        ? MaskChar.Value.ToString()
                        : _value.Substring(curIdx, cursorEnd - curIdx);
                    b.Span(cursorChar, cursorFg, cursorBg);
                    AppendRange(b, cursorEnd, visEnd, fg, bg);
                }
                else
                {
                    b.Span(" ", cursorFg, cursorBg);
                }
            }
            else
            {
                AppendRange(b, _scrollIdx, visEnd, fg, bg);
            }

            b.Render();
        }

        private void RenderPlaceholder(
            RatatuiTerminal term, uint areaId, int width,
            Color fg, Color bg, Color cursorFg, Color cursorBg, Color placeholderFg, bool focused)
        {
            var b = term.BeginStyledParagraph(areaId, Alignment.Left, wrap: false);
            string ph = Placeholder ?? string.Empty;
            if (ph.Length > width) ph = ph.Substring(0, width);

            bool drawCursor = focused && CursorVisible();
            if (drawCursor)
            {
                string head = ph.Length > 0 ? ph.Substring(0, 1) : " ";
                b.Span(head, cursorFg, cursorBg);
                if (ph.Length > 1) b.Span(ph.Substring(1), placeholderFg, bg);
            }
            else if (ph.Length > 0)
            {
                b.Span(ph, placeholderFg, bg);
            }
            else
            {
                // Ensure at least one span — native panics on empty styled paragraphs.
                b.Span(" ", fg, bg);
            }
            b.Render();
        }

        private void EnsureCursorVisible(int width)
        {
            if (width <= 0) return;
            if (_scrollIdx < 0) _scrollIdx = 0;
            if (_scrollIdx > _value.Length) _scrollIdx = _value.Length;
            if (_cursor < _scrollIdx) _scrollIdx = _cursor;

            int cursorW = (_cursor < _value.Length) ? WidthAt(_cursor) : 1;
            while (DisplayWidthRange(_scrollIdx, _cursor) + cursorW > width)
            {
                int next = TextUtils.NextBoundary(_value, _scrollIdx);
                if (next == _scrollIdx) break;
                _scrollIdx = next;
            }
        }

        private int ComputeVisibleEnd(int width)
        {
            int i = _scrollIdx;
            int col = 0;
            while (i < _value.Length)
            {
                int w = WidthAt(i);
                if (col + w > width) break;
                col += w;
                i = TextUtils.NextBoundary(_value, i);
            }
            return i;
        }

        private int WidthAt(int idx)
        {
            if (MaskChar.HasValue) return 1;
            return TextUtils.CodepointDisplayWidth(TextUtils.CodepointAt(_value, idx));
        }

        private int DisplayWidthRange(int start, int end)
        {
            if (MaskChar.HasValue)
            {
                int n = 0, i = start;
                while (i < end) { n++; i = TextUtils.NextBoundary(_value, i); }
                return n;
            }
            return TextUtils.DisplayWidth(_value, start, end);
        }

        private void AppendRange(StyledText b, int start, int end, Color fg, Color bg)
        {
            if (end <= start) return;
            string text;
            if (MaskChar.HasValue)
            {
                int n = 0, i = start;
                while (i < end) { n++; i = TextUtils.NextBoundary(_value, i); }
                text = new string(MaskChar.Value, n);
            }
            else
            {
                text = _value.Substring(start, end - start);
            }
            b.Span(text, fg, bg);
        }
    }
}
