using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class CompiledStateMask
    {
        private readonly uint[][] _binaryStateBits;
        private readonly byte[][] _continuousStateCoverage;
        private readonly uint[] _categoricalUnknownBits;

        private CompiledStateMask(
            int width,
            int height,
            uint[][] binaryStateBits,
            byte[][] continuousStateCoverage,
            uint[] categoricalUnknownBits,
            bool hasCategoricalRules)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height");
            }

            Width = width;
            Height = height;
            PixelCount = checked(width * height);
            _binaryStateBits = binaryStateBits;
            _continuousStateCoverage = continuousStateCoverage;
            _categoricalUnknownBits = categoricalUnknownBits;
            HasCategoricalRules = hasCategoricalRules;
            Kind = binaryStateBits != null
                ? CompiledMaskKind.Binary
                : CompiledMaskKind.Continuous;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public int PixelCount { get; private set; }

        public CompiledMaskKind Kind { get; private set; }

        public bool IsBinary
        {
            get { return Kind == CompiledMaskKind.Binary; }
        }

        public bool HasCategoricalRules { get; private set; }

        public long StorageBytes
        {
            get
            {
                long bytes = 0;
                if (_binaryStateBits != null)
                {
                    bytes += UniqueArrayBytes(_binaryStateBits[0], _binaryStateBits[1], _binaryStateBits[2], 4);
                    if (_categoricalUnknownBits != null)
                    {
                        bytes += (long)_categoricalUnknownBits.Length * 4;
                    }
                }
                else
                {
                    bytes += UniqueArrayBytes(
                        _continuousStateCoverage[0],
                        _continuousStateCoverage[1],
                        _continuousStateCoverage[2],
                        1);
                }

                return bytes;
            }
        }

        public byte GetHideCoverage(int pixelIndex, byte rawState)
        {
            if (pixelIndex < 0 || pixelIndex >= PixelCount)
            {
                throw new ArgumentOutOfRangeException("pixelIndex");
            }

            if (rawState > 2)
            {
                return 0;
            }

            if (_binaryStateBits != null)
            {
                uint word = _binaryStateBits[rawState][pixelIndex >> 5];
                return (word & (1u << (pixelIndex & 31))) != 0 ? byte.MaxValue : (byte)0;
            }

            return _continuousStateCoverage[rawState][pixelIndex];
        }

        public bool HasAnyCoverage(byte rawState)
        {
            if (rawState > 2)
            {
                return false;
            }

            if (_binaryStateBits != null)
            {
                uint[] words = _binaryStateBits[rawState];
                for (int index = 0; index < words.Length; index++)
                {
                    if (words[index] != 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            byte[] coverage = _continuousStateCoverage[rawState];
            for (int index = 0; index < coverage.Length; index++)
            {
                if (coverage[index] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal uint[] GetBinaryStateBits(byte rawState)
        {
            return rawState <= 2 && _binaryStateBits != null
                ? _binaryStateBits[rawState]
                : null;
        }

        internal byte[] GetContinuousStateCoverage(byte rawState)
        {
            return rawState <= 2 && _continuousStateCoverage != null
                ? _continuousStateCoverage[rawState]
                : null;
        }

        internal bool IsCategoricalUnknown(int pixelIndex)
        {
            return _categoricalUnknownBits != null &&
                   (_categoricalUnknownBits[pixelIndex >> 5] &
                    (1u << (pixelIndex & 31))) != 0;
        }

        internal static CompiledStateMask FromCategorical(
            int width,
            int height,
            MaskPixelRule[] rules)
        {
            ValidateDimensionsAndLength(width, height, rules, "rules");
            int wordCount = (rules.Length + 31) >> 5;
            uint[] full = new uint[wordCount];
            uint[] partial = new uint[wordCount];
            uint[] unknown = null;
            for (int index = 0; index < rules.Length; index++)
            {
                MaskPixelRule rule = rules[index];
                uint bit = 1u << (index & 31);
                int wordIndex = index >> 5;
                if (rule == MaskPixelRule.HideWhenFull || rule == MaskPixelRule.HideWhenNotOff)
                {
                    full[wordIndex] |= bit;
                }

                if (rule == MaskPixelRule.HideWhenNotOff)
                {
                    partial[wordIndex] |= bit;
                }

                if (rule == MaskPixelRule.Unknown)
                {
                    if (unknown == null)
                    {
                        unknown = new uint[wordCount];
                    }

                    unknown[wordIndex] |= bit;
                }
            }

            if (WordArraysEqual(full, partial))
            {
                partial = full;
            }

            return new CompiledStateMask(
                width,
                height,
                new uint[][] { full, partial, partial },
                null,
                unknown,
                true);
        }

        internal static CompiledStateMask FromCoverageOwned(
            int width,
            int height,
            byte[] state0,
            byte[] state1,
            byte[] state2)
        {
            ValidateDimensionsAndLength(width, height, state0, "state0");
            ValidateDimensionsAndLength(width, height, state1, "state1");
            ValidateDimensionsAndLength(width, height, state2, "state2");

            if (ArraysEqual(state0, state1))
            {
                state1 = state0;
            }

            if (ArraysEqual(state0, state2))
            {
                state2 = state0;
            }
            else if (ArraysEqual(state1, state2))
            {
                state2 = state1;
            }

            bool binary = IsBinaryCoverage(state0) &&
                          IsBinaryCoverage(state1) &&
                          IsBinaryCoverage(state2);
            if (!binary)
            {
                return new CompiledStateMask(
                    width,
                    height,
                    null,
                    new byte[][] { state0, state1, state2 },
                    null,
                    false);
            }

            uint[] bits0 = PackBits(state0);
            uint[] bits1 = object.ReferenceEquals(state0, state1) ? bits0 : PackBits(state1);
            uint[] bits2 = object.ReferenceEquals(state0, state2)
                ? bits0
                : (object.ReferenceEquals(state1, state2) ? bits1 : PackBits(state2));
            return new CompiledStateMask(
                width,
                height,
                new uint[][] { bits0, bits1, bits2 },
                null,
                null,
                false);
        }

        private static uint[] PackBits(byte[] coverage)
        {
            uint[] words = new uint[(coverage.Length + 31) >> 5];
            for (int index = 0; index < coverage.Length; index++)
            {
                if (coverage[index] != 0)
                {
                    words[index >> 5] |= 1u << (index & 31);
                }
            }

            return words;
        }

        private static bool IsBinaryCoverage(byte[] coverage)
        {
            for (int index = 0; index < coverage.Length; index++)
            {
                if (coverage[index] != 0 && coverage[index] != byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ArraysEqual(byte[] first, byte[] second)
        {
            if (object.ReferenceEquals(first, second))
            {
                return true;
            }

            for (int index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool WordArraysEqual(uint[] first, uint[] second)
        {
            if (object.ReferenceEquals(first, second))
            {
                return true;
            }

            for (int index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static long UniqueArrayBytes(Array first, Array second, Array third, int elementBytes)
        {
            long bytes = (long)first.Length * elementBytes;
            if (!object.ReferenceEquals(first, second))
            {
                bytes += (long)second.Length * elementBytes;
            }

            if (!object.ReferenceEquals(first, third) && !object.ReferenceEquals(second, third))
            {
                bytes += (long)third.Length * elementBytes;
            }

            return bytes;
        }

        private static void ValidateDimensionsAndLength(
            int width,
            int height,
            Array values,
            string parameterName)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height");
            }

            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (values.Length != checked(width * height))
            {
                throw new ArgumentException(
                    "The value count does not match the mask dimensions.",
                    parameterName);
            }
        }
    }

    public sealed class SemanticMask
    {
        private readonly CompiledStateMask _compiled;

        public SemanticMask(int width, int height, MaskPixelRule[] rules)
            : this(CompiledStateMask.FromCategorical(width, height, rules))
        {
        }

        private SemanticMask(CompiledStateMask compiled)
        {
            if (compiled == null)
            {
                throw new ArgumentNullException("compiled");
            }

            _compiled = compiled;
        }

        public int Width
        {
            get { return _compiled.Width; }
        }

        public int Height
        {
            get { return _compiled.Height; }
        }

        public int PixelCount
        {
            get { return _compiled.PixelCount; }
        }

        public CompiledMaskKind Kind
        {
            get { return _compiled.Kind; }
        }

        public bool IsBinary
        {
            get { return _compiled.IsBinary; }
        }

        public bool HasCategoricalRules
        {
            get { return _compiled.HasCategoricalRules; }
        }

        public long StorageBytes
        {
            get { return _compiled.StorageBytes; }
        }

        public CompiledStateMask Compiled
        {
            get { return _compiled; }
        }

        public MaskPixelRule[] Rules
        {
            get
            {
                MaskPixelRule[] rules = new MaskPixelRule[PixelCount];
                for (int index = 0; index < rules.Length; index++)
                {
                    rules[index] = GetCategoricalRule(index);
                }

                return rules;
            }
        }

        public byte GetHideCoverage(int pixelIndex, GarmentState state)
        {
            switch (state)
            {
                case GarmentState.Full:
                    return _compiled.GetHideCoverage(pixelIndex, 0);
                case GarmentState.Partial:
                    return _compiled.GetHideCoverage(pixelIndex, 1);
                default:
                    return 0;
            }
        }

        public byte GetHideCoverageForRawState(int pixelIndex, byte rawState)
        {
            return _compiled.GetHideCoverage(pixelIndex, rawState);
        }

        public bool AreStatePlanesEquivalent(byte firstRawState, byte secondRawState)
        {
            if (firstRawState > 2 || secondRawState > 2)
            {
                return firstRawState > 2 && secondRawState > 2;
            }

            if (IsBinary)
            {
                return object.ReferenceEquals(
                    _compiled.GetBinaryStateBits(firstRawState),
                    _compiled.GetBinaryStateBits(secondRawState));
            }

            return object.ReferenceEquals(
                _compiled.GetContinuousStateCoverage(firstRawState),
                _compiled.GetContinuousStateCoverage(secondRawState));
        }

        public MaskPixelRule GetCategoricalRule(int pixelIndex)
        {
            if (_compiled.IsCategoricalUnknown(pixelIndex))
            {
                return MaskPixelRule.Unknown;
            }

            byte full = _compiled.GetHideCoverage(pixelIndex, 0);
            byte partial1 = _compiled.GetHideCoverage(pixelIndex, 1);
            byte partial2 = _compiled.GetHideCoverage(pixelIndex, 2);
            if (full == 0 && partial1 == 0 && partial2 == 0)
            {
                return MaskPixelRule.NeverHide;
            }

            if (full == byte.MaxValue && partial1 == 0 && partial2 == 0)
            {
                return MaskPixelRule.HideWhenFull;
            }

            if (full == byte.MaxValue &&
                partial1 == byte.MaxValue &&
                partial2 == byte.MaxValue)
            {
                return MaskPixelRule.HideWhenNotOff;
            }

            return MaskPixelRule.Unknown;
        }

        public static SemanticMask FromStateCoverage(
            int width,
            int height,
            byte[] state0,
            byte[] state1,
            byte[] state2)
        {
            if (state0 == null)
            {
                throw new ArgumentNullException("state0");
            }

            if (state1 == null)
            {
                throw new ArgumentNullException("state1");
            }

            if (state2 == null)
            {
                throw new ArgumentNullException("state2");
            }

            return FromStateCoverageOwned(
                width,
                height,
                (byte[])state0.Clone(),
                (byte[])state1.Clone(),
                (byte[])state2.Clone());
        }

        internal static SemanticMask FromStateCoverageOwned(
            int width,
            int height,
            byte[] state0,
            byte[] state1,
            byte[] state2)
        {
            return new SemanticMask(
                CompiledStateMask.FromCoverageOwned(width, height, state0, state1, state2));
        }

        internal uint[] GetBinaryStateBits(byte rawState)
        {
            return _compiled.GetBinaryStateBits(rawState);
        }

        internal byte[] GetContinuousStateCoverage(byte rawState)
        {
            return _compiled.GetContinuousStateCoverage(rawState);
        }
    }

    public sealed class MaskColorStatistics
    {
        public int YellowPixels;
        public int GreenPixels;
        public int BlackPixels;
        public int RedPixels;
        public int BluePixels;
        public int UnknownPixels;
        public int UnexpectedAlphaPixels;
        public int ContinuousPixels;
        public int EdgePixels;
        public int AmbiguousPixels;
        public int PackedDataPixels;
        public int PixelsWithBlueData;
        public int LegacyStatePixels;

        public int TotalPixels
        {
            get
            {
                return YellowPixels + GreenPixels + BlackPixels + RedPixels + BluePixels +
                       UnknownPixels + ContinuousPixels + LegacyStatePixels;
            }
        }

        public MaskColorStatistics Clone()
        {
            return (MaskColorStatistics)MemberwiseClone();
        }

        public override string ToString()
        {
            return string.Format(
                "yellow={0}, green={1}, black={2}, red={3}, blue={4}, unknown={5}, continuous={6}, edge={7}, ambiguous={8}, packed={9}, blueData={10}, legacy={11}, alphaUnexpected={12}",
                YellowPixels,
                GreenPixels,
                BlackPixels,
                RedPixels,
                BluePixels,
                UnknownPixels,
                ContinuousPixels,
                EdgePixels,
                AmbiguousPixels,
                PackedDataPixels,
                PixelsWithBlueData,
                LegacyStatePixels,
                UnexpectedAlphaPixels);
        }
    }

    public sealed class MaskDecodeOptions
    {
        public ColorClassificationMode ClassificationMode = ColorClassificationMode.Threshold;
        public int ColorTolerance = 12;
        public UnknownColorPolicy UnknownColorPolicy = UnknownColorPolicy.RejectMask;
        public GradientHandlingMode GradientHandlingMode = GradientHandlingMode.Auto;
    }
}
