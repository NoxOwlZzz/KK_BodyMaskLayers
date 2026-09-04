using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal interface ICharacterMaskMutationSink
    {
        void RequestMaskDirty(string reason, bool baseContentMayHaveChanged);

        void PersistMaskData();

        void RequestImmediateRuntimeRefresh();
    }

    internal sealed class CharacterNativeMaskService
    {
        private readonly NativeMaskLayerStore store;
        private readonly NativeMaskDecodePipeline decodePipeline;
        private readonly ExternalPortableLayerConverter externalConverter;
        private readonly CharacterClothingRuntime runtimeState;
        private readonly CharacterExternalMaskSession externalSession;
        private readonly ICharacterMaskMutationSink mutationSink;

        public CharacterNativeMaskService(
            NativeMaskLayerStore layerStore,
            NativeMaskDecodePipeline nativeDecodePipeline,
            ExternalPortableLayerConverter portableExternalConverter,
            CharacterClothingRuntime clothingRuntime,
            CharacterExternalMaskSession externalMaskSession,
            ICharacterMaskMutationSink sink)
        {
            if (layerStore == null)
            {
                throw new ArgumentNullException("layerStore");
            }

            if (nativeDecodePipeline == null)
            {
                throw new ArgumentNullException("nativeDecodePipeline");
            }

            if (portableExternalConverter == null)
            {
                throw new ArgumentNullException("portableExternalConverter");
            }

            if (clothingRuntime == null)
            {
                throw new ArgumentNullException("clothingRuntime");
            }

            if (externalMaskSession == null)
            {
                throw new ArgumentNullException("externalMaskSession");
            }

            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            store = layerStore;
            decodePipeline = nativeDecodePipeline;
            externalConverter = portableExternalConverter;
            runtimeState = clothingRuntime;
            externalSession = externalMaskSession;
            mutationSink = sink;
        }

        public bool TryImportPng(
            ChaControl character,
            ClothingSlot slot,
            byte[] pngBytes,
            out string result)
        {
            result = null;
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                result = "Invalid clothing slot.";
                return false;
            }

            ClothingItemIdentity identity = ClothingItemIdentityResolver.Resolve(character, slot);
            if (!identity.HasItem)
            {
                result = "The selected slot has no bindable clothing item.";
                return false;
            }

            if (pngBytes == null)
            {
                result = "Layer or PNG bytes are missing.";
                return false;
            }

            GradientHandlingMode gradientHandlingMode =
                BodyMaskLayersPlugin.Settings.GradientHandling.Value;
            NativeMaskDecodeResult decode = decodePipeline.Decode(
                new NativeMaskDecodeRequest
                {
                    PngBytes = pngBytes,
                    ColorFormatVersion = 1,
                    SourceContract = MaskSourceContract.Native,
                    GradientHandlingMode = gradientHandlingMode
                });
            if (!decode.Success)
            {
                MaskPngDecodeService.WarnAboutBluePixels(
                    slot,
                    decode.Statistics,
                    false);
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Rejected " + ClothingSlotRegistry.GetDisplayName(slot) +
                    " mask import: " + decode.Error);
                result = decode.Error;
                return false;
            }

            MaskPngDecodeService.WarnAboutBluePixels(slot, decode.Statistics, true);
            ClothingMaskLayerData layer = new ClothingMaskLayerData
            {
                Slot = slot,
                Enabled = true,
                OriginalPngBytes = (byte[])pngBytes.Clone(),
                Width = decode.Validation.Width,
                Height = decode.Validation.Height,
                Hash = decode.Hash,
                BoundItemIdentity = identity,
                ColorFormatVersion = 1,
                CreatedWithPluginVersion = BodyMaskLayersPlugin.PluginVersion,
                LastValidationResult = decode.BuildValidationDescription(),
                SourceContract = MaskSourceContract.Native,
                GradientHandlingMode = gradientHandlingMode
            };
            store.ReplaceOwned(slot, layer);
            store.SetDecoded(slot, decode.SemanticMask, decode.Statistics);
            runtimeState.SetCurrentIdentity(slot, identity);
            externalSession.MarkSlotDirty(slot);
            CommitPersistentMutation(
                "PNG imported for " + ClothingSlotRegistry.GetDisplayName(slot));
            result = decode.ReusedDecode
                ? string.Format(
                    "Loaded {0}x{1} mask using cached decode.",
                    decode.Validation.Width,
                    decode.Validation.Height)
                : decode.NormalizedPixelCount == 0
                    ? string.Format(
                        "Loaded {0}x{1} mask.",
                        decode.Validation.Width,
                        decode.Validation.Height)
                    : string.Format(
                        "Loaded {0}x{1} mask; normalized {2} palette/edge pixels.",
                        decode.Validation.Width,
                        decode.Validation.Height,
                        decode.NormalizedPixelCount);
            BodyMaskLayersPlugin.Log.LogInfo(
                "Loaded " + ClothingSlotRegistry.GetDisplayName(slot) + " mask: " +
                result + " " + decode.Statistics + "; SHA-256 " +
                layer.Hash.Substring(0, 12) + "...");
            return true;
        }

        public bool ClearLayer(ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot) || store.Get(slot).Layer == null)
            {
                return false;
            }

            ClothingMaskLayerData removed = store.Clear(slot);
            externalSession.RegisterPortableLayerClear(slot, removed);
            externalSession.MarkSlotDirty(slot);
            mutationSink.RequestImmediateRuntimeRefresh();
            CommitPersistentMutation(
                "mask cleared for " + ClothingSlotRegistry.GetDisplayName(slot));
            return true;
        }

        public bool SetLayerEnabled(ClothingSlot slot, bool enabled)
        {
            if (!ClothingSlotRegistry.IsValid(slot) || store.Get(slot).Layer == null)
            {
                return false;
            }

            store.SetEnabled(slot, enabled);
            CommitPersistentMutation(
                "layer enabled state changed for " +
                ClothingSlotRegistry.GetDisplayName(slot));
            return true;
        }

        public bool BindLayerToCurrentItem(
            ChaControl character,
            ClothingSlot slot,
            out string result)
        {
            result = null;
            if (!ClothingSlotRegistry.IsValid(slot) || store.Get(slot).Layer == null)
            {
                result = "No mask is loaded in this slot.";
                return false;
            }

            ClothingItemIdentity identity = ClothingItemIdentityResolver.Resolve(character, slot);
            if (!identity.HasItem)
            {
                result = "The selected slot has no bindable clothing item.";
                return false;
            }

            store.SetBoundItemIdentity(slot, identity);
            runtimeState.SetCurrentIdentity(slot, identity);
            CommitPersistentMutation(
                "item binding changed for " + ClothingSlotRegistry.GetDisplayName(slot));
            result = "Bound to " + identity;
            return true;
        }

        public byte[] ExportOriginalPng(ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return null;
            }

            ClothingMaskLayerData layer = store.Get(slot).Layer;
            return layer == null || layer.OriginalPngBytes == null
                ? null
                : (byte[])layer.OriginalPngBytes.Clone();
        }

        public bool IsLayerEnabled(ClothingSlot slot)
        {
            return ClothingSlotRegistry.IsValid(slot) &&
                   store.Get(slot).Layer != null &&
                   store.Get(slot).Layer.Enabled;
        }

        public bool HasLayer(ClothingSlot slot)
        {
            return ClothingSlotRegistry.IsValid(slot) && store.Get(slot).Layer != null;
        }

        public bool IsLayerUsable(ClothingSlot slot)
        {
            return ClothingSlotRegistry.IsValid(slot) &&
                   store.Get(slot).Layer != null &&
                   store.Get(slot).SemanticMask != null;
        }

        public bool HasBindableItem(ChaControl character, ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return false;
            }

            ClothingItemIdentity identity =
                runtimeState.GetCurrentIdentity(character, slot);
            return identity != null && identity.HasItem;
        }

        public string GetLayerHash(ClothingSlot slot)
        {
            return ClothingSlotRegistry.IsValid(slot) && store.Get(slot).Layer != null
                ? store.Get(slot).Layer.Hash
                : null;
        }

        public string GetLayerPreviewKey(ClothingSlot slot)
        {
            return ClothingSlotRegistry.IsValid(slot)
                ? store.GetPreviewKey(slot)
                : null;
        }

        public bool TryBuildLayerPreview(
            ClothingSlot slot,
            int maximumDimension,
            out Rgba32[] pixels,
            out int width,
            out int height)
        {
            pixels = null;
            width = 0;
            height = 0;
            if (!ClothingSlotRegistry.IsValid(slot) || store.Get(slot).SemanticMask == null)
            {
                return false;
            }

            pixels = MaskPreviewBuilder.Build(
                store.Get(slot).SemanticMask,
                maximumDimension,
                out width,
                out height);
            BodyMaskPerformanceMetrics.RecordPreviewCreation();
            return true;
        }

        public Dictionary<ClothingSlot, ClothingMaskLayerData> SnapshotLayers()
        {
            return store.Snapshot();
        }

        public void ApplyPartialCoordinateLayers(
            Dictionary<ClothingSlot, ClothingMaskLayerData> source,
            bool[] selectedSlots)
        {
            if (selectedSlots == null || selectedSlots.Length < ClothingSlotRegistry.SlotCount)
            {
                throw new ArgumentException(
                    "Nine slot selection flags are required.",
                    "selectedSlots");
            }

            for (int index = 0; index < ClothingSlotRegistry.SlotCount; index++)
            {
                if (!selectedSlots[index])
                {
                    continue;
                }

                ClothingSlot slot = (ClothingSlot)index;
                externalSession.ResetConversionTracking(slot);
                ClothingMaskLayerData layer;
                if (source != null && source.TryGetValue(slot, out layer))
                {
                    store.ReplaceLoadedData(slot, layer);
                    DecodePersistedLayer(index);
                }
                else
                {
                    NativeMaskLayerSlotState native = store.Get(slot);
                    if (native.Layer != null)
                    {
                        store.Clear(slot);
                    }
                    else
                    {
                        store.SetDecoded(slot, null, null);
                    }
                }
            }

            CommitPersistentMutation("partial coordinate slot merge");
        }

        public void MarkDecodeConfigurationDirty()
        {
            store.MarkDecodeConfigurationDirty();
        }

        public void ApplyPendingDecodeConfiguration()
        {
            if (!store.ResetDecodeGeneration())
            {
                return;
            }

            for (int index = 0; index < store.SlotCount; index++)
            {
                if (store.Get((ClothingSlot)index).Layer != null)
                {
                    DecodePersistedLayer(index);
                }
            }

            mutationSink.RequestMaskDirty("mask decoding configuration change", false);
        }

        public void LoadPluginData(ExtensibleSaveFormat.PluginData data, string source)
        {
            externalSession.ResetConversionTracking();
            Dictionary<ClothingSlot, ClothingMaskLayerData> loaded;
            string error;
            if (!CoordinateDataHandler.TryReadPluginData(data, out loaded, out error))
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Could not load BodyMask Layers from " + source + ": " + error);
                loaded = new Dictionary<ClothingSlot, ClothingMaskLayerData>();
            }

            for (int index = 0; index < store.SlotCount; index++)
            {
                store.SetDecoded((ClothingSlot)index, null, null);
            }

            for (int index = 0; index < store.SlotCount; index++)
            {
                ClothingSlot slot = (ClothingSlot)index;
                ClothingMaskLayerData layer;
                store.ReplaceLoadedData(
                    slot,
                    loaded.TryGetValue(slot, out layer) ? layer : null);
                if (store.Get(slot).Layer != null)
                {
                    DecodePersistedLayer(index);
                }
            }

            mutationSink.RequestMaskDirty("plugin data loaded from " + source, false);
        }

        public bool TryConvertExternal(
            ChaControl character,
            int index,
            ExternalResolvedMask external,
            out string result)
        {
            ClothingSlot slot = (ClothingSlot)index;
            ClothingItemIdentity identity =
                runtimeState.GetCurrentIdentity(character, slot);
            PortableExternalLayer converted;
            if (!externalConverter.TryConvert(
                    index,
                    external,
                    runtimeState.IsStructurallySuppressed(index),
                    store.Get(slot).Layer,
                    identity,
                    out converted,
                    out result))
            {
                return false;
            }

            store.ReplaceOwned(slot, converted.Layer);
            store.SetDecoded(slot, converted.SemanticMask, converted.Statistics);
            runtimeState.SetCurrentIdentity(slot, converted.Layer.BoundItemIdentity);
            return true;
        }

        public IEnumerable<ClothingMaskLayerData> EnumerateOwnedLayers()
        {
            return store.EnumerateOwnedLayers();
        }

        private void CommitPersistentMutation(string reason)
        {
            mutationSink.RequestMaskDirty(reason, false);
            mutationSink.PersistMaskData();
        }

        private void DecodePersistedLayer(int index)
        {
            ClothingSlot slot = (ClothingSlot)index;
            ClothingMaskLayerData layer = store.Get(slot).Layer;
            store.SetDecoded(slot, null, null);
            NativeMaskDecodeResult decode = decodePipeline.Decode(
                new NativeMaskDecodeRequest
                {
                    PngBytes = layer == null ? null : layer.OriginalPngBytes,
                    ExpectedHash = layer == null ? null : layer.Hash,
                    ColorFormatVersion = layer == null ? 1 : layer.ColorFormatVersion,
                    AllowReuseWhileConfigurationDirty = true,
                    SourceContract = layer == null
                        ? MaskSourceContract.Native
                        : layer.SourceContract,
                    GradientHandlingMode = layer == null
                        ? GradientHandlingMode.StrictCategorical
                        : layer.GradientHandlingMode
                });
            if (decode.Success)
            {
                MaskPngDecodeService.WarnAboutBluePixels(
                    slot,
                    decode.Statistics,
                    true);
                store.UpdateValidationMetadata(
                    slot,
                    decode.Validation.Width,
                    decode.Validation.Height,
                    decode.Hash,
                    decode.BuildValidationDescription());
                store.SetDecoded(slot, decode.SemanticMask, decode.Statistics);
                return;
            }

            if (layer != null)
            {
                MaskPngDecodeService.WarnAboutBluePixels(
                    slot,
                    decode.Statistics,
                    false);
                store.UpdateValidationMetadata(
                    slot,
                    layer.Width,
                    layer.Height,
                    layer.Hash,
                    decode.BuildValidationDescription());
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Stored " + ClothingSlotRegistry.GetDisplayName(slot) +
                    " mask is inactive: " + decode.Error);
            }
        }
    }
}
