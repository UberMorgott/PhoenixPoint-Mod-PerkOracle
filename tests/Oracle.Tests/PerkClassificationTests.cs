using System;
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
    }
}
