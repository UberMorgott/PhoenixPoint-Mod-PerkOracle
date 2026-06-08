using System;
using System.Collections.Generic;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Attached to a subclass picker button (available OR greyed-injected). On right-click it opens the
    /// view-only "CLASS PERKS" wiki banner for this button's <see cref="SpecializationDef"/>. Left-clicks
    /// are ignored so the native "select this subclass" action is untouched on available buttons; greyed
    /// buttons are non-selectable, so only the preview applies. Wrapped so a UI hiccup never throws into
    /// the event system. Reuses the right-click-to-open convention from the progression screen.
    /// </summary>
    public sealed class SubclassWikiClickHandler : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>I2 term + English fallback for the class-perk banner title.</summary>
        public const string ClassTitleTerm = "PERKORACLE_WIKI_TITLE_CLASS";
        public const string ClassTitleFallback = "CLASS PERKS";

        /// <summary>The subclass whose guaranteed perks this button previews.</summary>
        public SpecializationDef Spec;

        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
                {
                    return; // only right-click opens the preview; left-click stays native
                }
                if ((UnityEngine.Object)(object)Spec == (UnityEngine.Object)null)
                {
                    return;
                }

                // Wiki already open -> right-click toggles it closed (matches the progression screen).
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                    return;
                }

                List<TacticalAbilityDef> defs = ClassPerkProvider.GetClassPerks(Spec);
                if (defs == null || defs.Count == 0)
                {
                    Debug.Log("[PerkOracle] subclass wiki: empty class-perk list for "
                              + ((UnityEngine.Object)Spec).name);
                    return;
                }

                Canvas canvas = ((Component)this).GetComponentInParent<Canvas>();
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null)
                {
                    return;
                }

                // View-only: swapContext = null. Custom title => "CLASS PERKS".
                PerkWikiPanel.Open(canvas, defs, null, ClassTitleTerm, ClassTitleFallback);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassWikiClickHandler.OnPointerClick failed: " + ex.Message);
            }
        }
    }
}
