using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public delegate int LegacyResolvedItemIdResolver(int originalItemId, int category, string modGuid);

    public static class LegacyManifestParser
    {
        public const int ParserVersion = 1;

        public static IList<LegacyMaskDescriptor> Parse(
            LegacyManifestSource source,
            LegacyResolvedItemIdResolver idResolver,
            out int rejectedEntries)
        {
            rejectedEntries = 0;
            List<LegacyMaskDescriptor> result = new List<LegacyMaskDescriptor>();
            if (source == null || string.IsNullOrEmpty(source.ManifestXml))
            {
                return result;
            }

            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            document.LoadXml(source.ManifestXml);
            XmlElement root = document.DocumentElement;
            XmlElement section = FindDirectChild(root, "ChaAlphaMask");
            if (root == null || section == null)
            {
                return result;
            }

            string manifestGuid = FirstNonEmpty(source.ModGuid, ReadDirectChild(root, "guid"));
            int sourceOrder = 0;
            for (XmlNode node = section.FirstChild; node != null; node = node.NextSibling)
            {
                XmlElement mask = node as XmlElement;
                if (mask == null || !NameEquals(mask, "mask"))
                {
                    continue;
                }

                LegacyMaskDescriptor descriptor;
                if (!TryParseMask(mask, manifestGuid, source, idResolver, sourceOrder, out descriptor))
                {
                    rejectedEntries++;
                    continue;
                }

                result.Add(descriptor);
                sourceOrder++;
            }

            return result;
        }

        private static bool TryParseMask(
            XmlElement mask,
            string fallbackGuid,
            LegacyManifestSource source,
            LegacyResolvedItemIdResolver idResolver,
            int sourceOrder,
            out LegacyMaskDescriptor descriptor)
        {
            descriptor = null;
            int category;
            int originalItemId;
            if (!TryReadInt(mask, "category", out category) ||
                !TryReadInt(mask, "id", out originalItemId))
            {
                return false;
            }

            string pngPath = ReadDirectChild(mask, "pngBodyMaskPath");
            string bundlePath = ReadDirectChild(mask, "abPath");
            string assetName = ReadDirectChild(mask, "abBodyMaskName");
            bool hasPng = !string.IsNullOrEmpty(pngPath);
            bool hasBundle = !string.IsNullOrEmpty(bundlePath) && !string.IsNullOrEmpty(assetName);
            if (!hasPng && !hasBundle)
            {
                return false;
            }

            string guid = FirstNonEmpty(ReadDirectChild(mask, "guid"), fallbackGuid);
            int resolvedItemId = originalItemId;
            if (idResolver != null)
            {
                resolvedItemId = idResolver(originalItemId, category, guid);
            }

            descriptor = new LegacyMaskDescriptor();
            descriptor.SourceOrder = sourceOrder;
            descriptor.ModGuid = guid;
            descriptor.ArchivePath = source.ArchivePath;
            descriptor.Category = category;
            descriptor.BotMask = ReadBool(mask, "botMask", false);
            ClothingSlot parsedSlot;
            if (!TrySlotFromCategory(category, descriptor.BotMask, out parsedSlot))
            {
                return false;
            }

            descriptor.Slot = parsedSlot;
            descriptor.OriginalItemId = originalItemId;
            descriptor.ResolvedItemId = resolvedItemId;
            descriptor.ObjectOption01 = ReadNullableBool(mask, "objOpt01");
            descriptor.ObjectOption02 = ReadNullableBool(mask, "objOpt02");
            descriptor.PngPath = !hasBundle && hasPng ? NormalizePngPath(pngPath) : null;
            descriptor.AssetBundlePath = hasBundle ? bundlePath.Trim() : null;
            descriptor.PrefabName = ReadDirectChild(mask, "prefab");
            descriptor.MaskAssetName = hasBundle ? assetName.Trim() : null;
            descriptor.MetadataFingerprint = descriptor.BuildFingerprint(
                GradientHandlingMode.PreserveContinuous,
                null);
            return true;
        }

        private static bool TrySlotFromCategory(int category, bool botMask, out ClothingSlot slot)
        {
            if (botMask && category == 105)
            {
                slot = ClothingSlot.Bottom;
                return true;
            }

            if (botMask && category == 107)
            {
                slot = ClothingSlot.Shorts;
                return true;
            }

            int value = category - 105;
            if (value < (int)ClothingSlot.Bottom || value > (int)ClothingSlot.IndoorShoes)
            {
                slot = ClothingSlot.Top;
                return false;
            }

            slot = (ClothingSlot)value;
            return true;
        }

        private static string NormalizePngPath(string value)
        {
            value = (value ?? string.Empty).Trim().Replace('\\', '/');
            return value.StartsWith("abdata/", StringComparison.OrdinalIgnoreCase)
                ? value
                : "abdata/" + value.TrimStart('/');
        }

        private static bool TryReadInt(XmlElement parent, string name, out int value)
        {
            return int.TryParse(
                ReadDirectChild(parent, name),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static bool ReadBool(XmlElement parent, string name, bool fallback)
        {
            bool value;
            return bool.TryParse(ReadDirectChild(parent, name), out value) ? value : fallback;
        }

        private static bool? ReadNullableBool(XmlElement parent, string name)
        {
            string text = ReadDirectChild(parent, name);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            bool value;
            return bool.TryParse(text, out value) ? (bool?)value : null;
        }

        private static XmlElement FindDirectChild(XmlElement parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (XmlNode node = parent.FirstChild; node != null; node = node.NextSibling)
            {
                XmlElement child = node as XmlElement;
                if (child != null && NameEquals(child, name))
                {
                    return child;
                }
            }

            return null;
        }

        private static string ReadDirectChild(XmlElement parent, string name)
        {
            XmlElement child = FindDirectChild(parent, name);
            return child == null ? null : child.InnerText.Trim();
        }

        private static bool NameEquals(XmlElement element, string name)
        {
            return string.Equals(element.LocalName, name, StringComparison.Ordinal);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrEmpty(first) ? second : first;
        }
    }
}
