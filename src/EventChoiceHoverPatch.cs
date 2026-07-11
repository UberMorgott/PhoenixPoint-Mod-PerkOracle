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
    ///
    /// Multi-choice gate: the preview is only useful for COMPARING outcomes across different answer
    /// choices, so it is shown ONLY for events that present the player 2+ choices. A single-choice /
    /// "OK"-acknowledge event has one inevitable outcome and gets NO preview. The gate reads the count
    /// at render time from <see cref="EventChoiceCountGatePatch"/> (post any TFTV choice filtering) and
    /// is checked in <see cref="OnEnter"/> — buttons are pooled/reused across events, so a handler left
    /// on a button by a prior multi-choice event must still honour the CURRENT event's count.
    /// </summary>
    // SetChoice is `internal`, so it cannot be referenced via nameof(); target it by string name plus
    // an explicit argument-type array. Its full signature is
    // internal override string SetChoice(GeoFaction, GeoEventChoice, SiteBaseChoiceButton, GeoscapeEventContext).
    [HarmonyPatch(typeof(SiteEncounterChoicesController), "SetChoice",
        new Type[] { typeof(GeoFaction), typeof(GeoEventChoice), typeof(SiteBaseChoiceButton), typeof(GeoscapeEventContext) })]
    public static class EventChoiceHoverPatch
    {
        /// <summary>
        /// True when the currently-displayed site-encounter event presents the player 2+ answer choices;
        /// false for a single inevitable / "OK"-acknowledge choice (or none). Set per event render by
        /// <see cref="EventChoiceCountGatePatch"/> and gates all preview output in <see cref="OnEnter"/>.
        /// </summary>
        public static bool MultiChoiceEvent;

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
                // Multi-choice gate: no preview for a single-choice / "OK"-acknowledge event.
                if (!OracleMain.ShowEventOutcomePreview || !MultiChoiceEvent || choice == null || choice.Outcome == null)
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
    /// Records, per event render, whether the site-encounter dialog is showing the player 2+ answer choices,
    /// so <see cref="EventChoiceHoverPatch.OnEnter"/> can suppress the outcome preview for single-choice /
    /// "OK"-acknowledge events. <see cref="SiteBaseChoicesController.SetChoices"/> is the single render entry
    /// point that lays out the choice buttons; it iterates <c>eventData.EventData.Choices</c> (decompile
    /// SiteBaseChoicesController.cs:31/42) — the exact post-filter list the buttons are built from, so its
    /// Count is the number the player actually sees (Count==0 renders one synthetic empty button). A Prefix
    /// runs before that loop, so the per-choice hover Postfix + later hovers read a current flag. Fully
    /// guarded; any failure defaults to "not multi-choice" (preview stays hidden rather than risk showing).
    /// </summary>
    [HarmonyPatch(typeof(SiteBaseChoicesController), nameof(SiteBaseChoicesController.SetChoices))]
    public static class EventChoiceCountGatePatch
    {
        [HarmonyPrefix]
        public static void Prefix(GeoscapeEvent eventData)
        {
            try
            {
                EventChoiceHoverPatch.MultiChoiceEvent =
                    eventData != null && eventData.EventData != null
                    && eventData.EventData.Choices != null
                    && eventData.EventData.Choices.Count >= 2;
            }
            catch (Exception ex)
            {
                EventChoiceHoverPatch.MultiChoiceEvent = false;
                OracleLog.Debug("[Oracle] EventChoiceCountGatePatch.Prefix failed: " + ex.Message);
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
