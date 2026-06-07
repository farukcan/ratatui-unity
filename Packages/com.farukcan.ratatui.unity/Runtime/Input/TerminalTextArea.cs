using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Multi-line text editor: line-aware cursor, vertical scrolling, selection
    /// spanning lines, clipboard (preserves newlines), undo/redo, click-and-drag
    /// selection, double-click word + triple-click all, focus-time select-all,
    /// ReadOnly, optional MaxLength + CharFilter.
    /// </summary>
    public class TerminalTextArea
    {
        // ── State ────────────────────────────────────────────────────────
        private string _value;
        private int _cursor;
        private int _anchor = -1;
        private int _scrollLine;
        private int _preferredCol = -1;  // for Up/Down column preservation

        private uint _lastAreaId;
        private int _lastAreaX, _lastAreaY, _lastAreaW, _lastAreaH;

        // Per-line horizontal shift applied to the cursor line so a long line
        // keeps the cursor on-screen. -1 = no line is shifted.
        private int _hScrollLine = -1;
        private int _hScrollStartIdx;

        private float _blinkAnchor;

        private bool _isDragging;
        private bool _downConsumed;
        private float _lastClickTime = -10f;
        private int _lastClickCol = -1000, _lastClickRow = -1000;
        private int _clickStreak;

        private struct Snap { public string V; public int C; public int A; public int S; }
        private enum EditKind { Other, Insert, DeleteBack, DeleteForward }
        private readonly List<Snap> _history = new List<Snap>();
        private int _histIdx;
        private EditKind _lastEditKind = EditKind.Other;
        private float _lastEditTime = -10f;
        private const float CoalesceWindow = 0.6f;
        private const float MultiClickWindow = 0.4f;

        // ── Configuration ────────────────────────────────────────────────
        public string Placeholder { get; set; } = "";
        public int MaxLength { get; set; } = int.MaxValue;
        public Func<char, bool> CharFilter { get; set; }
        public bool ReadOnly { get; set; }
        public bool SelectAllOnFocus { get; set; } = true;
        public float BlinkPeriod { get; set; } = 0.5f;
        /// <summary>Virtual keyboard layout requested on iOS / Android / mobile WebGL.</summary>
        public TouchScreenKeyboardType KeyboardType { get; set; } = TouchScreenKeyboardType.Default;
        /// <summary>Enable native autocorrection on the virtual keyboard.</summary>
        public bool AutoCorrection { get; set; } = false;

        private readonly MobileKeyboardBridge _mobileKb = new MobileKeyboardBridge();

        // ── Public state ─────────────────────────────────────────────────
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

        public int ScrollLine => _scrollLine;

        public int LineCount
        {
            get
            {
                int n = 1;
                for (int i = 0; i < _value.Length; i++) if (_value[i] == '\n') n++;
                return n;
            }
        }

        public int CursorLine { get { var lc = IndexToLineCol(_cursor); return lc.line; } }
        public int CursorColumn { get { var lc = IndexToLineCol(_cursor); return lc.col; } }

        public bool HasSelection => _anchor >= 0 && _anchor != _cursor;
        public int SelectionStart => HasSelection ? Math.Min(_anchor, _cursor) : _cursor;
        public int SelectionEnd => HasSelection ? Math.Max(_anchor, _cursor) : _cursor;
        public string SelectedText => HasSelection
            ? _value.Substring(SelectionStart, SelectionEnd - SelectionStart)
            : string.Empty;

        // ── Construction ─────────────────────────────────────────────────
        public TerminalTextArea(string initialValue = "")
        {
            _value = initialValue ?? string.Empty;
            _cursor = _value.Length;
            ResetHistory();
            // See TerminalInput ctor — NowTime() forbidden in MonoBehaviour field init.
        }

        // ── Focus ────────────────────────────────────────────────────────
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
                multiline: true,
                secure: false,
                placeholder: Placeholder,
                characterLimit: MaxLength,
                type: KeyboardType,
                autocorrection: AutoCorrection);
        }

        public void SyncMobileKeyboard()
        {
            if (!_mobileKb.Poll(out string text, out int caret, out bool closed)) return;
            if (text != null) ApplyMobileKeyboardText(text, caret);
            if (closed) ClearSelection();
        }

        private void ApplyMobileKeyboardText(string text, int caret)
        {
            if (ReadOnly) return;
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            if (CharFilter != null)
            {
                var sb = new System.Text.StringBuilder(text.Length);
                foreach (char c in text) if (c == '\n' || CharFilter(c)) sb.Append(c);
                if (sb.Length != text.Length && caret > sb.Length) caret = sb.Length;
                text = sb.ToString();
            }
            if (text.Length > MaxLength)
            {
                text = text.Substring(0, MaxLength);
                if (caret > text.Length) caret = text.Length;
            }

            if (text == _value && caret == _cursor) return;
            var kind = text.Length >= _value.Length ? EditKind.Insert : EditKind.DeleteBack;
            _value = text;
            _cursor = Mathf.Clamp(caret, 0, _value.Length);
            _anchor = -1;
            _preferredCol = -1;
            ResetBlink();
            PushHistory(kind);
            _mobileKb.PushText(text);
        }

        // ── Selection ────────────────────────────────────────────────────
        public void SelectAll()
        {
            if (_value.Length == 0) { ClearSelection(); return; }
            _anchor = 0; _cursor = _value.Length;
            ResetBlink();
        }
        public void ClearSelection() => _anchor = -1;

        public void Select(int start, int end)
        {
            _anchor = Mathf.Clamp(start, 0, _value.Length);
            _cursor = Mathf.Clamp(end, 0, _value.Length);
            ResetBlink();
        }

        // ── Movement ─────────────────────────────────────────────────────
        public void MoveLeft(bool extend)
        {
            int p = (HasSelection && !extend) ? SelectionStart : TextUtils.PrevBoundary(_value, _cursor);
            SetCursor(p, extend);
        }
        public void MoveRight(bool extend)
        {
            int p = (HasSelection && !extend) ? SelectionEnd : TextUtils.NextBoundary(_value, _cursor);
            SetCursor(p, extend);
        }
        public void MoveUp(bool extend)
        {
            var (line, col) = IndexToLineCol(_cursor);
            if (_preferredCol < 0) _preferredCol = col;
            if (line == 0) { SetCursor(0, extend, preservePreferredCol: true); return; }
            int idx = LineColToIndex(line - 1, _preferredCol);
            SetCursor(idx, extend, preservePreferredCol: true);
        }
        public void MoveDown(bool extend)
        {
            var (line, col) = IndexToLineCol(_cursor);
            if (_preferredCol < 0) _preferredCol = col;
            int last = LineCount - 1;
            if (line >= last) { SetCursor(_value.Length, extend, preservePreferredCol: true); return; }
            int idx = LineColToIndex(line + 1, _preferredCol);
            SetCursor(idx, extend, preservePreferredCol: true);
        }
        public void MoveLineStart(bool extend) => SetCursor(LineStartIdx(IndexToLineCol(_cursor).line), extend);
        public void MoveLineEnd(bool extend)   => SetCursor(LineEndIdx(IndexToLineCol(_cursor).line),   extend);
        public void MoveDocStart(bool extend)  => SetCursor(0, extend);
        public void MoveDocEnd(bool extend)    => SetCursor(_value.Length, extend);

        public void MoveWordLeft(bool extend)  => SetCursor(ComputeWordLeftTarget(),  extend);
        public void MoveWordRight(bool extend) => SetCursor(ComputeWordRightTarget(), extend);

        public void PageUp(bool extend)   => MovePage(-PageSize(), extend);
        public void PageDown(bool extend) => MovePage( PageSize(), extend);
        private int PageSize() => _lastAreaH > 0 ? _lastAreaH : 10;

        private void MovePage(int deltaLines, bool extend)
        {
            var (line, col) = IndexToLineCol(_cursor);
            if (_preferredCol < 0) _preferredCol = col;
            int target = Mathf.Clamp(line + deltaLines, 0, LineCount - 1);
            int idx = LineColToIndex(target, _preferredCol);
            SetCursor(idx, extend, preservePreferredCol: true);
            _scrollLine = Mathf.Clamp(_scrollLine + deltaLines, 0, Math.Max(0, LineCount - 1));
        }

        private void SetCursor(int pos, bool extend, bool preservePreferredCol = false)
        {
            pos = Mathf.Clamp(pos, 0, _value.Length);
            if (extend) { if (_anchor < 0) _anchor = _cursor; }
            else _anchor = -1;
            _cursor = pos;
            if (!preservePreferredCol) _preferredCol = -1;
            ResetBlink();
        }

        private int ComputeWordLeftTarget()
        {
            if (_cursor == 0) return 0;
            int i = TextUtils.PrevBoundary(_value, _cursor);
            while (i > 0 && !IsWordOrNewline(i))
                i = TextUtils.PrevBoundary(_value, i);
            while (i > 0)
            {
                int prev = TextUtils.PrevBoundary(_value, i);
                if (!IsWordOrNewline(prev)) break;
                if (_value[prev] == '\n') break;
                i = prev;
            }
            return i;
        }
        private int ComputeWordRightTarget()
        {
            int i = _cursor;
            while (i < _value.Length && IsWordOrNewline(i) && _value[i] != '\n')
                i = TextUtils.NextBoundary(_value, i);
            while (i < _value.Length && !IsWordOrNewline(i))
                i = TextUtils.NextBoundary(_value, i);
            return i;
        }
        private bool IsWordOrNewline(int idx)
            => idx < _value.Length
            && (_value[idx] == '\n' || TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, idx)));

        // ── Editing ──────────────────────────────────────────────────────
        public void InsertChar(char c)
        {
            if (ReadOnly) return;
            if (c != '\n' && CharFilter != null && !CharFilter(c)) return;
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
            // Normalize line endings
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            if (CharFilter != null)
            {
                var sb = new System.Text.StringBuilder(s.Length);
                foreach (char c in s) if (c == '\n' || CharFilter(c)) sb.Append(c);
                s = sb.ToString();
                if (s.Length == 0) return;
            }
            int avail = MaxLength - _value.Length;
            if (avail <= 0) return;
            if (s.Length > avail) s = s.Substring(0, avail);
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
            GUIUtility.systemCopyBuffer = SelectedText;
            return true;
        }
        public bool Cut()
        {
            if (ReadOnly) return Copy();
            if (!HasSelection) return false;
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
            _history.Add(new Snap { V = _value, C = _cursor, A = _anchor, S = _scrollLine });
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
            var snap = new Snap { V = _value, C = _cursor, A = _anchor, S = _scrollLine };
            if (coalesce) _history[_histIdx] = snap;
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
            _value = s.V; _cursor = s.C; _anchor = s.A; _scrollLine = s.S;
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
            // Cmd (macOS) or Ctrl (Windows/Linux) shortcuts.
            if (e.HasCmdOrCtrl && !e.HasAlt)
            {
                switch (e.Key)
                {
                    case KeyCode.A: SelectAll(); return true;
                    case KeyCode.C: Copy();  return true;
                    case KeyCode.X: Cut();   return true;
                    case KeyCode.V: Paste(); return true;
                    case KeyCode.Z: if (e.HasShift) Redo(); else Undo(); return true;
                    case KeyCode.Y: Redo();  return true;
                    case KeyCode.Home: MoveDocStart(e.HasShift); return true;
                    case KeyCode.End:  MoveDocEnd(e.HasShift);   return true;
                }
            }

            // Enter inserts newline
            if (e.Key == KeyCode.Return || e.Key == KeyCode.KeypadEnter)
            {
                InsertChar('\n');
                return true;
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
                    if (e.HasCtrl) DeleteWordBack(); else DeleteBack(); return true;
                case KeyCode.Delete:
                    if (e.HasCtrl) DeleteWordForward(); else DeleteForward(); return true;
                case KeyCode.LeftArrow:
                    if (e.HasCtrl) MoveWordLeft(shift); else MoveLeft(shift); return true;
                case KeyCode.RightArrow:
                    if (e.HasCtrl) MoveWordRight(shift); else MoveRight(shift); return true;
                case KeyCode.UpArrow:    MoveUp(shift);   return true;
                case KeyCode.DownArrow:  MoveDown(shift); return true;
                case KeyCode.Home:       MoveLineStart(shift); return true;
                case KeyCode.End:        MoveLineEnd(shift);   return true;
                case KeyCode.PageUp:     PageUp(shift);   return true;
                case KeyCode.PageDown:   PageDown(shift); return true;
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
                case MouseEventType.Move: return OnMouseMove(e);
                case MouseEventType.Up:   return OnMouseUp(e);
                case MouseEventType.Click:
                    if (_downConsumed) { _downConsumed = false; return true; }
                    return OnMouseClick(e);
                default: return false;
            }
        }

        private bool OnMouseClick(TerminalMouseEvent e)
        {
            _cursor = HitTestIndex(e.Col, e.Row);
            _anchor = -1;
            _preferredCol = -1;
            ResetBlink();
            return true;
        }
        private bool OnMouseDown(TerminalMouseEvent e)
        {
            int idx = HitTestIndex(e.Col, e.Row);
            float now = NowTime();
            bool sameSpot = Mathf.Abs(e.Col - _lastClickCol) <= 1 && e.Row == _lastClickRow;
            bool inTime = (now - _lastClickTime) < MultiClickWindow;
            _clickStreak = (sameSpot && inTime) ? _clickStreak + 1 : 1;
            _lastClickTime = now;
            _lastClickCol = e.Col;
            _lastClickRow = e.Row;

            if (_clickStreak >= 3) { SelectAll(); _isDragging = false; _clickStreak = 0; return true; }
            if (_clickStreak == 2) { SelectWordAt(idx); _isDragging = false; return true; }
            _cursor = idx; _anchor = idx; _isDragging = true; _preferredCol = -1;
            ResetBlink();
            return true;
        }
        private bool OnMouseMove(TerminalMouseEvent e)
        {
            if (!_isDragging) return false;
            int idx = HitTestIndex(e.Col, e.Row);
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
        private int HitTestIndex(int absCol, int absRow)
        {
            int relRow = absRow - _lastAreaY;
            int line = _scrollLine + Mathf.Max(0, relRow);
            int last = LineCount - 1;
            if (line > last) line = last;
            int dispCol = Mathf.Max(0, absCol - _lastAreaX);

            if (line == _hScrollLine)
                return IndexAtColumnFromStart(_hScrollStartIdx, LineEndIdx(line), dispCol);
            return LineColToIndex(line, dispCol);
        }
        private void SelectWordAt(int idx)
        {
            var (line, _) = IndexToLineCol(idx);
            int lineStart = LineStartIdx(line);
            int lineEnd   = LineEndIdx(line);
            int s = Mathf.Clamp(idx, lineStart, lineEnd);
            int e = s;
            while (s > lineStart)
            {
                int prev = TextUtils.PrevBoundary(_value, s);
                if (!TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, prev))) break;
                s = prev;
            }
            while (e < lineEnd && TextUtils.IsWordCodepoint(TextUtils.CodepointAt(_value, e)))
                e = TextUtils.NextBoundary(_value, e);
            if (s == e) { _cursor = idx; _anchor = -1; }
            else        { _anchor = s; _cursor = e; }
            ResetBlink();
        }

        // ── Line/index helpers ───────────────────────────────────────────
        private (int line, int col) IndexToLineCol(int idx)
        {
            if (idx > _value.Length) idx = _value.Length;
            int line = 0, lineStart = 0;
            for (int i = 0; i < idx; i++)
                if (_value[i] == '\n') { line++; lineStart = i + 1; }
            int col = TextUtils.DisplayWidth(_value, lineStart, idx);
            return (line, col);
        }
        private int LineStartIdx(int line)
        {
            if (line <= 0) return 0;
            int l = 0;
            for (int i = 0; i < _value.Length; i++)
                if (_value[i] == '\n') { l++; if (l == line) return i + 1; }
            return _value.Length;
        }
        private int LineEndIdx(int line)
        {
            int start = LineStartIdx(line);
            int i = start;
            while (i < _value.Length && _value[i] != '\n') i++;
            return i;
        }
        private int LineColToIndex(int line, int dispCol)
            => IndexAtColumnFromStart(LineStartIdx(line), LineEndIdx(line), dispCol);

        private int IndexAtColumnFromStart(int start, int end, int targetCol)
        {
            int col = 0, i = start;
            while (i < end)
            {
                int w = TextUtils.CodepointDisplayWidth(TextUtils.CodepointAt(_value, i));
                if (col + w > targetCol)
                    return (targetCol - col) * 2 < w ? i : TextUtils.NextBoundary(_value, i);
                col += w;
                i = TextUtils.NextBoundary(_value, i);
            }
            return end;
        }

        // First codepoint boundary at or past `targetCol`, skipping any straddling
        // codepoint so the visible render starts on a clean cell.
        private int IndexAfterColumn(int start, int end, int targetCol)
        {
            int col = 0, i = start;
            while (i < end)
            {
                int w = TextUtils.CodepointDisplayWidth(TextUtils.CodepointAt(_value, i));
                if (col + w > targetCol)
                    return TextUtils.NextBoundary(_value, i);
                col += w;
                i = TextUtils.NextBoundary(_value, i);
            }
            return end;
        }

        // ── Rendering ────────────────────────────────────────────────────
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
            if (!term.TryGetAreaRect(areaId, out int ax, out int ay, out int aw, out int ah)
                || aw <= 0 || ah <= 0)
            {
                _lastAreaX = 0; _lastAreaY = 0; _lastAreaW = 0; _lastAreaH = 0;
                return;
            }
            _lastAreaX = ax; _lastAreaY = ay; _lastAreaW = aw; _lastAreaH = ah;

            if (fg.a < 0.01f)            fg            = Color.white;
            if (cursorFg.a < 0.01f)      cursorFg      = Color.black;
            if (cursorBg.a < 0.01f)      cursorBg      = Color.white;
            if (selectionFg.a < 0.01f)   selectionFg   = Color.white;
            if (selectionBg.a < 0.01f)   selectionBg   = new Color(0.2f, 0.4f, 0.8f);
            if (placeholderFg.a < 0.01f) placeholderFg = new Color(0.45f, 0.45f, 0.45f);

            EnsureCursorVisible(ah);

            // Empty placeholder
            if (_value.Length == 0)
            {
                var pb = term.BeginStyledParagraph(areaId, Alignment.Left, wrap: false);
                string ph = Placeholder ?? "";
                if (focused && CursorVisible())
                {
                    string head = ph.Length > 0 ? ph.Substring(0, 1) : " ";
                    pb.Span(head, cursorFg, cursorBg);
                    if (ph.Length > 1) pb.Span(ph.Substring(1), placeholderFg, bg);
                }
                else if (ph.Length > 0)
                {
                    pb.Span(ph, placeholderFg, bg);
                }
                pb.Render();
                return;
            }

            var b = term.BeginStyledParagraph(areaId, Alignment.Left, wrap: false);

            int lineCount = LineCount;
            var (cursorLine, _) = IndexToLineCol(_cursor);
            _hScrollLine = -1;

            for (int relLine = 0; relLine < ah; relLine++)
            {
                int line = _scrollLine + relLine;
                if (line >= lineCount) break;

                int lineStart = LineStartIdx(line);
                int lineEnd   = LineEndIdx(line);
                int renderStart = lineStart;

                // Shift the cursor line horizontally when the cursor would fall
                // off the right edge. Other lines stay anchored at column 0.
                if (line == cursorLine)
                {
                    int cursorColInLine = TextUtils.DisplayWidth(_value, lineStart, _cursor);
                    if (cursorColInLine >= aw)
                    {
                        int targetStartCol = cursorColInLine - aw + 1;
                        renderStart = IndexAfterColumn(lineStart, lineEnd, targetStartCol - 1);
                        _hScrollLine = line;
                        _hScrollStartIdx = renderStart;
                    }
                }

                int visEnd = ClipLineToWidth(renderStart, lineEnd, aw);
                RenderLineContent(b, renderStart, visEnd, lineEnd, focused, fg, bg, cursorFg, cursorBg, selectionFg, selectionBg);

                if (relLine + 1 < ah && line + 1 < lineCount)
                    b.Line();
            }

            b.Render();
        }

        private int ClipLineToWidth(int lineStart, int lineEnd, int width)
        {
            int i = lineStart, col = 0;
            while (i < lineEnd)
            {
                int w = TextUtils.CodepointDisplayWidth(TextUtils.CodepointAt(_value, i));
                if (col + w > width) break;
                col += w;
                i = TextUtils.NextBoundary(_value, i);
            }
            return i;
        }

        private void RenderLineContent(
            StyledText b, int lineStart, int visEnd, int lineEnd, bool focused,
            Color fg, Color bg, Color curFg, Color curBg, Color selFg, Color selBg)
        {
            int selS = Mathf.Clamp(SelectionStart, lineStart, visEnd);
            int selE = Mathf.Clamp(SelectionEnd,   lineStart, visEnd);
            bool hasSelInView = HasSelection && selE > selS;

            if (hasSelInView)
            {
                AppendRange(b, lineStart, selS, fg, bg);
                AppendRange(b, selS, selE, selFg, selBg);
                AppendRange(b, selE, visEnd, fg, bg);
                return;
            }

            bool cursorOnLine = focused && CursorVisible()
                             && !HasSelection
                             && _cursor >= lineStart
                             && _cursor <= lineEnd
                             && _cursor <= visEnd;

            if (cursorOnLine)
            {
                AppendRange(b, lineStart, _cursor, fg, bg);
                bool hasRealChar = _cursor < visEnd
                                && _cursor < _value.Length
                                && _value[_cursor] != '\n';
                if (hasRealChar)
                {
                    int cEnd = TextUtils.NextBoundary(_value, _cursor);
                    b.Span(_value.Substring(_cursor, cEnd - _cursor), curFg, curBg);
                    AppendRange(b, cEnd, visEnd, fg, bg);
                }
                else
                {
                    b.Span(" ", curFg, curBg);
                }
                return;
            }

            AppendRange(b, lineStart, visEnd, fg, bg);
        }

        private void AppendRange(StyledText b, int start, int end, Color fg, Color bg)
        {
            if (end <= start) return;
            b.Span(_value.Substring(start, end - start), fg, bg);
        }

        private void EnsureCursorVisible(int height)
        {
            if (height <= 0) return;
            var (cLine, _) = IndexToLineCol(_cursor);
            if (cLine < _scrollLine) _scrollLine = cLine;
            if (cLine >= _scrollLine + height) _scrollLine = cLine - height + 1;
            if (_scrollLine < 0) _scrollLine = 0;
        }
    }
}
