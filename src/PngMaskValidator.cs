using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class MaskValidationResult
    {
        public bool IsValid;
        public int Width;
        public int Height;
        public byte BitDepth;
        public byte ColorType;
        public string Message;

        public MaskValidationResult Clone()
        {
            return (MaskValidationResult)MemberwiseClone();
        }
    }

    public static class PngMaskValidator
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static MaskValidationResult Validate(
            byte[] bytes,
            int minimumResolution,
            int maximumResolution,
            int maximumBytes)
        {
            MaskValidationResult result = new MaskValidationResult();
            if (bytes == null || bytes.Length < 33)
            {
                result.Message = "The file is too short to be a PNG.";
                return result;
            }

            if (bytes.Length > maximumBytes)
            {
                result.Message = "The PNG exceeds the configured byte limit.";
                return result;
            }

            for (int i = 0; i < Signature.Length; i++)
            {
                if (bytes[i] != Signature[i])
                {
                    result.Message = "The file does not have a PNG signature.";
                    return result;
                }
            }

            if (ReadUInt32(bytes, 8) != 13 ||
                bytes[12] != (byte)'I' || bytes[13] != (byte)'H' ||
                bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
            {
                result.Message = "The PNG does not start with a valid IHDR chunk.";
                return result;
            }

            uint width = ReadUInt32(bytes, 16);
            uint height = ReadUInt32(bytes, 20);
            if (width > int.MaxValue || height > int.MaxValue)
            {
                result.Message = "The PNG dimensions are not supported.";
                return result;
            }

            result.Width = (int)width;
            result.Height = (int)height;
            result.BitDepth = bytes[24];
            result.ColorType = bytes[25];

            if (result.Width != result.Height)
            {
                result.Message = "Body masks must be square.";
                return result;
            }

            if (result.Width < minimumResolution || result.Width > maximumResolution)
            {
                result.Message = string.Format(
                    "Resolution {0} is outside the allowed range {1}-{2}.",
                    result.Width,
                    minimumResolution,
                    maximumResolution);
                return result;
            }

            if (!IsPowerOfTwo(result.Width))
            {
                result.Message = "Body mask resolution must be a power of two.";
                return result;
            }

            if (result.BitDepth != 8)
            {
                result.Message = "Only 8-bit PNG channels are supported.";
                return result;
            }

            if (result.ColorType != 2 && result.ColorType != 3 &&
                result.ColorType != 4 && result.ColorType != 6)
            {
                result.Message = "Unsupported PNG color type.";
                return result;
            }

            result.IsValid = true;
            result.Message = "PNG header is valid.";
            return result;
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) |
                   ((uint)bytes[offset + 1] << 16) |
                   ((uint)bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
