using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class PureLogicTests
    {
        private const string Schema1FixtureBase64 =
            "Qk1MMQEAAAABAAAAAgAAAAAAAQAAAAEAAAEAAAABAwAAAAsAAABsZWdhY3ktaGFzaAUAAAAwLjEuMg4AAABsZWdhY3kgZml4dHVyZQECAAAAawAAAOEQAADSBAAACgAAAGxlZ2FjeS5tb2QEAAAAaXRlbQQAAABJSktM";
        private const string Schema1FixtureSha256 =
            "59f02b0530d7eebd7c73a2316f8d9d0f8c92763cd7f9f5a0db2a1405f51eacf6";

        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("states: raw 0/1/2/3 and every unknown byte", StateRawMapping),
                new TestCase("states: every unknown-state policy", StateUnknownPolicies),
                new TestCase("colors: canonical yellow/green/black/red", ColorCanonicalExact),
                new TestCase("colors: blue policies", ColorBluePolicies),
                new TestCase("colors: unknown policies", ColorUnknownPolicies),
                new TestCase("colors: exact, threshold and nearest modes", ColorClassificationModes),
                new TestCase("colors: compatible exported palette normalization", ColorPaletteCompatibility),
                new TestCase("colors: alpha statistics and invalid input", ColorAlphaAndInvalidInput),
                new TestCase("composer: complete rule/state truth table", ComposerTruthTable),
                new TestCase("composer: potential contribution fast-skip", ComposerPotentialContribution),
                new TestCase("composer: simultaneous OR and overlap", ComposerOrAndOverlap),
                new TestCase("composer: independent states and layer removal", ComposerIndependentStatesAndRemoval),
                new TestCase("composer: accumulation across resolutions", ComposerResampledAccumulation),
                new TestCase("nearest: source-coordinate mappings", ResolutionSourceCoordinates),
                new TestCase("nearest: 2x2 to 4x4 upscale", ResolutionUpscale),
                new TestCase("nearest: downscale and identity", ResolutionDownscaleAndIdentity),
                new TestCase("nearest: invalid arguments", ResolutionInvalidArguments),
                new TestCase("preview: categorical colors and bounded size", PreviewCategoricalAndBounded),
                new TestCase("format: vanilla/KKLT canonical formula", FormatCanonicalFormula),
                new TestCase("format: vanilla/KKLT threshold and fractional controls", FormatThresholdAndControls),
                new TestCase("format: binary write OR and B/A preservation", FormatWritePreservesChannels),
                new TestCase("format: nearest base sampling and top-off case", FormatWriteResamplesAndTopOff),
                new TestCase("format: no base texture and invalid buffers", FormatNoBaseAndErrors),
                new TestCase("PNG: valid color types and boundary sizes", PngValidTypesAndBounds),
                new TestCase("PNG: signature and IHDR structure", PngSignatureAndIhdr),
                new TestCase("PNG: structural dimensions and internal resolution limit", PngDimensions),
                new TestCase("PNG: bit depth, color type and internal byte limit", PngFormatAndByteLimits),
                new TestCase("dimensions: representable pixel counts", MaskDimensionPixelCounts),
                new TestCase("serializer: complete roundtrip", SerializerCompleteRoundTrip),
                new TestCase("serializer: BML1 golden migration defaults", SerializerSchema1Migration),
                new TestCase("serializer: multiple, null and duplicate layers", SerializerCollections),
                new TestCase("serializer: envelope corruption", SerializerEnvelopeCorruption),
                new TestCase("serializer: exhaustive truncation and layer corruption", SerializerLayerCorruption),
                new TestCase("serializer: hard limits", SerializerLimits),
                new TestCase("serializer: SHA-256 and deep clone", HashAndDeepClone),
                new TestCase("binding: strict local identity", BindingLocalIdentity),
                new TestCase("binding: strict Sideloader identity", BindingSideloaderIdentity)
            };
        }

        private static void StateRawMapping()
        {
            Check.Equal(GarmentState.Full, ClothingStateResolver.Resolve(0), "Raw state 0 must be Full.");
            Check.Equal(GarmentState.Partial, ClothingStateResolver.Resolve(1), "Raw state 1 must be Partial.");
            Check.Equal(GarmentState.Partial, ClothingStateResolver.Resolve(2), "Raw state 2 must be Partial.");
            Check.Equal(GarmentState.Off, ClothingStateResolver.Resolve(3), "Raw state 3 must be Off.");

            for (int raw = 4; raw <= byte.MaxValue; raw++)
            {
                Check.Equal(
                    GarmentState.Unknown,
                    ClothingStateResolver.Resolve((byte)raw),
                    "Every raw state above 3 must remain Unknown: " + raw + ".");
            }
        }

        private static void StateUnknownPolicies()
        {
            Check.Equal(
                GarmentState.Off,
                ClothingStateResolver.ApplyUnknownPolicy(
                    GarmentState.Unknown,
                    UnknownStatePolicy.NoContribution,
                    GarmentState.Full),
                "NoContribution must resolve Unknown to Off.");
            Check.Equal(
                GarmentState.Partial,
                ClothingStateResolver.ApplyUnknownPolicy(
                    GarmentState.Unknown,
                    UnknownStatePolicy.TreatAsPartial,
                    GarmentState.Off),
                "TreatAsPartial must resolve Unknown to Partial.");
            Check.Equal(
                GarmentState.Full,
                ClothingStateResolver.ApplyUnknownPolicy(
                    GarmentState.Unknown,
                    UnknownStatePolicy.TreatAsFull,
                    GarmentState.Off),
                "TreatAsFull must resolve Unknown to Full.");

            GarmentState[] lastKnownStates =
            {
                GarmentState.Full,
                GarmentState.Partial,
                GarmentState.Off,
                GarmentState.Unknown
            };
            GarmentState[] preservedExpected =
            {
                GarmentState.Full,
                GarmentState.Partial,
                GarmentState.Off,
                GarmentState.Off
            };
            for (int i = 0; i < lastKnownStates.Length; i++)
            {
                Check.Equal(
                    preservedExpected[i],
                    ClothingStateResolver.ApplyUnknownPolicy(
                        GarmentState.Unknown,
                        UnknownStatePolicy.PreserveLastKnown,
                        lastKnownStates[i]),
                    "PreserveLastKnown returned an unexpected result.");
            }

            GarmentState[] knownStates =
            {
                GarmentState.Full,
                GarmentState.Partial,
                GarmentState.Off
            };
            UnknownStatePolicy[] policies =
            {
                UnknownStatePolicy.NoContribution,
                UnknownStatePolicy.TreatAsPartial,
                UnknownStatePolicy.TreatAsFull,
                UnknownStatePolicy.PreserveLastKnown
            };
            for (int stateIndex = 0; stateIndex < knownStates.Length; stateIndex++)
            {
                for (int policyIndex = 0; policyIndex < policies.Length; policyIndex++)
                {
                    Check.Equal(
                        knownStates[stateIndex],
                        ClothingStateResolver.ApplyUnknownPolicy(
                            knownStates[stateIndex],
                            policies[policyIndex],
                            GarmentState.Off),
                        "Policies must not alter a recognized state.");
                }
            }
        }

        private static void ColorCanonicalExact()
        {
            Rgba32[] pixels =
            {
                new Rgba32(255, 255, 0, 255),
                new Rgba32(0, 255, 0, 255),
                new Rgba32(0, 0, 0, 255),
                new Rgba32(255, 0, 0, 255)
            };
            MaskColorStatistics statistics;
            SemanticMask mask = Decode(
                pixels,
                2,
                2,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out statistics);

            Check.SequenceEqual(
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide,
                    MaskPixelRule.HideWhenFull,
                    MaskPixelRule.HideWhenNotOff,
                    MaskPixelRule.HideWhenNotOff
                },
                mask.Rules,
                "Canonical colors decoded incorrectly.");
            Check.Equal(1, statistics.YellowPixels, "Yellow count mismatch.");
            Check.Equal(1, statistics.GreenPixels, "Green count mismatch.");
            Check.Equal(1, statistics.BlackPixels, "Black count mismatch.");
            Check.Equal(1, statistics.RedPixels, "Red count mismatch.");
            Check.Equal(0, statistics.BluePixels, "Blue count mismatch.");
            Check.Equal(0, statistics.UnknownPixels, "Unknown count mismatch.");
            Check.Equal(0, statistics.UnexpectedAlphaPixels, "Unexpected alpha count mismatch.");
            Check.Equal(4, statistics.TotalPixels, "Total pixel count mismatch.");
        }

        private static void ColorBluePolicies()
        {
            Rgba32[] blue = { new Rgba32(0, 0, 255, 255) };
            SemanticMask mask;
            MaskColorStatistics statistics;
            string error;

            bool accepted = MaskColorDecoder.TryDecode(
                blue,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "RejectMask must reject blue.");
            Check.Null(mask, "Rejected blue must not produce a semantic mask.");
            Check.Equal(1, statistics.BluePixels, "Blue must be counted separately.");
            Check.Contains("blue=1", error, "Blue rejection should be diagnostic.");

            mask = Decode(
                blue,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.NoContribution),
                out statistics);
            Check.Equal(MaskPixelRule.NeverHide, mask.Rules[0], "NoContribution must neutralize blue.");
            Check.Equal(1, statistics.BluePixels, "Blue count must survive neutralization.");

            mask = Decode(
                blue,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.HideWhenNotOff),
                out statistics);
            Check.Equal(MaskPixelRule.HideWhenNotOff, mask.Rules[0], "HideWhenNotOff must map blue conservatively.");

            Rgba32[] nearBlue = { new Rgba32(20, 20, 240, 255) };
            accepted = MaskColorDecoder.TryDecode(
                nearBlue,
                1,
                1,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "Blue-dominant pixels inside the detector limits must be rejected.");
            Check.Equal(1, statistics.BluePixels, "Near-blue count mismatch.");
        }

        private static void ColorUnknownPolicies()
        {
            Rgba32[] unknown = { new Rgba32(50, 60, 70, 255) };
            SemanticMask mask;
            MaskColorStatistics statistics;
            string error;

            bool accepted = MaskColorDecoder.TryDecode(
                unknown,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "RejectMask must reject an unknown RGB category.");
            Check.Null(mask, "Rejected unknown color must not produce a mask.");
            Check.Equal(1, statistics.UnknownPixels, "Unknown color count mismatch.");
            Check.Contains("unknown=1", error, "Unknown-color rejection should be diagnostic.");

            mask = Decode(
                unknown,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.NoContribution),
                out statistics);
            Check.Equal(MaskPixelRule.NeverHide, mask.Rules[0], "NoContribution must neutralize unknown RGB.");

            mask = Decode(
                unknown,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.HideWhenNotOff),
                out statistics);
            Check.Equal(MaskPixelRule.HideWhenNotOff, mask.Rules[0], "Conservative policy must hide unknown RGB while worn.");
        }

        private static void ColorClassificationModes()
        {
            MaskColorStatistics statistics;
            SemanticMask exact = Decode(
                new Rgba32[] { new Rgba32(250, 248, 2, 255) },
                1,
                1,
                Options(ColorClassificationMode.Exact, 12, UnknownColorPolicy.NoContribution),
                out statistics);
            Check.Equal(MaskPixelRule.NeverHide, exact.Rules[0], "Exact near-yellow is neutral under NoContribution.");
            Check.Equal(1, statistics.UnknownPixels, "Exact mode must not accept near-yellow.");

            Rgba32[] nearCanonical =
            {
                new Rgba32(250, 248, 2, 255),
                new Rgba32(4, 250, 5, 255),
                new Rgba32(5, 4, 3, 255),
                new Rgba32(250, 4, 4, 255)
            };
            SemanticMask threshold = Decode(
                nearCanonical,
                2,
                2,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out statistics);
            Check.SequenceEqual(
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide,
                    MaskPixelRule.HideWhenFull,
                    MaskPixelRule.HideWhenNotOff,
                    MaskPixelRule.HideWhenNotOff
                },
                threshold.Rules,
                "Threshold mode failed near canonical colors.");
            Check.Equal(0, statistics.UnknownPixels, "Threshold near-canonical pixels should all classify.");

            SemanticMask outsideThreshold = Decode(
                new Rgba32[] { new Rgba32(240, 255, 0, 255) },
                1,
                1,
                Options(ColorClassificationMode.Threshold, 5, UnknownColorPolicy.NoContribution),
                out statistics);
            Check.Equal(MaskPixelRule.NeverHide, outsideThreshold.Rules[0], "Out-of-threshold color must use unknown policy.");
            Check.Equal(1, statistics.UnknownPixels, "Out-of-threshold color must be counted unknown.");

            Rgba32[] nearestPixels =
            {
                new Rgba32(200, 200, 50, 255),
                new Rgba32(10, 180, 10, 255),
                new Rgba32(20, 20, 20, 255),
                new Rgba32(200, 20, 20, 255)
            };
            SemanticMask nearest = Decode(
                nearestPixels,
                2,
                2,
                Options(ColorClassificationMode.NearestCategory, 0, UnknownColorPolicy.RejectMask),
                out statistics);
            Check.SequenceEqual(
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide,
                    MaskPixelRule.HideWhenFull,
                    MaskPixelRule.HideWhenNotOff,
                    MaskPixelRule.HideWhenNotOff
                },
                nearest.Rules,
                "Nearest-category classification mismatch.");

            SemanticMask negativeTolerance = Decode(
                new Rgba32[]
                {
                    new Rgba32(255, 255, 0, 255),
                    new Rgba32(254, 255, 0, 255)
                },
                2,
                1,
                Options(ColorClassificationMode.Threshold, -50, UnknownColorPolicy.NoContribution),
                out statistics);
            Check.Equal(MaskPixelRule.NeverHide, negativeTolerance.Rules[0], "Tolerance clamps to zero but canonical remains valid.");
            Check.Equal(MaskPixelRule.NeverHide, negativeTolerance.Rules[1], "Near color uses neutral unknown policy at zero tolerance.");
            Check.Equal(1, statistics.YellowPixels, "Canonical yellow count mismatch at clamped tolerance.");
            Check.Equal(1, statistics.UnknownPixels, "Near-yellow must be unknown at clamped zero tolerance.");

            SemanticMask highTolerance = Decode(
                new Rgba32[] { new Rgba32(100, 100, 0, 255) },
                1,
                1,
                Options(ColorClassificationMode.Threshold, 999, UnknownColorPolicy.RejectMask),
                out statistics);
            Check.Equal(MaskPixelRule.HideWhenNotOff, highTolerance.Rules[0], "Tolerance above 255 must clamp and classify by nearest distance.");
        }

        private static void ColorPaletteCompatibility()
        {
            Rgba32[] compatible = new Rgba32[16];
            for (int i = 0; i < compatible.Length; i++)
            {
                compatible[i] = new Rgba32(255, 255, 0, 255);
            }

            compatible[0] = new Rgba32(76, 255, 0, 255);
            compatible[1] = new Rgba32(190, 255, 0, 255);

            SemanticMask mask;
            MaskColorStatistics statistics;
            int normalizedPixelCount;
            string error;
            bool accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                compatible,
                4,
                4,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out normalizedPixelCount,
                out error);
            Check.True(accepted, "A small exported-palette deviation should normalize safely.");
            Check.NotNull(mask, "A normalized palette must produce a semantic mask.");
            Check.Null(error, "Successful palette normalization must clear the decode error.");
            Check.Equal(1, normalizedPixelCount, "Normalized pixel count mismatch.");
            Check.Equal(15, statistics.YellowPixels, "Nearest compatible yellow count mismatch.");
            Check.Equal(1, statistics.GreenPixels, "Exported green variant should normalize to green.");
            Check.Equal(0, statistics.UnknownPixels, "Compatible normalization must resolve unknown colors.");
            Check.Equal(
                MaskPixelRule.HideWhenFull,
                mask.Rules[0],
                "The exported green variant must retain green semantics.");

            Rgba32[] excessive = new Rgba32[8];
            for (int i = 0; i < excessive.Length; i++)
            {
                excessive[i] = new Rgba32(255, 255, 0, 255);
            }

            excessive[0] = new Rgba32(190, 255, 0, 255);
            excessive[1] = new Rgba32(110, 110, 0, 255);
            accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                excessive,
                4,
                2,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out normalizedPixelCount,
                out error);
            Check.False(accepted, "More than 12.5 percent unknown pixels must remain rejected.");
            Check.Null(mask, "An excessive unknown fraction must not produce a mask.");
            Check.Equal(0, normalizedPixelCount, "A rejected palette must not report normalization.");
            Check.Equal(2, statistics.UnknownPixels, "Rejected unknown count mismatch.");

            Rgba32[] distant = new Rgba32[16];
            for (int i = 0; i < distant.Length; i++)
            {
                distant[i] = new Rgba32(255, 255, 0, 255);
            }

            distant[0] = new Rgba32(255, 255, 255, 255);
            accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                distant,
                4,
                4,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out normalizedPixelCount,
                out error);
            Check.False(accepted, "A distant accidental color must remain rejected.");
            Check.Equal(1, statistics.UnknownPixels, "Distant-color unknown count mismatch.");
            Check.Equal(0, normalizedPixelCount, "A distant color must not report normalization.");

            Rgba32[] exportedGreen = new Rgba32[16];
            for (int i = 0; i < exportedGreen.Length; i++)
            {
                exportedGreen[i] = new Rgba32(76, 255, 0, 255);
            }

            accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                exportedGreen,
                4,
                4,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out normalizedPixelCount,
                out error);
            Check.True(accepted, "The known exported green alias must work at any coverage.");
            Check.Equal(16, statistics.GreenPixels, "Exported green alias count mismatch.");
            Check.Equal(0, normalizedPixelCount, "A recognized green alias needs no fallback.");

            Rgba32[] blue = new Rgba32[8];
            for (int i = 0; i < blue.Length; i++)
            {
                blue[i] = new Rgba32(255, 255, 0, 255);
            }

            blue[0] = new Rgba32(0, 0, 255, 255);
            accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                blue,
                4,
                2,
                Options(ColorClassificationMode.Threshold, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out normalizedPixelCount,
                out error);
            Check.False(accepted, "Blue packed-data pixels must never use palette normalization.");
            Check.Equal(1, statistics.BluePixels, "Blue safety count mismatch.");
            Check.Equal(0, normalizedPixelCount, "Blue rejection must not report normalization.");

            accepted = MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                compatible,
                4,
                4,
                Options(ColorClassificationMode.Exact, 12, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out normalizedPixelCount,
                out error);
            Check.False(accepted, "Explicit Exact mode must remain strict.");
            Check.Equal(0, normalizedPixelCount, "Exact mode must not report automatic normalization.");
        }

        private static void ColorAlphaAndInvalidInput()
        {
            Rgba32[] pixels =
            {
                new Rgba32(255, 255, 0, 0),
                new Rgba32(0, 255, 0, 255),
                new Rgba32(0, 0, 0, 1),
                new Rgba32(255, 0, 0, 128)
            };
            MaskColorStatistics statistics;
            Decode(
                pixels,
                2,
                2,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out statistics);
            Check.Equal(2, statistics.UnexpectedAlphaPixels, "Only alpha values other than 0 and 255 are unexpected.");

            SemanticMask mask;
            string error;
            bool accepted = MaskColorDecoder.TryDecode(
                null,
                1,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "Null pixels must fail.");
            Check.Contains("missing", error, "Null-pixel diagnostic mismatch.");

            accepted = MaskColorDecoder.TryDecode(
                new Rgba32[0],
                0,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "Zero width must fail.");

            accepted = MaskColorDecoder.TryDecode(
                new Rgba32[1],
                2,
                1,
                Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "Mismatched pixel count must fail.");

            accepted = MaskColorDecoder.TryDecode(
                new Rgba32[1],
                1,
                1,
                null,
                out mask,
                out statistics,
                out error);
            Check.False(accepted, "Null options must fail.");
            Check.Contains("options", error, "Null-options diagnostic mismatch.");

            Check.Throws<OverflowException>(
                delegate
                {
                    MaskColorDecoder.TryDecode(
                        new Rgba32[0],
                        int.MaxValue,
                        2,
                        Options(ColorClassificationMode.Exact, 0, UnknownColorPolicy.RejectMask),
                        out mask,
                        out statistics,
                        out error);
                },
                "Overflowing dimensions must fail safely.");
        }

        private static void ComposerTruthTable()
        {
            MaskPixelRule[] rules =
            {
                MaskPixelRule.Unknown,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.HideWhenNotOff
            };
            GarmentState[] states =
            {
                GarmentState.Unknown,
                GarmentState.Full,
                GarmentState.Partial,
                GarmentState.Off
            };
            for (int ruleIndex = 0; ruleIndex < rules.Length; ruleIndex++)
            {
                for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
                {
                    bool expected =
                        (rules[ruleIndex] == MaskPixelRule.HideWhenFull && states[stateIndex] == GarmentState.Full) ||
                        (rules[ruleIndex] == MaskPixelRule.HideWhenNotOff &&
                         (states[stateIndex] == GarmentState.Full || states[stateIndex] == GarmentState.Partial));
                    Check.Equal(
                        expected,
                        MaskComposer.ShouldHide(rules[ruleIndex], states[stateIndex]),
                        "Rule/state truth table mismatch for " + rules[ruleIndex] + "/" + states[stateIndex] + ".");
                }
            }
        }

        private static void ComposerPotentialContribution()
        {
            MaskColorStatistics yellow = new MaskColorStatistics();
            yellow.YellowPixels = 10;
            Check.False(
                MaskComposer.HasPotentialContribution(
                    yellow,
                    UnknownColorPolicy.RejectMask,
                    GarmentState.Full),
                "Yellow-only masks cannot contribute in Full.");

            MaskColorStatistics green = new MaskColorStatistics();
            green.GreenPixels = 10;
            Check.True(
                MaskComposer.HasPotentialContribution(
                    green,
                    UnknownColorPolicy.RejectMask,
                    GarmentState.Full),
                "Green must contribute in Full.");
            Check.False(
                MaskComposer.HasPotentialContribution(
                    green,
                    UnknownColorPolicy.RejectMask,
                    GarmentState.Partial),
                "Green must not contribute in Partial.");

            MaskColorStatistics black = new MaskColorStatistics();
            black.BlackPixels = 10;
            Check.True(
                MaskComposer.HasPotentialContribution(
                    black,
                    UnknownColorPolicy.RejectMask,
                    GarmentState.Partial),
                "Black must contribute in Partial.");

            MaskColorStatistics unknown = new MaskColorStatistics();
            unknown.UnknownPixels = 10;
            Check.False(
                MaskComposer.HasPotentialContribution(
                    unknown,
                    UnknownColorPolicy.NoContribution,
                    GarmentState.Full),
                "Neutral unknown policy must permit the fast-skip.");
            Check.True(
                MaskComposer.HasPotentialContribution(
                    unknown,
                    UnknownColorPolicy.HideWhenNotOff,
                    GarmentState.Full),
                "Conservative unknown pixels must contribute in Full.");
            Check.True(
                MaskComposer.HasPotentialContribution(
                    unknown,
                    UnknownColorPolicy.HideWhenNotOff,
                    GarmentState.Partial),
                "Conservative unknown pixels must contribute in Partial.");
            Check.False(
                MaskComposer.HasPotentialContribution(
                    unknown,
                    UnknownColorPolicy.HideWhenNotOff,
                    GarmentState.Off),
                "No mask contributes in Off.");
            Check.True(
                MaskComposer.HasPotentialContribution(
                    null,
                    UnknownColorPolicy.RejectMask,
                    GarmentState.Full),
                "Missing statistics must disable the optimization rather than skip work.");
        }

        private static void ComposerOrAndOverlap()
        {
            SemanticMask first = Mask(
                2,
                2,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.NeverHide,
                MaskPixelRule.NeverHide);
            SemanticMask second = Mask(
                2,
                2,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.NeverHide);
            bool[] hidden = new bool[4];

            Check.Equal(2, MaskComposer.Accumulate(first, GarmentState.Full, 2, 2, hidden), "First layer newly-hidden count mismatch.");
            Check.Equal(1, MaskComposer.Accumulate(second, GarmentState.Full, 2, 2, hidden), "Overlap must not be double-counted.");
            Check.SequenceEqual(
                new bool[] { true, true, true, false },
                hidden,
                "Simultaneous masks must compose by OR.");
            Check.Equal(0, MaskComposer.Accumulate(second, GarmentState.Full, 2, 2, hidden), "Reapplying a layer must add no hidden pixels.");
        }

        private static void ComposerIndependentStatesAndRemoval()
        {
            SemanticMask greenLayer = Mask(
                2,
                2,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.NeverHide);
            SemanticMask blackLayer = Mask(
                2,
                2,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.NeverHide);

            bool[] combined = new bool[4];
            Check.Equal(0, MaskComposer.Accumulate(greenLayer, GarmentState.Partial, 2, 2, combined), "Green must reveal during Partial.");
            Check.Equal(2, MaskComposer.Accumulate(blackLayer, GarmentState.Full, 2, 2, combined), "Black must hide while Full.");
            Check.Equal(1, MaskComposer.Accumulate(greenLayer, GarmentState.Full, 2, 2, combined), "Green Full must add only its non-overlap.");
            Check.SequenceEqual(new bool[] { true, true, true, false }, combined, "Independent-state composition mismatch.");

            bool[] afterBlackRemoval = new bool[4];
            MaskComposer.Accumulate(greenLayer, GarmentState.Full, 2, 2, afterBlackRemoval);
            MaskComposer.Accumulate(blackLayer, GarmentState.Off, 2, 2, afterBlackRemoval);
            Check.SequenceEqual(
                new bool[] { true, false, true, false },
                afterBlackRemoval,
                "Removing one garment must remove only its contribution.");

            bool[] allOff = new bool[4];
            MaskComposer.Accumulate(greenLayer, GarmentState.Off, 2, 2, allOff);
            MaskComposer.Accumulate(blackLayer, GarmentState.Off, 2, 2, allOff);
            Check.SequenceEqual(new bool[] { false, false, false, false }, allOff, "All-Off must have no custom contribution.");
        }

        private static void ComposerResampledAccumulation()
        {
            SemanticMask layer = Mask(
                2,
                1,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.NeverHide);
            bool[] hidden = new bool[8];
            int count = MaskComposer.Accumulate(layer, GarmentState.Partial, 4, 2, hidden);
            Check.Equal(4, count, "Nearest-resampled layer hidden count mismatch.");
            Check.SequenceEqual(
                new bool[]
                {
                    true, true, false, false,
                    true, true, false, false
                },
                hidden,
                "Nearest-resampled layer pattern mismatch.");

            Check.Throws<ArgumentNullException>(
                delegate { MaskComposer.Accumulate(null, GarmentState.Full, 1, 1, new bool[1]); },
                "Null semantic layer must throw.");
            Check.Throws<ArgumentException>(
                delegate { MaskComposer.Accumulate(layer, GarmentState.Full, 4, 2, new bool[7]); },
                "Mismatched output buffer must throw.");
        }

        private static void ResolutionSourceCoordinates()
        {
            int[] up = new int[4];
            for (int i = 0; i < up.Length; i++)
            {
                up[i] = MaskResolutionConverter.SourceCoordinate(i, 2, 4);
            }

            Check.SequenceEqual(new int[] { 0, 0, 1, 1 }, up, "2-to-4 nearest mapping mismatch.");

            int[] down = new int[2];
            for (int i = 0; i < down.Length; i++)
            {
                down[i] = MaskResolutionConverter.SourceCoordinate(i, 4, 2);
            }

            Check.SequenceEqual(new int[] { 0, 2 }, down, "4-to-2 nearest mapping mismatch.");

            int[] uneven = new int[5];
            for (int i = 0; i < uneven.Length; i++)
            {
                uneven[i] = MaskResolutionConverter.SourceCoordinate(i, 3, 5);
            }

            Check.SequenceEqual(new int[] { 0, 0, 1, 1, 2 }, uneven, "3-to-5 nearest mapping mismatch.");
            Check.Equal(49999, MaskResolutionConverter.SourceCoordinate(99999, 50000, 100000), "Large-coordinate mapping must avoid overflow.");
        }

        private static void ResolutionUpscale()
        {
            SemanticMask source = Mask(
                2,
                2,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.Unknown);
            SemanticMask resized = MaskResolutionConverter.ResizeNearest(source, 4, 4);

            Check.Equal(4, resized.Width, "Upscaled width mismatch.");
            Check.Equal(4, resized.Height, "Upscaled height mismatch.");
            Check.SequenceEqual(
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide, MaskPixelRule.NeverHide, MaskPixelRule.HideWhenFull, MaskPixelRule.HideWhenFull,
                    MaskPixelRule.NeverHide, MaskPixelRule.NeverHide, MaskPixelRule.HideWhenFull, MaskPixelRule.HideWhenFull,
                    MaskPixelRule.HideWhenNotOff, MaskPixelRule.HideWhenNotOff, MaskPixelRule.Unknown, MaskPixelRule.Unknown,
                    MaskPixelRule.HideWhenNotOff, MaskPixelRule.HideWhenNotOff, MaskPixelRule.Unknown, MaskPixelRule.Unknown
                },
                resized.Rules,
                "2x2-to-4x4 nearest upscale mismatch.");
        }

        private static void ResolutionDownscaleAndIdentity()
        {
            SemanticMask source = Mask(
                4,
                4,
                MaskPixelRule.NeverHide, MaskPixelRule.HideWhenFull, MaskPixelRule.HideWhenNotOff, MaskPixelRule.Unknown,
                MaskPixelRule.Unknown, MaskPixelRule.Unknown, MaskPixelRule.Unknown, MaskPixelRule.Unknown,
                MaskPixelRule.HideWhenNotOff, MaskPixelRule.Unknown, MaskPixelRule.HideWhenFull, MaskPixelRule.Unknown,
                MaskPixelRule.Unknown, MaskPixelRule.Unknown, MaskPixelRule.Unknown, MaskPixelRule.NeverHide);
            SemanticMask down = MaskResolutionConverter.ResizeNearest(source, 2, 2);
            Check.SequenceEqual(
                new MaskPixelRule[]
                {
                    MaskPixelRule.NeverHide,
                    MaskPixelRule.HideWhenNotOff,
                    MaskPixelRule.HideWhenNotOff,
                    MaskPixelRule.HideWhenFull
                },
                down.Rules,
                "4x4-to-2x2 nearest downscale mismatch.");

            SemanticMask identity = MaskResolutionConverter.ResizeNearest(source, 4, 4);
            Check.SequenceEqual(source.Rules, identity.Rules, "Identity resize must preserve every rule.");
            Check.NotSame(source, identity, "Identity resize must return a separate mask object.");
            Check.NotSame(source.Rules, identity.Rules, "Identity resize must return a separate rule buffer.");
        }

        private static void ResolutionInvalidArguments()
        {
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskResolutionConverter.SourceCoordinate(-1, 2, 2); },
                "Negative destination coordinate must throw.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskResolutionConverter.SourceCoordinate(2, 2, 2); },
                "Destination coordinate at size must throw.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskResolutionConverter.SourceCoordinate(0, 0, 2); },
                "Zero source size must throw.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskResolutionConverter.SourceCoordinate(0, 2, 0); },
                "Zero destination size must throw.");
            Check.Throws<ArgumentNullException>(
                delegate { MaskResolutionConverter.ResizeNearest(null, 1, 1); },
                "Null source mask must throw.");

            SemanticMask one = Mask(1, 1, MaskPixelRule.NeverHide);
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskResolutionConverter.ResizeNearest(one, 0, 1); },
                "Zero destination width must throw.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskResolutionConverter.ResizeNearest(one, 1, 0); },
                "Zero destination height must throw.");
            Check.Throws<OverflowException>(
                delegate { MaskResolutionConverter.ResizeNearest(one, int.MaxValue, 2); },
                "Overflowing destination dimensions must throw.");
        }

        private static void PreviewCategoricalAndBounded()
        {
            SemanticMask source = Mask(
                4,
                2,
                MaskPixelRule.NeverHide,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.Unknown,
                MaskPixelRule.Unknown);
            int width;
            int height;
            Rgba32[] pixels = MaskPreviewBuilder.Build(source, 2, out width, out height);
            Check.Equal(2, width, "Preview width must respect the maximum dimension.");
            Check.Equal(1, height, "Preview must preserve the source aspect ratio.");
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(255, 255, 0, 255),
                    new Rgba32(0, 255, 0, 255)
                },
                pixels,
                "Preview category colors or nearest sampling mismatch.");

            SemanticMask categories = Mask(
                4,
                1,
                MaskPixelRule.NeverHide,
                MaskPixelRule.HideWhenFull,
                MaskPixelRule.HideWhenNotOff,
                MaskPixelRule.Unknown);
            pixels = MaskPreviewBuilder.Build(categories, 4, out width, out height);
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(255, 255, 0, 255),
                    new Rgba32(0, 255, 0, 255),
                    new Rgba32(0, 0, 0, 255),
                    new Rgba32(255, 0, 255, 255)
                },
                pixels,
                "Preview must expose every semantic category deterministically.");
            Check.Throws<ArgumentNullException>(
                delegate { MaskPreviewBuilder.Build(null, 1, out width, out height); },
                "Null preview source must throw.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { MaskPreviewBuilder.Build(categories, 0, out width, out height); },
                "Non-positive preview bound must throw.");
        }

        private static void FormatCanonicalFormula()
        {
            Rgba32 yellow = new Rgba32(255, 255, 0, 17);
            Rgba32 green = new Rgba32(0, 255, 0, 18);
            Rgba32 black = new Rgba32(0, 0, 0, 19);
            Rgba32 red = new Rgba32(255, 0, 0, 20);
            Rgba32 blue = new Rgba32(0, 0, 255, 21);
            Rgba32 white = new Rgba32(255, 255, 255, 22);

            Check.False(BodyMaskFormatAdapter.IsHiddenByShader(yellow, 1f, 1f), "Yellow R/G must remain visible with both controls active.");
            Check.True(BodyMaskFormatAdapter.IsHiddenByShader(green, 1f, 1f), "Green R/G must be hidden by the raw shader formula.");
            Check.True(BodyMaskFormatAdapter.IsHiddenByShader(black, 1f, 1f), "Black R/G must be hidden.");
            Check.True(BodyMaskFormatAdapter.IsHiddenByShader(red, 1f, 1f), "Red R/G must be hidden by the second channel.");
            Check.True(BodyMaskFormatAdapter.IsHiddenByShader(blue, 1f, 1f), "Blue has black R/G and must be hidden by the body formula.");
            Check.False(BodyMaskFormatAdapter.IsHiddenByShader(white, 1f, 1f), "White R/G must remain visible.");

            Check.True(BodyMaskFormatAdapter.IsHiddenByShader(red, 0f, 1f), "alphaA=0 only disables the R-channel test; red still fails G.");
            Check.False(BodyMaskFormatAdapter.IsHiddenByShader(green, 0f, 1f), "alphaA=0 makes green visible through R override.");
            Check.False(BodyMaskFormatAdapter.IsHiddenByShader(red, 1f, 0f), "alphaB=0 makes red visible through G override.");
            Check.True(BodyMaskFormatAdapter.IsHiddenByShader(green, 1f, 0f), "alphaB=0 leaves green failing R.");

            Check.False(BodyMaskFormatAdapter.IsHiddenByShader(black, 0f, 0f), "Both controls zero must disable vanilla clipping even for black.");
            Check.False(BodyMaskFormatAdapter.IsHiddenByShader(blue, 0f, 0f), "Both controls zero must disable vanilla clipping regardless of B.");
        }

        private static void FormatThresholdAndControls()
        {
            Check.True(
                BodyMaskFormatAdapter.IsHiddenByShader(new Rgba32(127, 255, 1, 2), 1f, 1f),
                "R=127 must be below the 0.5 visibility threshold.");
            Check.False(
                BodyMaskFormatAdapter.IsHiddenByShader(new Rgba32(128, 255, 1, 2), 1f, 1f),
                "R=128 must be at or above the 0.5 visibility threshold.");
            Check.True(
                BodyMaskFormatAdapter.IsHiddenByShader(new Rgba32(255, 127, 1, 2), 1f, 1f),
                "G=127 must be below the 0.5 visibility threshold.");
            Check.False(
                BodyMaskFormatAdapter.IsHiddenByShader(new Rgba32(255, 128, 1, 2), 1f, 1f),
                "G=128 must be at or above the 0.5 visibility threshold.");

            Rgba32 green = new Rgba32(0, 255, 77, 88);
            Check.False(
                BodyMaskFormatAdapter.IsHiddenByShader(green, 0.5f, 1f),
                "Exactly 0.5 from the control must not clip.");
            Check.True(
                BodyMaskFormatAdapter.IsHiddenByShader(green, 0.5001f, 1f),
                "A control just above 0.5 must expose a zero mask channel to clipping.");

            Rgba32 red = new Rgba32(255, 0, 77, 88);
            Check.False(
                BodyMaskFormatAdapter.IsHiddenByShader(red, 1f, 0.5f),
                "Exactly 0.5 on alphaB must not clip.");
            Check.True(
                BodyMaskFormatAdapter.IsHiddenByShader(red, 1f, 0.5001f),
                "alphaB just above 0.5 must permit clipping by G.");
        }

        private static void FormatWritePreservesChannels()
        {
            Rgba32[] basePixels =
            {
                new Rgba32(255, 255, 10, 20),
                new Rgba32(0, 0, 30, 40),
                new Rgba32(255, 255, 50, 60),
                new Rgba32(255, 0, 70, 80)
            };
            bool[] custom = { false, false, true, false };
            Rgba32[] output = new Rgba32[4];
            int hidden = BodyMaskFormatAdapter.WriteBinaryBodyMask(
                basePixels,
                2,
                2,
                1f,
                1f,
                custom,
                2,
                2,
                output);

            Check.Equal(3, hidden, "Combined vanilla/custom hidden count mismatch.");
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(255, 255, 10, 20),
                    new Rgba32(0, 0, 30, 40),
                    new Rgba32(0, 0, 50, 60),
                    new Rgba32(255, 0, 70, 80)
                },
                output,
                "Binary body mask output or B/A preservation mismatch.");

            for (int i = 0; i < output.Length; i++)
            {
                Check.Equal(basePixels[i].B, output[i].B, "B must be preserved at pixel " + i + ".");
                Check.Equal(basePixels[i].A, output[i].A, "A must be preserved at pixel " + i + ".");
            }
        }

        private static void FormatWriteResamplesAndTopOff()
        {
            Rgba32[] basePixels =
            {
                new Rgba32(0, 0, 11, 12),
                new Rgba32(255, 255, 21, 22)
            };
            bool[] custom =
            {
                true, false, false, false,
                false, false, true, false
            };
            Rgba32[] output = new Rgba32[8];
            int hidden = BodyMaskFormatAdapter.WriteBinaryBodyMask(
                basePixels,
                2,
                1,
                0f,
                0f,
                custom,
                4,
                2,
                output);

            Check.Equal(2, hidden, "Top Off must remove vanilla contribution but preserve custom contribution.");
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(0, 0, 11, 12),
                    new Rgba32(255, 255, 11, 12),
                    new Rgba32(255, 255, 21, 22),
                    new Rgba32(255, 255, 21, 22),
                    new Rgba32(255, 255, 11, 12),
                    new Rgba32(255, 255, 11, 12),
                    new Rgba32(0, 0, 21, 22),
                    new Rgba32(255, 255, 21, 22)
                },
                output,
                "Nearest base sampling, top-off behavior or B/A preservation mismatch.");
        }

        private static void FormatNoBaseAndErrors()
        {
            bool[] custom = { false, true };
            Rgba32[] output = new Rgba32[2];
            int hidden = BodyMaskFormatAdapter.WriteBinaryBodyMask(
                null,
                0,
                0,
                1f,
                1f,
                custom,
                2,
                1,
                output);
            Check.Equal(1, hidden, "No-base hidden count mismatch.");
            Check.SequenceEqual(
                new Rgba32[]
                {
                    new Rgba32(255, 255, 255, 255),
                    new Rgba32(0, 0, 255, 255)
                },
                output,
                "No-base output must synthesize white unmanaged channels.");

            Rgba32[] invalidBaseOutput = new Rgba32[1];
            BodyMaskFormatAdapter.WriteBinaryBodyMask(
                new Rgba32[] { new Rgba32(0, 0, 1, 2) },
                2,
                2,
                1f,
                1f,
                new bool[] { false },
                1,
                1,
                invalidBaseOutput);
            Check.Equal(new Rgba32(255, 255, 255, 255), invalidBaseOutput[0], "Malformed base input must be treated as absent.");

            Check.Throws<ArgumentException>(
                delegate
                {
                    BodyMaskFormatAdapter.WriteBinaryBodyMask(
                        null, 0, 0, 0f, 0f, new bool[1], 2, 1, new Rgba32[2]);
                },
                "Wrong custom buffer size must throw.");
            Check.Throws<ArgumentException>(
                delegate
                {
                    BodyMaskFormatAdapter.WriteBinaryBodyMask(
                        null, 0, 0, 0f, 0f, new bool[2], 2, 1, new Rgba32[1]);
                },
                "Wrong output buffer size must throw.");
            Check.Throws<OverflowException>(
                delegate
                {
                    BodyMaskFormatAdapter.WriteBinaryBodyMask(
                        null, 0, 0, 0f, 0f, new bool[0], int.MaxValue, 2, new Rgba32[0]);
                },
                "Overflowing output dimensions must throw.");
        }

        private static void PngValidTypesAndBounds()
        {
            byte[] colorTypes = { 2, 3, 4, 6 };
            for (int i = 0; i < colorTypes.Length; i++)
            {
                byte[] bytes = BuildPngHeader(64, 64, 8, colorTypes[i]);
                MaskValidationResult result = PngMaskValidator.Validate(bytes);
                Check.True(result.IsValid, "Supported PNG color type must validate: " + colorTypes[i] + ".");
                Check.Equal(64, result.Width, "Valid PNG width mismatch.");
                Check.Equal(64, result.Height, "Valid PNG height mismatch.");
                Check.Equal((byte)8, result.BitDepth, "Valid PNG bit depth mismatch.");
                Check.Equal(colorTypes[i], result.ColorType, "Valid PNG color type mismatch.");
            }

            MaskValidationResult smallest = PngMaskValidator.Validate(
                BuildPngHeader(1, 1, 8, 6));
            Check.True(
                smallest.IsValid,
                "Resolution 1x1 must validate now that there is no configurable minimum.");
            MaskValidationResult maximum = PngMaskValidator.Validate(
                BuildPngHeader(
                    PortableMaskFormatLimits.MaximumDimension,
                    PortableMaskFormatLimits.MaximumDimension,
                    8,
                    6));
            Check.True(maximum.IsValid, "The exact internal card-data resolution limit must validate.");
        }

        private static void PngSignatureAndIhdr()
        {
            MaskValidationResult result = PngMaskValidator.Validate(null);
            Check.False(result.IsValid, "Null PNG must fail.");
            Check.Contains("too short", result.Message, "Null PNG diagnostic mismatch.");

            result = PngMaskValidator.Validate(new byte[32]);
            Check.False(result.IsValid, "Short PNG must fail.");

            byte[] bytes = BuildPngHeader(64, 64, 8, 6);
            bytes[0] = 0;
            result = PngMaskValidator.Validate(bytes);
            Check.False(result.IsValid, "Bad PNG signature must fail.");
            Check.Contains("signature", result.Message, "Bad-signature diagnostic mismatch.");

            bytes = BuildPngHeader(64, 64, 8, 6);
            WriteUInt32BigEndian(bytes, 8, 12);
            result = PngMaskValidator.Validate(bytes);
            Check.False(result.IsValid, "Non-13 IHDR length must fail.");
            Check.Contains("IHDR", result.Message, "IHDR-length diagnostic mismatch.");

            bytes = BuildPngHeader(64, 64, 8, 6);
            bytes[12] = (byte)'X';
            result = PngMaskValidator.Validate(bytes);
            Check.False(result.IsValid, "Wrong first chunk type must fail.");
            Check.Contains("IHDR", result.Message, "IHDR-type diagnostic mismatch.");
        }

        private static void PngDimensions()
        {
            MaskValidationResult result = PngMaskValidator.Validate(BuildPngHeader(64, 32, 8, 6));
            Check.False(result.IsValid, "Non-square PNG must fail.");
            Check.Contains("square", result.Message, "Non-square diagnostic mismatch.");

            result = PngMaskValidator.Validate(BuildPngHeader(16, 16, 8, 6));
            Check.True(result.IsValid, "A mask below the former configurable minimum must validate.");

            result = PngMaskValidator.Validate(BuildPngHeader(2048, 2048, 8, 6));
            Check.True(result.IsValid, "A mask above the former configurable maximum must validate.");

            int unsupported = PortableMaskFormatLimits.MaximumDimension * 2;
            result = PngMaskValidator.Validate(BuildPngHeader(unsupported, unsupported, 8, 6));
            Check.False(result.IsValid, "A mask above the internal card-data resolution limit must fail.");
            Check.Contains("card-data resolution", result.Message, "Internal-resolution diagnostic mismatch.");

            result = PngMaskValidator.Validate(BuildPngHeader(96, 96, 8, 6));
            Check.False(result.IsValid, "Non-power-of-two PNG must fail.");
            Check.Contains("power of two", result.Message, "Power-of-two diagnostic mismatch.");

            result = PngMaskValidator.Validate(BuildPngHeader(0, 0, 8, 6));
            Check.False(result.IsValid, "Zero dimensions must fail.");

            result = PngMaskValidator.Validate(BuildPngHeader(uint.MaxValue, uint.MaxValue, 8, 6));
            Check.False(result.IsValid, "Dimensions above Int32 must fail.");
            Check.Contains("not supported", result.Message, "Oversized-dimension diagnostic mismatch.");

            result = PngMaskValidator.Validate(BuildPngHeader(65536, 65536, 8, 6));
            Check.False(result.IsValid, "A pixel count above Int32 must fail before decoding.");
            Check.Contains("represented safely", result.Message, "Pixel-count overflow diagnostic mismatch.");
        }

        private static void PngFormatAndByteLimits()
        {
            MaskValidationResult result = PngMaskValidator.Validate(BuildPngHeader(64, 64, 16, 6));
            Check.False(result.IsValid, "16-bit PNG must fail validation.");
            Check.Contains("8-bit", result.Message, "Bit-depth diagnostic mismatch.");

            byte[] invalidTypes = { 0, 1, 5, 7, 255 };
            for (int i = 0; i < invalidTypes.Length; i++)
            {
                result = PngMaskValidator.Validate(BuildPngHeader(64, 64, 8, invalidTypes[i]));
                Check.False(result.IsValid, "Unsupported PNG color type must fail: " + invalidTypes[i] + ".");
                Check.Contains("color type", result.Message, "Color-type diagnostic mismatch.");
            }

            byte[] header = BuildPngHeader(64, 64, 8, 6);
            byte[] bytes = new byte[PortableMaskFormatLimits.MaximumPngBytes];
            Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            result = PngMaskValidator.Validate(bytes);
            Check.True(
                result.IsValid,
                "The exact internal byte limit must validate, including files above the removed configurable limit.");

            Array.Resize<byte>(ref bytes, PortableMaskFormatLimits.MaximumPngBytes + 1);
            result = PngMaskValidator.Validate(bytes);
            Check.False(result.IsValid, "A PNG above the internal card-data byte limit must fail.");
            Check.Contains("card-data size", result.Message, "Internal-byte-limit diagnostic mismatch.");
        }

        private static void MaskDimensionPixelCounts()
        {
            int pixelCount;
            Check.False(
                MaskDimensions.TryGetPixelCount(0, 1, out pixelCount),
                "Zero width must not produce a pixel count.");
            Check.False(
                MaskDimensions.TryGetPixelCount(1, 0, out pixelCount),
                "Zero height must not produce a pixel count.");
            Check.False(
                MaskDimensions.TryGetPixelCount(-1, 1, out pixelCount),
                "Negative dimensions must not produce a pixel count.");
            Check.True(
                MaskDimensions.TryGetPixelCount(1, 1, out pixelCount),
                "A single pixel must be representable.");
            Check.Equal(1, pixelCount, "Single-pixel count mismatch.");
            Check.True(
                MaskDimensions.TryGetPixelCount(46340, 46340, out pixelCount),
                "The largest equal dimensions whose product fits Int32 must be representable.");
            Check.Equal(2147395600, pixelCount, "Large representable pixel count mismatch.");
            Check.False(
                MaskDimensions.TryGetPixelCount(46341, 46341, out pixelCount),
                "An overflowing square pixel count must be rejected.");
            Check.Equal(0, pixelCount, "Rejected dimensions must clear the output count.");
            Check.False(
                MaskDimensions.TryGetPixelCount(int.MaxValue, 2, out pixelCount),
                "An overflowing rectangular pixel count must be rejected.");
        }

        private static void SerializerCompleteRoundTrip()
        {
            ClothingMaskLayerData layer = Layer(ClothingSlot.Pantyhose, 31);
            layer.Enabled = false;
            layer.Width = 512;
            layer.Height = 512;
            layer.ColorFormatVersion = 7;
            layer.Hash = "abcdef012345";
            layer.OptionalStatePolicy = UnknownStatePolicy.TreatAsPartial;
            layer.CreatedWithPluginVersion = "0.1.0";
            layer.LastValidationResult = "válido ✓";
            layer.BoundItemIdentity = Identity(
                ClothingSlot.Pantyhose,
                109,
                12345,
                67890,
                "NightOwlZzz.mod");
            layer.BoundItemIdentity.DisplayName = "Prenda ñ";
            layer.SourceContract = MaskSourceContract.ExternalRgbStateCoverage;
            layer.GradientHandlingMode = GradientHandlingMode.PreserveContinuous;
            layer.SourceProviderId = "nakay.kk.ChaAlphaMask";
            layer.SourceFingerprint = "f0e1d2c3b4a5";
            layer.SourceAsset = "abdata/list/characustom/example.unity3d#mab_55";

            byte[] payload = CardDataSerializer.Serialize(new ClothingMaskLayerData[] { layer });
            Check.Equal(CardDataSerializer.SchemaVersion, ReadInt32LittleEndian(payload, 4), "Writer schema mismatch.");
            Dictionary<ClothingSlot, ClothingMaskLayerData> layers;
            string error;
            bool accepted = CardDataSerializer.TryDeserialize(payload, out layers, out error);
            Check.True(accepted, "Complete serialized payload must deserialize: " + error);
            Check.Null(error, "Successful deserialization must not report an error.");
            Check.Equal(1, layers.Count, "Roundtrip layer count mismatch.");

            ClothingMaskLayerData restored = layers[ClothingSlot.Pantyhose];
            Check.Equal(layer.Slot, restored.Slot, "Roundtrip slot mismatch.");
            Check.Equal(layer.Enabled, restored.Enabled, "Roundtrip enabled mismatch.");
            Check.Equal(layer.Width, restored.Width, "Roundtrip width mismatch.");
            Check.Equal(layer.Height, restored.Height, "Roundtrip height mismatch.");
            Check.Equal(layer.ColorFormatVersion, restored.ColorFormatVersion, "Roundtrip format version mismatch.");
            Check.Equal(layer.Hash, restored.Hash, "Roundtrip hash mismatch.");
            Check.Equal(layer.OptionalStatePolicy, restored.OptionalStatePolicy, "Roundtrip state policy mismatch.");
            Check.Equal(layer.CreatedWithPluginVersion, restored.CreatedWithPluginVersion, "Roundtrip plugin version mismatch.");
            Check.Equal(layer.LastValidationResult, restored.LastValidationResult, "Roundtrip validation text mismatch.");
            Check.Equal(layer.SourceContract, restored.SourceContract, "Roundtrip source contract mismatch.");
            Check.Equal(layer.GradientHandlingMode, restored.GradientHandlingMode, "Roundtrip gradient mode mismatch.");
            Check.Equal(layer.SourceProviderId, restored.SourceProviderId, "Roundtrip source provider mismatch.");
            Check.Equal(layer.SourceFingerprint, restored.SourceFingerprint, "Roundtrip source fingerprint mismatch.");
            Check.Equal(layer.SourceAsset, restored.SourceAsset, "Roundtrip source asset mismatch.");
            Check.SequenceEqual(layer.OriginalPngBytes, restored.OriginalPngBytes, "Roundtrip PNG bytes mismatch.");
            Check.NotSame(layer.OriginalPngBytes, restored.OriginalPngBytes, "Roundtrip PNG must be a separate array.");

            Check.NotNull(restored.BoundItemIdentity, "Roundtrip identity missing.");
            Check.Equal(layer.BoundItemIdentity.Slot, restored.BoundItemIdentity.Slot, "Identity slot mismatch.");
            Check.Equal(layer.BoundItemIdentity.Category, restored.BoundItemIdentity.Category, "Identity category mismatch.");
            Check.Equal(layer.BoundItemIdentity.LocalItemId, restored.BoundItemIdentity.LocalItemId, "Identity local ID mismatch.");
            Check.Equal(layer.BoundItemIdentity.OriginalItemId, restored.BoundItemIdentity.OriginalItemId, "Identity original ID mismatch.");
            Check.Equal(layer.BoundItemIdentity.SideloaderGuid, restored.BoundItemIdentity.SideloaderGuid, "Identity GUID mismatch.");
            Check.Equal(layer.BoundItemIdentity.DisplayName, restored.BoundItemIdentity.DisplayName, "Identity display name mismatch.");
        }

        private static void SerializerSchema1Migration()
        {
            ClothingMaskLayerData schema1Layer = Layer(ClothingSlot.Bra, 73);
            schema1Layer.Enabled = false;
            schema1Layer.Width = 256;
            schema1Layer.Height = 256;
            schema1Layer.ColorFormatVersion = 1;
            schema1Layer.OptionalStatePolicy = UnknownStatePolicy.PreserveLastKnown;
            schema1Layer.Hash = "legacy-hash";
            schema1Layer.CreatedWithPluginVersion = "0.1.2";
            schema1Layer.LastValidationResult = "legacy fixture";
            schema1Layer.BoundItemIdentity = Identity(
                ClothingSlot.Bra,
                107,
                4321,
                1234,
                "legacy.mod");

            byte[] fixture = Convert.FromBase64String(Schema1FixtureBase64);
            Check.Equal(
                Schema1FixtureSha256,
                HashUtility.Sha256(fixture),
                "Pinned BML1 fixture bytes changed.");
            Check.Equal(
                CardDataSerializer.Schema1Version,
                ReadInt32LittleEndian(fixture, 4),
                "Fixture schema mismatch.");

            Dictionary<ClothingSlot, ClothingMaskLayerData> layers;
            string error;
            Check.True(
                CardDataSerializer.TryDeserialize(fixture, out layers, out error),
                "BML1 fixture must load unchanged: " + error);
            ClothingMaskLayerData restored = layers[ClothingSlot.Bra];
            Check.Equal(schema1Layer.Enabled, restored.Enabled, "BML1 enabled flag mismatch.");
            Check.Equal(schema1Layer.Width, restored.Width, "BML1 width mismatch.");
            Check.Equal(schema1Layer.Height, restored.Height, "BML1 height mismatch.");
            Check.Equal(schema1Layer.Hash, restored.Hash, "BML1 hash mismatch.");
            Check.Equal(schema1Layer.OptionalStatePolicy, restored.OptionalStatePolicy, "BML1 policy mismatch.");
            Check.SequenceEqual(schema1Layer.OriginalPngBytes, restored.OriginalPngBytes, "BML1 original PNG mismatch.");
            Check.Equal(
                MaskSourceContract.Native,
                restored.SourceContract,
                "BML1 must migrate to the native source contract.");
            Check.Equal(
                GradientHandlingMode.StrictCategorical,
                restored.GradientHandlingMode,
                "BML1 must use its deterministic categorical interpretation.");
            Check.Null(restored.SourceProviderId, "BML1 must not invent a source provider.");
            Check.Null(restored.SourceFingerprint, "BML1 must not invent a source fingerprint.");
            Check.Null(restored.SourceAsset, "BML1 must not invent a source asset.");

            byte[] migrated = CardDataSerializer.Serialize(layers.Values);
            Check.Equal(
                CardDataSerializer.SchemaVersion,
                ReadInt32LittleEndian(migrated, 4),
                "Migrated data must write BML2.");
            Check.SequenceEqual(
                schema1Layer.OriginalPngBytes,
                layers[ClothingSlot.Bra].OriginalPngBytes,
                "Migration must not replace or compile the source PNG.");

            Dictionary<ClothingSlot, ClothingMaskLayerData> migratedLayers;
            Check.True(
                CardDataSerializer.TryDeserialize(migrated, out migratedLayers, out error),
                "Migrated BML2 payload must deserialize: " + error);
            ClothingMaskLayerData migratedLayer = migratedLayers[ClothingSlot.Bra];
            Check.Equal(restored.Slot, migratedLayer.Slot, "Migrated slot mismatch.");
            Check.Equal(restored.Enabled, migratedLayer.Enabled, "Migrated enabled mismatch.");
            Check.Equal(restored.Width, migratedLayer.Width, "Migrated width mismatch.");
            Check.Equal(restored.Height, migratedLayer.Height, "Migrated height mismatch.");
            Check.Equal(restored.ColorFormatVersion, migratedLayer.ColorFormatVersion, "Migrated format mismatch.");
            Check.Equal(restored.OptionalStatePolicy, migratedLayer.OptionalStatePolicy, "Migrated policy mismatch.");
            Check.Equal(restored.Hash, migratedLayer.Hash, "Migrated hash mismatch.");
            Check.Equal(restored.CreatedWithPluginVersion, migratedLayer.CreatedWithPluginVersion, "Migrated creator mismatch.");
            Check.Equal(restored.LastValidationResult, migratedLayer.LastValidationResult, "Migrated validation mismatch.");
            Check.Equal(restored.SourceContract, migratedLayer.SourceContract, "Migrated source contract mismatch.");
            Check.Equal(restored.GradientHandlingMode, migratedLayer.GradientHandlingMode, "Migrated gradient mode mismatch.");
            Check.Null(migratedLayer.SourceProviderId, "Migrated source provider must remain null.");
            Check.Null(migratedLayer.SourceFingerprint, "Migrated source fingerprint must remain null.");
            Check.Null(migratedLayer.SourceAsset, "Migrated source asset must remain null.");
            Check.SequenceEqual(restored.OriginalPngBytes, migratedLayer.OriginalPngBytes, "Migrated PNG mismatch.");
            Check.Equal(restored.BoundItemIdentity.Slot, migratedLayer.BoundItemIdentity.Slot, "Migrated identity slot mismatch.");
            Check.Equal(restored.BoundItemIdentity.Category, migratedLayer.BoundItemIdentity.Category, "Migrated identity category mismatch.");
            Check.Equal(restored.BoundItemIdentity.LocalItemId, migratedLayer.BoundItemIdentity.LocalItemId, "Migrated local ID mismatch.");
            Check.Equal(restored.BoundItemIdentity.OriginalItemId, migratedLayer.BoundItemIdentity.OriginalItemId, "Migrated original ID mismatch.");
            Check.Equal(restored.BoundItemIdentity.SideloaderGuid, migratedLayer.BoundItemIdentity.SideloaderGuid, "Migrated identity GUID mismatch.");

            for (int length = 0; length < fixture.Length; length++)
            {
                Dictionary<ClothingSlot, ClothingMaskLayerData> truncatedLayers;
                string truncatedError;
                Check.False(
                    CardDataSerializer.TryDeserialize(
                        Prefix(fixture, length),
                        out truncatedLayers,
                        out truncatedError),
                    "Every strict BML1 fixture prefix must fail; length=" + length + ".");
            }
        }

        private static void SerializerCollections()
        {
            ClothingMaskLayerData top = Layer(ClothingSlot.Top, 1);
            ClothingMaskLayerData bottom = Layer(ClothingSlot.Bottom, 2);
            byte[] payload = CardDataSerializer.Serialize(
                new ClothingMaskLayerData[] { null, top, null, bottom });
            Dictionary<ClothingSlot, ClothingMaskLayerData> layers;
            string error;
            Check.True(CardDataSerializer.TryDeserialize(payload, out layers, out error), "Collection with null entries must deserialize.");
            Check.Equal(2, layers.Count, "Null layers must be omitted.");
            Check.True(layers.ContainsKey(ClothingSlot.Top), "Top layer missing.");
            Check.True(layers.ContainsKey(ClothingSlot.Bottom), "Bottom layer missing.");

            ClothingMaskLayerData firstTop = Layer(ClothingSlot.Top, 3);
            firstTop.Width = 128;
            ClothingMaskLayerData secondTop = Layer(ClothingSlot.Top, 4);
            secondTop.Width = 1024;
            payload = CardDataSerializer.Serialize(new ClothingMaskLayerData[] { firstTop, secondTop });
            Check.True(CardDataSerializer.TryDeserialize(payload, out layers, out error), "Duplicate slots remain structurally readable.");
            Check.Equal(1, layers.Count, "Duplicate slots must collapse in the result dictionary.");
            Check.Equal(1024, layers[ClothingSlot.Top].Width, "Last duplicate slot must win deterministically.");

            payload = CardDataSerializer.Serialize(new ClothingMaskLayerData[0]);
            Check.True(CardDataSerializer.TryDeserialize(payload, out layers, out error), "Empty collection must roundtrip.");
            Check.Equal(0, layers.Count, "Empty collection must remain empty.");

            Check.Throws<ArgumentNullException>(
                delegate { CardDataSerializer.Serialize(null); },
                "Null layer collection must throw.");
        }

        private static void SerializerEnvelopeCorruption()
        {
            Dictionary<ClothingSlot, ClothingMaskLayerData> layers;
            string error;
            Check.False(CardDataSerializer.TryDeserialize(null, out layers, out error), "Null payload must fail.");
            Check.Contains("missing or truncated", error, "Null-payload diagnostic mismatch.");
            Check.False(CardDataSerializer.TryDeserialize(new byte[11], out layers, out error), "Short envelope must fail.");

            byte[] valid = CardDataSerializer.Serialize(new ClothingMaskLayerData[] { Layer(ClothingSlot.Top, 7) });

            byte[] corrupt = Clone(valid);
            corrupt[0] = (byte)'X';
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Bad magic must fail.");
            Check.Contains("magic", error, "Bad-magic diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 4, CardDataSerializer.SchemaVersion + 1);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Unknown schema must fail.");
            Check.Contains("schema version", error, "Schema diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 8, -1);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Negative layer count must fail.");
            Check.Contains("layer count", error, "Negative-count diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 8, CardDataSerializer.MaximumLayerCount + 1);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Excessive layer count must fail.");

            corrupt = Append(valid, 0x42);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Trailing payload bytes must fail.");
            Check.Contains("trailing", error, "Trailing-byte diagnostic mismatch.");
            Check.Equal(0, layers.Count, "Failed deserialization must clear partial results.");
        }

        private static void SerializerLayerCorruption()
        {
            ClothingMaskLayerData layer = Layer(ClothingSlot.Gloves, 9);
            layer.Hash = "x";
            byte[] valid = CardDataSerializer.Serialize(new ClothingMaskLayerData[] { layer });
            Dictionary<ClothingSlot, ClothingMaskLayerData> layers;
            string error;

            for (int length = 0; length < valid.Length; length++)
            {
                byte[] truncated = Prefix(valid, length);
                bool accepted = CardDataSerializer.TryDeserialize(truncated, out layers, out error);
                Check.False(accepted, "Every strict prefix of a one-layer payload must fail; length=" + length + ".");
                Check.NotNull(error, "Every truncated payload must provide an error; length=" + length + ".");
            }

            byte[] corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 16, 99);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Unknown slot must fail.");
            Check.Contains("Unknown clothing slot", error, "Unknown-slot diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 12, 0);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Zero record length must fail.");
            Check.Contains("record length", error, "Zero-record diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 12, CardDataSerializer.MaximumSerializedLayerBytes + 1);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Oversized record length must fail.");

            int[] provenanceOffsets = FindV2ProvenanceOffsets(valid);
            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, provenanceOffsets[0], 99);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Unknown source contract must fail.");
            Check.Contains("source contract", error, "Source-contract diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, provenanceOffsets[1], 99);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Unknown gradient mode must fail.");
            Check.Contains("gradient handling mode", error, "Gradient-mode diagnostic mismatch.");

            int pngLengthOffset = valid.Length - layer.OriginalPngBytes.Length - 4;
            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, pngLengthOffset, 0);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Zero PNG length must fail.");
            Check.Contains("PNG byte length", error, "Zero-PNG diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, pngLengthOffset, int.MaxValue);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Huge PNG length must fail.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, pngLengthOffset, layer.OriginalPngBytes.Length + 1);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "PNG length beyond remaining payload must fail.");

            corrupt = Clone(valid);
            corrupt[38] = 0xFF;
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Invalid UTF-8 must fail.");
            Check.Contains("could not be read", error, "Invalid-UTF8 diagnostic mismatch.");

            corrupt = Clone(valid);
            WriteInt32LittleEndian(corrupt, 34, 4097);
            Check.False(CardDataSerializer.TryDeserialize(corrupt, out layers, out error), "Oversized serialized string length must fail.");
            Check.Contains("string length", error, "Oversized-string diagnostic mismatch.");
        }

        private static void SerializerLimits()
        {
            ClothingMaskLayerData noPng = Layer(ClothingSlot.Top, 1);
            noPng.OriginalPngBytes = null;
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { noPng }); },
                "Null PNG bytes must fail serialization.");

            noPng.OriginalPngBytes = new byte[0];
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { noPng }); },
                "Empty PNG bytes must fail serialization.");

            ClothingMaskLayerData[] maximumLayers = new ClothingMaskLayerData[CardDataSerializer.MaximumLayerCount];
            for (int i = 0; i < maximumLayers.Length; i++)
            {
                maximumLayers[i] = Layer((ClothingSlot)i, (byte)i);
            }

            byte[] ninePayload = CardDataSerializer.Serialize(maximumLayers);
            Check.True(ninePayload.Length > 12, "Exact maximum layer count must serialize.");

            ClothingMaskLayerData[] tooManyLayers = new ClothingMaskLayerData[CardDataSerializer.MaximumLayerCount + 1];
            for (int i = 0; i < tooManyLayers.Length; i++)
            {
                tooManyLayers[i] = Layer(ClothingSlot.Top, (byte)i);
            }

            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(tooManyLayers); },
                "Layer count above the hard maximum must fail.");

            ClothingMaskLayerData stringLayer = Layer(ClothingSlot.Top, 2);
            stringLayer.Hash = new string('a', 4096);
            Check.True(
                CardDataSerializer.Serialize(new ClothingMaskLayerData[] { stringLayer }).Length > 4096,
                "A string exactly at 4096 UTF-8 bytes must serialize.");
            stringLayer.Hash = new string('a', 4097);
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { stringLayer }); },
                "A string above 4096 UTF-8 bytes must fail.");

            ClothingMaskLayerData provenanceLayer = Layer(ClothingSlot.Top, 8);
            provenanceLayer.SourceProviderId = new string('p', CardDataSerializer.MaximumProviderIdBytes);
            provenanceLayer.SourceFingerprint = new string('f', CardDataSerializer.MaximumFingerprintBytes);
            provenanceLayer.SourceAsset = new string('a', CardDataSerializer.MaximumStringBytes);
            CardDataSerializer.Serialize(new ClothingMaskLayerData[] { provenanceLayer });
            provenanceLayer.SourceProviderId = new string('p', CardDataSerializer.MaximumProviderIdBytes + 1);
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { provenanceLayer }); },
                "Provider ID above its byte limit must fail.");
            provenanceLayer.SourceProviderId = null;
            provenanceLayer.SourceFingerprint = new string('f', CardDataSerializer.MaximumFingerprintBytes + 1);
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { provenanceLayer }); },
                "Fingerprint above its byte limit must fail.");
            provenanceLayer.SourceFingerprint = null;
            provenanceLayer.SourceAsset = new string('a', CardDataSerializer.MaximumStringBytes + 1);
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { provenanceLayer }); },
                "Source asset above its byte limit must fail.");

            ClothingMaskLayerData invalidMetadata = Layer(ClothingSlot.Top, 9);
            invalidMetadata.Width = 0;
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { invalidMetadata }); },
                "Zero serialized resolution must fail.");
            invalidMetadata.Width = PortableMaskFormatLimits.MaximumDimension + 1;
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { invalidMetadata }); },
                "Resolution above the absolute limit must fail.");
            invalidMetadata.Width = 64;
            invalidMetadata.ColorFormatVersion = 0;
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { invalidMetadata }); },
                "Non-positive color format version must fail.");

            ClothingMaskLayerData maximumPngLayer = Layer(ClothingSlot.Top, 3);
            maximumPngLayer.OriginalPngBytes = new byte[PortableMaskFormatLimits.MaximumPngBytes];
            byte[] maximumPngPayload = CardDataSerializer.Serialize(new ClothingMaskLayerData[] { maximumPngLayer });
            Check.True(
                maximumPngPayload.Length > PortableMaskFormatLimits.MaximumPngBytes,
                "PNG exactly at the absolute byte limit must serialize.");

            maximumPngPayload = null;
            maximumPngLayer.OriginalPngBytes = new byte[PortableMaskFormatLimits.MaximumPngBytes + 1];
            Check.Throws<IOException>(
                delegate { CardDataSerializer.Serialize(new ClothingMaskLayerData[] { maximumPngLayer }); },
                "PNG above the absolute byte limit must fail.");
        }

        private static void HashAndDeepClone()
        {
            Check.Equal(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                HashUtility.Sha256(Encoding.ASCII.GetBytes("abc")),
                "SHA-256 known vector mismatch.");
            Check.Throws<ArgumentNullException>(
                delegate { HashUtility.Sha256(null); },
                "Null SHA-256 input must throw.");

            ClothingMaskLayerData original = Layer(ClothingSlot.Bra, 44);
            original.BoundItemIdentity = Identity(ClothingSlot.Bra, 107, 100, 200, "guid");
            original.SourceContract = MaskSourceContract.ExternalRgbStateCoverage;
            original.GradientHandlingMode = GradientHandlingMode.PreserveContinuous;
            original.SourceProviderId = "provider";
            original.SourceFingerprint = "fingerprint";
            original.SourceAsset = "asset";
            ClothingMaskLayerData clone = original.DeepClone();
            Check.NotSame(original, clone, "Layer clone must be a new object.");
            Check.NotSame(original.OriginalPngBytes, clone.OriginalPngBytes, "Layer clone must copy PNG bytes.");
            Check.NotSame(original.BoundItemIdentity, clone.BoundItemIdentity, "Layer clone must copy identity.");
            Check.SequenceEqual(original.OriginalPngBytes, clone.OriginalPngBytes, "Layer clone PNG mismatch.");
            Check.Equal(original.SourceContract, clone.SourceContract, "Layer clone source contract mismatch.");
            Check.Equal(original.GradientHandlingMode, clone.GradientHandlingMode, "Layer clone gradient mode mismatch.");
            Check.Equal(original.SourceProviderId, clone.SourceProviderId, "Layer clone provider mismatch.");
            Check.Equal(original.SourceFingerprint, clone.SourceFingerprint, "Layer clone fingerprint mismatch.");
            Check.Equal(original.SourceAsset, clone.SourceAsset, "Layer clone asset mismatch.");

            clone.OriginalPngBytes[0]++;
            clone.BoundItemIdentity.LocalItemId++;
            Check.False(original.OriginalPngBytes[0] == clone.OriginalPngBytes[0], "Mutating cloned PNG must not affect original.");
            Check.False(
                original.BoundItemIdentity.LocalItemId == clone.BoundItemIdentity.LocalItemId,
                "Mutating cloned identity must not affect original.");

            ClothingMaskLayerData noOptionalData = Layer(ClothingSlot.Bra, 45);
            noOptionalData.BoundItemIdentity = null;
            noOptionalData.OriginalPngBytes = null;
            ClothingMaskLayerData noOptionalClone = noOptionalData.DeepClone();
            Check.Null(noOptionalClone.BoundItemIdentity, "Null identity must remain null in clone.");
            Check.Null(noOptionalClone.OriginalPngBytes, "Null PNG must remain null in clone.");
        }

        private static void BindingLocalIdentity()
        {
            ClothingItemIdentity bound = Identity(ClothingSlot.Bottom, 106, 123, 500, null);
            ClothingItemIdentity same = Identity(ClothingSlot.Bottom, 106, 123, 999, string.Empty);
            Check.True(bound.Matches(same), "Local identity must match slot/category/local ID.");

            ClothingItemIdentity differentLocal = Identity(ClothingSlot.Bottom, 106, 124, 500, null);
            Check.False(bound.Matches(differentLocal), "Different local ID must not match.");

            ClothingItemIdentity differentCategory = Identity(ClothingSlot.Bottom, 107, 123, 500, null);
            Check.False(bound.Matches(differentCategory), "Different category must not match.");

            ClothingItemIdentity differentSlot = Identity(ClothingSlot.Top, 106, 123, 500, null);
            Check.False(bound.Matches(differentSlot), "Different slot must not match.");

            ClothingItemIdentity emptyCurrent = Identity(ClothingSlot.Bottom, 106, 0, 500, null);
            Check.False(bound.Matches(emptyCurrent), "Empty current item must not match.");
            Check.False(bound.Matches(null), "A null current identity must not match.");

            bound.LocalItemId = 0;
            Check.False(bound.Matches(same), "An empty bound item must not match.");
        }

        private static void BindingSideloaderIdentity()
        {
            ClothingItemIdentity bound = Identity(ClothingSlot.Gloves, 110, 100, 700, "NightOwlZzz.asset");
            ClothingItemIdentity sameStable = Identity(ClothingSlot.Gloves, 110, 999, 700, "NightOwlZzz.asset");
            Check.True(
                bound.Matches(sameStable),
                "Sideloader GUID + original ID must remain stable across local-ID changes.");

            ClothingItemIdentity differentOriginal = Identity(ClothingSlot.Gloves, 110, 100, 701, "NightOwlZzz.asset");
            Check.False(bound.Matches(differentOriginal), "Different Sideloader original ID must not match.");

            ClothingItemIdentity differentCase = Identity(ClothingSlot.Gloves, 110, 100, 700, "nightowlzzz.asset");
            Check.False(bound.Matches(differentCase), "Sideloader GUID comparison must be ordinal/case-sensitive.");

            ClothingItemIdentity noGuid = Identity(ClothingSlot.Gloves, 110, 100, 700, null);
            Check.False(bound.Matches(noGuid), "One missing GUID must not fall back to local identity.");

            ClothingItemIdentity differentCategory = Identity(ClothingSlot.Gloves, 111, 999, 700, "NightOwlZzz.asset");
            Check.False(bound.Matches(differentCategory), "Category mismatch must fail before stable identity.");

            sameStable.LocalItemId = 0;
            Check.False(bound.Matches(sameStable), "Stable GUID still requires an actual current item.");
        }

        private static MaskDecodeOptions Options(
            ColorClassificationMode mode,
            int tolerance,
            UnknownColorPolicy unknownPolicy)
        {
            MaskDecodeOptions options = new MaskDecodeOptions();
            options.ClassificationMode = mode;
            options.ColorTolerance = tolerance;
            options.UnknownColorPolicy = unknownPolicy;
            options.GradientHandlingMode = GradientHandlingMode.StrictCategorical;
            return options;
        }

        private static SemanticMask Decode(
            Rgba32[] pixels,
            int width,
            int height,
            MaskDecodeOptions options,
            out MaskColorStatistics statistics)
        {
            SemanticMask mask;
            string error;
            bool accepted = MaskColorDecoder.TryDecode(
                pixels,
                width,
                height,
                options,
                out mask,
                out statistics,
                out error);
            Check.True(accepted, "Mask decode unexpectedly failed: " + (error ?? "<no error>"));
            Check.NotNull(mask, "Successful decode must return a semantic mask.");
            Check.Null(error, "Successful decode must not return an error.");
            return mask;
        }

        private static SemanticMask Mask(int width, int height, params MaskPixelRule[] rules)
        {
            return new SemanticMask(width, height, rules);
        }

        private static ClothingMaskLayerData Layer(ClothingSlot slot, byte seed)
        {
            ClothingMaskLayerData layer = new ClothingMaskLayerData();
            layer.Slot = slot;
            layer.Enabled = true;
            layer.OriginalPngBytes = new byte[] { seed, (byte)(seed + 1), (byte)(seed + 2), (byte)(seed + 3) };
            layer.Width = 64;
            layer.Height = 64;
            layer.Hash = "hash-" + seed;
            layer.CreatedWithPluginVersion = "0.1.0";
            layer.LastValidationResult = null;
            return layer;
        }

        private static ClothingItemIdentity Identity(
            ClothingSlot slot,
            int category,
            int localId,
            int originalId,
            string guid)
        {
            ClothingItemIdentity identity = new ClothingItemIdentity();
            identity.Slot = slot;
            identity.Category = category;
            identity.LocalItemId = localId;
            identity.OriginalItemId = originalId;
            identity.SideloaderGuid = guid;
            identity.DisplayName = "item";
            return identity;
        }

        private static int[] FindV2ProvenanceOffsets(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload, false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                stream.Position = 12;
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadBoolean();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                if (reader.ReadBoolean())
                {
                    reader.ReadInt32();
                }

                SkipSerializedString(reader);
                SkipSerializedString(reader);
                SkipSerializedString(reader);
                if (reader.ReadBoolean())
                {
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    SkipSerializedString(reader);
                    SkipSerializedString(reader);
                }

                int sourceContractOffset = (int)stream.Position;
                reader.ReadInt32();
                int gradientHandlingOffset = (int)stream.Position;
                return new int[] { sourceContractOffset, gradientHandlingOffset };
            }
        }

        private static void SkipSerializedString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length > 0)
            {
                reader.BaseStream.Position += length;
            }
        }

        private static byte[] BuildPngHeader(int width, int height, byte bitDepth, byte colorType)
        {
            return BuildPngHeader((uint)width, (uint)height, bitDepth, colorType);
        }

        private static byte[] BuildPngHeader(uint width, uint height, byte bitDepth, byte colorType)
        {
            byte[] bytes = new byte[33];
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            Array.Copy(signature, 0, bytes, 0, signature.Length);
            WriteUInt32BigEndian(bytes, 8, 13);
            bytes[12] = (byte)'I';
            bytes[13] = (byte)'H';
            bytes[14] = (byte)'D';
            bytes[15] = (byte)'R';
            WriteUInt32BigEndian(bytes, 16, width);
            WriteUInt32BigEndian(bytes, 20, height);
            bytes[24] = bitDepth;
            bytes[25] = colorType;
            bytes[26] = 0;
            bytes[27] = 0;
            bytes[28] = 0;
            return bytes;
        }

        private static void WriteUInt32BigEndian(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt32LittleEndian(byte[] bytes, int offset)
        {
            return bytes[offset] |
                   (bytes[offset + 1] << 8) |
                   (bytes[offset + 2] << 16) |
                   (bytes[offset + 3] << 24);
        }

        private static byte[] Clone(byte[] bytes)
        {
            return (byte[])bytes.Clone();
        }

        private static byte[] Prefix(byte[] bytes, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(bytes, 0, result, 0, length);
            return result;
        }

        private static byte[] Append(byte[] bytes, byte value)
        {
            byte[] result = new byte[bytes.Length + 1];
            Array.Copy(bytes, 0, result, 0, bytes.Length);
            result[result.Length - 1] = value;
            return result;
        }
    }
}
