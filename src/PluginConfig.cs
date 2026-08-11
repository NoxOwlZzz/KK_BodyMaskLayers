using BepInEx.Configuration;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class PluginConfig
    {
        public ConfigEntry<bool> Enabled;
        public ConfigEntry<int> MinimumResolution;
        public ConfigEntry<int> MaximumMaskResolution;
        public ConfigEntry<int> DefaultOutputResolution;
        public ConfigEntry<int> MaximumPngBytes;
        public ConfigEntry<ColorClassificationMode> ColorClassification;
        public ConfigEntry<int> ColorTolerance;
        public ConfigEntry<UnknownColorPolicy> UnknownColorPolicy;
        public ConfigEntry<UnknownStatePolicy> UnknownStatePolicy;
        public ConfigEntry<MaskBindingMode> MaskBindingMode;
        public ConfigEntry<bool> AllowUnauditedChaAlphaMask;
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
            result.MinimumResolution = config.Bind("Masks", "Minimum resolution", 128,
                "Minimum accepted square power-of-two PNG size.");
            result.MaximumMaskResolution = config.Bind("Masks", "MaximumMaskResolution", 2048,
                "Maximum accepted PNG and composed texture dimension. A larger upstream mask is not downsampled; composition yields safely.");
            result.DefaultOutputResolution = config.Bind("Masks", "Default output resolution", 512,
                "Composition size when the current vanilla/external body mask has no usable size.");
            result.MaximumPngBytes = config.Bind("Masks", "Maximum PNG bytes", 16 * 1024 * 1024,
                "Per-slot safety limit for embedded source PNG bytes.");
            result.ColorClassification = config.Bind("Masks", "Color classification",
                ColorClassificationMode.Threshold,
                "Exact, tolerance threshold, or nearest categorical color decoding.");
            result.ColorTolerance = config.Bind("Masks", "ColorTolerance", 12,
                "RGB tolerance used by Threshold classification (0-255).");
            result.UnknownColorPolicy = config.Bind("Masks", "UnknownColorPolicy",
                global::NightOwlZzz.Koikatsu.BodyMaskLayers.UnknownColorPolicy.RejectMask,
                "RejectMask is the safe default. Blue is considered unsupported/packed data.");
            result.UnknownStatePolicy = config.Bind("States", "UnknownStatePolicy",
                global::NightOwlZzz.Koikatsu.BodyMaskLayers.UnknownStatePolicy.NoContribution,
                "Behavior for clothing state values outside the confirmed vanilla 0-3 range.");
            result.MaskBindingMode = config.Bind("Binding", "MaskBindingMode",
                global::NightOwlZzz.Koikatsu.BodyMaskLayers.MaskBindingMode.SlotAndItem,
                "SlotAndItem prevents a mask from following an unrelated replacement item. SlotOnly is advanced.");
            result.AllowUnauditedChaAlphaMask = config.Bind(
                "Compatibility",
                "Allow unaudited ChaAlphaMask versions",
                false,
                "If false, custom composition stays inactive when a ChaAlphaMask version other than audited 1.0.0 is detected.");
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
                UnknownColorPolicy = UnknownColorPolicy.Value
            };
        }

        public int GetMaximumResolution()
        {
            return Clamp(MaximumMaskResolution.Value, 128, 4096);
        }

        public int GetMinimumResolution()
        {
            return Clamp(MinimumResolution.Value, 1, GetMaximumResolution());
        }

        public int GetDefaultOutputResolution()
        {
            return Clamp(DefaultOutputResolution.Value, GetMinimumResolution(), GetMaximumResolution());
        }

        public int GetMaximumPngBytes()
        {
            return Clamp(MaximumPngBytes.Value, 1024, CardDataSerializer.AbsoluteMaximumPngBytes);
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
