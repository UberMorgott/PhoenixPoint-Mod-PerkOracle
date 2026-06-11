using System;
using System.Collections.Generic;
using Base.Core;
using PhoenixPoint.Geoscape.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// A lightweight framed tooltip that lists outcome-preview rows (label + value) next to the hovered
    /// event choice. One live instance owned statically: <see cref="Show"/> builds (or rebuilds) it under
    /// the supplied root canvas; <see cref="Hide"/> tears it down. Styled to match the mod's wiki panel
    /// (dark translucent frame, game Phoenixpedia font). Every public entry point is wrapped in try/catch +
    /// <see cref="OracleLog"/> so a UI hiccup can never throw back into the event screen. Mirrors the
    /// try/catch + cursor->canvas-local positioning of <see cref="WikiAbilityTooltipTrigger"/>.
    /// </summary>
    public static class EventOutcomeTooltip
    {
        private const float Width = 320f;
        private const float RowHeight = 26f;
        private const float Padding = 12f;
        private const float CursorXOffset = 18f;

        private static GameObject _root;
        private static RectTransform _rootRt;
        private static RectTransform _canvasRect;
        private static Canvas _rootCanvas;
        private static Font _font;

        /// <summary>True while a tooltip instance is live.</summary>
        public static bool IsShown => (UnityEngine.Object)(object)_root != (UnityEngine.Object)null;

        /// <summary>
        /// Build + show the tooltip for <paramref name="rows"/> parented to <paramref name="anchorCanvas"/>'s
        /// root canvas, positioned at the current mouse position. No-op (and hides any prior instance) when
        /// rows is null/empty or the canvas is missing. Localizes any label that is an <c>ORACLE_*</c> key.
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

                float panelHeight = rows.Count * RowHeight + 2f * Padding;

                _root = new GameObject("OracleEventOutcomeTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                _root.transform.SetParent(rootParent, false);
                _rootRt = _root.GetComponent<RectTransform>();
                _rootRt.anchorMin = new Vector2(0.5f, 0.5f);
                _rootRt.anchorMax = new Vector2(0.5f, 0.5f);
                _rootRt.pivot = new Vector2(0f, 1f); // top-left pivot so it grows down-right from the cursor
                _rootRt.sizeDelta = new Vector2(Width, panelHeight);

                var bg = _root.GetComponent<Image>();
                ((Graphic)bg).color = new Color(0f, 0.05f, 0.086f, 0.96f); // matches PerkWikiPanel
                bg.raycastTarget = false;

                // Never intercept pointer events (mirrors PerkWikiPanel tooltip clone's blocksRaycasts=false
                // flicker fix): the tooltip must not steal the hover from the choice button beneath it.
                var cg = _root.GetComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                var layout = _root.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
                layout.spacing = 0f;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = true;

                foreach (EventOutcomeRow row in rows)
                {
                    BuildRow(_root.transform, row);
                }

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
        /// Build one "label .... value" line. A label that begins with "ORACLE_" is treated as a loc key
        /// and resolved via <see cref="Loc"/> (the pure formatter emits raw keys for fixed scalar rows);
        /// any other label is already-localized text from <see cref="EventOutcomeAdapter"/>.
        /// </summary>
        private static void BuildRow(Transform parent, EventOutcomeRow row)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(parent, false);
            var hl = rowGo.GetComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.spacing = 8f;

            var le = rowGo.AddComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;

            string label = row.Label ?? string.Empty;
            if (label.StartsWith("ORACLE_"))
            {
                label = Loc.Get(label, FallbackLabel(label));
            }

            MakeText(rowGo.transform, label, TextAnchor.MiddleLeft, flexibleWidth: 1f);
            if (!string.IsNullOrEmpty(row.Value))
            {
                MakeText(rowGo.transform, row.Value, TextAnchor.MiddleRight, flexibleWidth: 0f);
            }
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

        private static void MakeText(Transform parent, string content, TextAnchor anchor, float flexibleWidth)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = GetFont();
            text.fontSize = 16;
            ((Graphic)text).color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = flexibleWidth;
        }

        /// <summary>Position the tooltip's top-left near the cursor (mirrors WikiAbilityTooltipTrigger.Position).</summary>
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

        /// <summary>The game's Phoenixpedia entry-title font (cached); Arial fallback. Mirrors PerkWikiPanel.GetTitleFont.</summary>
        private static Font GetFont()
        {
            if ((UnityEngine.Object)(object)_font != (UnityEngine.Object)null)
            {
                return _font;
            }
            try
            {
                Font native = GameUtl.CurrentLevel()
                    .GetComponent<GeoLevelController>()
                    .View.GeoscapeModules.PhoenixpediaModule.EntryTitle.font;
                if ((UnityEngine.Object)(object)native != (UnityEngine.Object)null)
                {
                    _font = native;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeTooltip.GetFont failed: " + ex.Message);
            }
            if ((UnityEngine.Object)(object)_font == (UnityEngine.Object)null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _font;
        }
    }
}
