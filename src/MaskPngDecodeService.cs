using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class MaskPngDecodeService : IMaskPngDecoder
    {
        public bool TryDecode(
            byte[] pngBytes,
            MaskSourceContract sourceContract,
            GradientHandlingMode gradientHandlingMode,
            out SemanticMask semantic,
            out MaskColorStatistics statistics,
            out MaskValidationResult validation,
            out int normalizedPixelCount,
            out string error)
        {
            semantic = null;
            statistics = null;
            normalizedPixelCount = 0;
            error = null;
            PluginConfig settings = BodyMaskLayersPlugin.Settings;
            validation = PngMaskValidator.Validate(pngBytes);
            if (!validation.IsValid)
            {
                error = validation.Message;
                return false;
            }

            Texture2D decoded = null;
            try
            {
                decoded = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                BodyMaskPerformanceMetrics.RecordNewTextureAllocation();
                if (!decoded.LoadImage(pngBytes))
                {
                    error = "Unity could not decode the PNG image.";
                    return false;
                }

                if (decoded.width != validation.Width || decoded.height != validation.Height)
                {
                    error = "Decoded PNG dimensions do not match its validated IHDR header.";
                    return false;
                }

                Rgba32[] pixels = TexturePixelReader.Convert(decoded.GetPixels32());
                bool accepted;
                if (sourceContract == MaskSourceContract.NakayRgbStateCoverage)
                {
                    accepted = MaskColorDecoder.TryDecodeLegacyRgbStateCoverage(
                        pixels,
                        decoded.width,
                        decoded.height,
                        out semantic,
                        out statistics,
                        out error);
                }
                else
                {
                    MaskDecodeOptions options = settings.CreateDecodeOptions();
                    options.GradientHandlingMode = gradientHandlingMode;
                    accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                        pixels,
                        decoded.width,
                        decoded.height,
                        options,
                        out semantic,
                        out statistics,
                        out normalizedPixelCount,
                        out error);
                }

                BodyMaskPerformanceMetrics.RecordPngDecode(
                    accepted ? semantic : null,
                    sourceContract == MaskSourceContract.Native,
                    sourceContract == MaskSourceContract.NakayRgbStateCoverage ||
                    gradientHandlingMode != GradientHandlingMode.StrictCategorical);
                return accepted;
            }
            catch (Exception exception)
            {
                error = "PNG decoding failed: " + exception.Message;
                return false;
            }
            finally
            {
                if (decoded != null)
                {
                    UnityEngine.Object.Destroy(decoded);
                }
            }
        }

        public static void WarnAboutBluePixels(
            ClothingSlot slot,
            MaskColorStatistics statistics,
            bool accepted)
        {
            if (statistics == null || statistics.BluePixels <= 0)
            {
                return;
            }

            BodyMaskLayersPlugin.Log.LogWarning(string.Format(
                "{0} mask contains {1} blue categorical pixels. Blue may carry packed/unsupported " +
                "shader data; the mask was {2} under UnknownColorPolicy={3}.",
                ClothingSlotRegistry.GetDisplayName(slot),
                statistics.BluePixels,
                accepted ? "accepted" : "rejected",
                BodyMaskLayersPlugin.Settings.UnknownColorPolicy.Value));
        }
    }
}
