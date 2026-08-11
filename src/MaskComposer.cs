using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
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
            bool[] hiddenPixels)
        {
            if (layer == null)
            {
                throw new ArgumentNullException("layer");
            }

            if (hiddenPixels == null || hiddenPixels.Length != checked(outputWidth * outputHeight))
            {
                throw new ArgumentException("Output buffer does not match the requested dimensions.", "hiddenPixels");
            }

            if (state != GarmentState.Full && state != GarmentState.Partial)
            {
                return 0;
            }

            int newlyHidden = 0;
            bool full = state == GarmentState.Full;
            if (layer.Width == outputWidth && layer.Height == outputHeight)
            {
                for (int index = 0; index < hiddenPixels.Length; index++)
                {
                    if (hiddenPixels[index])
                    {
                        continue;
                    }

                    MaskPixelRule rule = layer.Rules[index];
                    if ((full && (rule == MaskPixelRule.HideWhenFull ||
                                  rule == MaskPixelRule.HideWhenNotOff)) ||
                        (!full && rule == MaskPixelRule.HideWhenNotOff))
                    {
                        hiddenPixels[index] = true;
                        newlyHidden++;
                    }
                }

                return newlyHidden;
            }

            int[] sourceCoordinates = new int[outputWidth];
            for (int x = 0; x < outputWidth; x++)
            {
                sourceCoordinates[x] =
                    MaskResolutionConverter.SourceCoordinate(x, layer.Width, outputWidth);
            }

            for (int y = 0; y < outputHeight; y++)
            {
                int sourceY = MaskResolutionConverter.SourceCoordinate(y, layer.Height, outputHeight);
                int sourceRow = sourceY * layer.Width;
                int outputRow = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    int outputIndex = outputRow + x;
                    if (hiddenPixels[outputIndex])
                    {
                        continue;
                    }

                    int sourceX = sourceCoordinates[x];
                    MaskPixelRule rule = layer.Rules[sourceRow + sourceX];
                    if ((full && (rule == MaskPixelRule.HideWhenFull ||
                                  rule == MaskPixelRule.HideWhenNotOff)) ||
                        (!full && rule == MaskPixelRule.HideWhenNotOff))
                    {
                        hiddenPixels[outputIndex] = true;
                        newlyHidden++;
                    }
                }
            }

            return newlyHidden;
        }
    }
}
