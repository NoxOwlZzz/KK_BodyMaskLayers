using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class NativeMaskLayerStoreTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("Native store owns and clones replacement boundaries", ReplacementOwnershipAndCloning),
                new TestCase("Native store reuses semantic masks and clones statistics", DecodedReuseContract),
                new TestCase("Native store clears decoded state and advances preview revisions", ClearAndPreviewRevision),
                new TestCase("Native store snapshots and enumerates owned layers", SnapshotAndOwnershipBoundaries),
                new TestCase("Native store resets dirty decode generations exactly once", DecodeGenerationReset),
                new TestCase("Native store validates typed slot boundaries", SlotValidation)
            };
        }

        private static void ReplacementOwnershipAndCloning()
        {
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            ClothingMaskLayerData owned = CreateLayer(ClothingSlot.Top, "owned", new byte[] { 1, 2, 3 });
            store.ReplaceOwned(ClothingSlot.Top, owned);
            Check.Same(owned, store.Get(ClothingSlot.Top).Layer,
                "ReplaceOwned must transfer the exact layer instance.");

            ClothingMaskLayerData source = CreateLayer(ClothingSlot.Bottom, "clone", new byte[] { 4, 5, 6 });
            store.ReplaceClone(ClothingSlot.Bottom, source);
            ClothingMaskLayerData stored = store.Get(ClothingSlot.Bottom).Layer;
            Check.NotSame(source, stored, "ReplaceClone must clone the layer object.");
            Check.NotSame(source.OriginalPngBytes, stored.OriginalPngBytes,
                "ReplaceClone must clone PNG bytes.");
            Check.NotSame(source.BoundItemIdentity, stored.BoundItemIdentity,
                "ReplaceClone must clone the bound identity.");

            source.OriginalPngBytes[0] = 99;
            source.BoundItemIdentity.LocalItemId = 99;
            Check.Equal((byte)4, stored.OriginalPngBytes[0],
                "Mutating the replacement source must not mutate store-owned PNG bytes.");
            Check.Equal(11, stored.BoundItemIdentity.LocalItemId,
                "Mutating the replacement source must not mutate the store-owned identity.");

            Check.True(store.SetEnabled(ClothingSlot.Top, false),
                "The store must own enabled-state mutations.");
            Check.False(store.Get(ClothingSlot.Top).Layer.Enabled,
                "Enabled-state mutation must update the owned layer.");
            ClothingItemIdentity rebound = new ClothingItemIdentity
            {
                Slot = ClothingSlot.Top,
                LocalItemId = 42
            };
            Check.True(store.SetBoundItemIdentity(ClothingSlot.Top, rebound),
                "The store must own binding mutations.");
            Check.Same(rebound, store.Get(ClothingSlot.Top).Layer.BoundItemIdentity,
                "Binding mutation must update the owned layer.");
            store.UpdateValidationMetadata(
                ClothingSlot.Top,
                512,
                256,
                "validated",
                "Valid: test");
            Check.Equal(512, store.Get(ClothingSlot.Top).Layer.Width,
                "Validation metadata width must be owned by the store.");
            Check.Equal(256, store.Get(ClothingSlot.Top).Layer.Height,
                "Validation metadata height must be owned by the store.");
            Check.Equal("validated", store.Get(ClothingSlot.Top).Layer.Hash,
                "Validation metadata hash must be owned by the store.");
            Check.Equal("Valid: test", store.Get(ClothingSlot.Top).Layer.LastValidationResult,
                "Validation status must be owned by the store.");
            Check.False(store.SetEnabled(ClothingSlot.Shorts, false),
                "Mutating enabled state on an empty slot must be a no-op.");
            Check.False(store.SetBoundItemIdentity(ClothingSlot.Shorts, rebound),
                "Mutating binding on an empty slot must be a no-op.");
            Check.Throws<InvalidOperationException>(
                delegate
                {
                    store.UpdateValidationMetadata(
                        ClothingSlot.Shorts,
                        1,
                        1,
                        "none",
                        "none");
                },
                "Updating validation metadata on an empty slot must fail explicitly.");

            SemanticMask loadedMask = new SemanticMask(
                1,
                1,
                new MaskPixelRule[] { MaskPixelRule.HideWhenFull });
            store.SetDecoded(
                ClothingSlot.Pantyhose,
                loadedMask,
                new MaskColorStatistics { RedPixels = 1 });
            int loadedRevision = store.Get(ClothingSlot.Pantyhose).SemanticRevision;
            ClothingMaskLayerData loaded = CreateLayer(
                ClothingSlot.Pantyhose,
                "loaded",
                new byte[] { 9, 8, 7 });
            store.ReplaceLoadedData(ClothingSlot.Pantyhose, loaded);
            NativeMaskLayerSlotState loadedState = store.Get(ClothingSlot.Pantyhose);
            Check.NotSame(loaded, loadedState.Layer,
                "Loaded serialized data must be cloned at the store boundary.");
            Check.NotSame(loaded.OriginalPngBytes, loadedState.Layer.OriginalPngBytes,
                "Loaded PNG bytes must be cloned at the store boundary.");
            Check.Same(loadedMask, loadedState.SemanticMask,
                "Replacing serialized data alone must not mutate decoded state.");
            Check.Equal(loadedRevision, loadedState.SemanticRevision,
                "Replacing serialized data alone must not advance semantic revisions.");

            store.ReplaceLoadedData(ClothingSlot.Pantyhose, null);
            Check.Null(store.Get(ClothingSlot.Pantyhose).Layer,
                "A null loaded layer must clear only the serialized layer data.");
        }

        private static void DecodedReuseContract()
        {
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            ClothingMaskLayerData layer = CreateLayer(
                ClothingSlot.Gloves,
                "AbCdEf",
                new byte[] { 7 });
            layer.SourceContract = MaskSourceContract.ExternalRgbStateCoverage;
            layer.GradientHandlingMode = GradientHandlingMode.PreserveContinuous;
            store.ReplaceOwned(ClothingSlot.Gloves, layer);

            SemanticMask mask = new SemanticMask(
                1,
                1,
                new MaskPixelRule[] { MaskPixelRule.HideWhenFull });
            MaskColorStatistics ownedStatistics = new MaskColorStatistics
            {
                GreenPixels = 3,
                EdgePixels = 2
            };
            store.SetDecoded(ClothingSlot.Gloves, mask, ownedStatistics);

            SemanticMask reusedMask;
            MaskColorStatistics reusedStatistics;
            Check.True(
                store.TryReuseDecoded(
                    "abcdef",
                    MaskSourceContract.ExternalRgbStateCoverage,
                    GradientHandlingMode.PreserveContinuous,
                    out reusedMask,
                    out reusedStatistics),
                "An identical decode key must be reusable case-insensitively.");
            Check.Same(mask, reusedMask, "SemanticMask must be shared by reference on reuse.");
            Check.NotSame(ownedStatistics, reusedStatistics,
                "Statistics must be cloned at the reuse boundary.");
            Check.Equal(3, reusedStatistics.GreenPixels, "Reused statistics must preserve values.");

            reusedStatistics.GreenPixels = 100;
            MaskColorStatistics secondStatistics;
            Check.True(
                store.TryReuseDecoded(
                    "ABCDEF",
                    MaskSourceContract.ExternalRgbStateCoverage,
                    GradientHandlingMode.PreserveContinuous,
                    out reusedMask,
                    out secondStatistics),
                "A caller mutation must not corrupt later reuse results.");
            Check.Equal(3, secondStatistics.GreenPixels,
                "Each reuse must clone the store-owned statistics independently.");

            Check.False(
                store.TryReuseDecoded(
                    "abcdef",
                    MaskSourceContract.Native,
                    GradientHandlingMode.PreserveContinuous,
                    out reusedMask,
                    out reusedStatistics),
                "A different source contract must not reuse a decode.");
            Check.False(
                store.TryReuseDecoded(
                    "abcdef",
                    MaskSourceContract.ExternalRgbStateCoverage,
                    GradientHandlingMode.StrictCategorical,
                    out reusedMask,
                    out reusedStatistics),
                "A different gradient mode must not reuse a decode.");
            Check.False(
                store.TryReuseDecoded(
                    "different",
                    MaskSourceContract.ExternalRgbStateCoverage,
                    GradientHandlingMode.PreserveContinuous,
                    out reusedMask,
                    out reusedStatistics),
                "A different hash must not reuse a decode.");
        }

        private static void ClearAndPreviewRevision()
        {
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            ClothingMaskLayerData layer = CreateLayer(ClothingSlot.Bra, "preview", new byte[] { 8 });
            store.ReplaceOwned(ClothingSlot.Bra, layer);
            Check.Equal("preview:0", store.GetPreviewKey(ClothingSlot.Bra),
                "A newly replaced layer must start at semantic revision zero.");

            SemanticMask mask = new SemanticMask(
                1,
                1,
                new MaskPixelRule[] { MaskPixelRule.NeverHide });
            store.SetDecoded(ClothingSlot.Bra, mask, new MaskColorStatistics());
            Check.Equal("preview:1", store.GetPreviewKey(ClothingSlot.Bra),
                "Setting decoded state must advance the preview revision.");

            ClothingMaskLayerData removed = store.Clear(ClothingSlot.Bra);
            Check.Same(layer, removed, "Clear must transfer the removed owned layer back to the caller.");
            NativeMaskLayerSlotState state = store.Get(ClothingSlot.Bra);
            Check.Null(state.Layer, "Clear must remove the layer.");
            Check.Null(state.SemanticMask, "Clear must remove the semantic mask.");
            Check.Null(state.Statistics, "Clear must remove statistics.");
            Check.Equal(2, state.SemanticRevision,
                "Clearing decoded state must advance the semantic revision once.");
            Check.Null(store.GetPreviewKey(ClothingSlot.Bra),
                "An empty slot must not expose a preview key.");

            Check.Null(store.Clear(ClothingSlot.Bra), "Clearing an empty slot must be a no-op.");
            Check.Equal(2, state.SemanticRevision,
                "Clearing an empty slot must not advance the semantic revision.");
        }

        private static void SnapshotAndOwnershipBoundaries()
        {
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            ClothingMaskLayerData top = CreateLayer(ClothingSlot.Top, "top", new byte[] { 1, 2 });
            ClothingMaskLayerData gloves = CreateLayer(ClothingSlot.Gloves, "gloves", new byte[] { 3, 4 });
            ClothingMaskLayerData empty = CreateLayer(ClothingSlot.Bottom, "empty", new byte[0]);
            store.ReplaceOwned(ClothingSlot.Gloves, gloves);
            store.ReplaceOwned(ClothingSlot.Bottom, empty);
            store.ReplaceOwned(ClothingSlot.Top, top);

            Dictionary<ClothingSlot, ClothingMaskLayerData> snapshot = store.Snapshot();
            Check.Equal(3, snapshot.Count, "Snapshot must include every non-null layer.");
            Check.NotSame(top, snapshot[ClothingSlot.Top], "Snapshot layers must be deep clones.");
            Check.NotSame(top.OriginalPngBytes, snapshot[ClothingSlot.Top].OriginalPngBytes,
                "Snapshot PNG bytes must be deep clones.");
            snapshot[ClothingSlot.Top].OriginalPngBytes[0] = 99;
            Check.Equal((byte)1, store.Get(ClothingSlot.Top).Layer.OriginalPngBytes[0],
                "Mutating a snapshot must not mutate the store.");

            List<ClothingMaskLayerData> owned =
                new List<ClothingMaskLayerData>(store.EnumerateOwnedLayers());
            Check.Equal(3, owned.Count,
                "Owned enumeration must include every populated slot; persistence filters elsewhere.");
            Check.Equal(ClothingSlot.Top, owned[0].Slot,
                "Owned enumeration must retain deterministic slot order.");
            Check.Equal(ClothingSlot.Bottom, owned[1].Slot,
                "Owned enumeration must retain deterministic slot order.");
            Check.Equal(ClothingSlot.Gloves, owned[2].Slot,
                "Owned enumeration must retain deterministic slot order.");
            Check.Same(top, owned[0],
                "Internal owned enumeration must avoid cloning large PNG payloads.");
            Check.Same(empty, owned[1],
                "Filtering empty PNG payloads belongs to PersistableLayerSelector.");
            Check.Same(gloves, owned[2],
                "Internal owned enumeration must expose the store-owned instance.");
        }

        private static void DecodeGenerationReset()
        {
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            ClothingMaskLayerData layer = CreateLayer(ClothingSlot.Top, "generation", new byte[] { 1 });
            store.ReplaceOwned(ClothingSlot.Top, layer);
            store.SetDecoded(
                ClothingSlot.Top,
                new SemanticMask(1, 1, new MaskPixelRule[] { MaskPixelRule.HideWhenNotOff }),
                new MaskColorStatistics { YellowPixels = 1 });

            Check.Equal(0, store.DecodeConfigurationRevision,
                "The initial decode generation must be zero.");
            Check.False(store.DecodeConfigurationDirty,
                "The initial decode generation must be clean.");
            Check.False(store.ResetDecodeGeneration(),
                "Resetting a clean generation must be a no-op.");

            store.MarkDecodeConfigurationDirty();
            store.MarkDecodeConfigurationDirty();
            Check.True(store.DecodeConfigurationDirty, "Dirty marking must be idempotent.");

            SemanticMask reusedMask;
            MaskColorStatistics reusedStatistics;
            Check.True(
                store.TryReuseDecoded(
                    "generation",
                    MaskSourceContract.Native,
                    GradientHandlingMode.StrictCategorical,
                    out reusedMask,
                    out reusedStatistics),
                "The store exposes its cached generation; callers decide whether pending configuration blocks reuse.");

            int topRevision = store.Get(ClothingSlot.Top).SemanticRevision;
            int bottomRevision = store.Get(ClothingSlot.Bottom).SemanticRevision;
            Check.True(store.ResetDecodeGeneration(),
                "A dirty generation must reset exactly once.");
            Check.False(store.DecodeConfigurationDirty, "Reset must clear the dirty marker.");
            Check.Equal(1, store.DecodeConfigurationRevision,
                "Reset must advance the global decode generation once.");
            Check.Equal(topRevision + 1, store.Get(ClothingSlot.Top).SemanticRevision,
                "Reset must invalidate decoded state in populated slots.");
            Check.Equal(bottomRevision + 1, store.Get(ClothingSlot.Bottom).SemanticRevision,
                "Reset must invalidate decoded state in empty slots as before.");
            Check.Equal(1, store.Get(ClothingSlot.Top).DecodedConfigurationRevision,
                "Invalidated slots must be stamped with the new generation.");
            Check.Null(store.Get(ClothingSlot.Top).SemanticMask,
                "Reset must clear the previous semantic mask.");
            Check.Null(store.Get(ClothingSlot.Top).Statistics,
                "Reset must clear the previous statistics.");
            Check.False(store.ResetDecodeGeneration(),
                "A second clean reset must not advance revisions.");
            Check.Equal(1, store.DecodeConfigurationRevision,
                "A second clean reset must preserve the generation.");
        }

        private static void SlotValidation()
        {
            NativeMaskLayerStore store = new NativeMaskLayerStore();
            Check.Equal(9, store.SlotCount, "The store must own exactly the nine clothing slots.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { store.Get((ClothingSlot)(-1)); },
                "A negative clothing slot must be rejected.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { store.Get((ClothingSlot)9); },
                "A clothing slot beyond the store must be rejected.");
            Check.Throws<ArgumentNullException>(
                delegate { store.ReplaceOwned(ClothingSlot.Top, null); },
                "ReplaceOwned must reject null; Clear expresses removal.");
            Check.Throws<ArgumentException>(
                delegate
                {
                    store.ReplaceOwned(
                        ClothingSlot.Top,
                        CreateLayer(ClothingSlot.Bottom, "mismatch", new byte[] { 1 }));
                },
                "A layer cannot be installed into a different typed slot.");
        }

        private static ClothingMaskLayerData CreateLayer(
            ClothingSlot slot,
            string hash,
            byte[] pngBytes)
        {
            return new ClothingMaskLayerData
            {
                Slot = slot,
                Enabled = true,
                OriginalPngBytes = pngBytes,
                Width = 1,
                Height = 1,
                Hash = hash,
                BoundItemIdentity = new ClothingItemIdentity
                {
                    Slot = slot,
                    Category = 4,
                    LocalItemId = 11,
                    OriginalItemId = 12,
                    SideloaderGuid = "test.guid",
                    DisplayName = "Test item"
                },
                ColorFormatVersion = 1,
                SourceContract = MaskSourceContract.Native,
                GradientHandlingMode = GradientHandlingMode.StrictCategorical
            };
        }
    }
}
