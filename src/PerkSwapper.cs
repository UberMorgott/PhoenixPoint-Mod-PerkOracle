using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels.Factions;
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

        /// <summary>
        /// The ability this slot held before PerkOracle first changed it (from
        /// <see cref="OriginalPerkStore"/>), or null when nothing is stored / the stored def no longer
        /// resolves. Purely informational here: the wiki uses it to mark the "default" cell; the swap
        /// itself treats that cell like any other candidate.
        /// </summary>
        public readonly TacticalAbilityDef OriginalDef;

        public PerkSwapContext(GeoCharacter character, AbilityTrackSlot slot,
            UIModuleCharacterProgression module, int level0, TacticalAbilityDef originalDef = null)
        {
            Character = character;
            Slot = slot;
            Module = module;
            Level0 = level0;
            OriginalDef = originalDef;
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
                PerkSwapVerdict verdict = PerkSwapDecision.Evaluate(
                    chosenDef, oldDef, owned, null, ScheduledPerks(progression, slot),
                    BuildDrillContext(ctx.Character, oldDef, chosenDef, IsRevertToOriginal(ctx, chosenDef)));
                if (verdict != PerkSwapVerdict.Allow)
                {
                    if (verdict == PerkSwapVerdict.DenyAlreadyOwned)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap skipped: "
                                  + DefName(chosenDef) + " already owned by soldier.");
                    }
                    else if (verdict == PerkSwapVerdict.DenyDrillReSwapBlocked)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap skipped: " + DefName(oldDef)
                                  + " is an acquired drill (enable \"Allow Re-Swapping Drills\" to move it).");
                    }
                    else if (verdict == PerkSwapVerdict.DenyDrillLocked)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap skipped: drill " + DefName(chosenDef)
                                  + " is locked for this soldier.");
                    }
                    // DenyDrillBreaksAcquired already logged the blocking drills in BuildDrillContext.
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

                // Re-read the slot immediately before mutating: TFTV's own drill apply writes
                // slot.Ability directly (DrillsUI.Helpers.cs:294) with no locking, so a drill taken
                // while this wiki was open would otherwise be silently clobbered. Last writer wins —
                // make sure it is not us.
                if (!ReferenceEquals(slot.Ability, oldDef))
                {
                    OracleLog.Debug("[Oracle] PerkSwap aborted: slot changed under us ("
                              + DefName(oldDef) + " -> " + DefName(slot.Ability) + ").");
                    return false;
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

                // Remember what this slot originally held so the player can put it back. First write wins
                // (a second swap still reverts to the true default) and the entry clears itself once the
                // slot is restored — see OriginalPerkStore. Vanilla-safe: no TFTV involved.
                RecordOriginal(ctx, oldDef, chosenDef);

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

        /// <summary>
        /// Persist "this slot originally held <paramref name="oldDef"/>" for the soldier's own serialized
        /// <c>GeoCharacter.Id</c> + slot level. Guarded: the store is a convenience, never a reason for a
        /// committed swap to report failure.
        /// </summary>
        private static void RecordOriginal(PerkSwapContext ctx, TacticalAbilityDef oldDef, TacticalAbilityDef chosenDef)
        {
            try
            {
                if (ctx.Level0 < 0)
                {
                    // The store keys on the slot level, so a slot whose level could not be resolved can
                    // never be recorded — and would silently become non-revertable. Say so.
                    OracleLog.Debug("[Oracle] PerkSwap: slot level unknown (Level0=" + ctx.Level0
                              + "); this swap is NOT recorded and the slot will not be revertable.");
                    return;
                }
                OriginalPerkStore.RecordSwap((int)ctx.Character.Id, ctx.Level0, TrackKey(ctx.Slot),
                    DefName(oldDef), DefName(chosenDef));
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap original-perk record failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Track dimension of the store key: without it two slots at the same level in DIFFERENT tracks
        /// (primary class / secondary class / personal) collide on one entry. Taken live from the track's
        /// own <c>Source</c> enum, so nothing is hardcoded. Empty when the track cannot be resolved — the
        /// key then degrades to the pre-track shape rather than dropping the entry.
        /// </summary>
        public static string TrackKey(AbilityTrackSlot slot)
        {
            try
            {
                AbilityTrack track = slot?.AbilityTrack;
                return track != null ? track.Source.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap track-key resolve failed: " + ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Collect the TFTV drill facts for one swap, or null when TFTV is absent (then
        /// <see cref="PerkSwapDecision.Evaluate{T}"/> skips its drill step entirely and the gate is
        /// exactly the pre-drills one). Everything comes from <see cref="TftvDrillsBridge"/>, which is
        /// itself no-op/false without TFTV; guarded so a bridge hiccup degrades to "no drill context"
        /// rather than blocking a vanilla swap.
        /// </summary>
        private static DrillSwapContext BuildDrillContext(GeoCharacter character, TacticalAbilityDef oldDef,
            TacticalAbilityDef chosenDef, bool isRevertToOriginal)
        {
            // Outside the try on purpose: TFTV-absent must stay a plain "no drill context" (vanilla
            // behaviour), never the fail-closed deny the catch below returns.
            if (!TftvDrillsBridge.DrillsAvailable)
            {
                return null;
            }
            try
            {
                List<TacticalAbilityDef> drills = TftvDrillsBridge.AllDrills;
                bool chosenIsDrill = drills.Contains(chosenDef);
                bool currentIsDrill = drills.Contains(oldDef);
                GeoPhoenixFaction faction = character?.Faction?.GeoLevel?.PhoenixFaction;

                // The two SAFETY answers are nullable: null means the bridge could not obtain them
                // (member unresolved / TFTV threw). Fail CLOSED — an unknown safety answer is treated
                // exactly like "yes, this swap would break an acquired drill", so one exception inside
                // TFTV can never let a dangerous swap through.
                bool? currentIsAcquired = currentIsDrill
                    ? TftvDrillsBridge.CharacterHasDrill(character, oldDef)
                    : false;
                bool? breaksAcquired = TftvDrillsBridge.WouldBreakWeaponProficiencyRequirement(
                    character, oldDef, out List<string> blockingDrills);
                bool unknown = currentIsAcquired == null || breaksAcquired == null;
                bool denies = DrillSwapContext.SafetyDenies(currentIsAcquired, breaksAcquired);

                var info = new DrillSwapContext
                {
                    ChosenIsDrill = chosenIsDrill,
                    ChosenDrillUnlocked = chosenIsDrill
                        && TftvDrillsBridge.IsDrillUnlocked(faction, character, chosenDef),
                    CurrentIsAcquiredDrill = currentIsAcquired ?? true,
                    RemovalBreaksAcquiredDrill = denies,
                    IgnoreDrillRequirements = OracleMain.IgnoreDrillRequirements,
                    AllowDrillReSwap = OracleMain.AllowDrillReSwap,
                    IsRevertToOriginal = isRevertToOriginal,
                };
                if (unknown)
                {
                    OracleLog.Debug("[Oracle] PerkSwap blocked: TFTV drill safety check unavailable for "
                              + DefName(oldDef) + "; denying rather than risking an acquired drill.");
                }
                else if (info.RemovalBreaksAcquiredDrill)
                {
                    OracleLog.Debug("[Oracle] PerkSwap blocked: removing " + DefName(oldDef)
                              + " would invalidate acquired drill(s): "
                              + string.Join(", ", blockingDrills.ToArray()));
                }
                return info;
            }
            catch (Exception ex)
            {
                // Drills ARE loaded (checked above) but the context could not be built: fail closed.
                OracleLog.Debug("[Oracle] PerkSwap drill context build failed, denying: " + ex.Message);
                return new DrillSwapContext { RemovalBreaksAcquiredDrill = true };
            }
        }

        /// <summary>
        /// True when the click puts the slot back to the ability it held before PerkOracle first changed
        /// it (<see cref="PerkSwapContext.OriginalDef"/>). Restoring the default must always be possible,
        /// so this lifts the acquired-drill re-swap gate — see <see cref="DrillSwapContext.IsRevertToOriginal"/>.
        /// </summary>
        private static bool IsRevertToOriginal(PerkSwapContext ctx, TacticalAbilityDef chosenDef)
        {
            return (UnityEngine.Object)(object)ctx.OriginalDef != (UnityEngine.Object)null
                   && ReferenceEquals(ctx.OriginalDef, chosenDef);
        }

        /// <summary>
        /// Every perk baked into the soldier's tracks (all slots, including future, not-yet-unlocked
        /// ones), EXCEPT the slot being swapped so re-swapping the same slot stays allowed. Feeds the
        /// duplicate gate: without it a perk scheduled at a later level can be swapped into an early
        /// slot and the soldier gets it twice / levels early (issue #1). Guarded: on any failure returns
        /// null, i.e. the previous behaviour.
        /// </summary>
        private static List<TacticalAbilityDef> ScheduledPerks(CharacterProgression progression, AbilityTrackSlot skip)
        {
            try
            {
                var scheduled = new List<TacticalAbilityDef>();
                foreach (AbilityTrack track in progression.AbilityTracks)
                {
                    if (track == null || track.AbilitiesByLevel == null)
                    {
                        continue;
                    }
                    foreach (AbilityTrackSlot s in track.AbilitiesByLevel)
                    {
                        if (s == null || ReferenceEquals(s, skip) || s.Ability == null)
                        {
                            continue;
                        }
                        scheduled.Add(s.Ability);
                    }
                }
                return scheduled;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap scheduled-perk scan failed: " + ex.Message);
                return null;
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
        /// consistent (see the shadow-desync note above): decrement the module's <c>_currentSkillPoints</c>
        /// (displayed; a later native CommitStatChanges then writes the already-charged value back — no
        /// double charge, commit is an absolute write) and <c>_startingSkillPoints</c> (so a native
        /// cancel/reset of pending stat edits cannot restore the pre-swap SP), then the persisted
        /// <c>Progression.SkillPoints</c> (the save entity). Shadow writes go FIRST and are each
        /// independently guarded so one failing can never skip the other (a half-written pair is exactly
        /// the desync that lets a later native commit refund the swap); a failed/absent shadow degrades to
        /// the persisted-only charge (same as the module-closed path), logged. The public RefreshStatPanel
        /// repaint always runs. Called only after a fully-committed swap with affordability verified.
        /// Guarded: a hiccup is logged, never thrown.
        /// </summary>
        private static void ChargeSwapCost(CharacterProgression progression, UIModuleCharacterProgression module, int cost)
        {
            if (cost <= 0)
            {
                return;
            }
            bool moduleOpen = (UnityEngine.Object)(object)module != (UnityEngine.Object)null;
            if (moduleOpen)
            {
                AddToShadow(module, CurrentSkillPointsField, -cost);
                AddToShadow(module, StartingSkillPointsField, -cost);
            }
            try
            {
                progression.SkillPoints -= cost;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap SP persisted charge failed: " + ex.Message);
            }
            if (moduleOpen)
            {
                try
                {
                    module.RefreshStatPanel();
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] PerkSwap SP repaint failed: " + ex.Message);
                }
            }
        }

        /// <summary>Guarded in-place add on a private int shadow field; a miss/failure is logged, never thrown.</summary>
        private static void AddToShadow(UIModuleCharacterProgression module, FieldInfo field, int delta)
        {
            try
            {
                if (field == null)
                {
                    OracleLog.Debug("[Oracle] PerkSwap SP shadow field missing; charging persisted only.");
                    return;
                }
                field.SetValue(module, (int)field.GetValue(module) + delta);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap SP shadow write failed: " + ex.Message);
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
