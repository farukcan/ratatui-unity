using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo1 "Colors" tab — shows all named terminal colors as styled spans.
    /// Uses StyledParagraph so each row is colored with its own fg/bg sample.
    /// </summary>
    public class ColorsTab : ITab
    {
        public string Title => "Colors";

        private static readonly (string Name, Color Rgb)[] NamedColors =
        {
            ("Black",   Color.black),
            ("Red",     Color.red),
            ("Green",   Color.green),
            ("Yellow",  Color.yellow),
            ("Blue",    Color.blue),
            ("Magenta", Color.magenta),
            ("Cyan",    Color.cyan),
            ("White",   Color.white),
            ("DarkGray", new Color(0.25f, 0.25f, 0.25f)),
            ("LightRed",     new Color(1f, 0.5f, 0.5f)),
            ("LightGreen",   new Color(0.5f, 1f, 0.5f)),
            ("LightYellow",  new Color(1f, 1f, 0.5f)),
            ("LightBlue",    new Color(0.5f, 0.5f, 1f)),
            ("LightMagenta", new Color(1f, 0.5f, 1f)),
            ("LightCyan",    new Color(0.5f, 1f, 1f)),
            ("Gray",         new Color(0.5f, 0.5f, 0.5f)),
        };

        public void Update(float dt) { }
        public void OnInput(KeyCode key) { }
        public void OnKeyEvent(TerminalKeyEvent e) { }
        public void OnMouseEvent(TerminalMouseEvent e) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Colors", Borders.All);
            uint inner = term.Inner(area);

            var cols = term.Split(inner, Direction.Horizontal,
                Constraint.Percentage(50),
                Constraint.Percentage(50));

            if (cols.Length < 2) return;

            // Build and render each column separately: the FFI layer holds only one
            // pending styled paragraph at a time, so both builders must not overlap.
            var left = term.BeginStyledParagraph(cols[0], Alignment.Left, false);
            for (int i = 0; i < NamedColors.Length / 2; i++)
            {
                var (name, rgb) = NamedColors[i];
                left.Span($"{name,-14}: ", rgb, modifiers: Modifier.Bold)
                    .Span(" Foreground ", rgb)
                    .Span("  ")
                    .Span(" Background ", Color.black, rgb)
                    .Line();
            }
            left.Render();

            var right = term.BeginStyledParagraph(cols[1], Alignment.Left, false);
            for (int i = NamedColors.Length / 2; i < NamedColors.Length; i++)
            {
                var (name, rgb) = NamedColors[i];
                right.Span($"{name,-14}: ", rgb, modifiers: Modifier.Bold)
                     .Span(" Foreground ", rgb)
                     .Span("  ")
                     .Span(" Background ", Color.black, rgb)
                     .Line();
            }
            right.Render();
        }
    }
}
