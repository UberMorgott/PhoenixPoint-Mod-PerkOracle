using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.Oracle
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

                // Decision gate (learned? same? already-owned?). Owned set = progression.Abilities.
                IReadOnlyList<TacticalAbilityDef> owned = progression.Abilities;
                PerkSwapVerdict verdict = PerkSwapDecision.Evaluate(chosenDef, oldDef, owned);
                if (verdict != PerkSwapVerdict.Allow)
                {
                    if (verdict == PerkSwapVerdict.DenyAlreadyOwned)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap skipped: "
                                  + DefName(chosenDef) + " already owned by soldier.");
                    }
                    // Not-learned / same-as-current / invalid: silent no-op per design.
                    return false;
                }

                // Skill-point cost: charge only a swap that will actually happen (verdict == Allow). Abort
                // BEFORE mutating _abilities if the soldier can't afford it, so nothing is half-done. The
                // affordability read is shadow-aware (see GetAvailableSkillPoints): the open progression
                // module displays its private _currentSkillPoints copy, which already reflects pending
                // un-committed stat purchases — that is the number the user sees. The click site pre-checks
                // this too (to show a message); this guard also protects any direct caller.
                int swapCost = PerkSwapDecision.EffectiveCost(
                    OracleMain.PerkSwapCostsResources, OracleMain.PerkSwapSkillPointCost);
                if (swapCost > 0)
                {
                    int available = GetAvailableSkillPoints(progression, ctx.Module);
                    if (available < swapCost)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap aborted: insufficient skill points ("
                                  + available + " < " + swapCost + ").");
                        return false;
                    }
                }

                // step 2: un-learn old via reflection (no public remover on CharacterProgression).
                List<TacticalAbilityDef> abilities =
                    Traverse.Create(progression).Field("_abilities").GetValue<List<TacticalAbilityDef>>();
                if (abilities == null)
                {
                    OracleLog.Debug("[Oracle] PerkSwap aborted: could not reflect _abilities.");
                    return false;
                }
                // If the old def is not actually in _abilities the swap would leave the slot pointing at a
                // def the soldier doesn't own. Abort BEFORE mutating slot.Ability so nothing is half-done.
                if (!abilities.Remove(oldDef))
                {
                    OracleLog.Debug("[Oracle] PerkSwap aborted: old def "
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
                        OracleLog.Debug("[Oracle] PerkSwap stat recompute (UpdateStats) failed: " + ex.Message);
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
                        OracleLog.Debug("[Oracle] PerkSwap repaint (SetAbilityTracks) failed: " + ex.Message);
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
                        OracleLog.Debug("[Oracle] PerkSwap rollback failed: " + rollbackEx.Message);
                    }
                    OracleLog.Debug("[Oracle] PerkSwap failed mid-sequence, rolled back: " + ex.Message);
                    return false;
                }

                // Swap fully committed: spend the skill points (affordability was verified above) and
                // repaint the SP counter. Done last so a mid-swap failure/rollback never charges the player.
                ChargeSwapCost(progression, ctx.Module, swapCost);

                OracleLog.Debug("[Oracle] PerkSwap: " + DefName(oldDef) + " -> " + DefName(chosenDef)
                          + " @ level " + LevelLabel(ctx));
                return true;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwapper.TrySwap failed: " + ex.Message);
                return false;
            }
        }

        // The open progression module does NOT read Progression.SkillPoints live: it snapshots it into
        // private shadows (UIModuleCharacterProgression.cs — _currentSkillPoints:229 drives the displayed
        // counter:622, _startingSkillPoints:217 is the cancel/reset baseline:520) and a later native
        // CommitStatChanges() writes the shadow back ABSOLUTELY (SkillPoints = _currentSkillPoints, :375),
        // which would erase a bare Progression.SkillPoints decrement. The native spend pair
        // (ConsumeAbilityCost:428 + CommitStatChanges:367, as used by BuyAbility:403-405) cannot be reused
        // verbatim: ConsumeAbilityCost spills overflow into the FACTION SP pool (:436-441; our cost is
        // soldier-only) and CommitStatChanges also commits any pending un-confirmed stat edits (:369-374)
        // the player may have open. So we mirror the spend across all three stores instead (see
        // ChargeSwapCost). AccessTools.Field is cached once; null (game update renamed the field) degrades
        // to persisted-only behavior, logged.
        private static readonly FieldInfo CurrentSkillPointsField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_currentSkillPoints");
        private static readonly FieldInfo StartingSkillPointsField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_startingSkillPoints");

        /// <summary>
        /// The SP figure the USER currently sees: the open module's private _currentSkillPoints shadow
        /// (which already reflects pending, un-committed stat purchases), falling back to the persisted
        /// <c>Progression.SkillPoints</c> when the module/shadow is unavailable. Used by both the click-site
        /// pre-check (deny message) and the TrySwap guard so they can never disagree with the display.
        /// </summary>
        public static int GetAvailableSkillPoints(CharacterProgression progression, UIModuleCharacterProgression module)
        {
            try
            {
                if (CurrentSkillPointsField != null
                    && (UnityEngine.Object)(object)module != (UnityEngine.Object)null)
                {
                    return (int)CurrentSkillPointsField.GetValue(module);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] GetAvailableSkillPoints shadow read failed: " + ex.Message);
            }
            return progression != null ? progression.SkillPoints : 0;
        }

        /// <summary>
        /// Spend the swap's skill-point cost, keeping the persisted field and the module's shadow copies
        /// consistent (see the shadow-desync note above): decrement <c>Progression.SkillPoints</c> (the save
        /// entity), the module's <c>_currentSkillPoints</c> (displayed; a later native CommitStatChanges then
        /// writes the already-charged value back — no double charge, commit is an absolute write) and
        /// <c>_startingSkillPoints</c> (so a native cancel/reset of pending stat edits cannot restore the
        /// pre-swap SP). Then repaint via the public RefreshStatPanel. Called only after a fully-committed
        /// swap with affordability verified. Guarded: a hiccup is logged, never thrown.
        /// </summary>
        private static void ChargeSwapCost(CharacterProgression progression, UIModuleCharacterProgression module, int cost)
        {
            if (cost <= 0)
            {
                return;
            }
            try
            {
                progression.SkillPoints -= cost;
                if ((UnityEngine.Object)(object)module != (UnityEngine.Object)null)
                {
                    if (CurrentSkillPointsField != null && StartingSkillPointsField != null)
                    {
                        CurrentSkillPointsField.SetValue(module, (int)CurrentSkillPointsField.GetValue(module) - cost);
                        StartingSkillPointsField.SetValue(module, (int)StartingSkillPointsField.GetValue(module) - cost);
                    }
                    else
                    {
                        OracleLog.Debug("[Oracle] PerkSwap SP shadow fields missing; charged persisted only.");
                    }
                    module.RefreshStatPanel();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap SP charge/refresh failed: " + ex.Message);
            }
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
