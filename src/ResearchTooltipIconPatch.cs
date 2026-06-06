using System;
using HarmonyLib;
using PhoenixPoint.Geoscape.View.ViewControllers.Research;
using UnityEngine;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Shows the mod's custom research illustration for the Operative Reconditioning project.
    ///
    /// The research-screen illustration is the <see cref="ResearchTooltip.Icon"/> Image, which the tooltip
    /// writes only in <c>Init</c> (and <c>ShowDefault</c>) — there is no animation/coroutine that re-clobbers
    /// it, so setting the sprite in an <c>Init</c> postfix holds until the next <c>Init</c> (which we ride
    /// again). <c>Init</c> runs on every tooltip open (UIModuleResearch.ShowTooltip / ShowTooltipDelayed).
    ///
    /// We patch the displayed Image rather than the def's icon because <c>ResearchViewElementDef.ResearchIcon</c>
    /// is a read-only Addressable reference that cannot take a runtime-loaded <see cref="Sprite"/>.
    ///
    /// Gate: only act for our research (<see cref="PerkSwapResearch.ResearchId"/>) and only when the custom
    /// sprite actually loaded (<see cref="PerkSwapResearch.IconSprite"/> != null). Everything is wrapped so a
    /// failure here can never break the research tooltip.
    /// </summary>
    [HarmonyPatch(typeof(ResearchTooltip), "Init")]
    internal static class ResearchTooltipIconPatch
    {
        private static void Postfix(ResearchTooltip __instance)
        {
            try
            {
                Sprite icon = PerkSwapResearch.IconSprite;
                if ((UnityEngine.Object)(object)icon == (UnityEngine.Object)null)
                {
                    // No custom sprite loaded: never touch the shared Icon at all (leave its flag as-is).
                    return;
                }
                if ((UnityEngine.Object)(object)__instance.Icon == (UnityEngine.Object)null)
                {
                    return;
                }

                // ResearchTooltip.Icon is a SINGLE shared Image reused for every research
                // (UIModuleResearch.cs:54). We must own preserveAspect symmetrically: set it true only while
                // OUR research is displayed, and restore it to false for any other research — otherwise a
                // stale true leaks onto stock art viewed afterward and can letterbox it.
                if (__instance.Research?.ResearchDef?.Id == PerkSwapResearch.ResearchId)
                {
                    __instance.Icon.sprite = icon;
                    // Defensive: the Icon slot's RectTransform/preserveAspect are prefab-baked (unknown).
                    // Force preserveAspect so the square custom sprite is not stretched on a non-square slot.
                    __instance.Icon.preserveAspect = true;
                }
                else
                {
                    // Some other research is showing: undo any preserveAspect we may have left set.
                    __instance.Icon.preserveAspect = false;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] ResearchTooltipIconPatch.Postfix failed: " + ex.Message);
            }
        }
    }
}
