using System;
using HarmonyLib;
using PhoenixPoint.Geoscape.View.ViewStates;
using UnityEngine;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Two-stage cancel for the dual-class subclass picker's "CLASS PERKS" wiki banner.
    ///
    /// The geoscape modal back/cancel action (RMB or Esc) routes through
    /// <see cref="UIStateGeoModal.OnCancel"/> (UIStateGeoModal.cs:94), which closes the modal via
    /// FinishQueriedState. While our banner is open we intercept the FIRST cancel to close ONLY the banner
    /// (modal stays open); a SECOND cancel (banner already closed) falls through to the native exit. The
    /// banner is only ever opened from within this modal (SubclassWikiClickHandler), and the progression
    /// screen's wiki uses a different seam (UIModuleCharacterProgression.OnCancelInputHandler), so this
    /// prefix never affects the progression-screen behavior.
    ///
    /// An ExitState postfix is the orphan fail-safe: whenever a geoscape modal actually closes (by ANY
    /// route — cancel, selecting a class, Esc), any still-open banner is torn down so it can never outlive
    /// the modal as a stranded overlay.
    /// </summary>
    [HarmonyPatch(typeof(UIStateGeoModal), "OnCancel")]
    internal static class UIStateGeoModalOnCancelPatch
    {
        private static bool Prefix()
        {
            try
            {
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                    return false; // swallow this cancel: keep the modal open, just close our banner
                }
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] UIStateGeoModal.OnCancel prefix failed: " + ex.Message);
            }
            return true; // banner not open -> let the native cancel exit the modal as normal
        }
    }

    [HarmonyPatch(typeof(UIStateGeoModal), "ExitState")]
    internal static class UIStateGeoModalExitStatePatch
    {
        private static void Postfix()
        {
            try
            {
                // Fail-safe: the modal is closing for good -> never leave an orphan banner behind.
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                }
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] UIStateGeoModal.ExitState postfix failed: " + ex.Message);
            }
        }
    }
}
