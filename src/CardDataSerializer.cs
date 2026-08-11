using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class CardDataSerializer
    {
        public const int SchemaVersion = 1;
        public const int MaximumLayerCount = 9;
        public const int AbsoluteMaximumPngBytes = 32 * 1024 * 1024;
        private const int MaximumStringBytes = 4096;
        private static readonly byte[] Magic = { (byte)'B', (byte)'M', (byte)'L', (byte)'1' };
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Serialize(IEnumerable<ClothingMaskLayerData> layers)
        {
            if (layers == null)
            {
                throw new ArgumentNullException("layers");
            }

            List<ClothingMaskLayerData> materialized = new List<ClothingMaskLayerData>();
            foreach (ClothingMaskLayerData layer in layers)
            {
                if (layer != null)
                {
                    materialized.Add(layer);
                }
            }

            if (materialized.Count > MaximumLayerCount)
            {
                throw new IOException("Too many clothing mask layers.");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(SchemaVersion);
                writer.Write(materialized.Count);
                for (int i = 0; i < materialized.Count; i++)
                {
                    WriteLayer(writer, materialized[i]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryDeserialize(
            byte[] payload,
            out Dictionary<ClothingSlot, ClothingMaskLayerData> layers,
            out string error)
        {
            layers = new Dictionary<ClothingSlot, ClothingMaskLayerData>();
            error = null;
            if (payload == null || payload.Length < Magic.Length + 8)
            {
                error = "BodyMask Layers payload is missing or truncated.";
                return false;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(payload, false))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    for (int i = 0; i < Magic.Length; i++)
                    {
                        if (reader.ReadByte() != Magic[i])
                        {
                            error = "BodyMask Layers payload has an invalid magic value.";
                            return false;
                        }
                    }

                    int schemaVersion = reader.ReadInt32();
                    if (schemaVersion != SchemaVersion)
                    {
                        error = "Unsupported BodyMask Layers schema version " + schemaVersion + ".";
                        return false;
                    }

                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaximumLayerCount)
                    {
                        error = "BodyMask Layers payload has an invalid layer count.";
                        return false;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        ClothingMaskLayerData layer = ReadLayer(reader);
                        int slot = (int)layer.Slot;
                        if (slot < 0 || slot >= MaximumLayerCount)
                        {
                            throw new IOException("Unknown clothing slot " + slot + ".");
                        }

                        layers[layer.Slot] = layer;
                    }

                    if (stream.Position != stream.Length)
                    {
                        throw new IOException("Unexpected trailing bytes in BodyMask Layers payload.");
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                layers.Clear();
                error = "BodyMask Layers payload could not be read: " + exception.Message;
                return false;
            }
        }

        private static void WriteLayer(BinaryWriter writer, ClothingMaskLayerData layer)
        {
            if (layer.OriginalPngBytes == null || layer.OriginalPngBytes.Length == 0)
            {
                throw new IOException("A layer has no PNG bytes.");
            }

            if (layer.OriginalPngBytes.Length > AbsoluteMaximumPngBytes)
            {
                throw new IOException("A layer exceeds the absolute PNG byte limit.");
            }

            writer.Write((int)layer.Slot);
            writer.Write(layer.Enabled);
            writer.Write(layer.Width);
            writer.Write(layer.Height);
            writer.Write(layer.ColorFormatVersion);
            writer.Write(layer.OptionalStatePolicy.HasValue);
            if (layer.OptionalStatePolicy.HasValue)
            {
                writer.Write((int)layer.OptionalStatePolicy.Value);
            }

            WriteString(writer, layer.Hash);
            WriteString(writer, layer.CreatedWithPluginVersion);
            WriteString(writer, layer.LastValidationResult);
            writer.Write(layer.BoundItemIdentity != null);
            if (layer.BoundItemIdentity != null)
            {
                ClothingItemIdentity identity = layer.BoundItemIdentity;
                writer.Write((int)identity.Slot);
                writer.Write(identity.Category);
                writer.Write(identity.LocalItemId);
                writer.Write(identity.OriginalItemId);
                WriteString(writer, identity.SideloaderGuid);
                WriteString(writer, identity.DisplayName);
            }

            writer.Write(layer.OriginalPngBytes.Length);
            writer.Write(layer.OriginalPngBytes);
        }

        private static ClothingMaskLayerData ReadLayer(BinaryReader reader)
        {
            ClothingMaskLayerData layer = new ClothingMaskLayerData();
            layer.Slot = (ClothingSlot)reader.ReadInt32();
            layer.Enabled = reader.ReadBoolean();
            layer.Width = reader.ReadInt32();
            layer.Height = reader.ReadInt32();
            layer.ColorFormatVersion = reader.ReadInt32();
            if (reader.ReadBoolean())
            {
                layer.OptionalStatePolicy = (UnknownStatePolicy)reader.ReadInt32();
            }

            layer.Hash = ReadString(reader);
            layer.CreatedWithPluginVersion = ReadString(reader);
            layer.LastValidationResult = ReadString(reader);
            if (reader.ReadBoolean())
            {
                ClothingItemIdentity identity = new ClothingItemIdentity();
                identity.Slot = (ClothingSlot)reader.ReadInt32();
                identity.Category = reader.ReadInt32();
                identity.LocalItemId = reader.ReadInt32();
                identity.OriginalItemId = reader.ReadInt32();
                identity.SideloaderGuid = ReadString(reader);
                identity.DisplayName = ReadString(reader);
                layer.BoundItemIdentity = identity;
            }

            int pngLength = reader.ReadInt32();
            if (pngLength <= 0 || pngLength > AbsoluteMaximumPngBytes ||
                pngLength > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new IOException("Invalid PNG byte length in serialized layer.");
            }

            layer.OriginalPngBytes = reader.ReadBytes(pngLength);
            if (layer.OriginalPngBytes.Length != pngLength)
            {
                throw new EndOfStreamException("Truncated PNG bytes in serialized layer.");
            }

            return layer;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            byte[] bytes = Utf8.GetBytes(value);
            if (bytes.Length > MaximumStringBytes)
            {
                throw new IOException("A serialized string exceeds the safe length limit.");
            }

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == -1)
            {
                return null;
            }

            if (length < 0 || length > MaximumStringBytes ||
                length > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new IOException("Invalid serialized string length.");
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Truncated serialized string.");
            }

            return Utf8.GetString(bytes);
        }
    }
}
