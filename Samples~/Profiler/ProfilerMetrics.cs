using System;
using UnityEngine;
using UnityProfiler = UnityEngine.Profiling.Profiler;

namespace RatatuiUnity.Samples.Profiler
{
    /// <summary>
    /// Plain data collector for runtime performance metrics. Sample() is called
    /// every frame to accumulate raw readings; displayed values and history are
    /// emitted on a fixed display window (default 200 ms) so the UI is readable
    /// and sparklines advance at a steady cadence regardless of frame rate.
    /// </summary>
    internal sealed class ProfilerMetrics
    {
        // 60 s rolling window at one slot per DisplayWindowSeconds, so sparkline
        // normalization can reference the 1-minute max even when only the last
        // few visible bars are inside the current panel.
        public const float DisplayWindowSeconds = 0.2f;
        public const int HistorySize = 300;
        private const float GcFrequencyWindowSeconds = 1f;

        // Ring buffers (index Head is newest after each display flush).
        public readonly float[] FpsHistory = new float[HistorySize];
        public readonly float[] FrameTimeMsHistory = new float[HistorySize];
        public readonly long[] GcAllocHistory = new long[HistorySize];
        public readonly int[] BatchesHistory = new int[HistorySize];
        public readonly long[] HeapUsedHistory = new long[HistorySize];
        public int Head;

        // Latest displayed snapshot (refreshed every DisplayWindowSeconds).
        public float Fps;
        public float FrameTimeMs;
        public float FrameTimeGoalMs = 16.6f;

        public long GcAllocThisFrame;
        public long GcTotalBytes;
        public float GcFrequencyHz;

        // Editor-only stats; -1 in player builds.
        public int Batches = -1;
        public int SetPassCalls = -1;

        public long SystemRamUsedBytes;
        public long SystemRamTotalBytes;
        public long VRamUsedBytes;
        public long VRamTotalBytes;

        public bool RamUsedAvailable;
        public bool VRamUsedAvailable;

        private long _lastGcTotal;
        private bool _hasLastGc;
        private float _gcWindowTimer;
        private int _gcEventsInWindow;
        private int _gcEventsRolling;

        // Display-window accumulators.
        private float _displayTimer;
        private float _accumDt;
        private int _accumFrames;
        private long _accumGcAlloc;
        private long _accumBatches;
        private long _accumSetPass;
        private int _accumStatsFrames;
        private long _accumRamUsed;
        private int _accumRamFrames;

        public void Sample(float deltaSeconds)
        {
            float dt = Mathf.Max(0.000001f, deltaSeconds);

            // GC delta and frequency are tracked per-frame so we don't miss
            // collection events that fire between display flushes.
            long gcTotalNow = GC.GetTotalMemory(false);
            long allocThisFrame = 0;
            if (_hasLastGc)
            {
                long delta = gcTotalNow - _lastGcTotal;
                if (delta >= 0) allocThisFrame = delta;
                else _gcEventsInWindow++;
            }
            else
            {
                _hasLastGc = true;
            }
            _lastGcTotal = gcTotalNow;
            GcTotalBytes = gcTotalNow;

            _gcWindowTimer += dt;
            if (_gcWindowTimer >= GcFrequencyWindowSeconds)
            {
                _gcEventsRolling = _gcEventsInWindow;
                _gcEventsInWindow = 0;
                _gcWindowTimer = 0f;
            }
            GcFrequencyHz = _gcEventsRolling / GcFrequencyWindowSeconds;

            int batchesNow, setPassNow;
#if UNITY_EDITOR
            batchesNow = UnityEditor.UnityStats.batches;
            setPassNow = UnityEditor.UnityStats.setPassCalls;
#else
            batchesNow = -1;
            setPassNow = -1;
#endif

            SystemRamTotalBytes = (long)SystemInfo.systemMemorySize * 1024L * 1024L;
            VRamTotalBytes = (long)SystemInfo.graphicsMemorySize * 1024L * 1024L;

            long reserved = UnityProfiler.GetTotalReservedMemoryLong();
            bool ramAvail = reserved > 0;

            // Accumulate raw per-frame readings.
            _accumDt += dt;
            _accumFrames++;
            _accumGcAlloc += allocThisFrame;
            if (batchesNow >= 0)
            {
                _accumBatches += batchesNow;
                _accumSetPass += setPassNow;
                _accumStatsFrames++;
            }
            if (ramAvail)
            {
                _accumRamUsed += reserved;
                _accumRamFrames++;
            }

            _displayTimer += dt;
            if (_displayTimer < DisplayWindowSeconds) return;

            // Flush averages to display values and push to history.
            float avgDt = _accumDt / Mathf.Max(1, _accumFrames);
            Fps = 1f / Mathf.Max(0.000001f, avgDt);
            FrameTimeMs = avgDt * 1000f;
            GcAllocThisFrame = _accumGcAlloc / Mathf.Max(1, _accumFrames);

            if (_accumStatsFrames > 0)
            {
                Batches = (int)(_accumBatches / _accumStatsFrames);
                SetPassCalls = (int)(_accumSetPass / _accumStatsFrames);
            }
            else
            {
                Batches = -1;
                SetPassCalls = -1;
            }

            if (_accumRamFrames > 0)
            {
                SystemRamUsedBytes = _accumRamUsed / _accumRamFrames;
                RamUsedAvailable = true;
            }
            else
            {
                SystemRamUsedBytes = 0;
                RamUsedAvailable = false;
            }

            VRamUsedBytes = 0;
            VRamUsedAvailable = false;

            Head = (Head + 1) % HistorySize;
            FpsHistory[Head] = Fps;
            FrameTimeMsHistory[Head] = FrameTimeMs;
            GcAllocHistory[Head] = GcAllocThisFrame;
            BatchesHistory[Head] = Batches < 0 ? 0 : Batches;
            HeapUsedHistory[Head] = SystemRamUsedBytes;

            _accumDt = 0f;
            _accumFrames = 0;
            _accumGcAlloc = 0;
            _accumBatches = 0;
            _accumSetPass = 0;
            _accumStatsFrames = 0;
            _accumRamUsed = 0;
            _accumRamFrames = 0;
            _displayTimer = 0f;
        }

    }
}
