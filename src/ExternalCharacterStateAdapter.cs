using System;
using System.Reflection;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal static class ExternalCharacterStateAdapter
    {
        private static MethodInfo forceHighPolyIsHiPoly;
        private static bool forceHighPolyLookupComplete;

        public static bool IsEffectiveHighPoly(ChaControl character)
        {
            if (character == null)
            {
                return false;
            }

            if (character.hiPoly)
            {
                return true;
            }

            if (!forceHighPolyLookupComplete)
            {
                forceHighPolyLookupComplete = true;
                Type type = Type.GetType("KK_Plugins.ForceHighPoly, KK_ForceHighPoly", false);
                forceHighPolyIsHiPoly = type == null
                    ? null
                    : type.GetMethod(
                        "IsHiPoly",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new Type[] { typeof(ChaControl) },
                        null);
            }

            if (forceHighPolyIsHiPoly == null)
            {
                return false;
            }

            try
            {
                object result = forceHighPolyIsHiPoly.Invoke(
                    null,
                    new object[] { character });
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.LogDebug(
                    "Force High Poly compatibility lookup failed safely: " +
                    exception.Message);
                return false;
            }
        }

        public static bool TryBuildBindingQuery(
            ChaControl character,
            ClothingSlot slot,
            out ExternalMaskBindingQuery query)
        {
            query = null;
            ClothingSlot identitySlot = slot;
            int category = (int)ClothingSlotRegistry.GetCategory(slot);
            bool botMask = false;
            if (slot == ClothingSlot.Bottom && character.notBot)
            {
                identitySlot = ClothingSlot.Top;
                category--;
                botMask = true;
            }
            else if (slot == ClothingSlot.Bra && character.notBra)
            {
                identitySlot = ClothingSlot.Top;
            }
            else if (slot == ClothingSlot.Shorts && character.notShorts)
            {
                identitySlot = ClothingSlot.Bra;
                category--;
                botMask = true;
            }

            ClothingItemIdentity identity =
                ClothingItemIdentityResolver.Resolve(character, identitySlot);
            if (identity == null || !identity.HasItem)
            {
                return false;
            }

            bool? option01;
            bool? option02;
            ReadDrawOptions(character, slot, out option01, out option02);
            query = new ExternalMaskBindingQuery
            {
                Slot = slot,
                Category = category,
                ResolvedItemId = identity.LocalItemId,
                OriginalItemId = identitySlot == slot ? identity.OriginalItemId : 0,
                SideloaderGuid = identity.SideloaderGuid,
                BotMask = botMask,
                ObjectOption01 = option01,
                ObjectOption02 = option02
            };
            return true;
        }

        public static int GetDrawOptionSignature(
            ChaControl character,
            ClothingSlot slot)
        {
            bool? option01;
            bool? option02;
            ReadDrawOptions(character, slot, out option01, out option02);
            return NullableBoolBits(option01) | (NullableBoolBits(option02) << 2);
        }

        public static void Reset()
        {
            forceHighPolyIsHiPoly = null;
            forceHighPolyLookupComplete = false;
        }

        private static void ReadDrawOptions(
            ChaControl character,
            ClothingSlot slot,
            out bool? option01,
            out bool? option02)
        {
            option01 = true;
            option02 = true;
            int index = (int)slot;
            try
            {
                if (slot == ClothingSlot.Bra && character.nowCoordinate != null)
                {
                    bool[] values = character.nowCoordinate.clothes.hideBraOpt;
                    option01 = values != null && values.Length > 0 ? !values[0] : (bool?)null;
                    option02 = values != null && values.Length > 1 ? !values[1] : (bool?)null;
                }
                else if (slot == ClothingSlot.Shorts && character.nowCoordinate != null)
                {
                    bool[] values = character.nowCoordinate.clothes.hideShortsOpt;
                    option01 = values != null && values.Length > 0 ? !values[0] : (bool?)null;
                    option02 = values != null && values.Length > 1 ? !values[1] : (bool?)null;
                }
                else if (character.nowCoordinate != null &&
                         character.nowCoordinate.clothes.parts != null &&
                         index < character.nowCoordinate.clothes.parts.Length)
                {
                    bool[] values = character.nowCoordinate.clothes.parts[index].hideOpt;
                    option01 = values != null && values.Length > 0 ? !values[0] : (bool?)true;
                    option02 = values != null && values.Length > 1 ? !values[1] : (bool?)true;
                }

                if (character.cusClothesCmp != null &&
                    index < character.cusClothesCmp.Length &&
                    character.cusClothesCmp[index] != null)
                {
                    if (character.cusClothesCmp[index].objOpt01 == null ||
                        character.cusClothesCmp[index].objOpt01.Length == 0)
                    {
                        option01 = null;
                    }

                    if (character.cusClothesCmp[index].objOpt02 == null ||
                        character.cusClothesCmp[index].objOpt02.Length == 0)
                    {
                        option02 = null;
                    }
                }
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.LogDebug(
                    "External draw-option lookup failed: " + exception.Message);
                option01 = null;
                option02 = null;
            }
        }

        private static int NullableBoolBits(bool? value)
        {
            return !value.HasValue ? 0 : (value.Value ? 2 : 1);
        }
    }
}
