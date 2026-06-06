using System;
using System.Collections.Generic;

namespace Morgott.PerkOracle
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
        ///   3. chosen already owned elsewhere — <see cref="PerkSwapVerdict.DenyAlreadyOwned"/>;
        ///   4. otherwise <see cref="PerkSwapVerdict.Allow"/>.
        /// </summary>
        /// <typeparam name="T">Def type (a real TacticalAbilityDef in game; a fake in tests).</typeparam>
        /// <param name="chosen">The candidate perk clicked in the wiki.</param>
        /// <param name="current">The perk currently in the slot.</param>
        /// <param name="owned">The perks the soldier already has; membership tested via Contains.</param>
        /// <param name="comparer">Optional equality comparer; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
        public static PerkSwapVerdict Evaluate<T>(
            T chosen,
            T current,
            IReadOnlyCollection<T> owned,
            IEqualityComparer<T> comparer = null)
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

            return PerkSwapVerdict.Allow;
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
