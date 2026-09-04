using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class NativeMaskDecodePipelineTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("Native decode rejects wrong expected hash before cache and decoder", ExpectedHashMismatchShortCircuits),
                new TestCase("Native decode reuses and revalidates a valid cache entry", ValidCacheIsReusedAndRevalidated),
                new TestCase("Native decode falls back when cached PNG validation fails", InvalidCacheFallsBackToDecoder),
                new TestCase("Native decode preserves decoder failure diagnostics", DecoderFailurePreservesDiagnostics),
                new TestCase("Native decode rejects incompatible versions and null bytes", InvalidInputPreconditions),
                new TestCase("Native decode applies the explicit dirty-cache policy", DirtyCachePolicy)
            };
        }

        private static void ExpectedHashMismatchShortCircuits()
        {
            byte[] bytes = new byte[] { 1, 2, 3, 4 };
            string actualHash = HashUtility.Sha256(bytes);
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            SeedCache(
                store,
                ClothingSlot.Top,
                bytes,
                MaskSourceContract.Native,
                GradientHandlingMode.StrictCategorical,
                CreateMask(MaskPixelRule.HideWhenFull),
                new MaskColorStatistics { YellowPixels = 1 });
            StubDecoder decoder = SuccessfulDecoder(
                CreateMask(MaskPixelRule.HideWhenNotOff),
                new MaskColorStatistics { RedPixels = 1 },
                0);
            StubValidator validator = ValidValidator();

            NativeMaskDecodeResult result =
                new NativeMaskDecodePipeline(store, decoder, validator).Decode(
                    Request(
                        bytes,
                        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                        1,
                        MaskSourceContract.Native,
                        GradientHandlingMode.StrictCategorical,
                        false));

            Check.False(result.Success, "A metadata hash mismatch must reject the PNG.");
            Check.False(result.ReusedDecode, "A rejected PNG must not report cache reuse.");
            Check.Equal(actualHash, result.Hash, "The computed hash must remain available.");
            Check.Equal(
                "Stored PNG SHA-256 does not match its metadata.",
                result.Error,
                "Hash mismatch text must remain exact.");
            Check.Equal(
                "Invalid: Stored PNG SHA-256 does not match its metadata.",
                result.BuildValidationDescription(),
                "Invalid hash validation text must remain exact.");
            Check.Null(result.SemanticMask, "A rejected PNG must not publish a semantic mask.");
            Check.Equal(0, decoder.CallCount, "Hash rejection must happen before decoding.");
            Check.Equal(0, validator.CallCount, "Hash rejection must happen before cache validation.");
        }

        private static void ValidCacheIsReusedAndRevalidated()
        {
            byte[] bytes = new byte[] { 8, 6, 7, 5, 3, 0, 9 };
            SemanticMask cachedMask = CreateMask(MaskPixelRule.HideWhenNotOff);
            MaskColorStatistics cachedStatistics = new MaskColorStatistics
            {
                GreenPixels = 3,
                EdgePixels = 2
            };
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            SeedCache(
                store,
                ClothingSlot.Gloves,
                bytes,
                MaskSourceContract.ExternalRgbStateCoverage,
                GradientHandlingMode.PreserveContinuous,
                cachedMask,
                cachedStatistics);
            StubDecoder decoder = SuccessfulDecoder(
                CreateMask(MaskPixelRule.NeverHide),
                new MaskColorStatistics(),
                0);
            MaskValidationResult validation = ValidValidation("Cached PNG is valid.");
            StubValidator validator = new StubValidator(validation);
            string hash = HashUtility.Sha256(bytes);

            NativeMaskDecodeResult result =
                new NativeMaskDecodePipeline(store, decoder, validator).Decode(
                    Request(
                        bytes,
                        hash.ToUpperInvariant(),
                        1,
                        MaskSourceContract.ExternalRgbStateCoverage,
                        GradientHandlingMode.PreserveContinuous,
                        false));

            Check.True(result.Success, "A valid identical cache entry must succeed.");
            Check.True(result.ReusedDecode, "An identical cache entry must report reuse.");
            Check.Same(cachedMask, result.SemanticMask, "SemanticMask must be shared on reuse.");
            Check.NotSame(cachedStatistics, result.Statistics, "Cached statistics must be cloned.");
            Check.Equal(3, result.Statistics.GreenPixels, "Cached statistics values must survive.");
            Check.Same(validation, result.Validation, "Fresh cache validation must be returned.");
            Check.Equal(1, validator.CallCount, "Every cache hit must be revalidated.");
            Check.Same(bytes, validator.LastBytes, "Revalidation must inspect the supplied bytes.");
            Check.Equal(0, decoder.CallCount, "A valid cache hit must avoid decoding.");
            Check.Equal(
                "Valid using cached identical decode: yellow=0, green=3, black=0, red=0, blue=0, unknown=0, continuous=0, edge=2, ambiguous=0, packed=0, blueData=0, external=0, alphaUnexpected=0",
                result.BuildValidationDescription(),
                "Cached validation text must remain exact.");
        }

        private static void InvalidCacheFallsBackToDecoder()
        {
            byte[] bytes = new byte[] { 9, 9, 9 };
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            SeedCache(
                store,
                ClothingSlot.Bottom,
                bytes,
                MaskSourceContract.Native,
                GradientHandlingMode.StrictCategorical,
                CreateMask(MaskPixelRule.HideWhenFull),
                new MaskColorStatistics { YellowPixels = 1 });
            SemanticMask decodedMask = CreateMask(MaskPixelRule.HideWhenNotOff);
            MaskColorStatistics decodedStatistics = new MaskColorStatistics
            {
                RedPixels = 4,
                ContinuousPixels = 2
            };
            MaskValidationResult decodedValidation = ValidValidation("Decoded after normalization.");
            StubDecoder decoder = new StubDecoder(
                true,
                decodedMask,
                decodedStatistics,
                decodedValidation,
                7,
                null);
            StubValidator validator = new StubValidator(
                new MaskValidationResult
                {
                    IsValid = false,
                    Message = "Cached bytes failed validation."
                });

            NativeMaskDecodeResult result =
                new NativeMaskDecodePipeline(store, decoder, validator).Decode(
                    Request(
                        bytes,
                        HashUtility.Sha256(bytes),
                        1,
                        MaskSourceContract.Native,
                        GradientHandlingMode.StrictCategorical,
                        false));

            Check.True(result.Success, "A valid fresh decode must recover from invalid cache.");
            Check.False(result.ReusedDecode, "An invalid cache entry must not report reuse.");
            Check.Equal(1, validator.CallCount, "The candidate cache hit must be validated once.");
            Check.Equal(1, decoder.CallCount, "Invalid cache validation must fall back to decoding.");
            Check.Same(decodedMask, result.SemanticMask, "Fresh semantic output must be returned.");
            Check.Same(decodedStatistics, result.Statistics, "Fresh statistics must be returned.");
            Check.Same(decodedValidation, result.Validation, "Fresh validation must replace cache failure.");
            Check.Equal(7, result.NormalizedPixelCount, "Normalization count must be returned.");
            Check.Null(result.Error, "A successful fallback must clear the cache error.");
            Check.Equal(
                "Valid after palette normalization (7 pixels): yellow=0, green=0, black=0, red=4, blue=0, unknown=0, continuous=2, edge=0, ambiguous=0, packed=0, blueData=0, external=0, alphaUnexpected=0",
                result.BuildValidationDescription(),
                "Normalized validation text must remain exact.");
        }

        private static void DecoderFailurePreservesDiagnostics()
        {
            byte[] bytes = new byte[] { 4, 2 };
            MaskColorStatistics statistics = new MaskColorStatistics
            {
                UnknownPixels = 5,
                UnexpectedAlphaPixels = 1
            };
            MaskValidationResult validation = new MaskValidationResult
            {
                IsValid = false,
                Width = 128,
                Height = 128,
                Message = "Unknown colors were found."
            };
            StubDecoder decoder = new StubDecoder(
                false,
                CreateMask(MaskPixelRule.HideWhenFull),
                statistics,
                validation,
                11,
                "Decoder rejected sample.");
            StubValidator validator = ValidValidator();

            NativeMaskDecodeResult result =
                new NativeMaskDecodePipeline(
                    new NativeMaskLayerStore(),
                    decoder,
                    validator).Decode(
                        Request(
                            bytes,
                            null,
                            1,
                            MaskSourceContract.Native,
                            GradientHandlingMode.StrictCategorical,
                            false));

            Check.False(result.Success, "A decoder failure must remain failed.");
            Check.False(result.ReusedDecode, "A decoder failure cannot report cache reuse.");
            Check.Equal(HashUtility.Sha256(bytes), result.Hash, "Failure must retain the hash.");
            Check.Same(statistics, result.Statistics, "Failure statistics must be preserved.");
            Check.Same(validation, result.Validation, "Failure validation must be preserved.");
            Check.Equal(11, result.NormalizedPixelCount, "Failure normalization stats must survive.");
            Check.Equal("Decoder rejected sample.", result.Error, "Failure text must survive.");
            Check.Null(result.SemanticMask, "Failure must not publish incomplete semantic output.");
            Check.Equal(1, decoder.CallCount, "A cache miss must invoke the decoder once.");
            Check.Equal(0, validator.CallCount, "Standalone validation is only for cache hits.");
            Check.Equal(
                "Invalid: Decoder rejected sample.",
                result.BuildValidationDescription(),
                "Invalid decoder validation text must remain exact.");
        }

        private static void InvalidInputPreconditions()
        {
            StubDecoder decoder = SuccessfulDecoder(
                CreateMask(MaskPixelRule.NeverHide),
                new MaskColorStatistics(),
                0);
            StubValidator validator = ValidValidator();
            NativeMaskDecodePipeline pipeline = new NativeMaskDecodePipeline(
                new NativeMaskLayerStore(),
                decoder,
                validator);

            NativeMaskDecodeResult incompatible = pipeline.Decode(
                Request(
                    new byte[] { 1 },
                    null,
                    2,
                    MaskSourceContract.Native,
                    GradientHandlingMode.StrictCategorical,
                    false));
            Check.False(incompatible.Success, "An incompatible version must be rejected.");
            Check.Null(incompatible.Hash, "Version rejection must happen before hashing.");
            Check.Equal(
                "Unsupported mask color format version 2.",
                incompatible.Error,
                "Version error text must remain exact.");
            Check.Equal(
                "Invalid: Unsupported mask color format version 2.",
                incompatible.BuildValidationDescription(),
                "Version validation text must remain exact.");

            NativeMaskDecodeResult missing = pipeline.Decode(
                Request(
                    null,
                    null,
                    1,
                    MaskSourceContract.Native,
                    GradientHandlingMode.StrictCategorical,
                    false));
            Check.False(missing.Success, "Null PNG bytes must be rejected.");
            Check.Null(missing.Hash, "Null PNG bytes must not be hashed.");
            Check.Equal(
                "Layer or PNG bytes are missing.",
                missing.Error,
                "Missing PNG error text must remain exact.");
            Check.Equal(
                "Invalid: Layer or PNG bytes are missing.",
                missing.BuildValidationDescription(),
                "Missing PNG validation text must remain exact.");
            Check.Equal(0, decoder.CallCount, "Precondition failures must avoid decoding.");
            Check.Equal(0, validator.CallCount, "Precondition failures must avoid validation.");
            Check.Throws<ArgumentNullException>(
                delegate { pipeline.Decode(null); },
                "A null request must be rejected explicitly.");
        }

        private static void DirtyCachePolicy()
        {
            byte[] bytes = new byte[] { 3, 1, 4, 1, 5 };
            string hash = HashUtility.Sha256(bytes);
            SemanticMask cachedMask = CreateMask(MaskPixelRule.HideWhenFull);
            MaskColorStatistics cachedStatistics = new MaskColorStatistics { BlackPixels = 1 };
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            SeedCache(
                store,
                ClothingSlot.Pantyhose,
                bytes,
                MaskSourceContract.Native,
                GradientHandlingMode.StrictCategorical,
                cachedMask,
                cachedStatistics);
            store.MarkDecodeConfigurationDirty();

            SemanticMask directMask;
            MaskColorStatistics directStatistics;
            Check.True(
                store.TryReuseDecoded(
                    hash,
                    MaskSourceContract.Native,
                    GradientHandlingMode.StrictCategorical,
                    out directMask,
                    out directStatistics),
                "The store must expose matching cache state to policy-aware callers.");
            Check.Same(cachedMask, directMask, "Dirty store lookup must return the cached mask.");
            Check.Equal(1, directStatistics.BlackPixels, "Dirty store lookup must clone statistics.");

            SemanticMask decodedMask = CreateMask(MaskPixelRule.HideWhenNotOff);
            MaskColorStatistics decodedStatistics = new MaskColorStatistics { BluePixels = 2 };
            StubDecoder decoder = SuccessfulDecoder(decodedMask, decodedStatistics, 0);
            StubValidator validator = ValidValidator();
            NativeMaskDecodePipeline pipeline =
                new NativeMaskDecodePipeline(store, decoder, validator);

            NativeMaskDecodeResult defaultResult = pipeline.Decode(
                Request(
                    bytes,
                    hash,
                    1,
                    MaskSourceContract.Native,
                    GradientHandlingMode.StrictCategorical,
                    false));
            Check.True(defaultResult.Success, "Dirty default policy must allow a fresh decode.");
            Check.False(defaultResult.ReusedDecode, "Import must block dirty-cache reuse by default.");
            Check.Same(decodedMask, defaultResult.SemanticMask, "Import must return fresh output.");
            Check.Equal(1, decoder.CallCount, "Blocked dirty-cache reuse must decode once.");
            Check.Equal(0, validator.CallCount, "A policy-blocked cache is not revalidated.");
            Check.Equal(
                "Valid: yellow=0, green=0, black=0, red=0, blue=2, unknown=0, continuous=0, edge=0, ambiguous=0, packed=0, blueData=0, external=0, alphaUnexpected=0",
                defaultResult.BuildValidationDescription(),
                "Ordinary successful validation text must remain exact.");

            NativeMaskDecodeResult allowedResult = pipeline.Decode(
                Request(
                    bytes,
                    hash,
                    1,
                    MaskSourceContract.Native,
                    GradientHandlingMode.StrictCategorical,
                    true));
            Check.True(allowedResult.Success, "Load/merge may explicitly reuse a dirty cache.");
            Check.True(allowedResult.ReusedDecode, "Explicit dirty reuse must be reported.");
            Check.Same(cachedMask, allowedResult.SemanticMask, "Explicit reuse must return cache.");
            Check.Equal(1, decoder.CallCount, "Explicit reuse must avoid another decode.");
            Check.Equal(1, validator.CallCount, "Explicit reuse must still validate the PNG.");
        }

        private static NativeMaskDecodeRequest Request(
            byte[] bytes,
            string expectedHash,
            int version,
            MaskSourceContract sourceContract,
            GradientHandlingMode gradientMode,
            bool allowDirtyReuse)
        {
            return new NativeMaskDecodeRequest
            {
                PngBytes = bytes,
                ExpectedHash = expectedHash,
                ColorFormatVersion = version,
                AllowReuseWhileConfigurationDirty = allowDirtyReuse,
                SourceContract = sourceContract,
                GradientHandlingMode = gradientMode
            };
        }

        private static void SeedCache(
            NativeMaskLayerStore store,
            ClothingSlot slot,
            byte[] bytes,
            MaskSourceContract sourceContract,
            GradientHandlingMode gradientMode,
            SemanticMask mask,
            MaskColorStatistics statistics)
        {
            store.ReplaceOwned(
                slot,
                new ClothingMaskLayerData
                {
                    Slot = slot,
                    Enabled = true,
                    OriginalPngBytes = (byte[])bytes.Clone(),
                    Width = 1,
                    Height = 1,
                    Hash = HashUtility.Sha256(bytes),
                    ColorFormatVersion = 1,
                    SourceContract = sourceContract,
                    GradientHandlingMode = gradientMode
                });
            store.SetDecoded(slot, mask, statistics);
        }

        private static SemanticMask CreateMask(MaskPixelRule rule)
        {
            return new SemanticMask(1, 1, new MaskPixelRule[] { rule });
        }

        private static MaskValidationResult ValidValidation(string message)
        {
            return new MaskValidationResult
            {
                IsValid = true,
                Width = 1,
                Height = 1,
                Message = message
            };
        }

        private static StubDecoder SuccessfulDecoder(
            SemanticMask mask,
            MaskColorStatistics statistics,
            int normalizedPixelCount)
        {
            return new StubDecoder(
                true,
                mask,
                statistics,
                ValidValidation("Decoded."),
                normalizedPixelCount,
                null);
        }

        private static StubValidator ValidValidator()
        {
            return new StubValidator(ValidValidation("PNG header is valid."));
        }

        private sealed class StubDecoder : IMaskPngDecoder
        {
            private readonly bool result;
            private readonly SemanticMask mask;
            private readonly MaskColorStatistics statistics;
            private readonly MaskValidationResult validation;
            private readonly int normalizedPixelCount;
            private readonly string error;

            public StubDecoder(
                bool decodeResult,
                SemanticMask decodedMask,
                MaskColorStatistics decodedStatistics,
                MaskValidationResult decodedValidation,
                int decodedNormalizedPixelCount,
                string decodedError)
            {
                result = decodeResult;
                mask = decodedMask;
                statistics = decodedStatistics;
                validation = decodedValidation;
                normalizedPixelCount = decodedNormalizedPixelCount;
                error = decodedError;
            }

            public int CallCount { get; private set; }

            public bool TryDecode(
                byte[] pngBytes,
                MaskSourceContract sourceContract,
                GradientHandlingMode gradientHandlingMode,
                out SemanticMask decodedMask,
                out MaskColorStatistics decodedStatistics,
                out MaskValidationResult decodedValidation,
                out int decodedNormalizedPixelCount,
                out string decodedError)
            {
                CallCount++;
                decodedMask = mask;
                decodedStatistics = statistics;
                decodedValidation = validation;
                decodedNormalizedPixelCount = normalizedPixelCount;
                decodedError = error;
                return result;
            }
        }

        private sealed class StubValidator : IMaskPngValidator
        {
            private readonly MaskValidationResult result;

            public StubValidator(MaskValidationResult validationResult)
            {
                result = validationResult;
            }

            public int CallCount { get; private set; }

            public byte[] LastBytes { get; private set; }

            public MaskValidationResult Validate(byte[] pngBytes)
            {
                CallCount++;
                LastBytes = pngBytes;
                return result;
            }
        }
    }
}
