using System;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class Diagnostics
    {
        public static void LogEnvironment(ManualLogSource logger)
        {
            logger.LogInfo("Runtime: Unity " + Application.unityVersion +
                           ", CLR " + Environment.Version +
                           ", process " + Application.productName + ".");
            logger.LogInfo("Shader contract: _AlphaMask RG + _alpha_a/_alpha_b; output preserves base B/A.");
            logger.LogInfo("Slots: Top, Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, IndoorShoes, OutdoorShoes.");
            logger.LogInfo("State map: raw 0=Full, 1/2=Partial, 3=Off; unexpected values use configured policy.");
            logger.LogInfo("Compatibility bridge ordering: after nakay.kk.ChaAlphaMask when present.");
        }

        public static string BuildCharacterSnapshot(BodyMaskCharacterController controller)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("BodyMask Layers character=")
                .Append(controller == null || controller.ChaControl == null
                    ? "<missing>"
                    : controller.ChaControl.name)
                .Append(", instanceId=")
                .Append(controller == null ? 0 : controller.CharacterInstanceId)
                .Append(", coordinateType=")
                .Append(controller == null ? -1 : controller.CoordinateType)
                .AppendLine();
            if (controller == null)
            {
                return builder.ToString();
            }

            builder.Append("target=").Append(controller.TargetDescription)
                .Append(", compositeActive=").Append(controller.IsCompositeActive)
                .Append(", output=").Append(controller.OutputDescription)
                .AppendLine();
            builder.Append("baseTexture=").Append(controller.BaseTextureDescription)
                .Append(", lastRecompositionReason=").Append(controller.LastCompositionReason)
                .AppendLine();
            builder.Append("materialContract=").Append(controller.MaterialContractDescription)
                .AppendLine();
            builder.AppendLine(
                "compositionMethod=CPU dirty-only categorical OR; upstream RG is combined, B/A are copied unchanged");
            for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
            {
                builder.Append("  [").Append(i).Append("] ")
                    .Append(ClothingSlotRegistry.GetDisplayName((ClothingSlot)i))
                    .Append(": ")
                    .Append(controller.DescribeLayer((ClothingSlot)i))
                    .AppendLine();
            }

            return builder.ToString();
        }

        public static string BuildCompatibilitySnapshot()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Compatibility/possible conflicts: ");
            AppendPlugin(builder, "KCOX", "KCOX");
            AppendPlugin(builder, "nakay.kk.ChaAlphaMask", "ChaAlphaMask");
            AppendPlugin(builder, "com.deathweasel.bepinex.materialeditor", "MaterialEditor");
            AppendPlugin(builder, "com.deathweasel.bepinex.uncensorselector", "UncensorSelector");
            AppendPlugin(builder, "com.jim60105.kk.coordinateloadoption", "CoordinateLoadOption");
            AppendPlugin(builder, "com.bepis.bepinex.sideloader", "Sideloader");
            builder.Append("ownershipAllowed=").Append(CompatibilityPatches.CompositionOwnershipAllowed)
                .Append(", unauditedOverride=")
                .Append(BodyMaskLayersPlugin.Settings.AllowUnauditedChaAlphaMask.Value);
            return builder.ToString();
        }

        private static void AppendPlugin(StringBuilder builder, string guid, string label)
        {
            BepInEx.PluginInfo plugin;
            if (!Chainloader.PluginInfos.TryGetValue(guid, out plugin))
            {
                builder.Append(label).Append("=absent; ");
                return;
            }

            builder.Append(label).Append('=')
                .Append(plugin.Metadata.Version == null ? "unknown" : plugin.Metadata.Version.ToString())
                .Append("; ");
        }
    }
}
