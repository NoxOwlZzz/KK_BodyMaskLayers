using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI.Chara;
using System;
using System.IO;
using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("marco.kkapi", "1.42.2")]
    [BepInDependency("com.bepis.bepinex.extendedsave", "20.0")]
    [BepInDependency("com.bepis.bepinex.sideloader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("KCOX", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("nakay.kk.ChaAlphaMask", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("Koikatu.exe")]
    [BepInProcess("CharaStudio.exe")]
    public sealed class BodyMaskLayersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.nightowlzzz.koikatsu.bodymasklayers";
        public const string PluginName = "BodyMask Layers";
        public const string PluginVersion = "0.3.0";

        internal static ManualLogSource Log;
        internal static PluginConfig Settings;

        private Harmony _harmony;
        private MakerInterface _makerInterface;

        private void Awake()
        {
            Log = base.Logger;
            Settings = PluginConfig.Bind(Config);
            Config.SettingChanged += OnConfigSettingChanged;
            Diagnostics.LogEnvironment(Log);
            ExternalMaskProvider.Initialize();

            CharacterApi.RegisterExtraBehaviour<BodyMaskCharacterController>(PluginGuid);
            _makerInterface = new MakerInterface(this);
            _makerInterface.Register();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(BodyMaskLayersPlugin).Assembly);
            CompatibilityPatches.Install(_harmony);
            CompatibilityPatches.LogDetectedPlugins();
            Log.LogInfo(PluginName + " " + PluginVersion + " initialized.");
        }

        private void Update()
        {
            if (_makerInterface != null)
            {
                _makerInterface.Tick();
            }

            if (Settings != null && Settings.DumpDiagnosticsShortcut.Value.IsDown())
            {
                DumpDiagnostics();
            }
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnConfigSettingChanged;
            if (_makerInterface != null)
            {
                _makerInterface.Dispose();
                _makerInterface = null;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            ExternalMaskProvider.Shutdown();
        }

        public static void LogDebug(string message)
        {
            if (Log != null && Settings != null && Settings.DebugLogging.Value)
            {
                Log.LogInfo("[debug] " + message);
            }
        }

        private static void OnConfigSettingChanged(object sender, SettingChangedEventArgs eventArgs)
        {
            if (Settings == null || eventArgs == null)
            {
                return;
            }

            ConfigEntryBase changed = eventArgs.ChangedSetting;
            bool requiresDecode = changed == Settings.ColorClassification ||
                                  changed == Settings.ColorTolerance ||
                                  changed == Settings.UnknownColorPolicy ||
                                  changed == Settings.GradientHandling;
            bool affectsRuntime = requiresDecode ||
                                  changed == Settings.Enabled ||
                                  changed == Settings.DefaultOutputResolution ||
                                  changed == Settings.UnknownStatePolicy;
            if (changed == Settings.DebugLogging)
            {
                BodyMaskPerformanceMetrics.SetEnabled(Settings.DebugLogging.Value);
            }
            if (!affectsRuntime)
            {
                return;
            }

            BodyMaskCharacterController.NotifyConfigurationChanged(requiresDecode);
        }

        private static void DumpDiagnostics()
        {
            BodyMaskCharacterController[] controllers =
                BodyMaskCharacterController.GetControllersSnapshot();
            StringBuilder dump = new StringBuilder();
            dump.Append("BodyMask Layers diagnostic dump; UTC=")
                .Append(DateTime.UtcNow.ToString("o"))
                .Append("; controllers=").Append(controllers.Length)
                .Append("; enabled=").Append(Settings.Enabled.Value)
                .AppendLine();
            dump.AppendLine(Diagnostics.BuildCompatibilitySnapshot());
            dump.AppendLine(BodyMaskPerformanceMetrics.BuildSummary());
            for (int i = 0; i < controllers.Length; i++)
            {
                dump.AppendLine(Diagnostics.BuildCharacterSnapshot(controllers[i]));
            }

            string text = dump.ToString();
            Log.LogInfo(text);
            try
            {
                string directory = Path.Combine(
                    Path.Combine(Paths.ConfigPath, "BodyMaskLayers"),
                    "Diagnostics");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(
                    directory,
                    "BodyMaskLayers-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                File.WriteAllText(path, text, new UTF8Encoding(false));
                Log.LogInfo("Diagnostic snapshot written to " + path);
            }
            catch (Exception exception)
            {
                Log.LogError("Could not write diagnostic snapshot: " + exception.Message);
            }
        }
    }
}
