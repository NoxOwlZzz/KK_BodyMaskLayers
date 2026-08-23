using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal static class PersistableLayerSelector
    {
        public static List<ClothingMaskLayerData> Materialize(
            IEnumerable<ClothingMaskLayerData> layers)
        {
            if (layers == null)
            {
                throw new ArgumentNullException("layers");
            }

            List<ClothingMaskLayerData> result = new List<ClothingMaskLayerData>();
            foreach (ClothingMaskLayerData layer in layers)
            {
                if (IsPersistable(layer))
                {
                    result.Add(layer);
                }
            }

            return result;
        }

        public static bool IsPersistable(ClothingMaskLayerData layer)
        {
            return layer != null &&
                   layer.OriginalPngBytes != null &&
                   layer.OriginalPngBytes.Length != 0;
        }
    }
}
