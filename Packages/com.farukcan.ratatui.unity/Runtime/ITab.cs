using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Contract for a self-contained demo tab. The host <see cref="RatatuiRenderer"/>
    /// calls <see cref="Update"/> every frame, then <see cref="Render"/> inside
    /// <c>BuildFrame</c>, and forwards user input via <see cref="OnKeyEvent"/>
    /// and <see cref="OnMouseEvent"/>.
    /// </summary>
    public interface ITab
    {
        /// <summary>Label shown in the tab bar.</summary>
        string Title { get; }

        /// <summary>Called every frame for animation / signal advancement.</summary>
        void Update(float deltaTime);

        /// <summary>Forward a keyboard event to the tab.</summary>
        void OnKeyEvent(TerminalKeyEvent e);

        /// <summary>Forward a mouse event to the tab.</summary>
        void OnMouseEvent(TerminalMouseEvent e);

        /// <summary>Render all widgets for this tab into <paramref name="area"/>.</summary>
        void Render(RatatuiTerminal term, uint area);

        /// <summary>[Deprecated] Use OnKeyEvent instead.</summary>
        void OnInput(KeyCode key);
    }
}
