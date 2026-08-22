using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// Prefix on UIModuleCharacterProgression.OnCancelInputHandler (the "back"/cancel seam, fired for
    /// both RMB and Esc on the soldier screen). Intercepts cancel to drive the perk wiki:
    ///   1. wiki open  -> close it and consume the cancel (screen stays open);
    ///   2. else, pointer over a swap-eligible Personal cell -> open the wiki for that cell and consume;
    ///   3. else        -> let the original run (normal back / vanilla popup close).
    /// Consuming = set __result = true and skip the original, mirroring the vanilla popup-close path.
    /// Everything is wrapped so a failure never blocks the normal back action.
    /// </summary>
    [HarmonyPatch(typeof(UIModuleCharacterProgression), "OnCancelInputHandler")]
    internal static class OnCancelInputHandlerPatch
    {
        // Private GeoCharacter field on the module = the soldier currently shown.
        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_character");

        private static bool Prefix(UIModuleCharacterProgression __instance, ref bool __result)
        {
            try
            {
                // 1) Already open -> close + consume, keep the screen.
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                    __result = true;
                    return false;
                }

                // Resolve the shown soldier once; used by the Mutoid gate and the swap context below.
                var character = CharacterField?.GetValue(__instance) as GeoCharacter;

                // Resolve the class name once up front: the eligibility gate needs it to resolve
                // the slot's rolled pool (the poolMember half), and the wiki pool below reuses it.
                string className = GetClassName(__instance);

                // RISK (verify in-game): the Cancel action maps to BOTH RMB and Esc, so we cannot
                // rely on GetMouseButtonDown(1) being true here (RMB may already be consumed by input
                // routing). We therefore key off "pointer over a swap-eligible cell" instead: if the user
                // is hovering one when cancel fires we treat it as an inspect request.
                // Consequence: Esc while hovering such a cell opens the wiki (Esc again closes it),
                // which the design accepts. Cancel anywhere else falls through to the normal back.
                AbilityTrackSkillEntryElement cell =
                    FindSwapEligibleCellUnderPointer(className, character, out int level0);
                if (cell == null)
                {
                    return true; // not over an eligible cell -> normal back
                }

                // The pool itself stays exactly what it always was (ResolveForSlot is memoized per
                // (level0, className) and also feeds RolledPoolMembership, so it must not be polluted).
                // Drills and the stored original are appended HERE, on a private copy, for this one wiki.
                TacticalAbilityDef originalDef = ResolveStoredOriginal(character, cell.TrackSlot, level0);
                List<TacticalAbilityDef> defs = AppendExtraCandidates(
                    PerkWikiPool.ResolveForSlot(level0, className), character, originalDef);
                if (defs == null || defs.Count == 0)
                {
                    OracleLog.Debug("[Oracle] perk wiki: empty pool for level0=" + level0
                              + " class=" + (className ?? "<null>"));
                    return true; // nothing to show -> let normal back proceed
                }

                Canvas canvas = ((Component)__instance).GetComponentInParent<Canvas>();
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null)
                {
                    return true;
                }

                // Build the swap context so wiki left-clicks can replace this slot's perk (gated by the
                // AllowPerkSwap config at click time). Null character/slot just disables swapping.
                var swapContext = new PerkSwapContext(character, cell.TrackSlot, __instance, level0, originalDef);

                PerkWikiPanel.Open(canvas, defs, swapContext);
                __result = true;
                return false; // consume
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] OnCancelInputHandler prefix failed: " + ex.Message);
                return true; // on any failure, never block the normal back action
            }
        }

        /// <summary>
        /// The candidate list the wiki actually shows: the slot's normal pool, plus (when TFTV is loaded)
        /// the drills this soldier may take, plus the slot's stored original ability. Returns the pool
        /// list UNCHANGED — same reference — when there is nothing to append, so the no-TFTV / never-swapped
        /// path is byte-identical to before. Never throws; on any failure the plain pool is used.
        ///
        /// Everything appended is read live: the drill list, its order and its requirements all come out of
        /// TFTV at call time, so drills TFTV adds, renames or removes appear/disappear with no code change.
        /// </summary>
        private static List<TacticalAbilityDef> AppendExtraCandidates(List<TacticalAbilityDef> pool,
            GeoCharacter character, TacticalAbilityDef originalDef)
        {
            try
            {
                List<TacticalAbilityDef> extra = null;

                if (TftvDrillsBridge.DrillsAvailable)
                {
                    // "Ignore drill requirements" means the player wants to BROWSE everything, so show the
                    // whole live drill list; otherwise show exactly what TFTV says this soldier can take.
                    List<TacticalAbilityDef> drills = OracleMain.IgnoreDrillRequirements
                        ? TftvDrillsBridge.AllDrills
                        : TftvDrillsBridge.GetAvailableDrills(
                            character?.Faction?.GeoLevel?.PhoenixFaction, character);
                    Append(ref extra, pool, drills);
                }

                if ((UnityEngine.Object)(object)originalDef != (UnityEngine.Object)null)
                {
                    Append(ref extra, pool, new[] { originalDef });
                }

                if (extra == null)
                {
                    return pool;
                }

                var merged = new List<TacticalAbilityDef>(pool);
                merged.AddRange(extra);
                return merged;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] perk wiki: extra-candidate append failed: " + ex.Message);
                return pool;
            }
        }

        /// <summary>Collect the members of <paramref name="add"/> not already in the pool or in <paramref name="extra"/>.</summary>
        private static void Append(ref List<TacticalAbilityDef> extra, List<TacticalAbilityDef> pool,
            IEnumerable<TacticalAbilityDef> add)
        {
            if (add == null)
            {
                return;
            }
            foreach (TacticalAbilityDef def in add)
            {
                if ((UnityEngine.Object)(object)def == (UnityEngine.Object)null
                    || pool.Contains(def)
                    || (extra != null && extra.Contains(def)))
                {
                    continue;
                }
                (extra ?? (extra = new List<TacticalAbilityDef>())).Add(def);
            }
        }

        /// <summary>
        /// The ability this slot held before it was first changed, resolved against the LIVE def
        /// repository. The wiki BASELINES the slot on the way in (<see cref="OriginalPerkStore.ObserveSlot"/>):
        /// a personal slot is a random roll with no def-level default anywhere (AbilityTrack.cs
        /// CreatePersonalAbilityTrack — the roll is only ever persisted in the save's own
        /// AbilitiesByLevel), so the first sighting is the ONLY thing that can serve as its default. That
        /// is also what makes a slot changed by TFTV's own DrillSwapUI revertable, not just one PerkOracle
        /// wrote itself. Null when the slot is already at its baseline or the stored def no longer exists
        /// (renamed/removed by a content update — the entry is left on disk untouched). Never throws.
        /// </summary>
        private static TacticalAbilityDef ResolveStoredOriginal(GeoCharacter character, AbilityTrackSlot slot,
            int level0)
        {
            try
            {
                if (character == null || slot == null
                    || (UnityEngine.Object)(object)slot.Ability == (UnityEngine.Object)null)
                {
                    return null;
                }
                string trackKey = PerkSwapper.TrackKey(slot);
                OriginalPerkStore.ObserveSlot((int)character.Id, level0, trackKey, slot.Ability.name);
                string originalName = OriginalPerkStore.GetOriginalDefName(
                    (int)character.Id, level0, trackKey, slot.Ability.name);
                return TftvConfigBridge.ResolveDefByName(originalName);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] perk wiki: stored-original lookup failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Raycast the UI at the current mouse position and return the first swap-eligible Personal
        /// AbilityTrackSkillEntryElement under the pointer (with its 0-based slot index in
        /// <paramref name="level0"/>), or null (level0 = -1). <paramref name="className"/> feeds the
        /// rolled-pool gate's poolMember half; <paramref name="character"/> the store-baseline half.
        /// </summary>
        private static AbilityTrackSkillEntryElement FindSwapEligibleCellUnderPointer(
            string className, GeoCharacter character, out int level0)
        {
            level0 = -1;
            EventSystem es = EventSystem.current;
            if ((UnityEngine.Object)(object)es == (UnityEngine.Object)null)
            {
                return null;
            }

            var data = new PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);

            foreach (RaycastResult hit in hits)
            {
                if ((UnityEngine.Object)(object)hit.gameObject == (UnityEngine.Object)null)
                {
                    continue;
                }
                var cell = hit.gameObject.GetComponentInParent<AbilityTrackSkillEntryElement>();
                if (cell != null && IsSwapEligible(cell, className, character, out level0))
                {
                    return cell;
                }
            }
            return null;
        }

        /// <summary>0-based Personal slot index for the cell, or -1 if not a valid rolled Personal cell.</summary>
        private static int GetLevel0(AbilityTrackSkillEntryElement cell)
        {
            AbilityTrackSlot slot = cell.TrackSlot;
            if (slot == null)
            {
                return -1;
            }
            AbilityTrack track = slot.AbilityTrack;
            if (track == null)
            {
                return -1;
            }
            int level = track.GetAbilityLevel(slot);
            return level <= 0 ? -1 : level - 1;
        }

        /// <summary>
        /// Whether the wiki may take this cell's right-click (see
        /// <see cref="PerkClassification.IsSwapEligible"/>). Outputs the resolved 0-based slot index so
        /// callers need not recompute it. <paramref name="className"/> feeds the rolled-pool gate's
        /// poolMember half; <paramref name="character"/> the store-baseline half.
        /// </summary>
        private static bool IsSwapEligible(AbilityTrackSkillEntryElement cell, string className,
            GeoCharacter character, out int level0)
        {
            level0 = -1;
            if (cell.TrackSource != AbilityTrackSource.Personal)
            {
                return false;
            }
            level0 = GetLevel0(cell);
            if (level0 < 0)
            {
                return false;
            }
            bool abilityPresent = (UnityEngine.Object)(object)cell.AbilityDef != (UnityEngine.Object)null;
            // A Personal cell is a rolled perk only if its ability is a member of the engine's random
            // rolled-perk pool — either tagged with PersonalProgressionTag (vanilla / TheTurned
            // arm-marker) OR present in this slot's resolved rolled pool (TFTV human perks, untagged).
            bool isPoolMember = RolledPoolMembership.IsRolledPoolMember(cell.AbilityDef, level0, className);
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal,
                level0,
                abilityPresent,
                TftvConfigBridge.Available,
                TftvConfigBridge.IsSlotRandom,
                abilityIsRolledPoolMember: isPoolMember);
            // A slot the wiki already touched (or one holding a TFTV drill) is no longer a pool member and
            // classifies Fixed — without these two extra signals it would fall through to the vanilla
            // cancel, closing the screen and leaving the slot permanently un-editable.
            return PerkClassification.IsSwapEligible(
                AbilityTrackSource.Personal,
                kind,
                HasStoreBaseline(character, cell.TrackSlot, level0),
                TftvDrillsBridge.IsDrill(cell.AbilityDef));
        }

        /// <summary>
        /// Read-only "PerkOracle already observed/changed this slot" probe. Never writes (so hovering a
        /// cell cannot baseline it) and never throws; any failure just reports false.
        /// </summary>
        private static bool HasStoreBaseline(GeoCharacter character, AbilityTrackSlot slot, int level0)
        {
            try
            {
                if (character == null || slot == null)
                {
                    return false;
                }
                return OriginalPerkStore.HasBaseline((int)character.Id, level0, PerkSwapper.TrackKey(slot));
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] perk wiki: store-baseline probe failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>Soldier class name (e.g. "Assault"), or null if it cannot be resolved cleanly.</summary>
        private static string GetClassName(UIModuleCharacterProgression module)
        {
            try
            {
                var character = CharacterField?.GetValue(module) as GeoCharacter;
                ClassTagDef tag = character?.ClassTag;
                return tag != null ? tag.className : null;
            }
            catch
            {
                // className only gates the optional class blacklist; null => show the full pool.
                return null;
            }
        }
    }
}
