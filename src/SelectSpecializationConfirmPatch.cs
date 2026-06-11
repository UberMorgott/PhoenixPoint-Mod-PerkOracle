using System;
using System.Collections.Generic;
using Base.Core;
using Base.UI.MessageBox;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Modal;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Preview-then-confirm for AVAILABLE (selectable) subclasses in the dual-class picker.
    ///
    /// The native selection applier is <see cref="SelectSpecializationDataBind.SelectSpecializationElement"/>
    /// (SelectSpecializationDataBind.cs:68 -> sets _data.SelectedSpec + _modal.Confirm()). It is invoked by
    /// each active button's prefab onClick. We PREFIX it: on the first (un-confirmed) click we suppress the
    /// immediate native selection, open the view-only "CLASS PERKS" wiki banner for that subclass, and raise
    /// the native Yes/No message box ("Take {0} as a subclass?"). On YES we re-invoke the same native method
    /// with a one-shot guard so the original selection runs (the modal then confirms/closes, and our existing
    /// UIStateGeoModal.ExitState postfix tears the banner down — no orphan). On NO/cancel we just close the
    /// banner and stay in the picker.
    ///
    /// Greyed (locked) clones never reach this seam: their Button is non-interactable so its onClick never
    /// fires; their left-click preview is handled separately by SubclassWikiClickHandler. On any failure we
    /// let the native selection proceed, so the picker can never be bricked.
    /// </summary>
    [HarmonyPatch(typeof(SelectSpecializationDataBind), "SelectSpecializationElement")]
    internal static class SelectSpecializationConfirmPatch
    {
        public const string ConfirmTerm = "ORACLE_CONFIRM_SUBCLASS";
        public const string ConfirmFallback = "Take {0} as a subclass?";

        // One-shot guard: the element the user just confirmed with YES. When set, the next call for that
        // same element is allowed straight through to the native selection.
        private static SpecializationOptionElementController _confirmedElement;

        private static bool Prefix(SelectSpecializationDataBind __instance,
            SpecializationOptionElementController element)
        {
            try
            {
                // YES path: this is our re-invocation for the just-confirmed element -> run the original.
                if ((UnityEngine.Object)(object)element != (UnityEngine.Object)null
                    && (object)element == (object)_confirmedElement)
                {
                    _confirmedElement = null;
                    return true;
                }

                if ((UnityEngine.Object)(object)element == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)element.SpecializationDef == (UnityEngine.Object)null)
                {
                    return true; // nothing to gate -> native behavior
                }

                MessageBox box = GameUtl.GetMessageBox();
                if ((UnityEngine.Object)(object)box == (UnityEngine.Object)null)
                {
                    return true; // no message box available -> don't suppress; let native select proceed
                }

                SpecializationDef spec = element.SpecializationDef;

                // The class-perk row is embedded INTO the message box itself (see
                // SubclassConfirmPopupDecorator) rather than shown as a separate floating banner, so the
                // perks and the question read as one cohesive dialog. We pass the resolved perk defs as the
                // prompt's UserData marker; the decorator's Show postfix injects the icon row when it sees it.
                List<TacticalAbilityDef> perks = ClassPerkProvider.GetClassPerks(spec);
                var marker = new SubclassConfirmPromptData(perks);

                string className = ResolveClassName(spec);
                string question = string.Format(Loc.Get(ConfirmTerm, ConfirmFallback), className);
                box.ShowSimplePrompt(question, MessageBoxIcon.Question, MessageBoxButtons.YesNo,
                    delegate(MessageBoxCallbackResult res)
                    {
                        try
                        {
                            if (res.DialogResult == MessageBoxResult.Yes)
                            {
                                // Re-enter the native selection through the guard.
                                _confirmedElement = element;
                                __instance.SelectSpecializationElement(element);
                            }
                            // NO / cancel: nothing to do — the embedded row dies with the message box,
                            // the picker stays open, and no subclass is selected.
                        }
                        catch (Exception ex)
                        {
                            OracleLog.Debug("[Oracle] SelectSpecialization confirm callback failed: " + ex.Message);
                        }
                    },
                    sender: null, userData: marker);

                return false; // suppress the immediate native selection; the callback decides.
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SelectSpecializationElement prefix failed: " + ex.Message);
                _confirmedElement = null;
                return true; // never brick the picker -> let native selection proceed
            }
        }

        /// <summary>Localized class display name (proficiency view element); falls back to the def name.</summary>
        private static string ResolveClassName(SpecializationDef spec)
        {
            try
            {
                ClassProficiencyAbilityDef prof = spec.GetSpecProficiency();
                if ((UnityEngine.Object)(object)prof != (UnityEngine.Object)null
                    && (UnityEngine.Object)(object)prof.ViewElementDef != (UnityEngine.Object)null
                    && prof.ViewElementDef.DisplayName1 != null)
                {
                    string name = prof.ViewElementDef.DisplayName1.Localize();
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
            catch
            {
                // fall through to the def name
            }
            return ((UnityEngine.Object)spec).name;
        }
    }
}
