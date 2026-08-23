using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal static class SideloaderLegacyManifestSourceReader
    {
        public static IList<LegacyManifestSource> Read()
        {
            BodyMaskPerformanceMetrics.Increment(
                PerformanceCounter.GlobalSideloaderScans);
            Type sideloaderType = Type.GetType(
                "Sideloader.Sideloader, Sideloader",
                false);
            if (sideloaderType == null)
            {
                return new LegacyManifestSource[0];
            }

            FieldInfo manifestsField = sideloaderType.GetField(
                "Manifests",
                BindingFlags.Public | BindingFlags.Static);
            IDictionary manifests = manifestsField == null
                ? null
                : manifestsField.GetValue(null) as IDictionary;
            if (manifests == null)
            {
                return new LegacyManifestSource[0];
            }

            IDictionary archives = null;
            FieldInfo archivesField = sideloaderType.GetField(
                "ZipArchives",
                BindingFlags.Public | BindingFlags.Static);
            if (archivesField != null)
            {
                archives = archivesField.GetValue(null) as IDictionary;
            }

            List<LegacyManifestSource> result =
                new List<LegacyManifestSource>(manifests.Count);
            foreach (DictionaryEntry entry in manifests)
            {
                object manifest = entry.Value;
                if (manifest == null)
                {
                    continue;
                }

                Type type = manifest.GetType();
                string guid = ReadStringMember(type, manifest, "GUID") ??
                              ReadStringMember(type, manifest, "guid") ??
                              entry.Key as string;
                string xml = ReadStringMember(type, manifest, "ManifestString") ??
                             ReadStringMember(type, manifest, "manifestString");
                if (string.IsNullOrEmpty(xml))
                {
                    object document = ReadMember(type, manifest, "manifestDocument") ??
                                      ReadMember(type, manifest, "ManifestDocument");
                    XmlDocument xmlDocument = document as XmlDocument;
                    if (xmlDocument != null)
                    {
                        xml = xmlDocument.OuterXml;
                    }
                    else if (document != null)
                    {
                        PropertyInfo outerXml = document.GetType().GetProperty(
                            "OuterXml",
                            BindingFlags.Public | BindingFlags.Instance);
                        xml = outerXml == null
                            ? document.ToString()
                            : outerXml.GetValue(document, null) as string;
                    }
                }

                if (string.IsNullOrEmpty(xml) ||
                    xml.IndexOf("<ChaAlphaMask", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                string archivePath = archives == null || guid == null
                    ? null
                    : archives[guid] as string;
                FileInfo archive = !string.IsNullOrEmpty(archivePath)
                    ? new FileInfo(archivePath)
                    : null;
                result.Add(new LegacyManifestSource
                {
                    ModGuid = guid,
                    ArchivePath = archivePath,
                    ArchiveLength = archive != null && archive.Exists ? archive.Length : 0,
                    ArchiveLastWriteUtcTicks = archive != null && archive.Exists
                        ? archive.LastWriteTimeUtc.Ticks
                        : 0,
                    ManifestCrc = ComputeCrc32(xml),
                    ManifestXml = xml
                });
            }

            result.Sort(delegate(LegacyManifestSource left, LegacyManifestSource right)
            {
                return string.Compare(
                    left.ArchivePath ?? left.ModGuid,
                    right.ArchivePath ?? right.ModGuid,
                    StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        internal static uint ComputeCrc32(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
            uint crc = 0xffffffffu;
            for (int index = 0; index < bytes.Length; index++)
            {
                crc ^= bytes[index];
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1u) != 0
                        ? (crc >> 1) ^ 0xedb88320u
                        : crc >> 1;
                }
            }

            return ~crc;
        }

        private static object ReadMember(
            Type type,
            object instance,
            string name)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(instance);
        }

        private static string ReadStringMember(
            Type type,
            object instance,
            string name)
        {
            return ReadMember(type, instance, name) as string;
        }
    }
}
