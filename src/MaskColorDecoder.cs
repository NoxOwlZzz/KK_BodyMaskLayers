using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class MaskColorDecoder
    {
        private const int MaximumCompatibleUnknownFractionDenominator = 8;
        private const int MaximumCompatiblePaletteDistance = 160;

        private enum ColorCategory
        {
            Yellow,
            Green,
            Black,
            Red,
            Blue,
            Unknown
        }

        private static readonly ColorCategory[] SupportedCategories =
        {
            ColorCategory.Yellow,
            ColorCategory.Green,
            ColorCategory.Green,
            ColorCategory.Black,
            ColorCategory.Red
        };

        private static readonly int[] SupportedRed = { 255, 0, 76, 0, 255 };
        private static readonly int[] SupportedGreen = { 255, 255, 255, 0, 0 };
        private static readonly int[] SupportedBlue = { 0, 0, 0, 0, 0 };

        public static bool TryDecode(
            Rgba32[] pixels,
            int width,
            int height,
            MaskDecodeOptions options,
            out SemanticMask semanticMask,
            out MaskColorStatistics statistics,
            out string error)
        {
            semanticMask = null;
            statistics = new MaskColorStatistics();
            error = null;

            if (pixels == null)
            {
                error = "Pixel data is missing.";
                return false;
            }

            if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
            {
                error = "Pixel data does not match the declared dimensions.";
                return false;
            }

            if (options == null)
            {
                error = "Decode options are missing.";
                return false;
            }

            if (options.GradientHandlingMode != GradientHandlingMode.StrictCategorical)
            {
                return TryDecodeContinuousNative(
                    pixels,
                    width,
                    height,
                    options,
                    out semanticMask,
                    out statistics,
                    out error);
            }

            int tolerance = options.ColorTolerance;
            if (tolerance < 0)
            {
                tolerance = 0;
            }
            else if (tolerance > 255)
            {
                tolerance = 255;
            }

            MaskPixelRule[] rules = new MaskPixelRule[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Rgba32 pixel = pixels[i];
                if (pixel.A != 0 && pixel.A != byte.MaxValue)
                {
                    statistics.UnexpectedAlphaPixels++;
                }

                ColorCategory category = Classify(pixel, options.ClassificationMode, tolerance);
                switch (category)
                {
                    case ColorCategory.Yellow:
                        statistics.YellowPixels++;
                        rules[i] = MaskPixelRule.NeverHide;
                        break;
                    case ColorCategory.Green:
                        statistics.GreenPixels++;
                        rules[i] = MaskPixelRule.HideWhenFull;
                        break;
                    case ColorCategory.Black:
                        statistics.BlackPixels++;
                        rules[i] = MaskPixelRule.HideWhenNotOff;
                        break;
                    case ColorCategory.Red:
                        statistics.RedPixels++;
                        rules[i] = MaskPixelRule.HideWhenNotOff;
                        break;
                    case ColorCategory.Blue:
                        statistics.BluePixels++;
                        rules[i] = ResolveUnknown(options.UnknownColorPolicy);
                        break;
                    default:
                        statistics.UnknownPixels++;
                        rules[i] = ResolveUnknown(options.UnknownColorPolicy);
                        break;
                }
            }

            if (options.UnknownColorPolicy == UnknownColorPolicy.RejectMask &&
                (statistics.UnknownPixels != 0 || statistics.BluePixels != 0))
            {
                error = string.Format(
                    "Mask contains unsupported categorical colors (blue={0}, unknown={1}).",
                    statistics.BluePixels,
                    statistics.UnknownPixels);
                return false;
            }

            semanticMask = new SemanticMask(width, height, rules);
            return true;
        }

        public static bool TryDecodeWithPaletteCompatibility(
            Rgba32[] pixels,
            int width,
            int height,
            MaskDecodeOptions options,
            out SemanticMask semanticMask,
            out MaskColorStatistics statistics,
            out int normalizedPixelCount,
            out string error)
        {
            normalizedPixelCount = 0;
            if (options == null ||
                options.GradientHandlingMode != GradientHandlingMode.StrictCategorical ||
                options.ClassificationMode != ColorClassificationMode.Threshold ||
                options.UnknownColorPolicy != UnknownColorPolicy.RejectMask)
            {
                return TryDecode(
                    pixels,
                    width,
                    height,
                    options,
                    out semanticMask,
                    out statistics,
                    out error);
            }

            semanticMask = null;
            statistics = new MaskColorStatistics();
            error = null;
            if (pixels == null)
            {
                error = "Pixel data is missing.";
                return false;
            }

            if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
            {
                error = "Pixel data does not match the declared dimensions.";
                return false;
            }

            int tolerance = options.ColorTolerance;
            if (tolerance < 0)
            {
                tolerance = 0;
            }
            else if (tolerance > 255)
            {
                tolerance = 255;
            }

            int toleranceSquared = 3 * tolerance * tolerance;
            int maximumDistanceSquared =
                MaximumCompatiblePaletteDistance * MaximumCompatiblePaletteDistance;
            int normalizedYellow = 0;
            int normalizedGreen = 0;
            int normalizedBlack = 0;
            int normalizedRed = 0;
            bool compatibleDistances = true;
            MaskPixelRule[] rules = new MaskPixelRule[pixels.Length];
            for (int index = 0; index < pixels.Length; index++)
            {
                Rgba32 pixel = pixels[index];
                if (pixel.A != 0 && pixel.A != byte.MaxValue)
                {
                    statistics.UnexpectedAlphaPixels++;
                }

                if (IsBlueDominant(pixel, tolerance))
                {
                    statistics.BluePixels++;
                    rules[index] = MaskPixelRule.Unknown;
                    continue;
                }

                int distanceSquared;
                ColorCategory nearest = GetNearestSupportedCategory(pixel, out distanceSquared);
                if (distanceSquared <= toleranceSquared)
                {
                    AddRecognizedCategory(nearest, statistics);
                    rules[index] = RuleForCategory(nearest);
                    continue;
                }

                statistics.UnknownPixels++;
                rules[index] = RuleForCategory(nearest);
                compatibleDistances &= distanceSquared <= maximumDistanceSquared;
                switch (nearest)
                {
                    case ColorCategory.Yellow:
                        normalizedYellow++;
                        break;
                    case ColorCategory.Green:
                        normalizedGreen++;
                        break;
                    case ColorCategory.Black:
                        normalizedBlack++;
                        break;
                    case ColorCategory.Red:
                        normalizedRed++;
                        break;
                }
            }

            int unknownPixels = statistics.UnknownPixels;
            bool compatibleFraction = unknownPixels > 0 &&
                                      (long)unknownPixels *
                                      MaximumCompatibleUnknownFractionDenominator <=
                                      statistics.TotalPixels;
            if (statistics.BluePixels != 0 ||
                (unknownPixels != 0 && (!compatibleFraction || !compatibleDistances)))
            {
                error = string.Format(
                    "Mask contains unsupported categorical colors (blue={0}, unknown={1}).",
                    statistics.BluePixels,
                    statistics.UnknownPixels);
                return false;
            }

            normalizedPixelCount = unknownPixels;
            statistics.YellowPixels += normalizedYellow;
            statistics.GreenPixels += normalizedGreen;
            statistics.BlackPixels += normalizedBlack;
            statistics.RedPixels += normalizedRed;
            statistics.UnknownPixels = 0;
            semanticMask = new SemanticMask(width, height, rules);
            return true;
        }

        public static bool CanNormalizeRejectedPalette(
            MaskDecodeOptions options,
            MaskColorStatistics statistics,
            Rgba32[] pixels)
        {
            if (options == null || statistics == null ||
                options.GradientHandlingMode != GradientHandlingMode.StrictCategorical ||
                options.ClassificationMode != ColorClassificationMode.Threshold ||
                options.UnknownColorPolicy != UnknownColorPolicy.RejectMask ||
                statistics.BluePixels != 0 || statistics.UnknownPixels <= 0 ||
                statistics.TotalPixels <= 0 || pixels == null ||
                pixels.Length != statistics.TotalPixels)
            {
                return false;
            }

            if ((long)statistics.UnknownPixels *
                MaximumCompatibleUnknownFractionDenominator > statistics.TotalPixels)
            {
                return false;
            }

            int maximumDistanceSquared =
                MaximumCompatiblePaletteDistance * MaximumCompatiblePaletteDistance;
            int tolerance = options.ColorTolerance;
            if (tolerance < 0)
            {
                tolerance = 0;
            }
            else if (tolerance > 255)
            {
                tolerance = 255;
            }

            int toleranceSquared = 3 * tolerance * tolerance;
            for (int index = 0; index < pixels.Length; index++)
            {
                int distanceSquared;
                if (GetNearestSupportedCategory(pixels[index], out distanceSquared) ==
                    ColorCategory.Unknown ||
                    (distanceSquared > toleranceSquared &&
                     distanceSquared > maximumDistanceSquared))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryDecodeExternalRgbStateCoverage(
            Rgba32[] pixels,
            int width,
            int height,
            out SemanticMask semanticMask,
            out MaskColorStatistics statistics,
            out string error)
        {
            semanticMask = null;
            statistics = new MaskColorStatistics();
            error = null;
            if (pixels == null)
            {
                error = "Pixel data is missing.";
                return false;
            }

            if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
            {
                error = "Pixel data does not match the declared dimensions.";
                return false;
            }

            byte[] state0 = new byte[pixels.Length];
            byte[] state1 = new byte[pixels.Length];
            byte[] state2 = new byte[pixels.Length];
            for (int index = 0; index < pixels.Length; index++)
            {
                Rgba32 pixel = pixels[index];
                state0[index] = pixel.R;
                state1[index] = pixel.G;
                state2[index] = pixel.B;
                statistics.ExternalStatePixels++;
                if ((pixel.R != 0 && pixel.R != byte.MaxValue) ||
                    (pixel.G != 0 && pixel.G != byte.MaxValue) ||
                    (pixel.B != 0 && pixel.B != byte.MaxValue))
                {
                    statistics.EdgePixels++;
                }

                if (pixel.A != 0 && pixel.A != byte.MaxValue)
                {
                    statistics.UnexpectedAlphaPixels++;
                }
            }

            semanticMask = SemanticMask.FromStateCoverageOwned(
                width,
                height,
                state0,
                state1,
                state2);
            return true;
        }

        private static bool TryDecodeContinuousNative(
            Rgba32[] pixels,
            int width,
            int height,
            MaskDecodeOptions options,
            out SemanticMask semanticMask,
            out MaskColorStatistics statistics,
            out string error)
        {
            semanticMask = null;
            statistics = new MaskColorStatistics();
            error = null;
            byte[] state0 = new byte[pixels.Length];
            byte[] state1 = new byte[pixels.Length];
            byte[] state2 = new byte[pixels.Length];
            for (int index = 0; index < pixels.Length; index++)
            {
                Rgba32 pixel = pixels[index];
                if (pixel.A != 0 && pixel.A != byte.MaxValue)
                {
                    statistics.UnexpectedAlphaPixels++;
                }

                ColorCategory exact = GetExactCategorical(pixel);
                if (exact != ColorCategory.Unknown)
                {
                    AddRecognizedCategory(exact, statistics);
                    SetCategoricalCoverage(
                        RuleForCategory(exact),
                        index,
                        state0,
                        state1,
                        state2);
                    continue;
                }

                bool blueDominant = IsBlueDominant(pixel, ClampTolerance(options.ColorTolerance));
                if (pixel.B != 0)
                {
                    statistics.PixelsWithBlueData++;
                    if (blueDominant)
                    {
                        statistics.BluePixels++;
                    }
                    else
                    {
                        statistics.UnknownPixels++;
                        statistics.PackedDataPixels++;
                    }

                    SetUnknownCoverage(
                        options.UnknownColorPolicy,
                        index,
                        state0,
                        state1,
                        state2);
                    continue;
                }

                bool safeAuto = pixel.R <= pixel.G;
                if (options.GradientHandlingMode == GradientHandlingMode.Auto && !safeAuto)
                {
                    statistics.UnknownPixels++;
                    statistics.AmbiguousPixels++;
                    SetUnknownCoverage(
                        options.UnknownColorPolicy,
                        index,
                        state0,
                        state1,
                        state2);
                    continue;
                }

                state0[index] = (byte)(byte.MaxValue - pixel.R);
                state1[index] = (byte)(byte.MaxValue - pixel.G);
                state2[index] = state1[index];
                statistics.ContinuousPixels++;
                if ((pixel.R != 0 && pixel.R != byte.MaxValue) ||
                    (pixel.G != 0 && pixel.G != byte.MaxValue))
                {
                    statistics.EdgePixels++;
                }
            }

            if (options.UnknownColorPolicy == UnknownColorPolicy.RejectMask &&
                (statistics.UnknownPixels != 0 || statistics.BluePixels != 0))
            {
                error = string.Format(
                    "Mask contains incompatible native pixels (blue={0}, unknown={1}, ambiguous={2}, packed={3}).",
                    statistics.BluePixels,
                    statistics.UnknownPixels,
                    statistics.AmbiguousPixels,
                    statistics.PackedDataPixels);
                return false;
            }

            semanticMask = SemanticMask.FromStateCoverageOwned(
                width,
                height,
                state0,
                state1,
                state2);
            return true;
        }

        private static ColorCategory GetExactCategorical(Rgba32 pixel)
        {
            if (pixel.R == 255 && pixel.G == 255 && pixel.B == 0)
            {
                return ColorCategory.Yellow;
            }

            if (pixel.R == 0 && pixel.G == 255 && pixel.B == 0)
            {
                return ColorCategory.Green;
            }

            if (pixel.R == 76 && pixel.G == 255 && pixel.B == 0)
            {
                return ColorCategory.Green;
            }

            if (pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
            {
                return ColorCategory.Black;
            }

            if (pixel.R == 255 && pixel.G == 0 && pixel.B == 0)
            {
                return ColorCategory.Red;
            }

            return ColorCategory.Unknown;
        }

        private static void SetCategoricalCoverage(
            MaskPixelRule rule,
            int index,
            byte[] state0,
            byte[] state1,
            byte[] state2)
        {
            if (rule == MaskPixelRule.HideWhenFull || rule == MaskPixelRule.HideWhenNotOff)
            {
                state0[index] = byte.MaxValue;
            }

            if (rule == MaskPixelRule.HideWhenNotOff)
            {
                state1[index] = byte.MaxValue;
                state2[index] = byte.MaxValue;
            }
        }

        private static void SetUnknownCoverage(
            UnknownColorPolicy policy,
            int index,
            byte[] state0,
            byte[] state1,
            byte[] state2)
        {
            if (policy != UnknownColorPolicy.HideWhenNotOff)
            {
                return;
            }

            state0[index] = byte.MaxValue;
            state1[index] = byte.MaxValue;
            state2[index] = byte.MaxValue;
        }

        private static int ClampTolerance(int tolerance)
        {
            if (tolerance < 0)
            {
                return 0;
            }

            return tolerance > 255 ? 255 : tolerance;
        }

        private static MaskPixelRule ResolveUnknown(UnknownColorPolicy policy)
        {
            switch (policy)
            {
                case UnknownColorPolicy.NoContribution:
                    return MaskPixelRule.NeverHide;
                case UnknownColorPolicy.HideWhenNotOff:
                    return MaskPixelRule.HideWhenNotOff;
                default:
                    return MaskPixelRule.Unknown;
            }
        }

        private static void AddRecognizedCategory(
            ColorCategory category,
            MaskColorStatistics statistics)
        {
            switch (category)
            {
                case ColorCategory.Yellow:
                    statistics.YellowPixels++;
                    break;
                case ColorCategory.Green:
                    statistics.GreenPixels++;
                    break;
                case ColorCategory.Black:
                    statistics.BlackPixels++;
                    break;
                case ColorCategory.Red:
                    statistics.RedPixels++;
                    break;
            }
        }

        private static MaskPixelRule RuleForCategory(ColorCategory category)
        {
            switch (category)
            {
                case ColorCategory.Yellow:
                    return MaskPixelRule.NeverHide;
                case ColorCategory.Green:
                    return MaskPixelRule.HideWhenFull;
                case ColorCategory.Black:
                case ColorCategory.Red:
                    return MaskPixelRule.HideWhenNotOff;
                default:
                    return MaskPixelRule.Unknown;
            }
        }

        private static ColorCategory Classify(
            Rgba32 pixel,
            ColorClassificationMode mode,
            int tolerance)
        {
            if (IsBlueDominant(pixel, tolerance))
            {
                return ColorCategory.Blue;
            }

            if (mode == ColorClassificationMode.Exact)
            {
                if (pixel.R == 255 && pixel.G == 255 && pixel.B == 0)
                {
                    return ColorCategory.Yellow;
                }

                if (pixel.R == 0 && pixel.G == 255 && pixel.B == 0)
                {
                    return ColorCategory.Green;
                }

                if (pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
                {
                    return ColorCategory.Black;
                }

                if (pixel.R == 255 && pixel.G == 0 && pixel.B == 0)
                {
                    return ColorCategory.Red;
                }

                return ColorCategory.Unknown;
            }

            int bestDistance;
            ColorCategory best = GetNearestSupportedCategory(pixel, out bestDistance);
            if (mode == ColorClassificationMode.NearestCategory)
            {
                return best;
            }

            int toleranceSquared = 3 * tolerance * tolerance;
            return bestDistance <= toleranceSquared ? best : ColorCategory.Unknown;
        }

        private static ColorCategory GetNearestSupportedCategory(
            Rgba32 pixel,
            out int bestDistance)
        {
            bestDistance = int.MaxValue;
            ColorCategory best = ColorCategory.Unknown;
            for (int i = 0; i < SupportedCategories.Length; i++)
            {
                int dr = pixel.R - SupportedRed[i];
                int dg = pixel.G - SupportedGreen[i];
                int db = pixel.B - SupportedBlue[i];
                int distance = dr * dr + dg * dg + db * db;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = SupportedCategories[i];
                }
            }

            return best;
        }

        private static bool IsBlueDominant(Rgba32 pixel, int tolerance)
        {
            int lowLimit = Math.Max(0, tolerance * 2);
            int highLimit = Math.Max(128, 255 - tolerance * 2);
            return pixel.B >= highLimit && pixel.R <= lowLimit && pixel.G <= lowLimit;
        }
    }
}
