using System.Collections.Generic;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// Bounded command history with bash-style navigation.
    /// Index value -1 means "no history entry currently shown".
    /// </summary>
    public sealed class ConsoleHistory
    {
        private readonly List<string> _entries;
        private readonly int _capacity;
        private int _index = -1;

        public ConsoleHistory(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 64;
            _entries = new List<string>(_capacity);
        }

        public int Count => _entries.Count;
        public int Index => _index;

        public void Push(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            // Avoid duplicating the last entry.
            if (_entries.Count > 0 && _entries[_entries.Count - 1] == command)
            {
                _index = -1;
                return;
            }
            if (_entries.Count >= _capacity)
                _entries.RemoveAt(0);
            _entries.Add(command);
            _index = -1;
        }

        public void Reset()
        {
            _index = -1;
        }

        /// <summary>
        /// Move to the previous (older) entry. Returns the new text or null
        /// when there is no older entry to show.
        /// </summary>
        public string MovePrev()
        {
            if (_entries.Count == 0) return null;
            if (_index == -1)
                _index = _entries.Count - 1;
            else if (_index > 0)
                _index--;
            else
                return _entries[_index];
            return _entries[_index];
        }

        /// <summary>
        /// Move to the next (newer) entry. Returns the new text, or empty
        /// string when we step past the newest entry (clears the prompt).
        /// </summary>
        public string MoveNext()
        {
            if (_entries.Count == 0 || _index == -1) return null;
            if (_index < _entries.Count - 1)
            {
                _index++;
                return _entries[_index];
            }
            _index = -1;
            return string.Empty;
        }
    }
}
