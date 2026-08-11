using System;
using System.Collections.Generic;
using ExtensibleSaveFormat;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class CoordinateDataHandler
    {
        public const string PayloadKey = "Payload";

        public static PluginData CreatePluginData(IEnumerable<ClothingMaskLayerData> layers)
        {
            byte[] payload = CardDataSerializer.Serialize(layers);
            return new PluginData
            {
                version = CardDataSerializer.SchemaVersion,
                data = new Dictionary<string, object>
                {
                    { PayloadKey, payload }
                }
            };
        }

        public static bool TryReadPluginData(
            PluginData data,
            out Dictionary<ClothingSlot, ClothingMaskLayerData> layers,
            out string error)
        {
            layers = new Dictionary<ClothingSlot, ClothingMaskLayerData>();
            error = null;
            if (data == null)
            {
                return true;
            }

            if (data.data == null || !data.data.ContainsKey(PayloadKey))
            {
                error = "Extended data has no Payload entry.";
                return false;
            }

            byte[] payload = data.data[PayloadKey] as byte[];
            return CardDataSerializer.TryDeserialize(payload, out layers, out error);
        }

        public static PluginData ReadFromClothes(ChaFileClothes clothes)
        {
            if (clothes == null)
            {
                return null;
            }

            PluginData data;
            Extensions.TryGetExtendedDataById(clothes, BodyMaskLayersPlugin.PluginGuid, out data);
            return data;
        }

        public static void WriteToClothes(ChaFileClothes clothes, IEnumerable<ClothingMaskLayerData> layers)
        {
            if (clothes == null)
            {
                return;
            }

            List<ClothingMaskLayerData> materialized = new List<ClothingMaskLayerData>();
            foreach (ClothingMaskLayerData layer in layers)
            {
                if (layer != null && layer.OriginalPngBytes != null && layer.OriginalPngBytes.Length != 0)
                {
                    materialized.Add(layer);
                }
            }

            Extensions.SetExtendedDataById(
                clothes,
                BodyMaskLayersPlugin.PluginGuid,
                materialized.Count == 0 ? null : CreatePluginData(materialized));
        }
    }
}
