using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal enum LegacyAutoConversionAttemptDecision
    {
        Granted = 0,
        AlreadyAttempted = 1,
        DeferredUntilNextRefresh = 2
    }

    internal struct LegacyDirtySlotSet
    {
        private readonly int value;

        internal LegacyDirtySlotSet(int value)
        {
            this.value = value;
        }

        public bool IsEmpty
        {
            get { return value == 0; }
        }

        public int Count
        {
            get
            {
                int remaining = value;
                int count = 0;
                while (remaining != 0)
                {
                    count += remaining & 1;
                    remaining >>= 1;
                }

                return count;
            }
        }

        public bool Contains(ClothingSlot slot)
        {
            int index = ValidateSlot(slot);
            return index != (int)ClothingSlot.Top &&
                   (value & (1 << index)) != 0;
        }

        internal int Value
        {
            get { return value; }
        }

        private static int ValidateSlot(ClothingSlot slot)
        {
            int index = (int)slot;
            if (index < (int)ClothingSlot.Top ||
                index > (int)ClothingSlot.OutdoorShoes)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            return index;
        }
    }

    internal sealed class CharacterLegacyMaskSession
    {
        private const int SlotCount = (int)ClothingSlot.OutdoorShoes + 1;
        private const int FirstLegacySlot = (int)ClothingSlot.Bottom;
        private const int AllLegacySlots = 0x1fe;

        private readonly LegacyResolvedMask[] resolutions =
            new LegacyResolvedMask[SlotCount];
        private readonly string[] statuses = new string[SlotCount];
        private readonly string[] autoConversionAttemptKeys = new string[SlotCount];
        private readonly string[] clearedAutomaticFingerprints = new string[SlotCount];
        private int dirtySlots = AllLegacySlots;
        private bool autoConversionAttemptGrantedThisRefresh;

        public bool HasDirtySlots
        {
            get { return dirtySlots != 0; }
        }

        public void MarkAllDirty()
        {
            dirtySlots = AllLegacySlots;
        }

        public void MarkSlotDirty(ClothingSlot slot)
        {
            int index = ValidateSlot(slot);
            if (index >= FirstLegacySlot)
            {
                dirtySlots |= 1 << index;
            }
        }

        public LegacyDirtySlotSet ConsumeDirtySlots()
        {
            LegacyDirtySlotSet result = new LegacyDirtySlotSet(dirtySlots);
            dirtySlots = 0;
            return result;
        }

        public void DeferSlot(ClothingSlot slot)
        {
            int index = ValidateLegacySlot(slot);
            dirtySlots |= 1 << index;
        }

        public void DeferSlots(LegacyDirtySlotSet slots)
        {
            dirtySlots |= slots.Value & AllLegacySlots;
        }

        public LegacyResolvedMask GetResolution(ClothingSlot slot)
        {
            return resolutions[ValidateSlot(slot)];
        }

        public string GetStatus(ClothingSlot slot)
        {
            return statuses[ValidateSlot(slot)];
        }

        public bool SetResolution(
            ClothingSlot slot,
            LegacyResolvedMask resolution,
            string status)
        {
            int index = ValidateLegacySlot(slot);
            LegacyResolvedMask previous = resolutions[index];
            string previousFingerprint = previous == null ? null : previous.Fingerprint;
            string currentFingerprint = resolution == null ? null : resolution.Fingerprint;
            resolutions[index] = resolution;
            statuses[index] = status;
            return !string.Equals(
                previousFingerprint,
                currentFingerprint,
                StringComparison.Ordinal);
        }

        public bool ClearProviderResolutions(string status)
        {
            bool removed = false;
            for (int index = FirstLegacySlot; index < SlotCount; index++)
            {
                removed |= resolutions[index] != null;
                resolutions[index] = null;
                statuses[index] = status;
            }

            dirtySlots = 0;
            autoConversionAttemptGrantedThisRefresh = false;
            return removed;
        }

        public void ResetConversionTracking()
        {
            Array.Clear(
                autoConversionAttemptKeys,
                0,
                autoConversionAttemptKeys.Length);
            Array.Clear(
                clearedAutomaticFingerprints,
                0,
                clearedAutomaticFingerprints.Length);
            autoConversionAttemptGrantedThisRefresh = false;
        }

        public void ResetConversionTracking(ClothingSlot slot)
        {
            int index = ValidateSlot(slot);
            autoConversionAttemptKeys[index] = null;
            clearedAutomaticFingerprints[index] = null;
        }

        public void ResetConversionTrackingForSlots(IEnumerable<ClothingSlot> slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException("slots");
            }

            foreach (ClothingSlot slot in slots)
            {
                ResetConversionTracking(slot);
            }
        }

        public bool RegisterPortableLayerClear(
            ClothingSlot slot,
            ClothingMaskLayerData layer)
        {
            int index = ValidateSlot(slot);
            if (index < FirstLegacySlot || layer == null ||
                layer.SourceContract != MaskSourceContract.NakayRgbStateCoverage ||
                !string.Equals(
                    layer.SourceProviderId,
                    LegacyMaskDescriptor.ProviderIdValue,
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(layer.SourceFingerprint))
            {
                return false;
            }

            clearedAutomaticFingerprints[index] = layer.SourceFingerprint;
            return true;
        }

        public bool IsFingerprintSuppressed(ClothingSlot slot, string fingerprint)
        {
            int index = ValidateSlot(slot);
            return index >= FirstLegacySlot &&
                   !string.IsNullOrEmpty(clearedAutomaticFingerprints[index]) &&
                   !string.IsNullOrEmpty(fingerprint) &&
                   string.Equals(
                       clearedAutomaticFingerprints[index],
                       fingerprint,
                       StringComparison.Ordinal);
        }

        public bool IsCurrentResolutionSuppressed(ClothingSlot slot)
        {
            LegacyResolvedMask resolution = GetResolution(slot);
            return resolution != null &&
                   IsFingerprintSuppressed(slot, resolution.Fingerprint);
        }

        public void BeginAutoConversionRefresh()
        {
            autoConversionAttemptGrantedThisRefresh = false;
        }

        public LegacyAutoConversionAttemptDecision TryBeginAutoConversionAttempt(
            ClothingSlot slot,
            string attemptKey)
        {
            int index = ValidateLegacySlot(slot);
            if (string.Equals(
                autoConversionAttemptKeys[index],
                attemptKey,
                StringComparison.Ordinal))
            {
                return LegacyAutoConversionAttemptDecision.AlreadyAttempted;
            }

            if (autoConversionAttemptGrantedThisRefresh)
            {
                dirtySlots |= 1 << index;
                return LegacyAutoConversionAttemptDecision.DeferredUntilNextRefresh;
            }

            autoConversionAttemptGrantedThisRefresh = true;
            autoConversionAttemptKeys[index] = attemptKey;
            return LegacyAutoConversionAttemptDecision.Granted;
        }

        private static int ValidateSlot(ClothingSlot slot)
        {
            int index = (int)slot;
            if (index < (int)ClothingSlot.Top || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            return index;
        }

        private static int ValidateLegacySlot(ClothingSlot slot)
        {
            int index = ValidateSlot(slot);
            if (index < FirstLegacySlot)
            {
                throw new ArgumentOutOfRangeException(
                    "slot",
                    "The top slot is not a direct legacy-mask source.");
            }

            return index;
        }
    }
}
