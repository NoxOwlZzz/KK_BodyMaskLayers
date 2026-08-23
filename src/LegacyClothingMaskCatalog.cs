using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class LegacyClothingMaskCatalog
    {
        private sealed class ManifestCacheEntry
        {
            public string ChangeKey;
            public IList<LegacyMaskDescriptor> Descriptors;
            public int RejectedEntries;
        }

        private readonly Dictionary<string, ManifestCacheEntry> _manifestCache =
            new Dictionary<string, ManifestCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _manifestOrder = new List<string>();
        private readonly Dictionary<string, List<LegacyMaskDescriptor>> _lookup =
            new Dictionary<string, List<LegacyMaskDescriptor>>(StringComparer.Ordinal);
        private int _generation;

        public int Generation
        {
            get { return _generation; }
        }

        public int DescriptorCount { get; private set; }

        internal int LookupBuildCount { get; private set; }

        public LegacyIndexRefreshResult Refresh(
            IList<LegacyManifestSource> sources,
            LegacyResolvedItemIdResolver resolver,
            bool forceFullRescan)
        {
            sources = sources ?? new LegacyManifestSource[0];
            Dictionary<string, ManifestCacheEntry> next =
                new Dictionary<string, ManifestCacheEntry>(StringComparer.OrdinalIgnoreCase);
            List<string> nextOrder = new List<string>(sources.Count);
            LegacyIndexRefreshResult result = new LegacyIndexRefreshResult();
            bool changed = forceFullRescan;

            for (int index = 0; index < sources.Count; index++)
            {
                LegacyManifestSource source = sources[index];
                if (source == null)
                {
                    continue;
                }

                string cacheId = BuildCacheId(source, index);
                if (next.ContainsKey(cacheId))
                {
                    cacheId += "|duplicate:" + index;
                }

                nextOrder.Add(cacheId);
                string changeKey = LegacyManifestParser.ParserVersion + "|" + source.BuildChangeKey();
                ManifestCacheEntry cached;
                if (!forceFullRescan &&
                    _manifestCache.TryGetValue(cacheId, out cached) &&
                    string.Equals(cached.ChangeKey, changeKey, StringComparison.Ordinal))
                {
                    next[cacheId] = cached;
                    result.ReusedManifestCount++;
                    continue;
                }

                int rejected;
                IList<LegacyMaskDescriptor> descriptors;
                try
                {
                    descriptors = LegacyManifestParser.Parse(source, resolver, out rejected);
                }
                catch
                {
                    descriptors = new LegacyMaskDescriptor[0];
                    rejected = 1;
                }

                next[cacheId] = new ManifestCacheEntry
                {
                    ChangeKey = changeKey,
                    Descriptors = descriptors,
                    RejectedEntries = rejected
                };
                result.ParsedManifestCount++;
                changed = true;
            }

            if (!changed && HasSameManifestOrder(nextOrder))
            {
                PopulateUnchangedResult(result);
                return result;
            }

            if (!changed)
            {
                changed = true;
            }

            _manifestCache.Clear();
            foreach (KeyValuePair<string, ManifestCacheEntry> pair in next)
            {
                _manifestCache.Add(pair.Key, pair.Value);
            }

            _manifestOrder.Clear();
            _manifestOrder.AddRange(nextOrder);

            RebuildLookup(result);
            if (changed)
            {
                _generation++;
            }

            result.Changed = changed;
            result.Generation = _generation;
            result.ManifestCount = _manifestCache.Count;
            result.DescriptorCount = DescriptorCount;
            return result;
        }

        public bool TryResolve(LegacyMaskBindingQuery query, out LegacyMaskDescriptor descriptor)
        {
            descriptor = null;
            if (query == null || query.ResolvedItemId <= 0)
            {
                return false;
            }

            List<LegacyMaskDescriptor> candidates;
            if (!_lookup.TryGetValue(query.BuildLookupKey(), out candidates))
            {
                return false;
            }

            LegacyMaskDescriptor fallback = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                LegacyMaskDescriptor candidate = candidates[i];
                bool sharedShoesCategory = candidate.Category == 112 &&
                    (query.Slot == ClothingSlot.IndoorShoes ||
                     query.Slot == ClothingSlot.OutdoorShoes);
                bool optionsMatch = query.BotMask ||
                    (candidate.ObjectOption01 == query.ObjectOption01 &&
                     candidate.ObjectOption02 == query.ObjectOption02);
                if ((!sharedShoesCategory && candidate.Slot != query.Slot) ||
                    candidate.BotMask != query.BotMask || !optionsMatch)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                bool guidMatches = string.IsNullOrEmpty(query.SideloaderGuid) ||
                    string.Equals(
                        candidate.ModGuid,
                        query.SideloaderGuid,
                        StringComparison.OrdinalIgnoreCase);
                bool originalMatches = query.OriginalItemId <= 0 ||
                    candidate.OriginalItemId <= 0 ||
                    query.OriginalItemId == candidate.OriginalItemId;
                if (guidMatches && originalMatches &&
                    (!string.IsNullOrEmpty(query.SideloaderGuid) || query.OriginalItemId > 0))
                {
                    descriptor = candidate;
                    return true;
                }
            }

            descriptor = fallback;
            return descriptor != null;
        }

        public IList<LegacyMaskDescriptor> GetAllDescriptors()
        {
            List<LegacyMaskDescriptor> result = new List<LegacyMaskDescriptor>(DescriptorCount);
            for (int orderIndex = 0; orderIndex < _manifestOrder.Count; orderIndex++)
            {
                ManifestCacheEntry manifest = _manifestCache[_manifestOrder[orderIndex]];
                for (int i = 0; i < manifest.Descriptors.Count; i++)
                {
                    result.Add(manifest.Descriptors[i]);
                }
            }

            return result;
        }

        private void RebuildLookup(LegacyIndexRefreshResult result)
        {
            LookupBuildCount++;
            _lookup.Clear();
            DescriptorCount = 0;
            for (int orderIndex = 0; orderIndex < _manifestOrder.Count; orderIndex++)
            {
                ManifestCacheEntry manifest = _manifestCache[_manifestOrder[orderIndex]];
                result.RejectedEntryCount += manifest.RejectedEntries;
                for (int i = 0; i < manifest.Descriptors.Count; i++)
                {
                    LegacyMaskDescriptor descriptor = manifest.Descriptors[i];
                    string key = descriptor.Category + ":" + descriptor.ResolvedItemId;
                    List<LegacyMaskDescriptor> bucket;
                    if (!_lookup.TryGetValue(key, out bucket))
                    {
                        bucket = new List<LegacyMaskDescriptor>();
                        _lookup.Add(key, bucket);
                    }

                    bucket.Add(descriptor);
                    DescriptorCount++;
                }
            }
        }

        private void PopulateUnchangedResult(LegacyIndexRefreshResult result)
        {
            result.Changed = false;
            result.Generation = _generation;
            result.ManifestCount = _manifestCache.Count;
            result.DescriptorCount = DescriptorCount;
            for (int orderIndex = 0; orderIndex < _manifestOrder.Count; orderIndex++)
            {
                result.RejectedEntryCount +=
                    _manifestCache[_manifestOrder[orderIndex]].RejectedEntries;
            }
        }

        private bool HasSameManifestOrder(IList<string> nextOrder)
        {
            if (_manifestOrder.Count != nextOrder.Count)
            {
                return false;
            }

            for (int i = 0; i < nextOrder.Count; i++)
            {
                if (!string.Equals(
                    _manifestOrder[i],
                    nextOrder[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildCacheId(LegacyManifestSource source, int sourceIndex)
        {
            if (!string.IsNullOrEmpty(source.ArchivePath))
            {
                return "archive:" + source.ArchivePath + "|guid:" + (source.ModGuid ?? string.Empty);
            }

            if (!string.IsNullOrEmpty(source.ModGuid))
            {
                return "guid:" + source.ModGuid;
            }

            return "anonymous:" + sourceIndex;
        }
    }
}
