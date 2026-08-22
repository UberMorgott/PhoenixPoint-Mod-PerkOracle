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

        // ---- fail-closed on an UNKNOWN TFTV safety answer -------------------------------------------

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, false)]
        [InlineData(false, null)]
        [InlineData(false, true)]
        [InlineData(true, null)]
        public void SafetyDenies_UnknownOrBreaking_Denies(bool? currentIsAcquired, bool? removalBreaks)
        {
            Assert.True(DrillSwapContext.SafetyDenies(currentIsAcquired, removalBreaks));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        public void SafetyDenies_BothKnownAndSafe_Allows(bool? currentIsAcquired, bool? removalBreaks)
        {
            Assert.False(DrillSwapContext.SafetyDenies(currentIsAcquired, removalBreaks));
        }

        [Fact]
        public void UnknownSafetyAnswer_ReachesTheGateAsAHardDeny()
        {
            // What PerkSwapper.BuildDrillContext builds when the bridge could not answer: an unknown
            // safety result folds into RemovalBreaksAcquiredDrill, which no option can lift.
            var unknown = new DrillSwapContext
            {
                RemovalBreaksAcquiredDrill = DrillSwapContext.SafetyDenies(null, null),
                IgnoreDrillRequirements = true,
                AllowDrillReSwap = true,
                IsRevertToOriginal = true,
            };
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: unknown));
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

        // ---- fail-closed on the TARGET-drill direction of the proficiency check ---------------------

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        public void SafetyDenies_TargetDrillLosesProficiency_UnknownOrBreaking_Denies(bool? targetLoses)
        {
            // Both slot-side answers are known-safe; only TFTV's target-drill answer objects (or is
            // unavailable) — an unknown must count exactly like "yes, it breaks".
            Assert.True(DrillSwapContext.SafetyDenies(false, false, targetLoses));
        }

        [Fact]
        public void SafetyDenies_TargetDrillKeepsProficiency_Allows()
        {
            Assert.False(DrillSwapContext.SafetyDenies(false, false, false));
        }

        [Fact]
        public void SafetyDenies_TargetDrillArgumentDefaultsToNotApplicable()
        {
            // The chosen perk is not a drill => the target-drill check does not apply, and the default
            // must be the SAFE value (false), never the unknown sentinel.
            Assert.False(DrillSwapContext.SafetyDenies(false, false));
        }

        // ---- swap price: TFTV parity for drills, configured cost for everything else ----------------

        [Fact]
        public void SwapCost_VanillaPerk_UsesConfiguredCost()
        {
            Assert.Equal(50, PerkSwapDecision.SwapCost(
                chosenIsDrill: false, currentAbilityLearned: true,
                drillSwapSpCost: 10, slotCost: 7, costEnabled: true, configuredCost: 50));
        }

        [Fact]
        public void SwapCost_DrillOverLearnedAbility_UsesTftvFlatPrice()
        {
            // TFTV's own rule (DrillSwapUI.cs:379): replacing a LEARNED ability costs the flat SwapSpCost,
            // never PerkOracle's (higher) configured price.
            Assert.Equal(10, PerkSwapDecision.SwapCost(
                chosenIsDrill: true, currentAbilityLearned: true,
                drillSwapSpCost: 10, slotCost: 7, costEnabled: true, configuredCost: 50));
        }

        [Fact]
        public void SwapCost_DrillOverUnboughtSlot_UsesSlotCost()
        {
            Assert.Equal(7, PerkSwapDecision.SwapCost(
                chosenIsDrill: true, currentAbilityLearned: false,
                drillSwapSpCost: 10, slotCost: 7, costEnabled: true, configuredCost: 50));
        }

        [Fact]
        public void SwapCost_NegativeSlotCost_ClampsToFree()
        {
            Assert.Equal(0, PerkSwapDecision.SwapCost(
                chosenIsDrill: true, currentAbilityLearned: false,
                drillSwapSpCost: 10, slotCost: -3, costEnabled: true, configuredCost: 50));
        }

        [Fact]
        public void SwapCost_FreeSwapToggleOff_ZeroesBothPaths()
        {
            Assert.Equal(0, PerkSwapDecision.SwapCost(true, true, 10, 7, costEnabled: false, configuredCost: 50));
            Assert.Equal(0, PerkSwapDecision.SwapCost(false, true, 10, 7, costEnabled: false, configuredCost: 50));
        }

        [Fact]
        public void SwapCost_DrillPriceIgnoresConfiguredCostEntirely()
        {
            // A rebalanced TFTV SwapSpCost follows automatically; PerkOracle's own number never leaks in.
            Assert.Equal(3, PerkSwapDecision.SwapCost(true, true, 3, 7, true, 999));
        }

        // ---- denied candidates must LOOK denied ------------------------------------------------------

        [Fact]
        public void DimsCell_AllowedCandidate_StaysBright()
        {
            Assert.False(PerkSwapDecision.DimsCell(isCurrent: false, verdict: PerkSwapVerdict.Allow));
        }

        [Fact]
        public void DimsCell_CurrentPerk_Dims()
        {
            Assert.True(PerkSwapDecision.DimsCell(isCurrent: true, verdict: PerkSwapVerdict.Allow));
        }

        [Theory]
        [InlineData(PerkSwapVerdict.DenyAlreadyOwned)]
        [InlineData(PerkSwapVerdict.DenyDrillLocked)]
        [InlineData(PerkSwapVerdict.DenyDrillReSwapBlocked)]
        [InlineData(PerkSwapVerdict.DenyDrillBreaksAcquired)]
        [InlineData(PerkSwapVerdict.DenyNotLearned)]
        [InlineData(PerkSwapVerdict.DenySameAsCurrent)]
        [InlineData(PerkSwapVerdict.DenyInvalidInput)]
        [InlineData(PerkSwapVerdict.DenyDrillPriceUnresolved)]
        public void DimsCell_AnyDeniedVerdict_Dims(PerkSwapVerdict verdict)
        {
            Assert.True(PerkSwapDecision.DimsCell(isCurrent: false, verdict: verdict));
        }

        // ---- fail-closed contexts: TFTV present but un-interrogable --------------------------------

        [Fact]
        public void FaultedDrillContract_Denies()
        {
            // TFTV's drills ARE installed but its contract did not resolve (member renamed / signature
            // changed). Nothing about the soldier's drills can be verified, so the swap must be denied —
            // reading a fault as "TFTV absent" was the fail-open this context exists to close.
            var verdict = PerkSwapDecision.Evaluate("B", "A", Owned(), drills: DrillSwapContext.Denied());
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired, verdict);
        }

        [Fact]
        public void UnknownDrillList_Denies()
        {
            // A FAILED read of TFTV's drill list must not degrade into a valid empty list: an empty
            // snapshot would make the acquired-drill checks trivially pass. Same fail-closed context.
            var verdict = PerkSwapDecision.Evaluate("B", "A", Owned(), drills: DrillSwapContext.Denied());
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired, verdict);
        }

        [Fact]
        public void FaultedContext_DeniesEvenTheRevertToOriginal()
        {
            // The revert-to-original escape hatch lifts the re-swap gate, never the hard safety guard.
            DrillSwapContext denied = DrillSwapContext.Denied();
            denied.IsRevertToOriginal = true;
            denied.AllowDrillReSwap = true;
            denied.IgnoreDrillRequirements = true;
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: denied));
        }

        // ---- unresolved drill price ----------------------------------------------------------------

        [Fact]
        public void DrillPriceUnresolved_DeniesTheDrillSwap()
        {
            // TFTV present, drill unlocked, nothing unsafe — but its SwapSpCost could not be read, so
            // there is no defensible price. Refuse rather than charge an invented number.
            var ctx = new DrillSwapContext
            {
                ChosenIsDrill = true,
                ChosenDrillUnlocked = true,
                DrillPriceUnresolved = true,
            };
            Assert.Equal(PerkSwapVerdict.DenyDrillPriceUnresolved,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: ctx));
        }

        [Fact]
        public void DrillPriceUnresolved_DoesNotBlockAVanillaPerk()
        {
            // The drill price is irrelevant to a non-drill swap; it keeps the mod's configured cost.
            var ctx = new DrillSwapContext { ChosenIsDrill = false, DrillPriceUnresolved = true };
            Assert.Equal(PerkSwapVerdict.Allow, PerkSwapDecision.Evaluate("B", "A", Owned(), drills: ctx));
        }

        [Fact]
        public void DrillPriceResolved_AllowsTheDrillSwap()
        {
            var ctx = new DrillSwapContext
            {
                ChosenIsDrill = true,
                ChosenDrillUnlocked = true,
                DrillPriceUnresolved = false,
            };
            Assert.Equal(PerkSwapVerdict.Allow, PerkSwapDecision.Evaluate("B", "A", Owned(), drills: ctx));
        }

        [Fact]
        public void DrillPriceUnresolved_IsCheckedAfterTheSafetyGuard()
        {
            // Ordering matters for the log/UI wording: an unsafe swap reports the SAFETY reason, not the
            // price one, even when both apply.
            var ctx = new DrillSwapContext
            {
                ChosenIsDrill = true,
                ChosenDrillUnlocked = true,
                RemovalBreaksAcquiredDrill = true,
                DrillPriceUnresolved = true,
            };
            Assert.Equal(PerkSwapVerdict.DenyDrillBreaksAcquired,
                PerkSwapDecision.Evaluate("B", "A", Owned(), drills: ctx));
        }

        [Fact]
        public void TftvAbsent_IsUnaffectedByTheNewGates()
        {
            // A null drill context (TFTV genuinely absent) must still behave exactly as before drills
            // existed: no fault deny, no price deny, no drill gate at all.
            Assert.Equal(PerkSwapVerdict.Allow, PerkSwapDecision.Evaluate("B", "A", Owned(), drills: null));
        }
    }
}
