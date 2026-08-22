using System;
using System.Collections.Generic;

namespace Morgott.Oracle
{
    /// <summary>
    /// Outcome of evaluating whether a perk swap is permitted, before any reflection/Unity work.
    /// </summary>
    public enum PerkSwapVerdict
    {
        /// <summary>The swap is allowed and should be performed.</summary>
        Allow,

        /// <summary>The slot's current perk is not learned by the soldier; ignore the click.</summary>
        DenyNotLearned,

        /// <summary>The chosen perk equals the slot's current perk; nothing to do.</summary>
        DenySameAsCurrent,

        /// <summary>The chosen perk is already owned by the soldier elsewhere; skip to avoid a duplicate.</summary>
        DenyAlreadyOwned,

        /// <summary>Inputs were missing (null def / null owned set); cannot decide, treat as deny.</summary>
        DenyInvalidInput,

        /// <summary>
        /// The swap is gated behind the "Operative Reconditioning" research and that research has not been
        /// completed for the soldier's faction. The caller surfaces a localized feedback message and leaves
        /// the wiki open. Set only by the research gate in the entry point, never by <see cref="Evaluate{T}"/>
        /// (which stays Unity-free and game-API-free).
        /// </summary>
        DenyResearchLocked,

        /// <summary>
        /// Removing the slot's current perk would strip a weapon proficiency an already-acquired TFTV
        /// drill requires, invalidating that drill. Hard deny (no option bypasses it).
        /// </summary>
        DenyDrillBreaksAcquired,

        /// <summary>
        /// The slot's current perk is an already-acquired TFTV drill and re-swapping drills is not
        /// enabled (TFTV itself does not permit it). Lifted by <see cref="DrillSwapContext.AllowDrillReSwap"/>.
        /// </summary>
        DenyDrillReSwapBlocked,

        /// <summary>
        /// The chosen perk is a TFTV drill whose unlock requirements (level / research / weapon
        /// proficiency / training facility) are not met. Lifted by
        /// <see cref="DrillSwapContext.IgnoreDrillRequirements"/>.
        /// </summary>
        DenyDrillLocked,
    }

    /// <summary>
    /// Everything the decision needs to know about TFTV drills for ONE swap, as plain bools so the gate
    /// stays Unity-/TFTV-free. Built by <see cref="PerkSwapper"/> from <see cref="TftvDrillsBridge"/>;
    /// null (TFTV absent) means the drill gates are skipped entirely and behaviour is pre-drills exact.
    /// </summary>
    public sealed class DrillSwapContext
    {
        /// <summary>The clicked perk is one of TFTV's drills.</summary>
        public bool ChosenIsDrill;

        /// <summary>TFTV's own <c>IsDrillUnlocked</c> verdict for the clicked drill.</summary>
        public bool ChosenDrillUnlocked;

        /// <summary>The slot's current perk is a drill the soldier has already acquired.</summary>
        public bool CurrentIsAcquiredDrill;

        /// <summary>Removing the current perk would invalidate an already-acquired drill.</summary>
        public bool RemovalBreaksAcquiredDrill;

        /// <summary>Config option: take a drill even with its unlock requirements unmet.</summary>
        public bool IgnoreDrillRequirements;

        /// <summary>Config option: allow swapping an already-acquired drill away again.</summary>
        public bool AllowDrillReSwap;

        /// <summary>
        /// The chosen perk is the ability this slot originally held (<c>PerkSwapContext.OriginalDef</c>),
        /// i.e. the click is a RESTORE, not a re-swap. Putting a slot back to its default must always be
        /// possible, so this lifts the acquired-drill re-swap gate exactly like
        /// <see cref="AllowDrillReSwap"/> would — and nothing else: the duplicate guards and the
        /// acquired-drill proficiency guard still apply.
        /// </summary>
        public bool IsRevertToOriginal;

        /// <summary>
        /// Fold TFTV's nullable SAFETY answers into <see cref="RemovalBreaksAcquiredDrill"/>. A
        /// <c>null</c> means the bridge could NOT obtain the answer (member unresolved / TFTV threw), and
        /// an unknown safety answer must count exactly like "yes, this would break an acquired drill" —
        /// fail closed. Pure so the fail-closed rule is unit-tested engine-free.
        /// <paramref name="targetDrillLosesProficiency"/> is TFTV's target-drill direction of the same
        /// check (DrillsUnlock.TargetDrillLosesWeaponProficiencyRequirement:202): removing the current
        /// perk would strip a proficiency the CHOSEN drill itself requires. It defaults to <c>false</c>
        /// ("not applicable" — the chosen perk is not a drill), never to the unknown sentinel.
        /// </summary>
        public static bool SafetyDenies(bool? currentIsAcquiredDrill, bool? removalBreaksAcquiredDrill,
            bool? targetDrillLosesProficiency = false)
        {
            return currentIsAcquiredDrill == null
                   || removalBreaksAcquiredDrill == null
                   || removalBreaksAcquiredDrill == true
                   || targetDrillLosesProficiency == null
                   || targetDrillLosesProficiency == true;
        }
    }

    /// <summary>
    /// Pure, Unity-free decision core for the perk-swap feature. Given the chosen perk, the slot's
    /// current perk and the set of perks the soldier already owns, it decides whether the swap is
    /// permitted. Kept generic (<typeparamref name="T"/>) and dependency-free so it links and unit-tests
    /// under net8 with fakes, exactly like <see cref="PerkPoolResolver"/>. The reflection/write side
    /// lives in <see cref="PerkSwapper"/>.
    /// </summary>
    public static class PerkSwapDecision
    {
        /// <summary>
        /// Decide whether <paramref name="chosen"/> may replace <paramref name="current"/> in a slot,
        /// given the soldier's currently-owned perks <paramref name="owned"/>.
        /// Gating order mirrors the design spec:
        ///   1. current must be owned (learned) — else <see cref="PerkSwapVerdict.DenyNotLearned"/>;
        ///   2. chosen == current — <see cref="PerkSwapVerdict.DenySameAsCurrent"/> (no-op);
        ///   3. chosen already owned elsewhere, or already scheduled in a later (locked) slot of the
        ///      soldier's track — <see cref="PerkSwapVerdict.DenyAlreadyOwned"/>;
        ///   4. otherwise <see cref="PerkSwapVerdict.Allow"/>.
        /// </summary>
        /// <typeparam name="T">Def type (a real TacticalAbilityDef in game; a fake in tests).</typeparam>
        /// <param name="chosen">The candidate perk clicked in the wiki.</param>
        /// <param name="current">The perk currently in the slot.</param>
        /// <param name="owned">The perks the soldier already has; membership tested via Contains.</param>
        /// <param name="comparer">Optional equality comparer; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
        /// <param name="scheduled">
        /// Optional: perks already baked into OTHER slots of the soldier's track, including future,
        /// not-yet-unlocked ones. Kept SEPARATE from <paramref name="owned"/> on purpose — folding it in
        /// would make locked slots look learned and wrongly open swaps on them.
        /// </param>
        public static PerkSwapVerdict Evaluate<T>(
            T chosen,
            T current,
            IReadOnlyCollection<T> owned,
            IEqualityComparer<T> comparer = null,
            IReadOnlyCollection<T> scheduled = null,
            DrillSwapContext drills = null)
        {
            comparer = comparer ?? EqualityComparer<T>.Default;

            // Null chosen def or missing owned set => cannot decide.
            if (chosen == null || owned == null)
            {
                return PerkSwapVerdict.DenyInvalidInput;
            }

            // 1) The slot's current perk must be learned (present in owned). A not-yet-learned slot is
            //    out of scope: the click is ignored and the wiki keeps displaying.
            if (current == null || !Contains(owned, current, comparer))
            {
                return PerkSwapVerdict.DenyNotLearned;
            }

            // 2) Picking the same perk is a no-op.
            if (comparer.Equals(chosen, current))
            {
                return PerkSwapVerdict.DenySameAsCurrent;
            }

            // 3) The chosen perk is already owned in another slot: a swap would create a duplicate, so
            //    skip (and the caller logs it).
            if (Contains(owned, chosen, comparer))
            {
                return PerkSwapVerdict.DenyAlreadyOwned;
            }

            // 3b) The chosen perk is already scheduled in a later, not-yet-unlocked slot of the track:
            //     swapping it in early would hand the soldier the perk twice / ~4 levels early.
            if (scheduled != null && Contains(scheduled, chosen, comparer))
            {
                return PerkSwapVerdict.DenyAlreadyOwned;
            }

            // 4) TFTV drills. Skipped entirely when drills is null (TFTV absent) => behaviour above is
            //    the complete gate, exactly as before drills existed. Note there is NO separate
            //    "already has this drill" check: an acquired drill lives in Progression.Abilities (TFTV
            //    LearnAbility) and in its track slot, so steps 3/3b above already deduplicate it.
            if (drills != null)
            {
                // 4a) Hard guard: never strip a proficiency an acquired drill depends on. No option lifts
                //     this — it would silently invalidate a drill the soldier already paid for.
                if (drills.RemovalBreaksAcquiredDrill)
                {
                    return PerkSwapVerdict.DenyDrillBreaksAcquired;
                }

                // 4b) Swapping an acquired drill away is a TFTV-forbidden move; opt-in only. A revert to
                //     the slot's own stored original is a RESTORE, not a re-swap: the player must always
                //     be able to undo what this mod did, so it is allowed regardless of the option.
                if (drills.CurrentIsAcquiredDrill && !drills.AllowDrillReSwap && !drills.IsRevertToOriginal)
                {
                    return PerkSwapVerdict.DenyDrillReSwapBlocked;
                }

                // 4c) The chosen drill's own unlock requirements; opt-in bypass.
                if (drills.ChosenIsDrill && !drills.ChosenDrillUnlocked && !drills.IgnoreDrillRequirements)
                {
                    return PerkSwapVerdict.DenyDrillLocked;
                }
            }

            return PerkSwapVerdict.Allow;
        }

        /// <summary>
        /// Effective skill-point cost of a swap: the configured cost when the cost toggle is on, else 0.
        /// Values &lt;= 0 are clamped to 0 (free). Pure so the clamp is unit-tested engine-free; the actual
        /// SP charge/affordability lives in <see cref="PerkSwapper"/>.
        /// </summary>
        public static int EffectiveCost(bool costEnabled, int configuredCost)
        {
            if (!costEnabled || configuredCost <= 0)
            {
                return 0;
            }
            return configuredCost;
        }

        /// <summary>
        /// TFTV's own flat price for moving a drill onto an ALREADY-LEARNED ability
        /// (<c>refs/TFTV-src TFTVDrills/DrillsUI.cs:9 — private const int SwapSpCost = 10</c>). Used only
        /// as the fallback when that literal cannot be read out of the installed TFTV at runtime
        /// (see <c>TftvDrillsBridge.DrillSwapSpCost</c>); the single place the number is written down.
        /// </summary>
        public const int TftvDrillSwapSpCostFallback = 10;

        /// <summary>
        /// Price of ONE swap, in skill points. A TFTV drill follows TFTV's OWN rule so PerkOracle never
        /// charges more than taking the drill through TFTV's UI would
        /// (<c>DrillSwapUI.cs:379 — baseAbilityLearned ? SwapSpCost : max(0, baseAbilityCost)</c>); any
        /// other perk keeps the mod's configured cost. The free-swap master toggle
        /// (<paramref name="costEnabled"/>) still zeroes both. Pure so the price rule is unit-tested
        /// engine-free; the live inputs are gathered in <c>PerkSwapper.ResolveSwapCost</c>.
        /// </summary>
        /// <param name="chosenIsDrill">The clicked perk is one of TFTV's drills.</param>
        /// <param name="currentAbilityLearned">The slot's current ability is already learned (a real replace).</param>
        /// <param name="drillSwapSpCost">TFTV's flat replace price (its <c>SwapSpCost</c>).</param>
        /// <param name="slotCost">The slot's normal purchase price (<c>Progression.GetAbilitySlotCost</c>).</param>
        public static int SwapCost(bool chosenIsDrill, bool currentAbilityLearned, int drillSwapSpCost,
            int slotCost, bool costEnabled, int configuredCost)
        {
            if (!costEnabled)
            {
                return 0;
            }
            if (!chosenIsDrill)
            {
                return EffectiveCost(true, configuredCost);
            }
            int cost = currentAbilityLearned ? drillSwapSpCost : slotCost;
            return cost > 0 ? cost : 0;
        }

        /// <summary>
        /// Whether a wiki candidate cell must render in the dim "not a choice" look: the perk the slot
        /// already holds, or any candidate whose verdict is not <see cref="PerkSwapVerdict.Allow"/>
        /// (already owned / drill locked / re-swap blocked …). Such a cell is also made inert at the
        /// click site, so bright == clickable is an invariant of the grid.
        /// </summary>
        public static bool DimsCell(bool isCurrent, PerkSwapVerdict verdict)
        {
            return isCurrent || verdict != PerkSwapVerdict.Allow;
        }

        private static bool Contains<T>(IReadOnlyCollection<T> owned, T value, IEqualityComparer<T> comparer)
        {
            foreach (T item in owned)
            {
                if (comparer.Equals(item, value))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
