using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Bridges TerminalInput / TerminalTextArea to the native virtual keyboard
    /// (TouchScreenKeyboard) on iOS, Android, and mobile WebGL. On platforms
    /// where TouchScreenKeyboard.isSupported is false (desktop, console),
    /// every method is a no-op and the host widget continues to receive input
    /// through the normal Input.inputString path.
    /// </summary>
    internal class MobileKeyboardBridge
    {
        private TouchScreenKeyboard _kb;
        private string _lastText = string.Empty;

        public bool IsAvailable => TouchScreenKeyboard.isSupported;
        public bool IsActive => _kb != null && TouchScreenKeyboard.visible;

        public void Open(
            string initialText,
            bool multiline,
            bool secure,
            string placeholder,
            int characterLimit,
            TouchScreenKeyboardType type,
            bool autocorrection)
        {
            if (!IsAvailable) return;
            // If already open with the same text, reuse it — re-opening forces
            // the IME to redraw and steals focus on Android.
            if (_kb != null && _kb.active) return;

            int limit = characterLimit == int.MaxValue ? 0 : Mathf.Max(0, characterLimit);
            _lastText = initialText ?? string.Empty;
            _kb = TouchScreenKeyboard.Open(
                _lastText,
                type,
                autocorrection,
                multiline,
                secure,
                false,
                placeholder ?? string.Empty,
                limit);
        }

        public void Close()
        {
            if (_kb == null) return;
            _kb.active = false;
            _kb = null;
            _lastText = string.Empty;
        }

        /// <summary>
        /// Polls the native keyboard. Sets <paramref name="newText"/> when the
        /// buffer changed since the last poll, with <paramref name="caret"/>
        /// holding the IME caret index (0..newText.Length). Sets
        /// <paramref name="closed"/> when the user dismissed the keyboard
        /// (Done / Canceled / LostFocus). Returns true when either flag is
        /// meaningful.
        /// </summary>
        public bool Poll(out string newText, out int caret, out bool closed)
        {
            newText = null;
            caret = 0;
            closed = false;
            if (_kb == null) return false;

            string text = _kb.text ?? string.Empty;
            bool textChanged = text != _lastText;
            if (textChanged)
            {
                _lastText = text;
                newText = text;
                var sel = _kb.selection;
                // selection.start is the caret when length==0; otherwise the
                // end of the selection is closer to where the user is typing.
                int c = sel.start + sel.length;
                if (c < 0) c = 0;
                if (c > text.Length) c = text.Length;
                caret = c;
            }

            var status = _kb.status;
            if (status == TouchScreenKeyboard.Status.Done
                || status == TouchScreenKeyboard.Status.Canceled
                || status == TouchScreenKeyboard.Status.LostFocus)
            {
                _kb = null;
                _lastText = string.Empty;
                closed = true;
            }
            return textChanged || closed;
        }

        /// <summary>
        /// Sync the keyboard's internal buffer with an externally edited value
        /// (e.g. when the host widget mutates Value while the keyboard is open).
        /// </summary>
        public void PushText(string text)
        {
            if (_kb == null) return;
            string s = text ?? string.Empty;
            if (s == _lastText) return;
            _kb.text = s;
            _lastText = s;
        }
    }
}
