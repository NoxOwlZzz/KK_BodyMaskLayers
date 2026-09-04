using System;
using System.IO;
using BepInEx;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class MakerInterface : IDisposable
    {
        private const int PreviewSize = 150;
        private const float RefreshIntervalSeconds = 0.5f;
        private const float ActionMessageSeconds = 8f;

        private readonly BodyMaskLayersPlugin _owner;
        private SlotControls[] _slotControls;
        private bool _refreshing;
        private bool _sessionActive;
        private int _sessionGeneration;
        private float _nextStatusRefresh;

        public MakerInterface(BodyMaskLayersPlugin owner)
        {
            _owner = owner;
        }

        public void Register()
        {
            MakerAPI.RegisterCustomSubCategories += RegisterControls;
            MakerAPI.ReloadCustomInterface += ReloadInterface;
            MakerAPI.MakerExiting += MakerExiting;
        }

        public void Dispose()
        {
            MakerAPI.RegisterCustomSubCategories -= RegisterControls;
            MakerAPI.ReloadCustomInterface -= ReloadInterface;
            MakerAPI.MakerExiting -= MakerExiting;
            EndSession();
        }

        public void Tick()
        {
            if (!_sessionActive || _slotControls == null || !MakerAPI.InsideMaker ||
                Time.unscaledTime < _nextStatusRefresh)
            {
                return;
            }

            _nextStatusRefresh = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshAll(false);
        }

        private void RegisterControls(object sender, RegisterSubCategoriesEvent e)
        {
            EndSession();
            _sessionActive = true;
            _slotControls = new SlotControls[ClothingSlotRegistry.SlotCount];
            _nextStatusRefresh = 0f;

            for (int index = 0; index < ClothingSlotRegistry.SlotCount; index++)
            {
                ClothingSlot slot = (ClothingSlot)index;
                ClothingSlot capturedSlot = slot;
                MakerCategory category = GetMakerCategory(slot);
                SlotControls controls = new SlotControls(slot);
                _slotControls[index] = controls;

                e.AddControl(new MakerSeparator(category, _owner));
                e.AddControl(new MakerText("Native body alpha mask", category, _owner));

                controls.Preview = e.AddControl(new MakerImage(null, category, _owner)
                {
                    Width = PreviewSize,
                    Height = PreviewSize
                });

                controls.Enabled = e.AddControl(new MakerToggle(
                    category,
                    "Enable native mask",
                    false,
                    _owner));
                UniRx.ObservableExtensions.Subscribe<bool>(
                    controls.Enabled.ValueChanged,
                    delegate(bool value) { ToggleLayer(capturedSlot, value); });

                controls.Load = e.AddControl(new MakerButton(
                    "Load new mask texture",
                    category,
                    _owner));
                controls.Load.OnClick.AddListener(new UnityAction(delegate { LoadPng(capturedSlot); }));
                controls.Clear = e.AddControl(new MakerButton("Clear mask texture", category, _owner));
                controls.Clear.OnClick.AddListener(new UnityAction(delegate { Clear(capturedSlot); }));
                controls.Export = e.AddControl(new MakerButton("Export mask texture", category, _owner));
                controls.Export.OnClick.AddListener(new UnityAction(delegate { ExportPng(capturedSlot); }));
                controls.Bind = e.AddControl(new MakerButton("Bind mask to current item", category, _owner));
                controls.Bind.OnClick.AddListener(new UnityAction(delegate { BindToCurrent(capturedSlot); }));

                controls.Status = e.AddControl(new MakerText(
                    "Status: waiting for character...",
                    category,
                    _owner));

                e.AddControl(new MakerSeparator(category, _owner));
            }

            BodyMaskLayersPlugin.LogDebug(
                "Registered BodyMask Layers controls in all nine stock clothing tabs.");
            RefreshAll(true);
        }

        private void ReloadInterface(object sender, EventArgs eventArgs)
        {
            if (_sessionActive)
            {
                RefreshAll(true);
            }
        }

        private void MakerExiting(object sender, EventArgs eventArgs)
        {
            EndSession();
        }

        private void ToggleLayer(ClothingSlot slot, bool value)
        {
            if (_refreshing || !_sessionActive)
            {
                return;
            }

            if (!MakerAPI.InsideAndLoaded)
            {
                SetMessage(slot, "Maker is still loading.");
                return;
            }

            BodyMaskCharacterController controller = GetController();
            if (controller == null)
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Could not toggle " + ClothingSlotRegistry.GetDisplayName(slot) +
                    " mask: Maker character controller is unavailable.");
                SetMessage(slot, "Character controller unavailable.");
            }
            else if (!controller.SetLayerEnabled(slot, value))
            {
                SetMessage(slot, "Load a mask before enabling it.");
            }
            else
            {
                SetMessage(slot, value ? "Mask enabled." : "Mask disabled.");
            }

            RefreshSlot(slot, false);
        }

        private void LoadPng(ClothingSlot slot)
        {
            int generation = _sessionGeneration;
            BodyMaskLayersPlugin.LogDebug(
                "Opening PNG picker for " + ClothingSlotRegistry.GetDisplayName(slot) + ".");
            OpenFileDialog.Show(delegate(string[] paths)
            {
                if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
                {
                    return;
                }

                byte[] bytes;
                try
                {
                    FileInfo source = new FileInfo(paths[0]);
                    if (!source.Exists)
                    {
                        QueueMessage(generation, slot, "PNG file was not found.");
                        return;
                    }

                    if (source.Length > PortableMaskFormatLimits.MaximumPngBytes)
                    {
                        BodyMaskLayersPlugin.Log.LogWarning(
                            "Rejected mask before reading: file exceeds the supported " +
                            PortableMaskFormatLimits.MaximumPngBytes + "-byte card-data size.");
                        QueueMessage(generation, slot, "PNG is too large to store in card data.");
                        return;
                    }

                    bytes = File.ReadAllBytes(paths[0]);
                }
                catch (Exception exception)
                {
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Could not read mask PNG: " + exception.Message);
                    QueueMessage(generation, slot, "Could not read the PNG. See log.");
                    return;
                }

                QueueMainThread(generation, delegate
                {
                    BodyMaskCharacterController controller = GetController();
                    string result;
                    if (controller == null)
                    {
                        BodyMaskLayersPlugin.Log.LogWarning(
                            "Could not import " + ClothingSlotRegistry.GetDisplayName(slot) +
                            " mask: Maker character controller is unavailable.");
                        SetMessage(slot, "Character controller unavailable.");
                    }
                    else if (!controller.TryImportPng(slot, bytes, out result))
                    {
                        BodyMaskLayersPlugin.Log.LogWarning(
                            "Could not import " + ClothingSlotRegistry.GetDisplayName(slot) +
                            " mask: " + result);
                        SetMessage(
                            slot,
                            result.IndexOf("no bindable clothing item", StringComparison.OrdinalIgnoreCase) >= 0
                                ? "Equip an item before loading a mask."
                                : "Load rejected. See BepInEx log.");
                    }
                    else
                    {
                        SetMessage(
                            slot,
                            result.IndexOf("normalized", StringComparison.OrdinalIgnoreCase) >= 0
                                ? "Mask loaded; palette normalized."
                                : "Mask loaded successfully.");
                    }

                    RefreshSlot(slot, true);
                });
            },
            "Load Body Mask Layer PNG",
            Paths.GameRootPath,
            "PNG image (*.png)|*.png",
            "png",
            OpenFileDialog.SingleFileFlags);
        }

        private void Clear(ClothingSlot slot)
        {
            BodyMaskCharacterController controller = GetController();
            bool hasAutomaticSource = controller != null && controller.HasExternalSource(slot);
            if (controller != null && controller.ClearLayer(slot))
            {
                SetMessage(
                    slot,
                    hasAutomaticSource
                        ? "Mask cleared for this session; compatible data may import again after reload."
                        : "Mask cleared.");
            }
            else
            {
                SetMessage(slot, "No mask to clear.");
            }

            RefreshSlot(slot, true);
        }

        private void BindToCurrent(ClothingSlot slot)
        {
            BodyMaskCharacterController controller = GetController();
            string result;
            if (controller == null)
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Could not bind " + ClothingSlotRegistry.GetDisplayName(slot) +
                    " mask: Maker character controller is unavailable.");
                SetMessage(slot, "Character controller unavailable.");
            }
            else if (!controller.BindLayerToCurrentItem(slot, out result))
            {
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Could not bind " + ClothingSlotRegistry.GetDisplayName(slot) +
                    " mask: " + result);
                SetMessage(slot, "Binding failed. See BepInEx log.");
            }
            else
            {
                SetMessage(slot, "Mask bound to current item.");
            }

            RefreshSlot(slot, false);
        }

        private void ExportPng(ClothingSlot slot)
        {
            BodyMaskCharacterController controller = GetController();
            byte[] bytes = controller == null ? null : controller.ExportOriginalPng(slot);
            if (bytes == null)
            {
                SetMessage(slot, "No mask to export.");
                RefreshSlot(slot, false);
                return;
            }

            int generation = _sessionGeneration;
            OpenFileDialog.OpenSaveFileDialgueFlags flags =
                OpenFileDialog.OpenSaveFileDialgueFlags.OFN_OVERWRITEPROMPT |
                OpenFileDialog.OpenSaveFileDialgueFlags.OFN_NOCHANGEDIR |
                OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER |
                OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES;
            OpenFileDialog.Show(delegate(string[] paths)
            {
                if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
                {
                    return;
                }

                try
                {
                    File.WriteAllBytes(paths[0], bytes);
                    QueueMessage(generation, slot, "Mask exported successfully.");
                }
                catch (Exception exception)
                {
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Could not export mask PNG: " + exception.Message);
                    QueueMessage(generation, slot, "Export failed. See BepInEx log.");
                }
            },
            "Export Body Mask Layer PNG",
            Paths.GameRootPath,
            "PNG image (*.png)|*.png",
            "png",
            flags);
        }

        private void RefreshAll(bool refreshPreviews)
        {
            if (_slotControls == null)
            {
                return;
            }

            BodyMaskCharacterController controller = GetController();
            for (int index = 0; index < _slotControls.Length; index++)
            {
                RefreshSlot((ClothingSlot)index, refreshPreviews, controller);
            }

        }

        private void RefreshSlot(ClothingSlot slot, bool refreshPreview)
        {
            RefreshSlot(slot, refreshPreview, GetController());
        }

        private void RefreshSlot(
            ClothingSlot slot,
            bool refreshPreview,
            BodyMaskCharacterController controller)
        {
            SlotControls controls = GetControls(slot);
            if (controls == null)
            {
                return;
            }

            bool hasLayer = controller != null && controller.HasLayer(slot);
            bool usableLayer = hasLayer && controller.IsLayerUsable(slot);
            bool hasBindableItem = controller != null && controller.HasBindableItem(slot);
            bool enabled = usableLayer && controller.IsLayerEnabled(slot);

            if (!controls.HasLastEnabledValue || controls.LastEnabledValue != enabled)
            {
                _refreshing = true;
                try
                {
                    controls.Enabled.SetValue(enabled, false);
                    controls.LastEnabledValue = enabled;
                    controls.HasLastEnabledValue = true;
                }
                finally
                {
                    _refreshing = false;
                }
            }

            SetInteractable(controls.Load, ref controls.LoadSelectable, hasBindableItem);
            SetInteractable(controls.Enabled, ref controls.EnabledSelectable, usableLayer);
            SetInteractable(controls.Clear, ref controls.ClearSelectable, hasLayer);
            SetInteractable(controls.Export, ref controls.ExportSelectable, hasLayer);
            SetInteractable(controls.Bind, ref controls.BindSelectable, hasLayer);

            if (controls.Status != null)
            {
                string status = controls.ActionMessage != null &&
                                Time.unscaledTime < controls.ActionMessageUntil
                    ? controls.ActionMessage
                    : controller == null
                        ? "Character unavailable."
                        : controller.DescribeLayerForMaker(slot);
                string statusText = "Status: " + status;
                if (!string.Equals(statusText, controls.LastStatusText, StringComparison.Ordinal))
                {
                    controls.Status.Text = statusText;
                    controls.LastStatusText = statusText;
                }
            }

            if (refreshPreview || !string.Equals(
                controls.PreviewHash,
                controller == null ? null : controller.GetLayerPreviewKey(slot),
                StringComparison.Ordinal))
            {
                RefreshPreview(controls, controller);
            }

        }

        private void RefreshPreview(
            SlotControls controls,
            BodyMaskCharacterController controller)
        {
            string previewKey = controller == null
                ? null
                : controller.GetLayerPreviewKey(controls.Slot);
            if (string.Equals(previewKey, controls.PreviewHash, StringComparison.Ordinal))
            {
                return;
            }

            ReleasePreview(controls);
            controls.PreviewHash = previewKey;
            if (string.IsNullOrEmpty(previewKey) || controls.Preview == null)
            {
                return;
            }

            try
            {
                controls.PreviewTexture = CreatePreviewTexture(controller, controls.Slot);
                if (controls.PreviewTexture == null)
                {
                    return;
                }

                controls.Preview.Texture = controls.PreviewTexture;
            }
            catch (Exception exception)
            {
                controls.PreviewHash = null;
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Could not create " + ClothingSlotRegistry.GetDisplayName(controls.Slot) +
                    " mask preview: " + exception.Message);
            }
        }

        private static Texture2D CreatePreviewTexture(
            BodyMaskCharacterController controller,
            ClothingSlot slot)
        {
            Rgba32[] sourcePixels;
            int targetWidth;
            int targetHeight;
            if (controller == null || !controller.TryBuildLayerPreview(
                slot,
                PreviewSize,
                out sourcePixels,
                out targetWidth,
                out targetHeight))
            {
                return null;
            }

            Color32[] targetPixels = new Color32[sourcePixels.Length];
            for (int index = 0; index < sourcePixels.Length; index++)
            {
                Rgba32 pixel = sourcePixels[index];
                targetPixels[index] = new Color32(pixel.R, pixel.G, pixel.B, pixel.A);
            }

            Texture2D preview = new Texture2D(
                targetWidth,
                targetHeight,
                TextureFormat.ARGB32,
                false);
            BodyMaskPerformanceMetrics.RecordNewTextureAllocation();
            preview.name = "BodyMaskLayers_MakerPreview";
            preview.filterMode = FilterMode.Point;
            preview.wrapMode = TextureWrapMode.Clamp;
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.SetPixels32(targetPixels);
            preview.Apply(false, false);
            return preview;
        }

        private void SetMessage(ClothingSlot slot, string message)
        {
            SlotControls controls = GetControls(slot);
            if (controls == null)
            {
                return;
            }

            controls.ActionMessage = message;
            controls.ActionMessageUntil = Time.unscaledTime + ActionMessageSeconds;
            if (controls.Status != null)
            {
                string statusText = "Status: " + message;
                controls.Status.Text = statusText;
                controls.LastStatusText = statusText;
            }
        }

        private void QueueMessage(int generation, ClothingSlot slot, string message)
        {
            QueueMainThread(generation, delegate
            {
                SetMessage(slot, message);
                RefreshSlot(slot, false);
            });
        }

        private void QueueMainThread(int generation, Action action)
        {
            ThreadingHelper.Instance.StartSyncInvoke(delegate
            {
                if (!_sessionActive || generation != _sessionGeneration || !MakerAPI.InsideMaker)
                {
                    BodyMaskLayersPlugin.LogDebug("Discarded a stale Maker file-dialog callback.");
                    return;
                }

                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    BodyMaskLayersPlugin.Log.LogError(
                        "Maker file-dialog callback failed: " + exception);
                }
            });
        }

        private void EndSession()
        {
            _sessionActive = false;
            _sessionGeneration++;
            if (_slotControls != null)
            {
                for (int index = 0; index < _slotControls.Length; index++)
                {
                    ReleasePreview(_slotControls[index]);
                }
            }

            _slotControls = null;
        }

        private static void ReleasePreview(SlotControls controls)
        {
            if (controls == null)
            {
                return;
            }

            if (controls.Preview != null)
            {
                controls.Preview.Texture = null;
            }

            if (controls.PreviewTexture != null)
            {
                UnityEngine.Object.Destroy(controls.PreviewTexture);
                controls.PreviewTexture = null;
            }

            controls.PreviewHash = null;
        }

        private SlotControls GetControls(ClothingSlot slot)
        {
            int index = (int)slot;
            return _slotControls != null && index >= 0 && index < _slotControls.Length
                ? _slotControls[index]
                : null;
        }

        private static void SetInteractable(
            BaseGuiEntry control,
            ref Selectable selectable,
            bool interactable)
        {
            if (selectable == null && control != null && control.ControlObject != null)
            {
                selectable = control.ControlObject.GetComponentInChildren<Selectable>();
            }

            if (selectable != null && selectable.interactable != interactable)
            {
                selectable.interactable = interactable;
            }
        }

        private static MakerCategory GetMakerCategory(ClothingSlot slot)
        {
            switch (slot)
            {
                case ClothingSlot.Top:
                    return MakerConstants.Clothes.Top;
                case ClothingSlot.Bottom:
                    return MakerConstants.Clothes.Bottom;
                case ClothingSlot.Bra:
                    return MakerConstants.Clothes.Bra;
                case ClothingSlot.Shorts:
                    return MakerConstants.Clothes.Shorts;
                case ClothingSlot.Gloves:
                    return MakerConstants.Clothes.Gloves;
                case ClothingSlot.Pantyhose:
                    return MakerConstants.Clothes.Panst;
                case ClothingSlot.Socks:
                    return MakerConstants.Clothes.Socks;
                case ClothingSlot.IndoorShoes:
                    return MakerConstants.Clothes.InnerShoes;
                case ClothingSlot.OutdoorShoes:
                    return MakerConstants.Clothes.OuterShoes;
                default:
                    throw new ArgumentOutOfRangeException("slot");
            }
        }

        private static BodyMaskCharacterController GetController()
        {
            if (!MakerAPI.InsideMaker)
            {
                return null;
            }

            ChaControl character = MakerAPI.GetCharacterControl();
            return character == null ? null : character.GetComponent<BodyMaskCharacterController>();
        }

        private sealed class SlotControls
        {
            public SlotControls(ClothingSlot slot)
            {
                Slot = slot;
            }

            public readonly ClothingSlot Slot;
            public MakerImage Preview;
            public MakerToggle Enabled;
            public MakerButton Load;
            public MakerButton Clear;
            public MakerButton Export;
            public MakerButton Bind;
            public MakerText Status;
            public Texture2D PreviewTexture;
            public string PreviewHash;
            public string ActionMessage;
            public float ActionMessageUntil;
            public string LastStatusText;
            public bool LastEnabledValue;
            public bool HasLastEnabledValue;
            public Selectable LoadSelectable;
            public Selectable EnabledSelectable;
            public Selectable ClearSelectable;
            public Selectable ExportSelectable;
            public Selectable BindSelectable;
        }
    }
}
