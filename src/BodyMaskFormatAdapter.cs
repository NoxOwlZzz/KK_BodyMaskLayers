using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class BodyMaskFormatAdapter
    {
        public static bool IsHiddenByShader(Rgba32 pixel, float alphaA, float alphaB)
        {
            float red = pixel.R / 255f;
            float green = pixel.G / 255f;
            float valueA = Math.Max(1f - alphaA, red);
            float valueB = Math.Max(1f - alphaB, green);
            return Math.Min(valueA, valueB) < 0.5f;
        }

        public static int WriteContinuousBodyMask(
            Rgba32[] basePixels,
            int baseWidth,
            int baseHeight,
            float baseAlphaA,
            float baseAlphaB,
            byte[] customHideCoverage,
            int outputWidth,
            int outputHeight,
            Rgba32[] outputPixels)
        {
            ValidateOutput(
                customHideCoverage,
                outputWidth,
                outputHeight,
                outputPixels,
                "customHideCoverage");
            bool hasBase = HasValidBase(basePixels, baseWidth, baseHeight);
            int hiddenCount = 0;
            for (int y = 0; y < outputHeight; y++)
            {
                int baseY = hasBase
                    ? MaskResolutionConverter.SourceCoordinate(y, baseHeight, outputHeight)
                    : 0;
                int baseRow = baseY * baseWidth;
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    int outputIndex = outputRow + x;
                    Rgba32 original = hasBase
                        ? basePixels[baseRow +
                            MaskResolutionConverter.SourceCoordinate(x, baseWidth, outputWidth)]
                        : new Rgba32(255, 255, 255, 255);
                    Rgba32 output = ComposePixel(
                        original,
                        hasBase ? baseAlphaA : 1f,
                        hasBase ? baseAlphaB : 1f,
                        customHideCoverage[outputIndex]);
                    outputPixels[outputIndex] = output;
                    if (output.R < 128 || output.G < 128)
                    {
                        hiddenCount++;
                    }
                }
            }

            return hiddenCount;
        }

        public static int WriteBinaryBodyMask(
            Rgba32[] basePixels,
            int baseWidth,
            int baseHeight,
            float baseAlphaA,
            float baseAlphaB,
            bool[] customHiddenPixels,
            int outputWidth,
            int outputHeight,
            Rgba32[] outputPixels)
        {
            ValidateOutput(
                customHiddenPixels,
                outputWidth,
                outputHeight,
                outputPixels,
                "customHiddenPixels");
            bool hasBase = HasValidBase(basePixels, baseWidth, baseHeight);
            int hiddenCount = 0;
            for (int y = 0; y < outputHeight; y++)
            {
                int baseY = hasBase
                    ? MaskResolutionConverter.SourceCoordinate(y, baseHeight, outputHeight)
                    : 0;
                int baseRow = baseY * baseWidth;
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    int outputIndex = outputRow + x;
                    Rgba32 original = hasBase
                        ? basePixels[baseRow +
                            MaskResolutionConverter.SourceCoordinate(x, baseWidth, outputWidth)]
                        : new Rgba32(255, 255, 255, 255);
                    byte hideCoverage = customHiddenPixels[outputIndex]
                        ? byte.MaxValue
                        : (byte)0;
                    Rgba32 output = ComposePixel(
                        original,
                        hasBase ? baseAlphaA : 1f,
                        hasBase ? baseAlphaB : 1f,
                        hideCoverage);
                    outputPixels[outputIndex] = output;
                    if (output.R < 128 || output.G < 128)
                    {
                        hiddenCount++;
                    }
                }
            }

            return hiddenCount;
        }

        private static Rgba32 ComposePixel(
            Rgba32 original,
            float baseAlphaA,
            float baseAlphaB,
            byte hideCoverage)
        {
            byte baseRed = EffectiveVisibility(original.R, baseAlphaA);
            byte baseGreen = EffectiveVisibility(original.G, baseAlphaB);
            byte customVisibility = (byte)(byte.MaxValue - hideCoverage);
            return new Rgba32(
                Math.Min(baseRed, customVisibility),
                Math.Min(baseGreen, customVisibility),
                original.B,
                original.A);
        }

        private static byte EffectiveVisibility(byte channel, float alpha)
        {
            float value = Math.Max(1f - alpha, channel / 255f);
            if (value <= 0f)
            {
                return 0;
            }

            if (value >= 1f)
            {
                return byte.MaxValue;
            }

            return (byte)(value * 255f + 0.5f);
        }

        private static bool HasValidBase(Rgba32[] basePixels, int baseWidth, int baseHeight)
        {
            return basePixels != null &&
                   baseWidth > 0 &&
                   baseHeight > 0 &&
                   basePixels.Length == checked(baseWidth * baseHeight);
        }

        private static int ValidateOutput(
            Array customCoverage,
            int outputWidth,
            int outputHeight,
            Rgba32[] outputPixels,
            string coverageParameterName)
        {
            if (outputWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("outputWidth");
            }

            if (outputHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("outputHeight");
            }

            int pixelCount = checked(outputWidth * outputHeight);
            if (customCoverage == null || customCoverage.Length != pixelCount)
            {
                throw new ArgumentException(
                    "Custom hidden buffer has an invalid size.",
                    coverageParameterName);
            }

            if (outputPixels == null || outputPixels.Length != pixelCount)
            {
                throw new ArgumentException(
                    "Output pixel buffer has an invalid size.",
                    "outputPixels");
            }

            return pixelCount;
        }
    }
}
