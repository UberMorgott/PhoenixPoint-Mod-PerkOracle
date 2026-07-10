using System.Collections.Generic;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure perk-swap decision gate. Defs are faked with plain strings so the
    /// gating logic is exercised without Unity/TFTV. Mirrors PerkPoolResolverTests' style.
    /// </summary>
    public class PerkSwapDecisionTests
    {
        // A soldier who has learned A (the current slot perk) and C (some other slot).
        private static List<string> Owned() => new List<string> { "A", "C" };

        [Fact]
        public void NotLearned_CurrentMissingFromOwned_Denies()
        {
            // Slot's current perk "X" is not in owned => slot is not learned => ignore.
            var verdict = PerkSwapDecision.Evaluate("B", "X", Owned());
            Assert.Equal(PerkSwapVerdict.DenyNotLearned, verdict);
        }

        [Fact]
        public void ChosenEqualsCurrent_Denies()
        {
            var verdict = PerkSwapDecision.Evaluate("A", "A", Owned());
            Assert.Equal(PerkSwapVerdict.DenySameAsCurrent, verdict);
        }

        [Fact]
        public void ChosenAlreadyOwnedElsewhere_Denies()
        {
            // Current "A" is learned; chosen "C" is already owned in another slot => duplicate guard.
            var verdict = PerkSwapDecision.Evaluate("C", "A", Owned());
            Assert.Equal(PerkSwapVerdict.DenyAlreadyOwned, verdict);
        }

        [Fact]
        public void ValidSwap_Allows()
        {
            // Current "A" learned; chosen "B" differs and is not owned => allow.
            var verdict = PerkSwapDecision.Evaluate("B", "A", Owned());
            Assert.Equal(PerkSwapVerdict.Allow, verdict);
        }

        [Fact]
        public void NullChosen_DeniesInvalidInput()
        {
            var verdict = PerkSwapDecision.Evaluate<string>(null, "A", Owned());
            Assert.Equal(PerkSwapVerdict.DenyInvalidInput, verdict);
        }

        [Fact]
        public void NullOwned_DeniesInvalidInput()
        {
            var verdict = PerkSwapDecision.Evaluate("B", "A", null);
            Assert.Equal(PerkSwapVerdict.DenyInvalidInput, verdict);
        }

        [Fact]
        public void NullCurrent_TreatedAsNotLearned()
        {
            // An empty slot (no current perk) is not a learned slot => out of scope.
            var verdict = PerkSwapDecision.Evaluate("B", null, Owned());
            Assert.Equal(PerkSwapVerdict.DenyNotLearned, verdict);
        }

        [Fact]
        public void EffectiveCost_Disabled_IsFree()
        {
            Assert.Equal(0, PerkSwapDecision.EffectiveCost(false, 50));
        }

        [Fact]
        public void EffectiveCost_EnabledPositive_ReturnsConfigured()
        {
            Assert.Equal(50, PerkSwapDecision.EffectiveCost(true, 50));
        }

        [Fact]
        public void EffectiveCost_NegativeConfig_ClampsToFree()
        {
            Assert.Equal(0, PerkSwapDecision.EffectiveCost(true, -10));
        }

        [Fact]
        public void EffectiveCost_ZeroConfig_IsFree()
        {
            Assert.Equal(0, PerkSwapDecision.EffectiveCost(true, 0));
        }
    }
}
