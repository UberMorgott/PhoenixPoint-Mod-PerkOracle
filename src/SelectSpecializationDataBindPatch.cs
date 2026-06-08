using System;
using System.Collections.Generic;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Modal;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// POSTFIX on the level-up subclass picker's populate seam. After the native code shows one button
    /// per available subclass and hides the spare slots, this:
    ///   1. makes every active subclass button right-clickable (preview its class perks);
    ///   2. injects greyed, non-selectable clones for the subclasses the screen omitted (unresearched),
    ///      also right-clickable for preview.
    /// Fully guarded so it can never break the picker; on any failure the native screen is untouched.
    /// Targets the VANILLA types (TFTV does not patch this modal). See spec 2026-06-08 Feature A.
    /// </summary>
    [HarmonyPatch(typeof(SelectSpecializationDataBind), "ModalShowHandler")]
    internal static class SelectSpecializationDataBindPatch
    {
        /// <summary>Name stamped on every greyed clone we inject, so we can find + dedup them.</summary>
        private const string GreyedCloneName = "PerkOracleGreyedSubclass";

        private static void Postfix(SelectSpecializationDataBind __instance, Transform ___DualClassButtonContainer)
        {
            try
            {
                if ((UnityEngine.Object)(object)___DualClassButtonContainer == (UnityEngine.Object)null)
                {
                    return;
                }

                // The modal is REUSED across level-ups (_shown resets on hide) and this container is
                // persistent, so clones from a prior show survive. Destroy them NOW (DestroyImmediate so
                // the scan below cannot re-pick stale clones this same frame) before injecting fresh ones.
                DestroyStaleClones(___DualClassButtonContainer);

                SpecializationOptionElementController[] elements =
                    ((Component)___DualClassButtonContainer)
                        .GetComponentsInChildren<SpecializationOptionElementController>(true);
                Debug.Log("[PerkOracle][diag] postfix start; elements=" + elements.Length); // TEMP diag (Feature A)

                // 1) Right-clickify every currently ACTIVE button + collect the shown specs.
                var shown = new List<SpecializationDef>();
                int attached = 0; // TEMP diag (Feature A)
                SpecializationOptionElementController activeTemplate = null;
                foreach (SpecializationOptionElementController el in elements)
                {
                    if ((UnityEngine.Object)(object)el == (UnityEngine.Object)null
                        || !((Component)el).gameObject.activeSelf)
                    {
                        continue;
                    }
                    // Belt-and-suspenders: never treat one of our own clones as a native shown entry
                    // (covers any clone that escaped DestroyImmediate, e.g. a Destroy left pending).
                    if (((Component)el).gameObject.name == GreyedCloneName)
                    {
                        continue;
                    }
                    if ((UnityEngine.Object)(object)el.SpecializationDef != (UnityEngine.Object)null)
                    {
                        shown.Add(el.SpecializationDef);
                    }
                    AttachHandler(((Component)el).gameObject, el.SpecializationDef);
                    attached++; // TEMP diag (Feature A)
                    Debug.Log("[PerkOracle][diag] active button spec=" // TEMP diag (Feature A)
                        + ((UnityEngine.Object)(object)el.SpecializationDef != (UnityEngine.Object)null
                            ? ((UnityEngine.Object)el.SpecializationDef).name : "<null>"));
                    if (activeTemplate == null)
                    {
                        activeTemplate = el; // a live, populated button to clone for greyed entries
                    }
                }

                // 2) Inject greyed clones for omitted subclasses. Need a live template to clone.
                if (activeTemplate == null)
                {
                    return; // nothing populated to clone from; leave the screen as-is
                }
                List<SpecializationDef> omitted = ClassPerkProvider.GetOmittedSubclasses(shown);
                Debug.Log("[PerkOracle][diag] shown=" + shown.Count + " attached=" + attached // TEMP diag (Feature A)
                    + " omitted=" + omitted.Count);
                foreach (SpecializationDef spec in omitted)
                {
                    Debug.Log("[PerkOracle][diag] omitted spec=" // TEMP diag (Feature A)
                        + ((UnityEngine.Object)(object)spec != (UnityEngine.Object)null
                            ? ((UnityEngine.Object)spec).name : "<null>"));
                    InjectGreyedEntry(activeTemplate, ___DualClassButtonContainer, spec);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SelectSpecializationDataBind postfix failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Destroy any greyed clones we injected on a previous show. Uses DestroyImmediate so the
        /// caller's same-frame component scan cannot re-pick them. Iterate a snapshot of child names
        /// (DestroyImmediate mutates the child list).
        /// </summary>
        private static void DestroyStaleClones(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (((Component)child).gameObject.name == GreyedCloneName)
                {
                    UnityEngine.Object.DestroyImmediate(((Component)child).gameObject);
                }
            }
        }

        /// <summary>Attach (or refresh) the right-click preview handler with the button's spec.</summary>
        private static void AttachHandler(GameObject go, SpecializationDef spec)
        {
            var handler = go.GetComponent<SubclassWikiClickHandler>();
            if ((UnityEngine.Object)(object)handler == (UnityEngine.Object)null)
            {
                handler = go.AddComponent<SubclassWikiClickHandler>();
            }
            handler.Spec = spec;
        }

        /// <summary>
        /// Clone the native button <paramref name="template"/>, populate it for <paramref name="spec"/>
        /// via the native InitSpecialization, grey it out, strip its select affordance, and make it
        /// right-clickable for preview. Non-fatal: a single bad clone is logged and skipped.
        /// </summary>
        private static void InjectGreyedEntry(SpecializationOptionElementController template,
            Transform container, SpecializationDef spec)
        {
            if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null
                || (UnityEngine.Object)(object)spec.GetSpecProficiency() == (UnityEngine.Object)null)
            {
                return; // InitSpecialization dereferences the proficiency view element
            }

            GameObject cloneGo = UnityEngine.Object.Instantiate(
                ((Component)template).gameObject, container, false);
            cloneGo.name = GreyedCloneName;
            try
            {
                cloneGo.SetActive(true);

                var cloneEl = cloneGo.GetComponent<SpecializationOptionElementController>();
                if ((UnityEngine.Object)(object)cloneEl == (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(cloneGo);
                    return;
                }

                // Populate with the native path so icon/title/description look like a real entry.
                cloneEl.InitSpecialization(spec);

                // Non-selectable: remove the native button's click listeners + disable interactability so
                // a greyed (unresearched) class can never be picked. Right-click preview still works via
                // our handler (which listens at the EventSystem level, not the Button).
                var btn = cloneGo.GetComponent<Button>();
                if ((UnityEngine.Object)(object)btn != (UnityEngine.Object)null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.interactable = false;
                }

                // Grey tint: dim every Graphic (icon + labels) so it reads as inactive.
                foreach (Graphic g in cloneGo.GetComponentsInChildren<Graphic>(true))
                {
                    Color c = g.color;
                    g.color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a * 0.6f);
                }

                AttachHandler(cloneGo, spec);
            }
            catch (Exception ex)
            {
                // Destroy the half-built clone so no orphan is left parented in the container, then
                // continue with the next omitted subclass (outer postfix catch still guards the screen).
                if ((UnityEngine.Object)(object)cloneGo != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.DestroyImmediate(cloneGo);
                }
                Debug.Log("[PerkOracle] InjectGreyedEntry failed for "
                          + ((UnityEngine.Object)(object)spec != (UnityEngine.Object)null
                             ? ((UnityEngine.Object)spec).name : "<null>")
                          + ": " + ex.Message);
            }
        }
    }
}
