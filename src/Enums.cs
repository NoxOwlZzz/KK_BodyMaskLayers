namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public enum ClothingSlot
    {
        Top = 0,
        Bottom = 1,
        Bra = 2,
        Shorts = 3,
        Gloves = 4,
        Pantyhose = 5,
        Socks = 6,
        IndoorShoes = 7,
        OutdoorShoes = 8
    }

    public enum GarmentState
    {
        Unknown = 0,
        Full = 1,
        Partial = 2,
        Off = 3
    }

    public enum MaskPixelRule : byte
    {
        Unknown = 0,
        NeverHide = 1,
        HideWhenFull = 2,
        HideWhenNotOff = 3
    }

    public enum UnknownStatePolicy
    {
        NoContribution = 0,
        TreatAsPartial = 1,
        TreatAsFull = 2,
        PreserveLastKnown = 3
    }

    public enum UnknownColorPolicy
    {
        RejectMask = 0,
        NoContribution = 1,
        HideWhenNotOff = 2
    }

    public enum ColorClassificationMode
    {
        Exact = 0,
        Threshold = 1,
        NearestCategory = 2
    }

    public enum GradientHandlingMode
    {
        Auto = 0,
        PreserveContinuous = 1,
        StrictCategorical = 2
    }

    public enum MaskSourceContract
    {
        Native = 0,
        ExternalRgbStateCoverage = 1
    }

    public enum CompiledMaskKind
    {
        Binary = 0,
        Continuous = 1
    }

}
