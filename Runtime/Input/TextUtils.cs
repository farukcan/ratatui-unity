namespace RatatuiUnity
{
    /// <summary>
    /// Codepoint iteration and terminal display-width helpers for text input widgets.
    /// All indices are .NET UTF-16 char positions; widths are terminal cell counts.
    /// </summary>
    public static class TextUtils
    {
        /// <summary>Number of UTF-16 code units that make up the codepoint starting at index.</summary>
        public static int CodepointSize(string s, int index)
        {
            if (index < 0 || index >= s.Length) return 0;
            char c = s[index];
            return (char.IsHighSurrogate(c) && index + 1 < s.Length
                    && char.IsLowSurrogate(s[index + 1]))
                ? 2 : 1;
        }

        /// <summary>Char index of the next codepoint boundary at-or-after index.</summary>
        public static int NextBoundary(string s, int index)
        {
            int size = CodepointSize(s, index);
            return size == 0 ? index : index + size;
        }

        /// <summary>Char index of the previous codepoint boundary before index.</summary>
        public static int PrevBoundary(string s, int index)
        {
            if (index <= 0) return 0;
            int i = index - 1;
            if (i > 0 && char.IsLowSurrogate(s[i]) && char.IsHighSurrogate(s[i - 1]))
                i--;
            return i;
        }

        /// <summary>UTF-32 codepoint at the given char index, or 0 if past end.</summary>
        public static int CodepointAt(string s, int index)
        {
            if (index < 0 || index >= s.Length) return 0;
            char c = s[index];
            if (char.IsHighSurrogate(c) && index + 1 < s.Length
                && char.IsLowSurrogate(s[index + 1]))
                return char.ConvertToUtf32(c, s[index + 1]);
            return c;
        }

        /// <summary>Terminal cell width of a single codepoint (1 or 2). Control chars return 0.</summary>
        public static int CodepointDisplayWidth(int cp)
        {
            if (cp < 0x20 || cp == 0x7F) return 0;
            if (IsEastAsianWide(cp)) return 2;
            return 1;
        }

        /// <summary>Total display width of the substring [start, end).</summary>
        public static int DisplayWidth(string s, int start, int end)
        {
            if (start < 0) start = 0;
            if (end > s.Length) end = s.Length;
            int w = 0;
            int i = start;
            while (i < end)
            {
                int cp = CodepointAt(s, i);
                w += CodepointDisplayWidth(cp);
                i += CodepointSize(s, i);
            }
            return w;
        }

        /// <summary>Char index whose column ≥ targetColumn (clamped to s.Length).</summary>
        public static int IndexAtColumn(string s, int targetColumn)
        {
            if (targetColumn <= 0) return 0;
            int col = 0;
            int i = 0;
            while (i < s.Length)
            {
                int cp = CodepointAt(s, i);
                int w = CodepointDisplayWidth(cp);
                if (col + w > targetColumn) return i;
                col += w;
                i += CodepointSize(s, i);
            }
            return s.Length;
        }

        /// <summary>Determines if a codepoint is part of a word (for word-jump).</summary>
        public static bool IsWordCodepoint(int cp)
        {
            if (cp == '_') return true;
            if (cp >= '0' && cp <= '9') return true;
            if (cp >= 'A' && cp <= 'Z') return true;
            if (cp >= 'a' && cp <= 'z') return true;
            // Non-ASCII letters: treat anything above 0x7F that isn't punctuation/symbol space as word.
            if (cp > 0x7F)
            {
                char c = (char)System.Math.Min(cp, 0xFFFF);
                return !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c);
            }
            return false;
        }

        // East Asian Wide / Fullwidth ranges (simplified — covers CJK + Hangul + Fullwidth forms).
        private static bool IsEastAsianWide(int cp)
        {
            return (cp >= 0x1100 && cp <= 0x115F)
                || (cp >= 0x2E80 && cp <= 0x303E)
                || (cp >= 0x3041 && cp <= 0x33FF)
                || (cp >= 0x3400 && cp <= 0x4DBF)
                || (cp >= 0x4E00 && cp <= 0x9FFF)
                || (cp >= 0xA000 && cp <= 0xA4CF)
                || (cp >= 0xAC00 && cp <= 0xD7A3)
                || (cp >= 0xF900 && cp <= 0xFAFF)
                || (cp >= 0xFE30 && cp <= 0xFE4F)
                || (cp >= 0xFF00 && cp <= 0xFF60)
                || (cp >= 0xFFE0 && cp <= 0xFFE6)
                || (cp >= 0x20000 && cp <= 0x2FFFD)
                || (cp >= 0x30000 && cp <= 0x3FFFD);
        }
    }
}
