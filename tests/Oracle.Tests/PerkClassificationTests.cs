using System;
using System.Collections.Generic;
using PhoenixPoint.Common.Entities.Characters;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    public class PerkClassificationTests
    {
        // Random for slots 0, 3, 4 (mirrors the shipped TFTV config: Background/Proficiency).
        private static readonly Func<int, bool> ShippedRandom =
            level0 => level0 == 0 || level0 == 3 || level0 == 4;

        private static readonly Func<int, bool> NeverRandom = _ => false;

        [Theory]
        // Empty cell -> Unknown, regardless of source/bridge.
        [InlineData(AbilityTrackSource.Personal, 0, false, true, PerkKind.Unknown)]
        [InlineData(AbilityTrackSource.PrimaryClass, 0, false, true, PerkKind.Unknown)]
        [InlineData(AbilityTrackSource.Personal, 0, false, false, PerkKind.Unknown)]
        // Class rows are always Fixed when present.
        [InlineData(AbilityTrackSource.PrimaryClass, 0, true, true, PerkKind.Fixed)]
        [InlineData(AbilityTrackSource.SecondaryClass, 2, true, true, PerkKind.Fixed)]
        [InlineData(AbilityTrackSource.PrimaryClass, 0, true, false, PerkKind.Fixed)]
        // Personal + bridge available: follow IsSlotRandom.
        [InlineData(AbilityTrackSource.Personal, 0, true, true, PerkKind.Rolled)] // random slot
        [InlineData(AbilityTrackSource.Personal, 3, true, true, PerkKind.Rolled)] // random slot
        [InlineData(AbilityTrackSource.Personal, 4, true, true, PerkKind.Rolled)] // random slot
        [InlineData(AbilityTrackSource.Personal, 1, true, true, PerkKind.Fixed)]  // fixed slot
        [InlineData(AbilityTrackSource.Personal, 2, true, true, PerkKind.Fixed)]  // fixed slot
        [InlineData(AbilityTrackSource.Personal, 5, true, true, PerkKind.Fixed)]  // fixed slot
        // Personal + bridge unavailable (no TFTV): personal track is purely rolled.
        [InlineData(AbilityTrackSource.Personal, 0, true, false, PerkKind.Rolled)]
        [InlineData(AbilityTrackSource.Personal, 2, true, false, PerkKind.Rolled)]
        public void Classify_Cases(
            AbilityTrackSource source,
            int level0,
            bool abilityPresent,
            bool bridgeAvailable,
            PerkKind expected)
        {
            PerkKind actual = PerkClassification.Classify(
                source, level0, abilityPresent, bridgeAvailable, ShippedRandom);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Personal_AllFixedConfig_AreFixed()
        {
            for (int level0 = 0; level0 < 7; level0++)
            {
                PerkKind kind = PerkClassification.Classify(
                    AbilityTrackSource.Personal, level0, true, bridgeAvailable: true, NeverRandom);
                Assert.Equal(PerkKind.Fixed, kind);
            }
        }

        [Fact]
        public void Personal_NullLookup_WhenBridgeAvailable_IsFixed()
        {
            // Defensive: a null isSlotRandom must not throw; treated as not-random -> Fixed.
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, 0, true, bridgeAvailable: true, isSlotRandom: null);
            Assert.Equal(PerkKind.Fixed, kind);
        }

        [Fact]
        public void Personal_NullLookup_WhenBridgeUnavailable_IsRolled()
        {
            // No TFTV: lookup is irrelevant, personal == rolled.
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, 0, true, bridgeAvailable: false, isSlotRandom: null);
            Assert.Equal(PerkKind.Rolled, kind);
        }

        [Theory]
        // Not a rolled-pool member (ability lacks PersonalProgressionTag: augmentation / custom-mod
        // ability): present Personal cells are never rolled perks -> Fixed, regardless of TFTV bridge
        // state, slot index, or IsSlotRandom result. This is the bug fix: tagless augmentation cells
        // share the Personal track with a human's rolled perks but must not be highlighted.
        [InlineData(0, true, true)]   // bridge available, would-be-random slot under TFTV
        [InlineData(3, true, true)]   // another would-be-random slot
        [InlineData(1, true, true)]   // would-be-fixed slot (still Fixed)
        [InlineData(0, true, false)]  // no TFTV: whole Personal track would otherwise be Rolled
        [InlineData(4, true, false)]  // no TFTV, different slot
        public void Personal_NotPoolMember_IsFixed_NotRolled(int level0, bool bridgeAvailable, bool useShippedRandom)
        {
            Func<int, bool> lookup = useShippedRandom ? ShippedRandom : NeverRandom;
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, level0, abilityPresent: true,
                bridgeAvailable: bridgeAvailable, isSlotRandom: lookup, abilityIsRolledPoolMember: false);
            Assert.Equal(PerkKind.Fixed, kind);
        }

        [Theory]
        // Contrast: the SAME inputs for a rolled-pool member (tagged ability) keep the existing
        // human (vanilla / TFTV) behavior byte-for-byte.
        [InlineData(0, true, true, PerkKind.Rolled)]   // TFTV random slot -> Rolled
        [InlineData(1, true, true, PerkKind.Fixed)]    // TFTV fixed slot -> Fixed
        [InlineData(0, false, true, PerkKind.Rolled)]  // no TFTV -> Rolled
        public void Personal_PoolMember_UnchangedBehavior(
            int level0, bool bridgeAvailable, bool useShippedRandom, PerkKind expected)
        {
            Func<int, bool> lookup = useShippedRandom ? ShippedRandom : NeverRandom;
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, level0, abilityPresent: true,
                bridgeAvailable: bridgeAvailable, isSlotRandom: lookup, abilityIsRolledPoolMember: true);
            Assert.Equal(expected, kind);
        }

        [Fact]
        public void EmptyCell_NotPoolMember_IsUnknown()
        {
            // The empty-cell short-circuit precedes the pool-membership precondition: nothing to classify.
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, 0, abilityPresent: false,
                bridgeAvailable: true, isSlotRandom: ShippedRandom, abilityIsRolledPoolMember: false);
            Assert.Equal(PerkKind.Unknown, kind);
        }

        [Fact]
        public void Personal_NotPoolMember_NoBridge_GatesWholeTrackRolled()
        {
            // Precedence: the precondition gates the no-bridge (vanilla) whole-Personal-track-Rolled
            // path -- the most aggressive Rolled branch. Tagless ability -> Fixed, not Rolled.
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, 0, abilityPresent: true,
                bridgeAvailable: false, isSlotRandom: NeverRandom, abilityIsRolledPoolMember: false);
            Assert.Equal(PerkKind.Fixed, kind);
        }

        [Fact]
        public void Personal_NotPoolMember_TftvRandomSlot_GatesIsSlotRandom()
        {
            // Precedence: the precondition gates the TFTV IsSlotRandom==true branch too. Tagless
            // ability whose slot index would be random under TFTV -> Fixed, not Rolled.
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal, 0, abilityPresent: true,
                bridgeAvailable: true, isSlotRandom: _ => true, abilityIsRolledPoolMember: false);
            Assert.Equal(PerkKind.Fixed, kind);
        }

        // ---- right-click swap eligibility -------------------------------------------------------

        [Fact]
        public void SwapEligible_RolledCell_IsEligible()
        {
            Assert.True(PerkClassification.IsSwapEligible(
                AbilityTrackSource.Personal, PerkKind.Rolled,
                hasStoreBaseline: false, abilityIsDrill: false));
        }

        [Fact]
        public void SwapEligible_FixedCellWithStoreBaseline_IsEligible()
        {
            // The regression: once the wiki swaps a slot to something outside its rolled pool the cell
            // classifies Fixed, but the store baseline proves PerkOracle owns the slot -> keep it editable.
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = OriginalPerkStore.BuildKey(7, 3, "Personal");
            OriginalPerkStore.Record(map, key, "RolledPerk", "TftvDrill");
            Assert.True(OriginalPerkStore.HasBaseline(map, key));

            Assert.True(PerkClassification.IsSwapEligible(
                AbilityTrackSource.Personal, PerkKind.Fixed,
                hasStoreBaseline: OriginalPerkStore.HasBaseline(map, key), abilityIsDrill: false));
        }

        [Fact]
        public void SwapEligible_FixedCellHoldingDrill_IsEligible()
        {
            // Slot swapped to a drill by TFTV's own DrillSwapUI: no baseline of ours, drill signal carries it.
            Assert.True(PerkClassification.IsSwapEligible(
                AbilityTrackSource.Personal, PerkKind.Fixed,
                hasStoreBaseline: false, abilityIsDrill: true));
        }

        [Fact]
        public void SwapEligible_UntouchedFixedCell_IsNotEligible()
        {
            // No baseline, no drill (drills unavailable reports false) -> vanilla cancel must pass through.
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            Assert.False(OriginalPerkStore.HasBaseline(map, OriginalPerkStore.BuildKey(7, 3, "Personal")));

            Assert.False(PerkClassification.IsSwapEligible(
                AbilityTrackSource.Personal, PerkKind.Fixed,
                hasStoreBaseline: false, abilityIsDrill: false));
        }

        [Theory]
        // Never eligible: class rows (whatever the extra signals say) and empty cells.
        [InlineData(AbilityTrackSource.PrimaryClass, PerkKind.Fixed, true, true)]
        [InlineData(AbilityTrackSource.SecondaryClass, PerkKind.Rolled, true, true)]
        [InlineData(AbilityTrackSource.Personal, PerkKind.Unknown, true, true)]
        public void SwapEligible_NonPersonalOrEmpty_IsNotEligible(
            AbilityTrackSource source, PerkKind kind, bool hasStoreBaseline, bool abilityIsDrill)
        {
            Assert.False(PerkClassification.IsSwapEligible(source, kind, hasStoreBaseline, abilityIsDrill));
        }

        [Fact]
        public void SwapEligible_BaselineClearedOnRevert_FallsBackToClassification()
        {
            // Swapping back to the original drops the entry; the slot is Rolled again, so it stays
            // eligible through the classification half rather than the baseline half.
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = OriginalPerkStore.BuildKey(7, 3, "Personal");
            OriginalPerkStore.Record(map, key, "RolledPerk", "TftvDrill");
            OriginalPerkStore.Record(map, key, "TftvDrill", "RolledPerk");
            Assert.False(OriginalPerkStore.HasBaseline(map, key));

            Assert.True(PerkClassification.IsSwapEligible(
                AbilityTrackSource.Personal, PerkKind.Rolled,
                hasStoreBaseline: false, abilityIsDrill: false));
        }
    }
}
