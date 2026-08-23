using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal interface IMaskPngDecoder
    {
        bool TryDecode(
            byte[] pngBytes,
            MaskSourceContract sourceContract,
            GradientHandlingMode gradientHandlingMode,
            out SemanticMask semantic,
            out MaskColorStatistics statistics,
            out MaskValidationResult validation,
            out int normalizedPixelCount,
            out string error);
    }

    internal interface IMaskPngValidator
    {
        MaskValidationResult Validate(byte[] pngBytes);
    }

    internal sealed class NativeMaskDecodeRequest
    {
        public byte[] PngBytes { get; set; }

        public string ExpectedHash { get; set; }

        public int ColorFormatVersion { get; set; }

        public bool AllowReuseWhileConfigurationDirty { get; set; }

        public MaskSourceContract SourceContract { get; set; }

        public GradientHandlingMode GradientHandlingMode { get; set; }
    }

    internal sealed class NativeMaskDecodeResult
    {
        public bool Success { get; internal set; }

        public bool ReusedDecode { get; internal set; }

        public string Hash { get; internal set; }

        public SemanticMask SemanticMask { get; internal set; }

        public MaskColorStatistics Statistics { get; internal set; }

        public MaskValidationResult Validation { get; internal set; }

        public int NormalizedPixelCount { get; internal set; }

        public string Error { get; internal set; }

        public string BuildValidationDescription()
        {
            if (!Success)
            {
                return "Invalid: " + Error;
            }

            if (ReusedDecode)
            {
                return "Valid using cached identical decode: " + Statistics;
            }

            return NormalizedPixelCount == 0
                ? "Valid: " + Statistics
                : "Valid after palette normalization (" + NormalizedPixelCount +
                  " pixels): " + Statistics;
        }
    }

    internal sealed class NativeMaskDecodePipeline
    {
        private readonly NativeMaskLayerStore store;
        private readonly IMaskPngDecoder decoder;
        private readonly IMaskPngValidator validator;

        public NativeMaskDecodePipeline(
            NativeMaskLayerStore layerStore,
            IMaskPngDecoder pngDecoder,
            IMaskPngValidator pngValidator)
        {
            if (layerStore == null)
            {
                throw new ArgumentNullException("layerStore");
            }

            if (pngDecoder == null)
            {
                throw new ArgumentNullException("pngDecoder");
            }

            if (pngValidator == null)
            {
                throw new ArgumentNullException("pngValidator");
            }

            store = layerStore;
            decoder = pngDecoder;
            validator = pngValidator;
        }

        public NativeMaskDecodeResult Decode(NativeMaskDecodeRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            NativeMaskDecodeResult result = new NativeMaskDecodeResult
            {
                Error = "Layer or PNG bytes are missing."
            };
            if (request.ColorFormatVersion != 1)
            {
                result.Error = "Unsupported mask color format version " +
                               request.ColorFormatVersion + ".";
                return result;
            }

            if (request.PngBytes == null)
            {
                return result;
            }

            result.Hash = HashUtility.Sha256(request.PngBytes);
            if (!string.IsNullOrEmpty(request.ExpectedHash) &&
                !string.Equals(
                    request.ExpectedHash,
                    result.Hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "Stored PNG SHA-256 does not match its metadata.";
                return result;
            }

            SemanticMask semantic = null;
            MaskColorStatistics statistics = null;
            bool reusedDecode =
                (request.AllowReuseWhileConfigurationDirty ||
                 !store.DecodeConfigurationDirty) &&
                store.TryReuseDecoded(
                    result.Hash,
                    request.SourceContract,
                    request.GradientHandlingMode,
                    out semantic,
                    out statistics);
            MaskValidationResult validation = null;
            int normalizedPixelCount = 0;
            string error = result.Error;
            if (reusedDecode)
            {
                validation = validator.Validate(request.PngBytes);
                if (!validation.IsValid)
                {
                    reusedDecode = false;
                    semantic = null;
                    statistics = null;
                    error = validation.Message;
                }
            }

            if (!reusedDecode && (semantic == null || statistics == null) &&
                !decoder.TryDecode(
                    request.PngBytes,
                    request.SourceContract,
                    request.GradientHandlingMode,
                    out semantic,
                    out statistics,
                    out validation,
                    out normalizedPixelCount,
                    out error))
            {
                result.Statistics = statistics;
                result.Validation = validation;
                result.NormalizedPixelCount = normalizedPixelCount;
                result.Error = error;
                return result;
            }

            result.Success = true;
            result.ReusedDecode = reusedDecode;
            result.SemanticMask = semantic;
            result.Statistics = statistics;
            result.Validation = validation;
            result.NormalizedPixelCount = normalizedPixelCount;
            result.Error = null;
            return result;
        }
    }
}
