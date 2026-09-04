using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class PortableExternalLayer
    {
        public ClothingMaskLayerData Layer;
        public SemanticMask SemanticMask;
        public MaskColorStatistics Statistics;
    }

    internal sealed class ExternalPortableLayerConverter
    {
        public bool TryConvert(
            int index,
            ExternalResolvedMask external,
            bool structurallySuppressed,
            ClothingMaskLayerData existingNativeLayer,
            ClothingItemIdentity currentIdentity,
            out PortableExternalLayer converted,
            out string result)
        {
            converted = null;
            result = null;
            if (external == null || external.SemanticMask == null)
            {
                result = "No active external source is available for conversion.";
                return false;
            }

            if (index <= (int)ClothingSlot.Top ||
                index >= ClothingSlotRegistry.SlotCount ||
                external.Descriptor == null ||
                string.IsNullOrEmpty(external.Fingerprint))
            {
                result = "The external source does not contain enough portable provenance data.";
                return false;
            }

            if (structurallySuppressed)
            {
                result = "Integrated external masks remain available in compatibility mode, but the current native binding schema cannot preserve this integrated-slot relationship safely.";
                return false;
            }

            if (existingNativeLayer != null)
            {
                result = "A native layer already exists. Clear it explicitly before converting.";
                return false;
            }

            ExternalMaskDescriptor descriptor = external.Descriptor;
            if (descriptor.ObjectOption01.HasValue || descriptor.ObjectOption02.HasValue)
            {
                result = "This external mask depends on clothing object options. It remains available in compatibility mode because the current native schema cannot preserve that conditional binding safely.";
                return false;
            }

            if (currentIdentity == null || !currentIdentity.HasItem)
            {
                result = "The current clothing item is no longer bindable.";
                return false;
            }

            int width = external.SemanticMask.Width;
            int height = external.SemanticMask.Height;
            if (width != height)
            {
                result = "This rectangular external mask works in compatibility mode but cannot be stored in the current square-PNG schema.";
                return false;
            }

            int pixelCount;
            if (!MaskDimensions.TryGetPixelCount(width, height, out pixelCount) ||
                width > PortableMaskFormatLimits.MaximumDimension ||
                height > PortableMaskFormatLimits.MaximumDimension ||
                (width & (width - 1)) != 0)
            {
                result = "The external mask dimensions are not supported by the portable card-data format.";
                return false;
            }

            byte[] pngBytes;
            Texture2D encoded = null;
            try
            {
                int count = external.SemanticMask.PixelCount;
                Color32[] colors = new Color32[count];
                for (int pixelIndex = 0; pixelIndex < count; pixelIndex++)
                {
                    colors[pixelIndex] = new Color32(
                        external.SemanticMask.GetHideCoverageForRawState(pixelIndex, 0),
                        external.SemanticMask.GetHideCoverageForRawState(pixelIndex, 1),
                        external.SemanticMask.GetHideCoverageForRawState(pixelIndex, 2),
                        byte.MaxValue);
                }

                encoded = new Texture2D(
                    external.SemanticMask.Width,
                    external.SemanticMask.Height,
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
                LastValidationResult = "Valid portable conversion: " + external.Statistics,
                SourceContract = MaskSourceContract.ExternalRgbStateCoverage,
                GradientHandlingMode = GradientHandlingMode.PreserveContinuous,
                SourceProviderId = ExternalMaskDescriptor.ProviderIdValue,
                SourceFingerprint = external.Fingerprint,
                SourceAsset = GetAssetDescription(descriptor)
            };
            MaskValidationResult convertedValidation = PngMaskValidator.Validate(pngBytes);
            if (!convertedValidation.IsValid)
            {
                result = "The portable conversion is not a supported PNG: " +
                         convertedValidation.Message;
                return false;
            }

            converted = new PortableExternalLayer
            {
                Layer = layer,
                SemanticMask = external.SemanticMask,
                Statistics = external.Statistics.Clone()
            };
            result = "Converted to a portable native layer.";
            return true;
        }

        public static string GetAssetDescription(ExternalMaskDescriptor descriptor)
        {
            return descriptor.IsPng
                ? descriptor.PngPath
                : (descriptor.AssetBundlePath ?? string.Empty) + "::" +
                  (descriptor.MaskAssetName ?? string.Empty);
        }
    }
}
