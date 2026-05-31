using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    public enum ConsoleLogKind : byte
    {
        Log = 0,
        Warning = 1,
        Error = 2,
        Exception = 3,
        Assert = 4,
    }

    public readonly struct ConsoleLogEntry
    {
        public readonly DateTime Time;
        public readonly ConsoleLogKind Kind;
        public readonly string Message;
        public readonly string StackTrace;

        public ConsoleLogEntry(DateTime time, ConsoleLogKind kind, string message, string stackTrace)
        {
            Time = time;
            Kind = kind;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }
    }

    /// <summary>
    /// Captures Unity log messages from <c>Application.logMessageReceivedThreaded</c>
    /// into a bounded ring buffer. Thread-safe queue drains on the main thread
    /// via <see cref="DrainPending"/>.
    /// </summary>
    public sealed class ConsoleLogCapture
    {
        private readonly ConcurrentQueue<ConsoleLogEntry> _pending = new ConcurrentQueue<ConsoleLogEntry>();
        private readonly List<ConsoleLogEntry> _entries;
        private readonly int _capacity;
        private int _generation;

        public event Action Changed;

        /// <summary>Total times the buffer has been modified — used as a dirty token.</summary>
        public int Generation => _generation;

        /// <summary>Snapshot view of stored entries (oldest first).</summary>
        public IReadOnlyList<ConsoleLogEntry> Entries => _entries;

        public ConsoleLogCapture(int capacity)
        {
            _capacity = Mathf.Max(16, capacity);
            _entries = new List<ConsoleLogEntry>(_capacity);
        }

        public void Install()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        public void Uninstall()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
        }

        public void Clear()
        {
            // Drain pending too — otherwise threaded handlers that enqueued
            // microseconds before `clear` would silently re-appear next frame.
            while (_pending.TryDequeue(out _)) { }
            _entries.Clear();
            _generation++;
            Changed?.Invoke();
        }

        public void Append(ConsoleLogKind kind, string message, string stackTrace)
        {
            _pending.Enqueue(new ConsoleLogEntry(
                DateTime.Now, kind, message, stackTrace));
        }

        /// <summary>Drain pending entries from the producer queue into the ring buffer.</summary>
        public bool DrainPending()
        {
            bool changed = false;
            while (_pending.TryDequeue(out var entry))
            {
                if (_entries.Count >= _capacity)
                    _entries.RemoveAt(0);
                _entries.Add(entry);
                changed = true;
            }
            if (changed)
            {
                _generation++;
                Changed?.Invoke();
            }
            return changed;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            // logMessageReceivedThreaded fires on background threads; any exception
            // here would re-enter via Unity's pipeline and crash the worker.
            try
            {
                var kind = MapLogType(type);
                _pending.Enqueue(new ConsoleLogEntry(
                    DateTime.Now, kind, condition, stackTrace));
            }
            catch { /* swallow — never throw out of the log handler */ }
        }

        private static ConsoleLogKind MapLogType(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return ConsoleLogKind.Warning;
                case LogType.Error: return ConsoleLogKind.Error;
                case LogType.Exception: return ConsoleLogKind.Exception;
                case LogType.Assert: return ConsoleLogKind.Assert;
                default: return ConsoleLogKind.Log;
            }
        }
    }
}
