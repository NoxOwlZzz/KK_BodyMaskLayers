using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class MaskResolutionConverter
    {
        public static int SourceCoordinate(int destinationCoordinate, int sourceSize, int destinationSize)
        {
            if (destinationCoordinate < 0 || destinationCoordinate >= destinationSize)
            {
                throw new ArgumentOutOfRangeException("destinationCoordinate");
            }

            if (sourceSize <= 0)
            {
                throw new ArgumentOutOfRangeException("sourceSize");
            }

            if (destinationSize <= 0)
            {
                throw new ArgumentOutOfRangeException("destinationSize");
            }

            return (int)((long)destinationCoordinate * sourceSize / destinationSize);
        }

        public static void BilinearCoordinate(
            int destinationCoordinate,
            int sourceSize,
            int destinationSize,
            out int first,
            out int second,
            out int fraction)
        {
            if (destinationCoordinate < 0 || destinationCoordinate >= destinationSize)
            {
                throw new ArgumentOutOfRangeException("destinationCoordinate");
            }

            if (sourceSize <= 0)
            {
                throw new ArgumentOutOfRangeException("sourceSize");
            }

            if (destinationSize <= 0)
            {
                throw new ArgumentOutOfRangeException("destinationSize");
            }

            if (sourceSize == 1)
            {
                first = 0;
                second = 0;
                fraction = 0;
                return;
            }

            long numerator = (2L * destinationCoordinate + 1L) * sourceSize -
                             destinationSize;
            long denominator = 2L * destinationSize;
            if (numerator <= 0L)
            {
                first = 0;
                second = 0;
                fraction = 0;
                return;
            }

            long maximum = (long)(sourceSize - 1) * denominator;
            if (numerator >= maximum)
            {
                first = sourceSize - 1;
                second = first;
                fraction = 0;
                return;
            }

            first = (int)(numerator / denominator);
            long remainder = numerator % denominator;
            second = first + 1;
            fraction = (int)(remainder * 65536L / denominator);
        }

        public static SemanticMask ResizeNearest(SemanticMask source, int width, int height)
        {
            ValidateResize(source, width, height);
            if (source.HasCategoricalRules)
            {
                MaskPixelRule[] rules = new MaskPixelRule[checked(width * height)];
                for (int y = 0; y < height; y++)
                {
                    int sourceY = SourceCoordinate(y, source.Height, height);
                    int sourceRow = sourceY * source.Width;
                    int destinationRow = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int sourceX = SourceCoordinate(x, source.Width, width);
                        rules[destinationRow + x] =
                            source.GetCategoricalRule(sourceRow + sourceX);
                    }
                }

                return new SemanticMask(width, height, rules);
            }

            byte[] state0 = new byte[checked(width * height)];
            byte[] state1 = new byte[state0.Length];
            byte[] state2 = new byte[state0.Length];
            for (int y = 0; y < height; y++)
            {
                int sourceY = SourceCoordinate(y, source.Height, height);
                int sourceRow = sourceY * source.Width;
                int destinationRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = SourceCoordinate(x, source.Width, width);
                    int sourceIndex = sourceRow + sourceX;
                    int destinationIndex = destinationRow + x;
                    state0[destinationIndex] = source.GetHideCoverageForRawState(sourceIndex, 0);
                    state1[destinationIndex] = source.GetHideCoverageForRawState(sourceIndex, 1);
                    state2[destinationIndex] = source.GetHideCoverageForRawState(sourceIndex, 2);
                }
            }

            return SemanticMask.FromStateCoverageOwned(width, height, state0, state1, state2);
        }

        public static SemanticMask ResizeForComposition(SemanticMask source, int width, int height)
        {
            return source != null && source.IsBinary
                ? ResizeNearest(source, width, height)
                : ResizeBilinear(source, width, height);
        }

        public static SemanticMask ResizeBilinear(SemanticMask source, int width, int height)
        {
            ValidateResize(source, width, height);
            byte[] state0 = new byte[checked(width * height)];
            byte[] state1 = new byte[state0.Length];
            byte[] state2 = new byte[state0.Length];
            for (int y = 0; y < height; y++)
            {
                int y0;
                int y1;
                int fy;
                BilinearCoordinate(y, source.Height, height, out y0, out y1, out fy);
                int destinationRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int x0;
                    int x1;
                    int fx;
                    BilinearCoordinate(x, source.Width, width, out x0, out x1, out fx);
                    int destinationIndex = destinationRow + x;
                    state0[destinationIndex] = SampleBilinear(source, 0, x0, x1, fx, y0, y1, fy);
                    state1[destinationIndex] = SampleBilinear(source, 1, x0, x1, fx, y0, y1, fy);
                    state2[destinationIndex] = SampleBilinear(source, 2, x0, x1, fx, y0, y1, fy);
                }
            }

            return SemanticMask.FromStateCoverageOwned(width, height, state0, state1, state2);
        }

        private static byte SampleBilinear(
            SemanticMask source,
            byte rawState,
            int x0,
            int x1,
            int fx,
            int y0,
            int y1,
            int fy)
        {
            int row0 = y0 * source.Width;
            int row1 = y1 * source.Width;
            int top = Interpolate(
                source.GetHideCoverageForRawState(row0 + x0, rawState),
                source.GetHideCoverageForRawState(row0 + x1, rawState),
                fx);
            int bottom = Interpolate(
                source.GetHideCoverageForRawState(row1 + x0, rawState),
                source.GetHideCoverageForRawState(row1 + x1, rawState),
                fx);
            return (byte)Interpolate(top, bottom, fy);
        }

        private static int Interpolate(int first, int second, int fraction)
        {
            return first + (int)(((long)(second - first) * fraction + 32768) >> 16);
        }

        private static void ValidateResize(SemanticMask source, int width, int height)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height");
            }

            checked
            {
                int pixelCount = width * height;
                if (pixelCount <= 0)
                {
                    throw new OverflowException();
                }
            }
        }
    }
}
