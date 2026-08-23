using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class PortableLegacyLayer
    {
        public ClothingMaskLayerData Layer;
        public SemanticMask SemanticMask;
        public MaskColorStatistics Statistics;
    }

    internal sealed class LegacyPortableLayerConverter
    {
        public bool TryConvert(
            int index,
            LegacyResolvedMask legacy,
            bool structurallySuppressed,
            ClothingMaskLayerData existingNativeLayer,
            ClothingItemIdentity currentIdentity,
            out PortableLegacyLayer converted,
            out string result)
        {
            converted = null;
            result = null;
            if (legacy == null || legacy.SemanticMask == null)
            {
                result = "No active legacy source is available for conversion.";
                return false;
            }

            if (index <= (int)ClothingSlot.Top ||
                index >= ClothingSlotRegistry.SlotCount ||
                legacy.Descriptor == null ||
                string.IsNullOrEmpty(legacy.Fingerprint))
            {
                result = "The legacy source does not contain enough portable provenance data.";
                return false;
            }

            if (structurallySuppressed)
            {
                result = "Integrated legacy masks remain available in compatibility mode, but the current native binding schema cannot preserve this integrated-slot relationship safely.";
                return false;
            }

            if (existingNativeLayer != null)
            {
                result = "A native layer already exists. Clear it explicitly before converting.";
                return false;
            }

            LegacyMaskDescriptor descriptor = legacy.Descriptor;
            if (descriptor.ObjectOption01.HasValue || descriptor.ObjectOption02.HasValue)
            {
                result = "This legacy mask depends on clothing object options. It remains available in compatibility mode because the current native schema cannot preserve that conditional binding safely.";
                return false;
            }

            if (currentIdentity == null || !currentIdentity.HasItem)
            {
                result = "The current clothing item is no longer bindable.";
                return false;
            }

            int width = legacy.SemanticMask.Width;
            int height = legacy.SemanticMask.Height;
            if (width != height)
            {
                result = "This rectangular legacy mask works in compatibility mode but cannot be stored in the current square-PNG schema.";
                return false;
            }

            int pixelCount;
            if (!MaskDimensions.TryGetPixelCount(width, height, out pixelCount) ||
                width > PortableMaskFormatLimits.MaximumDimension ||
                height > PortableMaskFormatLimits.MaximumDimension ||
                (width & (width - 1)) != 0)
            {
                result = "The legacy mask dimensions are not supported by the portable card-data format.";
                return false;
            }

            byte[] pngBytes;
            Texture2D encoded = null;
            try
            {
                int count = legacy.SemanticMask.PixelCount;
                Color32[] colors = new Color32[count];
                for (int pixelIndex = 0; pixelIndex < count; pixelIndex++)
                {
                    colors[pixelIndex] = new Color32(
                        legacy.SemanticMask.GetHideCoverageForRawState(pixelIndex, 0),
                        legacy.SemanticMask.GetHideCoverageForRawState(pixelIndex, 1),
                        legacy.SemanticMask.GetHideCoverageForRawState(pixelIndex, 2),
                        byte.MaxValue);
                }

                encoded = new Texture2D(
                    legacy.SemanticMask.Width,
                    legacy.SemanticMask.Height,
                    TextureFormat.RGBA32,
                    false);
                BodyMaskPerformanceMetrics.RecordNewTextureAllocation();
                encoded.SetPixels32(colors);
                encoded.Apply(false, false);
                pngBytes = encoded.EncodeToPNG();
            }
            catch (Exception exception)
            {
                result = "Could not encode the portable native copy: " + exception.Message;
                return false;
            }
            finally
            {
                if (encoded != null)
                {
                    UnityEngine.Object.Destroy(encoded);
                }
            }

            if (pngBytes == null || pngBytes.Length == 0 ||
                pngBytes.Length > PortableMaskFormatLimits.MaximumPngBytes)
            {
                result = "The converted PNG cannot be stored in the portable card-data format.";
                return false;
            }

            ClothingItemIdentity binding = currentIdentity.DeepClone();
            ClothingMaskLayerData layer = new ClothingMaskLayerData
            {
                Slot = (ClothingSlot)index,
                Enabled = true,
                OriginalPngBytes = pngBytes,
                Width = width,
                Height = height,
                Hash = HashUtility.Sha256(pngBytes),
                BoundItemIdentity = binding,
                ColorFormatVersion = 1,
                CreatedWithPluginVersion = BodyMaskLayersPlugin.PluginVersion,
                LastValidationResult = "Valid portable conversion: " + legacy.Statistics,
                SourceContract = MaskSourceContract.NakayRgbStateCoverage,
                GradientHandlingMode = GradientHandlingMode.PreserveContinuous,
                SourceProviderId = LegacyMaskDescriptor.ProviderIdValue,
                SourceFingerprint = legacy.Fingerprint,
                SourceAsset = GetAssetDescription(descriptor)
            };
            MaskValidationResult convertedValidation = PngMaskValidator.Validate(pngBytes);
            if (!convertedValidation.IsValid)
            {
                result = "The portable conversion is not a supported PNG: " +
                         convertedValidation.Message;
                return false;
            }

            converted = new PortableLegacyLayer
            {
                Layer = layer,
                SemanticMask = legacy.SemanticMask,
                Statistics = legacy.Statistics.Clone()
            };
            result = "Converted to a portable native layer.";
            return true;
        }

        public static bool IsPortableLegacyLayer(ClothingMaskLayerData layer)
        {
            return layer != null &&
                   layer.SourceContract == MaskSourceContract.NakayRgbStateCoverage &&
                   string.Equals(
                       layer.SourceProviderId,
                       LegacyMaskDescriptor.ProviderIdValue,
                       StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrEmpty(layer.SourceFingerprint);
        }

        public static string GetAssetDescription(LegacyMaskDescriptor descriptor)
        {
            return descriptor.IsPng
                ? descriptor.PngPath
                : (descriptor.AssetBundlePath ?? string.Empty) + "::" +
                  (descriptor.MaskAssetName ?? string.Empty);
        }
    }
}
