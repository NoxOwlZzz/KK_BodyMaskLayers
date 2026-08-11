using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class BodyMaskTargetResolver
    {
        private static readonly MethodInfo VanillaMaskGetter = AccessTools.PropertyGetter(
            typeof(ChaControl), "texBodyAlphaMask");

        public static Material GetExactBodyMaterial(ChaControl character)
        {
            if (character == null || character.customMatBody == null)
            {
                return null;
            }

            return character.customMatBody;
        }

        public static Texture GetVanillaTopMask(ChaControl character)
        {
            if (character == null || VanillaMaskGetter == null)
            {
                return null;
            }

            try
            {
                return VanillaMaskGetter.Invoke(character, null) as Texture;
            }
            catch
            {
                return null;
            }
        }

        public static bool SupportsBodyMaskContract(Material material)
        {
            return material != null &&
                   material.HasProperty(ChaShader._AlphaMask) &&
                   material.HasProperty(ChaShader._alpha_a) &&
                   material.HasProperty(ChaShader._alpha_b);
        }
    }
}
