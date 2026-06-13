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
                // rolled-perk pool (carries PersonalProgressionTag). Augmentation / custom-mod
                // abilities (PersonalTrackTags empty) are never rolled -> never highlighted.
                bool isPoolMember = RolledPoolMembership.IsRolledPoolMember(__instance.AbilityDef);

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
