using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class ClothingSlotRegistry
    {
        public const int SlotCount = 9;

        private static readonly string[] DisplayNames =
        {
            "Top",
            "Bottom",
            "Bra",
            "Shorts",
            "Gloves",
            "Pantyhose",
            "Socks",
            "Indoor shoes",
            "Outdoor shoes"
        };

        public static string GetDisplayName(ClothingSlot slot)
        {
            int index = (int)slot;
            return index >= 0 && index < DisplayNames.Length
                ? DisplayNames[index]
                : "Unknown (" + index + ")";
        }

        public static string[] GetDisplayNames()
        {
            return (string[])DisplayNames.Clone();
        }

        public static bool IsValid(ClothingSlot slot)
        {
            int index = (int)slot;
            return index >= 0 && index < SlotCount;
        }

        public static ChaListDefine.CategoryNo GetCategory(ClothingSlot slot)
        {
            switch (slot)
            {
                case ClothingSlot.Top:
                    return ChaListDefine.CategoryNo.co_top;
                case ClothingSlot.Bottom:
                    return ChaListDefine.CategoryNo.co_bot;
                case ClothingSlot.Bra:
                    return ChaListDefine.CategoryNo.co_bra;
                case ClothingSlot.Shorts:
                    return ChaListDefine.CategoryNo.co_shorts;
                case ClothingSlot.Gloves:
                    return ChaListDefine.CategoryNo.co_gloves;
                case ClothingSlot.Pantyhose:
                    return ChaListDefine.CategoryNo.co_panst;
                case ClothingSlot.Socks:
                    return ChaListDefine.CategoryNo.co_socks;
                case ClothingSlot.IndoorShoes:
                case ClothingSlot.OutdoorShoes:
                    return ChaListDefine.CategoryNo.co_shoes;
                default:
                    throw new ArgumentOutOfRangeException("slot");
            }
        }
    }
}
