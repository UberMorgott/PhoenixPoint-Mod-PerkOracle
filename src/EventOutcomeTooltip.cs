using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Geoscape.View.ViewModules;
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
    /// arbitrary rows -- we repurpose its ability-description field as the composed, sign-coloured row list.
    /// The ability title (no native header exists for the encounter reward UI, so we show none rather than
    /// invent one), icon and skill-cost groups are deactivated. One live instance owned statically: <see cref="Show"/>
    /// builds it under the supplied root canvas; <see cref="Hide"/> tears it down. Every public entry
    /// point is wrapped in try/catch + <see cref="OracleLog"/> so a UI hiccup can never throw back into
    /// the event screen. Mirrors the try/catch + cursor->canvas-local positioning + CanvasGroup
    /// blocksRaycasts=false flicker fix of <see cref="WikiAbilityTooltipTrigger"/> / PerkWikiPanel.
    /// </summary>
    public static class EventOutcomeTooltip
    {
        // Reward colours captured from the LIVE encounter module each Show, so our rows match the native
        // post-choice reward display exactly (UIModuleSiteEncounters):
        //   * value, positive  = RewardsColorsDef.PrimaryUIColor   (the "+" pattern, decompile line 173)
        //   * value, negative  = RewardsColorsDef.SecondaryUIColor (the "-" pattern, decompile line 174)
        //   * label            = EncounterRewardTextPrefab's Text colour (the gold/orange the native
        //                        resource line renders its DisplayName1 in; line 417 emits it uncoloured,
        //                        so its colour IS the prefab default).
        // These fall back to the constants below only when the module/def/prefab can't be read.
        private const string FallbackPositiveColor = "#6FCF6F";
        private const string FallbackNegativeColor = "#E06C6C";
        private const string FallbackLabelColor = "#D9A441"; // PP-style reward gold

        private static string _positiveColor = FallbackPositiveColor;
        private static string _negativeColor = FallbackNegativeColor;
        private static string _labelColor = FallbackLabelColor;

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
        /// rows is null/empty, the canvas is missing, or no native tooltip template can be cloned. Every row
        /// label is already-localized native game text (resolved by <see cref="EventOutcomeAdapter"/>).
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

                CaptureNativeRewardColors();
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

                // CRITICAL: strip every I2 Localization `Localize` MonoBehaviour under the clone.
                // The native title/description Text objects each carry an I2.Loc.Localize component bound
                // to the ability's term. Its OnEnable()/Awake() (I2.Loc/Localize.cs:112-125) calls
                // OnLocalize() -> mLocalizeTarget.DoLocalize(...) (Localize.cs:237), which OVERWRITES the
                // Text.text from the (now stale/empty) term. With no valid term in our repurposed clone,
                // I2 emits its "NEEDS TEXT" placeholder, clobbering the raw strings we set in
                // PopulateTooltip the instant Show() reactivates the hierarchy. The perk-wiki tooltip never
                // hits this because it feeds the native Show() a value that matches the bound term, so I2
                // re-resolves to the same string. We set arbitrary (non-term) text, so we must remove the
                // localizers. Destroying only the Localize component leaves the Text's font/fontSize/color/
                // alignment untouched, so the framed look is preserved.
                foreach (var loc in _root.GetComponentsInChildren<I2.Loc.Localize>(true))
                {
                    if ((UnityEngine.Object)(object)loc != (UnityEngine.Object)null)
                    {
                        UnityEngine.Object.DestroyImmediate(loc);
                    }
                }

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
        /// Repurpose the cloned widget's text fields for the outcome preview instead of an ability: the
        /// description line shows the composed, sign-coloured rows. There is NO header — the native encounter
        /// reward UI (UIModuleSiteEncounters.ShowReward -> AddRewardText) simply appends reward lines with no
        /// heading, and the game has no "Outcome"/"Rewards" loc term for one, so we drop the title rather than
        /// invent a label. The ability title, icon and SP/AP/WP skill-cost groups are deactivated so only the
        /// framed body remains.
        /// </summary>
        private static void PopulateTooltip(GeoRosterAbilityDetailTooltip tip, List<EventOutcomeRow> rows)
        {
            if ((UnityEngine.Object)(object)tip.AbilityDescription != (UnityEngine.Object)null)
            {
                tip.AbilityDescription.text = ComposeBody(rows);
            }

            SetActiveSafe(tip.AbilityTitleText);
            SetActiveSafe(tip.AbilityIcon);
            SetActiveSafe(tip.AbilitySkillCostGroup);
            SetActiveSafe(tip.AbilitySkillCostText);
            SetActiveSafe(tip.AbilitySkillAPCostText);
            SetActiveSafe(tip.AbilitySkillWPCostText);
            SetActiveSafe(tip.SkillCostHeaderText);

            ResizeToContent(tip);
        }

        // Inset between the framed background edge and the body text, in canvas units. Small but enough
        // to keep the text off the frame's border sprite. These are PADDING, not a fixed box size: the
        // window is sized to (body preferred size + 2*inset), so it always hugs the rows and can never
        // clip the longest native line — only the empty margin the native ability layout reserved (for
        // its now-deactivated title/icon/SP-AP-WP cost groups) is removed.
        private const float PadX = 24f;
        private const float PadY = 18f;

        /// <summary>
        /// Shrink the cloned frame so the box hugs the body text instead of carrying the native ability
        /// tooltip's full fixed footprint (which reserved room for the title/icon/cost groups we deactivate,
        /// leaving the rows in the top-left with large empty space). Sizes the root <see cref="RectTransform"/>
        /// to the description <see cref="Text"/>'s own preferred width/height plus a small inset, and re-anchors
        /// that description to the frame's top-left with the same inset so it stays fully inside the smaller box.
        /// Reads <see cref="Text.preferredWidth"/>/<see cref="Text.preferredHeight"/> (the text generator's
        /// measurement of the CURRENT text with its EXISTING font/fontSize) — font, fontSize, style and scale
        /// are never touched, so the text renders identically; only the surrounding box changes. Guarded so a
        /// measurement hiccup can never break population.
        /// </summary>
        private static void ResizeToContent(GeoRosterAbilityDetailTooltip tip)
        {
            try
            {
                Text body = tip.AbilityDescription;
                if ((UnityEngine.Object)(object)body == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)_rootRt == (UnityEngine.Object)null)
                {
                    return;
                }

                var bodyRt = body.rectTransform;
                if ((UnityEngine.Object)(object)bodyRt == (UnityEngine.Object)null)
                {
                    return;
                }

                // Measured from the current text + existing font (no font/size change). preferredWidth can
                // include the full unwrapped line; clamp to the body's current width if it already wraps so a
                // wrapped paragraph keeps its height rather than ballooning back to one long line.
                float prefW = body.preferredWidth;
                float prefH = body.preferredHeight;
                if (prefW <= 0f || prefH <= 0f)
                {
                    return;
                }

                // Re-anchor the body to the frame's top-left so it sits inside the shrunk box at the inset.
                bodyRt.anchorMin = new Vector2(0f, 1f);
                bodyRt.anchorMax = new Vector2(0f, 1f);
                bodyRt.pivot = new Vector2(0f, 1f);
                bodyRt.sizeDelta = new Vector2(prefW, prefH);
                bodyRt.anchoredPosition = new Vector2(PadX, -PadY);

                // Size the framed root to hug that content (a stretched background child follows the root).
                _rootRt.sizeDelta = new Vector2(prefW + 2f * PadX, prefH + 2f * PadY);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.ResizeToContent failed: " + ex.Message);
            }
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
        /// Read the native reward colours from the live <see cref="UIModuleSiteEncounters"/> so our rows
        /// match the post-choice reward display: positive/negative value colours come from
        /// <c>RewardsColorsDef.PrimaryUIColor</c>/<c>SecondaryUIColor</c> (the same two colours the module
        /// bakes into its Positive/Negative reward text patterns, decompile lines 173-174), and the label
        /// gold comes from the encounter reward-text prefab's own <see cref="Text"/> colour (the native
        /// resource line renders its label uncoloured, so the prefab default IS that gold). Any piece that
        /// can't be read keeps its Fallback* constant. Prefers an active module, then a scene instance.
        /// </summary>
        private static void CaptureNativeRewardColors()
        {
            // Reset to fallbacks so a partial read never leaks a stale value from a prior Show.
            _positiveColor = FallbackPositiveColor;
            _negativeColor = FallbackNegativeColor;
            _labelColor = FallbackLabelColor;
            try
            {
                UIModuleSiteEncounters module = UnityEngine.Object.FindObjectOfType<UIModuleSiteEncounters>();
                if ((UnityEngine.Object)(object)module == (UnityEngine.Object)null)
                {
                    module = Resources.FindObjectsOfTypeAll<UIModuleSiteEncounters>()
                        .FirstOrDefault(m => (UnityEngine.Object)(object)m != (UnityEngine.Object)null
                            && m.gameObject.scene.IsValid());
                }
                if ((UnityEngine.Object)(object)module == (UnityEngine.Object)null)
                {
                    return;
                }

                if ((UnityEngine.Object)(object)module.RewardsColorsDef != (UnityEngine.Object)null)
                {
                    _positiveColor = "#" + ColorUtility.ToHtmlStringRGB(module.RewardsColorsDef.PrimaryUIColor);
                    _negativeColor = "#" + ColorUtility.ToHtmlStringRGB(module.RewardsColorsDef.SecondaryUIColor);
                }

                if ((UnityEngine.Object)(object)module.EncounterRewardTextPrefab != (UnityEngine.Object)null)
                {
                    Text t = module.EncounterRewardTextPrefab.GetComponentInChildren<Text>(true);
                    if ((UnityEngine.Object)(object)t != (UnityEngine.Object)null)
                    {
                        _labelColor = "#" + ColorUtility.ToHtmlStringRGB(t.color);
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.CaptureNativeRewardColors failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Compose the body text: one line per row as "label   value". Every label is already-localized game
        /// text from <see cref="EventOutcomeAdapter"/> — a native resource/item/faction name, or a complete
        /// native reward sentence — so nothing is resolved here. The label is wrapped in the native reward gold
        /// colour and the value in the native positive ("+") / negative ("-") colour; other values (ranges,
        /// "xN", "N%") and name-only rows (incl. the native sentence rows) keep the gold label but leave the
        /// value plain.
        /// </summary>
        private static string ComposeBody(List<EventOutcomeRow> rows)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                EventOutcomeRow row = rows[i];
                string label = row.Label ?? string.Empty;

                if (i > 0)
                {
                    sb.Append('\n');
                }
                sb.Append("<color=").Append(_labelColor).Append('>').Append(label).Append("</color>");
                if (!string.IsNullOrEmpty(row.Value))
                {
                    sb.Append("   ").Append(Colorize(row.Value));
                }
            }
            return sb.ToString();
        }

        /// <summary>Wrap a signed value in the native positive/negative reward colour; leave neutral values plain.</summary>
        private static string Colorize(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }
            if (value[0] == '+')
            {
                return "<color=" + _positiveColor + ">" + value + "</color>";
            }
            if (value[0] == '-')
            {
                return "<color=" + _negativeColor + ">" + value + "</color>";
            }
            return value;
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
