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
                "Auto preserves safe continuous R/G coverage, PreserveContinuous accepts explicit R/G coverage, and StrictCategorical accepts only the defined palette.");
            result.UnknownStatePolicy = config.Bind("States", "UnknownStatePolicy",
                global::NightOwlZzz.Koikatsu.BodyMaskLayers.UnknownStatePolicy.NoContribution,
                "Behavior for clothing state values outside the confirmed vanilla 0-3 range.");
            result.DebugLogging = config.Bind("Diagnostics", "DebugLogging", false,
                "Enable diagnostic debug messages and numeric runtime counters.");
            result.LogStateChanges = config.Bind("Diagnostics", "LogStateChanges", false,
                "Log runtime clothing state/item/availability transitions (rate limited per character).");
            result.LogComposition = config.Bind("Diagnostics", "LogComposition", false,
                "Log CPU composition duration whenever a dirty mask is rebuilt.");
            result.LogIntervalSeconds = config.Bind("Diagnostics", "LogIntervalSeconds", 1f,
                "Minimum seconds between state-change log lines for each character (0.1-3600).");
            result.DumpDiagnosticsShortcut = config.Bind("Diagnostics", "DumpDiagnosticsShortcut",
                new KeyboardShortcut(UnityEngine.KeyCode.F8, UnityEngine.KeyCode.LeftControl),
                "Write a diagnostic snapshot to the log and the BepInEx config directory.");
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
