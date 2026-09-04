using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal interface IRuntimeDirtySink
    {
        string LastRuntimeDirtyReason { get; }

        void RequestRuntimeDirty(string reason, bool baseContentMayHaveChanged);
    }

    internal sealed class CharacterClothingRuntime
    {
        private readonly IRuntimeDirtySink dirtySink;
        private readonly byte[] observedRawStates =
            new byte[ClothingSlotRegistry.SlotCount];
        private readonly int[] observedItemIds =
            new int[ClothingSlotRegistry.SlotCount];
        private readonly int[] observedDrawOptionSignatures =
            new int[ClothingSlotRegistry.SlotCount];
        private readonly ClothingItemIdentity[] currentIdentities =
            new ClothingItemIdentity[ClothingSlotRegistry.SlotCount];
        private readonly GarmentState[] lastKnownStates =
            new GarmentState[ClothingSlotRegistry.SlotCount];
        private readonly bool[] warnedUnknownStates =
            new bool[ClothingSlotRegistry.SlotCount];
        private int shoesType = int.MinValue;
        private int availabilityMask = -1;
        private int structuralFlags = -1;
        private float nextStateLogTime;

        public CharacterClothingRuntime(IRuntimeDirtySink runtimeDirtySink)
        {
            if (runtimeDirtySink == null)
            {
                throw new ArgumentNullException("runtimeDirtySink");
            }

            dirtySink = runtimeDirtySink;
            Reset();
        }

        public int ShoesType
        {
            get { return shoesType; }
        }

        public int AvailabilityMask
        {
            get { return availabilityMask; }
        }

        public int StructuralFlags
        {
            get { return structuralFlags; }
        }

        public void Reset()
        {
            shoesType = int.MinValue;
            availabilityMask = -1;
            structuralFlags = -1;
            for (int index = 0; index < ClothingSlotRegistry.SlotCount; index++)
            {
                observedRawStates[index] = byte.MaxValue;
                observedItemIds[index] = int.MinValue;
                observedDrawOptionSignatures[index] = int.MinValue;
                currentIdentities[index] = null;
                lastKnownStates[index] = GarmentState.Unknown;
                warnedUnknownStates[index] = false;
            }
        }

        public void Poll(
            ChaControl character,
            NativeMaskLayerStore nativeLayers,
            CharacterExternalMaskSession externalSession)
        {
            if (nativeLayers == null)
            {
                throw new ArgumentNullException("nativeLayers");
            }

            if (externalSession == null)
            {
                throw new ArgumentNullException("externalSession");
            }

            bool runtimeChanged = false;
            int currentShoesType = character == null || character.fileStatus == null
                ? -1
                : character.fileStatus.shoesType;
            if (shoesType != currentShoesType)
            {
                shoesType = currentShoesType;
                dirtySink.RequestRuntimeDirty("active shoe type change", false);
                runtimeChanged = true;
                externalSession.MarkSlotDirty(ClothingSlot.IndoorShoes);
                externalSession.MarkSlotDirty(ClothingSlot.OutdoorShoes);
            }

            int currentAvailabilityMask = 0;
            if (character != null)
            {
                for (int index = 0; index < ClothingSlotRegistry.SlotCount; index++)
                {
                    if (character.IsClothes(index))
                    {
                        currentAvailabilityMask |= 1 << index;
                    }
                }
            }

            if (availabilityMask != currentAvailabilityMask)
            {
                availabilityMask = currentAvailabilityMask;
                dirtySink.RequestRuntimeDirty("clothing object availability change", false);
                runtimeChanged = true;
            }

            int currentStructuralFlags = GetStructuralFlags(character);
            if (structuralFlags != currentStructuralFlags)
            {
                structuralFlags = currentStructuralFlags;
                dirtySink.RequestRuntimeDirty("integrated garment structure change", false);
                runtimeChanged = true;
                externalSession.MarkSlotDirty(ClothingSlot.Bottom);
                externalSession.MarkSlotDirty(ClothingSlot.Bra);
                externalSession.MarkSlotDirty(ClothingSlot.Shorts);
            }

            bool directExternalCompatibility =
                ExternalMaskProvider.DirectCompatibilityActive;
            for (int index = 0; index < ClothingSlotRegistry.SlotCount; index++)
            {
                byte raw = ReadRawState(character, index);
                if (observedRawStates[index] != raw)
                {
                    byte previousRaw = observedRawStates[index];
                    observedRawStates[index] = raw;
                    GarmentState resolved = ClothingStateResolver.Resolve(raw);
                    bool equivalent = AreEffectiveStatePlanesEquivalent(
                        index,
                        previousRaw,
                        raw,
                        nativeLayers,
                        externalSession);
                    if (resolved != GarmentState.Unknown)
                    {
                        lastKnownStates[index] = resolved;
                        warnedUnknownStates[index] = false;
                    }
                    else
                    {
                        ClothingMaskLayerData layer =
                            nativeLayers.Get((ClothingSlot)index).Layer;
                        if (!warnedUnknownStates[index] && layer != null && layer.Enabled)
                        {
                            warnedUnknownStates[index] = true;
                            if (CanWriteStateLog())
                            {
                                BodyMaskLayersPlugin.Log.LogWarning(string.Format(
                                    "Unexpected clothing state {0} for {1}; applying {2}.",
                                    raw,
                                    ClothingSlotRegistry.GetDisplayName((ClothingSlot)index),
                                    BodyMaskLayersPlugin.Settings.UnknownStatePolicy.Value));
                            }
                        }
                    }

                    if (!equivalent)
                    {
                        dirtySink.RequestRuntimeDirty("clothing state change", false);
                    }
                    else
                    {
                        BodyMaskPerformanceMetrics.RecordStateFastPathHit();
                    }

                    runtimeChanged = true;
                }

                int itemId = GetCurrentItemId(character, index);
                if (observedItemIds[index] != itemId)
                {
                    observedItemIds[index] = itemId;
                    currentIdentities[index] = ClothingItemIdentityResolver.Resolve(
                        character,
                        (ClothingSlot)index);
                    externalSession.MarkSlotDirty((ClothingSlot)index);
                    dirtySink.RequestRuntimeDirty("clothing item change", true);
                    runtimeChanged = true;
                }

                if (index > 0 && directExternalCompatibility)
                {
                    int drawOptionSignature =
                        ExternalCharacterStateAdapter.GetDrawOptionSignature(
                            character,
                            (ClothingSlot)index);
                    if (observedDrawOptionSignatures[index] != drawOptionSignature)
                    {
                        observedDrawOptionSignatures[index] = drawOptionSignature;
                        externalSession.MarkSlotDirty((ClothingSlot)index);
                        runtimeChanged = true;
                    }
                }
            }

            if (runtimeChanged && BodyMaskLayersPlugin.Settings.LogStateChanges.Value &&
                CanWriteStateLog())
            {
                BodyMaskLayersPlugin.Log.LogInfo(
                    "BodyMask Layers runtime state changed for " +
                    (character == null ? "<missing>" : character.name) +
                    "; reason=" + dirtySink.LastRuntimeDirtyReason +
                    "; shoesType=" + shoesType +
                    "; available=0x" + availabilityMask.ToString("X3") +
                    "; structure=0x" + structuralFlags.ToString("X1") + ".");
            }
        }

        public bool ShouldDirtyForStateHook(
            ChaControl character,
            int clothingIndex,
            NativeMaskLayerStore nativeLayers,
            CharacterExternalMaskSession externalSession)
        {
            byte current = ReadRawState(character, clothingIndex);
            byte previous = observedRawStates[clothingIndex];
            return previous != current &&
                   !AreEffectiveStatePlanesEquivalent(
                       clothingIndex,
                       previous,
                       current,
                       nativeLayers,
                       externalSession);
        }

        public void InvalidateItem(ClothingSlot slot)
        {
            observedItemIds[(int)slot] = int.MinValue;
        }

        public void SetCurrentIdentity(ClothingSlot slot, ClothingItemIdentity identity)
        {
            int index = (int)slot;
            currentIdentities[index] = identity;
            observedItemIds[index] = identity == null ? int.MinValue : identity.LocalItemId;
        }

        public ClothingItemIdentity GetCurrentIdentity(
            ChaControl character,
            ClothingSlot slot)
        {
            int index = (int)slot;
            if (currentIdentities[index] == null)
            {
                currentIdentities[index] = ClothingItemIdentityResolver.Resolve(character, slot);
                observedItemIds[index] = currentIdentities[index].LocalItemId;
            }

            return currentIdentities[index];
        }

        public byte GetRawState(ChaControl character, int index)
        {
            return ReadRawState(character, index);
        }

        public GarmentState ResolveStateForLayer(
            ChaControl character,
            int index,
            NativeMaskLayerStore nativeLayers)
        {
            GarmentState state = ClothingStateResolver.Resolve(GetRawState(character, index));
            ClothingMaskLayerData layer =
                nativeLayers.Get((ClothingSlot)index).Layer;
            UnknownStatePolicy policy = layer.OptionalStatePolicy ??
                                        BodyMaskLayersPlugin.Settings.UnknownStatePolicy.Value;
            return ClothingStateResolver.ApplyUnknownPolicy(
                state,
                policy,
                lastKnownStates[index]);
        }

        public byte ResolveNativeRawState(
            int index,
            byte raw,
            NativeMaskLayerStore nativeLayers)
        {
            GarmentState state = ClothingStateResolver.Resolve(raw);
            ClothingMaskLayerData layer =
                nativeLayers.Get((ClothingSlot)index).Layer;
            UnknownStatePolicy policy = layer == null ||
                                        !layer.OptionalStatePolicy.HasValue
                ? BodyMaskLayersPlugin.Settings.UnknownStatePolicy.Value
                : layer.OptionalStatePolicy.Value;
            state = ClothingStateResolver.ApplyUnknownPolicy(
                state,
                policy,
                lastKnownStates[index]);
            if (state == GarmentState.Full)
            {
                return 0;
            }

            if (state == GarmentState.Partial)
            {
                return raw == 2 ? (byte)2 : (byte)1;
            }

            return 3;
        }

        public NativeLayerEligibilityContext CreateEligibilityContext(
            int index,
            bool pluginEnabled)
        {
            return new NativeLayerEligibilityContext(
                index,
                pluginEnabled,
                ExternalMaskProvider.IsSourceProviderInstalled,
                shoesType,
                availabilityMask,
                structuralFlags,
                null);
        }

        public bool IsStructurallySuppressed(int index)
        {
            return LayerEligibilityEvaluator.IsStructurallySuppressed(
                index,
                structuralFlags);
        }

        private bool AreEffectiveStatePlanesEquivalent(
            int index,
            byte previousRaw,
            byte currentRaw,
            NativeMaskLayerStore nativeLayers,
            CharacterExternalMaskSession externalSession)
        {
            if (previousRaw == currentRaw)
            {
                return true;
            }

            if (previousRaw == byte.MaxValue)
            {
                return false;
            }

            NativeMaskLayerSlotState native =
                nativeLayers.Get((ClothingSlot)index);
            if (native.SemanticMask != null && native.Layer != null && native.Layer.Enabled)
            {
                byte previousEffective = ResolveNativeRawState(index, previousRaw, nativeLayers);
                byte currentEffective = ResolveNativeRawState(index, currentRaw, nativeLayers);
                if (!native.SemanticMask.AreStatePlanesEquivalent(
                        previousEffective,
                        currentEffective))
                {
                    return false;
                }
            }

            ExternalResolvedMask external =
                externalSession.GetResolution((ClothingSlot)index);
            return external == null || external.SemanticMask == null ||
                   external.SemanticMask.AreStatePlanesEquivalent(previousRaw, currentRaw);
        }

        private bool CanWriteStateLog()
        {
            float now = Time.unscaledTime;
            if (now < nextStateLogTime)
            {
                return false;
            }

            nextStateLogTime = now +
                               BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
            return true;
        }

        private static int GetStructuralFlags(ChaControl character)
        {
            if (character == null)
            {
                return 0;
            }

            int flags = 0;
            if (character.notBot)
            {
                flags |= 1;
            }

            if (character.notBra)
            {
                flags |= 2;
            }

            if (character.notShorts)
            {
                flags |= 4;
            }

            return flags;
        }

        private static int GetCurrentItemId(ChaControl character, int index)
        {
            if (character == null || character.nowCoordinate == null ||
                character.nowCoordinate.clothes == null ||
                character.nowCoordinate.clothes.parts == null ||
                index >= character.nowCoordinate.clothes.parts.Length)
            {
                return 0;
            }

            return character.nowCoordinate.clothes.parts[index].id;
        }

        private static byte ReadRawState(ChaControl character, int index)
        {
            if (character == null || character.fileStatus == null ||
                character.fileStatus.clothesState == null ||
                index >= character.fileStatus.clothesState.Length)
            {
                return byte.MaxValue;
            }

            return character.fileStatus.clothesState[index];
        }
    }
}
