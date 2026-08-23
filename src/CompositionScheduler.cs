using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    [Flags]
    internal enum CompositionDirtyReason
    {
        None = 0,
        CharacterLoaded = 1 << 0,
        CoordinateLoaded = 1 << 1,
        ItemChanged = 1 << 2,
        ClothingStateChanged = 1 << 3,
        BaseMaskChanged = 1 << 4,
        NativeLayerChanged = 1 << 5,
        MaterialTargetChanged = 1 << 6,
        IndexChanged = 1 << 7,
        PreviewRequested = 1 << 8,
        NativeConversion = 1 << 9
    }

    internal sealed class CompositionScheduler
    {
        private readonly PerformanceCounters counters;
        private bool dirty;
        private CompositionDirtyReason pendingReasons;
        private bool hasCompositionFrame;
        private int lastCompositionFrame;
        private long requestCount;
        private long coalescedRequestCount;
        private long compositionCount;

        public CompositionScheduler()
            : this(null)
        {
        }

        public CompositionScheduler(PerformanceCounters performanceCounters)
        {
            counters = performanceCounters;
        }

        public bool IsDirty
        {
            get { return dirty; }
        }

        public CompositionDirtyReason PendingReasons
        {
            get { return pendingReasons; }
        }

        public long RequestCount
        {
            get { return requestCount; }
        }

        public long CoalescedRequestCount
        {
            get { return coalescedRequestCount; }
        }

        public long CompositionCount
        {
            get { return compositionCount; }
        }

        public bool RequestDirty(CompositionDirtyReason reason)
        {
            if (reason == CompositionDirtyReason.None)
            {
                return false;
            }

            requestCount++;
            if (counters != null)
            {
                counters.Increment(PerformanceCounter.DirtyRequests);
            }

            bool newlyDirty = !dirty;
            if (!newlyDirty)
            {
                coalescedRequestCount++;
                if (counters != null)
                {
                    counters.Increment(PerformanceCounter.DirtyRequestsCoalesced);
                }
            }

            pendingReasons |= reason;
            dirty = true;
            return newlyDirty;
        }

        public bool TryBeginComposition(int frameId, out CompositionDirtyReason reasons)
        {
            if (!dirty || (hasCompositionFrame && lastCompositionFrame == frameId))
            {
                reasons = CompositionDirtyReason.None;
                return false;
            }

            reasons = pendingReasons;
            pendingReasons = CompositionDirtyReason.None;
            dirty = false;
            hasCompositionFrame = true;
            lastCompositionFrame = frameId;
            compositionCount++;

            if (counters != null)
            {
                counters.Increment(PerformanceCounter.Compositions);
            }

            return true;
        }

        public void RestoreDirty(CompositionDirtyReason reasons)
        {
            if (reasons == CompositionDirtyReason.None)
            {
                return;
            }

            pendingReasons |= reasons;
            dirty = true;
        }

        public void Reset()
        {
            dirty = false;
            pendingReasons = CompositionDirtyReason.None;
            hasCompositionFrame = false;
            lastCompositionFrame = 0;
        }
    }

    internal enum RuntimeStatePlaneResult
    {
        Initialized = 0,
        Unchanged = 1,
        RawStateChangedSamePlane = 2,
        EffectivePlaneChanged = 3
    }

    internal sealed class RuntimeChangeTracker
    {
        private readonly CompositionScheduler scheduler;
        private readonly PerformanceCounters counters;
        private readonly bool[] initialized;
        private readonly int[] rawStates;
        private readonly long[] effectivePlaneIds;

        public RuntimeChangeTracker(int slotCount, CompositionScheduler compositionScheduler)
            : this(slotCount, compositionScheduler, null)
        {
        }

        public RuntimeChangeTracker(
            int slotCount,
            CompositionScheduler compositionScheduler,
            PerformanceCounters performanceCounters)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException("slotCount");
            }

            if (compositionScheduler == null)
            {
                throw new ArgumentNullException("compositionScheduler");
            }

            scheduler = compositionScheduler;
            counters = performanceCounters;
            initialized = new bool[slotCount];
            rawStates = new int[slotCount];
            effectivePlaneIds = new long[slotCount];
        }

        public int SlotCount
        {
            get { return initialized.Length; }
        }

        public RuntimeStatePlaneResult ObserveStatePlane(int slot, int rawState, long effectivePlaneId)
        {
            ValidateSlot(slot);

            if (!initialized[slot])
            {
                initialized[slot] = true;
                rawStates[slot] = rawState;
                effectivePlaneIds[slot] = effectivePlaneId;
                scheduler.RequestDirty(CompositionDirtyReason.ClothingStateChanged);
                return RuntimeStatePlaneResult.Initialized;
            }

            int previousRawState = rawStates[slot];
            long previousPlaneId = effectivePlaneIds[slot];
            rawStates[slot] = rawState;
            effectivePlaneIds[slot] = effectivePlaneId;

            if (previousPlaneId == effectivePlaneId)
            {
                if (previousRawState != rawState)
                {
                    if (counters != null)
                    {
                        counters.Increment(PerformanceCounter.StateFastPathHits);
                    }

                    return RuntimeStatePlaneResult.RawStateChangedSamePlane;
                }

                return RuntimeStatePlaneResult.Unchanged;
            }

            scheduler.RequestDirty(CompositionDirtyReason.ClothingStateChanged);
            return RuntimeStatePlaneResult.EffectivePlaneChanged;
        }

        public bool TryGetStatePlane(int slot, out int rawState, out long effectivePlaneId)
        {
            ValidateSlot(slot);
            if (!initialized[slot])
            {
                rawState = 0;
                effectivePlaneId = 0L;
                return false;
            }

            rawState = rawStates[slot];
            effectivePlaneId = effectivePlaneIds[slot];
            return true;
        }

        public void ResetSlot(int slot)
        {
            ValidateSlot(slot);
            initialized[slot] = false;
            rawStates[slot] = 0;
            effectivePlaneIds[slot] = 0L;
        }

        public void Reset()
        {
            Array.Clear(initialized, 0, initialized.Length);
            Array.Clear(rawStates, 0, rawStates.Length);
            Array.Clear(effectivePlaneIds, 0, effectivePlaneIds.Length);
        }

        private void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= initialized.Length)
            {
                throw new ArgumentOutOfRangeException("slot");
            }
        }
    }
}
