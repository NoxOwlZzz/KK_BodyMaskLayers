using System;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal static class ExternalMaskTextureCompiler
    {
        private static MethodInfo sideloaderGetPng;

        public static bool TryCompile(
            ExternalMaskDescriptor descriptor,
            out ExternalResolvedMask resolved,
            out string error)
        {
            resolved = null;
            error = null;
            Texture2D source = null;
            bool ownsSourceTexture = false;
            try
            {
                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.SourceLoads);
                if (descriptor.IsPng)
                {
                    MethodInfo getPng = GetSideloaderPngMethod();
                    if (getPng == null)
                    {
                        error = "Sideloader.GetPng is unavailable for the declared external source.";
                        return false;
                    }

                    BodyMaskPerformanceMetrics.Increment(PerformanceCounter.FileReads);
                    BodyMaskPerformanceMetrics.Increment(PerformanceCounter.ZipOpens);
                    source = getPng.Invoke(null, new object[]
                    {
                        descriptor.PngPath,
                        TextureFormat.RGB24,
                        true
                    }) as Texture2D;
                    ownsSourceTexture = source != null;
                    BodyMaskPerformanceMetrics.Increment(PerformanceCounter.PngDecodes);
                    BodyMaskPerformanceMetrics.Increment(
                        PerformanceCounter.NewTexture2DAllocations);
                }
                else
                {
                    BodyMaskPerformanceMetrics.Increment(
                        PerformanceCounter.AssetBundleOpens);
                    BodyMaskPerformanceMetrics.Increment(
                        PerformanceCounter.AssetBundleLoads);
                    source = CommonLib.LoadAsset<Texture2D>(
                        descriptor.AssetBundlePath,
                        descriptor.MaskAssetName,
                        false,
                        string.Empty);
                }

                if (source == null)
                {
                    error = "The declared external mask texture could not be loaded.";
                    return false;
                }

                int sourcePixelCount;
                if (!MaskDimensions.TryGetPixelCount(
                        source.width,
                        source.height,
                        out sourcePixelCount) ||
                    source.width > PortableMaskFormatLimits.MaximumDimension ||
                    source.height > PortableMaskFormatLimits.MaximumDimension)
                {
                    error = "External texture dimensions exceed the supported card-data resolution.";
                    return false;
                }

                Rgba32[] pixels;
                int width;
                int height;
                if (!TexturePixelReader.TryRead(
                        source,
                        out pixels,
                        out width,
                        out height,
                        out error))
                {
                    return false;
                }

                SemanticMask semantic;
                MaskColorStatistics statistics;
                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.GradientAnalyses);
                if (!MaskColorDecoder.TryDecodeExternalRgbStateCoverage(
                        pixels,
                        width,
                        height,
                        out semantic,
                        out statistics,
                        out error))
                {
                    return false;
                }

                BodyMaskPerformanceMetrics.Increment(
                    PerformanceCounter.ContinuousGradientDecodes);
                BodyMaskPerformanceMetrics.Increment(
                    semantic.IsBinary
                        ? PerformanceCounter.BinaryMasksCompiled
                        : PerformanceCounter.ContinuousMasksCompiled);
                BodyMaskPerformanceMetrics.Increment(
                    PerformanceCounter.Sha256Calculations);
                string contentHash = HashPixels(pixels);
                resolved = new ExternalResolvedMask
                {
                    Descriptor = descriptor,
                    SemanticMask = semantic,
                    Statistics = statistics,
                    ContentHash = contentHash,
                    Fingerprint = descriptor.BuildFingerprint(
                        GradientHandlingMode.PreserveContinuous,
                        contentHash)
                };
                return true;
            }
            catch (Exception exception)
            {
                error = "External mask load failed safely: " + exception.Message;
                return false;
            }
            finally
            {
                if (ownsSourceTexture && source != null)
                {
                    UnityEngine.Object.Destroy(source);
                }
            }
        }

        internal static string HashPixels(Rgba32[] pixels)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] chunk = new byte[4096];
                int used = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Rgba32 pixel = pixels[index];
                    chunk[used++] = pixel.R;
                    chunk[used++] = pixel.G;
                    chunk[used++] = pixel.B;
                    chunk[used++] = pixel.A;
                    if (used == chunk.Length)
                    {
                        hash.TransformBlock(chunk, 0, used, chunk, 0);
                        used = 0;
                    }
                }

                hash.TransformFinalBlock(chunk, 0, used);
                byte[] bytes = hash.Hash;
                System.Text.StringBuilder text =
                    new System.Text.StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                {
                    text.Append(bytes[index].ToString("x2"));
                }

                return text.ToString();
            }
        }

        private static MethodInfo GetSideloaderPngMethod()
        {
            if (sideloaderGetPng != null)
            {
                return sideloaderGetPng;
            }

            Type type = Type.GetType(GameTarget.SideloaderType, false);
            if (type == null)
            {
                return null;
            }

            sideloaderGetPng = type.GetMethod(
                "GetPng",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(string), typeof(TextureFormat), typeof(bool) },
                null);
            return sideloaderGetPng;
        }
    }
}
