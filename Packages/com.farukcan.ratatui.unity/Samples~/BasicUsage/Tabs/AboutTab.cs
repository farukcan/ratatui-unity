using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo2 "About" tab — styled text with key-binding instructions.
    /// </summary>
    public class AboutTab : ITab
    {
        public string Title => "About";

        public void Update(float dt) { }
        public void OnInput(KeyCode key) { }
        public void OnKeyEvent(TerminalKeyEvent e) { }
        public void OnMouseEvent(TerminalMouseEvent e) { }
        public void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            term.Block(area, "About Ratatui + Unity", Borders.All);
            uint inner = term.Inner(area);

            term.BeginStyledParagraph(inner, Alignment.Left, true)
                .Span("ratatui-unity", fg: Color.cyan, modifiers: Modifier.Bold)
                .Span(" — a Rust ")
                .Span("ratatui", fg: Color.yellow, modifiers: Modifier.Italic)
                .Span(" rendering backend for Unity via FFI.")
                .Line()
                .Line()
                .Span("• All widget ", modifiers: Modifier.Bold).Span("data is owned in C#").Line()
                .Span("• Rust acts as a ").Span("pure rendering engine", fg: Color.green).Line()
                .Span("• Zero-copy pixel buffer via raw pointer").Line()
                .Line()
                .Span("Keyboard", modifiers: Modifier.Bold | Modifier.Underlined).Line()
                .Span("  A / D        ", fg: Color.cyan).Span("switch tabs").Line()
                .Span("  W / S        ", fg: Color.cyan).Span("navigate lists").Line()
                .Span("  Arrows       ", fg: Color.cyan).Span("same as W/S/A/D").Line()
                .Line()
                .Span("Mouse", modifiers: Modifier.Bold | Modifier.Underlined).Line()
                .Span("  Click tab    ", fg: Color.cyan).Span("switch tabs").Line()
                .Span("  Click item   ", fg: Color.cyan).Span("select list item").Line()
                .Span("  Hover item   ", fg: Color.cyan).Span("highlight list item").Line()
                .Span("  Scroll       ", fg: Color.cyan).Span("navigate lists / cycle tabs").Line()
                .Render();
        }
    }
}
