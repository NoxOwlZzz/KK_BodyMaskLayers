using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class NativeMaskLayerSlotState
    {
        internal NativeMaskLayerSlotState()
        {
        }

        public ClothingMaskLayerData Layer { get; internal set; }

        public SemanticMask SemanticMask { get; internal set; }

        public MaskColorStatistics Statistics { get; internal set; }

        public int SemanticRevision { get; internal set; }

        public int DecodedConfigurationRevision { get; internal set; }
    }

    internal sealed class NativeMaskLayerStore
    {
        private const int StoreSlotCount = (int)ClothingSlot.OutdoorShoes + 1;
        private readonly NativeMaskLayerSlotState[] slots;
        private bool decodeConfigurationDirty;
        private int decodeConfigurationRevision;

        public NativeMaskLayerStore()
        {
            slots = new NativeMaskLayerSlotState[StoreSlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = new NativeMaskLayerSlotState();
            }
        }

        public int SlotCount
        {
            get { return slots.Length; }
        }

        public bool DecodeConfigurationDirty
        {
            get { return decodeConfigurationDirty; }
        }

        public int DecodeConfigurationRevision
        {
            get { return decodeConfigurationRevision; }
        }

        public NativeMaskLayerSlotState Get(ClothingSlot slot)
        {
            return slots[GetIndex(slot)];
        }

        public void ReplaceOwned(ClothingSlot slot, ClothingMaskLayerData layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException("layer");
            }

            if (layer.Slot != slot)
            {
                throw new ArgumentException("The layer slot does not match the destination slot.", "layer");
            }

            slots[GetIndex(slot)].Layer = layer;
        }

        public void ReplaceClone(ClothingSlot slot, ClothingMaskLayerData layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException("layer");
            }

            ReplaceOwned(slot, layer.DeepClone());
        }

        public void ReplaceLoadedData(
            ClothingSlot slot,
            ClothingMaskLayerData layer)
        {
            NativeMaskLayerSlotState state = slots[GetIndex(slot)];
            state.Layer = layer == null ? null : layer.DeepClone();
        }

        public ClothingMaskLayerData Clear(ClothingSlot slot)
        {
            NativeMaskLayerSlotState state = slots[GetIndex(slot)];
            ClothingMaskLayerData removed = state.Layer;
            if (removed == null)
            {
                return null;
            }

            state.Layer = null;
            SetDecoded(slot, null, null);
            return removed;
        }

        public bool SetEnabled(ClothingSlot slot, bool enabled)
        {
            ClothingMaskLayerData layer = slots[GetIndex(slot)].Layer;
            if (layer == null)
            {
                return false;
            }

            layer.Enabled = enabled;
            return true;
        }

        public bool SetBoundItemIdentity(
            ClothingSlot slot,
            ClothingItemIdentity identity)
        {
            ClothingMaskLayerData layer = slots[GetIndex(slot)].Layer;
            if (layer == null)
            {
                return false;
            }

            layer.BoundItemIdentity = identity;
            return true;
        }

        public void UpdateValidationMetadata(
            ClothingSlot slot,
            int width,
            int height,
            string hash,
            string validationResult)
        {
            ClothingMaskLayerData layer = slots[GetIndex(slot)].Layer;
            if (layer == null)
            {
                throw new InvalidOperationException(
                    "Cannot update validation metadata for an empty slot.");
            }

            layer.Width = width;
            layer.Height = height;
            layer.Hash = hash;
            layer.LastValidationResult = validationResult;
        }

        public void SetDecoded(
            ClothingSlot slot,
            SemanticMask semanticMask,
            MaskColorStatistics statistics)
        {
            NativeMaskLayerSlotState state = slots[GetIndex(slot)];
            state.SemanticMask = semanticMask;
            state.Statistics = statistics;
            state.DecodedConfigurationRevision = decodeConfigurationRevision;
            unchecked
            {
                state.SemanticRevision++;
            }
        }

        public bool TryReuseDecoded(
            string hash,
            MaskSourceContract sourceContract,
            GradientHandlingMode gradientHandlingMode,
            out SemanticMask semanticMask,
            out MaskColorStatistics statistics)
        {
            semanticMask = null;
            statistics = null;
            if (string.IsNullOrEmpty(hash))
            {
                return false;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                NativeMaskLayerSlotState state = slots[index];
                ClothingMaskLayerData layer = state.Layer;
                if (layer == null || state.SemanticMask == null || state.Statistics == null ||
                    state.DecodedConfigurationRevision != decodeConfigurationRevision ||
                    layer.SourceContract != sourceContract ||
                    layer.GradientHandlingMode != gradientHandlingMode ||
                    !string.Equals(layer.Hash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                semanticMask = state.SemanticMask;
                statistics = state.Statistics.Clone();
                return true;
            }

            return false;
        }

        public string GetPreviewKey(ClothingSlot slot)
        {
            NativeMaskLayerSlotState state = slots[GetIndex(slot)];
            if (state.Layer == null)
            {
                return null;
            }

            return (state.Layer.Hash ?? "no-hash") + ":" + state.SemanticRevision;
        }

        public Dictionary<ClothingSlot, ClothingMaskLayerData> Snapshot()
        {
            Dictionary<ClothingSlot, ClothingMaskLayerData> snapshot =
                new Dictionary<ClothingSlot, ClothingMaskLayerData>();
            for (int index = 0; index < slots.Length; index++)
            {
                ClothingMaskLayerData layer = slots[index].Layer;
                if (layer != null)
                {
                    snapshot[(ClothingSlot)index] = layer.DeepClone();
                }
            }

            return snapshot;
        }

        public IEnumerable<ClothingMaskLayerData> EnumerateOwnedLayers()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                ClothingMaskLayerData layer = slots[index].Layer;
                if (layer != null)
                {
                    yield return layer;
                }
            }
        }

        public void MarkDecodeConfigurationDirty()
        {
            decodeConfigurationDirty = true;
        }

        public bool ResetDecodeGeneration()
        {
            if (!decodeConfigurationDirty)
            {
                return false;
            }

            decodeConfigurationDirty = false;
            unchecked
            {
                decodeConfigurationRevision++;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                SetDecoded((ClothingSlot)index, null, null);
            }

            return true;
        }

        private static int GetIndex(ClothingSlot slot)
        {
            int index = (int)slot;
            if (index < 0 || index >= StoreSlotCount)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            return index;
        }
    }
}
