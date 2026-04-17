using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Fluent builder for styled paragraphs with per-span colors and text modifiers.
    /// </summary>
    /// <example>
    /// <code>
    /// term.BeginStyledParagraph(area, Alignment.Left, wrap: true)
    ///     .Span("Status: ")
    ///     .Span("OK", fg: Color.green, modifiers: Modifier.Bold)
    ///     .Line()
    ///     .Span("Error count: ")
    ///     .Span("3", fg: Color.red)
    ///     .Render();
    /// </code>
    /// </example>
    public sealed class StyledText
    {
        private readonly RatatuiTerminal _term;

        internal StyledText(RatatuiTerminal term, uint areaId, Alignment alignment, bool wrap)
        {
            _term = term;
            RatatuiNative.ratatui_styled_para_begin(
                term.Handle, areaId, (byte)alignment, (byte)(wrap ? 1 : 0));
        }

        /// <summary>
        /// Append a styled span to the current line.
        /// Use <c>Color.clear</c> for <paramref name="fg"/> / <paramref name="bg"/>
        /// to keep the terminal default color.
        /// </summary>
        public StyledText Span(
            string text,
            Color fg = default,
            Color bg = default,
            Modifier modifiers = Modifier.None)
        {
            bool defFg = fg.a < 0.01f;
            bool defBg = bg.a < 0.01f;
            RatatuiNative.ratatui_styled_para_span(
                _term.Handle, text ?? string.Empty,
                (byte)(fg.r * 255), (byte)(fg.g * 255), (byte)(fg.b * 255), (byte)(defFg ? 1 : 0),
                (byte)(bg.r * 255), (byte)(bg.g * 255), (byte)(bg.b * 255), (byte)(defBg ? 1 : 0),
                (byte)modifiers);
            return this;
        }

        /// <summary>Move to a new line in the paragraph.</summary>
        public StyledText Line()
        {
            RatatuiNative.ratatui_styled_para_newline(_term.Handle);
            return this;
        }

        /// <summary>
        /// Convenience: append a span then immediately start a new line.
        /// Equivalent to <c>.Span(text, fg, bg, modifiers).Line()</c>.
        /// </summary>
        public StyledText SpanLine(
            string text,
            Color fg = default,
            Color bg = default,
            Modifier modifiers = Modifier.None)
            => Span(text, fg, bg, modifiers).Line();

        /// <summary>
        /// Finalize and enqueue the styled paragraph widget command.
        /// Must be called exactly once; the builder is unusable afterward.
        /// </summary>
        public void Render()
        {
            RatatuiNative.ratatui_styled_para_end(_term.Handle);
        }
    }
}
