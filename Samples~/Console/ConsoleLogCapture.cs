using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
        public readonly int Repeat;

        public ConsoleLogEntry(DateTime time, ConsoleLogKind kind, string message, string stackTrace)
            : this(time, kind, message, stackTrace, 1) { }

        public ConsoleLogEntry(DateTime time, ConsoleLogKind kind, string message, string stackTrace, int repeat)
        {
            Time = time;
            Kind = kind;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Repeat = repeat < 1 ? 1 : repeat;
        }
    }

    /// <summary>
    /// Captures Unity log messages from <c>Application.logMessageReceivedThreaded</c>
    /// into a bounded ring buffer. The producer side (background threads) writes
    /// into a capped <see cref="ConcurrentQueue{T}"/>; the consumer side (main
    /// thread) drains a bounded number of entries per frame via
    /// <see cref="DrainPending(int)"/>.
    /// </summary>
    public sealed class ConsoleLogCapture
    {
        // Hard cap on the inter-thread queue. Without this, a stalled main thread
        // would let logMessageReceivedThreaded grow the queue unbounded and OOM.
        private const int PendingHardCap = 8192;

        /// <summary>Default max entries drained per frame. Bounds the worst-case
        /// per-frame CPU cost so a log burst cannot stall the renderer.</summary>
        public const int DefaultDrainBudget = 1024;

        private readonly ConcurrentQueue<ConsoleLogEntry> _pending = new ConcurrentQueue<ConsoleLogEntry>();
        private int _pendingApprox;       // Interlocked; >= true queue length
        private int _droppedSinceFlush;   // Interlocked; surfaced as a single summary entry on drain

        // Ring buffer — O(1) append, O(1) eviction. _head is the slot holding the
        // oldest retained entry; valid logical indices are [0, _count).
        private readonly ConsoleLogEntry[] _ring;
        private int _head;
        private int _count;
        private readonly RingView _view;

        private int _generation;
        private long _totalEvicted;

        // Incremental kind counters. Updated on every push / evict so the renderer
        // header does not re-tally the entire ring each frame.
        private int _logCount, _warnCount, _errCount, _excCount, _assertCount;

        public event Action Changed;

        /// <summary>Total times the buffer has been modified — used as a dirty token.</summary>
        public int Generation => _generation;

        /// <summary>Live read-only view over the ring buffer (oldest first).
        /// Main-thread only — mutation and iteration happen on the same thread,
        /// so no snapshot copy is taken.</summary>
        public IReadOnlyList<ConsoleLogEntry> Entries => _view;

        public int Capacity => _ring.Length;

        /// <summary>Monotonic count of entries evicted from the ring since boot.
        /// Renderers can diff this between frames to translate positional indices
        /// across evictions without re-scanning the whole ring.</summary>
        public long TotalEvicted => Interlocked.Read(ref _totalEvicted);

        public int LogCount => _logCount;
        public int WarningCount => _warnCount;
        public int ErrorAndAssertCount => _errCount + _assertCount;
        public int ExceptionCount => _excCount;

        public ConsoleLogCapture(int capacity)
        {
            int cap = Mathf.Max(16, capacity);
            _ring = new ConsoleLogEntry[cap];
            _view = new RingView(this);
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
            Interlocked.Exchange(ref _pendingApprox, 0);
            Interlocked.Exchange(ref _droppedSinceFlush, 0);
            // Account the clear as eviction of all current entries so renderers
            // that diff TotalEvicted across frames drop their stale positional
            // indices instead of pointing into a now-empty ring.
            if (_count > 0) Interlocked.Add(ref _totalEvicted, _count);
            Array.Clear(_ring, 0, _ring.Length);
            _head = 0;
            _count = 0;
            _logCount = _warnCount = _errCount = _excCount = _assertCount = 0;
            _generation++;
            Changed?.Invoke();
        }

        public void Append(ConsoleLogKind kind, string message, string stackTrace)
        {
            EnqueueOrDrop(new ConsoleLogEntry(DateTime.Now, kind, message, stackTrace, 1));
        }

        /// <summary>Backward-compatible drain using <see cref="DefaultDrainBudget"/>.</summary>
        public bool DrainPending() => DrainPending(DefaultDrainBudget);

        /// <summary>Drain up to <paramref name="maxEntries"/> pending entries into
        /// the ring buffer this frame. Returns true if anything changed.</summary>
        public bool DrainPending(int maxEntries)
        {
            if (maxEntries < 1) maxEntries = 1;
            bool changed = false;
            int processed = 0;

            while (processed < maxEntries && _pending.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _pendingApprox);
                processed++;

                // Coalesce identical consecutive messages: bump the last slot's
                // Repeat counter instead of appending. Preserve the FIRST
                // occurrence's Time and StackTrace — first stack is the most
                // diagnostically useful one; latest timestamp can be inferred
                // from the live Repeat counter.
                if (_count > 0)
                {
                    int lastSlot = (_head + _count - 1) % _ring.Length;
                    var last = _ring[lastSlot];
                    if (last.Kind == entry.Kind && last.Message == entry.Message)
                    {
                        _ring[lastSlot] = new ConsoleLogEntry(
                            last.Time, last.Kind, last.Message,
                            last.StackTrace, last.Repeat + 1);
                        changed = true;
                        continue;
                    }
                }

                Push(entry);
                changed = true;
            }

            // Surface dropped-from-queue logs as a single synthetic warning so
            // the user sees the console was overwhelmed instead of silently
            // losing entries.
            int dropped = Interlocked.Exchange(ref _droppedSinceFlush, 0);
            if (dropped > 0)
            {
                Push(new ConsoleLogEntry(
                    DateTime.Now, ConsoleLogKind.Warning,
                    "[RatatuiConsole] " + dropped + " log(s) dropped (capture overload).",
                    string.Empty, 1));
                changed = true;
            }

            if (changed)
            {
                _generation++;
                Changed?.Invoke();
            }
            return changed;
        }

        private void Push(ConsoleLogEntry entry)
        {
            if (_count >= _ring.Length)
            {
                var old = _ring[_head];
                DecrementKindCounter(old.Kind);
                _head = (_head + 1) % _ring.Length;
                _count--;
                Interlocked.Increment(ref _totalEvicted);
            }
            int slot = (_head + _count) % _ring.Length;
            _ring[slot] = entry;
            _count++;
            IncrementKindCounter(entry.Kind);
        }

        private void EnqueueOrDrop(ConsoleLogEntry entry)
        {
            // Cap is approximate under producer contention: multiple threads may
            // observe a transient count > PendingHardCap before any rolls back,
            // causing a few extra conservative drops. Direction is safe (never
            // under-counts to unbounded growth).
            int cur = Interlocked.Increment(ref _pendingApprox);
            if (cur > PendingHardCap)
            {
                Interlocked.Decrement(ref _pendingApprox);
                Interlocked.Increment(ref _droppedSinceFlush);
                return;
            }
            _pending.Enqueue(entry);
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            // logMessageReceivedThreaded fires on background threads; any exception
            // here would re-enter via Unity's pipeline and crash the worker.
            try
            {
                var kind = MapLogType(type);
                EnqueueOrDrop(new ConsoleLogEntry(
                    DateTime.Now, kind, condition, stackTrace, 1));
            }
            catch { /* swallow — never throw out of the log handler */ }
        }

        private void IncrementKindCounter(ConsoleLogKind kind)
        {
            switch (kind)
            {
                case ConsoleLogKind.Log: _logCount++; break;
                case ConsoleLogKind.Warning: _warnCount++; break;
                case ConsoleLogKind.Error: _errCount++; break;
                case ConsoleLogKind.Exception: _excCount++; break;
                case ConsoleLogKind.Assert: _assertCount++; break;
            }
        }

        private void DecrementKindCounter(ConsoleLogKind kind)
        {
            switch (kind)
            {
                case ConsoleLogKind.Log: _logCount--; break;
                case ConsoleLogKind.Warning: _warnCount--; break;
                case ConsoleLogKind.Error: _errCount--; break;
                case ConsoleLogKind.Exception: _excCount--; break;
                case ConsoleLogKind.Assert: _assertCount--; break;
            }
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

        // Virtualized read-only view over the ring buffer. Index 0 is the oldest
        // entry currently retained. Allocation-free indexing.
        private sealed class RingView : IReadOnlyList<ConsoleLogEntry>
        {
            private readonly ConsoleLogCapture _owner;
            public RingView(ConsoleLogCapture owner) { _owner = owner; }

            public ConsoleLogEntry this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_owner._count)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    int slot = (_owner._head + index) % _owner._ring.Length;
                    return _owner._ring[slot];
                }
            }

            public int Count => _owner._count;

            public IEnumerator<ConsoleLogEntry> GetEnumerator()
            {
                int n = _owner._count;
                for (int i = 0; i < n; i++) yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
