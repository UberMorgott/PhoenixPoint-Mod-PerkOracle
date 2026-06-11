using System;
using System.Collections.Generic;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// Attached to a GREYED-injected subclass clone in the dual-class picker. The clone is non-selectable
    /// (its native Button is disabled), so a LEFT-click there has no native action and we use it to open
    /// the view-only "CLASS PERKS" wiki banner for the clone's <see cref="SpecializationDef"/> (clicking
    /// again toggles it closed). We open on LEFT click because inside this MODAL right-click is routed to
    /// the game's Cancel/input pipeline and is NEVER delivered as a UGUI pointer-click (confirmed by the
    /// runtime diag log: only button=Left ever arrives) — the same reason the progression-screen wiki
    /// hooks the cancel seam instead of an IPointerClickHandler. We deliberately do NOT attach this to the
    /// native AVAILABLE buttons, so their left-click keeps the native "select this subclass" action intact.
    /// Wrapped so a UI hiccup never throws into the event system.
    /// </summary>
    public sealed class SubclassWikiClickHandler : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>I2 term + English fallback for the class-perk banner title.</summary>
        public const string ClassTitleTerm = "ORACLE_WIKI_TITLE_CLASS";
        public const string ClassTitleFallback = "CLASS PERKS";

        /// <summary>The subclass whose guaranteed perks this button previews.</summary>
        public SpecializationDef Spec;

        /// <summary>
        /// When true (greyed clones), a LEFT-click opens the preview. Set because right-click is consumed
        /// by the modal's Cancel pipeline and never reaches this handler. Right-click is also accepted as a
        /// trigger for robustness, in case a context ever does deliver it.
        /// </summary>
        public bool OpenOnLeftClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData == null)
                {
                    return;
                }
                bool isTrigger = eventData.button == PointerEventData.InputButton.Right
                    || (OpenOnLeftClick && eventData.button == PointerEventData.InputButton.Left);
                if (!isTrigger)
                {
                    return; // not our trigger button -> leave native handling (e.g. left-click select)
                }
                if ((UnityEngine.Object)(object)Spec == (UnityEngine.Object)null)
                {
                    return;
                }

                // Wiki already open -> a trigger-click toggles it closed (matches the progression screen).
                if (PerkWikiPanel.IsOpen)
                {
                    PerkWikiPanel.Close();
                    return;
                }

                List<TacticalAbilityDef> defs = ClassPerkProvider.GetClassPerks(Spec);
                if (defs == null || defs.Count == 0)
                {
                    OracleLog.Debug("[Oracle] subclass wiki: empty class-perk list for "
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
                OracleLog.Debug("[Oracle] SubclassWikiClickHandler.OnPointerClick failed: " + ex.Message);
            }
        }
    }
}
