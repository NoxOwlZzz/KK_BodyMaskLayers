using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class MaskPreviewBuilder
    {
        public static Rgba32[] Build(
            SemanticMask mask,
            int maximumDimension,
            out int width,
            out int height)
        {
            if (mask == null)
            {
                throw new ArgumentNullException("mask");
            }

            if (maximumDimension <= 0)
            {
                throw new ArgumentOutOfRangeException("maximumDimension");
            }

            if (mask.Width >= mask.Height)
            {
                width = Math.Min(mask.Width, maximumDimension);
                height = Math.Max(1, (int)((long)mask.Height * width / mask.Width));
            }
            else
            {
                height = Math.Min(mask.Height, maximumDimension);
                width = Math.Max(1, (int)((long)mask.Width * height / mask.Height));
            }

            Rgba32[] result = new Rgba32[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                int sourceY = MaskResolutionConverter.SourceCoordinate(y, mask.Height, height);
                int sourceRow = sourceY * mask.Width;
                int targetRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = MaskResolutionConverter.SourceCoordinate(x, mask.Width, width);
                    result[targetRow + x] = ToColor(mask.Rules[sourceRow + sourceX]);
                }
            }

            return result;
        }

        private static Rgba32 ToColor(MaskPixelRule rule)
        {
            switch (rule)
            {
                case MaskPixelRule.NeverHide:
                    return new Rgba32(255, 255, 0, 255);
                case MaskPixelRule.HideWhenFull:
                    return new Rgba32(0, 255, 0, 255);
                case MaskPixelRule.HideWhenNotOff:
                    return new Rgba32(0, 0, 0, 255);
                default:
                    return new Rgba32(255, 0, 255, 255);
            }
        }
    }
}
