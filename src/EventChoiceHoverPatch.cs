using System;
using HarmonyLib;
using PhoenixPoint.Geoscape.Events;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewControllers.SiteEncounters;
using PhoenixPoint.Geoscape.View.ViewModules;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Wires the outcome-preview tooltip onto normal site-encounter choices. On the normal path the
    /// choice button already FIRES <c>ChoicePointerEnter(GeoEventChoice)</c> / <c>ChoicePointerExit</c>
    /// (SiteBaseChoiceButton.cs:11/13, invoked :53/:57) but nothing subscribes them — unlike the
    /// marketplace, which binds them in TheMarketplaceChoicesController.SetChoice. This postfix on
    /// SiteEncounterChoicesController.SetChoice subscribes a handler (idempotent -=/+=, mirroring the
    /// marketplace) that builds + shows the tooltip from the choice's outcome. Config-gated; fully
    /// guarded so it can never break the event screen.
    /// </summary>
    // SetChoice is `internal`, so it cannot be referenced via nameof(); target it by string name plus
    // an explicit argument-type array. Its full signature is
    // internal override string SetChoice(GeoFaction, GeoEventChoice, SiteBaseChoiceButton, GeoscapeEventContext).
    [HarmonyPatch(typeof(SiteEncounterChoicesController), "SetChoice",
        new Type[] { typeof(GeoFaction), typeof(GeoEventChoice), typeof(SiteBaseChoiceButton), typeof(GeoscapeEventContext) })]
    public static class EventChoiceHoverPatch
    {
        // Harmony injects the postfix param BY NAME from the original method's signature
        // (GeoFaction, GeoEventChoice, SiteBaseChoiceButton choiceButton, GeoscapeEventContext),
        // so we declare only the one parameter we need, named exactly `choiceButton`.
        [HarmonyPostfix]
        public static void Postfix(SiteBaseChoiceButton choiceButton)
        {
            try
            {
                if (!OracleMain.ShowEventOutcomePreview)
                {
                    return;
                }
                if ((UnityEngine.Object)(object)choiceButton == (UnityEngine.Object)null)
                {
                    return;
                }
                // Only the normal-event button; never touch the marketplace variant.
                if (!(choiceButton is SiteEncounterChoiceButton))
                {
                    return;
                }

                // Idempotent rebind (mirrors marketplace Delegate.Remove + Delegate.Combine).
                choiceButton.ChoicePointerEnter -= OnEnter;
                choiceButton.ChoicePointerEnter += OnEnter;
                choiceButton.ChoicePointerExit -= OnExit;
                choiceButton.ChoicePointerExit += OnExit;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventChoiceHoverPatch.Postfix failed: " + ex.Message);
            }
        }

        private static void OnEnter(GeoEventChoice choice)
        {
            try
            {
                if (!OracleMain.ShowEventOutcomePreview || choice == null || choice.Outcome == null)
                {
                    return;
                }
                EventOutcomeData data = EventOutcomeAdapter.From(choice.Outcome);
                var rows = EventOutcomePreview.Build(data);
                if (rows.Count == 0)
                {
                    EventOutcomeTooltip.Hide();
                    return;
                }
                Canvas canvas = ResolveCanvas();
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null)
                {
                    return;
                }
                EventOutcomeTooltip.Show(rows, canvas);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventChoiceHoverPatch.OnEnter failed: " + ex.Message);
            }
        }

        private static void OnExit()
        {
            try
            {
                EventOutcomeTooltip.Hide();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventChoiceHoverPatch.OnExit failed: " + ex.Message);
            }
        }

        // Cached so we don't run a full-scene FindObjectsOfType<Canvas>() on every hover (part of the
        // per-hover cost that caused the hover lag). Re-resolved only when the cached canvas is gone or no
        // longer active/enabled (e.g. screen changed).
        private static Canvas _cachedCanvas;

        /// <summary>
        /// Find a live geoscape canvas to parent the tooltip (the top-most active Canvas in the scene),
        /// caching the result. The expensive <c>FindObjectsOfType&lt;Canvas&gt;()</c> scan runs only on the
        /// first hover and again whenever the cached canvas has been destroyed or disabled — not every hover.
        /// </summary>
        private static Canvas ResolveCanvas()
        {
            try
            {
                if ((UnityEngine.Object)(object)_cachedCanvas != (UnityEngine.Object)null
                    && _cachedCanvas.isActiveAndEnabled)
                {
                    return _cachedCanvas;
                }

                Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                Canvas best = null;
                foreach (Canvas c in canvases)
                {
                    if ((UnityEngine.Object)(object)c != (UnityEngine.Object)null
                        && c.isActiveAndEnabled
                        && (best == null || c.sortingOrder > best.sortingOrder))
                    {
                        best = c;
                    }
                }
                _cachedCanvas = best;
                return best;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventChoiceHoverPatch.ResolveCanvas failed: " + ex.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Catch-all that guarantees the cached outcome-preview tooltip can never linger. The hover wiring only
    /// hides the tooltip on <c>ChoicePointerExit</c>, but CLICKING a choice closes the event UI without ever
    /// firing PointerExit, so the cached GameObject was left <c>SetActive(true)</c> and stayed visible on the
    /// geoscape globe. <see cref="UIModuleSiteEncounters"/> extends <c>UIModuleBehavior</c> (a MonoBehaviour);
    /// its <c>OnDisable</c> (decompile UIModuleSiteEncounters.cs:182) runs whenever the module GameObject is
    /// disabled — i.e. for EVERY way the encounter screen goes away: a choice that completes/closes the event,
    /// the exit button, a view-state change, or returning to the globe. Hiding the tooltip here therefore
    /// covers both "after clicking a choice" and "after leaving to the globe". <see cref="EventOutcomeTooltip.Hide"/>
    /// is static, idempotent and Unity-null-safe (a destroyed cached object is skipped), so this can never throw.
    /// </summary>
    [HarmonyPatch(typeof(UIModuleSiteEncounters), "OnDisable")]
    public static class EventModuleHideTooltipPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                EventOutcomeTooltip.Hide();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventModuleHideTooltipPatch.Postfix failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Immediate hide the instant a choice is CLICKED, before the module decides whether to close, page to the
    /// next text, or show the closing reward step. <see cref="SiteBaseChoicesController.OnButtonChoiceSelected"/>
    /// (decompile SiteBaseChoicesController.cs:84, public virtual) is the single choke point invoked on every
    /// choice selection. A click always means the current hover is over, so hiding here removes the tooltip
    /// with no wait for the module to disable — and when a paging/closing step keeps the UI open with new
    /// buttons, the normal hover wiring re-shows it on the next PointerEnter. Safe for the marketplace variant
    /// too: <see cref="EventOutcomeTooltip.Hide"/> only deactivates OUR cached GameObject and is a no-op when
    /// nothing is shown.
    /// </summary>
    [HarmonyPatch(typeof(SiteBaseChoicesController), nameof(SiteBaseChoicesController.OnButtonChoiceSelected))]
    public static class EventChoiceSelectedHideTooltipPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                EventOutcomeTooltip.Hide();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventChoiceSelectedHideTooltipPatch.Postfix failed: " + ex.Message);
            }
        }
    }
}
