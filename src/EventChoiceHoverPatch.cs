using System;
using HarmonyLib;
using PhoenixPoint.Geoscape.Events;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewControllers.SiteEncounters;
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
}
