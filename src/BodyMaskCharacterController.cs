using System;
using System.Collections.Generic;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    // Top writes the body-mask scalars during the game's LateUpdate pass.
    // Compose afterwards so visibility changes reach the renderer in the same frame.
    [DefaultExecutionOrder(10000)]
    public sealed class BodyMaskCharacterController : CharaCustomFunctionController,
        IRuntimeDirtySink,
        IMaterialTargetDirtySink,
        ICharacterMaskMutationSink
    {
        private const float FallbackPollIntervalSeconds = 0.5f;
        private readonly NativeMaskLayerStore _nativeLayers = new NativeMaskLayerStore();
        private readonly ExternalPortableLayerConverter _externalConverter =
            new ExternalPortableLayerConverter();
        private readonly CharacterExternalMaskSession _externalSession =
            new CharacterExternalMaskSession();
        private readonly MaskCompositionResources _compositionResources =
            new MaskCompositionResources();
        private CharacterClothingRuntime _runtimeState;
        private CharacterBodyMaskMaterialTarget _materialTarget;
        private CharacterMaskCompositionEngine _compositionEngine;
        private CharacterNativeMaskService _nativeMaskService;
        private CharacterExternalMaskCoordinator _externalCoordinator;
        private CharacterMaskDiagnostics _maskDiagnostics;
        private CharacterMaskPersistence _maskPersistence;
        private float _nextErrorLogTime;
        private string _lastUpdateError;
        private float _nextFallbackPollTime;

        public bool IsCompositeActive
        {
            get { return _materialTarget.IsCompositeActive; }
        }

        public bool IsInternalMaterialWrite
        {
            get { return _materialTarget.IsInternalMaterialWrite; }
        }

        public string TargetDescription
        {
            get
            {
                return _materialTarget.TargetDescription;
            }
        }

        public string OutputDescription
        {
            get
            {
                return _compositionResources.OutputTexture == null
                    ? "none"
                    : string.Format(
                        "{0}x{1}, hidden={2}",
                        _compositionResources.OutputWidth,
                        _compositionResources.OutputHeight,
                        _compositionResources.HiddenPixelCount);
            }
        }

        public string BaseTextureDescription
        {
            get
            {
                return _materialTarget.GetBaseTextureDescription(ChaControl);
            }
        }

        public string LastCompositionReason
        {
            get { return _compositionEngine.LastCompositionReason; }
        }

        string IRuntimeDirtySink.LastRuntimeDirtyReason
        {
            get { return _compositionEngine.LastDirtyReason; }
        }

        void IRuntimeDirtySink.RequestRuntimeDirty(
            string reason,
            bool baseContentMayHaveChanged)
        {
            RequestDirty(reason, baseContentMayHaveChanged);
        }

        void IMaterialTargetDirtySink.RequestMaterialDirty(
            string reason,
            bool baseContentMayHaveChanged)
        {
            RequestDirty(reason, baseContentMayHaveChanged);
        }

        void ICharacterMaskMutationSink.RequestMaskDirty(
            string reason,
            bool baseContentMayHaveChanged)
        {
            RequestDirty(reason, baseContentMayHaveChanged);
        }

        void ICharacterMaskMutationSink.PersistMaskData()
        {
            _maskPersistence.SaveCurrent();
        }

        void ICharacterMaskMutationSink.RequestImmediateRuntimeRefresh()
        {
            _nextFallbackPollTime = 0f;
        }

        public int CharacterInstanceId
        {
            get { return ChaControl == null ? 0 : ChaControl.GetInstanceID(); }
        }

        public int CoordinateType
        {
            get
            {
                return ChaControl == null || ChaControl.fileStatus == null
                    ? -1
                    : ChaControl.fileStatus.coordinateType;
            }
        }

        public string MaterialContractDescription
        {
            get
            {
                return _materialTarget.MaterialContractDescription;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _runtimeState = new CharacterClothingRuntime(this);
            NativeMaskDecodePipeline decodePipeline = new NativeMaskDecodePipeline(
                _nativeLayers,
                new MaskPngDecodeService(),
                new PortableMaskPngValidator());
            _nativeMaskService = new CharacterNativeMaskService(
                _nativeLayers,
                decodePipeline,
                _externalConverter,
                _runtimeState,
                _externalSession,
                this);
            _externalCoordinator = new CharacterExternalMaskCoordinator(
                _externalSession,
                _nativeLayers,
                _nativeMaskService,
                _runtimeState,
                this);
            _materialTarget = new CharacterBodyMaskMaterialTarget(
                this,
                this,
                _compositionResources);
            _compositionEngine = new CharacterMaskCompositionEngine(
                _nativeLayers,
                _externalSession,
                _runtimeState,
                _materialTarget,
                _compositionResources);
            _maskDiagnostics = new CharacterMaskDiagnostics(
                _nativeLayers,
                _nativeMaskService,
                _externalCoordinator,
                _runtimeState,
                _compositionEngine);

            OutfitPayloadRouter<PluginData> payloadRouter =
                new OutfitPayloadRouter<PluginData>(
                delegate { return CoordinateType; },
                IsCoordinateIndexValid,
                delegate(int coordinateId) { return GetClothesExtData(coordinateId); },
                delegate(PluginData data) { SetClothesExtData(data); },
                delegate(PluginData data, int coordinateId)
                {
                    SetClothesExtData(data, coordinateId);
                });
            _maskPersistence = new CharacterMaskPersistence(
                payloadRouter,
                _nativeMaskService);
            BodyMaskControllerRegistry.RegisterController(this);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            _materialTarget.Refresh(ChaControl, true);
            _maskPersistence.CaptureCurrentClothes(ChaControl);
            _runtimeState.Reset();
            _externalSession.MarkAllDirty();
            _nextFallbackPollTime = 0f;

            if (maintainState || ShouldPreserveMakerClothes(currentGameMode))
            {
                RequestDirty("character reload preserving clothing data", true);
                return;
            }

            _maskPersistence.LoadCurrent(
                delegate { return GetClothesExtData(); },
                "current outfit");
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            _maskPersistence.SaveCurrent();
        }

        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            _maskPersistence.SaveCoordinate(coordinate);
        }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
        {
            CoordinateLoadFlags flags = KoikatuAPI.GetCurrentGameMode() == GameMode.Maker
                ? MakerAPI.GetCoordinateLoadFlags()
                : null;
            if (maintainState || (flags != null && !flags.Clothes))
            {
                return;
            }

            _maskPersistence.LoadCoordinate(coordinate, "coordinate");
            _maskPersistence.SaveCurrent();
        }

        private void LateUpdate()
        {
            try
            {
                ApplyPendingConfigurationChange();
                float now = Time.unscaledTime;
                bool fallbackPollDue = now >= _nextFallbackPollTime;
                if (fallbackPollDue)
                {
                    _nextFallbackPollTime = now + FallbackPollIntervalSeconds;
                    BodyMaskPerformanceMetrics.RecordFallbackPoll();
                    _materialTarget.Refresh(ChaControl, false);
                    DetectCoordinateReplacement();
                    _runtimeState.Poll(ChaControl, _nativeLayers, _externalSession);
                    _externalCoordinator.Refresh(ChaControl);
                    _materialTarget.DetectExternalReplacement();
                }

                if (_lastUpdateError == null || fallbackPollDue)
                {
                    _compositionEngine.TryCompose(
                        ChaControl,
                        Time.frameCount,
                        GetInstanceID());
                    _lastUpdateError = null;
                }
            }
            catch (Exception exception)
            {
                string message = exception.Message ?? exception.GetType().FullName;
                float now = Time.unscaledTime;
                if (!string.Equals(message, _lastUpdateError, StringComparison.Ordinal) ||
                    now >= _nextErrorLogTime)
                {
                    _lastUpdateError = message;
                    _nextErrorLogTime = now + BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
                    BodyMaskLayersPlugin.Log.LogError("Character update failed: " + exception);
                }

                _materialTarget.Restore();
            }
        }

        protected override void OnDestroy()
        {
            _materialTarget.Restore();
            _materialTarget.Unregister();
            BodyMaskControllerRegistry.UnregisterController(this);

            _compositionEngine.ReleaseResources();

            base.OnDestroy();
        }

        public static bool TryGetForMaterial(Material material, out BodyMaskCharacterController controller)
        {
            controller = null;
            if (material == null)
            {
                return false;
            }

            return BodyMaskControllerRegistry.TryGetByMaterial(material, out controller);
        }

        public static BodyMaskCharacterController[] GetControllersSnapshot()
        {
            return BodyMaskControllerRegistry.SnapshotControllers();
        }

        public static void NotifyCharacterDirty(ChaControl character, bool baseContentMayHaveChanged)
        {
            if (character == null)
            {
                return;
            }

            BodyMaskCharacterController controller = character.GetComponent<BodyMaskCharacterController>();
            if (controller != null)
            {
                controller.RequestDirty(
                    baseContentMayHaveChanged
                        ? "game clothing/material refresh"
                        : "game clothing state change",
                    baseContentMayHaveChanged);
            }
        }

        public static void NotifyClothingStateChanged(ChaControl character, int clothingIndex)
        {
            if (character == null || clothingIndex < 0 ||
                clothingIndex >= ClothingSlotRegistry.SlotCount)
            {
                return;
            }

            BodyMaskCharacterController controller = character.GetComponent<BodyMaskCharacterController>();
            if (controller != null)
            {
                if (controller._runtimeState.ShouldDirtyForStateHook(
                        character,
                        clothingIndex,
                        controller._nativeLayers,
                        controller._externalSession))
                {
                    controller.RequestDirty("game clothing state change", false);
                }

                controller._nextFallbackPollTime = 0f;
            }
        }

        public static void NotifyClothingItemChanged(ChaControl character, int clothingIndex)
        {
            if (character == null || clothingIndex < 0 ||
                clothingIndex >= ClothingSlotRegistry.SlotCount)
            {
                return;
            }

            BodyMaskCharacterController controller = character.GetComponent<BodyMaskCharacterController>();
            if (controller == null)
            {
                return;
            }

            controller._runtimeState.InvalidateItem((ClothingSlot)clothingIndex);
            controller._externalSession.MarkSlotDirty((ClothingSlot)clothingIndex);
            controller._nextFallbackPollTime = 0f;
            controller.RequestDirty("game clothing item/option change", true);
        }

        public static void NotifyExternalIndexChanged()
        {
            BodyMaskControllerRegistry.ForEachController(delegate(BodyMaskCharacterController controller)
            {
                if (controller == null)
                {
                    return;
                }

                controller._externalSession.MarkAllDirty();
                controller._nextFallbackPollTime = 0f;
                controller.RequestDirty("external mask index change", false);
            });
        }

        public static void NotifyConfigurationChanged(bool requiresDecode)
        {
            BodyMaskControllerRegistry.ForEachController(delegate(BodyMaskCharacterController controller)
            {
                if (controller == null)
                {
                    return;
                }

                if (requiresDecode)
                {
                    controller._nativeMaskService.MarkDecodeConfigurationDirty();
                }
                controller.RequestDirty(
                    requiresDecode
                        ? "mask decoding configuration change"
                        : "runtime configuration change",
                    false);
            });
        }

        public void InterceptExternalTexture(Texture incoming, ref Texture effectiveValue)
        {
            _materialTarget.InterceptExternalTexture(incoming, ref effectiveValue);
        }

        public void InterceptExternalFloat(int propertyId, float incoming, ref float effectiveValue)
        {
            _materialTarget.InterceptExternalFloat(
                propertyId,
                incoming,
                ref effectiveValue);
        }

        public void FinalizeInternalTextureWrite(ref Texture effectiveValue)
        {
            _materialTarget.FinalizeInternalTextureWrite(ref effectiveValue);
        }

        public void FinalizeInternalFloatWrite(int propertyId, ref float effectiveValue)
        {
            _materialTarget.FinalizeInternalFloatWrite(propertyId, ref effectiveValue);
        }

        public bool TryImportPng(ClothingSlot slot, byte[] pngBytes, out string result)
        {
            return _nativeMaskService.TryImportPng(
                ChaControl,
                slot,
                pngBytes,
                out result);
        }

        public bool ClearLayer(ClothingSlot slot)
        {
            return _nativeMaskService.ClearLayer(slot);
        }

        public bool SetLayerEnabled(ClothingSlot slot, bool enabled)
        {
            return _nativeMaskService.SetLayerEnabled(slot, enabled);
        }

        public bool BindLayerToCurrentItem(ClothingSlot slot, out string result)
        {
            return _nativeMaskService.BindLayerToCurrentItem(
                ChaControl,
                slot,
                out result);
        }

        public byte[] ExportOriginalPng(ClothingSlot slot)
        {
            return _nativeMaskService.ExportOriginalPng(slot);
        }

        public bool IsLayerEnabled(ClothingSlot slot)
        {
            return _nativeMaskService.IsLayerEnabled(slot);
        }

        public bool HasLayer(ClothingSlot slot)
        {
            return _nativeMaskService.HasLayer(slot);
        }

        public bool IsLayerUsable(ClothingSlot slot)
        {
            return _nativeMaskService.IsLayerUsable(slot);
        }

        public bool HasBindableItem(ClothingSlot slot)
        {
            return _nativeMaskService.HasBindableItem(ChaControl, slot);
        }

        public string GetLayerHash(ClothingSlot slot)
        {
            return _nativeMaskService.GetLayerHash(slot);
        }

        public string GetLayerPreviewKey(ClothingSlot slot)
        {
            return _nativeMaskService.GetLayerPreviewKey(slot);
        }

        public bool TryBuildLayerPreview(
            ClothingSlot slot,
            int maximumDimension,
            out Rgba32[] pixels,
            out int width,
            out int height)
        {
            return _nativeMaskService.TryBuildLayerPreview(
                slot,
                maximumDimension,
                out pixels,
                out width,
                out height);
        }

        public bool HasExternalSource(ClothingSlot slot)
        {
            return _externalCoordinator.HasSource(slot);
        }

        public string DescribeExternalDetails(ClothingSlot slot)
        {
            return _externalCoordinator.DescribeDetails(slot);
        }

        public string DescribeLayerForMaker(ClothingSlot slot)
        {
            return _maskDiagnostics.DescribeForMaker(ChaControl, slot);
        }

        public string DescribeLayer(ClothingSlot slot)
        {
            return _maskDiagnostics.Describe(ChaControl, slot);
        }

        public Dictionary<ClothingSlot, ClothingMaskLayerData> SnapshotLayers()
        {
            return _nativeMaskService.SnapshotLayers();
        }

        public void ApplyPartialCoordinateLayers(
            Dictionary<ClothingSlot, ClothingMaskLayerData> source,
            bool[] selectedSlots)
        {
            _nativeMaskService.ApplyPartialCoordinateLayers(source, selectedSlots);
        }

        private void DetectCoordinateReplacement()
        {
            ChaFileClothes clothes = ChaControl.nowCoordinate == null ? null : ChaControl.nowCoordinate.clothes;
            if (!_maskPersistence.TryCaptureCoordinateReplacement(clothes))
            {
                return;
            }

            _runtimeState.Reset();
            _externalSession.MarkAllDirty();
            _maskPersistence.LoadCurrent(
                delegate { return CoordinateDataHandler.ReadFromClothes(clothes); },
                "coordinate switch");
        }

        private void ApplyPendingConfigurationChange()
        {
            _nativeMaskService.ApplyPendingDecodeConfiguration();
        }

        private void RequestDirty(string reason, bool baseContentMayHaveChanged)
        {
            _compositionEngine.RequestDirty(reason, baseContentMayHaveChanged);
        }

        private bool ShouldPreserveMakerClothes(GameMode mode)
        {
            if (mode != GameMode.Maker)
            {
                return false;
            }

            CharacterLoadFlags flags = MakerAPI.GetCharacterLoadFlags();
            return flags != null && !flags.Clothes;
        }

        private bool IsCoordinateIndexValid(int coordinateId)
        {
            return coordinateId >= 0 &&
                   ChaControl != null &&
                   ChaControl.chaFile != null &&
                   ChaControl.chaFile.coordinate != null &&
                   coordinateId < ChaControl.chaFile.coordinate.Length;
        }

    }
}
