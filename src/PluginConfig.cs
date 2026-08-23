using BepInEx.Configuration;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class PluginConfig
    {
        public ConfigEntry<bool> Enabled;
        public ConfigEntry<int> DefaultOutputResolution;
        public ConfigEntry<ColorClassificationMode> ColorClassification;
        public ConfigEntry<int> ColorTolerance;
        public ConfigEntry<UnknownColorPolicy> UnknownColorPolicy;
        public ConfigEntry<GradientHandlingMode> GradientHandling;
        public ConfigEntry<UnknownStatePolicy> UnknownStatePolicy;
        public ConfigEntry<bool> AllowUnauditedChaAlphaMask;
        public ConfigEntry<bool> EnableNakayLegacyCompatibility;
        public ConfigEntry<bool> AutoConvertNakayLegacyMasks;
        public ConfigEntry<bool> LegacyIndexAutoRefresh;
        public ConfigEntry<int> LegacyCacheMemoryLimitMegabytes;
        public ConfigEntry<bool> LegacyDiagnostics;
        public ConfigEntry<bool> DebugLogging;
        public ConfigEntry<bool> LogStateChanges;
        public ConfigEntry<bool> LogComposition;
        public ConfigEntry<float> LogIntervalSeconds;
        public ConfigEntry<KeyboardShortcut> DumpDiagnosticsShortcut;

        public static PluginConfig Bind(ConfigFile config)
        {
            PluginConfig result = new PluginConfig();
            result.Enabled = config.Bind("General", "Enabled", true,
                "Global kill-switch. Disabling restores the upstream body material without deleting saved masks.");
            result.DefaultOutputResolution = config.Bind("Masks", "Default output resolution", 512,
                "Composition size when the current vanilla/external body mask has no usable size.");
            result.ColorClassification = config.Bind("Masks", "Color classification",
                ColorClassificationMode.Threshold,
                "Exact, tolerance threshold, or nearest categorical color decoding.");
            result.ColorTolerance = config.Bind("Masks", "ColorTolerance", 12,
                "RGB tolerance used by Threshold classification (0-255).");
            result.UnknownColorPolicy = config.Bind("Masks", "UnknownColorPolicy",
                global::NightOwlZzz.Koikatsu.BodyMaskLayers.UnknownColorPolicy.RejectMask,
                "RejectMask is the safe default. Blue is considered unsupported/packed data.");
            result.GradientHandling = config.Bind(
                "Masks",
                "Gradient Handling",
                GradientHandlingMode.Auto,
                "Auto preserves safe continuous R/G coverage, PreserveContinuous accepts explicit R/G coverage, and StrictCategorical keeps the original palette-only decoder.");
            result.UnknownStatePolicy = config.Bind("States", "UnknownStatePolicy",
                global::NightOwlZzz.Koikatsu.BodyMaskLayers.UnknownStatePolicy.NoContribution,
                "Behavior for clothing state values outside the confirmed vanilla 0-3 range.");
            result.AllowUnauditedChaAlphaMask = config.Bind(
                "Compatibility",
                "Allow unaudited ChaAlphaMask versions",
                false,
                "If false, custom composition stays inactive when a ChaAlphaMask version other than audited 1.0.0 is detected.");
            result.EnableNakayLegacyCompatibility = config.Bind(
                "Compatibility",
                "Enable Nakay Legacy Compatibility",
                true,
                "Resolve KK_ChaAlphaMask manifest textures directly when the old plugin is absent. Never modifies zipmods.");
            result.AutoConvertNakayLegacyMasks = config.Bind(
                "Compatibility",
                "Auto Convert Nakay Legacy Masks",
                true,
                "Silently create a portable native BML2 layer from a resolved legacy source without overwriting an existing native layer. Saving the card or coordinate persists the copy.");
            result.LegacyIndexAutoRefresh = config.Bind(
                "Compatibility",
                "Legacy Index Auto Refresh",
                true,
                "Build the session metadata index from Sideloader manifests once during startup.");
            result.LegacyCacheMemoryLimitMegabytes = config.Bind(
                "Compatibility",
                "Legacy Cache Memory Limit MB",
                128,
                "Maximum memory for shared compiled legacy masks. Values are clamped to 16-1024 MB.");
            result.LegacyDiagnostics = config.Bind(
                "Diagnostics",
                "Legacy Diagnostics",
                false,
                "Enable numeric legacy provider/cache counters. String formatting only occurs in explicit diagnostic dumps.");
            result.DebugLogging = config.Bind("Diagnostics", "DebugLogging", false,
                "Log state, binding and composition decisions.");
            result.LogStateChanges = config.Bind("Diagnostics", "LogStateChanges", false,
                "Log runtime clothing state/item/availability transitions (rate limited per character).");
            result.LogComposition = config.Bind("Diagnostics", "LogComposition", false,
                "Log CPU composition duration whenever a dirty mask is rebuilt.");
            result.LogIntervalSeconds = config.Bind("Diagnostics", "LogIntervalSeconds", 1f,
                "Minimum seconds between state-change log lines for each character (0.1-3600).");
            result.DumpDiagnosticsShortcut = config.Bind("Diagnostics", "DumpDiagnosticsShortcut",
                new KeyboardShortcut(UnityEngine.KeyCode.F8, UnityEngine.KeyCode.LeftControl),
                "Write a diagnostic snapshot to the log and BepInEx/config/BodyMaskLayers/Diagnostics.");
            return result;
        }

        public MaskDecodeOptions CreateDecodeOptions()
        {
            return new MaskDecodeOptions
            {
                ClassificationMode = ColorClassification.Value,
                ColorTolerance = ColorTolerance.Value,
                UnknownColorPolicy = UnknownColorPolicy.Value,
                GradientHandlingMode = GradientHandling.Value
            };
        }

        public int GetDefaultOutputResolution()
        {
            return Clamp(
                DefaultOutputResolution.Value,
                1,
                PortableMaskFormatLimits.MaximumDimension);
        }

        public int GetLegacyCacheMemoryLimitMegabytes()
        {
            return Clamp(LegacyCacheMemoryLimitMegabytes.Value, 16, 1024);
        }

        public float GetLogIntervalSeconds()
        {
            float value = LogIntervalSeconds.Value;
            if (value < 0.1f)
            {
                return 0.1f;
            }

            return value > 3600f ? 3600f : value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
