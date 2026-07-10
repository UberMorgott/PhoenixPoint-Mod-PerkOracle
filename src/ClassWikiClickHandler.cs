using System;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Morgott.Oracle
{
    /// <summary>
    /// Attached (by <c>ClassWikiCellPatch</c>) to the soldier's FIRST main-class skill cell. A LEFT-click
    /// opens the read-only class wiki: a left strip of every playable class plus a two-section perk popup
    /// (class ability track + random personal-perk pool) for the soldier's OWN class; clicking a strip
    /// icon switches classes. A second click closes it. The native cell left-click no-ops on a trained
    /// skill (<c>AbilityTrackSkillEntryElement.OnPointerClick</c> returns early when !IsBuyableSkill), so
    /// intercepting the click is safe. Everything is wrapped so a UI hiccup never throws into the event
    /// system.
    /// </summary>
    public sealed class ClassWikiClickHandler : MonoBehaviour, IPointerClickHandler
    {
        // Protected soldier field on the shared container base. Read at click time (not stashed) so it
        // always reflects the currently-shown soldier — the cell GO is pooled across soldiers.
        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");

        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                {
                    return; // left-click only
                }
                if (!OracleMain.EnablePerkWiki)
                {
                    return; // feature gate (live)
                }

                // Already open -> toggle closed (matches the progression + subclass wiki behavior).
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                    return;
                }

                var container = GetComponentInParent<AbilityTrackContainerElement>();
                if ((UnityEngine.Object)(object)container == (UnityEngine.Object)null)
                {
                    return;
                }
                var character = CharacterField?.GetValue(container) as GeoCharacter;
                SpecializationDef spec = character?.Progression?.MainSpecDef;
                if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null)
                {
                    return;
                }

                Canvas canvas = GetComponentInParent<Canvas>();
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null)
                {
                    return;
                }

                // Open the wiki on the soldier's own class; the strip lets the player switch classes.
                PerkWikiPanel.OpenClassWiki(canvas, spec);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassWikiClickHandler.OnPointerClick failed: " + ex.Message);
            }
        }
    }
}
