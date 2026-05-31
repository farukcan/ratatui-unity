using System;
using System.Collections.Generic;
using System.Text;

namespace RatatuiUnity.Samples.Console
{
    public sealed class ConsoleCommand
    {
        public readonly string Name;
        public readonly string Description;
        public readonly Action<string[]> Callback;

        public ConsoleCommand(string name, string description, Action<string[]> callback)
        {
            Name = name;
            Description = description ?? string.Empty;
            Callback = callback;
        }
    }

    /// <summary>
    /// Stores console commands and provides parsing + autocomplete matching.
    /// Command lookup is case-insensitive.
    /// </summary>
    public sealed class ConsoleCommandRegistry
    {
        private readonly Dictionary<string, ConsoleCommand> _commands =
            new Dictionary<string, ConsoleCommand>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, ConsoleCommand> Commands => _commands;

        public void Register(string name, string description, Action<string[]> callback)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Command name cannot be empty.", nameof(name));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _commands[name] = new ConsoleCommand(name, description, callback);
        }

        public bool Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _commands.Remove(name);
        }

        public bool TryGet(string name, out ConsoleCommand command)
        {
            return _commands.TryGetValue(name, out command);
        }

        /// <summary>
        /// Parse a raw input line into command name + argument tokens.
        /// Supports double-quoted strings to preserve whitespace.
        /// Returns false when the line is empty or whitespace-only.
        /// </summary>
        public static bool Parse(string raw, out string name, out string[] args)
        {
            name = null;
            args = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var tokens = Tokenize(raw);
            if (tokens.Count == 0) return false;

            name = tokens[0];
            if (tokens.Count == 1)
            {
                args = Array.Empty<string>();
            }
            else
            {
                args = new string[tokens.Count - 1];
                for (int i = 1; i < tokens.Count; i++)
                    args[i - 1] = tokens[i];
            }
            return true;
        }

        private static List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Length = 0;
                    }
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }

        /// <summary>
        /// Return autocomplete suggestions ranked by prefix match first, then substring.
        /// Caller controls maximum result count.
        /// </summary>
        public List<ConsoleCommand> Match(string query, int maxResults)
        {
            var results = new List<ConsoleCommand>();
            if (string.IsNullOrEmpty(query)) return results;

            // Token before whitespace — we only autocomplete the command name, not args.
            int firstSpace = query.IndexOf(' ');
            string prefix = firstSpace < 0 ? query : query.Substring(0, firstSpace);
            if (string.IsNullOrEmpty(prefix)) return results;

            var prefixMatches = new List<ConsoleCommand>();
            var substringMatches = new List<ConsoleCommand>();

            foreach (var cmd in _commands.Values)
            {
                if (cmd.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    prefixMatches.Add(cmd);
                else if (cmd.Name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    substringMatches.Add(cmd);
            }

            prefixMatches.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            substringMatches.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var cmd in prefixMatches)
            {
                if (results.Count >= maxResults) return results;
                results.Add(cmd);
            }
            foreach (var cmd in substringMatches)
            {
                if (results.Count >= maxResults) return results;
                results.Add(cmd);
            }
            return results;
        }

        public List<ConsoleCommand> SortedAll()
        {
            var list = new List<ConsoleCommand>(_commands.Values);
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
