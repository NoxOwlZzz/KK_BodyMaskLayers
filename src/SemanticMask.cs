using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class SemanticMask
    {
        public SemanticMask(int width, int height, MaskPixelRule[] rules)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height");
            }

            if (rules == null)
            {
                throw new ArgumentNullException("rules");
            }

            if (rules.Length != checked(width * height))
            {
                throw new ArgumentException("The rule count does not match the mask dimensions.", "rules");
            }

            Width = width;
            Height = height;
            Rules = rules;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public MaskPixelRule[] Rules { get; private set; }
    }

    public sealed class MaskColorStatistics
    {
        public int YellowPixels;
        public int GreenPixels;
        public int BlackPixels;
        public int RedPixels;
        public int BluePixels;
        public int UnknownPixels;
        public int UnexpectedAlphaPixels;

        public int TotalPixels
        {
            get
            {
                return YellowPixels + GreenPixels + BlackPixels + RedPixels + BluePixels + UnknownPixels;
            }
        }

        public MaskColorStatistics Clone()
        {
            return (MaskColorStatistics)MemberwiseClone();
        }

        public override string ToString()
        {
            return string.Format(
                "yellow={0}, green={1}, black={2}, red={3}, blue={4}, unknown={5}, alphaUnexpected={6}",
                YellowPixels,
                GreenPixels,
                BlackPixels,
                RedPixels,
                BluePixels,
                UnknownPixels,
                UnexpectedAlphaPixels);
        }
    }

    public sealed class MaskDecodeOptions
    {
        public ColorClassificationMode ClassificationMode = ColorClassificationMode.Threshold;
        public int ColorTolerance = 12;
        public UnknownColorPolicy UnknownColorPolicy = UnknownColorPolicy.RejectMask;
    }
}
