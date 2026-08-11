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
            int pixelCount = checked(outputWidth * outputHeight);
            if (customHiddenPixels == null || customHiddenPixels.Length != pixelCount)
            {
                throw new ArgumentException("Custom hidden buffer has an invalid size.", "customHiddenPixels");
            }

            if (outputPixels == null || outputPixels.Length != pixelCount)
            {
                throw new ArgumentException("Output pixel buffer has an invalid size.", "outputPixels");
            }

            bool hasBase = basePixels != null && baseWidth > 0 && baseHeight > 0 &&
                           basePixels.Length == checked(baseWidth * baseHeight);
            int hiddenCount = 0;
            if (hasBase && baseWidth == outputWidth && baseHeight == outputHeight)
            {
                for (int index = 0; index < outputPixels.Length; index++)
                {
                    Rgba32 original = basePixels[index];
                    bool hidden = customHiddenPixels[index] ||
                                  IsHiddenByShader(original, baseAlphaA, baseAlphaB);
                    byte managed = hidden ? (byte)0 : byte.MaxValue;
                    outputPixels[index] = new Rgba32(managed, managed, original.B, original.A);
                    if (hidden)
                    {
                        hiddenCount++;
                    }
                }

                return hiddenCount;
            }

            for (int y = 0; y < outputHeight; y++)
            {
                int baseY = hasBase
                    ? MaskResolutionConverter.SourceCoordinate(y, baseHeight, outputHeight)
                    : 0;
                int baseRow = baseY * baseWidth;
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    int index = outputRow + x;
                    Rgba32 original = hasBase
                        ? basePixels[baseRow + MaskResolutionConverter.SourceCoordinate(x, baseWidth, outputWidth)]
                        : new Rgba32(255, 255, 255, 255);
                    bool hidden = customHiddenPixels[index] ||
                                  (hasBase && IsHiddenByShader(original, baseAlphaA, baseAlphaB));
                    byte managed = hidden ? (byte)0 : byte.MaxValue;
                    outputPixels[index] = new Rgba32(managed, managed, original.B, original.A);
                    if (hidden)
                    {
                        hiddenCount++;
                    }
                }
            }

            return hiddenCount;
        }
    }
}
