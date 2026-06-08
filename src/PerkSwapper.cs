using System;
using System.Collections.Generic;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Immutable bundle of everything the swap needs from the click site: the soldier, the slot being
    /// viewed, the progression module to repaint, and the 0-based slot level (best-effort, for logging).
    /// Built once when the wiki opens and passed to every icon's click handler.
    /// </summary>
    public sealed class PerkSwapContext
    {
        public readonly GeoCharacter Character;
        public readonly AbilityTrackSlot Slot;
        public readonly UIModuleCharacterProgression Module;
        public readonly int Level0;

        public PerkSwapContext(GeoCharacter character, AbilityTrackSlot slot,
            UIModuleCharacterProgression module, int level0)
        {
            Character = character;
            Slot = slot;
            Module = module;
            Level0 = level0;
        }

        /// <summary>True if the context carries the minimum needed to attempt a swap.</summary>
        public bool IsUsable =>
            Character != null
            && Character.Progression != null
            && Slot != null
            && (UnityEngine.Object)(object)Module != (UnityEngine.Object)null;
    }

    /// <summary>
    /// Encapsulates the verified 5-step write sequence that replaces a soldier's learned perk in a
    /// progression slot with a chosen one. The pure allow/deny decision lives in
    /// <see cref="PerkSwapDecision"/>; this class performs the side effects (two reflection points,
    /// mirroring TFTV's Drills helper) on the geoscape <see cref="GeoCharacter"/>, which is itself the
    /// save entity, so no explicit persist call is needed. Everything is guarded so a failure logs and
    /// returns false rather than throwing into the UI.
    /// </summary>
    public static class PerkSwapper
    {
        /// <summary>
        /// Attempt to swap the perk in <paramref name="ctx"/>'s slot to <paramref name="chosenDef"/>.
        /// Returns true only when the swap was actually applied. Runs the pure decision gate first, then
        /// the 5-step write sequence. Never throws.
        /// </summary>
        public static bool TrySwap(PerkSwapContext ctx, TacticalAbilityDef chosenDef)
        {
            try
            {
                if (ctx == null || !ctx.IsUsable || (UnityEngine.Object)(object)chosenDef == (UnityEngine.Object)null)
                {
                    return false;
                }

                CharacterProgression progression = ctx.Character.Progression;
                AbilityTrackSlot slot = ctx.Slot;
                TacticalAbilityDef oldDef = slot.Ability; // step 1

                // STUB hook: future resource-cost insertion point. When PerkSwapCostsResources is enabled a
                // later iteration will charge resources here (and may abort the swap on insufficient funds).
                // For now this is intentionally a no-op so the swap stays free regardless of the toggle.
                ApplyResourceCostStub(ctx);

                // Decision gate (learned? same? already-owned?). Owned set = progression.Abilities.
                IReadOnlyList<TacticalAbilityDef> owned = progression.Abilities;
                PerkSwapVerdict verdict = PerkSwapDecision.Evaluate(chosenDef, oldDef, owned);
                if (verdict != PerkSwapVerdict.Allow)
                {
                    if (verdict == PerkSwapVerdict.DenyAlreadyOwned)
                    {
                        PerkOracleLog.Debug("[PerkOracle] PerkSwap skipped: "
                                  + DefName(chosenDef) + " already owned by soldier.");
                    }
                    // Not-learned / same-as-current / invalid: silent no-op per design.
                    return false;
                }

                // step 2: un-learn old via reflection (no public remover on CharacterProgression).
                List<TacticalAbilityDef> abilities =
                    Traverse.Create(progression).Field("_abilities").GetValue<List<TacticalAbilityDef>>();
                if (abilities == null)
                {
                    PerkOracleLog.Debug("[PerkOracle] PerkSwap aborted: could not reflect _abilities.");
                    return false;
                }
                // If the old def is not actually in _abilities the swap would leave the slot pointing at a
                // def the soldier doesn't own. Abort BEFORE mutating slot.Ability so nothing is half-done.
                if (!abilities.Remove(oldDef))
                {
                    PerkOracleLog.Debug("[PerkOracle] PerkSwap aborted: old def "
                              + DefName(oldDef) + " was not in _abilities (nothing mutated).");
                    return false;
                }

                // Track what we've changed so the catch below can restore prior state on a partial failure.
                bool oldRemoved = true;   // we just removed oldDef from _abilities
                bool slotChanged = false; // slot.Ability re-pointed
                bool chosenAdded = false; // chosenDef learned (added to _abilities)
                try
                {
                    // step 3: point the slot at the chosen def.
                    slot.Ability = chosenDef;
                    slotChanged = true;

                    // step 4: learn the new def (public; adds to _abilities + fires OnAbilityAdded).
                    progression.AddAbility(chosenDef);
                    chosenAdded = true;

                    // step 4b: force a full stat recompute. The raw _abilities.Remove fires no recompute, and
                    // AddAbility only recomputes when the NEW def is a passive/proficiency (GeoCharacter
                    // OnAbilityAdded). Swapping a passive/proficiency OUT for a non-passive would otherwise
                    // leave the old perk's bonus applied. GeoCharacter.UpdateStats(bool) (GeoCharacter.cs:1057)
                    // rebuilds stats from Abilities, fixing both the removed-passive and added-passive sides.
                    try
                    {
                        Traverse.Create(ctx.Character).Method("UpdateStats", new[] { typeof(bool) }).GetValue(false);
                    }
                    catch (Exception ex)
                    {
                        // Data swap already committed; only the recompute failed. Log and carry on.
                        PerkOracleLog.Debug("[PerkOracle] PerkSwap stat recompute (UpdateStats) failed: " + ex.Message);
                    }

                    // step 5: repaint the progression grid via reflection (private SetAbilityTracks).
                    try
                    {
                        Traverse.Create(ctx.Module).Method("SetAbilityTracks").GetValue();
                    }
                    catch (Exception ex)
                    {
                        // The data swap already succeeded; only the immediate repaint failed. Log and carry on
                        // (the grid refreshes on the next natural redraw).
                        PerkOracleLog.Debug("[PerkOracle] PerkSwap repaint (SetAbilityTracks) failed: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    // A core mutation (slot re-point or AddAbility) threw: roll back so the soldier is never
                    // left with a slot whose def is not in _abilities. Best-effort; restore in reverse order.
                    try
                    {
                        if (chosenAdded)
                        {
                            abilities.Remove(chosenDef);
                        }
                        if (slotChanged)
                        {
                            slot.Ability = oldDef;
                        }
                        if (oldRemoved && !abilities.Contains(oldDef))
                        {
                            abilities.Add(oldDef);
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        PerkOracleLog.Debug("[PerkOracle] PerkSwap rollback failed: " + rollbackEx.Message);
                    }
                    PerkOracleLog.Debug("[PerkOracle] PerkSwap failed mid-sequence, rolled back: " + ex.Message);
                    return false;
                }

                PerkOracleLog.Debug("[PerkOracle] PerkSwap: " + DefName(oldDef) + " -> " + DefName(chosenDef)
                          + " @ level " + LevelLabel(ctx));
                return true;
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] PerkSwapper.TrySwap failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// STUB for a future "perk swap costs resources" feature. Currently does nothing (the swap is always
        /// free). Reads <c>PerkOracleMain.PerkSwapCostsResources</c> only to mark where the future cost would
        /// be charged; it intentionally has no effect and never blocks the swap. No Wallet/SkillPoints access.
        /// </summary>
        private static void ApplyResourceCostStub(PerkSwapContext ctx)
        {
            _ = ctx;
            _ = PerkOracleMain.PerkSwapCostsResources;
            // TODO(resource-cost): when implemented, charge the configured cost here and return a
            // success/failure so TrySwap can abort BEFORE mutating _abilities on insufficient funds.
        }

        private static string DefName(TacticalAbilityDef def)
        {
            if ((UnityEngine.Object)(object)def == (UnityEngine.Object)null)
            {
                return "<null>";
            }
            return !string.IsNullOrEmpty(def.name) ? def.name : def.ToString();
        }

        /// <summary>1-based level when known, else the raw slot index marker for the log line.</summary>
        private static string LevelLabel(PerkSwapContext ctx)
        {
            return ctx.Level0 >= 0 ? (ctx.Level0 + 1).ToString() : "?(slot " + ctx.Level0 + ")";
        }
    }
}
