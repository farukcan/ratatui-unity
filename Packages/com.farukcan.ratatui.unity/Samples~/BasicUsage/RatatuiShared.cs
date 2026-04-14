using System;
using System.Linq;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Generic list with a navigable selection index, useful for list widgets.
    /// All data is generated and owned in C#; only the formatted string is passed to Rust.
    /// </summary>
    public class StatefulList<T>
    {
        public T[] Items { get; }
        public int Selected { get; private set; } = -1;

        public StatefulList(T[] items) { Items = items; }

        public void SelectFirst() { Selected = Items.Length > 0 ? 0 : -1; }

        public void Next()
        {
            if (Items.Length == 0) return;
            Selected = Selected < 0 ? 0 : (Selected + 1) % Items.Length;
        }

        public void Previous()
        {
            if (Items.Length == 0) return;
            Selected = Selected < 0 ? Items.Length - 1 : (Selected + Items.Length - 1) % Items.Length;
        }

        public void Select(int index)
        {
            if (index >= 0 && index < Items.Length)
                Selected = index;
        }

        /// <summary>Build a newline-separated string for the List widget.</summary>
        public string ToItemsString(Func<T, string> selector)
            => string.Join("\n", Items.Select(selector));
    }
}
