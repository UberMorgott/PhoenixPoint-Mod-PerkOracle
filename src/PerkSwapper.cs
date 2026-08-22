using System;
using System.Collections.Generic;
using System.Linq;
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

                // Decision gate (learned? same? already-owned? drills?). Same evaluator the wiki grid uses
                // to decide which cells render clickable, so a bright cell and an accepted click can never
                // disagree.
                PerkSwapVerdict verdict = GridVerdicts.Build(ctx).Evaluate(chosenDef);
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
                    else if (verdict == PerkSwapVerdict.DenyDrillPriceUnresolved)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap skipped: TFTV is present but its drill swap price"
                                  + " could not be read; refusing to charge an invented price for "
                                  + DefName(chosenDef) + ".");
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
                int swapCost = ResolveSwapCost(ctx, chosenDef);
                if (swapCost > 0)
                {
                    int available = GetAvailableSkillPoints(progression, ctx.Module);
                    if (available < swapCost)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap aborted: insufficient skill points ("
                                  + available + " < " + swapCost + ").");
                        return false;
                    }
                    // The charge is a THREE-store transaction (see ChargeSwapCost). If any store is
                    // unreachable the charge cannot be applied coherently and the player would get a
                    // partial charge or a free swap, so deny BEFORE touching the soldier.
                    if (!CanChargeSwapCost(ctx.Module))
                    {
                        OracleLog.Debug("[Oracle] PerkSwap aborted: skill points cannot be charged"
                                  + " coherently (SP shadow field unresolved); refusing a free swap.");
                        return false;
                    }
                }

                // Resolve the stat-recompute target BEFORE mutating. The swap removes from _abilities
                // directly, which fires no OnAbilityRemoved, so this recompute is part of the CORE
                // mutation, not cosmetic: without it a swapped-out passive keeps its bonus applied. If it
                // cannot even be resolved, abort now rather than commit a swap we cannot finish.
                if (UpdateStatsMethod == null)
                {
                    OracleLog.Debug("[Oracle] PerkSwap aborted: GeoCharacter.UpdateStats(bool) unresolved;"
                              + " a swap could not recompute stats (nothing mutated).");
                    return false;
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

                // Snapshot the list state we are about to change, rather than inferring it from whether a
                // call RETURNED. CharacterProgression.AddAbility does `_abilities.Add(ability);
                // OnAbilityAdded?.Invoke(ability);` (CharacterProgression.cs:158-159), so a throwing
                // subscriber leaves the ability ADDED on a call that never returned — a "did it mutate?"
                // flag would then skip the rollback and the soldier would silently keep an extra learned
                // ability. Counting occurrences also survives pre-existing duplicates: rollback restores
                // the original count instead of blindly removing every copy.
                int chosenCountBefore = CountOf(abilities, chosenDef);
                bool slotChanged = false; // slot.Ability re-pointed

                void Rollback()
                {
                    try
                    {
                        // Trim only what WE added, down to the pre-swap occurrence count.
                        while (CountOf(abilities, chosenDef) > chosenCountBefore && abilities.Remove(chosenDef))
                        {
                        }
                        if (slotChanged)
                        {
                            slot.Ability = oldDef;
                        }
                        if (!abilities.Contains(oldDef))
                        {
                            abilities.Add(oldDef);
                        }
                        // The restored ability set must drive the stats again, or the soldier keeps the
                        // half-swapped bonuses of a swap that was rolled back.
                        UpdateStatsMethod.Invoke(ctx.Character, new object[] { false });
                    }
                    catch (Exception rollbackEx)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap rollback failed: " + rollbackEx.Message);
                    }
                }

                try
                {
                    // step 3: point the slot at the chosen def.
                    slot.Ability = chosenDef;
                    slotChanged = true;

                    // step 4: learn the new def (public; adds to _abilities + fires OnAbilityAdded).
                    progression.AddAbility(chosenDef);

                    // step 4b: force a full stat recompute. The raw _abilities.Remove fires no recompute, and
                    // AddAbility only recomputes when the NEW def is a passive/proficiency (GeoCharacter
                    // OnAbilityAdded). Swapping a passive/proficiency OUT for a non-passive would otherwise
                    // leave the old perk's bonus applied. GeoCharacter.UpdateStats(bool) (GeoCharacter.cs:1185)
                    // rebuilds stats from Abilities, fixing both the removed-passive and added-passive sides.
                    // NOT guarded: it is part of the core mutation, so a failure must roll the swap back
                    // rather than leave a committed swap with stale stats and still charge for it.
                    UpdateStatsMethod.Invoke(ctx.Character, new object[] { false });
                }
                catch (Exception ex)
                {
                    // A core mutation (slot re-point / AddAbility / stat recompute) threw: roll back so the
                    // soldier is never left with a slot whose def is not in _abilities, or with stats that
                    // do not match the ability set.
                    Rollback();
                    OracleLog.Debug("[Oracle] PerkSwap failed mid-sequence, rolled back: " + ex.Message);
                    return false;
                }

                // Swap data committed: spend the skill points as ONE verified transaction. A charge that
                // cannot be applied coherently rolls the whole swap back — a free or half-paid swap is
                // worse than no swap. Affordability and reachability were both verified above.
                if (!ChargeSwapCost(progression, ctx.Module, swapCost))
                {
                    Rollback();
                    OracleLog.Debug("[Oracle] PerkSwap rolled back: skill-point charge could not be applied.");
                    return false;
                }

                // step 5: repaint the progression grid via reflection (private SetAbilityTracks). Cosmetic
                // and after the commit point: the grid refreshes on the next natural redraw anyway.
                try
                {
                    Traverse.Create(ctx.Module).Method("SetAbilityTracks").GetValue();
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] PerkSwap repaint (SetAbilityTracks) failed: " + ex.Message);
                }

                // TFTV parity: taking a DRILL over an already-learned ability drains the soldier's stamina
                // when TFTV's "stamina penalty from injury" campaign option is on
                // (DrillsUI.Helpers.cs:344-348). Drill takes only, TFTV-present only, option read live.
                ApplyDrillStaminaPenalty(ctx, chosenDef);

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
        /// Price of ONE swap in skill points, from the live inputs: a TFTV drill follows TFTV's own rule
        /// (flat <c>DrillsUI.SwapSpCost</c> when replacing a learned ability, else the slot's normal
        /// purchase price), anything else keeps the mod's configured cost. The rule itself is the pure
        /// <see cref="PerkSwapDecision.SwapCost"/>. ONE function so the confirm modal, the hover tooltip
        /// and the actual charge can never show three different numbers. Never throws.
        /// </summary>
        public static int ResolveSwapCost(PerkSwapContext ctx, TacticalAbilityDef chosenDef)
        {
            bool costEnabled = OracleMain.PerkSwapCostsResources;
            int configured = OracleMain.PerkSwapSkillPointCost;
            try
            {
                if (!costEnabled || ctx == null || (UnityEngine.Object)(object)chosenDef == (UnityEngine.Object)null
                    || !TftvDrillsBridge.IsDrill(chosenDef))
                {
                    return PerkSwapDecision.SwapCost(false, false, 0, 0, costEnabled, configured);
                }

                CharacterProgression progression = ctx.Character?.Progression;
                AbilityTrackSlot slot = ctx.Slot;
                TacticalAbilityDef current = slot?.Ability;
                bool learned = progression != null && current != null && progression.Abilities.Contains(current);
                // GetAbilitySlotCost dereferences slot.Ability.CharacterProgressionData (CharacterProgression
                // .cs:296) — only ask when it can answer, and only when it is the branch that applies.
                int slotCost = 0;
                if (!learned && progression != null
                    && (UnityEngine.Object)(object)current?.CharacterProgressionData != (UnityEngine.Object)null)
                {
                    slotCost = progression.GetAbilitySlotCost(slot);
                }
                return PerkSwapDecision.SwapCost(true, learned, TftvDrillsBridge.DrillSwapSpCost, slotCost,
                    costEnabled, configured);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap cost resolve failed, using configured cost: " + ex.Message);
                return PerkSwapDecision.EffectiveCost(costEnabled, configured);
            }
        }

        /// <summary>
        /// TFTV parity for a committed DRILL take: zero the soldier's stamina when TFTV is present and its
        /// <c>StaminaPenaltyFromInjurySetting</c> campaign option is on (DrillsUI.Helpers.cs:344-348).
        /// Nothing happens for a vanilla perk, without TFTV, or with the option off. Guarded — a cosmetic
        /// penalty must never fail a committed swap.
        /// </summary>
        private static void ApplyDrillStaminaPenalty(PerkSwapContext ctx, TacticalAbilityDef chosenDef)
        {
            try
            {
                if (!TftvDrillsBridge.IsDrill(chosenDef) || !TftvDrillsBridge.StaminaPenaltyEnabled)
                {
                    return;
                }
                TftvDrillsBridge.SetStaminaToZero(ctx.Character);
                if ((UnityEngine.Object)(object)ctx.Module != (UnityEngine.Object)null)
                {
                    ctx.Module.SetStatusesPanel();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap drill stamina penalty failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Verdicts for a WHOLE wiki grid against one slot. Everything that does not depend on the clicked
        /// candidate — the owned set, the scheduled-perk scan, the live drill list and TFTV's two
        /// slot-side safety answers — is gathered ONCE here instead of per cell; <see cref="Evaluate"/>
        /// then adds only the per-candidate facts. Used by the grid (to decide which cells render
        /// clickable) and by <see cref="TrySwap"/> itself, so the look and the gate cannot drift apart.
        /// </summary>
        internal sealed class GridVerdicts
        {
            private readonly PerkSwapContext _ctx;
            private readonly TacticalAbilityDef _currentDef;
            private readonly IReadOnlyList<TacticalAbilityDef> _owned;
            private readonly List<TacticalAbilityDef> _scheduled;
            private readonly List<TacticalAbilityDef> _drills;    // null => TFTV absent, drill gates skipped
            private readonly GeoPhoenixFaction _faction;
            private readonly bool _drillContextFailed;            // build threw => fail closed
            private readonly bool? _currentIsAcquiredDrill;
            private readonly bool? _removalBreaksAcquiredDrill;

            private GridVerdicts(PerkSwapContext ctx)
            {
                _ctx = ctx;
                _currentDef = ctx?.Slot?.Ability;
                _owned = ctx?.Character?.Progression?.Abilities;
                _scheduled = ScheduledPerks(ctx?.Character?.Progression, ctx?.Slot);

                // Outside the try on purpose: TFTV-ABSENT must stay a plain "no drill context" (vanilla
                // behaviour), never the fail-closed deny the catch below produces. FAULTED is the exact
                // opposite: TFTV's drills exist but its contract did not resolve, so nothing can be
                // verified and every swap must be denied. Reading a fault as an absence is the fail-open
                // this gate exists to prevent.
                TftvDrillsState state = TftvDrillsBridge.State;
                if (state == TftvDrillsState.Absent)
                {
                    return;
                }
                if (state == TftvDrillsState.Faulted)
                {
                    _drillContextFailed = true;
                    return;
                }
                try
                {
                    // Tri-state read: null is "could not read", NOT "no drills". An unreadable list makes
                    // the acquired-drill snapshot unknown, so deny instead of trusting an empty snapshot.
                    _drills = TftvDrillsBridge.AllDrillsOrNull();
                    if (_drills == null)
                    {
                        _drillContextFailed = true;
                        return;
                    }
                    _faction = ctx?.Character?.Faction?.GeoLevel?.PhoenixFaction;

                    // The SAFETY answers are nullable: null means the bridge could not obtain them
                    // (member unresolved / TFTV threw). Fail CLOSED — an unknown safety answer counts
                    // exactly like "yes, this swap would break an acquired drill", so one exception
                    // inside TFTV can never let a dangerous swap through.
                    _currentIsAcquiredDrill = _drills.Contains(_currentDef)
                        ? TftvDrillsBridge.CharacterHasDrill(ctx.Character, _currentDef)
                        : false;
                    _removalBreaksAcquiredDrill = TftvDrillsBridge.WouldBreakWeaponProficiencyRequirement(
                        ctx.Character, _currentDef, out List<string> blockingDrills);
                    if (_currentIsAcquiredDrill == null || _removalBreaksAcquiredDrill == null)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap blocked: TFTV drill safety check unavailable for "
                                  + DefName(_currentDef) + "; denying rather than risking an acquired drill.");
                    }
                    else if (_removalBreaksAcquiredDrill == true)
                    {
                        OracleLog.Debug("[Oracle] PerkSwap blocked: removing " + DefName(_currentDef)
                                  + " would invalidate acquired drill(s): "
                                  + string.Join(", ", blockingDrills.ToArray()));
                    }
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] PerkSwap drill context build failed, denying: " + ex.Message);
                    _drillContextFailed = true;
                }
            }

            /// <summary>Gather the slot-side facts once. Never returns null and never throws.</summary>
            public static GridVerdicts Build(PerkSwapContext ctx)
            {
                return new GridVerdicts(ctx);
            }

            /// <summary>Verdict for one candidate. <see cref="PerkSwapVerdict.DenyInvalidInput"/> on bad inputs.</summary>
            public PerkSwapVerdict Evaluate(TacticalAbilityDef chosen)
            {
                if (_ctx == null || !_ctx.IsUsable || _owned == null)
                {
                    return PerkSwapVerdict.DenyInvalidInput;
                }
                return PerkSwapDecision.Evaluate(chosen, _currentDef, _owned, null, _scheduled,
                    BuildDrillContext(chosen));
            }

            /// <summary>Per-candidate drill facts on top of the cached slot-side ones; null without TFTV.</summary>
            private DrillSwapContext BuildDrillContext(TacticalAbilityDef chosen)
            {
                if (_drillContextFailed)
                {
                    return DrillSwapContext.Denied();
                }
                if (_drills == null)
                {
                    return null;
                }
                try
                {
                    bool chosenIsDrill = _drills.Contains(chosen);
                    // TFTV's target-drill direction of the proficiency guard (DrillsUnlock.cs:202): only
                    // meaningful when the CHOSEN perk is a drill; same fail-closed nullable convention.
                    bool? targetLoses = false;
                    if (chosenIsDrill)
                    {
                        targetLoses = TftvDrillsBridge.TargetDrillLosesWeaponProficiencyRequirement(
                            _ctx.Character, chosen, _currentDef, out string targetBlocker);
                        if (targetLoses != false)
                        {
                            OracleLog.Debug("[Oracle] PerkSwap blocked: removing " + DefName(_currentDef)
                                      + " would strip a proficiency " + DefName(chosen) + " needs"
                                      + (targetBlocker != null ? " (" + targetBlocker + ")" : " (check unavailable)")
                                      + ".");
                        }
                    }
                    return new DrillSwapContext
                    {
                        ChosenIsDrill = chosenIsDrill,
                        ChosenDrillUnlocked = chosenIsDrill
                            && TftvDrillsBridge.IsDrillUnlocked(_faction, _ctx.Character, chosen),
                        CurrentIsAcquiredDrill = _currentIsAcquiredDrill ?? true,
                        RemovalBreaksAcquiredDrill = DrillSwapContext.SafetyDenies(
                            _currentIsAcquiredDrill, _removalBreaksAcquiredDrill, targetLoses),
                        IgnoreDrillRequirements = OracleMain.IgnoreDrillRequirements,
                        AllowDrillReSwap = OracleMain.AllowDrillReSwap,
                        IsRevertToOriginal = IsRevertToOriginal(_ctx, chosen),
                        // TFTV is present but its own swap price could not be read: refuse the drill swap
                        // rather than charge the compiled-in guess. Only while the cost toggle is on — a
                        // free swap needs no price and stays available.
                        DrillPriceUnresolved = OracleMain.PerkSwapCostsResources
                                               && !TftvDrillsBridge.DrillSwapSpCostResolved,
                    };
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] PerkSwap drill candidate check failed, denying: " + ex.Message);
                    return DrillSwapContext.Denied();
                }
            }
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
            if (progression == null)
            {
                return null;
            }
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
        /// <c>GeoCharacter.UpdateStats(bool)</c> (private, GeoCharacter.cs:1185), resolved ONCE at type
        /// load so a swap can verify its stat-recompute target before mutating anything. Null (a game
        /// update renamed it) aborts the swap up front instead of committing one that cannot recompute.
        /// </summary>
        private static readonly MethodInfo UpdateStatsMethod =
            AccessTools.Method(typeof(GeoCharacter), "UpdateStats", new[] { typeof(bool) });

        /// <summary>Occurrences of <paramref name="def"/> in <paramref name="list"/> (reference identity).</summary>
        private static int CountOf(List<TacticalAbilityDef> list, TacticalAbilityDef def)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], def))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// True when a non-zero SP charge can be applied to EVERY store it must touch. With the module
        /// open that means both private shadows as well as the persisted field: writing only some of them
        /// desyncs the display from the save and lets a later native CommitStatChanges refund the swap.
        /// Checked BEFORE any soldier mutation so an uncharageable swap is denied, never given away free.
        /// </summary>
        private static bool CanChargeSwapCost(UIModuleCharacterProgression module)
        {
            if ((UnityEngine.Object)(object)module == (UnityEngine.Object)null)
            {
                return true; // module closed: the persisted field is the only store.
            }
            return CurrentSkillPointsField != null && StartingSkillPointsField != null;
        }

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
        private static bool ChargeSwapCost(CharacterProgression progression, UIModuleCharacterProgression module, int cost)
        {
            if (cost <= 0)
            {
                return true;
            }
            bool moduleOpen = (UnityEngine.Object)(object)module != (UnityEngine.Object)null;

            // Snapshot every store first, so any failure can put ALL of them back. A partially applied
            // charge is the desync that lets a later native commit refund (or double-charge) the swap.
            int persistedBefore;
            int currentBefore = 0;
            int startingBefore = 0;
            try
            {
                persistedBefore = progression.SkillPoints;
                if (moduleOpen)
                {
                    currentBefore = (int)CurrentSkillPointsField.GetValue(module);
                    startingBefore = (int)StartingSkillPointsField.GetValue(module);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap SP snapshot failed, not charging: " + ex.Message);
                return false;
            }

            try
            {
                if (moduleOpen)
                {
                    CurrentSkillPointsField.SetValue(module, currentBefore - cost);
                    StartingSkillPointsField.SetValue(module, startingBefore - cost);
                }
                progression.SkillPoints = persistedBefore - cost;

                // Verify: a silently-swallowed or clamped write would otherwise pass as a charge.
                if (progression.SkillPoints != persistedBefore - cost
                    || (moduleOpen && ((int)CurrentSkillPointsField.GetValue(module) != currentBefore - cost
                                       || (int)StartingSkillPointsField.GetValue(module) != startingBefore - cost)))
                {
                    throw new Exception("post-charge verification mismatch");
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap SP charge failed, restoring: " + ex.Message);
                RestoreSkillPoints(progression, module, moduleOpen, persistedBefore, currentBefore, startingBefore);
                return false;
            }

            if (moduleOpen)
            {
                try
                {
                    module.RefreshStatPanel();
                }
                catch (Exception ex)
                {
                    // Cosmetic only, and the charge is already verified — never undo a good charge for it.
                    OracleLog.Debug("[Oracle] PerkSwap SP repaint failed: " + ex.Message);
                }
            }
            return true;
        }

        /// <summary>Put all three skill-point stores back to their snapshot; best-effort, never throws.</summary>
        private static void RestoreSkillPoints(CharacterProgression progression, UIModuleCharacterProgression module,
            bool moduleOpen, int persisted, int current, int starting)
        {
            try
            {
                progression.SkillPoints = persisted;
                if (moduleOpen)
                {
                    CurrentSkillPointsField.SetValue(module, current);
                    StartingSkillPointsField.SetValue(module, starting);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwap SP restore failed: " + ex.Message);
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
