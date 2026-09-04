using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class ExternalMaskProvider
    {
        private const int NegativeCacheEntries = 4096;
        private const int CacheLimitMegabytes = 128;
        private const string SourceProviderGuid = ExternalMaskDescriptor.ProviderIdValue;
        private static readonly object SyncRoot = new object();
        private static readonly ExternalClothingMaskCatalog Catalog = new ExternalClothingMaskCatalog();
        private static BoundedLruCache<string, ExternalResolvedMask> _compiledCache;
        private static GenerationNegativeCache<string> _negativeCache;
        private static bool _initialized;
        private static string _lastIndexError;
        private static ExternalIndexRefreshResult _lastRefresh;

        public static bool IsSourceProviderInstalled
        {
            get { return Chainloader.PluginInfos.ContainsKey(SourceProviderGuid); }
        }

        public static bool DirectCompatibilityActive
        {
            get
            {
                return !IsSourceProviderInstalled;
            }
        }

        public static int CatalogGeneration
        {
            get { return Catalog.Generation; }
        }

        public static int CatalogEntryCount
        {
            get { return Catalog.DescriptorCount; }
        }

        public static string LastIndexError
        {
            get { return _lastIndexError; }
        }

        public static string BuildIndexSummary()
        {
            ExternalIndexRefreshResult refresh = _lastRefresh;
            return string.Format(
                "compatibleProvider={0}; sourceProviderInstalled={1}; catalogGeneration={2}; " +
                "descriptors={3}; manifests={4}; reused={5}; parsed={6}; rejected={7}; " +
                "compiledCache={8} entries/{9} bytes (cap {10} MB); negativeCache={11}; error={12}",
                DirectCompatibilityActive,
                IsSourceProviderInstalled,
                Catalog.Generation,
                Catalog.DescriptorCount,
                refresh == null ? 0 : refresh.ManifestCount,
                refresh == null ? 0 : refresh.ReusedManifestCount,
                refresh == null ? 0 : refresh.ParsedManifestCount,
                refresh == null ? 0 : refresh.RejectedEntryCount,
                _compiledCache == null ? 0 : _compiledCache.Count,
                _compiledCache == null ? 0L : _compiledCache.CurrentBytes,
                CacheLimitMegabytes,
                _negativeCache == null ? 0 : _negativeCache.Count,
                string.IsNullOrEmpty(_lastIndexError) ? "none" : _lastIndexError);
        }

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                BodyMaskPerformanceMetrics.SetEnabled(
                    BodyMaskLayersPlugin.Settings.DebugLogging.Value);
                RecreateCaches();
                _initialized = true;
            }

            if (DirectCompatibilityActive)
            {
                RefreshIndex(false);
            }
        }

        public static ExternalIndexRefreshResult RefreshIndex(bool forceFullRescan)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.ExternalIndexScans);
                _lastIndexError = null;
                try
                {
                    IList<ExternalManifestSource> sources =
                        SideloaderExternalManifestSourceReader.Read();
                    ExternalIndexRefreshResult result = Catalog.Refresh(
                        sources,
                        ClothingItemIdentityResolver.ResolveExternalLocalItemId,
                        forceFullRescan);
                    if (result.Changed)
                    {
                        _compiledCache.Clear();
                        _negativeCache.BeginGeneration(result.Generation);
                        BodyMaskCharacterController.NotifyExternalIndexChanged();
                    }

                    BodyMaskPerformanceMetrics.Add(
                        PerformanceCounter.ManifestParses,
                        result.ParsedManifestCount);
                    _lastRefresh = result;
                    return result;
                }
                catch (Exception exception)
                {
                    _lastIndexError = exception.Message;
                    BodyMaskLayersPlugin.Log.LogWarning(
                        "Compatible mask index refresh failed: " + exception.Message);
                    return _lastRefresh ?? new ExternalIndexRefreshResult
                    {
                        Generation = Catalog.Generation,
                        DescriptorCount = Catalog.DescriptorCount
                    };
                }
            }
        }

        public static bool TryResolveForCharacter(
            ChaControl character,
            ClothingSlot slot,
            out ExternalResolvedMask resolved,
            out string status)
        {
            resolved = null;
            status = null;
            if (!DirectCompatibilityActive)
            {
                status = "A separate body-mask provider is active.";
                return false;
            }

            bool effectiveHighPoly = character != null &&
                                     ExternalCharacterStateAdapter.IsEffectiveHighPoly(character);
            if (character == null || slot == ClothingSlot.Top || !effectiveHighPoly)
            {
                status = character != null && !effectiveHighPoly
                    ? "Compatible body masks require a high-poly character."
                    : "No supported compatible mask slot.";
                return false;
            }

            EnsureInitialized();
            if (Catalog.DescriptorCount == 0)
            {
                status = "The compatible mask metadata catalog is empty.";
                return false;
            }

            ExternalMaskBindingQuery query;
            if (!ExternalCharacterStateAdapter.TryBuildBindingQuery(
                    character,
                    slot,
                    out query))
            {
                status = "The slot has no resolvable equipped item.";
                return false;
            }

            string queryKey = Catalog.Generation + "|" + query.BuildCacheKey();
            lock (SyncRoot)
            {
                if (_negativeCache.Contains(queryKey, Catalog.Generation))
                {
                    status = "No compatible mask metadata for the current item (cached).";
                    return false;
                }

                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.ProviderResolutions);
                ExternalMaskDescriptor descriptor;
                if (!Catalog.TryResolve(query, out descriptor))
                {
                    BodyMaskPerformanceMetrics.Increment(PerformanceCounter.MetadataCacheMisses);
                    _negativeCache.Add(queryKey, Catalog.Generation);
                    status = "No compatible mask metadata for the current item.";
                    return false;
                }

                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.MetadataCacheHits);
                descriptor = descriptor.CloneForSlot(slot);
                string cacheKey = Catalog.Generation + "|" + descriptor.MetadataFingerprint +
                                  "|" + (int)GradientHandlingMode.PreserveContinuous + "|" + (int)slot;
                if (_compiledCache.TryGetValue(cacheKey, out resolved))
                {
                    status = "Resolved from the compatible mask cache.";
                    return true;
                }

                string error;
                if (!ExternalMaskTextureCompiler.TryCompile(
                        descriptor,
                        out resolved,
                        out error))
                {
                    status = error;
                    return false;
                }

                resolved.CatalogGeneration = Catalog.Generation;
                _compiledCache.Put(cacheKey, resolved, EstimateCacheBytes(resolved));
                status = "Compatible source loaded from " +
                         (descriptor.IsPng ? "Sideloader PNG." : "AssetBundle texture.");
                return true;
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (_compiledCache != null)
                {
                    _compiledCache.Clear();
                }

                if (_negativeCache != null)
                {
                    _negativeCache.Clear();
                }

                _initialized = false;
                _lastRefresh = null;
                _lastIndexError = null;
                ExternalCharacterStateAdapter.Reset();
            }
        }

        private static long EstimateCacheBytes(ExternalResolvedMask value)
        {
            long bytes = Math.Max(1L, value.StorageBytes);
            bytes += 512;
            if (value.Fingerprint != null)
            {
                bytes += value.Fingerprint.Length * 2L;
            }

            return bytes;
        }

        private static long GetCacheLimitBytes()
        {
            return (long)CacheLimitMegabytes * 1024L * 1024L;
        }

        private static void RecreateCaches()
        {
            _compiledCache = new BoundedLruCache<string, ExternalResolvedMask>(
                GetCacheLimitBytes(),
                StringComparer.Ordinal,
                BodyMaskPerformanceMetrics.Counters);
            _negativeCache = new GenerationNegativeCache<string>(
                NegativeCacheEntries,
                StringComparer.Ordinal,
                BodyMaskPerformanceMetrics.Counters);
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }
    }
}
