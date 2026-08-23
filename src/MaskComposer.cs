using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class MaskComposeWorkspace
    {
        internal int[] SourceX;
        internal int[] SourceXNext;
        internal int[] SourceXFraction;

        internal void PrepareNearest(int sourceWidth, int outputWidth)
        {
            if (SourceX == null || SourceX.Length < outputWidth)
            {
                SourceX = new int[outputWidth];
            }

            for (int x = 0; x < outputWidth; x++)
            {
                SourceX[x] = MaskResolutionConverter.SourceCoordinate(x, sourceWidth, outputWidth);
            }
        }

        internal void PrepareBilinear(int sourceWidth, int outputWidth)
        {
            if (SourceX == null || SourceX.Length < outputWidth)
            {
                SourceX = new int[outputWidth];
            }

            if (SourceXNext == null || SourceXNext.Length < outputWidth)
            {
                SourceXNext = new int[outputWidth];
            }

            if (SourceXFraction == null || SourceXFraction.Length < outputWidth)
            {
                SourceXFraction = new int[outputWidth];
            }

            for (int x = 0; x < outputWidth; x++)
            {
                MaskResolutionConverter.BilinearCoordinate(
                    x,
                    sourceWidth,
                    outputWidth,
                    out SourceX[x],
                    out SourceXNext[x],
                    out SourceXFraction[x]);
            }
        }
    }

    public static class MaskComposer
    {
        public static bool ShouldHide(MaskPixelRule rule, GarmentState state)
        {
            switch (rule)
            {
                case MaskPixelRule.HideWhenFull:
                    return state == GarmentState.Full;
                case MaskPixelRule.HideWhenNotOff:
                    return state == GarmentState.Full || state == GarmentState.Partial;
                default:
                    return false;
            }
        }

        public static bool HasPotentialContribution(
            MaskColorStatistics statistics,
            UnknownColorPolicy unknownColorPolicy,
            GarmentState state)
        {
            if (state != GarmentState.Full && state != GarmentState.Partial)
            {
                return false;
            }

            if (statistics == null)
            {
                return true;
            }

            bool conservativeUnknown =
                unknownColorPolicy == UnknownColorPolicy.HideWhenNotOff &&
                (statistics.UnknownPixels != 0 || statistics.BluePixels != 0);
            if (statistics.ContinuousPixels != 0 || statistics.LegacyStatePixels != 0)
            {
                return true;
            }

            if (state == GarmentState.Full)
            {
                return statistics.GreenPixels != 0 ||
                       statistics.BlackPixels != 0 ||
                       statistics.RedPixels != 0 ||
                       conservativeUnknown;
            }

            return statistics.BlackPixels != 0 ||
                   statistics.RedPixels != 0 ||
                   conservativeUnknown;
        }

        public static int Accumulate(
            SemanticMask layer,
            GarmentState state,
            int outputWidth,
            int outputHeight,
            byte[] hideCoverage,
            MaskComposeWorkspace workspace)
        {
            byte rawState;
            if (!TryMapState(state, out rawState))
            {
                ValidateCoverageArguments(layer, outputWidth, outputHeight, hideCoverage);
                return 0;
            }

            return Accumulate(
                layer,
                rawState,
                outputWidth,
                outputHeight,
                hideCoverage,
                workspace);
        }

        public static int Accumulate(
            SemanticMask layer,
            byte rawState,
            int outputWidth,
            int outputHeight,
            byte[] hideCoverage,
            MaskComposeWorkspace workspace)
        {
            ValidateCoverageArguments(layer, outputWidth, outputHeight, hideCoverage);
            if (rawState > 2 || !layer.Compiled.HasAnyCoverage(rawState))
            {
                return 0;
            }

            if (layer.Width == outputWidth && layer.Height == outputHeight)
            {
                return layer.IsBinary
                    ? AccumulateBinarySameSize(layer, rawState, hideCoverage)
                    : AccumulateContinuousSameSize(layer, rawState, hideCoverage);
            }

            if (workspace == null)
            {
                workspace = new MaskComposeWorkspace();
            }

            return layer.IsBinary
                ? AccumulateBinaryResampled(
                    layer,
                    rawState,
                    outputWidth,
                    outputHeight,
                    hideCoverage,
                    workspace)
                : AccumulateContinuousResampled(
                    layer,
                    rawState,
                    outputWidth,
                    outputHeight,
                    hideCoverage,
                    workspace);
        }

        public static int Accumulate(
            SemanticMask layer,
            GarmentState state,
            int outputWidth,
            int outputHeight,
            bool[] hiddenPixels)
        {
            ValidateBooleanArguments(layer, outputWidth, outputHeight, hiddenPixels);
            byte rawState;
            if (!TryMapState(state, out rawState) || !layer.Compiled.HasAnyCoverage(rawState))
            {
                return 0;
            }

            MaskComposeWorkspace workspace = null;
            if (layer.Width != outputWidth || layer.Height != outputHeight)
            {
                workspace = new MaskComposeWorkspace();
                if (layer.IsBinary)
                {
                    workspace.PrepareNearest(layer.Width, outputWidth);
                }
                else
                {
                    workspace.PrepareBilinear(layer.Width, outputWidth);
                }
            }

            int changed = 0;
            for (int y = 0; y < outputHeight; y++)
            {
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    int outputIndex = outputRow + x;
                    if (hiddenPixels[outputIndex])
                    {
                        continue;
                    }

                    byte coverage = SampleCoverage(
                        layer,
                        rawState,
                        x,
                        y,
                        outputWidth,
                        outputHeight,
                        workspace);
                    if (coverage >= 128)
                    {
                        hiddenPixels[outputIndex] = true;
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static int AccumulateBinarySameSize(
            SemanticMask layer,
            byte rawState,
            byte[] hideCoverage)
        {
            uint[] words = layer.GetBinaryStateBits(rawState);
            int changed = 0;
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                uint word = words[wordIndex];
                if (word == 0)
                {
                    continue;
                }

                int firstIndex = wordIndex << 5;
                int limit = Math.Min(firstIndex + 32, hideCoverage.Length);
                for (int index = firstIndex; index < limit; index++)
                {
                    if ((word & (1u << (index & 31))) != 0 &&
                        hideCoverage[index] != byte.MaxValue)
                    {
                        hideCoverage[index] = byte.MaxValue;
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static int AccumulateContinuousSameSize(
            SemanticMask layer,
            byte rawState,
            byte[] hideCoverage)
        {
            byte[] source = layer.GetContinuousStateCoverage(rawState);
            int changed = 0;
            for (int index = 0; index < hideCoverage.Length; index++)
            {
                byte value = source[index];
                if (value > hideCoverage[index])
                {
                    hideCoverage[index] = value;
                    changed++;
                }
            }

            return changed;
        }

        private static int AccumulateBinaryResampled(
            SemanticMask layer,
            byte rawState,
            int outputWidth,
            int outputHeight,
            byte[] hideCoverage,
            MaskComposeWorkspace workspace)
        {
            workspace.PrepareNearest(layer.Width, outputWidth);
            int changed = 0;
            for (int y = 0; y < outputHeight; y++)
            {
                int sourceY = MaskResolutionConverter.SourceCoordinate(y, layer.Height, outputHeight);
                int sourceRow = sourceY * layer.Width;
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    int outputIndex = outputRow + x;
                    if (hideCoverage[outputIndex] == byte.MaxValue)
                    {
                        continue;
                    }

                    int sourceIndex = sourceRow + workspace.SourceX[x];
                    if (layer.GetHideCoverageForRawState(sourceIndex, rawState) != 0)
                    {
                        hideCoverage[outputIndex] = byte.MaxValue;
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static int AccumulateContinuousResampled(
            SemanticMask layer,
            byte rawState,
            int outputWidth,
            int outputHeight,
            byte[] hideCoverage,
            MaskComposeWorkspace workspace)
        {
            workspace.PrepareBilinear(layer.Width, outputWidth);
            int changed = 0;
            for (int y = 0; y < outputHeight; y++)
            {
                int y0;
                int y1;
                int fy;
                MaskResolutionConverter.BilinearCoordinate(
                    y,
                    layer.Height,
                    outputHeight,
                    out y0,
                    out y1,
                    out fy);
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    byte value = BilinearSample(
                        layer,
                        rawState,
                        workspace.SourceX[x],
                        workspace.SourceXNext[x],
                        workspace.SourceXFraction[x],
                        y0,
                        y1,
                        fy);
                    int outputIndex = outputRow + x;
                    if (value > hideCoverage[outputIndex])
                    {
                        hideCoverage[outputIndex] = value;
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static byte SampleCoverage(
            SemanticMask layer,
            byte rawState,
            int x,
            int y,
            int outputWidth,
            int outputHeight,
            MaskComposeWorkspace workspace)
        {
            if (layer.Width == outputWidth && layer.Height == outputHeight)
            {
                return layer.GetHideCoverageForRawState(y * layer.Width + x, rawState);
            }

            if (layer.IsBinary)
            {
                int sourceY = MaskResolutionConverter.SourceCoordinate(y, layer.Height, outputHeight);
                return layer.GetHideCoverageForRawState(
                    sourceY * layer.Width + workspace.SourceX[x],
                    rawState);
            }

            int y0;
            int y1;
            int fy;
            MaskResolutionConverter.BilinearCoordinate(
                y,
                layer.Height,
                outputHeight,
                out y0,
                out y1,
                out fy);
            return BilinearSample(
                layer,
                rawState,
                workspace.SourceX[x],
                workspace.SourceXNext[x],
                workspace.SourceXFraction[x],
                y0,
                y1,
                fy);
        }

        private static byte BilinearSample(
            SemanticMask layer,
            byte rawState,
            int x0,
            int x1,
            int fx,
            int y0,
            int y1,
            int fy)
        {
            int row0 = y0 * layer.Width;
            int row1 = y1 * layer.Width;
            int top = Interpolate(
                layer.GetHideCoverageForRawState(row0 + x0, rawState),
                layer.GetHideCoverageForRawState(row0 + x1, rawState),
                fx);
            int bottom = Interpolate(
                layer.GetHideCoverageForRawState(row1 + x0, rawState),
                layer.GetHideCoverageForRawState(row1 + x1, rawState),
                fx);
            return (byte)Interpolate(top, bottom, fy);
        }

        private static int Interpolate(int first, int second, int fraction)
        {
            return first + (int)(((long)(second - first) * fraction + 32768) >> 16);
        }

        private static bool TryMapState(GarmentState state, out byte rawState)
        {
            if (state == GarmentState.Full)
            {
                rawState = 0;
                return true;
            }

            if (state == GarmentState.Partial)
            {
                rawState = 1;
                return true;
            }

            rawState = 3;
            return false;
        }

        private static void ValidateCoverageArguments(
            SemanticMask layer,
            int outputWidth,
            int outputHeight,
            byte[] hideCoverage)
        {
            if (layer == null)
            {
                throw new ArgumentNullException("layer");
            }

            if (outputWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("outputWidth");
            }

            if (outputHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("outputHeight");
            }

            if (hideCoverage == null || hideCoverage.Length != checked(outputWidth * outputHeight))
            {
                throw new ArgumentException(
                    "Output buffer does not match the requested dimensions.",
                    "hideCoverage");
            }
        }

        private static void ValidateBooleanArguments(
            SemanticMask layer,
            int outputWidth,
            int outputHeight,
            bool[] hiddenPixels)
        {
            if (layer == null)
            {
                throw new ArgumentNullException("layer");
            }

            if (outputWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("outputWidth");
            }

            if (outputHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("outputHeight");
            }

            if (hiddenPixels == null || hiddenPixels.Length != checked(outputWidth * outputHeight))
            {
                throw new ArgumentException(
                    "Output buffer does not match the requested dimensions.",
                    "hiddenPixels");
            }
        }
    }
}
