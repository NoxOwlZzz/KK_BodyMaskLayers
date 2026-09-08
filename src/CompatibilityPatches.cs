using BepInEx.Bootstrap;
using HarmonyLib;
#if !KKS
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using ExtensibleSaveFormat;
using UnityEngine.UI;
#endif

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class CompatibilityPatches
    {
#if !KKS
        private sealed class PartialCoordinateLoadState
        {
            public ChaControl Target;
            public bool[] SelectedSlots;
            public Dictionary<ClothingSlot, ClothingMaskLayerData> SourceLayers;
        }
#endif

        public static void Install(Harmony harmony)
        {
#if KKS
            // This adapter can return before copying clothing; its postfix cannot identify a successful load.
            if (Chainloader.PluginInfos.ContainsKey(GameTarget.CoordinateLoadOptionGuid))
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Coordinate Load Option partial-slot mask merging is unavailable in Sunshine. " +
                    "Use full coordinate loading to transfer BodyMask Layers data.");
            }
#else
            try
            {
                InstallCoordinateLoadOptionBridge(harmony);
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Coordinate Load Option bridge was disabled after a safe install failure: " +
                    exception.Message);
            }
#endif
        }

        public static void LogDetectedPlugins()
        {
            LogPlugin("nakay.kk.ChaAlphaMask", "external body-mask provider");
            LogPlugin("com.deathweasel.bepinex.materialeditor", "Material Editor");
            LogPlugin("com.deathweasel.bepinex.uncensorselector", "Uncensor Selector");
            LogPlugin(GameTarget.CoordinateLoadOptionGuid, "Coordinate Load Option");
            LogPlugin("com.bepis.bepinex.sideloader", "Sideloader identity binding");
        }

#if !KKS
        private static void InstallCoordinateLoadOptionBridge(Harmony harmony)
        {
            PluginInfo pluginInfo;
            if (Chainloader.PluginInfos.TryGetValue(
                    "com.jim60105.kk.coordinateloadoption",
                    out pluginInfo))
            {
                string version = pluginInfo.Metadata.Version == null
                    ? string.Empty
                    : pluginInfo.Metadata.Version.ToString();
                if (!string.Equals(version, "21.1.4", StringComparison.Ordinal) &&
                    !string.Equals(version, "21.1.4.0", StringComparison.Ordinal))
                {
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Coordinate Load Option " + version +
                        " is not the audited 21.1.4 adapter target; partial-slot bridge is disabled.");
                    return;
                }
            }

            Type coordinateLoad = Type.GetType(
                "KK_CoordinateLoadOption.CoordinateLoad, KK_CoordinateLoadOption", false);
            if (coordinateLoad == null)
            {
                BodyMaskLayersPlugin.Log.LogInfo("Coordinate Load Option not detected; partial-slot bridge is idle.");
                return;
            }

            MethodInfo original = AccessTools.Method(
                coordinateLoad,
                "ChangeCoordinate",
                new Type[] { typeof(object) });
            if (original == null)
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Coordinate Load Option was detected, but ChangeCoordinate(object) was not found.");
                return;
            }

            HarmonyMethod prefix = new HarmonyMethod(AccessTools.Method(
                typeof(CompatibilityPatches), "CoordinateLoadPrefix"));
            HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(
                typeof(CompatibilityPatches), "CoordinateLoadPostfix"));
            harmony.Patch(original, prefix, postfix);
            BodyMaskLayersPlugin.Log.LogInfo(
                "Installed Coordinate Load Option per-slot metadata merge bridge.");
        }

        private static void CoordinateLoadPrefix(object __0, out PartialCoordinateLoadState __state)
        {
            __state = null;
            try
            {
                Type coordinateLoad = Type.GetType(
                    "KK_CoordinateLoadOption.CoordinateLoad, KK_CoordinateLoadOption", false);
                Type patches = Type.GetType(
                    "KK_CoordinateLoadOption.Patches, KK_CoordinateLoadOption", false);
                if (coordinateLoad == null || patches == null)
                {
                    return;
                }

                ChaControl target = ResolveChaControl(__0);
                FieldInfo temporaryField = AccessTools.Field(coordinateLoad, "tmpChaCtrl");
                ChaControl temporary = temporaryField == null
                    ? null
                    : temporaryField.GetValue(null) as ChaControl;
                if (target == null || temporary == null || temporary.nowCoordinate == null)
                {
                    return;
                }

                bool[] selected = ReadSelectedClothingSlots(patches);
                PluginData sourceData = CoordinateDataHandler.ReadFromClothes(
                    temporary.nowCoordinate.clothes);
                Dictionary<ClothingSlot, ClothingMaskLayerData> sourceLayers;
                string error;
                if (!CoordinateDataHandler.TryReadPluginData(sourceData, out sourceLayers, out error))
                {
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Skipping partial BodyMask Layers merge because source metadata is invalid: " + error);
                    return;
                }

                __state = new PartialCoordinateLoadState
                {
                    Target = target,
                    SelectedSlots = selected,
                    SourceLayers = sourceLayers
                };
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Coordinate Load Option prefix bridge failed safely: " + exception.Message);
            }
        }

        private static void CoordinateLoadPostfix(PartialCoordinateLoadState __state)
        {
            if (__state == null || __state.Target == null)
            {
                return;
            }

            try
            {
                BodyMaskCharacterController controller =
                    __state.Target.GetComponent<BodyMaskCharacterController>();
                if (controller == null)
                {
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Partial coordinate load target has no BodyMask Layers controller.");
                    return;
                }

                controller.ApplyPartialCoordinateLayers(
                    __state.SourceLayers,
                    __state.SelectedSlots);
                BodyMaskLayersPlugin.LogDebug(
                    "Merged BodyMask Layers metadata for selected Coordinate Load Option clothing slots.");
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Coordinate Load Option postfix bridge failed safely: " + exception.Message);
            }
        }

        private static bool[] ReadSelectedClothingSlots(Type patches)
        {
            bool[] result = new bool[ClothingSlotRegistry.SlotCount];
            FieldInfo togglesField = AccessTools.Field(patches, "tgls");
            Array toggles = togglesField == null ? null : togglesField.GetValue(null) as Array;
            if (toggles == null)
            {
                return result;
            }

            int count = Math.Min(ClothingSlotRegistry.SlotCount, toggles.Length);
            for (int i = 0; i < count; i++)
            {
                Toggle toggle = toggles.GetValue(i) as Toggle;
                result[i] = toggle != null && toggle.isOn;
            }

            return result;
        }

        private static ChaControl ResolveChaControl(object value)
        {
            ChaControl direct = value as ChaControl;
            if (direct != null || value == null)
            {
                return direct;
            }

            Type type = value.GetType();
            PropertyInfo property = type.GetProperty(
                "charInfo",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(value, null) as ChaControl;
            }

            FieldInfo field = type.GetField(
                "charInfo",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(value) as ChaControl;
        }

#endif

        private static void LogPlugin(string guid, string feature)
        {
            if (Chainloader.PluginInfos.ContainsKey(guid))
            {
                BodyMaskLayersPlugin.Log.LogInfo("Detected " + feature + " (" + guid + ").");
            }
            else
            {
                BodyMaskLayersPlugin.LogDebug(feature + " not detected (" + guid + ").");
            }
        }

    }
}
