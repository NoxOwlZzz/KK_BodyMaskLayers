using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class CharacterExternalMaskCoordinator
    {
        private readonly CharacterExternalMaskSession session;
        private readonly NativeMaskLayerStore nativeLayers;
        private readonly CharacterNativeMaskService nativeMasks;
        private readonly CharacterClothingRuntime runtimeState;
        private readonly ICharacterMaskMutationSink mutationSink;

        public CharacterExternalMaskCoordinator(
            CharacterExternalMaskSession externalSession,
            NativeMaskLayerStore layerStore,
            CharacterNativeMaskService nativeMaskService,
            CharacterClothingRuntime clothingRuntime,
            ICharacterMaskMutationSink sink)
        {
            if (externalSession == null)
            {
                throw new ArgumentNullException("externalSession");
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

            session = externalSession;
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

            ExternalResolvedMask external = session.GetResolution(slot);
            return external != null && external.SemanticMask != null;
        }

        public string DescribeDetails(ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                throw new IndexOutOfRangeException();
            }

            ExternalResolvedMask external = session.GetResolution(slot);
            if (external == null)
            {
                return session.GetStatus(slot) ?? "No resolved external source.";
            }

            ExternalMaskDescriptor descriptor = external.Descriptor;
            return string.Format(
                "provider={0}; contract={1}; modGuid={2}; slot={3}; category={4}; originalId={5}; " +
                "asset={6}; fingerprint={7}; gradientMode=PreserveContinuous; kind={8}; stats=[{9}]; " +
                "cacheGeneration={10}; resolutionStatus={11}",
                ExternalMaskDescriptor.ProviderIdValue,
                ExternalMaskDescriptor.ContractVersionValue,
                descriptor.ModGuid ?? "<none>",
                slot,
                descriptor.Category,
                descriptor.OriginalItemId,
                ExternalPortableLayerConverter.GetAssetDescription(descriptor),
                external.Fingerprint,
                external.IsContinuous ? "Continuous" : "Binary",
                external.Statistics,
                external.CatalogGeneration,
                session.GetStatus(slot) ?? "resolved");
        }

        public void Refresh(ChaControl character)
        {
            FlushPendingConvertedData();

            if (!session.HasDirtySlots)
            {
                return;
            }

            if (!ExternalMaskProvider.DirectCompatibilityActive)
            {
                bool removed = session.ClearProviderResolutions(
                    ExternalMaskProvider.IsSourceProviderInstalled
                        ? "A separate body-mask provider is active."
                        : "Compatible source loading is unavailable.");
                if (removed)
                {
                    mutationSink.RequestMaskDirty(
                        "external provider ownership change",
                        false);
                }

                return;
            }

            ExternalDirtySlotSet pending = session.ConsumeDirtySlots();
            try
            {
                RefreshPending(character, pending);
            }
            catch
            {
                session.DeferSlots(pending);
                throw;
            }
        }

        private void RefreshPending(
            ChaControl character,
            ExternalDirtySlotSet pending)
        {
            session.BeginAutoConversionRefresh();
            for (int index = 1; index < ClothingSlotRegistry.SlotCount; index++)
            {
                ClothingSlot slot = (ClothingSlot)index;
                if (!pending.Contains(slot))
                {
                    continue;
                }

                ExternalResolvedMask previous = session.GetResolution(slot);
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
                            "external shoe source became inactive",
                            false);
                    }

                    continue;
                }

                ExternalResolvedMask resolved;
                string status;
                bool found = ExternalMaskProvider.TryResolveForCharacter(
                    character,
                    slot,
                    out resolved,
                    out status);
                if (session.SetResolution(slot, found ? resolved : null, status))
                {
                    mutationSink.RequestMaskDirty(
                        "external source resolution change",
                        false);
                }

                if (found)
                {
                    ProcessAutomaticConversion(character, index, resolved);
                }
            }
        }

        private void FlushPendingConvertedData()
        {
            if (!session.ConvertedDataPersistencePending)
            {
                return;
            }

            mutationSink.RequestMaskDirty(
                "external sources automatically converted",
                false);
            mutationSink.PersistMaskData();
            session.CompleteConvertedDataPersistence();
        }

        private void ProcessAutomaticConversion(
            ChaControl character,
            int index,
            ExternalResolvedMask external)
        {
            if (external == null || external.SemanticMask == null ||
                nativeLayers.Get((ClothingSlot)index).Layer != null)
            {
                return;
            }

            ClothingSlot slot = (ClothingSlot)index;
            if (session.IsFingerprintSuppressed(slot, external.Fingerprint))
            {
                return;
            }

            string attemptKey = BuildAutoConversionAttemptKey(character, index, external);
            if (session.TryBeginAutoConversionAttempt(slot, attemptKey) !=
                ExternalAutoConversionAttemptDecision.Granted)
            {
                return;
            }

            string conversionResult;
            bool converted = nativeMasks.TryConvertExternal(
                character,
                index,
                external,
                out conversionResult);
            if (converted)
            {
                session.MarkConvertedDataPersistencePending();
                FlushPendingConvertedData();
            }

            BodyMaskLayersPlugin.LogDebug(
                (converted ? "Automatically converted " :
                    "Automatic conversion skipped for ") +
                ClothingSlotRegistry.GetDisplayName(slot) + ": " +
                conversionResult);
        }

        private string BuildAutoConversionAttemptKey(
            ChaControl character,
            int index,
            ExternalResolvedMask external)
        {
            ClothingItemIdentity identity = runtimeState.GetCurrentIdentity(
                character,
                (ClothingSlot)index);
            return string.Format(
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                external == null ? string.Empty : external.Fingerprint ?? string.Empty,
                identity == null ? 0 : identity.Category,
                identity == null ? 0 : identity.LocalItemId,
                identity == null ? 0 : identity.OriginalItemId,
                identity == null ? string.Empty : identity.SideloaderGuid ?? string.Empty,
                runtimeState.StructuralFlags,
                external == null || external.SemanticMask == null ? 0 :
                    external.SemanticMask.Width,
                external == null || external.SemanticMask == null ? 0 :
                    external.SemanticMask.Height);
        }
    }
}
