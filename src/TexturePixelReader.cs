using System;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class TexturePixelReader
    {
        public static bool TryRead(Texture texture, out Rgba32[] pixels, out int width, out int height, out string error)
        {
            pixels = null;
            width = 0;
            height = 0;
            error = null;
            if (texture == null)
            {
                return true;
            }

            width = texture.width;
            height = texture.height;
            if (width <= 0 || height <= 0)
            {
                error = "Texture has invalid dimensions.";
                return false;
            }

            Texture2D texture2D = texture as Texture2D;
            if (texture2D != null)
            {
                try
                {
                    pixels = Convert(texture2D.GetPixels32());
                    return true;
                }
                catch (UnityException)
                {
                    // Non-readable texture; use an explicit GPU readback only on a dirty rebuild.
                }
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readable = null;
            try
            {
                temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(width, height, TextureFormat.ARGB32, false);
                BodyMaskPerformanceMetrics.RecordNewTextureAllocation();
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);
                pixels = Convert(readable.GetPixels32());
                return true;
            }
            catch (Exception exception)
            {
                error = "Texture readback failed: " + exception.Message;
                pixels = null;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null)
                {
                    RenderTexture.ReleaseTemporary(temporary);
                }

                if (readable != null)
                {
                    UnityEngine.Object.Destroy(readable);
                }
            }
        }

        public static Rgba32[] Convert(Color32[] colors)
        {
            Rgba32[] result = new Rgba32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 color = colors[i];
                result[i] = new Rgba32(color.r, color.g, color.b, color.a);
            }

            return result;
        }

        public static Color32[] Convert(Rgba32[] colors)
        {
            Color32[] result = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Rgba32 color = colors[i];
                result[i] = new Color32(color.R, color.G, color.B, color.A);
            }

            return result;
        }
    }
}
