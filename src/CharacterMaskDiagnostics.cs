using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class CharacterMaskDiagnostics
    {
        private readonly NativeMaskLayerStore nativeLayers;
        private readonly CharacterNativeMaskService nativeMasks;
        private readonly CharacterExternalMaskCoordinator externalMasks;
        private readonly CharacterClothingRuntime runtimeState;
        private readonly CharacterMaskCompositionEngine composition;

        public CharacterMaskDiagnostics(
            NativeMaskLayerStore layerStore,
            CharacterNativeMaskService nativeMaskService,
            CharacterExternalMaskCoordinator externalMaskCoordinator,
            CharacterClothingRuntime clothingRuntime,
            CharacterMaskCompositionEngine compositionEngine)
        {
            if (layerStore == null)
            {
                throw new ArgumentNullException("layerStore");
            }

            if (nativeMaskService == null)
            {
                throw new ArgumentNullException("nativeMaskService");
            }

            if (externalMaskCoordinator == null)
            {
                throw new ArgumentNullException("externalMaskCoordinator");
            }

            if (clothingRuntime == null)
            {
                throw new ArgumentNullException("clothingRuntime");
            }

            if (compositionEngine == null)
            {
                throw new ArgumentNullException("compositionEngine");
            }

            nativeLayers = layerStore;
            nativeMasks = nativeMaskService;
            externalMasks = externalMaskCoordinator;
            runtimeState = clothingRuntime;
            composition = compositionEngine;
        }

        public string DescribeForMaker(ChaControl character, ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return "Invalid clothing slot.";
            }

            NativeMaskLayerSlotState native = nativeLayers.Get(slot);
            ClothingMaskLayerData layer = native.Layer;
            if (layer == null)
            {
                if (externalMasks.HasSource(slot))
                {
                    return "Compatible mask data is active and imports automatically.";
                }

                return nativeMasks.HasBindableItem(character, slot)
                    ? "No native mask loaded."
                    : "Equip an item before loading a native mask.";
            }

            if (native.SemanticMask == null)
            {
                return "Stored mask is invalid; see log.";
            }

            if (!layer.Enabled)
            {
                return string.Format(
                    "{0}x{1} mask - disabled.",
                    layer.Width,
                    layer.Height);
            }

            if (ExternalCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    ExternalMaskProvider.IsSourceProviderInstalled,
                    layer.SourceContract,
                    layer.SourceProviderId))
            {
                return string.Format(
                    "{0}x{1} mask - managed by an installed body-mask provider.",
                    layer.Width,
                    layer.Height);
            }

            ClothingItemIdentity current =
                runtimeState.GetCurrentIdentity(character, slot);
            bool bindingMatches = layer.BoundItemIdentity != null &&
                                  layer.BoundItemIdentity.Matches(
                                      current);
            if (!bindingMatches)
            {
                return string.Format(
                    "{0}x{1} mask - current item does not match.",
                    layer.Width,
                    layer.Height);
            }

            GarmentState state = runtimeState.ResolveStateForLayer(
                character,
                index,
                nativeLayers);
            return string.Format(
                "{0}x{1} mask - {2} ({3}).",
                layer.Width,
                layer.Height,
                composition.IsNativeLayerActive(character, index) ? "active" : "ready",
                state);
        }

        public string Describe(ChaControl character, ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return "invalid slot";
            }

            ClothingItemIdentity current =
                runtimeState.GetCurrentIdentity(character, slot);
            byte raw = runtimeState.GetRawState(character, index);
            GarmentState state = ClothingStateResolver.Resolve(raw);
            bool runtimeObject = (runtimeState.AvailabilityMask & (1 << index)) != 0;
            bool structurallySuppressed = runtimeState.IsStructurallySuppressed(index);
            NativeMaskLayerSlotState native = nativeLayers.Get(slot);
            ClothingMaskLayerData layer = native.Layer;
            if (layer == null)
            {
                return string.Format(
                    "no mask, state={0} (raw {1}), runtimeObject={2}, structurallySuppressed={3}, current=[{4}]",
                    state,
                    raw,
                    runtimeObject,
                    structurallySuppressed,
                    current == null ? "none" : current.ToString());
            }

            state = runtimeState.ResolveStateForLayer(character, index, nativeLayers);
            bool bindingMatches = layer.BoundItemIdentity != null &&
                                  layer.BoundItemIdentity.Matches(
                                      current);
            string hash = string.IsNullOrEmpty(layer.Hash)
                ? "no hash"
                : layer.Hash.Substring(0, Math.Min(12, layer.Hash.Length));
            return string.Format(
                "{0}, active={1}, {2}x{3}, state={4} (raw {5}), runtimeObject={6}, " +
                "structurallySuppressed={7}, binding={8}, current=[{9}], bound=[{10}], " +
                "hash={11}, stats=[{12}], {13}",
                layer.Enabled ? "enabled" : "disabled",
                composition.IsNativeLayerActive(character, index),
                layer.Width,
                layer.Height,
                state,
                raw,
                runtimeObject,
                structurallySuppressed,
                bindingMatches ? "match" : "mismatch",
                current == null ? "none" : current.ToString(),
                layer.BoundItemIdentity == null ? "none" :
                    layer.BoundItemIdentity.ToString(),
                hash,
                native.Statistics == null ? "none" : native.Statistics.ToString(),
                composition.GetNativeInactiveReason(character, index));
        }
    }
}
