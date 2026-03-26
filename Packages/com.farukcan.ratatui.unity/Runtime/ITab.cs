using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Contract for a self-contained demo tab. The host <see cref="RatatuiRenderer"/>
    /// calls <see cref="Update"/> every frame, then <see cref="Render"/> inside
    /// <c>BuildFrame</c>, and forwards user key presses via <see cref="OnInput"/>.
    /// </summary>
    public interface ITab
    {
        /// <summary>Label shown in the tab bar.</summary>
        string Title { get; }

        /// <summary>Called every frame for animation / signal advancement.</summary>
        void Update(float deltaTime);

        /// <summary>Forward a key press to the tab for internal navigation.</summary>
        void OnInput(KeyCode key);

        /// <summary>Render all widgets for this tab into <paramref name="area"/>.</summary>
        void Render(RatatuiTerminal term, uint area);
    }
}
