using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class CharacterLegacyMaskCoordinator
    {
        private readonly CharacterLegacyMaskSession session;
        private readonly NativeMaskLayerStore nativeLayers;
        private readonly CharacterNativeMaskService nativeMasks;
        private readonly CharacterClothingRuntime runtimeState;
        private readonly ICharacterMaskMutationSink mutationSink;

        public CharacterLegacyMaskCoordinator(
            CharacterLegacyMaskSession legacySession,
            NativeMaskLayerStore layerStore,
            CharacterNativeMaskService nativeMaskService,
            CharacterClothingRuntime clothingRuntime,
            ICharacterMaskMutationSink sink)
        {
            if (legacySession == null)
            {
                throw new ArgumentNullException("legacySession");
            }

            if (layerStore == null)
            {
                throw new ArgumentNullException("layerStore");
            }

            if (nativeMaskService == null)
            {
                throw new ArgumentNullException("nativeMaskService");
            }

            if (clothingRuntime == null)
            {
                throw new ArgumentNullException("clothingRuntime");
            }

            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            session = legacySession;
            nativeLayers = layerStore;
            nativeMasks = nativeMaskService;
            runtimeState = clothingRuntime;
            mutationSink = sink;
        }

        public bool HasSource(ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return false;
            }

            LegacyResolvedMask legacy = session.GetResolution(slot);
            return legacy != null && legacy.SemanticMask != null;
        }

        public string DescribeDetails(ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                throw new IndexOutOfRangeException();
            }

            LegacyResolvedMask legacy = session.GetResolution(slot);
            if (legacy == null)
            {
                return session.GetStatus(slot) ?? "No resolved legacy source.";
            }

            LegacyMaskDescriptor descriptor = legacy.Descriptor;
            return string.Format(
                "provider={0}; contract={1}; modGuid={2}; slot={3}; category={4}; originalId={5}; " +
                "asset={6}; fingerprint={7}; gradientMode=PreserveContinuous; kind={8}; stats=[{9}]; " +
                "cacheGeneration={10}; resolutionStatus={11}",
                LegacyMaskDescriptor.ProviderIdValue,
                LegacyMaskDescriptor.ContractVersionValue,
                descriptor.ModGuid ?? "<none>",
                slot,
                descriptor.Category,
                descriptor.OriginalItemId,
                LegacyPortableLayerConverter.GetAssetDescription(descriptor),
                legacy.Fingerprint,
                legacy.IsContinuous ? "Continuous" : "Binary",
                legacy.Statistics,
                legacy.CatalogGeneration,
                session.GetStatus(slot) ?? "resolved");
        }

        public void Refresh(ChaControl character)
        {
            if (!session.HasDirtySlots)
            {
                return;
            }

            if (!NakayChaAlphaMaskProvider.DirectCompatibilityActive)
            {
                bool removed = session.ClearProviderResolutions(
                    NakayChaAlphaMaskProvider.IsLegacyPluginInstalled
                        ? "Upstream KK_ChaAlphaMask bridge active."
                        : "Legacy compatibility disabled.");
                if (removed)
                {
                    mutationSink.RequestMaskDirty(
                        "legacy provider ownership change",
                        false);
                }

                return;
            }

            LegacyDirtySlotSet pending = session.ConsumeDirtySlots();
            session.BeginAutoConversionRefresh();
            bool convertedAny = false;
            for (int index = 1; index < ClothingSlotRegistry.SlotCount; index++)
            {
                ClothingSlot slot = (ClothingSlot)index;
                if (!pending.Contains(slot))
                {
                    continue;
                }

                LegacyResolvedMask previous = session.GetResolution(slot);
                if (!LayerEligibilityEvaluator.IsSelectedShoe(
                        index,
                        runtimeState.ShoesType))
                {
                    session.SetResolution(
                        slot,
                        null,
                        "The other shoe type is selected; source loading is deferred.");
                    if (previous != null)
                    {
                        mutationSink.RequestMaskDirty(
                            "legacy shoe source became inactive",
                            false);
                    }

                    continue;
                }

                LegacyResolvedMask resolved;
                string status;
                bool found = NakayChaAlphaMaskProvider.TryResolveForCharacter(
                    character,
                    slot,
                    out resolved,
                    out status);
                if (session.SetResolution(slot, found ? resolved : null, status))
                {
                    mutationSink.RequestMaskDirty(
                        "legacy source resolution change",
                        false);
                }

                if (found)
                {
                    convertedAny |= TryAutomaticallyConvert(character, index, resolved);
                }
            }

            if (convertedAny)
            {
                mutationSink.RequestMaskDirty(
                    "legacy sources automatically converted",
                    false);
                mutationSink.PersistMaskData();
            }
        }

        private bool TryAutomaticallyConvert(
            ChaControl character,
            int index,
            LegacyResolvedMask legacy)
        {
            if (!BodyMaskLayersPlugin.Settings.AutoConvertNakayLegacyMasks.Value ||
                legacy == null || legacy.SemanticMask == null ||
                nativeLayers.Get((ClothingSlot)index).Layer != null)
            {
                return false;
            }

            ClothingSlot slot = (ClothingSlot)index;
            if (session.IsFingerprintSuppressed(slot, legacy.Fingerprint))
            {
                return false;
            }

            string attemptKey = BuildAutoConversionAttemptKey(character, index, legacy);
            if (session.TryBeginAutoConversionAttempt(slot, attemptKey) !=
                LegacyAutoConversionAttemptDecision.Granted)
            {
                return false;
            }

            string conversionResult;
            bool converted = nativeMasks.TryConvertLegacy(
                character,
                index,
                legacy,
                out conversionResult);
            BodyMaskLayersPlugin.LogDebug(
                (converted ? "Automatically converted " :
                    "Automatic conversion skipped for ") +
                ClothingSlotRegistry.GetDisplayName(slot) + ": " +
                conversionResult);
            return converted;
        }

        private string BuildAutoConversionAttemptKey(
            ChaControl character,
            int index,
            LegacyResolvedMask legacy)
        {
            ClothingItemIdentity identity = runtimeState.GetCurrentIdentity(
                character,
                (ClothingSlot)index);
            return string.Format(
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                legacy == null ? string.Empty : legacy.Fingerprint ?? string.Empty,
                identity == null ? 0 : identity.Category,
                identity == null ? 0 : identity.LocalItemId,
                identity == null ? 0 : identity.OriginalItemId,
                identity == null ? string.Empty : identity.SideloaderGuid ?? string.Empty,
                runtimeState.StructuralFlags,
                legacy == null || legacy.SemanticMask == null ? 0 :
                    legacy.SemanticMask.Width,
                legacy == null || legacy.SemanticMask == null ? 0 :
                    legacy.SemanticMask.Height);
        }
    }
}
