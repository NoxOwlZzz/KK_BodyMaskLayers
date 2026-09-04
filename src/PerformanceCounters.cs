using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal enum PerformanceCounter
    {
        ExternalIndexScans = 0,
        MetadataCacheHits = 1,
        MetadataCacheMisses = 2,
        NegativeCacheHits = 3,
        ProviderResolutions = 4,
        FileReads = 5,
        ZipOpens = 6,
        AssetBundleOpens = 7,
        SourceLoads = 8,
        PngDecodes = 9,
        CategoricalDecodes = 10,
        ContinuousGradientDecodes = 11,
        BinaryMasksCompiled = 12,
        ContinuousMasksCompiled = 13,
        Compositions = 14,
        DirtyRequests = 15,
        DirtyRequestsCoalesced = 16,
        TextureUploads = 17,
        PreviewCreations = 18,
        CacheEvictions = 19,
        CacheMemoryBytes = 20,
        ManifestParses = 21,
        GlobalSideloaderScans = 22,
        AssetBundleLoads = 23,
        PaletteDecodes = 24,
        GradientAnalyses = 25,
        Sha256Calculations = 26,
        NewTexture2DAllocations = 27,
        StateFastPathHits = 28,
        OutputUnchangedSkips = 29,
        FallbackPolls = 30,
        CounterCount = 31
    }

    internal sealed class PerformanceSnapshot
    {
        private readonly long[] values;

        internal PerformanceSnapshot(long[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            values = new long[(int)PerformanceCounter.CounterCount];
            int copyLength = Math.Min(values.Length, source.Length);
            Array.Copy(source, values, copyLength);
        }

        public long this[PerformanceCounter counter]
        {
            get
            {
                ValidateCounter(counter);
                return values[(int)counter];
            }
        }

        public long ExternalIndexScans { get { return this[PerformanceCounter.ExternalIndexScans]; } }
        public long MetadataCacheHits { get { return this[PerformanceCounter.MetadataCacheHits]; } }
        public long MetadataCacheMisses { get { return this[PerformanceCounter.MetadataCacheMisses]; } }
        public long NegativeCacheHits { get { return this[PerformanceCounter.NegativeCacheHits]; } }
        public long ProviderResolutions { get { return this[PerformanceCounter.ProviderResolutions]; } }
        public long FileReads { get { return this[PerformanceCounter.FileReads]; } }
        public long ZipOpens { get { return this[PerformanceCounter.ZipOpens]; } }
        public long AssetBundleOpens { get { return this[PerformanceCounter.AssetBundleOpens]; } }
        public long SourceLoads { get { return this[PerformanceCounter.SourceLoads]; } }
        public long PngDecodes { get { return this[PerformanceCounter.PngDecodes]; } }
        public long CategoricalDecodes { get { return this[PerformanceCounter.CategoricalDecodes]; } }
        public long ContinuousGradientDecodes { get { return this[PerformanceCounter.ContinuousGradientDecodes]; } }
        public long BinaryMasksCompiled { get { return this[PerformanceCounter.BinaryMasksCompiled]; } }
        public long ContinuousMasksCompiled { get { return this[PerformanceCounter.ContinuousMasksCompiled]; } }
        public long Compositions { get { return this[PerformanceCounter.Compositions]; } }
        public long DirtyRequests { get { return this[PerformanceCounter.DirtyRequests]; } }
        public long DirtyRequestsCoalesced { get { return this[PerformanceCounter.DirtyRequestsCoalesced]; } }
        public long TextureUploads { get { return this[PerformanceCounter.TextureUploads]; } }
        public long PreviewCreations { get { return this[PerformanceCounter.PreviewCreations]; } }
        public long CacheEvictions { get { return this[PerformanceCounter.CacheEvictions]; } }
        public long CacheMemoryBytes { get { return this[PerformanceCounter.CacheMemoryBytes]; } }
        public long ManifestParses { get { return this[PerformanceCounter.ManifestParses]; } }
        public long GlobalSideloaderScans { get { return this[PerformanceCounter.GlobalSideloaderScans]; } }
        public long AssetBundleLoads { get { return this[PerformanceCounter.AssetBundleLoads]; } }
        public long PaletteDecodes { get { return this[PerformanceCounter.PaletteDecodes]; } }
        public long GradientAnalyses { get { return this[PerformanceCounter.GradientAnalyses]; } }
        public long Sha256Calculations { get { return this[PerformanceCounter.Sha256Calculations]; } }
        public long NewTexture2DAllocations { get { return this[PerformanceCounter.NewTexture2DAllocations]; } }
        public long StateFastPathHits { get { return this[PerformanceCounter.StateFastPathHits]; } }
        public long OutputUnchangedSkips { get { return this[PerformanceCounter.OutputUnchangedSkips]; } }
        public long FallbackPolls { get { return this[PerformanceCounter.FallbackPolls]; } }

        public PerformanceSnapshot Delta(PerformanceSnapshot baseline)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException("baseline");
            }

            long[] delta = new long[values.Length];
            for (int i = 0; i < delta.Length; i++)
            {
                delta[i] = values[i] - baseline.values[i];
            }

            return new PerformanceSnapshot(delta);
        }

        private static void ValidateCounter(PerformanceCounter counter)
        {
            int value = (int)counter;
            if (value < 0 || value >= (int)PerformanceCounter.CounterCount)
            {
                throw new ArgumentOutOfRangeException("counter");
            }
        }
    }

    internal sealed class PerformanceCounters
    {
        private readonly long[] values = new long[(int)PerformanceCounter.CounterCount];

        public PerformanceCounters()
        {
            Enabled = false;
        }

        public bool Enabled { get; set; }

        public void Increment(PerformanceCounter counter)
        {
            if (!Enabled)
            {
                return;
            }

            AddCore(counter, 1L);
        }

        public void Add(PerformanceCounter counter, long amount)
        {
            if (!Enabled || amount == 0L)
            {
                return;
            }

            AddCore(counter, amount);
        }

        public void SetCacheMemoryBytes(long bytes)
        {
            if (!Enabled)
            {
                return;
            }

            if (bytes < 0L)
            {
                throw new ArgumentOutOfRangeException("bytes");
            }

            values[(int)PerformanceCounter.CacheMemoryBytes] = bytes;
        }

        public void AdjustCacheMemoryBytes(long deltaBytes)
        {
            if (!Enabled || deltaBytes == 0L)
            {
                return;
            }

            int index = (int)PerformanceCounter.CacheMemoryBytes;
            long next = values[index] + deltaBytes;
            values[index] = next < 0L ? 0L : next;
        }

        public PerformanceSnapshot Capture()
        {
            return new PerformanceSnapshot(values);
        }

        public void Reset()
        {
            Array.Clear(values, 0, values.Length);
        }

        private void AddCore(PerformanceCounter counter, long amount)
        {
            int index = (int)counter;
            if (index < 0 || index >= (int)PerformanceCounter.CounterCount)
            {
                throw new ArgumentOutOfRangeException("counter");
            }

            values[index] += amount;
        }
    }
}
