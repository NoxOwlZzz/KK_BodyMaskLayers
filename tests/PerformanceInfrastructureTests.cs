using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class PerformanceInfrastructureTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("Performance counters are disabled by default", CountersDisabledByDefault),
                new TestCase("Performance snapshots produce numeric deltas", CounterSnapshotsAndDelta),
                new TestCase("Shared performance metrics own all runtime counters", SharedMetricsOwnRuntimeCounters),
                new TestCase("Bounded LRU honors bytes and recency", BoundedLruHonorsBytesAndRecency),
                new TestCase("Bounded LRU rejects oversized entries", BoundedLruRejectsOversizedEntries),
                new TestCase("Negative cache invalidates by generation", NegativeCacheInvalidatesByGeneration),
                new TestCase("Composition scheduler coalesces per frame", SchedulerCoalescesPerFrame),
                new TestCase("Runtime tracker compares effective state planes", RuntimeTrackerComparesEffectivePlanes),
                new TestCase("Warm compiled state switches reuse pure buffers", WarmCompiledStateSwitches),
                new TestCase("Synthetic scheduler idle remains stable for 600 ticks", IdleContractForSixHundredTicks)
            };
        }

        private static void CountersDisabledByDefault()
        {
            PerformanceCounters counters = new PerformanceCounters();
            Check.False(counters.Enabled, "Diagnostics counters must default to disabled.");

            for (int i = 0; i < (int)PerformanceCounter.CounterCount; i++)
            {
                counters.Increment((PerformanceCounter)i);
            }

            counters.Add(PerformanceCounter.FileReads, 7L);
            counters.SetCacheMemoryBytes(512L);
            counters.AdjustCacheMemoryBytes(64L);

            PerformanceSnapshot snapshot = counters.Capture();
            for (int i = 0; i < (int)PerformanceCounter.CounterCount; i++)
            {
                Check.Equal(0L, snapshot[(PerformanceCounter)i], "A disabled counter must remain zero.");
            }
        }

        private static void CounterSnapshotsAndDelta()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            counters.Increment(PerformanceCounter.LegacyIndexScans);
            counters.Add(PerformanceCounter.FileReads, 2L);
            counters.SetCacheMemoryBytes(100L);
            PerformanceSnapshot baseline = counters.Capture();

            counters.Add(PerformanceCounter.FileReads, 3L);
            counters.Increment(PerformanceCounter.Compositions);
            counters.Increment(PerformanceCounter.StateFastPathHits);
            counters.AdjustCacheMemoryBytes(-25L);

            PerformanceSnapshot current = counters.Capture();
            PerformanceSnapshot delta = current.Delta(baseline);
            Check.Equal(1L, baseline.LegacyIndexScans, "The baseline must remain immutable.");
            Check.Equal(2L, baseline.FileReads, "The baseline must preserve the captured value.");
            Check.Equal(3L, delta.FileReads, "Delta must contain only later file reads.");
            Check.Equal(1L, delta.Compositions, "Delta must include compositions.");
            Check.Equal(1L, delta.StateFastPathHits, "Delta must include fast-path hits.");
            Check.Equal(-25L, delta.CacheMemoryBytes, "Gauge deltas may be negative.");
            Check.Equal(0L, delta.LegacyIndexScans, "Unchanged counters must have a zero delta.");
        }

        private static void SharedMetricsOwnRuntimeCounters()
        {
            BodyMaskPerformanceMetrics.SetEnabled(true);
            try
            {
                PerformanceSnapshot baseline = BodyMaskPerformanceMetrics.Capture();
                BodyMaskPerformanceMetrics.RecordDirtyRequest(true);
                BodyMaskPerformanceMetrics.RecordComposition();
                BodyMaskPerformanceMetrics.RecordTextureUpload();
                BodyMaskPerformanceMetrics.RecordOutputUnchangedSkip();
                BodyMaskPerformanceMetrics.RecordFallbackPoll();
                BodyMaskPerformanceMetrics.RecordStateFastPathHit();
                BodyMaskPerformanceMetrics.RecordPreviewCreation();
                BodyMaskPerformanceMetrics.RecordNewTextureAllocation();
                BodyMaskPerformanceMetrics.RecordPngDecode(
                    new SemanticMask(
                        1,
                        1,
                        new MaskPixelRule[] { MaskPixelRule.HideWhenFull }),
                    true,
                    true);

                PerformanceSnapshot delta =
                    BodyMaskPerformanceMetrics.Capture().Delta(baseline);
                Check.Equal(1L, delta.DirtyRequests,
                    "The shared owner must record dirty requests.");
                Check.Equal(1L, delta.DirtyRequestsCoalesced,
                    "The shared owner must record coalesced requests.");
                Check.Equal(1L, delta.Compositions,
                    "The shared owner must record compositions.");
                Check.Equal(1L, delta.TextureUploads,
                    "The shared owner must record texture uploads.");
                Check.Equal(1L, delta.OutputUnchangedSkips,
                    "The shared owner must record unchanged-output skips.");
                Check.Equal(1L, delta.FallbackPolls,
                    "The shared owner must record fallback polls.");
                Check.Equal(1L, delta.StateFastPathHits,
                    "The shared owner must record state fast paths.");
                Check.Equal(1L, delta.PreviewCreations,
                    "The shared owner must record preview creation.");
                Check.Equal(1L, delta.NewTexture2DAllocations,
                    "The shared owner must record texture allocations.");
                Check.Equal(1L, delta.PngDecodes,
                    "The shared owner must record PNG decoding.");
                Check.Equal(1L, delta.PaletteDecodes,
                    "The shared owner must record palette decoding.");
                Check.Equal(1L, delta.GradientAnalyses,
                    "The shared owner must record gradient analysis.");
                Check.Equal(1L, delta.CategoricalDecodes,
                    "Binary semantic masks must record categorical decoding.");
                Check.Equal(1L, delta.BinaryMasksCompiled,
                    "Binary semantic masks must record binary compilation.");

                string summary = BodyMaskPerformanceMetrics.BuildSummary();
                Check.True(
                    summary.StartsWith(
                        "Legacy performance counters enabled=True",
                        StringComparison.Ordinal),
                    "The diagnostic summary prefix must remain compatible.");
                Check.True(summary.IndexOf("; DirtyRequests=", StringComparison.Ordinal) >= 0,
                    "The diagnostic summary must retain named counter fields.");
            }
            finally
            {
                BodyMaskPerformanceMetrics.SetEnabled(false);
            }
        }

        private static void BoundedLruHonorsBytesAndRecency()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            BoundedLruCache<string, string> cache =
                new BoundedLruCache<string, string>(6L, StringComparer.Ordinal, counters);

            Check.True(cache.Put("a", "A", 3L), "The first entry must fit.");
            Check.True(cache.Put("b", "B", 3L), "The second entry must fit.");

            string value;
            Check.True(cache.TryGetValue("a", out value), "Reading a cached value must succeed.");
            Check.Equal("A", value, "The cached value must be returned.");
            Check.True(cache.Put("c", "C", 3L), "A new entry must fit after one eviction.");

            Check.False(cache.TryGetValue("b", out value), "The least-recently-used entry must be evicted.");
            Check.True(cache.TryGetValue("a", out value), "The touched entry must remain cached.");
            Check.True(cache.TryGetValue("c", out value), "The newest entry must remain cached.");
            Check.Equal(2, cache.Count, "The cache must retain only entries within the byte limit.");
            Check.Equal(6L, cache.CurrentBytes, "The exact retained byte count must be tracked.");
            Check.Equal(1L, cache.EvictionCount, "One capacity eviction must be counted.");

            PerformanceSnapshot snapshot = counters.Capture();
            Check.Equal(1L, snapshot.CacheEvictions, "Cache evictions must feed diagnostics.");
            Check.Equal(6L, snapshot.CacheMemoryBytes, "Cache memory must feed the byte gauge.");
        }

        private static void BoundedLruRejectsOversizedEntries()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            BoundedLruCache<string, byte[]> cache =
                new BoundedLruCache<string, byte[]>(4L, StringComparer.Ordinal, counters);

            Check.True(cache.Put("same", new byte[4], 4L), "An entry equal to the limit must fit.");
            Check.False(cache.Put("same", new byte[5], 5L), "An oversized replacement must not be cached.");
            Check.Equal(0, cache.Count, "A stale value must not survive an oversized replacement.");
            Check.Equal(0L, cache.CurrentBytes, "Rejected data must not count toward memory.");
            Check.False(cache.Put("large", new byte[8], 8L), "A new oversized entry must be rejected.");
            Check.Equal(0L, cache.EvictionCount, "Oversized rejection is not a capacity eviction.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { cache.Put("zero", new byte[0], 0L); },
                "Entries require a positive measured byte size.");
            Check.Equal(0L, counters.Capture().CacheMemoryBytes, "The memory gauge must return to zero.");
        }

        private static void NegativeCacheInvalidatesByGeneration()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            GenerationNegativeCache<string> cache =
                new GenerationNegativeCache<string>(2, StringComparer.Ordinal, counters);

            cache.Add("a", 10L);
            cache.Add("b", 10L);
            Check.True(cache.Contains("a", 10L), "An entry must be found in its generation.");
            cache.Add("c", 10L);
            Check.False(cache.Contains("b", 10L), "The untouched oldest entry must be bounded out.");
            Check.True(cache.Contains("a", 10L), "A recently used negative entry must remain.");
            Check.Equal(1L, cache.EvictionCount, "Negative-cache capacity evictions must be counted.");

            cache.BeginGeneration(11L);
            Check.Equal(0, cache.Count, "Changing generation must invalidate all negative results.");
            Check.False(cache.Contains("a", 11L), "An old negative result must not leak into a new generation.");
            Check.Equal(11L, cache.Generation, "The active generation must be exposed numerically.");
            Check.Equal(2L, counters.Capture().NegativeCacheHits, "Only actual negative hits must be counted.");
        }

        private static void SchedulerCoalescesPerFrame()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            CompositionScheduler scheduler = new CompositionScheduler(counters);

            Check.True(
                scheduler.RequestDirty(CompositionDirtyReason.ItemChanged),
                "The first request must transition to dirty.");
            Check.False(
                scheduler.RequestDirty(CompositionDirtyReason.NativeLayerChanged),
                "A second pending request must be coalesced.");

            CompositionDirtyReason reasons;
            Check.True(scheduler.TryBeginComposition(50, out reasons), "A dirty scheduler must compose.");
            Check.True(
                (reasons & CompositionDirtyReason.ItemChanged) != 0,
                "Coalescing must retain the item-change reason.");
            Check.True(
                (reasons & CompositionDirtyReason.NativeLayerChanged) != 0,
                "Coalescing must retain the native-layer reason.");
            Check.False(scheduler.TryBeginComposition(50, out reasons), "Only one composition is allowed per frame.");

            scheduler.RequestDirty(CompositionDirtyReason.BaseMaskChanged);
            Check.False(
                scheduler.TryBeginComposition(50, out reasons),
                "A request arriving later in the same frame must remain pending.");
            Check.True(scheduler.IsDirty, "The deferred request must remain dirty.");
            Check.True(scheduler.TryBeginComposition(51, out reasons), "The deferred request must run next frame.");
            Check.Equal(2L, scheduler.CompositionCount, "Exactly two frames must compose.");
            Check.Equal(3L, scheduler.RequestCount, "Every non-empty request must be counted.");
            Check.Equal(1L, scheduler.CoalescedRequestCount, "Only the duplicate pending request is coalesced.");

            PerformanceSnapshot snapshot = counters.Capture();
            Check.Equal(3L, snapshot.DirtyRequests, "Dirty request diagnostics must match the scheduler.");
            Check.Equal(1L, snapshot.DirtyRequestsCoalesced, "Coalesced diagnostics must match the scheduler.");
            Check.Equal(2L, snapshot.Compositions, "Composition diagnostics must match successful scheduling.");
        }

        private static void RuntimeTrackerComparesEffectivePlanes()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            CompositionScheduler scheduler = new CompositionScheduler(counters);
            RuntimeChangeTracker tracker = new RuntimeChangeTracker(2, scheduler, counters);

            Check.Equal(
                RuntimeStatePlaneResult.Initialized,
                tracker.ObserveStatePlane(0, 1, 100L),
                "The first observed plane must initialize the slot.");

            CompositionDirtyReason reasons;
            Check.True(scheduler.TryBeginComposition(1, out reasons), "Initialization must request one composition.");
            Check.Equal(
                RuntimeStatePlaneResult.RawStateChangedSamePlane,
                tracker.ObserveStatePlane(0, 2, 100L),
                "Raw states selecting the same effective plane must use the fast path.");
            Check.False(scheduler.IsDirty, "The same effective contribution must not invalidate composition.");
            Check.Equal(
                RuntimeStatePlaneResult.Unchanged,
                tracker.ObserveStatePlane(0, 2, 100L),
                "An identical notification must be a no-op.");
            Check.False(scheduler.IsDirty, "An identical state-plane must stay clean.");
            Check.Equal(
                RuntimeStatePlaneResult.EffectivePlaneChanged,
                tracker.ObserveStatePlane(0, 2, 101L),
                "A different plane must invalidate composition.");
            Check.True(scheduler.IsDirty, "A changed effective contribution must be dirty.");
            Check.Equal(1L, counters.Capture().StateFastPathHits, "The raw-state same-plane shortcut must be counted.");

            int rawState;
            long planeId;
            Check.True(tracker.TryGetStatePlane(0, out rawState, out planeId), "Tracked state must be queryable.");
            Check.Equal(2, rawState, "The latest raw state must be retained.");
            Check.Equal(101L, planeId, "The latest effective plane must be retained.");
        }

        private static void IdleContractForSixHundredTicks()
        {
            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            BoundedLruCache<string, byte[]> cache =
                new BoundedLruCache<string, byte[]>(128L, StringComparer.Ordinal, counters);
            CompositionScheduler scheduler = new CompositionScheduler(counters);

            cache.Put("warm", new byte[64], 64L);
            counters.Increment(PerformanceCounter.FileReads);
            counters.Increment(PerformanceCounter.PngDecodes);
            counters.Increment(PerformanceCounter.TextureUploads);
            scheduler.RequestDirty(CompositionDirtyReason.CharacterLoaded);
            CompositionDirtyReason reasons;
            Check.True(scheduler.TryBeginComposition(0, out reasons), "Warm-up must consume the initial dirty state.");

            PerformanceSnapshot warm = counters.Capture();
            long warmCacheBytes = cache.CurrentBytes;
            for (int frame = 1; frame <= 600; frame++)
            {
                Check.False(
                    scheduler.TryBeginComposition(frame, out reasons),
                    "An idle tick must not schedule a composition.");
            }

            PerformanceSnapshot idleDelta = counters.Capture().Delta(warm);
            Check.Equal(0L, idleDelta.Compositions, "Idle must add no compositions.");
            Check.Equal(0L, idleDelta.TextureUploads, "Idle must add no texture uploads.");
            Check.Equal(0L, idleDelta.FileReads, "Idle must add no file reads.");
            Check.Equal(0L, idleDelta.ZipOpens, "Idle must add no zip opens.");
            Check.Equal(0L, idleDelta.AssetBundleOpens, "Idle must add no AssetBundle opens.");
            Check.Equal(0L, idleDelta.SourceLoads, "Idle must add no source loads.");
            Check.Equal(0L, idleDelta.ProviderResolutions, "Idle must add no provider resolutions.");
            Check.Equal(0L, idleDelta.LegacyIndexScans, "Idle must add no legacy index scans.");
            Check.Equal(0L, idleDelta.ManifestParses, "Idle must add no manifest parses.");
            Check.Equal(0L, idleDelta.GlobalSideloaderScans, "Idle must add no global Sideloader scans.");
            Check.Equal(0L, idleDelta.AssetBundleLoads, "Idle must add no AssetBundle loads.");
            Check.Equal(0L, idleDelta.PngDecodes, "Idle must add no PNG decodes.");
            Check.Equal(0L, idleDelta.PaletteDecodes, "Idle must add no palette decodes.");
            Check.Equal(0L, idleDelta.GradientAnalyses, "Idle must add no gradient analyses.");
            Check.Equal(0L, idleDelta.CategoricalDecodes, "Idle must add no categorical decodes.");
            Check.Equal(0L, idleDelta.ContinuousGradientDecodes, "Idle must add no continuous decodes.");
            Check.Equal(0L, idleDelta.BinaryMasksCompiled, "Idle must compile no binary masks.");
            Check.Equal(0L, idleDelta.ContinuousMasksCompiled, "Idle must compile no continuous masks.");
            Check.Equal(0L, idleDelta.Sha256Calculations, "Idle must add no SHA-256 calculations.");
            Check.Equal(0L, idleDelta.NewTexture2DAllocations, "Idle must add no Texture2D allocations.");
            Check.Equal(0L, idleDelta.PreviewCreations, "Idle must add no preview creations.");
            Check.Equal(0L, idleDelta.CacheMemoryBytes, "Idle must keep the cache-memory gauge stable.");
            Check.Equal(warmCacheBytes, cache.CurrentBytes, "Idle must retain a stable bounded-cache footprint.");
            Check.Equal(1L, scheduler.CompositionCount, "Only warm-up may compose.");
        }

        private static void WarmCompiledStateSwitches()
        {
            SemanticMask mask = SemanticMask.FromStateCoverage(
                2,
                1,
                new byte[] { 10, 20 },
                new byte[] { 30, 40 },
                new byte[] { 50, 60 });
            MaskComposeWorkspace workspace = new MaskComposeWorkspace();
            byte[] output = new byte[4];
            MaskComposer.Accumulate(mask, (byte)0, 4, 1, output, workspace);
            int[] sourceX = workspace.SourceX;
            int[] sourceXNext = workspace.SourceXNext;
            int[] sourceXFraction = workspace.SourceXFraction;

            PerformanceCounters counters = new PerformanceCounters();
            counters.Enabled = true;
            PerformanceSnapshot warm = counters.Capture();
            for (byte state = 1; state <= 3; state++)
            {
                Array.Clear(output, 0, output.Length);
                MaskComposer.Accumulate(mask, state, 4, 1, output, workspace);
            }

            Check.Same(sourceX, workspace.SourceX, "Warm state changes must reuse the x-coordinate buffer.");
            Check.Same(sourceXNext, workspace.SourceXNext, "Warm state changes must reuse the next-x buffer.");
            Check.Same(sourceXFraction, workspace.SourceXFraction, "Warm state changes must reuse the fraction buffer.");
            PerformanceSnapshot delta = counters.Capture().Delta(warm);
            Check.Equal(0L, delta.FileReads, "Pure warm state selection must perform no file IO.");
            Check.Equal(0L, delta.ZipOpens, "Pure warm state selection must open no zip.");
            Check.Equal(0L, delta.AssetBundleOpens, "Pure warm state selection must open no AssetBundle.");
            Check.Equal(0L, delta.PngDecodes, "Pure warm state selection must decode no PNG.");
            Check.Equal(0L, delta.PaletteDecodes, "Pure warm state selection must decode no palette.");
            Check.Equal(0L, delta.GradientAnalyses, "Pure warm state selection must analyze no gradient.");
            Check.Equal(0L, delta.Sha256Calculations, "Pure warm state selection must calculate no SHA-256.");
            Check.Equal(0L, delta.NewTexture2DAllocations, "Pure warm state selection must allocate no Texture2D.");
        }
    }
}
