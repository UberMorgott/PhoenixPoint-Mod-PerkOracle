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
                // Only our research → swap the sprite. For any other research, change nothing.
                if (__instance.Research?.ResearchDef?.Id != PerkSwapResearch.ResearchId)
                {
                    return;
                }

                Sprite icon = PerkSwapResearch.IconSprite;
                if ((UnityEngine.Object)(object)icon == (UnityEngine.Object)null)
                {
                    // No custom sprite loaded: leave the slot untouched.
                    return;
                }
                if ((UnityEngine.Object)(object)__instance.Icon == (UnityEngine.Object)null)
                {
                    return;
                }

                // Replicate vanilla rendering exactly: set ONLY the sprite and leave every slot flag
                // (preserveAspect, RectTransform, etc.) alone. Vanilla never sets preserveAspect on this
                // shared Icon Image, and our illustration is native 2:1 (1024×512) like every vanilla
                // research image — so swapping only the sprite makes it render identically to stock art.
                __instance.Icon.sprite = icon;
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] ResearchTooltipIconPatch.Postfix failed: " + ex.Message);
            }
        }
    }
}
