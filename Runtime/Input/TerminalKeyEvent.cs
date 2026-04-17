namespace RatatuiUnity
{
    /// <summary>Modifier keys bitmask.</summary>
    [System.Flags]
    public enum KeyModifiers : byte
    {
        None  = 0,
        Shift = 1,
        Ctrl  = 2,
        Alt   = 4,
    }

    /// <summary>
    /// A keyboard event in the terminal context.
    /// Carries both the Unity KeyCode and the typed character (if any).
    /// </summary>
    public readonly struct TerminalKeyEvent
    {
        /// <summary>Unity key code (e.g. KeyCode.Return, KeyCode.UpArrow).</summary>
        public readonly UnityEngine.KeyCode Key;

        /// <summary>
        /// Typed character from Input.inputString.
        /// '\0' if this is a non-character key (arrows, function keys, etc.).
        /// </summary>
        public readonly char Character;

        /// <summary>Active modifier keys at the time of the event.</summary>
        public readonly KeyModifiers Modifiers;

        public TerminalKeyEvent(UnityEngine.KeyCode key, char character, KeyModifiers modifiers)
        {
            Key       = key;
            Character = character;
            Modifiers = modifiers;
        }

        public bool HasShift => (Modifiers & KeyModifiers.Shift) != 0;
        public bool HasCtrl  => (Modifiers & KeyModifiers.Ctrl)  != 0;
        public bool HasAlt   => (Modifiers & KeyModifiers.Alt)   != 0;

        public override string ToString()
            => Character != '\0'
                ? $"Key({Key}, '{Character}', {Modifiers})"
                : $"Key({Key}, {Modifiers})";
    }
}
