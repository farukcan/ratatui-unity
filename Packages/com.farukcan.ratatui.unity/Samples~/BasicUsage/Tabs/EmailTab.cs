using System.Text;
using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo2 "Email" tab — inbox list with scrollbar and a reading pane.
    /// </summary>
    public class EmailTab : ITab
    {
        public string Title => "Email";

        private struct Email
        {
            public string From;
            public string Subject;
            public string Body;
            public bool   Read;
        }

        private static readonly Email[] Emails =
        {
            new Email { From="alice@rust.dev",   Subject="Ratatui 0.30 released!", Read=false,
                Body="Hey! Ratatui 0.30 is out with awesome canvas and calendar widgets." },
            new Email { From="bob@unity3d.com",  Subject="Unity 2025 roadmap",     Read=true,
                Body="The roadmap includes better native plugin support and faster IL2CPP." },
            new Email { From="ci@github.com",    Subject="Build #42 passed",        Read=true,
                Body="All 127 tests passed on macOS, Windows, and Linux." },
            new Email { From="spam@promo.biz",   Subject="You won a prize!",        Read=false,
                Body="Click here to claim your prize... (don't actually click)" },
            new Email { From="carol@dev.io",     Subject="PR review requested",     Read=false,
                Body="Please review the FFI layer changes in PR #88." },
            new Email { From="system@host.net",  Subject="Disk usage warning",      Read=true,
                Body="Disk usage is at 85%. Consider cleaning up build artifacts." },
        };

        private readonly StatefulList<Email> _emails = new StatefulList<Email>(Emails);

        // Mouse hit-testing: area ID and top row of the inbox list (from previous frame)
        private uint _inboxInnerArea;
        private int  _inboxTop;
        private int  _hoveredEmail = -1;

        public EmailTab() { _emails.SelectFirst(); }

        public void Update(float dt) { }

        public void OnInput(KeyCode key)
        {
            if (key == KeyCode.UpArrow || key == KeyCode.W)   _emails.Previous();
            if (key == KeyCode.DownArrow || key == KeyCode.S) _emails.Next();
        }

        public void OnKeyEvent(TerminalKeyEvent e)
        {
            if (e.Key == KeyCode.UpArrow   || e.Character == 'w' || e.Character == 'W') _emails.Previous();
            if (e.Key == KeyCode.DownArrow || e.Character == 's' || e.Character == 'S') _emails.Next();
        }

        public void OnMouseEvent(TerminalMouseEvent e)
        {
            if (e.AreaId == _inboxInnerArea)
            {
                if (e.Type == MouseEventType.Click && e.Button == MouseButton.Left)
                {
                    int localRow = e.Row - _inboxTop;
                    _emails.Select(localRow);
                }
                if (e.Type == MouseEventType.Scroll)
                {
                    if (e.ScrollDelta > 0) _emails.Previous();
                    else _emails.Next();
                }
            }
        }

        public void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState)
        {
            _hoveredEmail = (newState.IsInside && newState.AreaId == _inboxInnerArea)
                ? newState.Row - _inboxTop
                : -1;
        }

        public void Render(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Percentage(40),
                Constraint.Percentage(60));

            if (rows.Length < 2) return;

            RenderInbox(term, rows[0]);
            RenderBody(term, rows[1]);
        }

        private void RenderInbox(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Inbox", Borders.All);
            uint inner = term.Inner(area);

            // Store area info for mouse hit-testing
            _inboxInnerArea = inner;
            if (term.TryGetAreaRect(inner, out int ax, out int ay, out int aw, out int ah))
                _inboxTop = ay;

            RenderInboxList(term, inner, _emails.Selected, _hoveredEmail);

            term.Scrollbar(area, Emails.Length, System.Math.Max(0, _emails.Selected),
                viewportLength: 5, orientation: ScrollbarOrientation.VerticalRight);
        }

        private void RenderInboxList(RatatuiTerminal term, uint area, int selected, int hovered)
        {
            var b = term.BeginStyledParagraph(area, Alignment.Left, false);
            for (int i = 0; i < Emails.Length; i++)
            {
                bool isSelected = i == selected;
                bool isHovered  = i == hovered && !isSelected;
                Color fg = isSelected ? Color.black
                         : isHovered  ? Color.white
                         : Color.clear;
                Color bg = isSelected ? Color.cyan
                         : isHovered  ? new Color(0.15f, 0.15f, 0.3f)
                         : Color.clear;
                string text = $"{(Emails[i].Read ? "  " : "● ")}{Emails[i].From,-20} {Emails[i].Subject}";
                b.SpanLine(text, fg, bg);
            }
            b.Render();
        }

        private void RenderBody(RatatuiTerminal term, uint area)
        {
            int idx = _emails.Selected < 0 ? 0 : _emails.Selected;
            var email = Emails[idx];

            term.Block(area, $"From: {email.From}", Borders.All);
            uint inner = term.Inner(area);

            term.BeginStyledParagraph(inner, Alignment.Left, true)
                .Span("Subject: ", modifiers: Modifier.Bold)
                .Span(email.Subject).Line()
                .Span("─────────────────────────────────").Line()
                .Span(email.Body)
                .Render();
        }
    }
}
