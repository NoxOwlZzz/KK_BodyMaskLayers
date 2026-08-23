using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class ArchitectureComponentTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase(
                    "Outfit router prefers indexed payload without evaluating fallback",
                    RouterPrefersIndexedPayload),
                new TestCase(
                    "Outfit router evaluates fallback only when indexed data is absent",
                    RouterFallsBackWhenIndexedPayloadIsAbsent),
                new TestCase(
                    "Outfit router writes the same payload reference to both stores",
                    RouterWritesSameReferenceToBothStores),
                new TestCase(
                    "Outfit router skips indexed access for an invalid coordinate",
                    RouterSkipsInvalidCoordinate),
                new TestCase(
                    "Persistable selector filters empty layers and preserves order",
                    PersistableSelectorFiltersAndPreservesOrder),
                new TestCase(
                    "Native eligibility reason codes and descriptions remain stable",
                    EligibilityReasonCodesRemainStable),
                new TestCase(
                    "Native eligibility evaluates inactive reasons in stable order",
                    EligibilityReasonOrderRemainsStable),
                new TestCase(
                    "Disabled decoded Nakay layer still owns its matching legacy source",
                    DisabledNakayLayerStillOwnsMatchingLegacySource),
                new TestCase(
                    "Legacy binding query cache identity remains canonical",
                    LegacyBindingQueryCacheIdentityRemainsCanonical),
                new TestCase(
                    "Sideloader manifest CRC remains standard UTF-8 CRC32",
                    SideloaderManifestCrcRemainsStable)
            };
        }

        private static void RouterPrefersIndexedPayload()
        {
            PayloadToken indexed = new PayloadToken();
            int indexedReads = 0;
            int fallbackReads = 0;
            int readIndex = int.MinValue;
            OutfitPayloadRouter<PayloadToken> router = CreateRouter(
                delegate { return 3; },
                delegate(int index) { return index == 3; },
                delegate(int index)
                {
                    indexedReads++;
                    readIndex = index;
                    return indexed;
                },
                delegate(PayloadToken payload) { },
                delegate(PayloadToken payload, int index) { });

            PayloadToken actual = router.ReadActive(delegate
            {
                fallbackReads++;
                return new PayloadToken();
            });

            Check.Same(indexed, actual, "A non-null indexed payload must take precedence.");
            Check.Equal(1, indexedReads, "The indexed store must be read exactly once.");
            Check.Equal(3, readIndex, "The active coordinate index must be routed to the indexed reader.");
            Check.Equal(0, fallbackReads, "The fallback must remain lazy when indexed data exists.");
        }

        private static void RouterFallsBackWhenIndexedPayloadIsAbsent()
        {
            PayloadToken fallback = new PayloadToken();
            int indexedReads = 0;
            int fallbackReads = 0;
            OutfitPayloadRouter<PayloadToken> router = CreateRouter(
                delegate { return 4; },
                delegate(int index) { return index == 4; },
                delegate(int index)
                {
                    indexedReads++;
                    return null;
                },
                delegate(PayloadToken payload) { },
                delegate(PayloadToken payload, int index) { });

            PayloadToken actual = router.ReadActive(delegate
            {
                fallbackReads++;
                return fallback;
            });

            Check.Same(fallback, actual, "A null indexed payload must use the caller-provided fallback.");
            Check.Equal(1, indexedReads, "A valid coordinate must still be checked before fallback.");
            Check.Equal(1, fallbackReads, "The fallback must be evaluated exactly once when needed.");
        }

        private static void RouterWritesSameReferenceToBothStores()
        {
            PayloadToken payload = new PayloadToken();
            PayloadToken transientWrite = null;
            PayloadToken indexedWrite = null;
            int indexedCoordinate = int.MinValue;
            List<string> writeOrder = new List<string>();
            OutfitPayloadRouter<PayloadToken> router = CreateRouter(
                delegate { return 2; },
                delegate(int index) { return index == 2; },
                delegate(int index) { return null; },
                delegate(PayloadToken value)
                {
                    transientWrite = value;
                    writeOrder.Add("transient");
                },
                delegate(PayloadToken value, int index)
                {
                    indexedWrite = value;
                    indexedCoordinate = index;
                    writeOrder.Add("indexed");
                });

            router.WriteActive(payload);

            Check.Same(payload, transientWrite, "The transient store must receive the original payload instance.");
            Check.Same(payload, indexedWrite, "The indexed store must receive the original payload instance.");
            Check.Same(transientWrite, indexedWrite, "Both destinations must receive exactly the same reference.");
            Check.Equal(2, indexedCoordinate, "The indexed write must target the active coordinate.");
            Check.SequenceEqual(
                new string[] { "transient", "indexed" },
                writeOrder,
                "The transient copy must be written before the indexed copy.");
        }

        private static void RouterSkipsInvalidCoordinate()
        {
            PayloadToken fallback = new PayloadToken();
            PayloadToken payload = new PayloadToken();
            int indexedReads = 0;
            int indexedWrites = 0;
            int transientWrites = 0;
            int fallbackReads = 0;
            OutfitPayloadRouter<PayloadToken> router = CreateRouter(
                delegate { return -1; },
                delegate(int index) { return false; },
                delegate(int index)
                {
                    indexedReads++;
                    return new PayloadToken();
                },
                delegate(PayloadToken value)
                {
                    transientWrites++;
                    Check.Same(payload, value, "An invalid index must not alter the transient payload.");
                },
                delegate(PayloadToken value, int index) { indexedWrites++; });

            PayloadToken actual = router.ReadActive(delegate
            {
                fallbackReads++;
                return fallback;
            });
            router.WriteActive(payload);

            Check.Same(fallback, actual, "An invalid coordinate must read through the fallback.");
            Check.Equal(0, indexedReads, "An invalid coordinate must not invoke the indexed reader.");
            Check.Equal(1, fallbackReads, "An invalid coordinate must invoke the fallback once.");
            Check.Equal(1, transientWrites, "Transient persistence remains required without a valid index.");
            Check.Equal(0, indexedWrites, "An invalid coordinate must not invoke the indexed writer.");
        }

        private static void PersistableSelectorFiltersAndPreservesOrder()
        {
            ClothingMaskLayerData nullBytes = new ClothingMaskLayerData
            {
                Slot = ClothingSlot.Top,
                OriginalPngBytes = null
            };
            ClothingMaskLayerData emptyBytes = new ClothingMaskLayerData
            {
                Slot = ClothingSlot.Bottom,
                OriginalPngBytes = new byte[0]
            };
            ClothingMaskLayerData first = new ClothingMaskLayerData
            {
                Slot = ClothingSlot.Bra,
                OriginalPngBytes = new byte[] { 1 }
            };
            ClothingMaskLayerData disabled = new ClothingMaskLayerData
            {
                Slot = ClothingSlot.Gloves,
                Enabled = false,
                OriginalPngBytes = new byte[] { 2, 3 }
            };
            ClothingMaskLayerData last = new ClothingMaskLayerData
            {
                Slot = ClothingSlot.Socks,
                OriginalPngBytes = new byte[] { 4 }
            };

            List<ClothingMaskLayerData> selected = PersistableLayerSelector.Materialize(
                new ClothingMaskLayerData[]
                {
                    null,
                    nullBytes,
                    first,
                    emptyBytes,
                    disabled,
                    last
                });

            Check.Equal(3, selected.Count, "Only layers with non-empty original PNG bytes are persistable.");
            Check.Same(first, selected[0], "The first persistable layer must retain source order and identity.");
            Check.Same(disabled, selected[1], "Disabled layers still carry card data and must be persisted.");
            Check.Same(last, selected[2], "The final persistable layer must retain source order and identity.");
            Check.False(PersistableLayerSelector.IsPersistable(null), "A null layer is not persistable.");
            Check.False(PersistableLayerSelector.IsPersistable(nullBytes), "A layer without PNG bytes is not persistable.");
            Check.False(PersistableLayerSelector.IsPersistable(emptyBytes), "An empty PNG payload is not persistable.");
            Check.True(PersistableLayerSelector.IsPersistable(disabled), "Enabled state must not control persistence.");
        }

        private static void EligibilityReasonCodesRemainStable()
        {
            NativeLayerInactiveReason[] reasons = new NativeLayerInactiveReason[]
            {
                NativeLayerInactiveReason.None,
                NativeLayerInactiveReason.NoLayer,
                NativeLayerInactiveReason.PluginDisabled,
                NativeLayerInactiveReason.LayerDisabled,
                NativeLayerInactiveReason.MissingDecodedMask,
                NativeLayerInactiveReason.UpstreamPluginOwnsSource,
                NativeLayerInactiveReason.MissingRuntimeObject,
                NativeLayerInactiveReason.StructurallySuppressed,
                NativeLayerInactiveReason.OtherShoeTypeSelected,
                NativeLayerInactiveReason.BindingMismatch
            };
            string[] descriptions = new string[]
            {
                "active",
                "inactive: no mask",
                "inactive: plugin disabled",
                "inactive: layer disabled",
                "inactive: invalid or undecoded mask",
                "inactive: upstream KK_ChaAlphaMask owns the converted legacy source",
                "inactive: no runtime clothing object",
                "inactive: garment is integrated into another slot",
                "inactive: other shoe type is selected",
                "inactive: item binding mismatch"
            };

            for (int index = 0; index < reasons.Length; index++)
            {
                Check.Equal(index, (int)reasons[index], "Eligibility reason numeric codes are serialized diagnostics.");
                Check.Equal(
                    descriptions[index],
                    LayerEligibilityEvaluator.DescribeInactive(reasons[index]),
                    "Each eligibility reason must retain its diagnostic text.");
            }
        }

        private static void EligibilityReasonOrderRemainsStable()
        {
            ClothingItemIdentity bottomIdentity = CreateIdentity(ClothingSlot.Bottom, 101);
            ClothingMaskLayerData layer = CreateLayer(bottomIdentity);
            NativeLayerEligibilityContext context = CreateContext(bottomIdentity);

            context.PluginEnabled = false;
            layer.Enabled = false;
            Check.Equal(
                NativeLayerInactiveReason.NoLayer,
                LayerEligibilityEvaluator.EvaluateNative(null, false, context),
                "A missing layer must precede every other inactive reason.");
            Check.Equal(
                NativeLayerInactiveReason.PluginDisabled,
                LayerEligibilityEvaluator.EvaluateNative(layer, false, context),
                "Plugin-disabled must precede layer and decode state.");

            context.PluginEnabled = true;
            Check.Equal(
                NativeLayerInactiveReason.LayerDisabled,
                LayerEligibilityEvaluator.EvaluateNative(layer, false, context),
                "Layer-disabled must precede a missing decode.");

            layer.Enabled = true;
            layer.SourceContract = MaskSourceContract.NakayRgbStateCoverage;
            layer.SourceProviderId = LegacyMaskDescriptor.ProviderIdValue;
            context.LegacyPluginInstalled = true;
            context.AvailabilityMask = 0;
            Check.Equal(
                NativeLayerInactiveReason.MissingDecodedMask,
                LayerEligibilityEvaluator.EvaluateNative(layer, false, context),
                "Missing decode must precede upstream ownership and runtime availability.");
            Check.Equal(
                NativeLayerInactiveReason.UpstreamPluginOwnsSource,
                LayerEligibilityEvaluator.EvaluateNative(layer, true, context),
                "Upstream ownership must precede runtime availability.");

            layer.SourceContract = MaskSourceContract.Native;
            layer.SourceProviderId = null;
            context.LegacyPluginInstalled = false;
            context.StructuralFlags = 1;
            Check.Equal(
                NativeLayerInactiveReason.MissingRuntimeObject,
                LayerEligibilityEvaluator.EvaluateNative(layer, true, context),
                "Missing runtime object must precede structural suppression.");

            context.AvailabilityMask = 1 << (int)ClothingSlot.Bottom;
            Check.Equal(
                NativeLayerInactiveReason.StructurallySuppressed,
                LayerEligibilityEvaluator.EvaluateNative(layer, true, context),
                "Integrated garment structure must suppress an otherwise available layer.");

            ClothingItemIdentity shoeIdentity = CreateIdentity(ClothingSlot.IndoorShoes, 202);
            ClothingMaskLayerData shoeLayer = CreateLayer(shoeIdentity);
            NativeLayerEligibilityContext shoeContext = CreateContext(shoeIdentity);
            shoeContext.ShoesType = 1;
            shoeContext.CurrentIdentity = CreateIdentity(ClothingSlot.IndoorShoes, 999);
            Check.Equal(
                NativeLayerInactiveReason.OtherShoeTypeSelected,
                LayerEligibilityEvaluator.EvaluateNative(shoeLayer, true, shoeContext),
                "Shoe selection must precede binding mismatch.");

            shoeContext.ShoesType = 0;
            Check.Equal(
                NativeLayerInactiveReason.BindingMismatch,
                LayerEligibilityEvaluator.EvaluateNative(shoeLayer, true, shoeContext),
                "Binding mismatch is the final inactive reason.");

            shoeContext.CurrentIdentity = shoeIdentity.DeepClone();
            Check.Equal(
                NativeLayerInactiveReason.None,
                LayerEligibilityEvaluator.EvaluateNative(shoeLayer, true, shoeContext),
                "A fully eligible layer must report no inactive reason.");
        }

        private static void DisabledNakayLayerStillOwnsMatchingLegacySource()
        {
            const string fingerprint = "legacy-source-fingerprint";
            ClothingItemIdentity identity = CreateIdentity(ClothingSlot.Gloves, 303);
            ClothingMaskLayerData layer = CreateLayer(identity);
            layer.Enabled = false;
            layer.SourceContract = MaskSourceContract.NakayRgbStateCoverage;
            layer.SourceProviderId = LegacyMaskDescriptor.ProviderIdValue;
            layer.SourceFingerprint = fingerprint;
            NativeLayerEligibilityContext context = CreateContext(identity);

            Check.Equal(
                NativeLayerInactiveReason.LayerDisabled,
                LayerEligibilityEvaluator.EvaluateNative(layer, true, context),
                "A disabled converted layer must remain inactive as a native contribution.");
            Check.True(
                LayerEligibilityEvaluator.NativeOwnsLegacySource(
                    layer,
                    true,
                    context,
                    fingerprint),
                "A decoded and correctly bound Nakay copy must suppress its identical legacy source even while disabled.");

            context.CurrentIdentity = CreateIdentity(ClothingSlot.Gloves, 404);
            Check.False(
                LayerEligibilityEvaluator.NativeOwnsLegacySource(
                    layer,
                    true,
                    context,
                    fingerprint),
                "An identical Nakay fingerprint must not suppress legacy data for a different current item.");
            context.CurrentIdentity = identity.DeepClone();

            Check.False(
                LayerEligibilityEvaluator.NativeOwnsLegacySource(
                    layer,
                    true,
                    context,
                    "different-fingerprint"),
                "A disabled Nakay copy must not suppress a different legacy source.");
            Check.False(
                LayerEligibilityEvaluator.NativeOwnsLegacySource(
                    layer,
                    false,
                    context,
                    fingerprint),
                "A converted layer without a decoded mask cannot own the legacy source.");
        }

        private static void LegacyBindingQueryCacheIdentityRemainsCanonical()
        {
            LegacyMaskBindingQuery query = new LegacyMaskBindingQuery
            {
                Slot = ClothingSlot.Bra,
                Category = 5,
                OriginalItemId = 0,
                ResolvedItemId = 42,
                SideloaderGuid = null,
                BotMask = true,
                ObjectOption01 = null,
                ObjectOption02 = false
            };

            Check.Equal("5:42", query.BuildLookupKey(),
                "Catalog lookup identity must remain unchanged.");
            Check.Equal(
                (int)ClothingSlot.Bra + "|5|0|42||True|null|0",
                query.BuildCacheKey(),
                "Negative-cache identity must preserve every field and ternary option encoding.");

            query.SideloaderGuid = "example.guid";
            query.ObjectOption01 = true;
            query.ObjectOption02 = null;
            Check.Equal(
                (int)ClothingSlot.Bra + "|5|0|42|example.guid|True|1|null",
                query.BuildCacheKey(),
                "Cache identity must distinguish GUID and all option states.");
        }

        private static void SideloaderManifestCrcRemainsStable()
        {
            Check.Equal(
                0u,
                SideloaderLegacyManifestSourceReader.ComputeCrc32(null),
                "Null manifest text must retain the empty UTF-8 CRC32 value.");
            Check.Equal(
                0xcbf43926u,
                SideloaderLegacyManifestSourceReader.ComputeCrc32("123456789"),
                "Manifest CRC must use the standard reflected CRC32 polynomial over UTF-8.");
        }

        private static OutfitPayloadRouter<PayloadToken> CreateRouter(
            Func<int> activeCoordinateIndex,
            Func<int, bool> coordinateIndexValid,
            Func<int, PayloadToken> indexedReader,
            Action<PayloadToken> transientWriter,
            Action<PayloadToken, int> indexedWriter)
        {
            return new OutfitPayloadRouter<PayloadToken>(
                activeCoordinateIndex,
                coordinateIndexValid,
                indexedReader,
                transientWriter,
                indexedWriter);
        }

        private static ClothingItemIdentity CreateIdentity(ClothingSlot slot, int itemId)
        {
            return new ClothingItemIdentity
            {
                Slot = slot,
                Category = 100 + (int)slot,
                LocalItemId = itemId,
                OriginalItemId = itemId
            };
        }

        private static ClothingMaskLayerData CreateLayer(ClothingItemIdentity identity)
        {
            return new ClothingMaskLayerData
            {
                Slot = identity.Slot,
                Enabled = true,
                OriginalPngBytes = new byte[] { 1 },
                BoundItemIdentity = identity.DeepClone(),
                SourceContract = MaskSourceContract.Native
            };
        }

        private static NativeLayerEligibilityContext CreateContext(ClothingItemIdentity identity)
        {
            return new NativeLayerEligibilityContext
            {
                SlotIndex = (int)identity.Slot,
                PluginEnabled = true,
                LegacyPluginInstalled = false,
                ShoesType = 0,
                AvailabilityMask = 1 << (int)identity.Slot,
                StructuralFlags = 0,
                CurrentIdentity = identity.DeepClone()
            };
        }

        private sealed class PayloadToken
        {
        }
    }
}
