using System;
using PhoenixPoint.Common.Entities.Characters;
using Morgott.PerkOracle;
using Xunit;

namespace Morgott.PerkOracle.Tests
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
    }
}
