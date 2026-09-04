using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class CardDataSerializer
    {
        public const int Schema1Version = 1;
        public const int SchemaVersion = 2;
        public const int MaximumLayerCount = 9;
        public const int MaximumStringBytes = 4096;
        public const int MaximumProviderIdBytes = 256;
        public const int MaximumFingerprintBytes = 512;
        public const int MaximumSerializedLayerBytes =
            PortableMaskFormatLimits.MaximumPngBytes + (64 * 1024);

        // The format identifier remains stable; the following integer selects the schema reader.
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
                    WriteLayerRecord(writer, materialized[i]);
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
                    if (schemaVersion != Schema1Version && schemaVersion != SchemaVersion)
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
                        ClothingMaskLayerData layer = schemaVersion == Schema1Version
                            ? ReadSchema1Layer(reader)
                            : ReadLayerRecord(reader);
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

        private static void WriteLayerRecord(BinaryWriter writer, ClothingMaskLayerData layer)
        {
            ValidateLayerForWrite(layer);

            long lengthPosition = writer.BaseStream.Position;
            writer.Write(0);
            long recordStart = writer.BaseStream.Position;
            WriteCurrentLayer(writer, layer);
            long recordEnd = writer.BaseStream.Position;
            long recordLength = recordEnd - recordStart;
            if (recordLength <= 0 || recordLength > MaximumSerializedLayerBytes)
            {
                throw new IOException("A serialized layer record exceeds the safe length limit.");
            }

            writer.BaseStream.Position = lengthPosition;
            writer.Write((int)recordLength);
            writer.BaseStream.Position = recordEnd;
        }

        private static void WriteCurrentLayer(BinaryWriter writer, ClothingMaskLayerData layer)
        {
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

            WriteString(writer, layer.Hash, MaximumStringBytes, "hash");
            WriteString(
                writer,
                layer.CreatedWithPluginVersion,
                MaximumStringBytes,
                "created-with version");
            WriteString(
                writer,
                layer.LastValidationResult,
                MaximumStringBytes,
                "validation result");
            writer.Write(layer.BoundItemIdentity != null);
            if (layer.BoundItemIdentity != null)
            {
                ClothingItemIdentity identity = layer.BoundItemIdentity;
                writer.Write((int)identity.Slot);
                writer.Write(identity.Category);
                writer.Write(identity.LocalItemId);
                writer.Write(identity.OriginalItemId);
                WriteString(writer, identity.SideloaderGuid, MaximumStringBytes, "Sideloader GUID");
                WriteString(writer, identity.DisplayName, MaximumStringBytes, "display name");
            }

            writer.Write((int)layer.SourceContract);
            writer.Write((int)layer.GradientHandlingMode);
            WriteString(
                writer,
                layer.SourceProviderId,
                MaximumProviderIdBytes,
                "source provider ID");
            WriteString(
                writer,
                layer.SourceFingerprint,
                MaximumFingerprintBytes,
                "source fingerprint");
            WriteString(writer, layer.SourceAsset, MaximumStringBytes, "source asset");

            writer.Write(layer.OriginalPngBytes.Length);
            writer.Write(layer.OriginalPngBytes);
        }

        private static ClothingMaskLayerData ReadLayerRecord(BinaryReader reader)
        {
            long payloadEnd = reader.BaseStream.Length;
            int recordLength = ReadInt32(reader, payloadEnd, "layer record length");
            if (recordLength <= 0 || recordLength > MaximumSerializedLayerBytes ||
                recordLength > payloadEnd - reader.BaseStream.Position)
            {
                throw new IOException("Invalid serialized layer record length.");
            }

            long recordEnd = reader.BaseStream.Position + recordLength;
            ClothingMaskLayerData layer = new ClothingMaskLayerData();
            layer.Slot = (ClothingSlot)ReadInt32(reader, recordEnd, "slot");
            layer.Enabled = ReadBoolean(reader, recordEnd, "enabled flag");
            layer.Width = ReadInt32(reader, recordEnd, "width");
            layer.Height = ReadInt32(reader, recordEnd, "height");
            layer.ColorFormatVersion = ReadInt32(reader, recordEnd, "color format version");
            bool hasStatePolicy = ReadBoolean(reader, recordEnd, "state-policy flag");
            if (hasStatePolicy)
            {
                int statePolicy = ReadInt32(reader, recordEnd, "state policy");
                if (!IsValidUnknownStatePolicy(statePolicy))
                {
                    throw new IOException("Invalid optional state policy " + statePolicy + ".");
                }

                layer.OptionalStatePolicy = (UnknownStatePolicy)statePolicy;
            }

            layer.Hash = ReadString(reader, recordEnd, MaximumStringBytes, "hash");
            layer.CreatedWithPluginVersion = ReadString(
                reader,
                recordEnd,
                MaximumStringBytes,
                "created-with version");
            layer.LastValidationResult = ReadString(
                reader,
                recordEnd,
                MaximumStringBytes,
                "validation result");
            if (ReadBoolean(reader, recordEnd, "bound-identity flag"))
            {
                ClothingItemIdentity identity = new ClothingItemIdentity();
                int identitySlot = ReadInt32(reader, recordEnd, "identity slot");
                if (!IsValidSlot(identitySlot))
                {
                    throw new IOException("Unknown bound-identity clothing slot " + identitySlot + ".");
                }

                identity.Slot = (ClothingSlot)identitySlot;
                identity.Category = ReadInt32(reader, recordEnd, "identity category");
                identity.LocalItemId = ReadInt32(reader, recordEnd, "identity local item ID");
                identity.OriginalItemId = ReadInt32(reader, recordEnd, "identity original item ID");
                identity.SideloaderGuid = ReadString(
                    reader,
                    recordEnd,
                    MaximumStringBytes,
                    "Sideloader GUID");
                identity.DisplayName = ReadString(
                    reader,
                    recordEnd,
                    MaximumStringBytes,
                    "display name");
                layer.BoundItemIdentity = identity;
            }

            int sourceContract = ReadInt32(reader, recordEnd, "source contract");
            if (!IsValidSourceContract(sourceContract))
            {
                throw new IOException("Invalid mask source contract " + sourceContract + ".");
            }

            layer.SourceContract = (MaskSourceContract)sourceContract;
            int gradientHandlingMode = ReadInt32(reader, recordEnd, "gradient handling mode");
            if (!IsValidGradientHandlingMode(gradientHandlingMode))
            {
                throw new IOException("Invalid gradient handling mode " + gradientHandlingMode + ".");
            }

            layer.GradientHandlingMode = (GradientHandlingMode)gradientHandlingMode;
            layer.SourceProviderId = ReadString(
                reader,
                recordEnd,
                MaximumProviderIdBytes,
                "source provider ID");
            layer.SourceFingerprint = ReadString(
                reader,
                recordEnd,
                MaximumFingerprintBytes,
                "source fingerprint");
            layer.SourceAsset = ReadString(
                reader,
                recordEnd,
                MaximumStringBytes,
                "source asset");

            int pngLength = ReadInt32(reader, recordEnd, "PNG byte length");
            if (pngLength <= 0 || pngLength > PortableMaskFormatLimits.MaximumPngBytes ||
                pngLength > recordEnd - reader.BaseStream.Position)
            {
                throw new IOException("Invalid PNG byte length in serialized layer.");
            }

            layer.OriginalPngBytes = ReadBytes(reader, recordEnd, pngLength, "PNG bytes");
            if (reader.BaseStream.Position != recordEnd)
            {
                throw new IOException("Unexpected trailing bytes in serialized layer record.");
            }

            ValidateCurrentLayerAfterRead(layer);
            return layer;
        }

        private static ClothingMaskLayerData ReadSchema1Layer(BinaryReader reader)
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

            layer.Hash = ReadSchema1String(reader);
            layer.CreatedWithPluginVersion = ReadSchema1String(reader);
            layer.LastValidationResult = ReadSchema1String(reader);
            if (reader.ReadBoolean())
            {
                ClothingItemIdentity identity = new ClothingItemIdentity();
                identity.Slot = (ClothingSlot)reader.ReadInt32();
                identity.Category = reader.ReadInt32();
                identity.LocalItemId = reader.ReadInt32();
                identity.OriginalItemId = reader.ReadInt32();
                identity.SideloaderGuid = ReadSchema1String(reader);
                identity.DisplayName = ReadSchema1String(reader);
                layer.BoundItemIdentity = identity;
            }

            int pngLength = reader.ReadInt32();
            if (pngLength <= 0 || pngLength > PortableMaskFormatLimits.MaximumPngBytes ||
                pngLength > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new IOException("Invalid PNG byte length in serialized layer.");
            }

            layer.OriginalPngBytes = reader.ReadBytes(pngLength);
            if (layer.OriginalPngBytes.Length != pngLength)
            {
                throw new EndOfStreamException("Truncated PNG bytes in serialized layer.");
            }

            layer.SourceContract = MaskSourceContract.Native;
            layer.GradientHandlingMode = GradientHandlingMode.StrictCategorical;
            layer.SourceProviderId = null;
            layer.SourceFingerprint = null;
            layer.SourceAsset = null;
            ValidateSchema1LayerAfterRead(layer);
            return layer;
        }

        private static void ValidateLayerForWrite(ClothingMaskLayerData layer)
        {
            if (!IsValidSlot((int)layer.Slot))
            {
                throw new IOException("Unknown clothing slot " + (int)layer.Slot + ".");
            }

            ValidateCurrentLayerAfterRead(layer);
            if (layer.OptionalStatePolicy.HasValue &&
                !IsValidUnknownStatePolicy((int)layer.OptionalStatePolicy.Value))
            {
                throw new IOException("Invalid optional state policy.");
            }

            if (layer.BoundItemIdentity != null &&
                !IsValidSlot((int)layer.BoundItemIdentity.Slot))
            {
                throw new IOException("Unknown bound-identity clothing slot.");
            }

            if (!IsValidSourceContract((int)layer.SourceContract))
            {
                throw new IOException("Invalid mask source contract.");
            }

            if (!IsValidGradientHandlingMode((int)layer.GradientHandlingMode))
            {
                throw new IOException("Invalid gradient handling mode.");
            }

            if (layer.OriginalPngBytes == null || layer.OriginalPngBytes.Length == 0)
            {
                throw new IOException("A layer has no PNG bytes.");
            }

            if (layer.OriginalPngBytes.Length > PortableMaskFormatLimits.MaximumPngBytes)
            {
                throw new IOException("A layer exceeds the absolute PNG byte limit.");
            }
        }

        private static void ValidateCurrentLayerAfterRead(ClothingMaskLayerData layer)
        {
            if (!IsValidSlot((int)layer.Slot))
            {
                throw new IOException("Unknown clothing slot " + (int)layer.Slot + ".");
            }

            if (layer.Width <= 0 ||
                layer.Width > PortableMaskFormatLimits.MaximumDimension ||
                layer.Height <= 0 ||
                layer.Height > PortableMaskFormatLimits.MaximumDimension)
            {
                throw new IOException("Invalid serialized mask resolution.");
            }

            if (layer.ColorFormatVersion <= 0)
            {
                throw new IOException("Invalid mask color format version.");
            }

            if (layer.OptionalStatePolicy.HasValue &&
                !IsValidUnknownStatePolicy((int)layer.OptionalStatePolicy.Value))
            {
                throw new IOException("Invalid optional state policy.");
            }

            if (layer.BoundItemIdentity != null &&
                !IsValidSlot((int)layer.BoundItemIdentity.Slot))
            {
                throw new IOException("Unknown bound-identity clothing slot.");
            }

            if (!IsValidSourceContract((int)layer.SourceContract) ||
                !IsValidGradientHandlingMode((int)layer.GradientHandlingMode))
            {
                throw new IOException("Invalid mask interpretation metadata.");
            }
        }

        private static void ValidateSchema1LayerAfterRead(ClothingMaskLayerData layer)
        {
            if (!IsValidSlot((int)layer.Slot))
            {
                throw new IOException("Unknown clothing slot " + (int)layer.Slot + ".");
            }

            if (layer.Width <= 0 ||
                layer.Width > PortableMaskFormatLimits.MaximumDimension ||
                layer.Height <= 0 ||
                layer.Height > PortableMaskFormatLimits.MaximumDimension)
            {
                throw new IOException("Invalid serialized mask resolution.");
            }

            if (layer.ColorFormatVersion <= 0)
            {
                throw new IOException("Invalid mask color format version.");
            }

            if (layer.OptionalStatePolicy.HasValue &&
                !IsValidUnknownStatePolicy((int)layer.OptionalStatePolicy.Value))
            {
                throw new IOException("Invalid optional state policy.");
            }

            if (layer.BoundItemIdentity != null &&
                !IsValidSlot((int)layer.BoundItemIdentity.Slot))
            {
                throw new IOException("Unknown bound-identity clothing slot.");
            }
        }

        private static void WriteString(
            BinaryWriter writer,
            string value,
            int maximumBytes,
            string fieldName)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            byte[] bytes = Utf8.GetBytes(value);
            if (bytes.Length > maximumBytes)
            {
                throw new IOException(
                    "The serialized " + fieldName + " string exceeds the safe length limit.");
            }

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(
            BinaryReader reader,
            long recordEnd,
            int maximumBytes,
            string fieldName)
        {
            int length = ReadInt32(reader, recordEnd, fieldName + " string length");
            if (length == -1)
            {
                return null;
            }

            if (length < 0 || length > maximumBytes ||
                length > recordEnd - reader.BaseStream.Position)
            {
                throw new IOException("Invalid serialized " + fieldName + " string length.");
            }

            byte[] bytes = ReadBytes(reader, recordEnd, length, fieldName);
            return Utf8.GetString(bytes);
        }

        private static string ReadSchema1String(BinaryReader reader)
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

        private static int ReadInt32(BinaryReader reader, long end, string fieldName)
        {
            EnsureAvailable(reader, end, 4, fieldName);
            return reader.ReadInt32();
        }

        private static bool ReadBoolean(BinaryReader reader, long end, string fieldName)
        {
            EnsureAvailable(reader, end, 1, fieldName);
            byte value = reader.ReadByte();
            if (value > 1)
            {
                throw new IOException("Invalid serialized " + fieldName + ".");
            }

            return value != 0;
        }

        private static byte[] ReadBytes(BinaryReader reader, long end, int length, string fieldName)
        {
            EnsureAvailable(reader, end, length, fieldName);
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Truncated serialized " + fieldName + ".");
            }

            return bytes;
        }

        private static void EnsureAvailable(BinaryReader reader, long end, int length, string fieldName)
        {
            if (length < 0 || reader.BaseStream.Position > end ||
                length > end - reader.BaseStream.Position)
            {
                throw new EndOfStreamException("Truncated serialized " + fieldName + ".");
            }
        }

        private static bool IsValidSlot(int value)
        {
            return value >= 0 && value < MaximumLayerCount;
        }

        private static bool IsValidUnknownStatePolicy(int value)
        {
            return value >= (int)UnknownStatePolicy.NoContribution &&
                   value <= (int)UnknownStatePolicy.PreserveLastKnown;
        }

        private static bool IsValidSourceContract(int value)
        {
            return value == (int)MaskSourceContract.Native ||
                   value == (int)MaskSourceContract.ExternalRgbStateCoverage;
        }

        private static bool IsValidGradientHandlingMode(int value)
        {
            return value == (int)GradientHandlingMode.Auto ||
                   value == (int)GradientHandlingMode.PreserveContinuous ||
                   value == (int)GradientHandlingMode.StrictCategorical;
        }
    }
}
