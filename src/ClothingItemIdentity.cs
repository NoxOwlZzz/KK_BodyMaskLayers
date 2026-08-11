using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class ClothingItemIdentity
    {
        public ClothingSlot Slot;
        public int Category;
        public int LocalItemId;
        public int OriginalItemId;
        public string SideloaderGuid;
        public string DisplayName;

        public bool HasItem
        {
            get { return LocalItemId > 0; }
        }

        public ClothingItemIdentity DeepClone()
        {
            return (ClothingItemIdentity)MemberwiseClone();
        }

        public bool Matches(ClothingItemIdentity current, MaskBindingMode bindingMode)
        {
            if (current == null || Slot != current.Slot)
            {
                return false;
            }

            if (bindingMode == MaskBindingMode.SlotOnly)
            {
                return current.HasItem;
            }

            if (!HasItem || !current.HasItem || Category != current.Category)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(SideloaderGuid) || !string.IsNullOrEmpty(current.SideloaderGuid))
            {
                return string.Equals(SideloaderGuid, current.SideloaderGuid, StringComparison.Ordinal) &&
                       OriginalItemId == current.OriginalItemId;
            }

            return LocalItemId == current.LocalItemId;
        }

        public override string ToString()
        {
            if (!HasItem)
            {
                return Slot + ": none";
            }

            string stable = string.IsNullOrEmpty(SideloaderGuid)
                ? "local=" + LocalItemId
                : string.Format("guid={0}, slot={1}, local={2}", SideloaderGuid, OriginalItemId, LocalItemId);
            return string.Format("{0}: category={1}, {2}, name={3}", Slot, Category, stable, DisplayName ?? string.Empty);
        }
    }
}
