using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public struct Rgba32 : IEquatable<Rgba32>
    {
        public Rgba32(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public bool Equals(Rgba32 other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object obj)
        {
            return obj is Rgba32 && Equals((Rgba32)obj);
        }

        public override int GetHashCode()
        {
            return (((R * 397) ^ G) * 397 ^ B) * 397 ^ A;
        }

        public override string ToString()
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", R, G, B, A);
        }
    }
}
