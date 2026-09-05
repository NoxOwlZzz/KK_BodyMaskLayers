using System;
using System.Diagnostics;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class CharacterMaskCompositionEngine
    {
        private readonly NativeMaskLayerStore nativeLayers;
        private readonly CharacterExternalMaskSession externalSession;
        private readonly CharacterClothingRuntime runtimeState;
        private readonly CharacterBodyMaskMaterialTarget materialTarget;
        private readonly MaskCompositionResources resources;
        private readonly CompositionScheduler scheduler = new CompositionScheduler();
        private readonly bool[] activeNative =
            new bool[ClothingSlotRegistry.SlotCount];
        private readonly bool[] activeExternal =
            new bool[ClothingSlotRegistry.SlotCount];
        private readonly GarmentState[] compositionStates =
            new GarmentState[ClothingSlotRegistry.SlotCount];
        private readonly byte[] compositionRawStates =
            new byte[ClothingSlotRegistry.SlotCount];
        private float nextCompositionLogTime;
        private float nextWarningLogTime;
        private string lastDirtyReason = "initialization";
        private string lastCompositionReason = "not composed yet";

        public CharacterMaskCompositionEngine(
            NativeMaskLayerStore layerStore,
            CharacterExternalMaskSession externalMaskSession,
            CharacterClothingRuntime clothingRuntime,
            CharacterBodyMaskMaterialTarget bodyMaterialTarget,
            MaskCompositionResources compositionResources)
        {
            if (layerStore == null)
            {
                throw new ArgumentNullException("layerStore");
            }

            if (externalMaskSession == null)
            {
                throw new ArgumentNullException("externalMaskSession");
            }

            if (clothingRuntime == null)
            {
                throw new ArgumentNullException("clothingRuntime");
            }

            if (bodyMaterialTarget == null)
            {
                throw new ArgumentNullException("bodyMaterialTarget");
            }

            if (compositionResources == null)
            {
                throw new ArgumentNullException("compositionResources");
            }

            nativeLayers = layerStore;
            externalSession = externalMaskSession;
            runtimeState = clothingRuntime;
            materialTarget = bodyMaterialTarget;
            resources = compositionResources;
        }

        public string LastDirtyReason
        {
            get { return lastDirtyReason; }
        }

        public string LastCompositionReason
        {
            get { return lastCompositionReason; }
        }

        public void RequestDirty(string reason, bool baseContentMayHaveChanged)
        {
            bool coalesced = !scheduler.RequestDirty(ClassifyDirtyReason(reason));
            lastDirtyReason = reason;
            if (baseContentMayHaveChanged)
            {
                resources.InvalidateBasePixels();
            }

            BodyMaskPerformanceMetrics.RecordDirtyRequest(coalesced);
        }

        public bool TryCompose(ChaControl character, int frameId, int ownerInstanceId)
        {
            CompositionDirtyReason pendingReasons;
            if (!scheduler.TryBeginComposition(frameId, out pendingReasons))
            {
                return false;
            }

            try
            {
                Rebuild(character, ownerInstanceId);
                return true;
            }
            catch
            {
                scheduler.RestoreDirty(pendingReasons);
                throw;
            }
        }

        public bool IsNativeLayerActive(ChaControl character, int index)
        {
            return EvaluateNativeLayer(
                character,
                index,
                BodyMaskLayersPlugin.Settings.Enabled.Value) ==
                NativeLayerInactiveReason.None;
        }

        public string GetNativeInactiveReason(ChaControl character, int index)
        {
            NativeLayerInactiveReason reason = EvaluateNativeLayer(
                character,
                index,
                BodyMaskLayersPlugin.Settings.Enabled.Value);
            return LayerEligibilityEvaluator.DescribeInactive(reason);
        }

        public void ReleaseResources()
        {
            resources.Release();
        }

        private void Rebuild(ChaControl character, int ownerInstanceId)
        {
            lastCompositionReason = lastDirtyReason;
            if (!BodyMaskLayersPlugin.Settings.Enabled.Value)
            {
                materialTarget.Restore();
                resources.Release();
                return;
            }

            if (!materialTarget.SupportsContract)
            {
                if (materialTarget.TryMarkUnsupportedShaderWarning())
                {
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Body material shader lacks the confirmed _AlphaMask/_alpha_a/_alpha_b contract: " +
                        materialTarget.TargetDescription +
                        ". Custom layers are disabled for this character.");
                }

                materialTarget.Restore();
                resources.Release();
                return;
            }

            int activeCount = 0;
            for (int index = 0; index < activeNative.Length; index++)
            {
                NativeMaskLayerSlotState native =
                    nativeLayers.Get((ClothingSlot)index);
                bool eligible = IsNativeLayerActive(character, index);
                GarmentState state = eligible
                    ? runtimeState.ResolveStateForLayer(character, index, nativeLayers)
                    : GarmentState.Off;
                compositionStates[index] = state;
                byte rawState = eligible
                    ? runtimeState.ResolveNativeRawState(
                        index,
                        runtimeState.GetRawState(character, index),
                        nativeLayers)
                    : (byte)3;
                compositionRawStates[index] = rawState;
                bool contributes = eligible && LayerCanContributeInState(index, state);
                if (contributes)
                {
                    for (int previous = 0; previous < index; previous++)
                    {
                        if (!activeNative[previous] ||
                            !ReferenceEquals(
                                nativeLayers.Get((ClothingSlot)previous).SemanticMask,
                                native.SemanticMask) ||
                            !native.SemanticMask.AreStatePlanesEquivalent(
                                compositionRawStates[previous],
                                rawState))
                        {
                            continue;
                        }

                        contributes = false;
                        break;
                    }
                }

                activeNative[index] = contributes;
                if (contributes)
                {
                    activeCount++;
                }

                ExternalResolvedMask external =
                    externalSession.GetResolution((ClothingSlot)index);
                byte externalRawState = eligible || external != null
                    ? runtimeState.GetEffectiveRawState(character, index)
                    : (byte)3;
                bool externalContributes = IsExternalLayerActive(index) &&
                                         externalRawState <= 2 &&
                                         external.SemanticMask.Compiled.HasAnyCoverage(externalRawState) &&
                                         !IsExternalSuppressedByNative(character, index, external);
                activeExternal[index] = externalContributes;
                if (externalContributes)
                {
                    activeCount++;
                }
            }

            if (activeCount == 0)
            {
                materialTarget.Restore();
                if (!HasDecodedLayer() && !HasResolvedExternalLayer())
                {
                    resources.Release();
                }
                else
                {
                    resources.RetainInactive(materialTarget.BaseTexture);
                }

                return;
            }

            Texture baseTexture = materialTarget.GetCompositionBaseTexture(character);
            int maximumResolution = PortableMaskFormatLimits.MaximumDimension;
            if (baseTexture != null &&
                (baseTexture.width > maximumResolution || baseTexture.height > maximumResolution))
            {
                LogWarningRateLimited(string.Format(
                    "Upstream body mask {0}x{1} exceeds the supported {2}px mask resolution. " +
                    "Composition was skipped to preserve upstream RGBA safely.",
                    baseTexture.width,
                    baseTexture.height,
                    maximumResolution));
                materialTarget.Restore();
                resources.Release();
                return;
            }

            bool shouldLogComposition = BodyMaskLayersPlugin.Settings.LogComposition.Value &&
                                        Time.unscaledTime >= nextCompositionLogTime;
            Stopwatch timer = shouldLogComposition ? Stopwatch.StartNew() : null;
            int width;
            int height;
            SelectOutputSize(
                character,
                activeNative,
                activeExternal,
                baseTexture,
                out width,
                out height);
            if (resources.Prepare(width, height, ownerInstanceId))
            {
                BodyMaskPerformanceMetrics.RecordNewTextureAllocation();
            }

            int customHiddenCount = 0;
            for (int index = 0; index < activeNative.Length; index++)
            {
                if (activeNative[index])
                {
                    customHiddenCount += MaskComposer.Accumulate(
                        nativeLayers.Get((ClothingSlot)index).SemanticMask,
                        compositionRawStates[index],
                        width,
                        height,
                        resources.HideCoverage,
                        resources.Workspace);
                }

                if (activeExternal[index])
                {
                    ExternalResolvedMask external =
                        externalSession.GetResolution((ClothingSlot)index);
                    customHiddenCount += MaskComposer.Accumulate(
                        external.SemanticMask,
                        runtimeState.GetEffectiveRawState(character, index),
                        width,
                        height,
                        resources.HideCoverage,
                        resources.Workspace);
                }
            }

            if (customHiddenCount == 0)
            {
                materialTarget.Restore();
                resources.RetainInactive(materialTarget.BaseTexture);
                return;
            }

            Rgba32[] basePixels;
            int baseWidth;
            int baseHeight;
            string readError;
            if (!resources.TryGetBasePixels(
                    baseTexture,
                    out basePixels,
                    out baseWidth,
                    out baseHeight,
                    out readError))
            {
                LogWarningRateLimited(
                    "Could not read the current vanilla/external body mask. Custom composition was skipped " +
                    "to preserve upstream RGBA safely. " + readError);
                materialTarget.Restore();
                resources.Release();
                return;
            }

            BodyMaskPerformanceMetrics.RecordComposition();
            resources.HiddenPixelCount = BodyMaskFormatAdapter.WriteContinuousBodyMask(
                basePixels,
                baseWidth,
                baseHeight,
                materialTarget.BaseAlphaA,
                materialTarget.BaseAlphaB,
                resources.HideCoverage,
                width,
                height,
                resources.OutputPixels);
            resources.ConfigureSampler(baseTexture, HasActiveContinuousContribution());
            if (resources.UploadIfChanged())
            {
                BodyMaskPerformanceMetrics.RecordTextureUpload();
            }
            else
            {
                BodyMaskPerformanceMetrics.RecordOutputUnchangedSkip();
            }

            materialTarget.ApplyComposite();

            if (timer != null)
            {
                timer.Stop();
                nextCompositionLogTime = Time.unscaledTime +
                                         BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
                BodyMaskLayersPlugin.Log.LogInfo(string.Format(
                    "Composed {0} unique mask contributions at {1}x{2} in {3:F2} ms ({4} hidden pixels).",
                    activeCount,
                    width,
                    height,
                    timer.Elapsed.TotalMilliseconds,
                    resources.HiddenPixelCount));
            }
        }

        private bool HasDecodedLayer()
        {
            for (int index = 0; index < nativeLayers.SlotCount; index++)
            {
                if (nativeLayers.Get((ClothingSlot)index).SemanticMask != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasResolvedExternalLayer()
        {
            for (int index = 1; index < ClothingSlotRegistry.SlotCount; index++)
            {
                ExternalResolvedMask external =
                    externalSession.GetResolution((ClothingSlot)index);
                if (external != null && external.SemanticMask != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsExternalLayerActive(int index)
        {
            ExternalResolvedMask external = index > 0 && index < ClothingSlotRegistry.SlotCount
                ? externalSession.GetResolution((ClothingSlot)index)
                : null;
            if (!ExternalMaskProvider.DirectCompatibilityActive ||
                !BodyMaskLayersPlugin.Settings.Enabled.Value || external == null ||
                external.SemanticMask == null ||
                externalSession.IsFingerprintSuppressed(
                    (ClothingSlot)index,
                    external.Fingerprint) ||
                !LayerEligibilityEvaluator.IsSelectedShoe(index, runtimeState.ShoesType))
            {
                return false;
            }

            return runtimeState.IsStructurallySuppressed(index) ||
                   (runtimeState.AvailabilityMask & (1 << index)) != 0;
        }

        private bool IsExternalSuppressedByNative(
            ChaControl character,
            int index,
            ExternalResolvedMask external)
        {
            NativeMaskLayerSlotState native = nativeLayers.Get((ClothingSlot)index);
            NativeLayerEligibilityContext context =
                runtimeState.CreateEligibilityContext(index, true);
            if (!LayerEligibilityEvaluator.CanNativeOwnExternalSourceWithoutCurrentIdentity(
                    native.Layer,
                    native.SemanticMask != null,
                    context))
            {
                return false;
            }

            return LayerEligibilityEvaluator.NativeOwnsExternalSourceAfterPrecheck(
                native.Layer,
                runtimeState.GetCurrentIdentity(character, (ClothingSlot)index),
                external == null ? null : external.Fingerprint);
        }

        private bool HasActiveContinuousContribution()
        {
            for (int index = 0; index < ClothingSlotRegistry.SlotCount; index++)
            {
                SemanticMask native = nativeLayers.Get((ClothingSlot)index).SemanticMask;
                if (activeNative[index] && native != null && !native.IsBinary)
                {
                    return true;
                }

                ExternalResolvedMask external =
                    externalSession.GetResolution((ClothingSlot)index);
                if (activeExternal[index] && external != null &&
                    !external.SemanticMask.IsBinary)
                {
                    return true;
                }
            }

            return false;
        }

        private bool LayerCanContributeInState(int index, GarmentState state)
        {
            return MaskComposer.HasPotentialContribution(
                nativeLayers.Get((ClothingSlot)index).Statistics,
                BodyMaskLayersPlugin.Settings.UnknownColorPolicy.Value,
                state);
        }

        private void SelectOutputSize(
            ChaControl character,
            bool[] active,
            bool[] externalActive,
            Texture baseTexture,
            out int width,
            out int height)
        {
            if (baseTexture != null && baseTexture.width > 0 && baseTexture.height > 0)
            {
                width = baseTexture.width;
                height = baseTexture.height;
                return;
            }

            int dimension = BodyMaskLayersPlugin.Settings.GetDefaultOutputResolution();
            for (int index = 0; index < active.Length; index++)
            {
                SemanticMask native = nativeLayers.Get((ClothingSlot)index).SemanticMask;
                if (native != null &&
                    IsNativeSourceEligibleForStableSize(character, index))
                {
                    dimension = Math.Max(
                        dimension,
                        Math.Max(native.Width, native.Height));
                }

                ExternalResolvedMask external =
                    externalSession.GetResolution((ClothingSlot)index);
                if (external != null && external.SemanticMask != null)
                {
                    dimension = Math.Max(
                        dimension,
                        Math.Max(external.SemanticMask.Width, external.SemanticMask.Height));
                }
            }

            dimension = Math.Min(PortableMaskFormatLimits.MaximumDimension, dimension);
            width = dimension;
            height = dimension;
        }

        private bool IsNativeSourceEligibleForStableSize(
            ChaControl character,
            int index)
        {
            return EvaluateNativeLayer(character, index, true) ==
                   NativeLayerInactiveReason.None;
        }

        private NativeLayerInactiveReason EvaluateNativeLayer(
            ChaControl character,
            int index,
            bool pluginEnabled)
        {
            NativeMaskLayerSlotState native = nativeLayers.Get((ClothingSlot)index);
            NativeLayerEligibilityContext context =
                runtimeState.CreateEligibilityContext(index, pluginEnabled);
            NativeLayerInactiveReason reason =
                LayerEligibilityEvaluator.EvaluateNativeWithoutCurrentIdentity(
                native.Layer,
                native.SemanticMask != null,
                context);
            if (reason != NativeLayerInactiveReason.None)
            {
                return reason;
            }

            return LayerEligibilityEvaluator.EvaluateNativeBinding(
                native.Layer,
                runtimeState.GetCurrentIdentity(character, (ClothingSlot)index));
        }

        private void LogWarningRateLimited(string message)
        {
            float now = Time.unscaledTime;
            if (now < nextWarningLogTime)
            {
                return;
            }

            nextWarningLogTime = now +
                                  BodyMaskLayersPlugin.Settings.GetLogIntervalSeconds();
            BodyMaskLayersPlugin.Log.LogWarning(message);
        }

        private static CompositionDirtyReason ClassifyDirtyReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return CompositionDirtyReason.NativeLayerChanged;
            }

            if (reason.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return reason.IndexOf("converted", StringComparison.OrdinalIgnoreCase) >= 0
                    ? CompositionDirtyReason.NativeConversion
                    : CompositionDirtyReason.IndexChanged;
            }

            if (reason.IndexOf("coordinate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CompositionDirtyReason.CoordinateLoaded;
            }

            if (reason.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("shoe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("availability", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("structure", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CompositionDirtyReason.ItemChanged;
            }

            if (reason.IndexOf("state", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CompositionDirtyReason.ClothingStateChanged;
            }

            if (reason.IndexOf("material target", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("shader target", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CompositionDirtyReason.MaterialTargetChanged;
            }

            if (reason.IndexOf("upstream", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("body-mask", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("base refresh", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CompositionDirtyReason.BaseMaskChanged;
            }

            if (reason.IndexOf("reload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("plugin data loaded", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CompositionDirtyReason.CharacterLoaded;
            }

            return CompositionDirtyReason.NativeLayerChanged;
        }
    }
}
