using System;
using System.Security.Cryptography;
using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class ClothingMaskLayerData
    {
        public ClothingSlot Slot;
        public bool Enabled = true;
        public byte[] OriginalPngBytes;
        public int Width;
        public int Height;
        public string Hash;
        public ClothingItemIdentity BoundItemIdentity;
        public int ColorFormatVersion = 1;
        public UnknownStatePolicy? OptionalStatePolicy;
        public string CreatedWithPluginVersion;
        public string LastValidationResult;
        public MaskSourceContract SourceContract = MaskSourceContract.Native;
        public GradientHandlingMode GradientHandlingMode = GradientHandlingMode.StrictCategorical;
        public string SourceProviderId;
        public string SourceFingerprint;
        public string SourceAsset;

        public ClothingMaskLayerData DeepClone()
        {
            ClothingMaskLayerData clone = (ClothingMaskLayerData)MemberwiseClone();
            clone.OriginalPngBytes = OriginalPngBytes == null
                ? null
                : (byte[])OriginalPngBytes.Clone();
            clone.BoundItemIdentity = BoundItemIdentity == null
                ? null
                : BoundItemIdentity.DeepClone();
            return clone;
        }
    }

    public static class HashUtility
    {
        public static string Sha256(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
