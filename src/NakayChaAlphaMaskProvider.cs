using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class NakayChaAlphaMaskProvider
    {
        public const string PluginGuid = LegacyMaskDescriptor.ProviderIdValue;
        public const string AuditedVersion = "1.0.0";

        private const int NegativeCacheEntries = 4096;
        private static readonly object SyncRoot = new object();
        private static readonly LegacyClothingMaskCatalog Catalog = new LegacyClothingMaskCatalog();
        private static BoundedLruCache<string, LegacyResolvedMask> _compiledCache;
        private static GenerationNegativeCache<string> _negativeCache;
        private static bool _initialized;
        private static string _lastIndexError;
        private static LegacyIndexRefreshResult _lastRefresh;

        public static bool IsLegacyPluginInstalled
        {
            get { return Chainloader.PluginInfos.ContainsKey(PluginGuid); }
        }

        public static bool DirectCompatibilityActive
        {
            get
            {
                return BodyMaskLayersPlugin.Settings != null &&
                       LegacyCompatibilityPolicy.ShouldUseDirectProvider(
                           BodyMaskLayersPlugin.Settings.EnableNakayLegacyCompatibility.Value,
                           IsLegacyPluginInstalled);
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
            LegacyIndexRefreshResult refresh = _lastRefresh;
            return string.Format(
                "Nakay directProvider={0}; oldPluginInstalled={1}; catalogGeneration={2}; " +
                "descriptors={3}; manifests={4}; reused={5}; parsed={6}; rejected={7}; " +
                "compiledCache={8} entries/{9} bytes (limit {10}); negativeCache={11}; error={12}",
                DirectCompatibilityActive,
                IsLegacyPluginInstalled,
                Catalog.Generation,
                Catalog.DescriptorCount,
                refresh == null ? 0 : refresh.ManifestCount,
                refresh == null ? 0 : refresh.ReusedManifestCount,
                refresh == null ? 0 : refresh.ParsedManifestCount,
                refresh == null ? 0 : refresh.RejectedEntryCount,
                _compiledCache == null ? 0 : _compiledCache.Count,
                _compiledCache == null ? 0L : _compiledCache.CurrentBytes,
                _compiledCache == null ? 0L : _compiledCache.MaxBytes,
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
                    BodyMaskLayersPlugin.Settings.LegacyDiagnostics.Value);
                RecreateCaches();
                _initialized = true;
            }

            if (DirectCompatibilityActive &&
                BodyMaskLayersPlugin.Settings.LegacyIndexAutoRefresh.Value)
            {
                RefreshIndex(false);
            }
        }

        public static LegacyIndexRefreshResult RefreshIndex(bool forceFullRescan)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.LegacyIndexScans);
                _lastIndexError = null;
                try
                {
                    IList<LegacyManifestSource> sources =
                        SideloaderLegacyManifestSourceReader.Read();
                    LegacyIndexRefreshResult result = Catalog.Refresh(
                        sources,
                        ClothingItemIdentityResolver.ResolveLegacyLocalItemId,
                        forceFullRescan);
                    if (result.Changed)
                    {
                        _compiledCache.Clear();
                        _negativeCache.BeginGeneration(result.Generation);
                        BodyMaskCharacterController.NotifyLegacyIndexChanged();
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
                        "Nakay legacy mask index refresh failed safely: " + exception.Message);
                    return _lastRefresh ?? new LegacyIndexRefreshResult
                    {
                        Generation = Catalog.Generation,
                        DescriptorCount = Catalog.DescriptorCount
                    };
                }
            }
        }

        public static void ApplyConfigurationChange(bool cacheLimitChanged)
        {
            EnsureInitialized();
            bool shouldBuildInitialIndex;
            lock (SyncRoot)
            {
                BodyMaskPerformanceMetrics.SetEnabled(
                    BodyMaskLayersPlugin.Settings.LegacyDiagnostics.Value);
                if (cacheLimitChanged)
                {
                    _compiledCache.SetMemoryLimit(GetCacheLimitBytes());
                }

                _negativeCache.BeginGeneration(Catalog.Generation + 1L);
                _negativeCache.BeginGeneration(Catalog.Generation);
                shouldBuildInitialIndex = DirectCompatibilityActive &&
                                          BodyMaskLayersPlugin.Settings.LegacyIndexAutoRefresh.Value &&
                                          _lastRefresh == null;
            }

            if (shouldBuildInitialIndex)
            {
                RefreshIndex(false);
            }
        }

        public static bool TryResolveForCharacter(
            ChaControl character,
            ClothingSlot slot,
            out LegacyResolvedMask resolved,
            out string status)
        {
            resolved = null;
            status = null;
            if (!DirectCompatibilityActive)
            {
                status = IsLegacyPluginInstalled
                    ? "KK_ChaAlphaMask is installed; the upstream bridge owns legacy sources."
                    : "Nakay legacy compatibility is disabled.";
                return false;
            }

            bool effectiveHighPoly = character != null &&
                                     LegacyCharacterStateAdapter.IsEffectiveHighPoly(character);
            if (character == null || slot == ClothingSlot.Top || !effectiveHighPoly)
            {
                status = character != null && !effectiveHighPoly
                    ? "Nakay body masks are limited to high-poly characters."
                    : "No supported legacy slot.";
                return false;
            }

            EnsureInitialized();
            if (Catalog.DescriptorCount == 0)
            {
                status = "The legacy metadata catalog is empty.";
                return false;
            }

            LegacyMaskBindingQuery query;
            if (!LegacyCharacterStateAdapter.TryBuildBindingQuery(
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
                    status = "No legacy mask metadata for the current item (cached).";
                    return false;
                }

                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.ProviderResolutions);
                LegacyMaskDescriptor descriptor;
                if (!Catalog.TryResolve(query, out descriptor))
                {
                    BodyMaskPerformanceMetrics.Increment(PerformanceCounter.MetadataCacheMisses);
                    _negativeCache.Add(queryKey, Catalog.Generation);
                    status = "No legacy mask metadata for the current item.";
                    return false;
                }

                BodyMaskPerformanceMetrics.Increment(PerformanceCounter.MetadataCacheHits);
                descriptor = descriptor.CloneForSlot(slot);
                string cacheKey = Catalog.Generation + "|" + descriptor.MetadataFingerprint +
                                  "|" + (int)GradientHandlingMode.PreserveContinuous + "|" + (int)slot;
                if (_compiledCache.TryGetValue(cacheKey, out resolved))
                {
                    status = "Resolved from compiled legacy mask cache.";
                    return true;
                }

                string error;
                if (!LegacyMaskTextureCompiler.TryCompile(
                        descriptor,
                        out resolved,
                        out error))
                {
                    status = error;
                    return false;
                }

                resolved.CatalogGeneration = Catalog.Generation;
                _compiledCache.Put(cacheKey, resolved, EstimateCacheBytes(resolved));
                status = "Legacy source compiled from " +
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
                LegacyCharacterStateAdapter.Reset();
            }
        }













        private static long EstimateCacheBytes(LegacyResolvedMask value)
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
            int megabytes = BodyMaskLayersPlugin.Settings.GetLegacyCacheMemoryLimitMegabytes();
            return (long)megabytes * 1024L * 1024L;
        }

        private static void RecreateCaches()
        {
            _compiledCache = new BoundedLruCache<string, LegacyResolvedMask>(
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
