using System;
using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class LegacyMaskDescriptor
    {
        public const string ProviderIdValue = "nakay.kk.ChaAlphaMask";
        public const string ContractVersionValue = "nakay-rgb-state-selector-v1";

        public int SourceOrder;
        public string Game = "Koikatsu";
        public string ModGuid;
        public string ArchivePath;
        public ClothingSlot Slot;
        public int Category;
        public int OriginalItemId;
        public int ResolvedItemId;
        public bool BotMask;
        public bool? ObjectOption01;
        public bool? ObjectOption02;
        public string PngPath;
        public string AssetBundlePath;
        public string PrefabName;
        public string MaskAssetName;
        public string MetadataFingerprint;

        public bool IsPng
        {
            get { return !string.IsNullOrEmpty(PngPath); }
        }

        public string BuildCanonicalIdentity(GradientHandlingMode gradientMode)
        {
            StringBuilder value = new StringBuilder(256);
            Append(value, ProviderIdValue);
            Append(value, ContractVersionValue);
            Append(value, Game);
            Append(value, ((int)Slot).ToString());
            Append(value, Category.ToString());
            Append(value, OriginalItemId.ToString());
            Append(value, ResolvedItemId.ToString());
            Append(value, ModGuid);
            Append(value, AssetBundlePath);
            Append(value, PrefabName);
            Append(value, MaskAssetName);
            Append(value, PngPath);
            Append(value, BotMask ? "1" : "0");
            Append(value, NullableBool(ObjectOption01));
            Append(value, NullableBool(ObjectOption02));
            Append(value, ((int)gradientMode).ToString());
            return value.ToString();
        }

        public string BuildFingerprint(GradientHandlingMode gradientMode, string contentHash)
        {
            string canonical = BuildCanonicalIdentity(gradientMode) + "|" + (contentHash ?? string.Empty);
            return HashUtility.Sha256(Encoding.UTF8.GetBytes(canonical));
        }

        public LegacyMaskDescriptor CloneForSlot(ClothingSlot slot)
        {
            LegacyMaskDescriptor clone = (LegacyMaskDescriptor)MemberwiseClone();
            clone.Slot = slot;
            return clone;
        }

        private static void Append(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }

        private static string NullableBool(bool? value)
        {
            if (!value.HasValue)
            {
                return "null";
            }

            return value.Value ? "true" : "false";
        }
    }

    public sealed class LegacyMaskBindingQuery
    {
        public ClothingSlot Slot;
        public int Category;
        public int OriginalItemId;
        public int ResolvedItemId;
        public string SideloaderGuid;
        public bool BotMask;
        public bool? ObjectOption01;
        public bool? ObjectOption02;

        public string BuildLookupKey()
        {
            return Category + ":" + ResolvedItemId;
        }

        internal string BuildCacheKey()
        {
            return (int)Slot + "|" + Category + "|" + OriginalItemId + "|" +
                   ResolvedItemId + "|" + (SideloaderGuid ?? string.Empty) + "|" +
                   BotMask + "|" + NullableBool(ObjectOption01) + "|" +
                   NullableBool(ObjectOption02);
        }

        private static string NullableBool(bool? value)
        {
            return !value.HasValue ? "null" : (value.Value ? "1" : "0");
        }
    }

    public sealed class LegacyManifestSource
    {
        public string ModGuid;
        public string ArchivePath;
        public long ArchiveLength;
        public long ArchiveLastWriteUtcTicks;
        public uint ManifestCrc;
        public string ManifestXml;

        public string BuildChangeKey()
        {
            if (!string.IsNullOrEmpty(ArchivePath) &&
                (ArchiveLength != 0 || ArchiveLastWriteUtcTicks != 0 || ManifestCrc != 0))
            {
                return ArchivePath + "|" + ArchiveLength + "|" +
                       ArchiveLastWriteUtcTicks + "|" + ManifestCrc;
            }

            byte[] xml = Encoding.UTF8.GetBytes(ManifestXml ?? string.Empty);
            return "xml|" + HashUtility.Sha256(xml);
        }
    }

    public sealed class LegacyIndexRefreshResult
    {
        public bool Changed;
        public int Generation;
        public int ManifestCount;
        public int ReusedManifestCount;
        public int ParsedManifestCount;
        public int DescriptorCount;
        public int RejectedEntryCount;
    }

    public static class LegacyCompatibilityPolicy
    {
        public static bool ShouldUseDirectProvider(
            bool compatibilityEnabled,
            bool oldPluginInstalled)
        {
            return compatibilityEnabled && !oldPluginInstalled;
        }

        public static bool NativeFingerprintSuppressesLegacy(
            string nativeFingerprint,
            bool nativeOwnsSource,
            string legacyFingerprint)
        {
            return nativeOwnsSource && !string.IsNullOrEmpty(nativeFingerprint) &&
                   !string.IsNullOrEmpty(legacyFingerprint) &&
                   string.Equals(
                       nativeFingerprint,
                       legacyFingerprint,
                       StringComparison.Ordinal);
        }

        public static bool NativeLayerOwnsLegacySource(
            ClothingMaskLayerData nativeLayer,
            bool decodedMaskAvailable,
            bool bindingMatches,
            string legacyFingerprint)
        {
            return nativeLayer != null && decodedMaskAvailable && bindingMatches &&
                   nativeLayer.SourceContract == MaskSourceContract.NakayRgbStateCoverage &&
                   string.Equals(
                       nativeLayer.SourceProviderId,
                       LegacyMaskDescriptor.ProviderIdValue,
                       StringComparison.OrdinalIgnoreCase) &&
                   NativeFingerprintSuppressesLegacy(
                       nativeLayer.SourceFingerprint,
                       true,
                       legacyFingerprint);
        }

        public static bool UpstreamPluginSuppressesConvertedNative(
            bool oldPluginInstalled,
            MaskSourceContract sourceContract,
            string sourceProviderId)
        {
            return oldPluginInstalled &&
                   sourceContract == MaskSourceContract.NakayRgbStateCoverage &&
                   string.Equals(
                       sourceProviderId,
                       LegacyMaskDescriptor.ProviderIdValue,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
