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
            logger.LogInfo("Mask states follow slot visibility: 0=Full, visible 1/2=Partial, hidden=Off; unexpected values use configured policy.");
            logger.LogInfo("External body-mask writes are treated as the composition base.");
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
                "compositionMethod=CPU dirty-only max hide coverage; binary bitsets and continuous byte planes share one compositor; upstream B/A are copied unchanged");
            for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
            {
                builder.Append("  [").Append(i).Append("] ")
                    .Append(ClothingSlotRegistry.GetDisplayName((ClothingSlot)i))
                    .Append(": ")
                    .Append(controller.DescribeLayer((ClothingSlot)i))
                    .AppendLine();
                if (controller.HasExternalSource((ClothingSlot)i))
                {
                    builder.Append("      external: ")
                        .Append(controller.DescribeExternalDetails((ClothingSlot)i))
                        .AppendLine();
                }
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
            AppendPlugin(builder, GameTarget.CoordinateLoadOptionGuid, "CoordinateLoadOption");
            AppendPlugin(builder, "com.bepis.bepinex.sideloader", "Sideloader");
            builder.AppendLine()
                .Append(ExternalMaskProvider.BuildIndexSummary());
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
