using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// Shows the event-outcome preview using the SAME framed widget the perk wiki uses: it clones the
    /// game's native <see cref="GeoRosterAbilityDetailTooltip"/> GameObject (exactly like
    /// <see cref="PerkWikiPanel.CreateTooltipClone"/>) so we inherit its real frame sprite, font,
    /// fontSize, padding and content-size fitting for free. Instead of calling the widget's data-bound
    /// <c>Show(abilityDef, view, ...)</c> -- which strictly requires a ViewElementDef and cannot accept
    /// arbitrary rows -- we repurpose its existing text fields: the ability-title line becomes the
    /// "Outcome" header and the ability-description becomes the composed, sign-coloured row list. The
    /// icon and skill-cost groups are deactivated. One live instance owned statically: <see cref="Show"/>
    /// builds it under the supplied root canvas; <see cref="Hide"/> tears it down. Every public entry
    /// point is wrapped in try/catch + <see cref="OracleLog"/> so a UI hiccup can never throw back into
    /// the event screen. Mirrors the try/catch + cursor->canvas-local positioning + CanvasGroup
    /// blocksRaycasts=false flicker fix of <see cref="WikiAbilityTooltipTrigger"/> / PerkWikiPanel.
    /// </summary>
    public static class EventOutcomeTooltip
    {
        // Standard reward sign colours, readable on the native dark frame. Used in place of the
        // def-driven RewardsColorsDef.PrimaryUIColor/SecondaryUIColor (UIModuleSiteEncounters.cs:173-174):
        // those are only assembled into PositiveRewardTextPattern/NegativeRewardTextPattern inside that
        // module's Awake and are not reachable as a stable static here, so we fall back to plain
        // green / red rich-text tags (the prompt's documented fallback path).
        private const string PositiveColor = "#6FCF6F";
        private const string NegativeColor = "#E06C6C";

        private const float CursorXOffset = 18f;

        private static GameObject _root;
        private static RectTransform _rootRt;
        private static RectTransform _canvasRect;
        private static Canvas _rootCanvas;

        /// <summary>True while a tooltip instance is live.</summary>
        public static bool IsShown => (UnityEngine.Object)(object)_root != (UnityEngine.Object)null;

        /// <summary>
        /// Build + show the tooltip for <paramref name="rows"/> parented to <paramref name="anchorCanvas"/>'s
        /// root canvas, positioned at the current mouse position. No-op (and hides any prior instance) when
        /// rows is null/empty, the canvas is missing, or no native tooltip template can be cloned.
        /// Localizes any label that is an <c>ORACLE_*</c> key.
        /// </summary>
        public static void Show(List<EventOutcomeRow> rows, Canvas anchorCanvas)
        {
            try
            {
                Hide();
                if (rows == null || rows.Count == 0
                    || (UnityEngine.Object)(object)anchorCanvas == (UnityEngine.Object)null)
                {
                    return;
                }

                _rootCanvas = anchorCanvas.rootCanvas;
                Transform rootParent = ((UnityEngine.Object)(object)_rootCanvas != (UnityEngine.Object)null)
                    ? _rootCanvas.transform
                    : anchorCanvas.transform;
                _canvasRect = ((UnityEngine.Object)(object)_rootCanvas != (UnityEngine.Object)null)
                    ? _rootCanvas.transform as RectTransform
                    : null;

                GeoRosterAbilityDetailTooltip tip = CloneNativeTooltip(rootParent);
                if ((UnityEngine.Object)(object)tip == (UnityEngine.Object)null)
                {
                    // No native template available on this screen; show nothing rather than fall back to
                    // an unstyled box (the framed look depends entirely on the cloned widget).
                    Hide();
                    return;
                }

                PopulateTooltip(tip, rows);

                _root.SetActive(true);
                _root.transform.SetAsLastSibling(); // render above the event module
                Position();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.Show failed: " + ex.Message);
                Hide();
            }
        }

        /// <summary>Tear down the live tooltip instance, if any. Safe to call when nothing is shown.</summary>
        public static void Hide()
        {
            try
            {
                if ((UnityEngine.Object)(object)_root != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(_root);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.Hide failed: " + ex.Message);
            }
            _root = null;
            _rootRt = null;
            _canvasRect = null;
            _rootCanvas = null;
        }

        /// <summary>
        /// Clone the game's native ability-detail tooltip GameObject under <paramref name="rootParent"/>
        /// (mirrors <see cref="PerkWikiPanel.CreateTooltipClone"/>): prefer a live in-scene instance, else
        /// any inactive scene instance. The clone is kept inactive while it is configured, never blocks
        /// raycasts (so it cannot steal the hover from the choice button beneath it) and is full size.
        /// Stores the clone in <see cref="_root"/>/<see cref="_rootRt"/>. Returns the widget component or
        /// null when no template exists / the clone fails.
        /// </summary>
        private static GeoRosterAbilityDetailTooltip CloneNativeTooltip(Transform rootParent)
        {
            try
            {
                var template = UnityEngine.Object.FindObjectsOfType<GeoRosterAbilityDetailTooltip>().FirstOrDefault();
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    template = Resources.FindObjectsOfTypeAll<GeoRosterAbilityDetailTooltip>()
                        .FirstOrDefault(t => (UnityEngine.Object)(object)t != (UnityEngine.Object)null
                            && t.gameObject.scene.IsValid()); // a scene instance, not a prefab asset
                }
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    return null;
                }

                _root = UnityEngine.Object.Instantiate(template.gameObject, rootParent, false);
                _root.name = "OracleEventOutcomeTooltip";
                _root.transform.localScale = Vector3.one; // full size (NOT 0.5)
                _root.SetActive(false); // stay hidden until populated

                // Never intercept pointer events (mirrors the wiki tooltip clone's blocksRaycasts=false
                // flicker fix): the tooltip must not steal the hover from the choice button beneath it.
                var cg = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                _rootRt = _root.GetComponent<RectTransform>();
                return _root.GetComponent<GeoRosterAbilityDetailTooltip>();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.CloneNativeTooltip failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Repurpose the cloned widget's text fields for the outcome preview instead of an ability:
        /// the title line shows the localized "Outcome" header and the description line shows the
        /// composed, sign-coloured rows. The ability icon and the SP/AP/WP skill-cost groups are
        /// deactivated so only the framed title + body remain.
        /// </summary>
        private static void PopulateTooltip(GeoRosterAbilityDetailTooltip tip, List<EventOutcomeRow> rows)
        {
            if ((UnityEngine.Object)(object)tip.AbilityTitleText != (UnityEngine.Object)null)
            {
                tip.AbilityTitleText.text = Loc.Get("ORACLE_OUTCOME_HEADER", "Outcome");
            }
            if ((UnityEngine.Object)(object)tip.AbilityDescription != (UnityEngine.Object)null)
            {
                tip.AbilityDescription.text = ComposeBody(rows);
            }

            SetActiveSafe(tip.AbilityIcon);
            SetActiveSafe(tip.AbilitySkillCostGroup);
            SetActiveSafe(tip.AbilitySkillCostText);
            SetActiveSafe(tip.AbilitySkillAPCostText);
            SetActiveSafe(tip.AbilitySkillWPCostText);
            SetActiveSafe(tip.SkillCostHeaderText);
        }

        /// <summary>Deactivate the GameObject behind a widget field (a Component), if present.</summary>
        private static void SetActiveSafe(Component c)
        {
            if ((UnityEngine.Object)(object)c != (UnityEngine.Object)null)
            {
                c.gameObject.SetActive(false);
            }
        }

        /// <summary>Deactivate a widget field that is exposed directly as a GameObject, if present.</summary>
        private static void SetActiveSafe(GameObject go)
        {
            if ((UnityEngine.Object)(object)go != (UnityEngine.Object)null)
            {
                go.SetActive(false);
            }
        }

        /// <summary>
        /// Compose the body text: one line per row as "label   value", with the value wrapped in a
        /// green (positive "+") / red (negative "-") rich-text colour tag; other values (ranges, "xN",
        /// "N%") and name-only rows stay the field's default colour. A label that begins with "ORACLE_"
        /// is treated as a loc key and resolved via <see cref="Loc"/> (the pure formatter emits raw keys
        /// for fixed scalar rows); any other label is already-localized text from
        /// <see cref="EventOutcomeAdapter"/>.
        /// </summary>
        private static string ComposeBody(List<EventOutcomeRow> rows)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                EventOutcomeRow row = rows[i];
                string label = row.Label ?? string.Empty;
                if (label.StartsWith("ORACLE_"))
                {
                    label = Loc.Get(label, FallbackLabel(label));
                }

                if (i > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(label);
                if (!string.IsNullOrEmpty(row.Value))
                {
                    sb.Append("   ").Append(Colorize(row.Value));
                }
            }
            return sb.ToString();
        }

        /// <summary>Wrap a signed value in a green/red rich-text colour tag; leave neutral values plain.</summary>
        private static string Colorize(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }
            if (value[0] == '+')
            {
                return "<color=" + PositiveColor + ">" + value + "</color>";
            }
            if (value[0] == '-')
            {
                return "<color=" + NegativeColor + ">" + value + "</color>";
            }
            return value;
        }

        /// <summary>English fallback strings for the fixed-scalar ORACLE_ row keys (CSV provides translations).</summary>
        private static string FallbackLabel(string key)
        {
            switch (key)
            {
                case "ORACLE_OUTCOME_HP": return "Soldier HP";
                case "ORACLE_OUTCOME_HP_ALL": return "All Soldiers HP";
                case "ORACLE_OUTCOME_STAMINA": return "Soldier Stamina";
                case "ORACLE_OUTCOME_STAMINA_ALL": return "All Soldiers Stamina";
                case "ORACLE_OUTCOME_AIRCRAFT": return "Aircraft";
                case "ORACLE_OUTCOME_SKILLPOINTS": return "Skill Points";
                case "ORACLE_OUTCOME_HAVENPOP": return "Haven Population";
                case "ORACLE_OUTCOME_SDI": return "SDI";
                default: return key;
            }
        }

        /// <summary>Position the tooltip near the cursor (mirrors WikiAbilityTooltipTrigger.Position).</summary>
        private static void Position()
        {
            try
            {
                if ((UnityEngine.Object)(object)_rootRt == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)_canvasRect == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)_rootCanvas == (UnityEngine.Object)null)
                {
                    return;
                }
                Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect, Input.mousePosition, cam, out local))
                {
                    _rootRt.anchoredPosition = local + new Vector2(CursorXOffset, 0f);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.Position failed: " + ex.Message);
            }
        }
    }
}
