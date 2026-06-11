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
    ///   2. else, pointer over a rolled Personal cell -> open the wiki for that cell and consume;
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

                // RISK (verify in-game): the Cancel action maps to BOTH RMB and Esc, so we cannot
                // rely on GetMouseButtonDown(1) being true here (RMB may already be consumed by input
                // routing). We therefore key off "pointer over a rolled cell" instead: if the user is
                // hovering a rolled Personal cell when cancel fires we treat it as an inspect request.
                // Consequence: Esc while hovering a rolled cell opens the wiki (Esc again closes it),
                // which the design accepts. Cancel anywhere else falls through to the normal back.
                AbilityTrackSkillEntryElement cell = FindRolledCellUnderPointer(out int level0);
                if (cell == null)
                {
                    return true; // not over a rolled cell -> normal back
                }

                string className = GetClassName(__instance);
                List<TacticalAbilityDef> defs = PerkWikiPool.ResolveForSlot(level0, className);
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
                var character = CharacterField?.GetValue(__instance) as GeoCharacter;
                var swapContext = new PerkSwapContext(character, cell.TrackSlot, __instance, level0);

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
        /// Raycast the UI at the current mouse position and return the first rolled Personal
        /// AbilityTrackSkillEntryElement under the pointer (with its 0-based slot index in
        /// <paramref name="level0"/>), or null (level0 = -1).
        /// </summary>
        private static AbilityTrackSkillEntryElement FindRolledCellUnderPointer(out int level0)
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
                if (cell != null && IsRolled(cell, out level0))
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
        /// Reuse the highlight classifier: a rolled cell is a Personal slot classed Rolled. Outputs
        /// the resolved 0-based slot index so callers need not recompute it.
        /// </summary>
        private static bool IsRolled(AbilityTrackSkillEntryElement cell, out int level0)
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
            PerkKind kind = PerkClassification.Classify(
                AbilityTrackSource.Personal,
                level0,
                abilityPresent,
                TftvConfigBridge.Available,
                TftvConfigBridge.IsSlotRandom);
            return kind == PerkKind.Rolled;
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
