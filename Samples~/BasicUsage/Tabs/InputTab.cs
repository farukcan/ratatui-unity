using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo tab showcasing TerminalInput fields with keyboard editing
    /// and mouse-click cursor positioning.
    /// </summary>
    public class InputTab : ITab
    {
        public string Title => "Input";

        private readonly TerminalInput _nameInput = new TerminalInput("Faruk");
        private readonly TerminalInput _messageInput = new TerminalInput("Hello, ratatui-unity!");

        private int _focusedField; // 0 = name, 1 = message
        private string _submitted = "";

        // Area IDs for mouse hit-testing
        private uint _nameArea;
        private uint _messageArea;

        private TerminalInput FocusedInput =>
            _focusedField == 0 ? _nameInput : _messageInput;

        public void Update(float dt) { }
        public void OnInput(KeyCode key) { }

        public void OnKeyEvent(TerminalKeyEvent e)
        {
            // Tab switches focus
            if (e.Key == KeyCode.Tab)
            {
                _focusedField = (_focusedField + 1) % 2;
                return;
            }

            // Enter submits
            if (e.Key == KeyCode.Return || e.Key == KeyCode.KeypadEnter)
            {
                _submitted = $"Name=\"{_nameInput.Value}\" Message=\"{_messageInput.Value}\"";
                return;
            }

            FocusedInput.HandleKeyEvent(e);
        }

        public void OnMouseEvent(TerminalMouseEvent e)
        {
            if (e.Type != MouseEventType.Click || e.Button != MouseButton.Left)
                return;

            if (e.AreaId == _nameArea)
            {
                _focusedField = 0;
                _nameInput.HandleMouseEvent(e);
            }
            else if (e.AreaId == _messageArea)
            {
                _focusedField = 1;
                _messageInput.HandleMouseEvent(e);
            }
        }

        public void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Length(3),  // name field
                Constraint.Length(3),  // message field
                Constraint.Length(3),  // help text
                Constraint.Fill(1));   // output

            if (rows.Length < 4) return;

            // Name field
            RenderField(term, rows[0], "Name", _nameInput, _focusedField == 0, out _nameArea);

            // Message field
            RenderField(term, rows[1], "Message", _messageInput, _focusedField == 1, out _messageArea);

            // Help text
            term.BeginStyledParagraph(rows[2], Alignment.Left, wrap: true)
                .Span(" Tab", Color.cyan, modifiers: Modifier.Bold)
                .Span("=switch field  ")
                .Span("Enter", Color.cyan, modifiers: Modifier.Bold)
                .Span("=submit  ")
                .Span("Ctrl+←→", Color.cyan, modifiers: Modifier.Bold)
                .Span("=word jump  ")
                .Span("Home/End", Color.cyan, modifiers: Modifier.Bold)
                .Span("=start/end")
                .Render();

            // Output area
            term.Block(rows[3], "Output", Borders.All);
            uint outputInner = term.Inner(rows[3]);

            var info = FocusedInput;
            string status = $"Cursor: {info.Cursor}/{info.Value.Length}  Scroll: {info.ScrollOffset}";
            if (!string.IsNullOrEmpty(_submitted))
                status += $"\nSubmitted: {_submitted}";

            term.Paragraph(outputInner, status);
        }

        private void RenderField(
            RatatuiTerminal term, uint area, string label,
            TerminalInput input, bool focused, out uint inputArea)
        {
            Color borderFg = focused
                ? Color.cyan
                : new Color(0.4f, 0.4f, 0.4f);

            term.SetStyle(borderFg, Color.clear, focused ? Modifier.Bold : Modifier.None);
            term.Block(area, label, Borders.All);
            inputArea = term.Inner(area);

            Color cursorBg = focused ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            input.Render(term, inputArea,
                cursorFg: Color.black, cursorBg: cursorBg);
        }
    }
}
