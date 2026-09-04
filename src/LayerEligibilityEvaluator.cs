namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal enum NativeLayerInactiveReason
    {
        None = 0,
        NoLayer = 1,
        PluginDisabled = 2,
        LayerDisabled = 3,
        MissingDecodedMask = 4,
        UpstreamPluginOwnsSource = 5,
        MissingRuntimeObject = 6,
        StructurallySuppressed = 7,
        OtherShoeTypeSelected = 8,
        BindingMismatch = 9
    }

    internal struct NativeLayerEligibilityContext
    {
        public NativeLayerEligibilityContext(
            int slotIndex,
            bool pluginEnabled,
            bool externalPluginInstalled,
            int shoesType,
            int availabilityMask,
            int structuralFlags,
            ClothingItemIdentity currentIdentity)
        {
            SlotIndex = slotIndex;
            PluginEnabled = pluginEnabled;
            ExternalPluginInstalled = externalPluginInstalled;
            ShoesType = shoesType;
            AvailabilityMask = availabilityMask;
            StructuralFlags = structuralFlags;
            CurrentIdentity = currentIdentity;
        }

        public int SlotIndex;
        public bool PluginEnabled;
        public bool ExternalPluginInstalled;
        public int ShoesType;
        public int AvailabilityMask;
        public int StructuralFlags;
        public ClothingItemIdentity CurrentIdentity;
    }

    internal static class LayerEligibilityEvaluator
    {
        public static NativeLayerInactiveReason EvaluateNative(
            ClothingMaskLayerData layer,
            bool decodedMaskAvailable,
            NativeLayerEligibilityContext context)
        {
            NativeLayerInactiveReason reason = EvaluateNativeWithoutCurrentIdentity(
                layer,
                decodedMaskAvailable,
                context);
            return reason == NativeLayerInactiveReason.None
                ? EvaluateNativeBinding(
                    layer,
                    context.CurrentIdentity)
                : reason;
        }

        public static NativeLayerInactiveReason EvaluateNativeWithoutCurrentIdentity(
            ClothingMaskLayerData layer,
            bool decodedMaskAvailable,
            NativeLayerEligibilityContext context)
        {
            if (layer == null)
            {
                return NativeLayerInactiveReason.NoLayer;
            }

            if (!context.PluginEnabled)
            {
                return NativeLayerInactiveReason.PluginDisabled;
            }

            if (!layer.Enabled)
            {
                return NativeLayerInactiveReason.LayerDisabled;
            }

            if (!decodedMaskAvailable)
            {
                return NativeLayerInactiveReason.MissingDecodedMask;
            }

            if (ExternalCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    context.ExternalPluginInstalled,
                    layer.SourceContract,
                    layer.SourceProviderId))
            {
                return NativeLayerInactiveReason.UpstreamPluginOwnsSource;
            }

            if ((context.AvailabilityMask & (1 << context.SlotIndex)) == 0)
            {
                return NativeLayerInactiveReason.MissingRuntimeObject;
            }

            if (IsStructurallySuppressed(context.SlotIndex, context.StructuralFlags))
            {
                return NativeLayerInactiveReason.StructurallySuppressed;
            }

            if (!IsSelectedShoe(context.SlotIndex, context.ShoesType))
            {
                return NativeLayerInactiveReason.OtherShoeTypeSelected;
            }

            if (layer.BoundItemIdentity == null)
            {
                return NativeLayerInactiveReason.BindingMismatch;
            }

            return NativeLayerInactiveReason.None;
        }

        public static NativeLayerInactiveReason EvaluateNativeBinding(
            ClothingMaskLayerData layer,
            ClothingItemIdentity currentIdentity)
        {
            return layer == null || layer.BoundItemIdentity == null ||
                   !layer.BoundItemIdentity.Matches(currentIdentity)
                ? NativeLayerInactiveReason.BindingMismatch
                : NativeLayerInactiveReason.None;
        }

        public static bool NativeOwnsExternalSource(
            ClothingMaskLayerData nativeLayer,
            bool decodedMaskAvailable,
            NativeLayerEligibilityContext context,
            string externalFingerprint)
        {
            return CanNativeOwnExternalSourceWithoutCurrentIdentity(
                       nativeLayer,
                       decodedMaskAvailable,
                       context) &&
                   NativeOwnsExternalSourceAfterPrecheck(
                       nativeLayer,
                       context.CurrentIdentity,
                       externalFingerprint);
        }

        public static bool CanNativeOwnExternalSourceWithoutCurrentIdentity(
            ClothingMaskLayerData nativeLayer,
            bool decodedMaskAvailable,
            NativeLayerEligibilityContext context)
        {
            if (nativeLayer == null || !decodedMaskAvailable ||
                nativeLayer.BoundItemIdentity == null ||
                (context.AvailabilityMask & (1 << context.SlotIndex)) == 0 ||
                IsStructurallySuppressed(context.SlotIndex, context.StructuralFlags) ||
                !IsSelectedShoe(context.SlotIndex, context.ShoesType))
            {
                return false;
            }

            return true;
        }

        public static bool NativeOwnsExternalSourceAfterPrecheck(
            ClothingMaskLayerData nativeLayer,
            ClothingItemIdentity currentIdentity,
            string externalFingerprint)
        {
            bool bindingMatches = nativeLayer.BoundItemIdentity.Matches(
                currentIdentity);
            return ExternalCompatibilityPolicy.NativeLayerOwnsExternalSource(
                nativeLayer,
                true,
                bindingMatches,
                externalFingerprint);
        }

        public static bool IsSelectedShoe(int slotIndex, int shoesType)
        {
            return (slotIndex != (int)ClothingSlot.IndoorShoes || shoesType == 0) &&
                   (slotIndex != (int)ClothingSlot.OutdoorShoes || shoesType == 1);
        }

        public static bool IsStructurallySuppressed(int slotIndex, int structuralFlags)
        {
            return (slotIndex == (int)ClothingSlot.Bottom && (structuralFlags & 1) != 0) ||
                   (slotIndex == (int)ClothingSlot.Bra && (structuralFlags & 2) != 0) ||
                   (slotIndex == (int)ClothingSlot.Shorts && (structuralFlags & 4) != 0);
        }

        public static string DescribeInactive(NativeLayerInactiveReason reason)
        {
            switch (reason)
            {
                case NativeLayerInactiveReason.NoLayer:
                    return "inactive: no mask";
                case NativeLayerInactiveReason.PluginDisabled:
                    return "inactive: plugin disabled";
                case NativeLayerInactiveReason.LayerDisabled:
                    return "inactive: layer disabled";
                case NativeLayerInactiveReason.MissingDecodedMask:
                    return "inactive: invalid or undecoded mask";
                case NativeLayerInactiveReason.UpstreamPluginOwnsSource:
                    return "inactive: upstream KK_ChaAlphaMask owns the converted external source";
                case NativeLayerInactiveReason.MissingRuntimeObject:
                    return "inactive: no runtime clothing object";
                case NativeLayerInactiveReason.StructurallySuppressed:
                    return "inactive: garment is integrated into another slot";
                case NativeLayerInactiveReason.OtherShoeTypeSelected:
                    return "inactive: other shoe type is selected";
                case NativeLayerInactiveReason.BindingMismatch:
                    return "inactive: item binding mismatch";
                default:
                    return "active";
            }
        }
    }
}
