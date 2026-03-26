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

        public EmailTab() { _emails.SelectFirst(); }

        public void Update(float dt) { }

        public void OnInput(KeyCode key)
        {
            if (key == KeyCode.UpArrow || key == KeyCode.W)   _emails.Previous();
            if (key == KeyCode.DownArrow || key == KeyCode.S) _emails.Next();
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

            string items = _emails.ToItemsString(e =>
                $"{(e.Read ? "  " : "● ")}{e.From,-20} {e.Subject}");
            term.SetStyle(Color.clear, Color.clear);
            term.List(inner, items, _emails.Selected);

            term.Scrollbar(area, Emails.Length, System.Math.Max(0, _emails.Selected),
                viewportLength: 5, orientation: ScrollbarOrientation.VerticalRight);
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
