using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class BodyMaskCharacterController : CharaCustomFunctionController
    {
        private static readonly object RegistryLock = new object();
        private static readonly Dictionary<int, BodyMaskCharacterController> MaterialTargets =
            new Dictionary<int, BodyMaskCharacterController>();
        private static readonly List<BodyMaskCharacterController> Controllers =
            new List<BodyMaskCharacterController>();

        private readonly ClothingMaskLayerData[] _layers =
            new ClothingMaskLayerData[ClothingSlotRegistry.SlotCount];
        private readonly SemanticMask[] _semanticMasks =
            new SemanticMask[ClothingSlotRegistry.SlotCount];
        private readonly MaskColorStatistics[] _statistics =
            new MaskColorStatistics[ClothingSlotRegistry.SlotCount];
        private readonly int[] _semanticRevisions =
            new int[ClothingSlotRegistry.SlotCount];
        private readonly byte[] _observedRawStates =
            new byte[ClothingSlotRegistry.SlotCount];
        private readonly int[] _observedItemIds =
            new int[ClothingSlotRegistry.SlotCount];
        private readonly ClothingItemIdentity[] _currentIdentities =
            new ClothingItemIdentity[ClothingSlotRegistry.SlotCount];
        private readonly GarmentState[] _lastKnownStates =
            new GarmentState[ClothingSlotRegistry.SlotCount];
        private readonly bool[] _warnedUnknownStates =
            new bool[ClothingSlotRegistry.SlotCount];
        private readonly bool[] _activeLayers =
            new bool[ClothingSlotRegistry.SlotCount];
        private readonly GarmentState[] _compositionStates =
            new GarmentState[ClothingSlotRegistry.SlotCount];

        private Material _targetMaterial;
        private Shader _targetShader;
        private int _targetMaterialId;
        private Texture _baseTexture;
        private float _baseAlphaA = 1f;
        private float _baseAlphaB = 1f;
        private Texture2D _outputTexture;
        private bool[] _hiddenPixels;
        private Rgba32[] _outputPixels;
        private Color32[] _outputColors;
        private Texture _cachedBasePixelSource;
        private Rgba32[] _cachedBasePixels;
        private int _cachedBaseWidth;
        private int _cachedBaseHeight;
        private bool _basePixelsDirty = true;
        private bool _dirty = true;
        private bool _internalMaterialWrite;
        private bool _applyingCompositeWrite;
        private bool _compositeActive;
        private bool _warnedUnsupportedShader;
        private int _outputWidth;
        private int _outputHeight;
        private int _hiddenPixelCount;
        private int _observedShoesType = int.MinValue;
        private int _observedAvailabilityMask = -1;
        private int _observedStructuralFlags = -1;
        private ChaFileClothes _observedClothes;
        private bool _decodeConfigurationDirty;
        private float _nextStateLogTime;
        private float _nextCompositionLogTime;
        private float _nextWarningLogTime;
        private float _nextErrorLogTime;
        private string _lastUpdateError;
        private int _warnedOversizedBaseId;
        private string _lastDirtyReason = "initialization";
        private string _lastCompositionReason = "not composed yet";

        public bool IsCompositeActive
        {
            get { return _compositeActive; }
        }

        public bool IsInternalMaterialWrite
        {
            get { return _internalMaterialWrite; }
        }

        public string TargetDescription
        {
            get
            {
                if (_targetMaterial == null)
                {
                    return "unresolved";
                }

                string shader = _targetMaterial.shader == null ? "<no shader>" : _targetMaterial.shader.name;
                return _targetMaterial.name + " / " + shader;
            }
        }

        public string OutputDescription
        {
            get
            {
                return _outputTexture == null
                    ? "none"
                    : string.Format("{0}x{1}, hidden={2}", _outputWidth, _outputHeight, _hiddenPixelCount);
            }
        }

        public string BaseTextureDescription
        {
            get
            {
                Texture texture = _baseTexture == _outputTexture
                    ? BodyMaskTargetResolver.GetVanillaTopMask(ChaControl)
                    : _baseTexture;
                return texture == null
                    ? "none"
                    : string.Format("{0}, {1}x{2}, {3}", texture.name, texture.width, texture.height,
                        texture.GetType().Name);
            }
        }

        public string LastCompositionReason
        {
            get { return _lastCompositionReason; }
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
                if (_targetMaterial == null)
                {
                    return "material unresolved";
                }

                Texture actual = BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial)
                    ? _targetMaterial.GetTexture(ChaShader._AlphaMask)
                    : null;
                float actualA = BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial)
                    ? _targetMaterial.GetFloat(ChaShader._alpha_a)
                    : float.NaN;
                float actualB = BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial)
                    ? _targetMaterial.GetFloat(ChaShader._alpha_b)
                    : float.NaN;
                return string.Format(
                    "properties=_AlphaMask/_alpha_a/_alpha_b, actualTexture={0}, actualFloats=({1},{2}), " +
                    "capturedUpstreamFloats=({3},{4}), managed=R/G, preserved=B/A",
                    actual == null ? "none" : actual.name,
                    actualA,
                    actualB,
                    _baseAlphaA,
                    _baseAlphaB);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
            {
                _observedRawStates[i] = byte.MaxValue;
                _observedItemIds[i] = int.MinValue;
                _lastKnownStates[i] = GarmentState.Unknown;
            }

            lock (RegistryLock)
            {
                Controllers.Add(this);
            }
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            RefreshTarget(true);
            _observedClothes = ChaControl.nowCoordinate == null ? null : ChaControl.nowCoordinate.clothes;
            ResetObservedRuntimeValues();

            if (maintainState || ShouldPreserveMakerClothes(currentGameMode))
            {
                _dirty = true;
                _lastDirtyReason = "character reload preserving clothing data";
                return;
            }

            LoadPluginData(GetClothesExtData(), "current outfit");
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            PersistToCurrentOutfit();
        }

        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            if (coordinate != null)
            {
                CoordinateDataHandler.WriteToClothes(coordinate.clothes, EnumerateLayers());
            }
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

            LoadPluginData(
                coordinate == null ? null : CoordinateDataHandler.ReadFromClothes(coordinate.clothes),
                "coordinate");
            PersistToCurrentOutfit();
        }

        protected override void Update()
        {
            try
            {
                RefreshTarget(false);
                DetectCoordinateReplacement();
                ApplyPendingConfigurationChange();
                PollClothingStateAndItems();
                DetectExternalMaterialReplacement();
                if (_dirty)
                {
                    RebuildComposite();
                }
            }
            catch (Exception exception)
            {
                string message = exception.Message;
                float now = Time.unscaledTime;
                if (!string.Equals(message, _lastUpdateError, StringComparison.Ordinal) ||
                    now >= _nextErrorLogTime)
                {
                    _lastUpdateError = message;
                    _nextErrorLogTime = now + BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
                    BodyMaskLayersPlugin.Log.LogError("Character update failed: " + exception);
                }

                RestoreBaseMaterial();
                _dirty = false;
            }
            finally
            {
                base.Update();
            }
        }

        protected override void OnDestroy()
        {
            RestoreBaseMaterial();
            UnregisterTarget();
            lock (RegistryLock)
            {
                Controllers.Remove(this);
            }

            ReleaseCompositionResources();

            base.OnDestroy();
        }

        public static bool TryGetForMaterial(Material material, out BodyMaskCharacterController controller)
        {
            controller = null;
            if (material == null)
            {
                return false;
            }

            lock (RegistryLock)
            {
                return MaterialTargets.TryGetValue(material.GetInstanceID(), out controller) &&
                       controller != null;
            }
        }

        public static BodyMaskCharacterController[] GetControllersSnapshot()
        {
            lock (RegistryLock)
            {
                return Controllers.ToArray();
            }
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
                controller._dirty = true;
                controller._lastDirtyReason = baseContentMayHaveChanged
                    ? "game clothing/material refresh"
                    : "game clothing state change";
                if (baseContentMayHaveChanged)
                {
                    controller._basePixelsDirty = true;
                }
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
            if (controller != null &&
                controller._observedRawStates[clothingIndex] != controller.GetRawState(clothingIndex))
            {
                controller._dirty = true;
                controller._lastDirtyReason = "game clothing state change";
            }
        }

        public static void NotifyConfigurationChanged(bool requiresDecode)
        {
            lock (RegistryLock)
            {
                for (int i = 0; i < Controllers.Count; i++)
                {
                    BodyMaskCharacterController controller = Controllers[i];
                    if (controller == null)
                    {
                        continue;
                    }

                    controller._decodeConfigurationDirty |= requiresDecode;
                    controller._dirty = true;
                    controller._lastDirtyReason = requiresDecode
                        ? "mask decoding configuration change"
                        : "runtime configuration change";
                }
            }
        }

        public void InterceptExternalTexture(Texture incoming, ref Texture effectiveValue)
        {
            if (_internalMaterialWrite || incoming == _outputTexture)
            {
                return;
            }

            _baseTexture = incoming;
            _basePixelsDirty = true;
            _dirty = true;
            _lastDirtyReason = "upstream body-mask texture write";
            if (_compositeActive && _outputTexture != null)
            {
                effectiveValue = _outputTexture;
            }
        }

        public void InterceptExternalFloat(int propertyId, float incoming, ref float effectiveValue)
        {
            if (_internalMaterialWrite)
            {
                return;
            }

            if (propertyId == ChaShader._alpha_a)
            {
                _baseAlphaA = incoming;
            }
            else if (propertyId == ChaShader._alpha_b)
            {
                _baseAlphaB = incoming;
            }
            else
            {
                return;
            }

            _dirty = true;
            _lastDirtyReason = "upstream body-mask scalar write";
            if (_compositeActive)
            {
                effectiveValue = 1f;
            }
        }

        public void FinalizeInternalTextureWrite(ref Texture effectiveValue)
        {
            if (_internalMaterialWrite && _applyingCompositeWrite && _outputTexture != null)
            {
                effectiveValue = _outputTexture;
            }
        }

        public void FinalizeInternalFloatWrite(int propertyId, ref float effectiveValue)
        {
            if (_internalMaterialWrite && _applyingCompositeWrite &&
                (propertyId == ChaShader._alpha_a || propertyId == ChaShader._alpha_b))
            {
                effectiveValue = 1f;
            }
        }

        public bool TryImportPng(ClothingSlot slot, byte[] pngBytes, out string result)
        {
            result = null;
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                result = "Invalid clothing slot.";
                return false;
            }

            ClothingItemIdentity identity = ClothingItemIdentityResolver.Resolve(ChaControl, slot);
            if (!identity.HasItem)
            {
                result = "The selected slot has no bindable clothing item.";
                return false;
            }

            if (pngBytes == null)
            {
                result = "Layer or PNG bytes are missing.";
                return false;
            }

            string hash = HashUtility.Sha256(pngBytes);
            SemanticMask semantic = null;
            MaskColorStatistics statistics = null;
            MaskValidationResult validation = null;
            int normalizedPixelCount = 0;
            bool reusedDecode = !_decodeConfigurationDirty &&
                                TryReuseDecodedLayer(hash, out semantic, out statistics);
            string error = "Layer or PNG bytes are missing.";
            if (reusedDecode)
            {
                PluginConfig settings = BodyMaskLayersPlugin.Settings;
                validation = PngMaskValidator.Validate(
                    pngBytes,
                    settings.GetMinimumResolution(),
                    settings.GetMaximumResolution(),
                    settings.GetMaximumPngBytes());
                if (!validation.IsValid)
                {
                    reusedDecode = false;
                    semantic = null;
                    statistics = null;
                    error = validation.Message;
                }
            }

            if (!reusedDecode && (semantic == null || statistics == null) &&
                !TryDecodePng(
                    pngBytes,
                    out semantic,
                    out statistics,
                    out validation,
                    out normalizedPixelCount,
                    out error))
            {
                WarnAboutBluePixels(slot, statistics, false);
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Rejected " + ClothingSlotRegistry.GetDisplayName(slot) +
                    " mask import: " + error);
                result = error;
                return false;
            }

            WarnAboutBluePixels(slot, statistics, true);

            int index = (int)slot;
            ClothingMaskLayerData layer = new ClothingMaskLayerData
            {
                Slot = slot,
                Enabled = true,
                OriginalPngBytes = (byte[])pngBytes.Clone(),
                Width = validation.Width,
                Height = validation.Height,
                Hash = hash,
                BoundItemIdentity = identity,
                ColorFormatVersion = 1,
                CreatedWithPluginVersion = BodyMaskLayersPlugin.PluginVersion,
                LastValidationResult = reusedDecode
                    ? "Valid using cached identical decode: " + statistics
                    : normalizedPixelCount == 0
                    ? "Valid: " + statistics
                    : "Valid after palette normalization (" + normalizedPixelCount +
                      " pixels): " + statistics
            };
            _layers[index] = layer;
            SetDecodedLayer(index, semantic, statistics);
            _currentIdentities[index] = identity;
            _observedItemIds[index] = identity.LocalItemId;
            _dirty = true;
            _lastDirtyReason = "PNG imported for " + ClothingSlotRegistry.GetDisplayName(slot);
            PersistToCurrentOutfit();
            result = reusedDecode
                ? string.Format("Loaded {0}x{1} mask using cached decode.", validation.Width, validation.Height)
                : normalizedPixelCount == 0
                ? string.Format("Loaded {0}x{1} mask.", validation.Width, validation.Height)
                : string.Format(
                    "Loaded {0}x{1} mask; normalized {2} palette/edge pixels.",
                    validation.Width,
                    validation.Height,
                    normalizedPixelCount);
            BodyMaskLayersPlugin.Log.LogInfo(
                "Loaded " + ClothingSlotRegistry.GetDisplayName(slot) + " mask: " +
                result + " " + statistics + "; SHA-256 " + layer.Hash.Substring(0, 12) + "...");
            return true;
        }

        private bool TryReuseDecodedLayer(
            string hash,
            out SemanticMask semantic,
            out MaskColorStatistics statistics)
        {
            semantic = null;
            statistics = null;
            if (string.IsNullOrEmpty(hash))
            {
                return false;
            }

            for (int index = 0; index < _layers.Length; index++)
            {
                ClothingMaskLayerData layer = _layers[index];
                if (layer == null || _semanticMasks[index] == null || _statistics[index] == null ||
                    !string.Equals(layer.Hash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                semantic = _semanticMasks[index];
                statistics = _statistics[index].Clone();
                return true;
            }

            return false;
        }

        private void SetDecodedLayer(
            int index,
            SemanticMask semantic,
            MaskColorStatistics statistics)
        {
            _semanticMasks[index] = semantic;
            _statistics[index] = statistics;
            unchecked
            {
                _semanticRevisions[index]++;
            }
        }

        public bool ClearLayer(ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot) || _layers[index] == null)
            {
                return false;
            }

            _layers[index] = null;
            SetDecodedLayer(index, null, null);
            _dirty = true;
            _lastDirtyReason = "mask cleared for " + ClothingSlotRegistry.GetDisplayName(slot);
            PersistToCurrentOutfit();
            return true;
        }

        public bool SetLayerEnabled(ClothingSlot slot, bool enabled)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot) || _layers[index] == null)
            {
                return false;
            }

            _layers[index].Enabled = enabled;
            _dirty = true;
            _lastDirtyReason = "layer enabled state changed for " +
                               ClothingSlotRegistry.GetDisplayName(slot);
            PersistToCurrentOutfit();
            return true;
        }

        public bool BindLayerToCurrentItem(ClothingSlot slot, out string result)
        {
            result = null;
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot) || _layers[index] == null)
            {
                result = "No mask is loaded in this slot.";
                return false;
            }

            ClothingItemIdentity identity = ClothingItemIdentityResolver.Resolve(ChaControl, slot);
            if (!identity.HasItem)
            {
                result = "The selected slot has no bindable clothing item.";
                return false;
            }

            _layers[index].BoundItemIdentity = identity;
            _currentIdentities[index] = identity;
            _observedItemIds[index] = identity.LocalItemId;
            _dirty = true;
            _lastDirtyReason = "item binding changed for " + ClothingSlotRegistry.GetDisplayName(slot);
            PersistToCurrentOutfit();
            result = "Bound to " + identity;
            return true;
        }

        public byte[] ExportOriginalPng(ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot) || _layers[index] == null ||
                _layers[index].OriginalPngBytes == null)
            {
                return null;
            }

            return (byte[])_layers[index].OriginalPngBytes.Clone();
        }

        public bool IsLayerEnabled(ClothingSlot slot)
        {
            int index = (int)slot;
            return ClothingSlotRegistry.IsValid(slot) &&
                   _layers[index] != null &&
                   _layers[index].Enabled;
        }

        public bool HasLayer(ClothingSlot slot)
        {
            int index = (int)slot;
            return ClothingSlotRegistry.IsValid(slot) && _layers[index] != null;
        }

        public bool IsLayerUsable(ClothingSlot slot)
        {
            int index = (int)slot;
            return ClothingSlotRegistry.IsValid(slot) &&
                   _layers[index] != null &&
                   _semanticMasks[index] != null;
        }

        public bool HasBindableItem(ClothingSlot slot)
        {
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return false;
            }

            ClothingItemIdentity identity = GetCurrentIdentity(slot);
            return identity != null && identity.HasItem;
        }

        public string GetLayerHash(ClothingSlot slot)
        {
            int index = (int)slot;
            return ClothingSlotRegistry.IsValid(slot) && _layers[index] != null
                ? _layers[index].Hash
                : null;
        }

        public string GetLayerPreviewKey(ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot) || _layers[index] == null)
            {
                return null;
            }

            return (_layers[index].Hash ?? "no-hash") + ":" + _semanticRevisions[index];
        }

        public bool TryBuildLayerPreview(
            ClothingSlot slot,
            int maximumDimension,
            out Rgba32[] pixels,
            out int width,
            out int height)
        {
            pixels = null;
            width = 0;
            height = 0;
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot) || _semanticMasks[index] == null)
            {
                return false;
            }

            pixels = MaskPreviewBuilder.Build(
                _semanticMasks[index],
                maximumDimension,
                out width,
                out height);
            return true;
        }

        public string DescribeLayerForMaker(ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return "Invalid clothing slot.";
            }

            ClothingMaskLayerData layer = _layers[index];
            if (layer == null)
            {
                return HasBindableItem(slot)
                    ? "No mask loaded."
                    : "Equip an item before loading a mask.";
            }

            if (_semanticMasks[index] == null)
            {
                return "Stored mask is invalid; see log.";
            }

            if (!layer.Enabled)
            {
                return string.Format("{0}x{1} mask - disabled.", layer.Width, layer.Height);
            }

            ClothingItemIdentity current = GetCurrentIdentity(slot);
            bool bindingMatches = layer.BoundItemIdentity != null &&
                                  layer.BoundItemIdentity.Matches(
                                      current,
                                      BodyMaskLayersPlugin.Settings.MaskBindingMode.Value);
            if (!bindingMatches)
            {
                return string.Format("{0}x{1} mask - current item does not match.", layer.Width, layer.Height);
            }

            GarmentState state = ResolveStateForLayer(index);
            return string.Format(
                "{0}x{1} mask - {2} ({3}).",
                layer.Width,
                layer.Height,
                IsLayerActive(index) ? "active" : "ready",
                state);
        }

        public string DescribeLayer(ClothingSlot slot)
        {
            int index = (int)slot;
            if (!ClothingSlotRegistry.IsValid(slot))
            {
                return "invalid slot";
            }

            ClothingItemIdentity current = GetCurrentIdentity(slot);
            byte raw = GetRawState(index);
            GarmentState state = ClothingStateResolver.Resolve(raw);
            bool runtimeObject = (_observedAvailabilityMask & (1 << index)) != 0;
            bool structurallySuppressed = IsStructurallySuppressed(index);
            ClothingMaskLayerData layer = _layers[index];
            if (layer == null)
            {
                return string.Format(
                    "no mask, state={0} (raw {1}), runtimeObject={2}, structurallySuppressed={3}, current=[{4}]",
                    state,
                    raw,
                    runtimeObject,
                    structurallySuppressed,
                    current == null ? "none" : current.ToString());
            }

            state = ResolveStateForLayer(index);

            bool bindingMatches = layer.BoundItemIdentity != null &&
                                  layer.BoundItemIdentity.Matches(
                                      current,
                                      BodyMaskLayersPlugin.Settings.MaskBindingMode.Value);
            string hash = string.IsNullOrEmpty(layer.Hash)
                ? "no hash"
                : layer.Hash.Substring(0, Math.Min(12, layer.Hash.Length));
            return string.Format(
                "{0}, active={1}, {2}x{3}, state={4} (raw {5}), runtimeObject={6}, " +
                "structurallySuppressed={7}, binding={8}, current=[{9}], bound=[{10}], " +
                "hash={11}, stats=[{12}], {13}",
                layer.Enabled ? "enabled" : "disabled",
                IsLayerActive(index),
                layer.Width,
                layer.Height,
                state,
                raw,
                runtimeObject,
                structurallySuppressed,
                bindingMatches ? "match" : "mismatch",
                current == null ? "none" : current.ToString(),
                layer.BoundItemIdentity == null ? "none" : layer.BoundItemIdentity.ToString(),
                hash,
                _statistics[index] == null ? "none" : _statistics[index].ToString(),
                GetInactiveReason(index));
        }

        public Dictionary<ClothingSlot, ClothingMaskLayerData> SnapshotLayers()
        {
            Dictionary<ClothingSlot, ClothingMaskLayerData> result =
                new Dictionary<ClothingSlot, ClothingMaskLayerData>();
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] != null)
                {
                    result[(ClothingSlot)i] = _layers[i].DeepClone();
                }
            }

            return result;
        }

        public void ApplyPartialCoordinateLayers(
            Dictionary<ClothingSlot, ClothingMaskLayerData> source,
            bool[] selectedSlots)
        {
            if (selectedSlots == null || selectedSlots.Length < ClothingSlotRegistry.SlotCount)
            {
                throw new ArgumentException("Nine slot selection flags are required.", "selectedSlots");
            }

            for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
            {
                if (!selectedSlots[i])
                {
                    continue;
                }

                ClothingMaskLayerData layer;
                if (source != null && source.TryGetValue((ClothingSlot)i, out layer))
                {
                    _layers[i] = layer.DeepClone();
                    DecodePersistedLayer(i);
                }
                else
                {
                    _layers[i] = null;
                    SetDecodedLayer(i, null, null);
                }
            }

            _dirty = true;
            _lastDirtyReason = "partial coordinate slot merge";
            PersistToCurrentOutfit();
        }

        private void DetectCoordinateReplacement()
        {
            ChaFileClothes clothes = ChaControl.nowCoordinate == null ? null : ChaControl.nowCoordinate.clothes;
            if (ReferenceEquals(clothes, _observedClothes))
            {
                return;
            }

            _observedClothes = clothes;
            ResetObservedRuntimeValues();
            LoadPluginData(CoordinateDataHandler.ReadFromClothes(clothes), "coordinate switch");
        }

        private void ApplyPendingConfigurationChange()
        {
            if (!_decodeConfigurationDirty)
            {
                return;
            }

            _decodeConfigurationDirty = false;
            for (int i = 0; i < _layers.Length; i++)
            {
                SetDecodedLayer(i, null, null);
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] != null)
                {
                    DecodePersistedLayer(i);
                }
            }

            _dirty = true;
            _lastDirtyReason = "mask decoding configuration change";
        }

        private void PollClothingStateAndItems()
        {
            bool runtimeChanged = false;
            int shoesType = ChaControl == null || ChaControl.fileStatus == null
                ? -1
                : ChaControl.fileStatus.shoesType;
            if (_observedShoesType != shoesType)
            {
                _observedShoesType = shoesType;
                _dirty = true;
                _lastDirtyReason = "active shoe type change";
                runtimeChanged = true;
            }

            int availabilityMask = 0;
            if (ChaControl != null)
            {
                for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
                {
                    if (ChaControl.IsClothes(i))
                    {
                        availabilityMask |= 1 << i;
                    }
                }
            }

            if (_observedAvailabilityMask != availabilityMask)
            {
                _observedAvailabilityMask = availabilityMask;
                _dirty = true;
                _lastDirtyReason = "clothing object availability change";
                runtimeChanged = true;
            }

            int structuralFlags = GetStructuralFlags();
            if (_observedStructuralFlags != structuralFlags)
            {
                _observedStructuralFlags = structuralFlags;
                _dirty = true;
                _lastDirtyReason = "integrated garment structure change";
                runtimeChanged = true;
            }

            for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
            {
                byte raw = GetRawState(i);
                if (_observedRawStates[i] != raw)
                {
                    _observedRawStates[i] = raw;
                    GarmentState resolved = ClothingStateResolver.Resolve(raw);
                    if (resolved != GarmentState.Unknown)
                    {
                        _lastKnownStates[i] = resolved;
                        _warnedUnknownStates[i] = false;
                    }
                    else if (!_warnedUnknownStates[i] && _layers[i] != null && _layers[i].Enabled)
                    {
                        _warnedUnknownStates[i] = true;
                        if (CanWriteStateLog())
                        {
                            BodyMaskLayersPlugin.Log.LogWarning(string.Format(
                                "Unexpected clothing state {0} for {1}; applying {2}.",
                                raw,
                                ClothingSlotRegistry.GetDisplayName((ClothingSlot)i),
                                BodyMaskLayersPlugin.Settings.UnknownStatePolicy.Value));
                        }
                    }

                    _dirty = true;
                    _lastDirtyReason = "clothing state change";
                    runtimeChanged = true;
                }

                int itemId = GetCurrentItemId(i);
                if (_observedItemIds[i] != itemId)
                {
                    _observedItemIds[i] = itemId;
                    _currentIdentities[i] = ClothingItemIdentityResolver.Resolve(ChaControl, (ClothingSlot)i);
                    _basePixelsDirty = true;
                    _dirty = true;
                    _lastDirtyReason = "clothing item change";
                    runtimeChanged = true;
                }
            }

            if (runtimeChanged && BodyMaskLayersPlugin.Settings.LogStateChanges.Value && CanWriteStateLog())
            {
                BodyMaskLayersPlugin.Log.LogInfo(
                    "BodyMask Layers runtime state changed for " +
                    (ChaControl == null ? "<missing>" : ChaControl.name) +
                    "; reason=" + _lastDirtyReason +
                    "; shoesType=" + _observedShoesType +
                    "; available=0x" + _observedAvailabilityMask.ToString("X3") +
                    "; structure=0x" + _observedStructuralFlags.ToString("X1") + ".");
            }
        }

        private void DetectExternalMaterialReplacement()
        {
            if (_targetMaterial == null || !BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial))
            {
                return;
            }

            Texture actual = _targetMaterial.GetTexture(ChaShader._AlphaMask);
            if (_compositeActive)
            {
                if (actual != _outputTexture)
                {
                    _baseTexture = actual;
                    _basePixelsDirty = true;
                    _dirty = true;
                    _lastDirtyReason = "unhooked upstream body-mask texture replacement";
                }
            }
            else if (actual != _baseTexture)
            {
                _baseTexture = actual;
                _basePixelsDirty = true;
                _dirty = true;
                _lastDirtyReason = "upstream body-mask replacement while inactive";
            }

            float actualAlphaA = _targetMaterial.GetFloat(ChaShader._alpha_a);
            float actualAlphaB = _targetMaterial.GetFloat(ChaShader._alpha_b);
            if (_compositeActive)
            {
                bool externalScalarWrite = false;
                if (Math.Abs(actualAlphaA - 1f) > 0.0001f)
                {
                    _baseAlphaA = actualAlphaA;
                    externalScalarWrite = true;
                }

                if (Math.Abs(actualAlphaB - 1f) > 0.0001f)
                {
                    _baseAlphaB = actualAlphaB;
                    externalScalarWrite = true;
                }

                if (externalScalarWrite)
                {
                    _dirty = true;
                    _lastDirtyReason = "unhooked upstream body-mask scalar replacement";
                }
            }
            else
            {
                _baseAlphaA = actualAlphaA;
                _baseAlphaB = actualAlphaB;
            }
        }

        private void RefreshTarget(bool force)
        {
            Material resolved = BodyMaskTargetResolver.GetExactBodyMaterial(ChaControl);
            bool sameReference = ReferenceEquals(resolved, _targetMaterial);
            if (!force && sameReference &&
                ((resolved != null && resolved.shader == _targetShader) ||
                 (resolved == null && _targetMaterialId == 0)))
            {
                return;
            }

            if (!ReferenceEquals(_targetMaterial, null))
            {
                RestoreBaseMaterial();
                UnregisterTarget();
            }

            _targetMaterial = resolved;
            _targetShader = resolved == null ? null : resolved.shader;
            _baseTexture = null;
            _basePixelsDirty = true;
            _baseAlphaA = 1f;
            _baseAlphaB = 1f;
            _warnedUnsupportedShader = false;
            if (_targetMaterial != null && BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial))
            {
                _baseTexture = _targetMaterial.GetTexture(ChaShader._AlphaMask);
                if (_baseTexture == _outputTexture)
                {
                    _baseTexture = BodyMaskTargetResolver.GetVanillaTopMask(ChaControl);
                }

                _baseAlphaA = _targetMaterial.GetFloat(ChaShader._alpha_a);
                _baseAlphaB = _targetMaterial.GetFloat(ChaShader._alpha_b);
                RegisterTarget(_targetMaterial);
            }

            _dirty = true;
            _lastDirtyReason = "body material or shader target change";
        }

        private void RebuildComposite()
        {
            _dirty = false;
            _lastCompositionReason = _lastDirtyReason;
            if (!BodyMaskLayersPlugin.Settings.Enabled.Value)
            {
                RestoreBaseMaterial();
                ReleaseCompositionResources();
                return;
            }

            if (!CompatibilityPatches.CompositionOwnershipAllowed &&
                !BodyMaskLayersPlugin.Settings.AllowUnauditedChaAlphaMask.Value)
            {
                RestoreBaseMaterial();
                ReleaseCompositionResources();
                return;
            }

            if (_targetMaterial == null || !BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial))
            {
                if (_targetMaterial != null && !_warnedUnsupportedShader)
                {
                    _warnedUnsupportedShader = true;
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Body material shader lacks the confirmed _AlphaMask/_alpha_a/_alpha_b contract: " +
                        TargetDescription + ". Custom layers are disabled for this character.");
                }

                RestoreBaseMaterial();
                ReleaseCompositionResources();
                return;
            }

            int activeCount = 0;
            for (int i = 0; i < _activeLayers.Length; i++)
            {
                bool eligible = IsLayerActive(i);
                GarmentState state = eligible
                    ? ResolveStateForLayer(i)
                    : GarmentState.Off;
                _compositionStates[i] = state;
                bool contributes = eligible && LayerCanContributeInState(i, state);
                if (contributes)
                {
                    for (int previous = 0; previous < i; previous++)
                    {
                        if (!_activeLayers[previous] ||
                            !ReferenceEquals(_semanticMasks[previous], _semanticMasks[i]))
                        {
                            continue;
                        }

                        if (state == GarmentState.Full)
                        {
                            _compositionStates[previous] = GarmentState.Full;
                        }

                        contributes = false;
                        break;
                    }
                }

                _activeLayers[i] = contributes;
                if (_activeLayers[i])
                {
                    activeCount++;
                }
            }

            if (activeCount == 0)
            {
                RestoreBaseMaterial();
                if (!HasDecodedLayer())
                {
                    ReleaseCompositionResources();
                }
                else
                {
                    PrepareRetainedInactiveResources();
                }
                return;
            }

            Texture baseTexture = _baseTexture == _outputTexture
                ? BodyMaskTargetResolver.GetVanillaTopMask(ChaControl)
                : _baseTexture;
            int maximumResolution = BodyMaskLayersPlugin.Settings.GetMaximumResolution();
            if (baseTexture != null &&
                (baseTexture.width > maximumResolution || baseTexture.height > maximumResolution))
            {
                int baseId = baseTexture.GetInstanceID();
                if (_warnedOversizedBaseId != baseId)
                {
                    _warnedOversizedBaseId = baseId;
                    BodyMaskLayersPlugin.Log.LogWarning(string.Format(
                        "Upstream body mask {0}x{1} exceeds MaximumMaskResolution={2}. " +
                        "Composition yielded without downsampling so B/A remain untouched.",
                        baseTexture.width,
                        baseTexture.height,
                        maximumResolution));
                }

                RestoreBaseMaterial();
                ReleaseCompositionResources();
                return;
            }

            bool shouldLogComposition = BodyMaskLayersPlugin.Settings.LogComposition.Value &&
                                        Time.unscaledTime >= _nextCompositionLogTime;
            Stopwatch timer = shouldLogComposition
                ? Stopwatch.StartNew()
                : null;
            int width;
            int height;
            SelectOutputSize(_activeLayers, baseTexture, out width, out height);
            EnsureOutputBuffers(width, height);
            Array.Clear(_hiddenPixels, 0, _hiddenPixels.Length);
            int customHiddenCount = 0;
            for (int i = 0; i < _activeLayers.Length; i++)
            {
                if (!_activeLayers[i])
                {
                    continue;
                }

                customHiddenCount += MaskComposer.Accumulate(
                    _semanticMasks[i],
                    _compositionStates[i],
                    width,
                    height,
                    _hiddenPixels);
            }

            if (customHiddenCount == 0)
            {
                RestoreBaseMaterial();
                PrepareRetainedInactiveResources();
                return;
            }

            Rgba32[] basePixels;
            int baseWidth;
            int baseHeight;
            string readError;
            if (!TryGetBasePixels(
                    baseTexture,
                    out basePixels,
                    out baseWidth,
                    out baseHeight,
                    out readError))
            {
                LogWarningRateLimited(
                    "Could not read the current vanilla/external body mask. Custom composition was skipped " +
                    "to preserve upstream RGBA safely. " + readError);
                RestoreBaseMaterial();
                ReleaseCompositionResources();
                return;
            }

            _hiddenPixelCount = BodyMaskFormatAdapter.WriteBinaryBodyMask(
                basePixels,
                baseWidth,
                baseHeight,
                _baseAlphaA,
                _baseAlphaB,
                _hiddenPixels,
                width,
                height,
                _outputPixels);
            ConfigureOutputSampler(baseTexture);
            for (int i = 0; i < _outputPixels.Length; i++)
            {
                Rgba32 pixel = _outputPixels[i];
                _outputColors[i] = new Color32(pixel.R, pixel.G, pixel.B, pixel.A);
            }

            _outputTexture.SetPixels32(_outputColors);
            _outputTexture.Apply(false, false);
            ApplyCompositeMaterial();

            if (timer != null)
            {
                timer.Stop();
                _nextCompositionLogTime = Time.unscaledTime +
                                          BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
                BodyMaskLayersPlugin.Log.LogInfo(string.Format(
                    "Composed {0} unique mask contributions at {1}x{2} in {3:F2} ms ({4} hidden pixels).",
                    activeCount,
                    width,
                    height,
                    timer.Elapsed.TotalMilliseconds,
                    _hiddenPixelCount));
            }
        }

        private bool IsLayerActive(int index)
        {
            ClothingMaskLayerData layer = _layers[index];
            if (!BodyMaskLayersPlugin.Settings.Enabled.Value ||
                layer == null || !layer.Enabled || _semanticMasks[index] == null ||
                layer.BoundItemIdentity == null)
            {
                return false;
            }

            if ((index == (int)ClothingSlot.IndoorShoes && _observedShoesType != 0) ||
                (index == (int)ClothingSlot.OutdoorShoes && _observedShoesType != 1))
            {
                return false;
            }

            if ((_observedAvailabilityMask & (1 << index)) == 0 || IsStructurallySuppressed(index))
            {
                return false;
            }

            return layer.BoundItemIdentity.Matches(
                GetCurrentIdentity((ClothingSlot)index),
                BodyMaskLayersPlugin.Settings.MaskBindingMode.Value);
        }

        private bool HasDecodedLayer()
        {
            for (int index = 0; index < _semanticMasks.Length; index++)
            {
                if (_semanticMasks[index] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool LayerCanContributeInState(int index, GarmentState state)
        {
            return MaskComposer.HasPotentialContribution(
                _statistics[index],
                BodyMaskLayersPlugin.Settings.UnknownColorPolicy.Value,
                state);
        }

        private void PrepareRetainedInactiveResources()
        {
            _hiddenPixelCount = 0;
            if (_baseTexture is RenderTexture)
            {
                _cachedBasePixelSource = null;
                _cachedBasePixels = null;
                _cachedBaseWidth = 0;
                _cachedBaseHeight = 0;
                _basePixelsDirty = true;
            }
        }

        private string GetInactiveReason(int index)
        {
            ClothingMaskLayerData layer = _layers[index];
            if (layer == null)
            {
                return "inactive: no mask";
            }

            if (!BodyMaskLayersPlugin.Settings.Enabled.Value)
            {
                return "inactive: plugin disabled";
            }

            if (!layer.Enabled)
            {
                return "inactive: layer disabled";
            }

            if (_semanticMasks[index] == null)
            {
                return "inactive: invalid or undecoded mask";
            }

            if ((_observedAvailabilityMask & (1 << index)) == 0)
            {
                return "inactive: no runtime clothing object";
            }

            if (IsStructurallySuppressed(index))
            {
                return "inactive: garment is integrated into another slot";
            }

            if ((index == (int)ClothingSlot.IndoorShoes && _observedShoesType != 0) ||
                (index == (int)ClothingSlot.OutdoorShoes && _observedShoesType != 1))
            {
                return "inactive: other shoe type is selected";
            }

            return IsLayerActive(index) ? "active" : "inactive: item binding mismatch";
        }

        private int GetStructuralFlags()
        {
            if (ChaControl == null)
            {
                return 0;
            }

            int flags = 0;
            if (ChaControl.notBot)
            {
                flags |= 1;
            }

            if (ChaControl.notBra)
            {
                flags |= 2;
            }

            if (ChaControl.notShorts)
            {
                flags |= 4;
            }

            return flags;
        }

        private bool IsStructurallySuppressed(int index)
        {
            return (index == (int)ClothingSlot.Bottom && (_observedStructuralFlags & 1) != 0) ||
                   (index == (int)ClothingSlot.Bra && (_observedStructuralFlags & 2) != 0) ||
                   (index == (int)ClothingSlot.Shorts && (_observedStructuralFlags & 4) != 0);
        }

        private bool CanWriteStateLog()
        {
            float now = Time.unscaledTime;
            if (now < _nextStateLogTime)
            {
                return false;
            }

            _nextStateLogTime = now + BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
            return true;
        }

        private void LogWarningRateLimited(string message)
        {
            float now = Time.unscaledTime;
            if (now < _nextWarningLogTime)
            {
                return;
            }

            _nextWarningLogTime = now + BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
            BodyMaskLayersPlugin.Log.LogWarning(message);
        }

        private GarmentState ResolveStateForLayer(int index)
        {
            GarmentState state = ClothingStateResolver.Resolve(GetRawState(index));
            UnknownStatePolicy policy = _layers[index].OptionalStatePolicy ??
                                        BodyMaskLayersPlugin.Settings.UnknownStatePolicy.Value;
            return ClothingStateResolver.ApplyUnknownPolicy(state, policy, _lastKnownStates[index]);
        }

        private void SelectOutputSize(bool[] active, Texture baseTexture, out int width, out int height)
        {
            int maximum = BodyMaskLayersPlugin.Settings.GetMaximumResolution();
            if (baseTexture != null && baseTexture.width > 0 && baseTexture.height > 0)
            {
                width = baseTexture.width;
                height = baseTexture.height;
                return;
            }

            int dimension = BodyMaskLayersPlugin.Settings.GetDefaultOutputResolution();
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i] && _semanticMasks[i] != null)
                {
                    dimension = Math.Max(dimension, Math.Max(_semanticMasks[i].Width, _semanticMasks[i].Height));
                }
            }

            dimension = Math.Min(maximum, dimension);
            width = dimension;
            height = dimension;
        }

        private void EnsureOutputBuffers(int width, int height)
        {
            int count = checked(width * height);
            if (_hiddenPixels == null || _hiddenPixels.Length != count)
            {
                _hiddenPixels = new bool[count];
                _outputPixels = new Rgba32[count];
                _outputColors = new Color32[count];
            }

            if (_outputTexture != null && (_outputWidth != width || _outputHeight != height))
            {
                UnityEngine.Object.Destroy(_outputTexture);
                _outputTexture = null;
            }

            if (_outputTexture == null)
            {
                _outputTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                _outputTexture.name = "KK_BodyMaskLayers_Composite_" + GetInstanceID();
                _outputTexture.filterMode = FilterMode.Point;
                _outputTexture.wrapMode = TextureWrapMode.Clamp;
                _outputWidth = width;
                _outputHeight = height;
            }
        }

        private void ConfigureOutputSampler(Texture baseTexture)
        {
            if (_outputTexture == null)
            {
                return;
            }

            if (baseTexture == null)
            {
                _outputTexture.filterMode = FilterMode.Point;
                _outputTexture.wrapMode = TextureWrapMode.Clamp;
                _outputTexture.anisoLevel = 0;
                return;
            }

            _outputTexture.filterMode = baseTexture.filterMode;
            _outputTexture.wrapMode = baseTexture.wrapMode;
            _outputTexture.anisoLevel = baseTexture.anisoLevel;
        }

        private void ApplyCompositeMaterial()
        {
            if (_targetMaterial == null || _outputTexture == null)
            {
                return;
            }

            _internalMaterialWrite = true;
            _applyingCompositeWrite = true;
            try
            {
                _targetMaterial.SetTexture(ChaShader._AlphaMask, _outputTexture);
                _targetMaterial.SetFloat(ChaShader._alpha_a, 1f);
                _targetMaterial.SetFloat(ChaShader._alpha_b, 1f);
                _compositeActive = true;
            }
            finally
            {
                _applyingCompositeWrite = false;
                _internalMaterialWrite = false;
            }
        }

        private bool TryGetBasePixels(
            Texture texture,
            out Rgba32[] pixels,
            out int width,
            out int height,
            out string error)
        {
            if (texture == null)
            {
                _cachedBasePixelSource = null;
                _cachedBasePixels = null;
                _cachedBaseWidth = 0;
                _cachedBaseHeight = 0;
                _basePixelsDirty = false;
                pixels = null;
                width = 0;
                height = 0;
                error = null;
                return true;
            }

            if (!_basePixelsDirty && ReferenceEquals(texture, _cachedBasePixelSource) &&
                _cachedBasePixels != null)
            {
                pixels = _cachedBasePixels;
                width = _cachedBaseWidth;
                height = _cachedBaseHeight;
                error = null;
                return true;
            }

            if (!TexturePixelReader.TryRead(texture, out pixels, out width, out height, out error))
            {
                return false;
            }

            _cachedBasePixelSource = texture;
            _cachedBasePixels = pixels;
            _cachedBaseWidth = width;
            _cachedBaseHeight = height;
            _basePixelsDirty = false;
            return true;
        }

        private void RestoreBaseMaterial()
        {
            if (!_compositeActive || _targetMaterial == null)
            {
                _compositeActive = false;
                return;
            }

            if (!BodyMaskTargetResolver.SupportsBodyMaskContract(_targetMaterial))
            {
                _compositeActive = false;
                return;
            }

            _internalMaterialWrite = true;
            _applyingCompositeWrite = false;
            try
            {
                _targetMaterial.SetTexture(ChaShader._AlphaMask, _baseTexture);
                _targetMaterial.SetFloat(ChaShader._alpha_a, _baseAlphaA);
                _targetMaterial.SetFloat(ChaShader._alpha_b, _baseAlphaB);
            }
            finally
            {
                _internalMaterialWrite = false;
                _compositeActive = false;
            }
        }

        private void ReleaseCompositionResources()
        {
            if (_outputTexture != null)
            {
                UnityEngine.Object.Destroy(_outputTexture);
                _outputTexture = null;
            }

            _hiddenPixels = null;
            _outputPixels = null;
            _outputColors = null;
            _outputWidth = 0;
            _outputHeight = 0;
            _hiddenPixelCount = 0;
            _cachedBasePixelSource = null;
            _cachedBasePixels = null;
            _cachedBaseWidth = 0;
            _cachedBaseHeight = 0;
            _basePixelsDirty = true;
        }

        private bool TryDecodePng(
            byte[] pngBytes,
            out SemanticMask semantic,
            out MaskColorStatistics statistics,
            out MaskValidationResult validation,
            out int normalizedPixelCount,
            out string error)
        {
            semantic = null;
            statistics = null;
            normalizedPixelCount = 0;
            error = null;
            PluginConfig settings = BodyMaskLayersPlugin.Settings;
            validation = PngMaskValidator.Validate(
                pngBytes,
                settings.GetMinimumResolution(),
                settings.GetMaximumResolution(),
                settings.GetMaximumPngBytes());
            if (!validation.IsValid)
            {
                error = validation.Message;
                return false;
            }

            Texture2D decoded = null;
            try
            {
                decoded = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!decoded.LoadImage(pngBytes))
                {
                    error = "Unity could not decode the PNG image.";
                    return false;
                }

                if (decoded.width != validation.Width || decoded.height != validation.Height)
                {
                    error = "Decoded PNG dimensions do not match its validated IHDR header.";
                    return false;
                }

                Rgba32[] pixels = TexturePixelReader.Convert(decoded.GetPixels32());
                return MaskColorDecoder.TryDecodeWithPaletteCompatibility(
                    pixels,
                    decoded.width,
                    decoded.height,
                    settings.CreateDecodeOptions(),
                    out semantic,
                    out statistics,
                    out normalizedPixelCount,
                    out error);
            }
            catch (Exception exception)
            {
                error = "PNG decoding failed: " + exception.Message;
                return false;
            }
            finally
            {
                if (decoded != null)
                {
                    UnityEngine.Object.Destroy(decoded);
                }
            }
        }

        private void DecodePersistedLayer(int index)
        {
            ClothingMaskLayerData layer = _layers[index];
            SetDecodedLayer(index, null, null);
            SemanticMask semantic = null;
            MaskColorStatistics statistics = null;
            MaskValidationResult validation = null;
            int normalizedPixelCount = 0;
            string error = "Layer or PNG bytes are missing.";
            string actualHash = null;
            if (layer != null && layer.ColorFormatVersion != 1)
            {
                error = "Unsupported mask color format version " + layer.ColorFormatVersion + ".";
            }
            else if (layer != null && layer.OriginalPngBytes != null)
            {
                actualHash = HashUtility.Sha256(layer.OriginalPngBytes);
                if (!string.IsNullOrEmpty(layer.Hash) &&
                    !string.Equals(layer.Hash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Stored PNG SHA-256 does not match its metadata.";
                }
                else
                {
                    bool reusedDecode = TryReuseDecodedLayer(
                        actualHash,
                        out semantic,
                        out statistics);
                    if (reusedDecode)
                    {
                        PluginConfig settings = BodyMaskLayersPlugin.Settings;
                        validation = PngMaskValidator.Validate(
                            layer.OriginalPngBytes,
                            settings.GetMinimumResolution(),
                            settings.GetMaximumResolution(),
                            settings.GetMaximumPngBytes());
                        reusedDecode = validation.IsValid;
                        if (!reusedDecode)
                        {
                            semantic = null;
                            statistics = null;
                            error = validation.Message;
                        }
                    }

                    if (reusedDecode || TryDecodePng(
                        layer.OriginalPngBytes,
                        out semantic,
                        out statistics,
                        out validation,
                        out normalizedPixelCount,
                        out error))
                    {
                        WarnAboutBluePixels((ClothingSlot)index, statistics, true);
                        layer.Width = validation.Width;
                        layer.Height = validation.Height;
                        layer.Hash = actualHash;
                        layer.LastValidationResult = reusedDecode
                            ? "Valid using cached identical decode: " + statistics
                            : normalizedPixelCount == 0
                                ? "Valid: " + statistics
                                : "Valid after palette normalization (" + normalizedPixelCount +
                                  " pixels): " + statistics;
                        SetDecodedLayer(index, semantic, statistics);
                        return;
                    }
                }
            }

            if (layer != null)
            {
                WarnAboutBluePixels((ClothingSlot)index, statistics, false);
                layer.LastValidationResult = "Invalid: " + error;
                BodyMaskLayersPlugin.Log.LogWarning(
                    "Stored " + ClothingSlotRegistry.GetDisplayName((ClothingSlot)index) +
                    " mask is inactive: " + error);
            }
        }

        private static void WarnAboutBluePixels(
            ClothingSlot slot,
            MaskColorStatistics statistics,
            bool accepted)
        {
            if (statistics == null || statistics.BluePixels <= 0)
            {
                return;
            }

            BodyMaskLayersPlugin.Log.LogWarning(string.Format(
                "{0} mask contains {1} blue categorical pixels. Blue may carry packed/unsupported " +
                "shader data; the mask was {2} under UnknownColorPolicy={3}.",
                ClothingSlotRegistry.GetDisplayName(slot),
                statistics.BluePixels,
                accepted ? "accepted" : "rejected",
                BodyMaskLayersPlugin.Settings.UnknownColorPolicy.Value));
        }

        private void LoadPluginData(PluginData data, string source)
        {
            Dictionary<ClothingSlot, ClothingMaskLayerData> loaded;
            string error;
            if (!CoordinateDataHandler.TryReadPluginData(data, out loaded, out error))
            {
                BodyMaskLayersPlugin.Log.LogWarning("Could not load BodyMask Layers from " + source + ": " + error);
                loaded = new Dictionary<ClothingSlot, ClothingMaskLayerData>();
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                SetDecodedLayer(i, null, null);
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                ClothingMaskLayerData layer;
                _layers[i] = loaded.TryGetValue((ClothingSlot)i, out layer) ? layer.DeepClone() : null;
                if (_layers[i] != null)
                {
                    DecodePersistedLayer(i);
                }
            }

            _dirty = true;
            _lastDirtyReason = "plugin data loaded from " + source;
        }

        private void PersistToCurrentOutfit()
        {
            List<ClothingMaskLayerData> layers = new List<ClothingMaskLayerData>();
            foreach (ClothingMaskLayerData layer in EnumerateLayers())
            {
                layers.Add(layer);
            }

            SetClothesExtData(layers.Count == 0 ? null : CoordinateDataHandler.CreatePluginData(layers));
        }

        private IEnumerable<ClothingMaskLayerData> EnumerateLayers()
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] != null && _layers[i].OriginalPngBytes != null &&
                    _layers[i].OriginalPngBytes.Length != 0)
                {
                    yield return _layers[i];
                }
            }
        }

        private ClothingItemIdentity GetCurrentIdentity(ClothingSlot slot)
        {
            int index = (int)slot;
            if (_currentIdentities[index] == null)
            {
                _currentIdentities[index] = ClothingItemIdentityResolver.Resolve(ChaControl, slot);
                _observedItemIds[index] = _currentIdentities[index].LocalItemId;
            }

            return _currentIdentities[index];
        }

        private int GetCurrentItemId(int index)
        {
            if (ChaControl.nowCoordinate == null || ChaControl.nowCoordinate.clothes == null ||
                ChaControl.nowCoordinate.clothes.parts == null ||
                index >= ChaControl.nowCoordinate.clothes.parts.Length)
            {
                return 0;
            }

            return ChaControl.nowCoordinate.clothes.parts[index].id;
        }

        private byte GetRawState(int index)
        {
            if (ChaControl == null || ChaControl.fileStatus == null ||
                ChaControl.fileStatus.clothesState == null ||
                index >= ChaControl.fileStatus.clothesState.Length)
            {
                return byte.MaxValue;
            }

            return ChaControl.fileStatus.clothesState[index];
        }

        private void ResetObservedRuntimeValues()
        {
            _observedShoesType = int.MinValue;
            _observedAvailabilityMask = -1;
            _observedStructuralFlags = -1;
            for (int i = 0; i < ClothingSlotRegistry.SlotCount; i++)
            {
                _observedRawStates[i] = byte.MaxValue;
                _observedItemIds[i] = int.MinValue;
                _currentIdentities[i] = null;
                _lastKnownStates[i] = GarmentState.Unknown;
                _warnedUnknownStates[i] = false;
            }
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

        private void RegisterTarget(Material material)
        {
            lock (RegistryLock)
            {
                _targetMaterialId = material.GetInstanceID();
                MaterialTargets[_targetMaterialId] = this;
            }
        }

        private void UnregisterTarget()
        {
            if (_targetMaterialId == 0)
            {
                return;
            }

            lock (RegistryLock)
            {
                BodyMaskCharacterController existing;
                if (MaterialTargets.TryGetValue(_targetMaterialId, out existing) && existing == this)
                {
                    MaterialTargets.Remove(_targetMaterialId);
                }

                _targetMaterialId = 0;
            }
        }
    }
}
