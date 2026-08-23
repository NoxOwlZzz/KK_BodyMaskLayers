using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal static class BodyMaskPerformanceMetrics
    {
        private static readonly PerformanceCounters Store = new PerformanceCounters();

        public static PerformanceCounters Counters
        {
            get { return Store; }
        }

        public static void SetEnabled(bool enabled)
        {
            Store.Enabled = enabled;
        }

        public static PerformanceSnapshot Capture()
        {
            return Store.Capture();
        }

        public static string BuildSummary()
        {
            PerformanceSnapshot snapshot = Store.Capture();
            StringBuilder builder = new StringBuilder(512);
            builder.Append("Legacy performance counters enabled=").Append(Store.Enabled);
            for (int value = 0; value < (int)PerformanceCounter.CounterCount; value++)
            {
                PerformanceCounter counter = (PerformanceCounter)value;
                builder.Append("; ").Append(counter).Append('=').Append(snapshot[counter]);
            }

            return builder.ToString();
        }

        public static void Increment(PerformanceCounter counter)
        {
            Store.Increment(counter);
        }

        public static void Add(PerformanceCounter counter, long amount)
        {
            Store.Add(counter, amount);
        }

        public static void RecordDirtyRequest(bool coalesced)
        {
            Store.Increment(PerformanceCounter.DirtyRequests);
            if (coalesced)
            {
                Store.Increment(PerformanceCounter.DirtyRequestsCoalesced);
            }
        }

        public static void RecordComposition()
        {
            Store.Increment(PerformanceCounter.Compositions);
        }

        public static void RecordTextureUpload()
        {
            Store.Increment(PerformanceCounter.TextureUploads);
        }

        public static void RecordOutputUnchangedSkip()
        {
            Store.Increment(PerformanceCounter.OutputUnchangedSkips);
        }

        public static void RecordFallbackPoll()
        {
            Store.Increment(PerformanceCounter.FallbackPolls);
        }

        public static void RecordStateFastPathHit()
        {
            Store.Increment(PerformanceCounter.StateFastPathHits);
        }

        public static void RecordPreviewCreation()
        {
            Store.Increment(PerformanceCounter.PreviewCreations);
        }

        public static void RecordNewTextureAllocation()
        {
            Store.Increment(PerformanceCounter.NewTexture2DAllocations);
        }

        public static void RecordPngDecode(
            SemanticMask semanticMask,
            bool paletteDecode,
            bool gradientAnalysis)
        {
            Store.Increment(PerformanceCounter.PngDecodes);
            if (paletteDecode)
            {
                Store.Increment(PerformanceCounter.PaletteDecodes);
            }

            if (gradientAnalysis)
            {
                Store.Increment(PerformanceCounter.GradientAnalyses);
            }

            if (semanticMask == null)
            {
                return;
            }

            Store.Increment(
                semanticMask.IsBinary
                    ? PerformanceCounter.CategoricalDecodes
                    : PerformanceCounter.ContinuousGradientDecodes);
            Store.Increment(
                semanticMask.IsBinary
                    ? PerformanceCounter.BinaryMasksCompiled
                    : PerformanceCounter.ContinuousMasksCompiled);
        }
    }
}
