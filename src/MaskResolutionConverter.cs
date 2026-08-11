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

        public static SemanticMask ResizeNearest(SemanticMask source, int width, int height)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            MaskPixelRule[] destination = new MaskPixelRule[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                int sourceY = SourceCoordinate(y, source.Height, height);
                int sourceRow = sourceY * source.Width;
                int destinationRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = SourceCoordinate(x, source.Width, width);
                    destination[destinationRow + x] = source.Rules[sourceRow + sourceX];
                }
            }

            return new SemanticMask(width, height, destination);
        }
    }
}
