namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class MaskDimensions
    {
        public static bool TryGetPixelCount(int width, int height, out int pixelCount)
        {
            pixelCount = 0;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            long count = (long)width * height;
            if (count > int.MaxValue)
            {
                return false;
            }

            pixelCount = (int)count;
            return true;
        }
    }
}
