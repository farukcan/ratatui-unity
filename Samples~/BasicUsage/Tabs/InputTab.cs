using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo tab showcasing the full TerminalInput / TerminalTextArea feature set:
    /// placeholder, password mask, numeric filter + max-length, read-only field,
    /// multi-line textarea, selection, clipboard, undo/redo, click-and-drag,
    /// double-click word, triple-click all, focus-time select-all, cursor blink.
    /// </summary>
    public class InputTab : ITab
    {
        public string Title => "Input";

        private readonly TerminalInput _name = new TerminalInput("Faruk")
        {
            Placeholder = "Your name",
        };
        private readonly TerminalInput _pass = new TerminalInput()
        {
            Placeholder = "Password",
            MaskChar = '•',
        };
        private readonly TerminalInput _phone = new TerminalInput()
        {
            Placeholder = "Digits only (max 10)",
            MaxLength = 10,
            CharFilter = c => c >= '0' && c <= '9',
        };
        private readonly TerminalInput _readonly = new TerminalInput(
            "Read-only — select + copy works, edit doesn't")
        {
            ReadOnly = true,
            SelectAllOnFocus = false,
        };
        private readonly TerminalTextArea _note = new TerminalTextArea()
        {
            Placeholder = "Multi-line notepad. Enter inserts newline. CJK ok: 日本語",
            SelectAllOnFocus = false,
        };

        // -1 = no focus; 0..4 = field index
        private int _focusedField = -1;
        private string _submitted = "";

        // Area IDs for mouse hit-testing
        private readonly uint[] _areas = new uint[5];
        private static readonly string[] FieldNames =
            { "Name", "Password", "Phone", "ReadOnly", "Notepad" };

        public bool HasFocusedField => _focusedField >= 0;

        public void Update(float dt) { }
        public void OnInput(KeyCode key) { }
        public void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState) { }

        // ── Keyboard ─────────────────────────────────────────────────────
        public void OnKeyEvent(TerminalKeyEvent e)
        {
            // Tab / Shift+Tab cycles fields (and -1 = none).
            if (e.Key == KeyCode.Tab)
            {
                int n = FieldNames.Length;
                int next = e.HasShift ? _focusedField - 1 : _focusedField + 1;
                if (next > n - 1) next = -1;
                if (next < -1) next = n - 1;
                SetFocus(next);
                return;
            }

            if (_focusedField < 0) return;

            // Enter on a single-line field submits. Textarea handles Enter as newline.
            if (_focusedField != 4
                && (e.Key == KeyCode.Return || e.Key == KeyCode.KeypadEnter))
            {
                _submitted = $"Name=\"{_name.Value}\"  Pass={new string('*', _pass.Value.Length)}  Phone=\"{_phone.Value}\"  Note=\"{Truncate(_note.Value, 40)}\"";
                return;
            }

            // Dispatch to focused field.
            switch (_focusedField)
            {
                case 0: _name.HandleKeyEvent(e); break;
                case 1: _pass.HandleKeyEvent(e); break;
                case 2: _phone.HandleKeyEvent(e); break;
                case 3: _readonly.HandleKeyEvent(e); break;
                case 4: _note.HandleKeyEvent(e); break;
            }
        }

        // ── Mouse ────────────────────────────────────────────────────────
        public void OnMouseEvent(TerminalMouseEvent e)
        {
            // Scroll routes to the field under the cursor regardless of focus.
            if (e.Type == MouseEventType.Scroll)
            {
                int hit = HitField(e.AreaId);
                if (hit >= 0) DispatchMouse(hit, e);
                return;
            }

            if (e.Button != MouseButton.Left) return;

            // On Down: hit-test area, change focus, forward event.
            if (e.Type == MouseEventType.Down)
            {
                int hit = HitField(e.AreaId);
                if (hit < 0) return;
                if (hit != _focusedField) SetFocus(hit);
                DispatchMouse(hit, e);
                return;
            }

            // Move / Up routed to currently focused field (drag continues).
            if (_focusedField >= 0)
                DispatchMouse(_focusedField, e);
        }

        private int HitField(uint areaId)
        {
            for (int i = 0; i < _areas.Length; i++)
                if (areaId == _areas[i]) return i;
            // The textarea splits its inner area for scrollbars, so hit-testing
            // may resolve to a sub-area the loop above does not track.
            if (_note.OwnsArea(areaId)) return 4;
            return -1;
        }

        private void DispatchMouse(int field, TerminalMouseEvent e)
        {
            switch (field)
            {
                case 0: _name.HandleMouseEvent(e); break;
                case 1: _pass.HandleMouseEvent(e); break;
                case 2: _phone.HandleMouseEvent(e); break;
                case 3: _readonly.HandleMouseEvent(e); break;
                case 4: _note.HandleMouseEvent(e); break;
            }
        }

        private void SetFocus(int newField)
        {
            if (newField == _focusedField) return;
            // Blur old
            switch (_focusedField)
            {
                case 0: _name.OnBlur(); break;
                case 1: _pass.OnBlur(); break;
                case 2: _phone.OnBlur(); break;
                case 3: _readonly.OnBlur(); break;
                case 4: _note.OnBlur(); break;
            }
            _focusedField = newField;
            // Focus new
            switch (newField)
            {
                case 0: _name.OnFocus(); break;
                case 1: _pass.OnFocus(); break;
                case 2: _phone.OnFocus(); break;
                case 3: _readonly.OnFocus(); break;
                case 4: _note.OnFocus(); break;
            }
        }

        // ── Render ───────────────────────────────────────────────────────
        public void Render(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Length(3),   // Name + Password
                Constraint.Length(3),   // Phone + ReadOnly
                Constraint.Min(6),      // Notepad
                Constraint.Length(4),   // Help
                Constraint.Length(3));  // Output

            if (rows.Length < 5) return;

            // Fill(1) twice = deterministic equal split. Percentage(50)+Percentage(50)
            // can flip-flop column widths on odd parent widths under ratatui's solver
            // tie-breaker, which makes the entire tab oscillate frame-to-frame.
            var top = term.Split(rows[0], Direction.Horizontal,
                Constraint.Fill(1), Constraint.Fill(1));
            var mid = term.Split(rows[1], Direction.Horizontal,
                Constraint.Fill(1), Constraint.Fill(1));
            if (top.Length < 2 || mid.Length < 2) return;

            RenderInputField(term, top[0], "Name", _name, 0);
            RenderInputField(term, top[1], "Password (mask)", _pass, 1);
            RenderInputField(term, mid[0], "Phone (digits, max 10)", _phone, 2);
            RenderInputField(term, mid[1], "ReadOnly", _readonly, 3);
            RenderTextArea(term, rows[2], "Notepad (Enter=newline)", _note, 4);

            RenderHelp(term, rows[3]);
            RenderOutput(term, rows[4]);
        }

        private void RenderInputField(
            RatatuiTerminal term, uint area, string label, TerminalInput input, int fieldIdx)
        {
            bool focused = _focusedField == fieldIdx;
            Color borderFg = focused ? Color.cyan : new Color(0.4f, 0.4f, 0.4f);
            term.SetStyle(borderFg, Color.clear, focused ? Modifier.Bold : Modifier.None);
            term.Block(area, label, Borders.All);

            uint inner = term.Inner(area);
            _areas[fieldIdx] = inner;
            input.Render(term, inner, focused: focused);
        }

        private void RenderTextArea(
            RatatuiTerminal term, uint area, string label, TerminalTextArea ta, int fieldIdx)
        {
            bool focused = _focusedField == fieldIdx;
            Color borderFg = focused ? Color.cyan : new Color(0.4f, 0.4f, 0.4f);
            term.SetStyle(borderFg, Color.clear, focused ? Modifier.Bold : Modifier.None);
            term.Block(area, label, Borders.All);

            uint inner = term.Inner(area);
            _areas[fieldIdx] = inner;
            ta.Render(term, inner, focused: focused);
        }

        private void RenderHelp(RatatuiTerminal term, uint area)
        {
            term.SetStyle(new Color(0.4f, 0.4f, 0.4f), Color.clear, Modifier.None);
            term.Block(area, "Shortcuts", Borders.All);
            uint inner = term.Inner(area);

            term.BeginStyledParagraph(inner, Alignment.Left, wrap: true)
                .Span(" Tab", Color.cyan, modifiers: Modifier.Bold).Span("=next  ")
                .Span("Shift+Tab", Color.cyan, modifiers: Modifier.Bold).Span("=prev  ")
                .Span("Enter", Color.cyan, modifiers: Modifier.Bold).Span("=submit/newline  ")
                .Span("Shift+Arrows", Color.cyan, modifiers: Modifier.Bold).Span("=select  ")
                .Span("Ctrl+←→", Color.cyan, modifiers: Modifier.Bold).Span("=word  ")
                .Span("Ctrl+A/C/X/V", Color.cyan, modifiers: Modifier.Bold).Span("=select-all/copy/cut/paste  ")
                .Span("Ctrl+Z/Y", Color.cyan, modifiers: Modifier.Bold).Span("=undo/redo  ")
                .Span("Click/Drag", Color.cyan, modifiers: Modifier.Bold).Span("=position/select  ")
                .Span("Double", Color.cyan, modifiers: Modifier.Bold).Span("=word  ")
                .Span("Triple", Color.cyan, modifiers: Modifier.Bold).Span("=all")
                .Render();
        }

        private void RenderOutput(RatatuiTerminal term, uint area)
        {
            term.SetStyle(new Color(0.4f, 0.4f, 0.4f), Color.clear, Modifier.None);
            term.Block(area, "Output", Borders.All);
            uint inner = term.Inner(area);

            string status;
            if (_focusedField < 0)
            {
                status = "No field focused — Tab or click to focus.";
            }
            else
            {
                string field = FieldNames[_focusedField];
                if (_focusedField == 4)
                {
                    status = $"Field: {field}  Cursor=({_note.CursorLine},{_note.CursorColumn})  Lines={_note.LineCount}  Sel={_note.SelectedText.Length}";
                }
                else
                {
                    var inp = FocusedInput();
                    status = $"Field: {field}  Cursor={inp.Cursor}/{inp.Value.Length}  Scroll={inp.ScrollOffset}  Sel=\"{Truncate(inp.SelectedText, 20)}\"";
                }
            }
            if (!string.IsNullOrEmpty(_submitted))
                status += $"  |  Submitted: {Truncate(_submitted, 80)}";

            term.Paragraph(inner, status);
        }

        private TerminalInput FocusedInput()
        {
            switch (_focusedField)
            {
                case 0: return _name;
                case 1: return _pass;
                case 2: return _phone;
                case 3: return _readonly;
                default: return _name;
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max) + "…";
        }
    }
}
