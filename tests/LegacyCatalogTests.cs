using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class LegacyCatalogTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("legacy catalog: PNG manifest and resolved identity", PngManifestAndResolvedIdentity),
                new TestCase("legacy catalog: AssetBundle fields and GUID override", AssetBundleFieldsAndGuidOverride),
                new TestCase("legacy catalog: botMask and object options", BotMaskAndObjectOptions),
                new TestCase("legacy catalog: invalid and unsupported entries", InvalidAndUnsupportedEntries),
                new TestCase("legacy catalog: stable first match and identity refinement", StableFirstMatchAndIdentityRefinement),
                new TestCase("legacy catalog: incremental refresh lifecycle", IncrementalRefreshLifecycle),
                new TestCase("legacy catalog: large synthetic cold and warm index", LargeSyntheticColdAndWarmIndex),
                new TestCase("legacy catalog: malformed manifests are isolated", MalformedManifestsAreIsolated),
                new TestCase("legacy catalog: fingerprints are stable and discriminating", FingerprintsAreStableAndDiscriminating),
                new TestCase("legacy catalog: coexistence and fingerprint ownership policy", CoexistenceAndFingerprintOwnershipPolicy)
            };
        }

        private static void PngManifestAndResolvedIdentity()
        {
            int resolverCalls = 0;
            int resolverOriginal = -1;
            int resolverCategory = -1;
            string resolverGuid = null;
            LegacyManifestSource source = Source(
                null,
                "fixture-png.zipmod",
                101L,
                201L,
                301U,
                Manifest(
                    "fixture.root",
                    Mask(
                        "<category>106</category>" +
                        "<id>21</id>" +
                        "<pngBodyMaskPath> fixture\\masks\\body.png </pngBodyMaskPath>")));

            int rejected;
            IList<LegacyMaskDescriptor> descriptors = LegacyManifestParser.Parse(
                source,
                delegate(int originalId, int category, string guid)
                {
                    resolverCalls++;
                    resolverOriginal = originalId;
                    resolverCategory = category;
                    resolverGuid = guid;
                    return 9021;
                },
                out rejected);

            Check.Equal(0, rejected, "A complete PNG entry must not be rejected.");
            Check.Equal(1, descriptors.Count, "Exactly one PNG descriptor must be parsed.");
            LegacyMaskDescriptor descriptor = descriptors[0];
            Check.Equal(1, resolverCalls, "The local-ID resolver must run once per valid entry.");
            Check.Equal(21, resolverOriginal, "The resolver must receive the original item ID.");
            Check.Equal(106, resolverCategory, "The resolver must receive the list category.");
            Check.Equal("fixture.root", resolverGuid, "The root GUID must be the fallback identity.");
            Check.Equal("fixture.root", descriptor.ModGuid, "The root manifest GUID must be retained.");
            Check.Equal("fixture-png.zipmod", descriptor.ArchivePath, "The source archive identity must be retained.");
            Check.Equal(ClothingSlot.Bottom, descriptor.Slot, "Category 106 must bind to Bottom.");
            Check.Equal(106, descriptor.Category, "The exact category must be retained.");
            Check.Equal(21, descriptor.OriginalItemId, "The original item ID must be retained.");
            Check.Equal(9021, descriptor.ResolvedItemId, "The resolver result must become the local item ID.");
            Check.True(descriptor.IsPng, "The descriptor must expose PNG mode.");
            Check.Equal(
                "abdata/fixture/masks/body.png",
                descriptor.PngPath,
                "Relative PNG paths must normalize to an abdata path with forward slashes.");
            Check.Null(descriptor.AssetBundlePath, "PNG mode must not invent an AssetBundle path.");
            Check.Null(descriptor.MaskAssetName, "PNG mode must not invent an asset name.");
            Check.Equal(0, descriptor.SourceOrder, "The first accepted mask must have source order zero.");
            Check.Equal(
                descriptor.BuildFingerprint(GradientHandlingMode.PreserveContinuous, null),
                descriptor.MetadataFingerprint,
                "The parser fingerprint must be reproducible from metadata.");
        }

        private static void AssetBundleFieldsAndGuidOverride()
        {
            string resolverGuid = null;
            LegacyManifestSource source = Source(
                "fixture.source",
                "fixture-bundle.zipmod",
                102L,
                202L,
                302U,
                Manifest(
                    "fixture.root",
                    Mask(
                        "<guid>fixture.mask.override</guid>" +
                        "<category>112</category>" +
                        "<id>52</id>" +
                        "<abPath> bundle/path.unity3d </abPath>" +
                        "<prefab> FixturePrefab </prefab>" +
                        "<abBodyMaskName> ExactMaskAsset </abBodyMaskName>")));

            int rejected;
            IList<LegacyMaskDescriptor> descriptors = LegacyManifestParser.Parse(
                source,
                delegate(int originalId, int category, string guid)
                {
                    resolverGuid = guid;
                    return 8052;
                },
                out rejected);

            Check.Equal(0, rejected, "A complete AssetBundle entry must not be rejected.");
            Check.Equal(1, descriptors.Count, "Exactly one AssetBundle descriptor must be parsed.");
            LegacyMaskDescriptor descriptor = descriptors[0];
            Check.Equal("fixture.mask.override", resolverGuid, "The per-mask GUID must reach the resolver.");
            Check.Equal("fixture.mask.override", descriptor.ModGuid, "The per-mask GUID must override both fallbacks.");
            Check.Equal(ClothingSlot.IndoorShoes, descriptor.Slot, "The shared shoes category must map deterministically.");
            Check.False(descriptor.IsPng, "AssetBundle mode must not report PNG mode.");
            Check.Null(descriptor.PngPath, "AssetBundle mode must not invent a PNG path.");
            Check.Equal("bundle/path.unity3d", descriptor.AssetBundlePath, "The exact bundle path must be retained.");
            Check.Equal("FixturePrefab", descriptor.PrefabName, "The optional prefab name must be retained.");
            Check.Equal("ExactMaskAsset", descriptor.MaskAssetName, "The exact texture asset name must be retained.");
            Check.Equal(52, descriptor.OriginalItemId, "The AssetBundle item ID must be retained.");
            Check.Equal(8052, descriptor.ResolvedItemId, "The AssetBundle item must use the resolved ID.");

            LegacyManifestSource sourceFallback = Source(
                "fixture.source.fallback",
                "fixture-source-guid.zipmod",
                103L,
                203L,
                303U,
                Manifest(
                    "fixture.root.ignored",
                    Mask(
                        "<category>106</category>" +
                        "<id>53</id>" +
                        "<pngBodyMaskPath>abdata/fixture/already-normalized.png</pngBodyMaskPath>")));
            descriptors = LegacyManifestParser.Parse(sourceFallback, null, out rejected);
            Check.Equal(
                "fixture.source.fallback",
                descriptors[0].ModGuid,
                "A source GUID supplied by the metadata catalog must be preferred to a root fallback.");
            Check.Equal(
                "abdata/fixture/already-normalized.png",
                descriptors[0].PngPath,
                "An existing abdata prefix must not be duplicated.");
        }

        private static void BotMaskAndObjectOptions()
        {
            string xml = Manifest(
                "fixture.options",
                Mask(
                    "<category>105</category>" +
                    "<id>31</id>" +
                    "<botMask>true</botMask>" +
                    "<objOpt01>true</objOpt01>" +
                    "<objOpt02>false</objOpt02>" +
                    "<pngBodyMaskPath>fixture/bottom.png</pngBodyMaskPath>") +
                Mask(
                    "<category>107</category>" +
                    "<id>32</id>" +
                    "<botMask>true</botMask>" +
                    "<pngBodyMaskPath>fixture/shorts.png</pngBodyMaskPath>"));
            LegacyManifestSource source = Source(
                "fixture.options",
                "fixture-options.zipmod",
                104L,
                204L,
                304U,
                xml);
            int rejected;
            IList<LegacyMaskDescriptor> descriptors = LegacyManifestParser.Parse(source, null, out rejected);
            Check.Equal(0, rejected, "Supported botMask entries must be accepted.");
            Check.Equal(2, descriptors.Count, "Both botMask forms must be represented.");
            Check.Equal(ClothingSlot.Bottom, descriptors[0].Slot, "Top-category botMask must target Bottom.");
            Check.True(descriptors[0].BotMask, "The botMask flag must be retained.");
            Check.Equal((bool?)true, descriptors[0].ObjectOption01, "objOpt01 must retain true.");
            Check.Equal((bool?)false, descriptors[0].ObjectOption02, "objOpt02 must retain false.");
            Check.Equal(ClothingSlot.Shorts, descriptors[1].Slot, "Bra-category botMask must target Shorts.");
            Check.Null(descriptors[1].ObjectOption01, "An omitted option must remain unconstrained/null.");
            Check.Null(descriptors[1].ObjectOption02, "An omitted option must remain unconstrained/null.");

            LegacyClothingMaskCatalog catalog = new LegacyClothingMaskCatalog();
            catalog.Refresh(new LegacyManifestSource[] { source }, null, false);
            LegacyMaskDescriptor resolved;
            LegacyMaskBindingQuery query = Query(
                ClothingSlot.Bottom,
                105,
                31,
                31,
                "fixture.options",
                true,
                true,
                false);
            Check.True(catalog.TryResolve(query, out resolved), "Exact botMask/options must resolve.");
            Check.Same(descriptors[0].GetType(), resolved.GetType(), "The resolved object must be a legacy descriptor.");

            query.BotMask = false;
            Check.False(catalog.TryResolve(query, out resolved), "A different botMask flag must not resolve.");
            query.BotMask = true;
            query.ObjectOption02 = true;
            Check.True(catalog.TryResolve(query, out resolved), "botMask entries ignore object-option selectors in the audited contract.");

            query = Query(
                ClothingSlot.Bottom,
                105,
                31,
                31,
                "fixture.options",
                true,
                null,
                null);
            Check.True(catalog.TryResolve(query, out resolved), "botMask entries must resolve without option arrays.");
        }

        private static void InvalidAndUnsupportedEntries()
        {
            string xml = Manifest(
                "fixture.invalid",
                Mask(
                    "<category>105</category><id>1</id>" +
                    "<pngBodyMaskPath>fixture/top.png</pngBodyMaskPath>") +
                Mask(
                    "<category>104</category><id>2</id>" +
                    "<pngBodyMaskPath>fixture/low.png</pngBodyMaskPath>") +
                Mask(
                    "<category>113</category><id>3</id>" +
                    "<pngBodyMaskPath>fixture/high.png</pngBodyMaskPath>") +
                Mask(
                    "<id>4</id><pngBodyMaskPath>fixture/no-category.png</pngBodyMaskPath>") +
                Mask(
                    "<category>106</category><id>not-an-int</id>" +
                    "<pngBodyMaskPath>fixture/bad-id.png</pngBodyMaskPath>") +
                Mask("<category>106</category><id>6</id>") +
                Mask(
                    "<category>106</category><id>7</id>" +
                    "<abPath>fixture/incomplete.unity3d</abPath>") +
                Mask(
                    "<category>105</category><id>8</id><botMask>true</botMask>" +
                    "<pngBodyMaskPath>fixture/valid-bottom.png</pngBodyMaskPath>"));
            int rejected;
            IList<LegacyMaskDescriptor> descriptors = LegacyManifestParser.Parse(
                Source("fixture.invalid", "fixture-invalid.zipmod", 105L, 205L, 305U, xml),
                null,
                out rejected);

            Check.Equal(7, rejected, "Every malformed, top, or unsupported entry must be counted.");
            Check.Equal(1, descriptors.Count, "Only the supported control entry must survive.");
            Check.Equal(8, descriptors[0].OriginalItemId, "The accepted control entry must remain identifiable.");
            Check.Equal(ClothingSlot.Bottom, descriptors[0].Slot, "The accepted botMask control must target Bottom.");
        }

        private static void StableFirstMatchAndIdentityRefinement()
        {
            LegacyManifestSource first = Source(
                "fixture.first",
                "fixture-first.zipmod",
                106L,
                206L,
                306U,
                Manifest(
                    "fixture.first",
                    Mask(
                        "<category>106</category><id>41</id>" +
                        "<pngBodyMaskPath>fixture/first-a.png</pngBodyMaskPath>") +
                    Mask(
                        "<category>106</category><id>41</id>" +
                        "<pngBodyMaskPath>fixture/first-b.png</pngBodyMaskPath>")));
            LegacyManifestSource second = Source(
                "fixture.second",
                "fixture-second.zipmod",
                107L,
                207L,
                307U,
                Manifest(
                    "fixture.second",
                    Mask(
                        "<category>106</category><id>42</id>" +
                        "<pngBodyMaskPath>fixture/second.png</pngBodyMaskPath>")));
            LegacyResolvedItemIdResolver resolver = delegate(int originalId, int category, string guid)
            {
                return 7000;
            };
            LegacyClothingMaskCatalog catalog = new LegacyClothingMaskCatalog();
            catalog.Refresh(new LegacyManifestSource[] { first, second }, resolver, false);

            LegacyMaskDescriptor descriptor;
            LegacyMaskBindingQuery broad = Query(
                ClothingSlot.Bottom,
                106,
                0,
                7000,
                null,
                false,
                null,
                null);
            Check.True(catalog.TryResolve(broad, out descriptor), "A broad resolved-ID query must find a candidate.");
            Check.Equal(
                "abdata/fixture/first-a.png",
                descriptor.PngPath,
                "First source and first mask order must be deterministic.");

            LegacyIndexRefreshResult unchanged = catalog.Refresh(
                new LegacyManifestSource[] { first, second },
                resolver,
                false);
            Check.False(unchanged.Changed, "An unchanged refresh must preserve catalog order and generation.");
            Check.True(catalog.TryResolve(broad, out descriptor), "The broad query must survive cache reuse.");
            Check.Equal(
                "abdata/fixture/first-a.png",
                descriptor.PngPath,
                "A reused catalog must keep the same first match.");

            LegacyMaskBindingQuery exact = Query(
                ClothingSlot.Bottom,
                106,
                42,
                7000,
                "fixture.second",
                false,
                null,
                null);
            Check.True(catalog.TryResolve(exact, out descriptor), "Stable GUID/original metadata should refine a resolved-ID collision.");
            Check.Equal("abdata/fixture/second.png", descriptor.PngPath, "The refined query selected the wrong source.");

            exact.SideloaderGuid = "fixture.first";
            Check.True(
                catalog.TryResolve(exact, out descriptor),
                "A collision with incomplete/mismatched enriched metadata should retain deterministic fallback.");
            Check.Equal(
                "abdata/fixture/first-a.png",
                descriptor.PngPath,
                "Fallback must retain first catalog order when no exact enriched identity exists.");
            exact.SideloaderGuid = "fixture.second";
            exact.Slot = ClothingSlot.Gloves;
            Check.False(catalog.TryResolve(exact, out descriptor), "An inconsistent clothing slot must not resolve.");
        }

        private static void IncrementalRefreshLifecycle()
        {
            LegacyManifestSource first = SinglePngSource(
                "fixture.incremental.a",
                "incremental-a.zipmod",
                51,
                "fixture/a.png",
                401U);
            LegacyManifestSource second = SinglePngSource(
                "fixture.incremental.b",
                "incremental-b.zipmod",
                52,
                "fixture/b.png",
                402U);
            LegacyClothingMaskCatalog catalog = new LegacyClothingMaskCatalog();

            LegacyIndexRefreshResult initial = catalog.Refresh(
                new LegacyManifestSource[] { first, second },
                null,
                false);
            Check.True(initial.Changed, "The initial index build must be a change.");
            Check.Equal(1, initial.Generation, "The initial build must create generation one.");
            Check.Equal(2, initial.ParsedManifestCount, "The initial build must parse every source.");
            Check.Equal(0, initial.ReusedManifestCount, "The initial build cannot reuse sources.");
            Check.Equal(2, initial.ManifestCount, "Both manifests must be indexed.");
            Check.Equal(2, initial.DescriptorCount, "Both descriptors must be indexed.");

            LegacyMaskDescriptor firstBefore;
            Check.True(
                catalog.TryResolve(
                    Query(ClothingSlot.Bottom, 106, 51, 51, "fixture.incremental.a", false, null, null),
                    out firstBefore),
                "The first descriptor must be queryable before reuse.");
            LegacyIndexRefreshResult reused = catalog.Refresh(
                new LegacyManifestSource[] { first, second },
                null,
                false);
            Check.False(reused.Changed, "Identical source metadata must not change the generation.");
            Check.Equal(1, reused.Generation, "An unchanged refresh must retain its generation.");
            Check.Equal(0, reused.ParsedManifestCount, "Unchanged sources must not be reparsed.");
            Check.Equal(2, reused.ReusedManifestCount, "Every unchanged source must be reused.");
            LegacyMaskDescriptor firstAfter;
            catalog.TryResolve(
                Query(ClothingSlot.Bottom, 106, 51, 51, "fixture.incremental.a", false, null, null),
                out firstAfter);
            Check.Same(firstBefore, firstAfter, "An unchanged descriptor object must be reused.");

            LegacyManifestSource changedSecond = SinglePngSource(
                "fixture.incremental.b",
                "incremental-b.zipmod",
                52,
                "fixture/b-changed.png",
                499U);
            LegacyIndexRefreshResult changed = catalog.Refresh(
                new LegacyManifestSource[] { first, changedSecond },
                null,
                false);
            Check.True(changed.Changed, "A changed metadata key must advance the catalog.");
            Check.Equal(2, changed.Generation, "A changed source must advance the generation once.");
            Check.Equal(1, changed.ParsedManifestCount, "Only the changed source must be reparsed.");
            Check.Equal(1, changed.ReusedManifestCount, "The unchanged source must be reused.");
            LegacyMaskDescriptor changedDescriptor;
            catalog.TryResolve(
                Query(ClothingSlot.Bottom, 106, 52, 52, "fixture.incremental.b", false, null, null),
                out changedDescriptor);
            Check.Equal(
                "abdata/fixture/b-changed.png",
                changedDescriptor.PngPath,
                "The reparsed descriptor must expose changed metadata.");

            LegacyIndexRefreshResult deleted = catalog.Refresh(
                new LegacyManifestSource[] { changedSecond },
                null,
                false);
            Check.True(deleted.Changed, "Removing a source must change the index.");
            Check.Equal(3, deleted.Generation, "Deleting a source must advance the generation.");
            Check.Equal(1, deleted.ReusedManifestCount, "The remaining unchanged source must be reused.");
            Check.Equal(0, deleted.ParsedManifestCount, "Deletion alone must not reparse the survivor.");
            Check.Equal(1, deleted.ManifestCount, "The removed manifest must leave the index.");
            Check.Equal(1, deleted.DescriptorCount, "Descriptors from removed mods must leave the index.");
            Check.False(
                catalog.TryResolve(
                    Query(ClothingSlot.Bottom, 106, 51, 51, "fixture.incremental.a", false, null, null),
                    out firstAfter),
                "A descriptor from a removed source must no longer resolve.");

            LegacyIndexRefreshResult forced = catalog.Refresh(
                new LegacyManifestSource[] { changedSecond },
                null,
                true);
            Check.True(forced.Changed, "A force-full request must always publish a new generation.");
            Check.Equal(4, forced.Generation, "Force-full must advance the generation once.");
            Check.Equal(1, forced.ParsedManifestCount, "Force-full must parse every current source.");
            Check.Equal(0, forced.ReusedManifestCount, "Force-full must bypass reuse.");
        }

        private static void MalformedManifestsAreIsolated()
        {
            LegacyManifestSource good = SinglePngSource(
                "fixture.good",
                "fixture-good.zipmod",
                61,
                "fixture/good.png",
                501U);
            LegacyManifestSource malformed = Source(
                "fixture.malformed",
                "fixture-malformed.zipmod",
                111L,
                211L,
                502U,
                "<manifest><guid>fixture.malformed</guid><ChaAlphaMask><mask>");
            LegacyClothingMaskCatalog catalog = new LegacyClothingMaskCatalog();
            LegacyIndexRefreshResult result = catalog.Refresh(
                new LegacyManifestSource[] { malformed, good },
                null,
                false);
            Check.True(result.Changed, "An isolated malformed source still participates in an index build.");
            Check.Equal(2, result.ParsedManifestCount, "Both changed sources must be attempted.");
            Check.Equal(1, result.RejectedEntryCount, "The malformed source must be counted as rejected.");
            Check.Equal(1, result.DescriptorCount, "A malformed peer must not discard a valid source.");
            LegacyMaskDescriptor descriptor;
            Check.True(
                catalog.TryResolve(
                    Query(ClothingSlot.Bottom, 106, 61, 61, "fixture.good", false, null, null),
                    out descriptor),
                "The valid peer must remain queryable.");
        }

        private static void LargeSyntheticColdAndWarmIndex()
        {
            const int sourceCount = 2048;
            LegacyManifestSource[] sources = new LegacyManifestSource[sourceCount];
            for (int index = 0; index < sources.Length; index++)
            {
                int itemId = index + 1;
                sources[index] = SinglePngSource(
                    "synthetic.mod." + index,
                    "synthetic/mod-" + index + ".zipmod",
                    itemId,
                    "synthetic/mask-" + index + ".png",
                    (uint)(10000 + index));
            }

            int resolverCalls = 0;
            LegacyResolvedItemIdResolver resolver = delegate(int originalId, int category, string guid)
            {
                resolverCalls++;
                return originalId + 100000;
            };
            LegacyClothingMaskCatalog catalog = new LegacyClothingMaskCatalog();
            LegacyIndexRefreshResult cold = catalog.Refresh(sources, resolver, false);
            Check.True(cold.Changed, "The cold synthetic index must publish its first generation.");
            Check.Equal(1, cold.Generation, "The cold synthetic generation mismatch.");
            Check.Equal(sourceCount, cold.ParsedManifestCount, "Cold indexing must parse every synthetic manifest.");
            Check.Equal(0, cold.ReusedManifestCount, "Cold indexing cannot reuse manifests.");
            Check.Equal(sourceCount, cold.ManifestCount, "Cold synthetic manifest count mismatch.");
            Check.Equal(sourceCount, cold.DescriptorCount, "Cold synthetic descriptor count mismatch.");
            Check.Equal(sourceCount, resolverCalls, "Cold indexing must resolve each descriptor exactly once.");
            Check.Equal(1, catalog.LookupBuildCount, "Cold indexing must build lookup buckets once.");

            int[] sampleIndexes = { 0, 511, 1023, 1535, sourceCount - 1 };
            LegacyMaskDescriptor[] coldDescriptors =
                new LegacyMaskDescriptor[sampleIndexes.Length];
            for (int sample = 0; sample < sampleIndexes.Length; sample++)
            {
                int index = sampleIndexes[sample];
                int itemId = index + 1;
                LegacyMaskDescriptor descriptor;
                Check.True(
                    catalog.TryResolve(
                        Query(
                            ClothingSlot.Bottom,
                            106,
                            itemId,
                            itemId + 100000,
                            "synthetic.mod." + index,
                            false,
                            null,
                            null),
                        out descriptor),
                    "Cold synthetic lookup failed at " + index + ".");
                Check.Equal(
                    "abdata/synthetic/mask-" + index + ".png",
                    descriptor.PngPath,
                    "Cold synthetic lookup selected the wrong descriptor at " + index + ".");
                coldDescriptors[sample] = descriptor;
            }

            LegacyMaskDescriptor missing;
            Check.False(
                catalog.TryResolve(
                    Query(
                        ClothingSlot.Bottom,
                        106,
                        999999,
                        999999,
                        "synthetic.missing",
                        false,
                        null,
                        null),
                    out missing),
                "A missing synthetic item must be a negative lookup.");

            LegacyIndexRefreshResult warm = catalog.Refresh(sources, resolver, false);
            Check.False(warm.Changed, "An unchanged large synthetic index must stay warm.");
            Check.Equal(cold.Generation, warm.Generation, "Warm indexing must retain its generation.");
            Check.Equal(0, warm.ParsedManifestCount, "Warm indexing must parse zero manifests.");
            Check.Equal(sourceCount, warm.ReusedManifestCount, "Warm indexing must reuse every manifest.");
            Check.Equal(sourceCount, warm.ManifestCount, "Warm synthetic manifest count mismatch.");
            Check.Equal(sourceCount, warm.DescriptorCount, "Warm synthetic descriptor count mismatch.");
            Check.Equal(0, warm.RejectedEntryCount, "Warm synthetic rejection count mismatch.");
            Check.Equal(sourceCount, resolverCalls, "Warm indexing must perform no additional identity resolution.");
            Check.Equal(
                1,
                catalog.LookupBuildCount,
                "An unchanged warm index must not rebuild lookup buckets.");

            for (int sample = 0; sample < sampleIndexes.Length; sample++)
            {
                int index = sampleIndexes[sample];
                int itemId = index + 1;
                LegacyMaskDescriptor descriptor;
                Check.True(
                    catalog.TryResolve(
                        Query(
                            ClothingSlot.Bottom,
                            106,
                            itemId,
                            itemId + 100000,
                            "synthetic.mod." + index,
                            false,
                            null,
                            null),
                        out descriptor),
                    "Warm synthetic lookup failed at " + index + ".");
                Check.Same(
                    coldDescriptors[sample],
                    descriptor,
                    "Warm indexing must retain descriptor identity at " + index + ".");
            }
        }

        private static void FingerprintsAreStableAndDiscriminating()
        {
            LegacyMaskDescriptor descriptor = FingerprintDescriptor();
            string first = descriptor.BuildFingerprint(GradientHandlingMode.Auto, "content-a");
            string repeated = descriptor.BuildFingerprint(GradientHandlingMode.Auto, "content-a");
            Check.Equal(first, repeated, "Identical metadata and content must produce the same fingerprint.");
            Check.Equal(64, first.Length, "A SHA-256 fingerprint must contain 64 hexadecimal characters.");
            Check.False(
                string.Equals(
                    first,
                    descriptor.BuildFingerprint(GradientHandlingMode.Auto, "content-b"),
                    StringComparison.Ordinal),
                "Changing content identity must change the fingerprint.");
            Check.False(
                string.Equals(
                    first,
                    descriptor.BuildFingerprint(GradientHandlingMode.PreserveContinuous, "content-a"),
                    StringComparison.Ordinal),
                "Changing gradient interpretation must change the fingerprint.");

            LegacyMaskDescriptor otherAsset = FingerprintDescriptor();
            otherAsset.MaskAssetName = "DifferentMaskAsset";
            Check.False(
                string.Equals(
                    first,
                    otherAsset.BuildFingerprint(GradientHandlingMode.Auto, "content-a"),
                    StringComparison.Ordinal),
                "Changing the exact mask asset must change the fingerprint.");

            LegacyMaskDescriptor otherMode = FingerprintDescriptor();
            otherMode.PngPath = "abdata/fixture/source.png";
            otherMode.AssetBundlePath = null;
            otherMode.MaskAssetName = null;
            Check.False(
                string.Equals(
                    first,
                    otherMode.BuildFingerprint(GradientHandlingMode.Auto, "content-a"),
                    StringComparison.Ordinal),
                "Changing source mode and path must change the fingerprint.");

            LegacyManifestSource source = Source(
                "fixture.change-key",
                "fixture-change.zipmod",
                900L,
                901L,
                902U,
                "<manifest />");
            string stableChangeKey = source.BuildChangeKey();
            Check.Equal(stableChangeKey, source.BuildChangeKey(), "An unchanged source change key must be deterministic.");
            source.ManifestCrc = 903U;
            Check.False(
                string.Equals(stableChangeKey, source.BuildChangeKey(), StringComparison.Ordinal),
                "Changing relevant source metadata must change its incremental key.");
        }

        private static void CoexistenceAndFingerprintOwnershipPolicy()
        {
            Check.True(
                LegacyCompatibilityPolicy.ShouldUseDirectProvider(true, false),
                "Direct provider must run only when enabled and the old plugin is absent.");
            Check.False(
                LegacyCompatibilityPolicy.ShouldUseDirectProvider(false, false),
                "Disabled compatibility must fast-return.");
            Check.False(
                LegacyCompatibilityPolicy.ShouldUseDirectProvider(true, true),
                "The old plugin and direct provider must never own legacy sources together.");
            Check.False(
                LegacyCompatibilityPolicy.ShouldUseDirectProvider(false, true),
                "Disabled compatibility remains inactive with the old plugin installed.");

            Check.True(
                LegacyCompatibilityPolicy.NativeFingerprintSuppressesLegacy(
                    "same",
                    true,
                    "same"),
                "An owned converted native layer must suppress its identical legacy source.");
            Check.False(
                LegacyCompatibilityPolicy.NativeFingerprintSuppressesLegacy(
                    "same",
                    false,
                    "same"),
                "A fingerprint without native ownership must not suppress a legacy source.");
            Check.False(
                LegacyCompatibilityPolicy.NativeFingerprintSuppressesLegacy(
                    "native",
                    true,
                    "legacy"),
                "Different fingerprints must not suppress independent sources.");
            Check.False(
                LegacyCompatibilityPolicy.NativeFingerprintSuppressesLegacy(
                    null,
                    true,
                    "legacy"),
                "A native layer without provenance cannot claim a legacy source.");

            ClothingMaskLayerData converted = new ClothingMaskLayerData
            {
                Enabled = true,
                SourceContract = MaskSourceContract.NakayRgbStateCoverage,
                SourceProviderId = LegacyMaskDescriptor.ProviderIdValue.ToUpperInvariant(),
                SourceFingerprint = "owned-source"
            };
            Check.True(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    true,
                    true,
                    "owned-source"),
                "A valid decoded converted layer with matching binding must own its legacy source.");

            converted.Enabled = false;
            Check.True(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    true,
                    true,
                    "owned-source"),
                "Disabling an owned converted layer must not allow its identical legacy source to reappear.");

            Check.False(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    false,
                    true,
                    "owned-source"),
                "A corrupt or undecoded native copy must leave compatibility fallback available.");
            Check.False(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    true,
                    false,
                    "owned-source"),
                "A converted layer whose item binding does not match must not claim the current legacy source.");

            converted.SourceProviderId = "different.provider";
            Check.False(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    true,
                    true,
                    "owned-source"),
                "A layer without Nakay provenance must not claim a Nakay legacy source.");
            converted.SourceProviderId = LegacyMaskDescriptor.ProviderIdValue;
            converted.SourceContract = MaskSourceContract.Native;
            Check.False(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    true,
                    true,
                    "owned-source"),
                "An ordinary native contract must not claim a Nakay legacy source.");
            converted.SourceContract = MaskSourceContract.NakayRgbStateCoverage;
            Check.False(
                LegacyCompatibilityPolicy.NativeLayerOwnsLegacySource(
                    converted,
                    true,
                    true,
                    "different-fingerprint"),
                "A converted layer must not claim a different legacy fingerprint.");

            Check.True(
                LegacyCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    true,
                    MaskSourceContract.NakayRgbStateCoverage,
                    LegacyMaskDescriptor.ProviderIdValue.ToUpperInvariant()),
                "The installed upstream plugin must own a converted Nakay source regardless of provider-ID case.");
            Check.False(
                LegacyCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    false,
                    MaskSourceContract.NakayRgbStateCoverage,
                    LegacyMaskDescriptor.ProviderIdValue),
                "An absent upstream plugin cannot suppress converted native data.");
            Check.False(
                LegacyCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    true,
                    MaskSourceContract.Native,
                    LegacyMaskDescriptor.ProviderIdValue),
                "The upstream plugin must not suppress an ordinary native source.");
            Check.False(
                LegacyCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    true,
                    MaskSourceContract.NakayRgbStateCoverage,
                    "different.provider"),
                "The upstream plugin must not claim another provider's source.");
            Check.False(
                LegacyCompatibilityPolicy.UpstreamPluginSuppressesConvertedNative(
                    true,
                    MaskSourceContract.NakayRgbStateCoverage,
                    null),
                "Missing provider provenance must not be suppressed.");
        }

        private static LegacyMaskDescriptor FingerprintDescriptor()
        {
            LegacyMaskDescriptor descriptor = new LegacyMaskDescriptor();
            descriptor.Game = "Koikatsu";
            descriptor.ModGuid = "fixture.fingerprint";
            descriptor.Slot = ClothingSlot.Bottom;
            descriptor.Category = 106;
            descriptor.OriginalItemId = 71;
            descriptor.ResolvedItemId = 8071;
            descriptor.AssetBundlePath = "fixture/bundle.unity3d";
            descriptor.PrefabName = "FixturePrefab";
            descriptor.MaskAssetName = "FixtureMaskAsset";
            descriptor.BotMask = false;
            descriptor.ObjectOption01 = true;
            descriptor.ObjectOption02 = null;
            return descriptor;
        }

        private static LegacyManifestSource SinglePngSource(
            string guid,
            string archivePath,
            int itemId,
            string pngPath,
            uint manifestCrc)
        {
            return Source(
                guid,
                archivePath,
                1000L + itemId,
                2000L + itemId,
                manifestCrc,
                Manifest(
                    guid,
                    Mask(
                        "<category>106</category>" +
                        "<id>" + itemId + "</id>" +
                        "<pngBodyMaskPath>" + pngPath + "</pngBodyMaskPath>")));
        }

        private static LegacyManifestSource Source(
            string guid,
            string archivePath,
            long archiveLength,
            long lastWriteTicks,
            uint manifestCrc,
            string manifestXml)
        {
            LegacyManifestSource source = new LegacyManifestSource();
            source.ModGuid = guid;
            source.ArchivePath = archivePath;
            source.ArchiveLength = archiveLength;
            source.ArchiveLastWriteUtcTicks = lastWriteTicks;
            source.ManifestCrc = manifestCrc;
            source.ManifestXml = manifestXml;
            return source;
        }

        private static LegacyMaskBindingQuery Query(
            ClothingSlot slot,
            int category,
            int originalItemId,
            int resolvedItemId,
            string guid,
            bool botMask,
            bool? objectOption01,
            bool? objectOption02)
        {
            LegacyMaskBindingQuery query = new LegacyMaskBindingQuery();
            query.Slot = slot;
            query.Category = category;
            query.OriginalItemId = originalItemId;
            query.ResolvedItemId = resolvedItemId;
            query.SideloaderGuid = guid;
            query.BotMask = botMask;
            query.ObjectOption01 = objectOption01;
            query.ObjectOption02 = objectOption02;
            return query;
        }

        private static string Manifest(string guid, string masks)
        {
            return "<manifest><guid>" + guid + "</guid><ChaAlphaMask>" + masks +
                   "</ChaAlphaMask></manifest>";
        }

        private static string Mask(string body)
        {
            return "<mask>" + body + "</mask>";
        }
    }
}
