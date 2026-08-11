using System;
using HarmonyLib;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    [HarmonyPatch]
    internal static class BodyMaskHooks
    {
        [HarmonyPatch(typeof(Material), "SetTexture", new Type[] { typeof(int), typeof(Texture) })]
        [HarmonyPrefix]
        [HarmonyPriority(200)]
        [HarmonyAfter(new string[] { "KCOX", "KoiClothesOverlayController", "nakay.kk.ChaAlphaMask" })]
        private static void MaterialSetTexturePrefix(Material __instance, int nameID, ref Texture value)
        {
            if (nameID != ChaShader._AlphaMask)
            {
                return;
            }

            BodyMaskCharacterController controller;
            if (BodyMaskCharacterController.TryGetForMaterial(__instance, out controller))
            {
                if (controller.IsInternalMaterialWrite)
                {
                    controller.FinalizeInternalTextureWrite(ref value);
                    return;
                }

                Texture incoming = value;
                controller.InterceptExternalTexture(incoming, ref value);
            }
        }

        [HarmonyPatch(typeof(Material), "SetFloat", new Type[] { typeof(int), typeof(float) })]
        [HarmonyPrefix]
        [HarmonyPriority(200)]
        [HarmonyAfter(new string[] { "KCOX", "KoiClothesOverlayController", "nakay.kk.ChaAlphaMask" })]
        private static void MaterialSetFloatPrefix(Material __instance, int nameID, ref float value)
        {
            if (nameID != ChaShader._alpha_a && nameID != ChaShader._alpha_b)
            {
                return;
            }

            BodyMaskCharacterController controller;
            if (BodyMaskCharacterController.TryGetForMaterial(__instance, out controller))
            {
                if (controller.IsInternalMaterialWrite)
                {
                    controller.FinalizeInternalFloatWrite(nameID, ref value);
                    return;
                }

                float incoming = value;
                controller.InterceptExternalFloat(nameID, incoming, ref value);
            }
        }

        [HarmonyPatch(typeof(ChaControl), "SetClothesState", new Type[]
        {
            typeof(int), typeof(byte), typeof(bool)
        })]
        [HarmonyPostfix]
        private static void SetClothesStatePostfix(ChaControl __instance, int __0)
        {
            BodyMaskCharacterController.NotifyClothingStateChanged(__instance, __0);
        }

        [HarmonyPatch(typeof(ChaControl), "LoadAlphaMaskTexture", new Type[]
        {
            typeof(string), typeof(string), typeof(byte)
        })]
        [HarmonyPostfix]
        private static void LoadAlphaMaskTexturePostfix(ChaControl __instance, byte __2)
        {
            if (__2 == 0)
            {
                BodyMaskCharacterController.NotifyCharacterDirty(__instance, true);
            }
        }

    }
}
