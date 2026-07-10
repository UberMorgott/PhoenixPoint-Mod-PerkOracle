using System;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.View.ViewControllers;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Postfix on the cell's populate seam: classify the perk and show our dark background only
    /// for rolled Personal perks. All bodies are wrapped so an exception never breaks the UI build.
    /// </summary>
    [HarmonyPatch(typeof(AbilityTrackSkillEntryElement), "SetSkillState",
        new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class SetSkillStatePatch
    {
        private static void Postfix(AbilityTrackSkillEntryElement __instance)
        {
            try
            {
                // Wiki candidate cells are CLONES of native cells; they call SetSkill -> SetSkillState with
                // the real level-L Personal slot, which would otherwise classify as Rolled and get tinted.
                // Skip them: they are not progression cells and must keep their bright native look.
                if (((Component)__instance).gameObject.name.StartsWith(WikiIconFactory.CloneNamePrefix, StringComparison.Ordinal))
                {
                    return;
                }

                // Class rows are always fixed -> never highlight.
                if (__instance.TrackSource != AbilityTrackSource.Personal)
                {
                    CellBackground.Apply(__instance, false);
                    return;
                }

                AbilityTrackSlot slot = __instance.TrackSlot;
                if (slot == null || (UnityEngine.Object)(object)__instance.AbilityDef == (UnityEngine.Object)null)
                {
                    // Empty / non-ability cell.
                    CellBackground.Apply(__instance, false);
                    return;
                }

                AbilityTrack track = slot.AbilityTrack;
                if (track == null)
                {
                    CellBackground.Apply(__instance, false);
                    return;
                }

                // GetAbilityLevel is 1-based (0 = not found); convert to a 0-based slot index.
                int level = track.GetAbilityLevel(slot);
                if (level <= 0)
                {
                    CellBackground.Apply(__instance, false);
                    return;
                }
                int level0 = level - 1;

                // A Personal cell is Rolled only if its ability is a member of the engine's random
                // rolled-perk pool — either tagged with PersonalProgressionTag (vanilla / TheTurned
                // arm-marker) OR present in this slot's resolved rolled pool (TFTV human perks, which
                // arrive untagged). className is owner-independent here (null = full slot pool).
                bool isPoolMember = RolledPoolMembership.IsRolledPoolMember(__instance.AbilityDef, level0, className: null);

                PerkKind kind = PerkClassification.Classify(
                    AbilityTrackSource.Personal,
                    level0,
                    abilityPresent: true,
                    bridgeAvailable: TftvConfigBridge.Available,
                    isSlotRandom: TftvConfigBridge.IsSlotRandom,
                    abilityIsRolledPoolMember: isPoolMember);

                CellBackground.Apply(__instance, kind == PerkKind.Rolled);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SetSkillState postfix failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Second, independent postfix on the same populate seam. When the cell is the soldier's FIRST
    /// main-class skill — the already-trained level-1 <see cref="AbilityTrackSource.PrimaryClass"/> cell —
    /// attach the <see cref="ClassWikiClickHandler"/> so a left-click opens the class wiki. Every other
    /// cell (secondary/personal rows, buyable/empty cells, non-level-1 primary cells) and mutoids (whose
    /// cells live under a <see cref="MutoidAbilityTrackContainerElement"/>) are skipped. Attach is
    /// idempotent (check-before-add) because the cell GO is pooled and re-populated on every refresh /
    /// soldier switch. The native cell left-click no-ops on a trained skill (OnPointerClick returns early
    /// when !IsBuyableSkill), so intercepting the click here is safe. Kept separate from the tint postfix
    /// above so neither disturbs the other; fully guarded so it can never break the UI build.
    /// </summary>
    [HarmonyPatch(typeof(AbilityTrackSkillEntryElement), "SetSkillState",
        new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class ClassWikiCellPatch
    {
        private static void Postfix(AbilityTrackSkillEntryElement __instance)
        {
            try
            {
                if (!OracleMain.EnablePerkWiki)
                {
                    return; // feature gate (live) — also enforced in the handler + the panel open path
                }

                // Wiki clones re-enter SetSkillState with a Personal slot; never a progression cell.
                if (((Component)__instance).gameObject.name.StartsWith(WikiIconFactory.CloneNamePrefix, StringComparison.Ordinal))
                {
                    return;
                }

                // Only the trained FIRST main-class cell: PrimaryClass + known + not buyable + level-1 slot.
                if (__instance.TrackSource != AbilityTrackSource.PrimaryClass
                    || !__instance.KnownSkill
                    || __instance.IsBuyableSkill)
                {
                    return;
                }
                AbilityTrackSlot slot = __instance.TrackSlot;
                if (slot == null || slot.AbilityTrack == null || slot.AbilityTrack.GetAbilityLevel(slot) != 1)
                {
                    return; // not the level-1 (first) cell
                }

                // Mutoids use MutoidAbilityTrackContainerElement + a non-standard progression: never a target.
                if ((UnityEngine.Object)(object)((Component)__instance).GetComponentInParent<MutoidAbilityTrackContainerElement>()
                    != (UnityEngine.Object)null)
                {
                    return;
                }

                // Idempotent: the cell GO is reused across refreshes/soldiers, so add at most one handler.
                if ((UnityEngine.Object)(object)((Component)__instance).GetComponent<ClassWikiClickHandler>() == (UnityEngine.Object)null)
                {
                    ((Component)__instance).gameObject.AddComponent<ClassWikiClickHandler>();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassWikiCellPatch postfix failed: " + ex.Message);
            }
        }
    }

    /// <summary>Empty cells bypass SetSkillState; hide our background there too.</summary>
    [HarmonyPatch(typeof(AbilityTrackSkillEntryElement), "SetEmpty")]
    internal static class SetEmptyPatch
    {
        private static void Postfix(AbilityTrackSkillEntryElement __instance)
        {
            try
            {
                CellBackground.Apply(__instance, false);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SetEmpty postfix failed: " + ex.Message);
            }
        }
    }
}
