using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class GradientCoverageTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("coverage: native exact palette endpoints", NativePaletteEndpoints),
                new TestCase("coverage: native gradient modes and diagnostics", NativeGradientModes),
                new TestCase("coverage: horizontal vertical diagonal gradients and antialiasing", GradientShapesAndAntialiasing),
                new TestCase("coverage: legacy RGB raw states", LegacyRawStates),
                new TestCase("coverage: binary bitset boundaries and storage", BinaryBitsetBoundaries),
                new TestCase("coverage: 512 and 1024 binary versus continuous storage", LargeStorageFootprints),
                new TestCase("coverage: nearest binary and bilinear continuous", ResamplingPaths),
                new TestCase("coverage: 512 and 1024 resampling paths", LargeResolutionResampling),
                new TestCase("coverage: max overlay composition", MaximumOverlayComposition),
                new TestCase("coverage: native legacy and base overlap", NativeLegacyAndBaseOverlap),
                new TestCase("coverage: continuous output preserves base B/A", ContinuousOutputPreservesChannels)
            };
        }

        private static void NativePaletteEndpoints()
        {
            Rgba32[] pixels =
            {
                new Rgba32(255, 255, 0, 255),
                new Rgba32(0, 255, 0, 255),
                new Rgba32(76, 255, 0, 255),
                new Rgba32(0, 0, 0, 255),
                new Rgba32(255, 0, 0, 255)
            };
            SemanticMask mask;
            MaskColorStatistics statistics;
            string error;
            Check.True(
                MaskColorDecoder.TryDecode(
                    pixels,
                    5,
                    1,
                    ContinuousOptions(GradientHandlingMode.Auto, UnknownColorPolicy.RejectMask),
                    out mask,
                    out statistics,
                    out error),
                "Auto must accept exact categorical endpoints: " + error);
            Check.True(mask.IsBinary, "Exact categorical pixels must compile to binary storage.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(0, 0), "Yellow state 0 mismatch.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(0, 1), "Yellow state 1 mismatch.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(1, 0), "Green state 0 mismatch.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(1, 1), "Green state 1 mismatch.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(2, 0), "Exported green alias state 0 mismatch.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(2, 1), "Exported green alias state 1 mismatch.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(3, 0), "Black state 0 mismatch.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(3, 1), "Black state 1 mismatch.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(4, 0), "Exact red must keep v1 Full behavior.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(4, 1), "Exact red must keep v1 Partial behavior.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(4, 3), "Off must be neutral.");
            Check.Equal(1, statistics.YellowPixels, "Yellow statistic mismatch.");
            Check.Equal(2, statistics.GreenPixels, "Green endpoint and exported-alias statistic mismatch.");
            Check.Equal(1, statistics.BlackPixels, "Black statistic mismatch.");
            Check.Equal(1, statistics.RedPixels, "Red statistic mismatch.");
        }

        private static void NativeGradientModes()
        {
            Rgba32[] safe = { new Rgba32(64, 192, 0, 127) };
            SemanticMask mask;
            MaskColorStatistics statistics;
            string error;
            Check.True(
                MaskColorDecoder.TryDecode(
                    safe,
                    1,
                    1,
                    ContinuousOptions(GradientHandlingMode.Auto, UnknownColorPolicy.RejectMask),
                    out mask,
                    out statistics,
                    out error),
                "Auto must accept monotonic R/G coverage: " + error);
            Check.False(mask.IsBinary, "Intermediate values must use continuous storage.");
            Check.Equal((byte)191, mask.GetHideCoverageForRawState(0, 0), "Full coverage must be 255-R.");
            Check.Equal((byte)63, mask.GetHideCoverageForRawState(0, 1), "Partial coverage must be 255-G.");
            Check.Equal((byte)63, mask.GetHideCoverageForRawState(0, 2), "Native raw state 2 must share Partial.");
            Check.Equal(1, statistics.ContinuousPixels, "Continuous statistic mismatch.");
            Check.Equal(1, statistics.EdgePixels, "Edge statistic mismatch.");
            Check.Equal(1, statistics.UnexpectedAlphaPixels, "Alpha diagnostic mismatch.");

            Rgba32[] nonMonotonic = { new Rgba32(192, 64, 0, 255) };
            Check.False(
                MaskColorDecoder.TryDecode(
                    nonMonotonic,
                    1,
                    1,
                    ContinuousOptions(GradientHandlingMode.Auto, UnknownColorPolicy.RejectMask),
                    out mask,
                    out statistics,
                    out error),
                "Auto must reject ambiguous non-monotonic R/G data.");
            Check.Equal(1, statistics.AmbiguousPixels, "Ambiguous diagnostic mismatch.");
            Check.Contains("ambiguous=1", error, "Ambiguous rejection must be diagnostic.");

            Check.True(
                MaskColorDecoder.TryDecode(
                    nonMonotonic,
                    1,
                    1,
                    ContinuousOptions(GradientHandlingMode.PreserveContinuous, UnknownColorPolicy.RejectMask),
                    out mask,
                    out statistics,
                    out error),
                "PreserveContinuous must keep explicitly requested R/G data: " + error);
            Check.Equal((byte)63, mask.GetHideCoverageForRawState(0, 0), "Preserved Full coverage mismatch.");
            Check.Equal((byte)191, mask.GetHideCoverageForRawState(0, 1), "Preserved Partial coverage mismatch.");

            Rgba32[] packed = { new Rgba32(64, 192, 17, 255) };
            Check.False(
                MaskColorDecoder.TryDecode(
                    packed,
                    1,
                    1,
                    ContinuousOptions(GradientHandlingMode.PreserveContinuous, UnknownColorPolicy.RejectMask),
                    out mask,
                    out statistics,
                    out error),
                "Native continuous decoding must not silently consume B data.");
            Check.Equal(1, statistics.PackedDataPixels, "Packed-data diagnostic mismatch.");
            Check.Equal(1, statistics.PixelsWithBlueData, "B-data diagnostic mismatch.");
        }

        private static void LegacyRawStates()
        {
            Rgba32[] pixels =
            {
                new Rgba32(0, 64, 128, 7),
                new Rgba32(255, 192, 1, 127)
            };
            SemanticMask mask;
            MaskColorStatistics statistics;
            string error;
            Check.True(
                MaskColorDecoder.TryDecodeLegacyRgbStateCoverage(
                    pixels,
                    2,
                    1,
                    out mask,
                    out statistics,
                    out error),
                "Legacy RGB decode failed: " + error);
            Check.False(mask.IsBinary, "Intermediate legacy channels require continuous storage.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(0, 0), "Legacy R/state 0 mismatch.");
            Check.Equal((byte)64, mask.GetHideCoverageForRawState(0, 1), "Legacy G/state 1 mismatch.");
            Check.Equal((byte)128, mask.GetHideCoverageForRawState(0, 2), "Legacy B/state 2 mismatch.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(0, 3), "Legacy Off must be neutral.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(1, 0), "Legacy state 0 endpoint mismatch.");
            Check.Equal((byte)192, mask.GetHideCoverageForRawState(1, 1), "Legacy state 1 value mismatch.");
            Check.Equal((byte)1, mask.GetHideCoverageForRawState(1, 2), "Legacy state 2 value mismatch.");
            Check.Equal(2, statistics.LegacyStatePixels, "Legacy pixel statistic mismatch.");
            Check.Equal(2, statistics.UnexpectedAlphaPixels, "Legacy alpha diagnostic mismatch.");

            byte[] stateOutput = new byte[2];
            MaskComposeWorkspace workspace = new MaskComposeWorkspace();
            MaskComposer.Accumulate(mask, (byte)1, 2, 1, stateOutput, workspace);
            Check.SequenceEqual(
                new byte[] { 64, 192 },
                stateOutput,
                "Raw state 1 must compose the legacy green channel.");
            Array.Clear(stateOutput, 0, stateOutput.Length);
            MaskComposer.Accumulate(mask, (byte)2, 2, 1, stateOutput, workspace);
            Check.SequenceEqual(
                new byte[] { 128, 1 },
                stateOutput,
                "Raw state 2 must compose the distinct legacy blue channel.");
            Array.Clear(stateOutput, 0, stateOutput.Length);
            Check.Equal(
                0,
                MaskComposer.Accumulate(mask, (byte)3, 2, 1, stateOutput, workspace),
                "Raw state 3/Off must not contribute.");
        }

        private static void GradientShapesAndAntialiasing()
        {
            const int size = 8;
            Rgba32[] horizontal = new Rgba32[size * size];
            Rgba32[] vertical = new Rgba32[size * size];
            Rgba32[] diagonal = new Rgba32[size * size];
            Rgba32[] greenToYellow = new Rgba32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte horizontalValue = (byte)(x * 255 / (size - 1));
                    byte verticalValue = (byte)(y * 255 / (size - 1));
                    byte diagonalValue = (byte)((x + y) * 255 / ((size - 1) * 2));
                    int index = y * size + x;
                    horizontal[index] = new Rgba32(horizontalValue, horizontalValue, 0, 255);
                    vertical[index] = new Rgba32(0, verticalValue, 0, 255);
                    diagonal[index] = new Rgba32(diagonalValue, diagonalValue, 0, 255);
                    greenToYellow[index] = new Rgba32(horizontalValue, 255, 0, 255);
                }
            }

            SemanticMask horizontalMask = DecodeAuto(horizontal, size, size);
            SemanticMask verticalMask = DecodeAuto(vertical, size, size);
            SemanticMask diagonalMask = DecodeAuto(diagonal, size, size);
            SemanticMask greenToYellowMask = DecodeAuto(greenToYellow, size, size);
            Check.False(horizontalMask.IsBinary, "A horizontal ramp must remain continuous.");
            Check.False(verticalMask.IsBinary, "A vertical ramp must remain continuous.");
            Check.False(diagonalMask.IsBinary, "A diagonal ramp must remain continuous.");
            Check.False(greenToYellowMask.IsBinary, "A green-to-yellow ramp must remain continuous.");

            for (int coordinate = 1; coordinate < size; coordinate++)
            {
                Check.True(
                    horizontalMask.GetHideCoverageForRawState(coordinate, 0) <
                    horizontalMask.GetHideCoverageForRawState(coordinate - 1, 0),
                    "Black-to-yellow horizontal Full coverage must be monotonic.");
                Check.True(
                    verticalMask.GetHideCoverageForRawState(coordinate * size, 1) <
                    verticalMask.GetHideCoverageForRawState((coordinate - 1) * size, 1),
                    "Black-to-green vertical Partial coverage must be monotonic.");
                Check.True(
                    diagonalMask.GetHideCoverageForRawState(coordinate * size + coordinate, 0) <
                    diagonalMask.GetHideCoverageForRawState(
                        (coordinate - 1) * size + coordinate - 1,
                        0),
                    "Diagonal coverage must be monotonic.");
                Check.True(
                    greenToYellowMask.GetHideCoverageForRawState(coordinate, 0) <
                    greenToYellowMask.GetHideCoverageForRawState(coordinate - 1, 0),
                    "Green-to-yellow Full coverage must be monotonic.");
                Check.Equal(
                    (byte)0,
                    greenToYellowMask.GetHideCoverageForRawState(coordinate, 1),
                    "Green-to-yellow must remain visible in Partial.");
            }

            Rgba32[] alphaRamp = new Rgba32[size];
            for (int x = 0; x < size; x++)
            {
                byte value = (byte)(x * 255 / (size - 1));
                alphaRamp[x] = new Rgba32(value, value, 0, value);
            }

            SemanticMask alphaMask;
            MaskColorStatistics alphaStatistics;
            string alphaError;
            Check.True(
                MaskColorDecoder.TryDecode(
                    alphaRamp,
                    size,
                    1,
                    ContinuousOptions(GradientHandlingMode.Auto, UnknownColorPolicy.RejectMask),
                    out alphaMask,
                    out alphaStatistics,
                    out alphaError),
                "A varying-alpha gradient must decode without altering R/G coverage: " + alphaError);
            Check.False(alphaMask.IsBinary, "A varying-alpha gradient must remain continuous.");
            Check.Equal(
                size - 2,
                alphaStatistics.UnexpectedAlphaPixels,
                "Only intermediate alpha samples should be diagnostic.");
            for (int x = 0; x < size; x++)
            {
                Check.Equal(
                    horizontalMask.GetHideCoverageForRawState(x, 0),
                    alphaMask.GetHideCoverageForRawState(x, 0),
                    "Alpha must not change Full coverage at " + x + ".");
                Check.Equal(
                    horizontalMask.GetHideCoverageForRawState(x, 1),
                    alphaMask.GetHideCoverageForRawState(x, 1),
                    "Alpha must not change Partial coverage at " + x + ".");
            }

            Rgba32[] onePixelEdge =
            {
                new Rgba32(0, 0, 0, 255),
                new Rgba32(128, 128, 0, 255),
                new Rgba32(255, 255, 0, 255)
            };
            SemanticMask onePixelMask = DecodeAuto(onePixelEdge, 3, 1);
            Check.Equal((byte)127, onePixelMask.GetHideCoverageForRawState(1, 0),
                "A one-pixel antialias edge must preserve its 8-bit intensity.");

            Rgba32[] fourPixelEdge =
            {
                new Rgba32(0, 0, 0, 255),
                new Rgba32(51, 51, 0, 255),
                new Rgba32(102, 102, 0, 255),
                new Rgba32(153, 153, 0, 255),
                new Rgba32(204, 204, 0, 255),
                new Rgba32(255, 255, 0, 255)
            };
            SemanticMask fourPixelMask = DecodeAuto(fourPixelEdge, 6, 1);
            for (int index = 1; index < fourPixelEdge.Length; index++)
            {
                Check.True(
                    fourPixelMask.GetHideCoverageForRawState(index, 0) <
                    fourPixelMask.GetHideCoverageForRawState(index - 1, 0),
                    "A four-pixel antialias ramp must not snap to categories.");
            }
        }

        private static void BinaryBitsetBoundaries()
        {
            MaskPixelRule[] rules = new MaskPixelRule[65];
            for (int index = 0; index < rules.Length; index++)
            {
                rules[index] = MaskPixelRule.NeverHide;
            }

            rules[0] = MaskPixelRule.HideWhenFull;
            rules[31] = MaskPixelRule.HideWhenNotOff;
            rules[32] = MaskPixelRule.HideWhenFull;
            rules[63] = MaskPixelRule.HideWhenNotOff;
            rules[64] = MaskPixelRule.HideWhenFull;
            SemanticMask mask = new SemanticMask(65, 1, rules);
            Check.True(mask.IsBinary, "Categorical masks must use bitsets.");
            Check.True(mask.StorageBytes < rules.Length * 2L, "Binary storage should be compact.");
            int[] hidden = { 0, 31, 32, 63, 64 };
            for (int i = 0; i < hidden.Length; i++)
            {
                Check.Equal(
                    byte.MaxValue,
                    mask.GetHideCoverageForRawState(hidden[i], 0),
                    "Full bitset lost boundary bit " + hidden[i] + ".");
            }

            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(31, 1), "Partial bit at 31 mismatch.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(32, 1), "Full-only bit at 32 leaked into Partial.");
            Check.Equal(byte.MaxValue, mask.GetHideCoverageForRawState(63, 2), "Raw state 2 bit at 63 mismatch.");
            Check.Equal((byte)0, mask.GetHideCoverageForRawState(64, 2), "Full-only bit at 64 leaked into raw state 2.");
        }

        private static void LargeStorageFootprints()
        {
            int[] sizes = { 512, 1024 };
            for (int sizeIndex = 0; sizeIndex < sizes.Length; sizeIndex++)
            {
                int size = sizes[sizeIndex];
                int count = size * size;
                MaskPixelRule[] rules = new MaskPixelRule[count];
                byte[] continuousFull = new byte[count];
                byte[] continuousPartial = new byte[count];
                for (int index = 0; index < count; index++)
                {
                    rules[index] = (index & 1) == 0
                        ? MaskPixelRule.HideWhenFull
                        : MaskPixelRule.HideWhenNotOff;
                    continuousFull[index] = (byte)(index % 255 + 1);
                    continuousPartial[index] = (byte)((index * 17) % 255 + 1);
                }

                SemanticMask binary = new SemanticMask(size, size, rules);
                SemanticMask continuous = SemanticMask.FromStateCoverage(
                    size,
                    size,
                    continuousFull,
                    continuousPartial,
                    continuousPartial);
                long expectedBinaryBytes = 2L * ((count + 31) / 32) * 4L;
                long expectedContinuousBytes = 2L * count;
                Check.Equal(expectedBinaryBytes, binary.StorageBytes,
                    "Binary Full/Partial bitset footprint mismatch at " + size + ".");
                Check.Equal(expectedContinuousBytes, continuous.StorageBytes,
                    "Continuous shared Partial footprint mismatch at " + size + ".");
                Check.True(binary.StorageBytes * 8L == continuous.StorageBytes,
                    "Binary storage should be exactly one eighth of two 8-bit planes.");
                Check.True(binary.IsBinary, "Categorical footprint fixture must remain binary.");
                Check.False(continuous.IsBinary, "Gradient footprint fixture must remain continuous.");
                Check.True(continuous.AreStatePlanesEquivalent(1, 2),
                    "Identical Partial planes must share storage and fast-path identity.");
            }
        }

        private static void ResamplingPaths()
        {
            SemanticMask binary = new SemanticMask(
                2,
                1,
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide,
                    MaskPixelRule.HideWhenNotOff
                });
            byte[] binaryOutput = new byte[4];
            MaskComposer.Accumulate(
                binary,
                GarmentState.Full,
                4,
                1,
                binaryOutput,
                new MaskComposeWorkspace());
            Check.SequenceEqual(
                new byte[] { 0, 0, 255, 255 },
                binaryOutput,
                "Binary resampling must stay nearest/category-aware.");

            SemanticMask continuous = SemanticMask.FromStateCoverage(
                2,
                1,
                new byte[] { 1, 254 },
                new byte[] { 1, 254 },
                new byte[] { 1, 254 });
            byte[] continuousOutput = new byte[5];
            MaskComposer.Accumulate(
                continuous,
                (byte)0,
                5,
                1,
                continuousOutput,
                new MaskComposeWorkspace());
            Check.SequenceEqual(
                new byte[] { 1, 26, 128, 229, 254 },
                continuousOutput,
                "Continuous resampling must use center-of-texel interpolation.");
            for (int index = 1; index < continuousOutput.Length; index++)
            {
                Check.True(
                    continuousOutput[index] >= continuousOutput[index - 1],
                    "Bilinear coverage must remain monotonic.");
            }

            SemanticMask resized = MaskResolutionConverter.ResizeForComposition(continuous, 5, 1);
            for (int index = 0; index < 5; index++)
            {
                Check.Equal(
                    continuousOutput[index],
                    resized.GetHideCoverageForRawState(index, 0),
                    "Explicit resize and streaming composition must agree at " + index + ".");
            }
        }

        private static void LargeResolutionResampling()
        {
            const int small = 512;
            const int large = 1024;
            byte[] ramp = new byte[small * small];
            for (int y = 0; y < small; y++)
            {
                int row = y * small;
                for (int x = 0; x < small; x++)
                {
                    ramp[row + x] = (byte)(x * 255 / (small - 1));
                }
            }

            SemanticMask source = SemanticMask.FromStateCoverage(small, small, ramp, ramp, ramp);
            SemanticMask upscaled = MaskResolutionConverter.ResizeForComposition(source, large, large);
            Check.Equal(large, upscaled.Width, "512-to-1024 width mismatch.");
            Check.Equal(large, upscaled.Height, "512-to-1024 height mismatch.");
            Check.Equal((byte)0, upscaled.GetHideCoverageForRawState(0, 0),
                "Upscale must preserve the first endpoint.");
            Check.Equal((byte)255, upscaled.GetHideCoverageForRawState(large - 1, 0),
                "Upscale must preserve the last endpoint.");
            int sampleRow = (large / 2) * large;
            byte previous = upscaled.GetHideCoverageForRawState(sampleRow, 0);
            for (int x = 1; x < large; x++)
            {
                byte current = upscaled.GetHideCoverageForRawState(sampleRow + x, 0);
                Check.True(current >= previous, "512-to-1024 bilinear coverage must remain monotonic.");
                previous = current;
            }

            SemanticMask downscaled = MaskResolutionConverter.ResizeForComposition(
                upscaled,
                small,
                small);
            Check.Equal(small, downscaled.Width, "1024-to-512 width mismatch.");
            Check.Equal(small, downscaled.Height, "1024-to-512 height mismatch.");
            for (int x = 0; x < small; x += 17)
            {
                int expected = ramp[x];
                int actual = downscaled.GetHideCoverageForRawState(x, 0);
                Check.True(
                    Math.Abs(expected - actual) <= 1,
                    "1024-to-512 roundtrip must preserve 8-bit coverage within one level.");
            }

            MaskPixelRule[] binaryRules = new MaskPixelRule[small * small];
            for (int y = 0; y < small; y++)
            {
                int row = y * small;
                for (int x = 0; x < small; x++)
                {
                    binaryRules[row + x] = x < small / 2
                        ? MaskPixelRule.NeverHide
                        : MaskPixelRule.HideWhenNotOff;
                }
            }

            SemanticMask binary = new SemanticMask(small, small, binaryRules);
            SemanticMask binaryUpscaled = MaskResolutionConverter.ResizeForComposition(
                binary,
                large,
                large);
            Check.True(binaryUpscaled.IsBinary, "Binary upscale must keep compact binary storage.");
            Check.Equal((byte)0, binaryUpscaled.GetHideCoverageForRawState(511, 0),
                "Nearest binary upscale must not bleed across the edge.");
            Check.Equal((byte)255, binaryUpscaled.GetHideCoverageForRawState(512, 0),
                "Nearest binary upscale must retain the categorical edge.");

            SemanticMask binaryDownscaled = MaskResolutionConverter.ResizeForComposition(
                binaryUpscaled,
                small,
                small);
            Check.True(binaryDownscaled.IsBinary, "Binary 1024-to-512 downscale must remain binary.");
            Check.Equal(small, binaryDownscaled.Width, "Binary 1024-to-512 width mismatch.");
            Check.Equal(small, binaryDownscaled.Height, "Binary 1024-to-512 height mismatch.");
            for (int x = 0; x < small; x++)
            {
                Check.Equal(
                    binary.GetHideCoverageForRawState(x, 0),
                    binaryDownscaled.GetHideCoverageForRawState(x, 0),
                    "Binary 1024-to-512 nearest mapping mismatch at " + x + ".");
            }

            SemanticMask rectangular = SemanticMask.FromStateCoverage(
                4,
                2,
                new byte[] { 0, 64, 128, 255, 0, 64, 128, 255 },
                new byte[] { 0, 64, 128, 255, 0, 64, 128, 255 },
                new byte[] { 0, 64, 128, 255, 0, 64, 128, 255 });
            SemanticMask rectangularResized = MaskResolutionConverter.ResizeForComposition(
                rectangular,
                8,
                4);
            Check.Equal(8, rectangularResized.Width, "Rectangular width mismatch.");
            Check.Equal(4, rectangularResized.Height, "Rectangular height mismatch.");
            byte[] rectangularExpected =
            {
                0, 16, 48, 80, 112, 160, 223, 255,
                0, 16, 48, 80, 112, 160, 223, 255,
                0, 16, 48, 80, 112, 160, 223, 255,
                0, 16, 48, 80, 112, 160, 223, 255
            };
            byte[] rectangularActual = new byte[rectangularExpected.Length];
            for (int index = 0; index < rectangularActual.Length; index++)
            {
                rectangularActual[index] =
                    rectangularResized.GetHideCoverageForRawState(index, 0);
            }

            Check.SequenceEqual(
                rectangularExpected,
                rectangularActual,
                "Rectangular resize must retain the center-of-texel coverage mapping.");
            Check.True(
                rectangularResized.AreStatePlanesEquivalent(0, 1) &&
                rectangularResized.AreStatePlanesEquivalent(1, 2),
                "Rectangular resize must retain shared equivalent state planes.");
        }

        private static void MaximumOverlayComposition()
        {
            SemanticMask first = SemanticMask.FromStateCoverage(
                3,
                1,
                new byte[] { 10, 200, 30 },
                new byte[] { 0, 0, 0 },
                new byte[] { 0, 0, 0 });
            SemanticMask second = SemanticMask.FromStateCoverage(
                3,
                1,
                new byte[] { 20, 100, 255 },
                new byte[] { 0, 0, 0 },
                new byte[] { 0, 0, 0 });
            byte[] output = new byte[3];
            MaskComposeWorkspace workspace = new MaskComposeWorkspace();
            Check.Equal(3, MaskComposer.Accumulate(first, (byte)0, 3, 1, output, workspace), "First overlay changed count mismatch.");
            Check.Equal(2, MaskComposer.Accumulate(second, (byte)0, 3, 1, output, workspace), "Second overlay max changed count mismatch.");
            Check.SequenceEqual(
                new byte[] { 20, 200, 255 },
                output,
                "Overlays must compose with max hide coverage, not addition.");
            Check.Equal(0, MaskComposer.Accumulate(second, (byte)3, 3, 1, output, workspace), "Off must contribute nothing.");
        }

        private static void NativeLegacyAndBaseOverlap()
        {
            SemanticMask native;
            MaskColorStatistics nativeStatistics;
            string error;
            Check.True(
                MaskColorDecoder.TryDecode(
                    new Rgba32[]
                    {
                        new Rgba32(191, 223, 0, 255),
                        new Rgba32(0, 255, 0, 255),
                        new Rgba32(255, 255, 0, 255)
                    },
                    3,
                    1,
                    ContinuousOptions(GradientHandlingMode.Auto, UnknownColorPolicy.RejectMask),
                    out native,
                    out nativeStatistics,
                    out error),
                "Native overlap fixture must decode: " + error);

            SemanticMask legacy;
            MaskColorStatistics legacyStatistics;
            Check.True(
                MaskColorDecoder.TryDecodeLegacyRgbStateCoverage(
                    new Rgba32[]
                    {
                        new Rgba32(96, 48, 16, 255),
                        new Rgba32(64, 32, 8, 255),
                        new Rgba32(128, 64, 4, 255)
                    },
                    3,
                    1,
                    out legacy,
                    out legacyStatistics,
                    out error),
                "Legacy overlap fixture must decode: " + error);

            SemanticMask binary = new SemanticMask(
                3,
                1,
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide,
                    MaskPixelRule.HideWhenFull,
                    MaskPixelRule.NeverHide
                });
            byte[] combined = new byte[3];
            MaskComposeWorkspace workspace = new MaskComposeWorkspace();
            MaskComposer.Accumulate(native, (byte)0, 3, 1, combined, workspace);
            MaskComposer.Accumulate(legacy, (byte)0, 3, 1, combined, workspace);
            MaskComposer.Accumulate(binary, (byte)0, 3, 1, combined, workspace);
            Check.SequenceEqual(
                new byte[] { 96, 255, 128 },
                combined,
                "Native, legacy and binary sources must use one max-hide operation.");

            Rgba32[] output = new Rgba32[3];
            BodyMaskFormatAdapter.WriteContinuousBodyMask(
                new Rgba32[]
                {
                    new Rgba32(200, 180, 11, 21),
                    new Rgba32(160, 140, 12, 22),
                    new Rgba32(120, 100, 13, 23)
                },
                3,
                1,
                1f,
                1f,
                combined,
                3,
                1,
                output);
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(159, 159, 11, 21),
                    new Rgba32(0, 0, 12, 22),
                    new Rgba32(120, 100, 13, 23)
                },
                output,
                "A soft base must combine by minimum visibility while preserving B/A.");
        }

        private static void ContinuousOutputPreservesChannels()
        {
            Rgba32[] basePixels =
            {
                new Rgba32(64, 192, 11, 22),
                new Rgba32(255, 128, 33, 44),
                new Rgba32(7, 9, 55, 66)
            };
            byte[] custom = { 0, 64, 255 };
            Rgba32[] output = new Rgba32[3];
            int hidden = BodyMaskFormatAdapter.WriteContinuousBodyMask(
                basePixels,
                3,
                1,
                1f,
                1f,
                custom,
                3,
                1,
                output);
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(64, 192, 11, 22),
                    new Rgba32(191, 128, 33, 44),
                    new Rgba32(0, 0, 55, 66)
                },
                output,
                "Continuous output must min effective base visibility against custom visibility.");
            Check.Equal(2, hidden, "Shader-threshold hidden count mismatch.");

            Rgba32[] scalarOutput = new Rgba32[1];
            BodyMaskFormatAdapter.WriteContinuousBodyMask(
                new Rgba32[] { new Rgba32(0, 64, 77, 88) },
                1,
                1,
                0.25f,
                0.5f,
                new byte[] { 0 },
                1,
                1,
                scalarOutput);
            Check.Equal(new Rgba32(191, 128, 77, 88), scalarOutput[0], "Scalar effective visibility mismatch.");
        }

        private static MaskDecodeOptions ContinuousOptions(
            GradientHandlingMode mode,
            UnknownColorPolicy unknownColorPolicy)
        {
            MaskDecodeOptions options = new MaskDecodeOptions();
            options.GradientHandlingMode = mode;
            options.UnknownColorPolicy = unknownColorPolicy;
            options.ClassificationMode = ColorClassificationMode.Threshold;
            options.ColorTolerance = 12;
            return options;
        }

        private static SemanticMask DecodeAuto(Rgba32[] pixels, int width, int height)
        {
            SemanticMask mask;
            MaskColorStatistics statistics;
            string error;
            Check.True(
                MaskColorDecoder.TryDecode(
                    pixels,
                    width,
                    height,
                    ContinuousOptions(GradientHandlingMode.Auto, UnknownColorPolicy.RejectMask),
                    out mask,
                    out statistics,
                    out error),
                "Synthetic gradient must decode: " + error);
            return mask;
        }
    }
}
