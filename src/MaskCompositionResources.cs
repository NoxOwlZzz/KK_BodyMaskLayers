using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class MaskCompositionResources
    {
        private Texture2D outputTexture;
        private byte[] hideCoverage;
        private Rgba32[] outputPixels;
        private Color32[] outputColors;
        private readonly MaskComposeWorkspace workspace = new MaskComposeWorkspace();
        private bool outputHasUploadedPixels;
        private Texture cachedBasePixelSource;
        private Rgba32[] cachedBasePixels;
        private int cachedBaseWidth;
        private int cachedBaseHeight;
        private bool basePixelsDirty = true;
        private int outputWidth;
        private int outputHeight;
        private int hiddenPixelCount;

        public Texture2D OutputTexture
        {
            get { return outputTexture; }
        }

        public byte[] HideCoverage
        {
            get { return hideCoverage; }
        }

        public Rgba32[] OutputPixels
        {
            get { return outputPixels; }
        }

        public MaskComposeWorkspace Workspace
        {
            get { return workspace; }
        }

        public int OutputWidth
        {
            get { return outputWidth; }
        }

        public int OutputHeight
        {
            get { return outputHeight; }
        }

        public int HiddenPixelCount
        {
            get { return hiddenPixelCount; }
            set { hiddenPixelCount = value; }
        }

        public bool Prepare(int width, int height, int ownerInstanceId)
        {
            int count = checked(width * height);
            if (hideCoverage == null || hideCoverage.Length != count)
            {
                hideCoverage = new byte[count];
                outputPixels = new Rgba32[count];
                outputColors = new Color32[count];
                outputHasUploadedPixels = false;
            }

            if (outputTexture != null && (outputWidth != width || outputHeight != height))
            {
                UnityEngine.Object.Destroy(outputTexture);
                outputTexture = null;
                outputHasUploadedPixels = false;
            }

            bool allocatedTexture = false;
            if (outputTexture == null)
            {
                outputTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                outputTexture.name = "KK_BodyMaskLayers_Composite_" + ownerInstanceId;
                outputTexture.filterMode = FilterMode.Point;
                outputTexture.wrapMode = TextureWrapMode.Clamp;
                outputWidth = width;
                outputHeight = height;
                allocatedTexture = true;
            }

            Array.Clear(hideCoverage, 0, hideCoverage.Length);
            return allocatedTexture;
        }

        public void ConfigureSampler(Texture baseTexture, bool hasContinuousContribution)
        {
            if (outputTexture == null)
            {
                return;
            }

            if (baseTexture == null)
            {
                outputTexture.filterMode = hasContinuousContribution
                    ? FilterMode.Bilinear
                    : FilterMode.Point;
                outputTexture.wrapMode = TextureWrapMode.Clamp;
                outputTexture.anisoLevel = 0;
                return;
            }

            outputTexture.filterMode = hasContinuousContribution
                ? FilterMode.Bilinear
                : baseTexture.filterMode;
            outputTexture.wrapMode = baseTexture.wrapMode;
            outputTexture.anisoLevel = baseTexture.anisoLevel;
        }

        public bool UploadIfChanged()
        {
            bool outputChanged = !outputHasUploadedPixels;
            for (int i = 0; i < outputPixels.Length; i++)
            {
                Rgba32 pixel = outputPixels[i];
                Color32 color = new Color32(pixel.R, pixel.G, pixel.B, pixel.A);
                if (!outputChanged &&
                    (outputColors[i].r != color.r || outputColors[i].g != color.g ||
                     outputColors[i].b != color.b || outputColors[i].a != color.a))
                {
                    outputChanged = true;
                }

                outputColors[i] = color;
            }

            if (outputChanged)
            {
                outputTexture.SetPixels32(outputColors);
                outputTexture.Apply(false, false);
                outputHasUploadedPixels = true;
            }

            return outputChanged;
        }

        public void InvalidateBasePixels()
        {
            basePixelsDirty = true;
        }

        public bool TryGetBasePixels(
            Texture texture,
            out Rgba32[] pixels,
            out int width,
            out int height,
            out string error)
        {
            if (texture == null)
            {
                ClearBasePixelCache(false);
                pixels = null;
                width = 0;
                height = 0;
                error = null;
                return true;
            }

            if (!basePixelsDirty && ReferenceEquals(texture, cachedBasePixelSource) &&
                cachedBasePixels != null)
            {
                pixels = cachedBasePixels;
                width = cachedBaseWidth;
                height = cachedBaseHeight;
                error = null;
                return true;
            }

            if (!TexturePixelReader.TryRead(texture, out pixels, out width, out height, out error))
            {
                return false;
            }

            cachedBasePixelSource = texture;
            cachedBasePixels = pixels;
            cachedBaseWidth = width;
            cachedBaseHeight = height;
            basePixelsDirty = false;
            return true;
        }

        public void RetainInactive(Texture baseTexture)
        {
            hiddenPixelCount = 0;
            if (baseTexture is RenderTexture)
            {
                ClearBasePixelCache(true);
            }
        }

        public void Release()
        {
            if (outputTexture != null)
            {
                UnityEngine.Object.Destroy(outputTexture);
                outputTexture = null;
            }

            hideCoverage = null;
            outputPixels = null;
            outputColors = null;
            outputWidth = 0;
            outputHeight = 0;
            hiddenPixelCount = 0;
            outputHasUploadedPixels = false;
            ClearBasePixelCache(true);
        }

        private void ClearBasePixelCache(bool dirty)
        {
            cachedBasePixelSource = null;
            cachedBasePixels = null;
            cachedBaseWidth = 0;
            cachedBaseHeight = 0;
            basePixelsDirty = dirty;
        }
    }
}
