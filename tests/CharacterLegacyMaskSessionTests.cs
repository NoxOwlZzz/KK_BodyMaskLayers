using System;
using System.Collections.Generic;
using System.Reflection;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class LegacyResolvedMask
    {
        public string Fingerprint;
    }
}

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class CharacterLegacyMaskSessionTests
    {
        public static TestCase[] All()
        {
            return new TestCase[]
            {
                new TestCase("legacy session: typed dirty-slot lifecycle", DirtySlotLifecycle),
                new TestCase("legacy session: resolution fingerprint changes", ResolutionFingerprintChanges),
                new TestCase("legacy session: provider clear lifecycle", ProviderClearLifecycle),
                new TestCase("legacy session: portable clear suppression", PortableClearSuppression),
                new TestCase("legacy session: complete and selected tracking reset", ConversionTrackingReset),
                new TestCase("legacy session: one auto-conversion attempt per refresh", AutoConversionAttemptArbitration),
                new TestCase("legacy session: slot validation and encapsulation", SlotValidationAndEncapsulation)
            };
        }

        private static void DirtySlotLifecycle()
        {
            CharacterLegacyMaskSession session = new CharacterLegacyMaskSession();
            Check.True(session.HasDirtySlots, "A new session must refresh all direct legacy slots.");

            LegacyDirtySlotSet initial = session.ConsumeDirtySlots();
            Check.Equal(8, initial.Count, "Exactly eight non-top slots must begin dirty.");
            Check.False(initial.Contains(ClothingSlot.Top), "Top must not be a direct legacy slot.");
            for (int index = (int)ClothingSlot.Bottom;
                 index <= (int)ClothingSlot.OutdoorShoes;
                 index++)
            {
                Check.True(
                    initial.Contains((ClothingSlot)index),
                    "Every direct legacy slot must begin dirty: " + index + ".");
            }

            Check.False(session.HasDirtySlots, "Consuming dirty slots must clear the pending set.");
            Check.True(session.ConsumeDirtySlots().IsEmpty, "A second consume must be empty.");

            session.MarkSlotDirty(ClothingSlot.Top);
            Check.False(session.HasDirtySlots, "Marking Top must remain a no-op.");
            session.MarkSlotDirty(ClothingSlot.Bra);
            LegacyDirtySlotSet bra = session.ConsumeDirtySlots();
            Check.Equal(1, bra.Count, "One marked slot must produce one pending entry.");
            Check.True(bra.Contains(ClothingSlot.Bra), "The marked slot must be returned.");

            session.DeferSlots(bra);
            Check.True(
                session.ConsumeDirtySlots().Contains(ClothingSlot.Bra),
                "A consumed typed set must be deferrable without exposing its bit mask.");
            session.DeferSlot(ClothingSlot.OutdoorShoes);
            Check.True(
                session.ConsumeDirtySlots().Contains(ClothingSlot.OutdoorShoes),
                "A single slot must be deferrable.");

            session.MarkAllDirty();
            Check.Equal(8, session.ConsumeDirtySlots().Count, "MarkAll must restore all eight slots.");
        }

        private static void ResolutionFingerprintChanges()
        {
            CharacterLegacyMaskSession session = EmptySession();
            LegacyResolvedMask first = Resolution("same");
            Check.True(
                session.SetResolution(ClothingSlot.Gloves, first, "first"),
                "Null to a non-empty fingerprint must be observable as a change.");
            Check.Same(first, session.GetResolution(ClothingSlot.Gloves), "The resolution must be retained.");
            Check.Equal("first", session.GetStatus(ClothingSlot.Gloves), "The resolution status must be retained.");

            LegacyResolvedMask replacement = Resolution("same");
            Check.False(
                session.SetResolution(ClothingSlot.Gloves, replacement, "refreshed"),
                "Replacing an object with the same ordinal fingerprint must not invalidate composition.");
            Check.Same(
                replacement,
                session.GetResolution(ClothingSlot.Gloves),
                "The newest resolved object must be retained even when its fingerprint is unchanged.");
            Check.Equal("refreshed", session.GetStatus(ClothingSlot.Gloves), "Status updates must not depend on fingerprint changes.");

            Check.True(
                session.SetResolution(ClothingSlot.Gloves, Resolution("SAME"), "case changed"),
                "Fingerprint comparison must remain ordinal and case-sensitive.");
            Check.True(
                session.SetResolution(ClothingSlot.Gloves, null, "missing"),
                "Removing a fingerprinted resolution must be a change.");
            Check.False(
                session.SetResolution(ClothingSlot.Gloves, null, "still missing"),
                "Null to null must not be a fingerprint change.");
            Check.Equal("still missing", session.GetStatus(ClothingSlot.Gloves), "A null resolution must still own its latest status.");
        }

        private static void ProviderClearLifecycle()
        {
            CharacterLegacyMaskSession session = EmptySession();
            session.SetResolution(ClothingSlot.Bottom, Resolution("bottom"), "ready");
            session.SetResolution(ClothingSlot.Bra, Resolution("bra"), "ready");
            session.MarkAllDirty();

            Check.True(
                session.ClearProviderResolutions("provider inactive"),
                "Clearing a provider with resolved masks must report a removal.");
            Check.False(session.HasDirtySlots, "Provider clearing must consume stale pending resolutions.");
            for (int index = (int)ClothingSlot.Bottom;
                 index <= (int)ClothingSlot.OutdoorShoes;
                 index++)
            {
                ClothingSlot slot = (ClothingSlot)index;
                Check.Null(session.GetResolution(slot), "Provider clear must remove slot " + slot + ".");
                Check.Equal(
                    "provider inactive",
                    session.GetStatus(slot),
                    "Provider clear must update every direct-slot status.");
            }

            Check.Null(session.GetStatus(ClothingSlot.Top), "Provider clear must not assign a Top status.");
            Check.False(
                session.ClearProviderResolutions("still inactive"),
                "Clearing an already empty provider must not report a removal.");
            Check.Equal(
                "still inactive",
                session.GetStatus(ClothingSlot.Socks),
                "Repeated provider clears must still refresh status text.");
        }

        private static void PortableClearSuppression()
        {
            CharacterLegacyMaskSession session = EmptySession();
            ClothingMaskLayerData native = PortableLayer("owned");
            native.SourceContract = MaskSourceContract.Native;
            Check.False(
                session.RegisterPortableLayerClear(ClothingSlot.Bra, native),
                "A native source must not create legacy suppression.");

            ClothingMaskLayerData wrongProvider = PortableLayer("owned");
            wrongProvider.SourceProviderId = "other.provider";
            Check.False(
                session.RegisterPortableLayerClear(ClothingSlot.Bra, wrongProvider),
                "An unrelated provider must not create legacy suppression.");

            ClothingMaskLayerData portable = PortableLayer("owned");
            portable.SourceProviderId = LegacyMaskDescriptor.ProviderIdValue.ToUpperInvariant();
            Check.True(
                session.RegisterPortableLayerClear(ClothingSlot.Bra, portable),
                "The Nakay provider ID must be matched case-insensitively.");
            Check.True(
                session.IsFingerprintSuppressed(ClothingSlot.Bra, "owned"),
                "The exact cleared fingerprint must be suppressed.");
            Check.False(
                session.IsFingerprintSuppressed(ClothingSlot.Bra, "OWNED"),
                "Fingerprint suppression must remain ordinal and case-sensitive.");
            Check.False(
                session.IsFingerprintSuppressed(ClothingSlot.Bottom, "owned"),
                "Suppression must remain local to one slot.");

            LegacyResolvedMask resolved = Resolution("owned");
            session.SetResolution(ClothingSlot.Bra, resolved, "ready");
            Check.True(
                session.IsCurrentResolutionSuppressed(ClothingSlot.Bra),
                "The current resolution helper must use its own fingerprint.");
            session.SetResolution(ClothingSlot.Bra, Resolution("new"), "changed");
            Check.False(
                session.IsCurrentResolutionSuppressed(ClothingSlot.Bra),
                "A changed source fingerprint must not inherit old suppression.");
            Check.False(
                session.RegisterPortableLayerClear(ClothingSlot.Top, portable),
                "Top must never register direct legacy suppression.");
        }

        private static void ConversionTrackingReset()
        {
            CharacterLegacyMaskSession session = EmptySession();
            session.RegisterPortableLayerClear(ClothingSlot.Bottom, PortableLayer("bottom-clear"));
            session.RegisterPortableLayerClear(ClothingSlot.Bra, PortableLayer("bra-clear"));

            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.Granted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bottom, "bottom-attempt"),
                "The Bottom attempt must be recorded.");
            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.Granted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bra, "bra-attempt"),
                "The Bra attempt must be recorded.");

            session.ResetConversionTrackingForSlots(
                new ClothingSlot[] { ClothingSlot.Bottom });
            Check.False(
                session.IsFingerprintSuppressed(ClothingSlot.Bottom, "bottom-clear"),
                "Selected reset must clear selected suppression.");
            Check.True(
                session.IsFingerprintSuppressed(ClothingSlot.Bra, "bra-clear"),
                "Selected reset must preserve unselected suppression.");
            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.Granted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bottom, "bottom-attempt"),
                "Selected reset must clear the selected attempt key.");
            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.AlreadyAttempted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bra, "bra-attempt"),
                "Selected reset must preserve an unselected attempt key.");

            session.ResetConversionTracking();
            Check.False(
                session.IsFingerprintSuppressed(ClothingSlot.Bra, "bra-clear"),
                "Complete reset must clear every suppression.");
            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.Granted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bra, "bra-attempt"),
                "Complete reset must clear every attempt key.");
            Check.Throws<ArgumentNullException>(
                delegate { session.ResetConversionTrackingForSlots(null); },
                "Selected reset requires an explicit slot sequence.");
        }

        private static void AutoConversionAttemptArbitration()
        {
            CharacterLegacyMaskSession session = EmptySession();
            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.Granted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bottom, "bottom-v1"),
                "The first new attempt in a refresh must be granted.");
            Check.Equal(
                LegacyAutoConversionAttemptDecision.AlreadyAttempted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bottom, "bottom-v1"),
                "An identical prior attempt must be rejected before consuming refresh quota.");
            Check.Equal(
                LegacyAutoConversionAttemptDecision.DeferredUntilNextRefresh,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bra, "bra-v1"),
                "A second new attempt must be deferred.");
            Check.True(
                session.ConsumeDirtySlots().Contains(ClothingSlot.Bra),
                "A deferred conversion must put its slot back into the dirty set.");

            session.BeginAutoConversionRefresh();
            Check.Equal(
                LegacyAutoConversionAttemptDecision.AlreadyAttempted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bottom, "bottom-v1"),
                "A prior attempt key must survive refresh boundaries.");
            Check.Equal(
                LegacyAutoConversionAttemptDecision.Granted,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bra, "bra-v1"),
                "An already-attempted key must not consume the new refresh quota.");
            Check.Equal(
                LegacyAutoConversionAttemptDecision.DeferredUntilNextRefresh,
                session.TryBeginAutoConversionAttempt(ClothingSlot.Bottom, "bottom-v2"),
                "A changed key must be deferred after another slot consumes the quota.");
            Check.True(
                session.ConsumeDirtySlots().Contains(ClothingSlot.Bottom),
                "A changed deferred key must remain pending.");
        }

        private static void SlotValidationAndEncapsulation()
        {
            CharacterLegacyMaskSession session = EmptySession();
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { session.GetResolution((ClothingSlot)(-1)); },
                "Negative slots must be rejected.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { session.MarkSlotDirty((ClothingSlot)9); },
                "Slots beyond OutdoorShoes must be rejected.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { session.SetResolution(ClothingSlot.Top, Resolution("top"), "invalid"); },
                "Top must not accept a direct legacy resolution.");
            Check.Throws<ArgumentOutOfRangeException>(
                delegate { session.DeferSlot(ClothingSlot.Top); },
                "Top must not be deferred as a direct legacy slot.");

            Type type = typeof(CharacterLegacyMaskSession);
            FieldInfo[] exposedFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Check.Equal(0, exposedFields.Length, "The session must expose no mutable fields.");
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < properties.Length; index++)
            {
                Check.False(
                    properties[index].PropertyType.IsArray,
                    "No session property may expose an internal array.");
            }

            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                Check.False(method.ReturnType.IsArray, "No method may return an internal array.");
                ParameterInfo[] parameters = method.GetParameters();
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    Check.False(
                        parameters[parameterIndex].ParameterType.IsArray,
                        "The typed API must not exchange raw arrays.");
                }
            }
        }

        private static CharacterLegacyMaskSession EmptySession()
        {
            CharacterLegacyMaskSession session = new CharacterLegacyMaskSession();
            session.ConsumeDirtySlots();
            return session;
        }

        private static LegacyResolvedMask Resolution(string fingerprint)
        {
            return new LegacyResolvedMask { Fingerprint = fingerprint };
        }

        private static ClothingMaskLayerData PortableLayer(string fingerprint)
        {
            return new ClothingMaskLayerData
            {
                SourceContract = MaskSourceContract.NakayRgbStateCoverage,
                SourceProviderId = LegacyMaskDescriptor.ProviderIdValue,
                SourceFingerprint = fingerprint
            };
        }
    }
}
