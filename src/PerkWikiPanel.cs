using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// A self-contained UGUI overlay listing candidate perk icons in a scrollable grid. Owns a single
    /// live instance: Open builds+shows it on top of the screen's canvas; Close destroys it. Clicking
    /// the transparent backdrop (outside the panel) also closes it. Depends on nothing in TFTV.
    /// </summary>
    public static class PerkWikiPanel
    {
        private const int Columns = 6;
        private const float CellSize = 110f;
        private const float CellSpacing = 8f;
        private const float Padding = 16f;
        private const float MaxPanelHeight = 620f;
        private const float TitleHeight = 44f;

        // I2 term for the wiki title; the literal is the English fallback when the term is missing.
        private const string TitleTerm = "PERKORACLE_WIKI_TITLE";
        private const string TitleFallback = "POSSIBLE SKILLS";

        private static GameObject _root;
        private static Font _titleFont;

        // Single native ability-tooltip clone, owned by the panel: created in Open, destroyed in Close.
        // Shared by every icon's WikiAbilityTooltipTrigger so there's exactly one live tooltip.
        private static GameObject _tooltipGo;
        private static GeoRosterAbilityDetailTooltip _tooltip;
        private static Canvas _rootCanvas;

        public static bool IsOpen => (UnityEngine.Object)(object)_root != (UnityEngine.Object)null;

        /// <summary>
        /// Build and show the wiki for <paramref name="defs"/> parented to <paramref name="canvas"/>.
        /// Rebuilds from scratch (idempotent): any prior instance is closed first. No-op if inputs are
        /// null/empty. Swallows and logs all errors so it can never break the host screen.
        /// </summary>
        public static void Open(Canvas canvas, List<TacticalAbilityDef> defs, PerkSwapContext swapContext = null,
            string titleTerm = TitleTerm, string titleFallback = TitleFallback)
        {
            try
            {
                Close();
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null || defs == null || defs.Count == 0)
                {
                    return;
                }

                // Ride the OUTERMOST canvas (the same one UITooltipText/TTUtil.GetRootCanvas parents
                // tooltips to). We do NOT add our own Canvas/overrideSorting/GraphicRaycaster: that
                // would (1) render the native ability tooltip behind us and (2) add a fresh raycaster
                // that delays pointer-enter under a stationary cursor. The root canvas already has a
                // GraphicRaycaster covering our descendants, so our backdrop button still gets clicks.
                Canvas rootCanvas = canvas.rootCanvas;
                Transform rootParent = ((UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null)
                    ? rootCanvas.transform
                    : canvas.transform;
                _rootCanvas = rootCanvas;

                // Clone ONE native ability tooltip onto the root canvas (canvas-local positioning).
                // Shared by all icon triggers; destroyed in Close. Non-fatal if the template is absent.
                CreateTooltipClone(rootParent);

                _root = new GameObject("RolledPerkWiki", typeof(RectTransform));
                _root.transform.SetParent(rootParent, false);
                StretchFull(_root.GetComponent<RectTransform>());
                // Draw above the progression cells. The native tooltip is cloned onto the same root
                // canvas LATER, so as a later sibling it renders above this panel (fixes z-order).
                _root.transform.SetAsLastSibling();

                // Backdrop: full-screen transparent button; clicking it (outside the panel) closes.
                var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
                backdropGo.transform.SetParent(_root.transform, false);
                StretchFull(backdropGo.GetComponent<RectTransform>());
                var backdropImg = backdropGo.GetComponent<Image>();
                ((Graphic)backdropImg).color = new Color(0f, 0f, 0f, 0.35f);
                backdropImg.raycastTarget = true;
                backdropGo.GetComponent<Button>().onClick.AddListener(Close);

                BuildPanel(_root.transform, defs, rootCanvas, swapContext, titleTerm, titleFallback);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] PerkWikiPanel.Open failed: " + ex.Message);
                Close();
            }
        }

        /// <summary>Destroy the live instance, if any. Safe to call when nothing is open.</summary>
        public static void Close()
        {
            if ((UnityEngine.Object)(object)_tooltipGo != (UnityEngine.Object)null)
            {
                UnityEngine.Object.Destroy(_tooltipGo);
            }
            _tooltipGo = null;
            _tooltip = null;
            _rootCanvas = null;
            // The shared tooltip clone is now destroyed; re-arm priming so the next panel's fresh clone
            // gets primed on its first hover (the static flag would otherwise stay true for the process).
            WikiAbilityTooltipTrigger.ResetPriming();

            if ((UnityEngine.Object)(object)_root != (UnityEngine.Object)null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
        }

        private static void BuildPanel(Transform parent, List<TacticalAbilityDef> defs, Canvas rootCanvas,
            PerkSwapContext swapContext, string titleTerm, string titleFallback)
        {
            // Try to resolve a live native cell to clone (so cells look exactly like the game's). If found,
            // measure its on-screen size and render native clones; otherwise fall back to custom icons.
            AbilityTrackSkillEntryElement template = ResolveTemplateCell(swapContext);
            AbilityTrackSlot slot = swapContext != null ? swapContext.Slot : null;
            bool useNative = (UnityEngine.Object)(object)template != (UnityEngine.Object)null && slot != null;
            float cellSize = useNative ? MeasureCellSize(template) : CellSize;

            // Centered panel sized to the column count; height clamped, content scrolls if it overflows.
            int rows = Mathf.CeilToInt(defs.Count / (float)Columns);
            float gridWidth = Columns * cellSize + (Columns - 1) * CellSpacing;
            float gridHeight = rows * cellSize + Mathf.Max(0, rows - 1) * CellSpacing;
            float panelWidth = gridWidth + 2f * Padding;
            float viewportHeight = Mathf.Min(gridHeight, MaxPanelHeight) + 2f * Padding;
            float panelHeight = viewportHeight + TitleHeight;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panelGo.GetComponent<Image>();
            ((Graphic)panelImg).color = new Color(0f, 0.05f, 0.086f, 0.96f);
            panelImg.raycastTarget = true; // eat clicks so they don't fall through to the backdrop

            // Fixed title strip across the panel top (does NOT scroll with the grid below it).
            BuildTitle(panelGo.transform, titleTerm, titleFallback);

            // Viewport (mask) + scrollable content holding the grid; sits BELOW the title strip.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(panelGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(Padding, Padding);
            viewportRt.offsetMax = new Vector2(-Padding, -TitleHeight); // leave room for the title strip
            var viewportImg = viewportGo.GetComponent<Image>();
            ((Graphic)viewportImg).color = new Color(1f, 1f, 1f, 0f);
            viewportImg.raycastTarget = true;

            var scroll = panelGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewportRt;
            scroll.scrollSensitivity = 30f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            scroll.content = contentRt;

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Canvas RectTransform for cursor->local mapping inside the triggers.
            RectTransform canvasRect = ((UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null)
                ? rootCanvas.transform as RectTransform
                : null;

            foreach (TacticalAbilityDef def in defs)
            {
                if (useNative)
                {
                    WikiIconFactory.MakeNative(contentGo.transform, def, template, slot,
                        _tooltip, canvasRect, rootCanvas, swapContext);
                }
                else
                {
                    WikiIconFactory.Make(contentGo.transform, def, _tooltip, canvasRect, rootCanvas, swapContext);
                }
            }
        }

        /// <summary>
        /// Find a live native <see cref="AbilityTrackSkillEntryElement"/> from the progression module to use
        /// as a clone template. Prefers a Personal cell that currently shows an ability (closest match to the
        /// candidate cells); falls back to any cell. Returns null if none is reachable.
        /// </summary>
        private static AbilityTrackSkillEntryElement ResolveTemplateCell(PerkSwapContext swapContext)
        {
            try
            {
                if (swapContext == null
                    || (UnityEngine.Object)(object)swapContext.Module == (UnityEngine.Object)null)
                {
                    return null;
                }

                AbilityTrackSkillEntryElement[] cells =
                    swapContext.Module.GetComponentsInChildren<AbilityTrackSkillEntryElement>(true);
                if (cells == null || cells.Length == 0)
                {
                    return null;
                }

                // Prefer a Personal cell with a real ability already shown.
                AbilityTrackSkillEntryElement best = cells.FirstOrDefault(c =>
                    (UnityEngine.Object)(object)c != (UnityEngine.Object)null
                    && c.TrackSource == AbilityTrackSource.Personal
                    && (UnityEngine.Object)(object)c.AbilityDef != (UnityEngine.Object)null);
                if ((UnityEngine.Object)(object)best != (UnityEngine.Object)null)
                {
                    return best;
                }

                return cells.FirstOrDefault(c => (UnityEngine.Object)(object)c != (UnityEngine.Object)null);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] PerkWikiPanel.ResolveTemplateCell failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Measure a native cell's on-screen square size from its <see cref="RectTransform"/>. Falls back to
        /// the constant <see cref="CellSize"/> if the rect has not been laid out (width &lt;= 0).
        /// </summary>
        private static float MeasureCellSize(AbilityTrackSkillEntryElement template)
        {
            try
            {
                var rt = ((Component)template).GetComponent<RectTransform>();
                if ((UnityEngine.Object)(object)rt != (UnityEngine.Object)null)
                {
                    float w = rt.rect.width;
                    float h = rt.rect.height;
                    float size = Mathf.Max(w, h);
                    if (size > 1f)
                    {
                        return size;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] PerkWikiPanel.MeasureCellSize failed: " + ex.Message);
            }
            return CellSize;
        }

        /// <summary>
        /// Instantiate a single live <see cref="GeoRosterAbilityDetailTooltip"/> from any in-scene
        /// template, parented under the root canvas (so positioning math is canvas-local). Scaled to
        /// Vector3.one (full size). Stored in <see cref="_tooltip"/>; torn down in <see cref="Close"/>.
        /// Fully guarded: a missing template just leaves icons tooltip-less, never breaks the panel.
        /// </summary>
        private static void CreateTooltipClone(Transform rootParent)
        {
            try
            {
                var template = UnityEngine.Object.FindObjectsOfType<GeoRosterAbilityDetailTooltip>().FirstOrDefault();
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    return;
                }

                _tooltipGo = UnityEngine.Object.Instantiate(template.gameObject, rootParent, false);
                var tipCg = _tooltipGo.GetComponent<CanvasGroup>() ?? _tooltipGo.AddComponent<CanvasGroup>();
                tipCg.blocksRaycasts = false;
                tipCg.interactable = false;
                _tooltipGo.name = "RolledPerkWikiAbilityTooltip";
                _tooltipGo.transform.localScale = Vector3.one; // full size (NOT 0.5)
                _tooltipGo.SetActive(false);
                _tooltip = _tooltipGo.GetComponent<GeoRosterAbilityDetailTooltip>();
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] PerkWikiPanel.CreateTooltipClone failed: " + ex.Message);
                _tooltipGo = null;
                _tooltip = null;
            }
        }

        /// <summary>
        /// Fixed (non-scrolling) title bar pinned to the top of the panel. A thin dark header bar
        /// behind a centered game-styled label. Fully guarded; failure leaves the panel intact.
        /// </summary>
        private static void BuildTitle(Transform panel, string titleTerm, string titleFallback)
        {
            try
            {
                // Thin dark header bar behind the title to match the game's framed look.
                var barGo = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
                barGo.transform.SetParent(panel, false);
                var barRt = barGo.GetComponent<RectTransform>();
                barRt.anchorMin = new Vector2(0f, 1f);
                barRt.anchorMax = new Vector2(1f, 1f);
                barRt.pivot = new Vector2(0.5f, 1f);
                barRt.offsetMin = new Vector2(0f, -TitleHeight);
                barRt.offsetMax = new Vector2(0f, 0f);
                var barImg = barGo.GetComponent<Image>();
                ((Graphic)barImg).color = new Color(0.086f, 0.133f, 0.165f, 1f); // ~#16222a
                barImg.raycastTarget = true; // part of the panel; eat clicks

                var textGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGo.transform.SetParent(barGo.transform, false);
                StretchFull(textGo.GetComponent<RectTransform>());
                var text = textGo.GetComponent<Text>();
                text.text = Loc.Get(titleTerm, titleFallback);
                text.font = GetTitleFont();
                text.fontSize = 22;
                ((Graphic)text).color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.supportRichText = true;
                text.raycastTarget = false;
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] PerkWikiPanel.BuildTitle failed: " + ex.Message);
            }
        }

        /// <summary>The game's Phoenixpedia entry-title font, cached; Arial fallback if unavailable.</summary>
        private static Font GetTitleFont()
        {
            if ((UnityEngine.Object)(object)_titleFont != (UnityEngine.Object)null)
            {
                return _titleFont;
            }
            try
            {
                Font native = GameUtl.CurrentLevel()
                    .GetComponent<GeoLevelController>()
                    .View.GeoscapeModules.PhoenixpediaModule.EntryTitle.font;
                if ((UnityEngine.Object)(object)native != (UnityEngine.Object)null)
                {
                    _titleFont = native;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] PerkWikiPanel.GetTitleFont failed: " + ex.Message);
            }
            if ((UnityEngine.Object)(object)_titleFont == (UnityEngine.Object)null)
            {
                _titleFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _titleFont;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
