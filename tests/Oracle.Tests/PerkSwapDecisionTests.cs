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
        public void ChosenScheduledInLaterSlot_Denies()
        {
            // "D" is not learned yet but is already baked into a later, locked slot of the track =>
            // swapping it into an early slot would grant it twice / levels early (issue #1).
            var verdict = PerkSwapDecision.Evaluate("D", "A", Owned(), scheduled: new List<string> { "D" });
            Assert.Equal(PerkSwapVerdict.DenyAlreadyOwned, verdict);
        }

        [Fact]
        public void ScheduledElsewhere_UnrelatedChoice_Allows()
        {
            // A scheduled perk the player did not pick must not block an otherwise valid swap.
            var verdict = PerkSwapDecision.Evaluate("B", "A", Owned(), scheduled: new List<string> { "D" });
            Assert.Equal(PerkSwapVerdict.Allow, verdict);
        }

        [Fact]
        public void ScheduledCurrent_StillNotLearned()
        {
            // Scheduled must NOT be folded into owned: a slot whose current perk is only scheduled
            // (not learned) stays out of scope.
            var verdict = PerkSwapDecision.Evaluate("B", "X", Owned(), scheduled: new List<string> { "X" });
            Assert.Equal(PerkSwapVerdict.DenyNotLearned, verdict);
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

        // --- TFTV drills -------------------------------------------------------------------------

        [Fact]
        public void NoDrillContext_TftvAbsent_BehavesAsBefore()
        {
            // drills == null (TFTV not installed) => no extra gate at all.
            Assert.Equal(PerkSwapVerdict.Allow, PerkSwapDecision.Evaluate("B", "A", Owned(), drills: null));
        }

        [Fact]
        public void DrillAlreadyOwned_DeniesViaExistingOwnedGate()
        {
            // An acquired drill lives in Progression.Abilities, so the shared owned/scheduled gate
            // catches it before the drill step ever runs — no parallel dedupe.
            var verdict = PerkSwapDecision.Evaluate("C", "A", Owned(),
                drills: new DrillSwapContext { ChosenIsDrill = true, ChosenDrillUnlocked = true });
            Assert.Equal(PerkSwapVerdict.DenyAlreadyOwned, verdict);
        }

        [Fact]
        public void LockedDrill_Denies_UnlessRequirementsIgnored()
        {
            var locked = new DrillSwapContext { ChosenIsDrill = true, ChosenDrillUnlocked = false };
            Assert.Equal(PerkSwapVerdict.DenyDrillLocked,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: locked));

            locked.IgnoreDrillRequirements = true; // option (a) flips the verdict
            Assert.Equal(PerkSwapVerdict.Allow,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: locked));
        }

        [Fact]
        public void AcquiredDrillInSlot_Denies_UnlessReSwapAllowed()
        {
            var acquired = new DrillSwapContext { CurrentIsAcquiredDrill = true };
            Assert.Equal(PerkSwapVerdict.DenyDrillReSwapBlocked,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: acquired));

            acquired.AllowDrillReSwap = true; // option (b) flips the verdict
            Assert.Equal(PerkSwapVerdict.Allow,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: acquired));
        }

        [Fact]
        public void RevertToOriginal_AllowedEvenWithReSwapOff()
        {
            // Slot currently holds an acquired drill; the click restores the slot's stored original.
            // A restore is not a re-swap, so it must pass with AllowDrillReSwap OFF (the default).
            var revert = new DrillSwapContext { CurrentIsAcquiredDrill = true, IsRevertToOriginal = true };
            Assert.Equal(PerkSwapVerdict.Allow,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: revert));
        }

        [Fact]
        public void NonRevertDrillReSwap_StillDeniedWithReSwapOff()
        {
            // Same slot state, but the click is an ordinary drill re-swap => still gated.
            var reswap = new DrillSwapContext { CurrentIsAcquiredDrill = true, IsRevertToOriginal = false };
            Assert.Equal(PerkSwapVerdict.DenyDrillReSwapBlocked,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: reswap));
        }

        [Fact]
        public void RevertToOriginal_StillDeniedWhenItBreaksAcquiredDrill()
        {
            // The proficiency guard outranks the restore: reverting must not invalidate a paid-for drill.
            var revert = new DrillSwapContext
            {
                CurrentIsAcquiredDrill = true,
                IsRevertToOriginal = true,
                RemovalBreaksAcquiredDrill = true,
            };
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: revert));
        }

        [Fact]
        public void RevertToOriginal_StillDeniedWhenAlreadyOwned()
        {
            // A restore does not bypass the duplicate guard.
            var revert = new DrillSwapContext { CurrentIsAcquiredDrill = true, IsRevertToOriginal = true };
            Assert.Equal(PerkSwapVerdict.DenyAlreadyOwned,
                PerkSwapDecision.Evaluate("C", "A", Owned(), drills: revert));
        }

        [Fact]
        public void RemovalBreakingAcquiredDrill_AlwaysDenies()
        {
            // Hard guard: neither option lifts it.
            var breaks = new DrillSwapContext
            {
                RemovalBreaksAcquiredDrill = true,
                IgnoreDrillRequirements = true,
                AllowDrillReSwap = true,
            };
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: breaks));
        }

        [Fact]
        public void UnlockedDrill_NotOwned_Allows()
        {
            var ok = new DrillSwapContext { ChosenIsDrill = true, ChosenDrillUnlocked = true };
            Assert.Equal(PerkSwapVerdict.Allow, PerkSwapDecision.Evaluate("B", "A", Owned(), drills: ok));
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
