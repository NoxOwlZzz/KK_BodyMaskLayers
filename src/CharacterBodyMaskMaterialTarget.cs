using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal interface IMaterialTargetDirtySink
    {
        void RequestMaterialDirty(string reason, bool baseContentMayHaveChanged);
    }

    internal sealed class CharacterBodyMaskMaterialTarget
    {
        private readonly BodyMaskCharacterController owner;
        private readonly IMaterialTargetDirtySink dirtySink;
        private readonly MaskCompositionResources resources;
        private Material targetMaterial;
        private Shader targetShader;
        private int targetMaterialId;
        private Texture baseTexture;
        private float baseAlphaA = 1f;
        private float baseAlphaB = 1f;
        private bool internalMaterialWrite;
        private bool applyingCompositeWrite;
        private bool compositeActive;
        private bool warnedUnsupportedShader;

        public CharacterBodyMaskMaterialTarget(
            BodyMaskCharacterController controller,
            IMaterialTargetDirtySink materialDirtySink,
            MaskCompositionResources compositionResources)
        {
            if (controller == null)
            {
                throw new ArgumentNullException("controller");
            }

            if (materialDirtySink == null)
            {
                throw new ArgumentNullException("materialDirtySink");
            }

            if (compositionResources == null)
            {
                throw new ArgumentNullException("compositionResources");
            }

            owner = controller;
            dirtySink = materialDirtySink;
            resources = compositionResources;
        }

        public Material Material
        {
            get { return targetMaterial; }
        }

        public Texture BaseTexture
        {
            get { return baseTexture; }
        }

        public float BaseAlphaA
        {
            get { return baseAlphaA; }
        }

        public float BaseAlphaB
        {
            get { return baseAlphaB; }
        }

        public bool IsCompositeActive
        {
            get { return compositeActive; }
        }

        public bool IsInternalMaterialWrite
        {
            get { return internalMaterialWrite; }
        }

        public bool SupportsContract
        {
            get
            {
                return targetMaterial != null &&
                       BodyMaskTargetResolver.SupportsBodyMaskContract(targetMaterial);
            }
        }

        public string TargetDescription
        {
            get
            {
                if (targetMaterial == null)
                {
                    return "unresolved";
                }

                string shader = targetMaterial.shader == null
                    ? "<no shader>"
                    : targetMaterial.shader.name;
                return targetMaterial.name + " / " + shader;
            }
        }

        public string MaterialContractDescription
        {
            get
            {
                if (targetMaterial == null)
                {
                    return "material unresolved";
                }

                Texture actual = SupportsContract
                    ? targetMaterial.GetTexture(ChaShader._AlphaMask)
                    : null;
                float actualA = SupportsContract
                    ? targetMaterial.GetFloat(ChaShader._alpha_a)
                    : float.NaN;
                float actualB = SupportsContract
                    ? targetMaterial.GetFloat(ChaShader._alpha_b)
                    : float.NaN;
                return string.Format(
                    "properties=_AlphaMask/_alpha_a/_alpha_b, actualTexture={0}, actualFloats=({1},{2}), " +
                    "capturedUpstreamFloats=({3},{4}), managed=R/G, preserved=B/A",
                    actual == null ? "none" : actual.name,
                    actualA,
                    actualB,
                    baseAlphaA,
                    baseAlphaB);
            }
        }

        public string GetBaseTextureDescription(ChaControl character)
        {
            Texture texture = GetCompositionBaseTexture(character);
            return texture == null
                ? "none"
                : string.Format(
                    "{0}, {1}x{2}, {3}",
                    texture.name,
                    texture.width,
                    texture.height,
                    texture.GetType().Name);
        }

        public Texture GetCompositionBaseTexture(ChaControl character)
        {
            return baseTexture == resources.OutputTexture
                ? BodyMaskTargetResolver.GetVanillaTopMask(character)
                : baseTexture;
        }

        public void Refresh(ChaControl character, bool force)
        {
            Material resolved = BodyMaskTargetResolver.GetExactBodyMaterial(character);
            bool sameReference = ReferenceEquals(resolved, targetMaterial);
            if (!force && sameReference &&
                ((resolved != null && resolved.shader == targetShader) ||
                 (resolved == null && targetMaterialId == 0)))
            {
                return;
            }

            if (!ReferenceEquals(targetMaterial, null))
            {
                Restore();
                Unregister();
            }

            targetMaterial = resolved;
            targetShader = resolved == null ? null : resolved.shader;
            baseTexture = null;
            resources.InvalidateBasePixels();
            baseAlphaA = 1f;
            baseAlphaB = 1f;
            warnedUnsupportedShader = false;
            if (SupportsContract)
            {
                baseTexture = targetMaterial.GetTexture(ChaShader._AlphaMask);
                if (baseTexture == resources.OutputTexture)
                {
                    baseTexture = BodyMaskTargetResolver.GetVanillaTopMask(character);
                }

                baseAlphaA = targetMaterial.GetFloat(ChaShader._alpha_a);
                baseAlphaB = targetMaterial.GetFloat(ChaShader._alpha_b);
                targetMaterialId = BodyMaskControllerRegistry.BindMaterial(
                    targetMaterial,
                    owner);
            }

            dirtySink.RequestMaterialDirty(
                "body material or shader target change",
                true);
        }

        public void DetectExternalReplacement()
        {
            if (!SupportsContract)
            {
                return;
            }

            Texture actual = targetMaterial.GetTexture(ChaShader._AlphaMask);
            if (compositeActive)
            {
                if (actual != resources.OutputTexture)
                {
                    baseTexture = actual;
                    dirtySink.RequestMaterialDirty(
                        "unhooked upstream body-mask texture replacement",
                        true);
                }
            }
            else if (actual != baseTexture)
            {
                baseTexture = actual;
                dirtySink.RequestMaterialDirty(
                    "upstream body-mask replacement while inactive",
                    true);
            }

            float actualAlphaA = targetMaterial.GetFloat(ChaShader._alpha_a);
            float actualAlphaB = targetMaterial.GetFloat(ChaShader._alpha_b);
            if (compositeActive)
            {
                bool externalScalarWrite = false;
                if (Math.Abs(actualAlphaA - 1f) > 0.0001f)
                {
                    baseAlphaA = actualAlphaA;
                    externalScalarWrite = true;
                }

                if (Math.Abs(actualAlphaB - 1f) > 0.0001f)
                {
                    baseAlphaB = actualAlphaB;
                    externalScalarWrite = true;
                }

                if (externalScalarWrite)
                {
                    dirtySink.RequestMaterialDirty(
                        "unhooked upstream body-mask scalar replacement",
                        false);
                }
            }
            else
            {
                baseAlphaA = actualAlphaA;
                baseAlphaB = actualAlphaB;
            }
        }

        public void InterceptExternalTexture(Texture incoming, ref Texture effectiveValue)
        {
            if (internalMaterialWrite || incoming == resources.OutputTexture)
            {
                return;
            }

            baseTexture = incoming;
            dirtySink.RequestMaterialDirty("upstream body-mask texture write", true);
            if (compositeActive && resources.OutputTexture != null)
            {
                effectiveValue = resources.OutputTexture;
            }
        }

        public void InterceptExternalFloat(
            int propertyId,
            float incoming,
            ref float effectiveValue)
        {
            if (internalMaterialWrite)
            {
                return;
            }

            if (propertyId == ChaShader._alpha_a)
            {
                baseAlphaA = incoming;
            }
            else if (propertyId == ChaShader._alpha_b)
            {
                baseAlphaB = incoming;
            }
            else
            {
                return;
            }

            dirtySink.RequestMaterialDirty("upstream body-mask scalar write", false);
            if (compositeActive)
            {
                effectiveValue = 1f;
            }
        }

        public void FinalizeInternalTextureWrite(ref Texture effectiveValue)
        {
            if (internalMaterialWrite && applyingCompositeWrite &&
                resources.OutputTexture != null)
            {
                effectiveValue = resources.OutputTexture;
            }
        }

        public void FinalizeInternalFloatWrite(int propertyId, ref float effectiveValue)
        {
            if (internalMaterialWrite && applyingCompositeWrite &&
                (propertyId == ChaShader._alpha_a || propertyId == ChaShader._alpha_b))
            {
                effectiveValue = 1f;
            }
        }

        public bool TryMarkUnsupportedShaderWarning()
        {
            if (targetMaterial == null || warnedUnsupportedShader)
            {
                return false;
            }

            warnedUnsupportedShader = true;
            return true;
        }

        public void ApplyComposite()
        {
            if (targetMaterial == null || resources.OutputTexture == null)
            {
                return;
            }

            internalMaterialWrite = true;
            applyingCompositeWrite = true;
            try
            {
                targetMaterial.SetTexture(
                    ChaShader._AlphaMask,
                    resources.OutputTexture);
                targetMaterial.SetFloat(ChaShader._alpha_a, 1f);
                targetMaterial.SetFloat(ChaShader._alpha_b, 1f);
                compositeActive = true;
            }
            finally
            {
                applyingCompositeWrite = false;
                internalMaterialWrite = false;
            }
        }

        public void Restore()
        {
            if (!compositeActive || targetMaterial == null)
            {
                compositeActive = false;
                return;
            }

            if (!SupportsContract)
            {
                compositeActive = false;
                return;
            }

            internalMaterialWrite = true;
            applyingCompositeWrite = false;
            try
            {
                targetMaterial.SetTexture(ChaShader._AlphaMask, baseTexture);
                targetMaterial.SetFloat(ChaShader._alpha_a, baseAlphaA);
                targetMaterial.SetFloat(ChaShader._alpha_b, baseAlphaB);
            }
            finally
            {
                internalMaterialWrite = false;
                compositeActive = false;
            }
        }

        public void Unregister()
        {
            if (targetMaterialId == 0)
            {
                return;
            }

            BodyMaskControllerRegistry.UnbindMaterial(targetMaterialId, owner);
            targetMaterialId = 0;
        }
    }
}
