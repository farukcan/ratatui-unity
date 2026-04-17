using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Single-line text input field with cursor navigation, editing,
    /// and horizontal scrolling. Pure C# state — renders via StyledParagraph.
    /// </summary>
    public class TerminalInput
    {
        private string _value;
        private int _cursor;
        private int _scrollOffset;

        // Stored from last Render() for mouse click positioning
        private uint _lastAreaId;
        private int _lastAreaX;

        /// <summary>Current text content. Setting this clamps the cursor.</summary>
        public string Value
        {
            get => _value;
            set
            {
                _value = value ?? string.Empty;
                if (_cursor > _value.Length) _cursor = _value.Length;
            }
        }

        /// <summary>Cursor position as a character index [0..Value.Length].</summary>
        public int Cursor
        {
            get => _cursor;
            set => _cursor = Mathf.Clamp(value, 0, _value.Length);
        }

        /// <summary>Index of the first visible character (auto-managed during Render).</summary>
        public int ScrollOffset => _scrollOffset;

        /// <summary>Create an input with optional initial value.</summary>
        public TerminalInput(string initialValue = "")
        {
            _value = initialValue ?? string.Empty;
            _cursor = _value.Length;
        }

        // ── Cursor Movement ──────────────────────────────────────────────

        public void MoveToStart() => _cursor = 0;
        public void MoveToEnd() => _cursor = _value.Length;
        public void MoveLeft() { if (_cursor > 0) _cursor--; }
        public void MoveRight() { if (_cursor < _value.Length) _cursor++; }

        public void MoveWordLeft()
        {
            if (_cursor == 0) return;
            int i = _cursor - 1;
            // Skip non-word chars
            while (i > 0 && !IsWordChar(_value[i])) i--;
            // Skip word chars
            while (i > 0 && IsWordChar(_value[i - 1])) i--;
            _cursor = i;
        }

        public void MoveWordRight()
        {
            if (_cursor >= _value.Length) return;
            int i = _cursor;
            // Skip word chars
            while (i < _value.Length && IsWordChar(_value[i])) i++;
            // Skip non-word chars
            while (i < _value.Length && !IsWordChar(_value[i])) i++;
            _cursor = i;
        }

        // ── Editing ──────────────────────────────────────────────────────

        public void InsertChar(char c)
        {
            _value = _value.Insert(_cursor, c.ToString());
            _cursor++;
        }

        /// <summary>Delete character before cursor (Backspace).</summary>
        public void DeleteBack()
        {
            if (_cursor <= 0) return;
            _value = _value.Remove(_cursor - 1, 1);
            _cursor--;
        }

        /// <summary>Delete character at cursor (Delete key).</summary>
        public void DeleteForward()
        {
            if (_cursor >= _value.Length) return;
            _value = _value.Remove(_cursor, 1);
        }

        /// <summary>Delete word before cursor (Ctrl+Backspace).</summary>
        public void DeleteWordBack()
        {
            if (_cursor <= 0) return;
            int oldCursor = _cursor;
            MoveWordLeft();
            _value = _value.Remove(_cursor, oldCursor - _cursor);
        }

        /// <summary>Delete word at cursor (Ctrl+Delete).</summary>
        public void DeleteWordForward()
        {
            if (_cursor >= _value.Length) return;
            int oldCursor = _cursor;
            MoveWordRight();
            int count = _cursor - oldCursor;
            _cursor = oldCursor;
            _value = _value.Remove(_cursor, count);
        }

        // ── Input Handling ───────────────────────────────────────────────

        /// <summary>
        /// Process a keyboard event. Call from OnTerminalKeyDown.
        /// Returns true if the event was consumed.
        /// </summary>
        public bool HandleKeyEvent(TerminalKeyEvent e)
        {
            // Printable character
            if (e.Character != '\0')
            {
                InsertChar(e.Character);
                return true;
            }

            switch (e.Key)
            {
                case KeyCode.Backspace:
                    if (e.HasCtrl) DeleteWordBack(); else DeleteBack();
                    return true;

                case KeyCode.Delete:
                    if (e.HasCtrl) DeleteWordForward(); else DeleteForward();
                    return true;

                case KeyCode.LeftArrow:
                    if (e.HasCtrl) MoveWordLeft(); else MoveLeft();
                    return true;

                case KeyCode.RightArrow:
                    if (e.HasCtrl) MoveWordRight(); else MoveRight();
                    return true;

                case KeyCode.Home:
                    MoveToStart();
                    return true;

                case KeyCode.End:
                    MoveToEnd();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Process a mouse click to position the cursor.
        /// Call from OnTerminalMouseEvent for Click events on the input area.
        /// Uses the area geometry stored from the last <see cref="Render"/> call.
        /// Returns true if cursor position changed.
        /// </summary>
        public bool HandleMouseEvent(TerminalMouseEvent e)
        {
            if (e.Type != MouseEventType.Click || e.Button != MouseButton.Left)
                return false;

            int localCol = e.Col - _lastAreaX;
            int textIndex = _scrollOffset + localCol;
            int oldCursor = _cursor;
            _cursor = Mathf.Clamp(textIndex, 0, _value.Length);
            return _cursor != oldCursor;
        }

        // ── Rendering ────────────────────────────────────────────────────

        /// <summary>
        /// Render the input field into the given area. Shows cursor as
        /// inverted-color cell. Handles horizontal scrolling automatically.
        /// </summary>
        /// <param name="fg">Text foreground color (Color.clear = terminal default white).</param>
        /// <param name="bg">Text background color (Color.clear = terminal default).</param>
        /// <param name="cursorFg">Cursor foreground (Color.clear = black).</param>
        /// <param name="cursorBg">Cursor background (Color.clear = white).</param>
        public void Render(
            RatatuiTerminal term, uint areaId,
            Color fg = default, Color bg = default,
            Color cursorFg = default, Color cursorBg = default)
        {
            // Store for mouse handling
            _lastAreaId = areaId;

            if (!term.TryGetAreaRect(areaId, out int ax, out int ay, out int aw, out int ah)
                || aw <= 0)
            {
                _lastAreaX = 0;
                return;
            }

            _lastAreaX = ax;
            int width = aw;

            // Resolve default colors
            if (fg.a < 0.01f) fg = Color.white;
            if (cursorFg.a < 0.01f) cursorFg = Color.black;
            if (cursorBg.a < 0.01f) cursorBg = Color.white;

            // Adjust scroll offset so cursor is visible
            if (_cursor < _scrollOffset)
                _scrollOffset = _cursor;
            if (_cursor >= _scrollOffset + width)
                _scrollOffset = _cursor - width + 1;
            if (_scrollOffset < 0)
                _scrollOffset = 0;

            // Compute visible portion
            int visibleStart = _scrollOffset;
            int visibleLen = Mathf.Min(width, _value.Length - visibleStart);
            if (visibleLen < 0) visibleLen = 0;

            int cursorInView = _cursor - _scrollOffset;

            // Build the styled paragraph
            var builder = term.BeginStyledParagraph(areaId, Alignment.Left, wrap: false);

            // Before cursor
            if (cursorInView > 0 && visibleLen > 0)
            {
                int beforeLen = Mathf.Min(cursorInView, visibleLen);
                string before = _value.Substring(visibleStart, beforeLen);
                builder.Span(before, fg, bg);
            }

            // Cursor character (inverted)
            if (_cursor < _value.Length)
            {
                builder.Span(_value[_cursor].ToString(), cursorFg, cursorBg);
            }
            else
            {
                // Cursor at end: show a space block
                builder.Span(" ", cursorFg, cursorBg);
            }

            // After cursor
            int afterStart = _cursor + 1;
            if (afterStart < _value.Length)
            {
                int afterVisibleStart = afterStart;
                int afterVisibleEnd = Mathf.Min(_value.Length, _scrollOffset + width);
                if (afterVisibleEnd > afterVisibleStart)
                {
                    string after = _value.Substring(afterVisibleStart, afterVisibleEnd - afterVisibleStart);
                    builder.Span(after, fg, bg);
                }
            }

            builder.Render();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
