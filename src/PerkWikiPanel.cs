using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.Oracle
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
        private const string TitleTerm = "ORACLE_WIKI_TITLE";
        private const string TitleFallback = "POSSIBLE SKILLS";

        // Section headers for the two-section class wiki (I2 terms + English fallbacks).
        private const string SectionClassTerm = "ORACLE_WIKI_SECTION_CLASS";
        private const string SectionClassFallback = "CLASS ABILITIES";
        private const string SectionRandomTerm = "ORACLE_WIKI_SECTION_RANDOM";
        private const string SectionRandomFallback = "RANDOM PERKS";

        // Left class-strip layout.
        private const float StripIconSize = 56f;
        private const float StripSpacing = 6f;
        private const float StripPadding = 8f;
        private const float StripLeftMargin = 24f;

        // Two-section panel layout.
        private const float SectionHeaderHeight = 30f;
        private const float SectionSpacing = 12f;

        private static GameObject _root;
        private static Font _titleFont;

        // Single native ability-tooltip clone, owned by the panel: created in Open, destroyed in Close.
        // Shared by every icon's WikiAbilityTooltipTrigger so there's exactly one live tooltip.
        private static GameObject _tooltipGo;
        // Full-screen sorting wrapper that parents the tooltip clone; its overrideSorting Canvas keeps the
        // tooltip topmost regardless of sibling order (destroying it also destroys the clone child).
        private static GameObject _tooltipLayer;
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
                if (!OracleMain.EnablePerkWiki)
                {
                    return; // feature disabled -> no panel; every open path routes through here
                }
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null || defs == null || defs.Count == 0)
                {
                    return;
                }

                // The PANEL rides the OUTERMOST canvas (same one UITooltipText/TTUtil.GetRootCanvas
                // parents tooltips to) and deliberately gets NO Canvas/overrideSorting/GraphicRaycaster
                // of its OWN: that would (1) render the native ability tooltip behind us and (2) add a
                // fresh raycaster that delays pointer-enter under a stationary cursor. The root canvas
                // already has a GraphicRaycaster covering our descendants, so the backdrop still clicks.
                Canvas rootCanvas = canvas.rootCanvas;
                Transform rootParent = ((UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null)
                    ? rootCanvas.transform
                    : canvas.transform;
                _rootCanvas = rootCanvas;

                // Clone ONE native ability tooltip, shared by all icon triggers (destroyed in Close;
                // non-fatal if the template is absent). It lives under a full-screen sorting WRAPPER with
                // overrideSorting (the wrapper carries the Canvas, NEVER the tooltip root — a Canvas there
                // breaks the native ContentSizeFitter word-wrap), mirroring TFTV's recruit overlay
                // (overrideSorting on an ANCESTOR canvas, never on the tooltip). Its sortingOrder is a hard
                // constant that sits above EVERY UI surface (see WikiIconFactory.TooltipSortingOrder — a
                // single ancestor-canvas order is unreliable); same constant as SubclassConfirmPopupDecorator
                // => identical z-behavior on both surfaces.
                CreateTooltipClone(rootParent, WikiIconFactory.TooltipSortingOrder);

                _root = new GameObject("RolledPerkWiki", typeof(RectTransform));
                _root.transform.SetParent(rootParent, false);
                StretchFull(_root.GetComponent<RectTransform>());
                // Keep the panel above sibling content of the root canvas (plain last-sibling). The
                // tooltip's own overrideSorting wrapper (created above) keeps it above THIS panel in turn.
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
                OracleLog.Debug("[Oracle] PerkWikiPanel.Open failed: " + ex.Message);
                Close();
            }
        }

        /// <summary>
        /// Build and show the READ-ONLY class wiki for <paramref name="spec"/>: a LEFT strip of every
        /// playable class plus a centered two-section popup — (1) the class ability track and (2) the
        /// random personal-perk pool (TFTV-aware). Reuses the single-instance machinery of
        /// <see cref="Open"/> (shared tooltip clone, backdrop, <see cref="Close"/>); the strip and the
        /// popup are both children of the one root, so the backdrop click (or any Close) tears down BOTH.
        /// Clicking a strip icon re-enters here for that class (close+reopen). No swap in this mode
        /// (view-only, swapContext null). No-op / self-heals on any error.
        /// </summary>
        public static void OpenClassWiki(Canvas canvas, SpecializationDef spec)
        {
            try
            {
                Close();
                if (!OracleMain.EnablePerkWiki)
                {
                    return; // feature disabled -> no panel
                }
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)spec == (UnityEngine.Object)null)
                {
                    return;
                }

                Canvas rootCanvas = canvas.rootCanvas;
                Transform rootParent = ((UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null)
                    ? rootCanvas.transform
                    : canvas.transform;
                _rootCanvas = rootCanvas;

                // Shared native ability tooltip (same overrideSorting wrapper as the swap wiki).
                CreateTooltipClone(rootParent, WikiIconFactory.TooltipSortingOrder);

                _root = new GameObject("ClassWiki", typeof(RectTransform));
                _root.transform.SetParent(rootParent, false);
                StretchFull(_root.GetComponent<RectTransform>());
                _root.transform.SetAsLastSibling();

                // Backdrop: full-screen transparent button; clicking outside the strip/popup closes both.
                var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
                backdropGo.transform.SetParent(_root.transform, false);
                StretchFull(backdropGo.GetComponent<RectTransform>());
                var backdropImg = backdropGo.GetComponent<Image>();
                ((Graphic)backdropImg).color = new Color(0f, 0f, 0f, 0.35f);
                backdropImg.raycastTarget = true;
                backdropGo.GetComponent<Button>().onClick.AddListener(Close);

                BuildClassStrip(_root.transform, canvas, spec);
                BuildClassWikiPanel(_root.transform, spec, rootCanvas);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.OpenClassWiki failed: " + ex.Message);
                Close();
            }
        }

        /// <summary>
        /// Left, vertically-centered strip of every playable class icon (the currently-viewed class shown
        /// at full brightness, the rest dimmed). Each icon is a button that re-opens the class wiki for
        /// that class. The current class is always included even if the universe omitted it. Guarded.
        /// </summary>
        private static void BuildClassStrip(Transform parent, Canvas canvas, SpecializationDef current)
        {
            try
            {
                List<SpecializationDef> classes = ClassPerkProvider.GetPlayableClasses();
                if ((UnityEngine.Object)(object)current != (UnityEngine.Object)null && !classes.Contains(current))
                {
                    classes.Insert(0, current); // always show the class we're viewing
                }
                if (classes.Count == 0)
                {
                    return;
                }

                var stripGo = new GameObject("ClassStrip",
                    typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                stripGo.transform.SetParent(parent, false);
                var rt = stripGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(StripLeftMargin, 0f);
                var bg = stripGo.GetComponent<Image>();
                ((Graphic)bg).color = new Color(0f, 0.05f, 0.086f, 0.92f);
                bg.raycastTarget = true; // eat clicks so the strip background never falls through to close

                var vlg = stripGo.GetComponent<VerticalLayoutGroup>();
                vlg.spacing = StripSpacing;
                vlg.padding = new RectOffset((int)StripPadding, (int)StripPadding, (int)StripPadding, (int)StripPadding);
                vlg.childAlignment = TextAnchor.MiddleCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;

                var fitter = stripGo.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                foreach (SpecializationDef spec in classes)
                {
                    bool isCurrent = (UnityEngine.Object)(object)spec == (UnityEngine.Object)(object)current;
                    BuildClassStripIcon(stripGo.transform, spec, canvas, isCurrent);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.BuildClassStrip failed: " + ex.Message);
            }
        }

        /// <summary>One class-strip icon: fixed-size sprite button that re-opens the wiki for its class.</summary>
        private static void BuildClassStripIcon(Transform parent, SpecializationDef spec, Canvas canvas, bool isCurrent)
        {
            try
            {
                var view = spec.ViewElementDef;
                Sprite sprite = null;
                if ((UnityEngine.Object)(object)view != (UnityEngine.Object)null)
                {
                    sprite = view.SmallIcon != null ? view.SmallIcon : view.LargeIcon;
                }

                var go = new GameObject("ClassIcon",
                    typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = true;
                // Dim the classes you're not viewing so the current one reads as selected.
                ((Graphic)img).color = isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.5f);

                var le = go.GetComponent<LayoutElement>();
                le.minWidth = StripIconSize;
                le.preferredWidth = StripIconSize;
                le.minHeight = StripIconSize;
                le.preferredHeight = StripIconSize;

                SpecializationDef captured = spec;
                Canvas capturedCanvas = canvas;
                go.GetComponent<Button>().onClick.AddListener(() => OpenClassWiki(capturedCanvas, captured));
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.BuildClassStripIcon failed: " + ex.Message);
            }
        }

        /// <summary>
        /// The centered two-section popup: a class-name title, then the class ability track and the random
        /// personal-perk pool, each under its own labeled header. Mirrors <see cref="BuildPanel"/>'s
        /// scroll/viewport scaffolding but stacks the two sections with a VerticalLayoutGroup instead of one
        /// flat grid. Read-only: icons carry the native hover tooltip, no swap (swapContext null).
        /// </summary>
        private static void BuildClassWikiPanel(Transform parent, SpecializationDef spec, Canvas rootCanvas)
        {
            List<TacticalAbilityDef> classPerks = ClassPerkProvider.GetClassPerks(spec);
            List<TacticalAbilityDef> randomPerks = ClassPerkProvider.GetClassRandomPool(spec);

            int rows1 = classPerks.Count > 0 ? Mathf.CeilToInt(classPerks.Count / (float)Columns) : 0;
            int rows2 = randomPerks.Count > 0 ? Mathf.CeilToInt(randomPerks.Count / (float)Columns) : 0;
            int sectionCount = (rows1 > 0 ? 1 : 0) + (rows2 > 0 ? 1 : 0);
            float gridWidth = Columns * CellSize + (Columns - 1) * CellSpacing;
            float gridH1 = rows1 > 0 ? rows1 * CellSize + (rows1 - 1) * CellSpacing : 0f;
            float gridH2 = rows2 > 0 ? rows2 * CellSize + (rows2 - 1) * CellSpacing : 0f;
            // content children = sectionCount*(header+grid); VLG puts SectionSpacing between every child.
            int childCount = sectionCount * 2;
            float contentHeight = gridH1 + gridH2 + sectionCount * SectionHeaderHeight
                + Mathf.Max(0, childCount - 1) * SectionSpacing;

            float panelWidth = gridWidth + 2f * Padding;
            float viewportHeight = Mathf.Min(contentHeight, MaxPanelHeight) + 2f * Padding;
            float panelHeight = viewportHeight + TitleHeight;

            var panelGo = new GameObject("ClassWikiPanel", typeof(RectTransform), typeof(Image));
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

            BuildTitleText(panelGo.transform, ResolveClassTitle(spec));

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(panelGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(Padding, Padding);
            viewportRt.offsetMax = new Vector2(-Padding, -TitleHeight);
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

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = SectionSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform canvasRect = ((UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null)
                ? rootCanvas.transform as RectTransform
                : null;

            BuildClassSection(contentGo.transform, Loc.Get(SectionClassTerm, SectionClassFallback),
                classPerks, rootCanvas, canvasRect);
            BuildClassSection(contentGo.transform, Loc.Get(SectionRandomTerm, SectionRandomFallback),
                randomPerks, rootCanvas, canvasRect);
        }

        /// <summary>
        /// One labeled section inside the class wiki content column: a header label plus a fixed-column grid
        /// of read-only perk icons (native hover tooltip, no swap). Skipped when <paramref name="defs"/> is
        /// empty. The grid's own GridLayoutGroup reports its preferred height to the parent layout.
        /// </summary>
        private static void BuildClassSection(Transform content, string headerText,
            List<TacticalAbilityDef> defs, Canvas rootCanvas, RectTransform canvasRect)
        {
            if (defs == null || defs.Count == 0)
            {
                return;
            }

            var headerGo = new GameObject("SectionHeader",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            headerGo.transform.SetParent(content, false);
            var htext = headerGo.GetComponent<Text>();
            htext.text = headerText;
            htext.font = GetTitleFont();
            htext.fontSize = 18;
            ((Graphic)htext).color = new Color(0.72f, 0.85f, 1f, 1f);
            htext.alignment = TextAnchor.LowerLeft;
            htext.horizontalOverflow = HorizontalWrapMode.Overflow;
            htext.verticalOverflow = VerticalWrapMode.Overflow;
            htext.raycastTarget = false;
            var hle = headerGo.GetComponent<LayoutElement>();
            hle.minHeight = SectionHeaderHeight;
            hle.preferredHeight = SectionHeaderHeight;
            hle.flexibleHeight = 0f;

            var gridGo = new GameObject("SectionGrid", typeof(RectTransform));
            gridGo.transform.SetParent(content, false);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellSize, CellSize);
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment = TextAnchor.UpperLeft;

            foreach (TacticalAbilityDef def in defs)
            {
                WikiIconFactory.Make(gridGo.transform, def, _tooltip, canvasRect, rootCanvas, null);
            }
        }

        /// <summary>Localized class display name for the popup title; falls back to the class name.</summary>
        private static string ResolveClassTitle(SpecializationDef spec)
        {
            try
            {
                var view = spec.ViewElementDef;
                if ((UnityEngine.Object)(object)view != (UnityEngine.Object)null && view.DisplayName1 != null)
                {
                    string s = view.DisplayName1.Localize();
                    if (!string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.ResolveClassTitle failed: " + ex.Message);
            }
            return (UnityEngine.Object)(object)spec.ClassTag != (UnityEngine.Object)null
                ? spec.ClassTag.className
                : Loc.Get(TitleTerm, TitleFallback);
        }

        /// <summary>Destroy the live instance, if any. Safe to call when nothing is open.</summary>
        public static void Close()
        {
            // Destroying the wrapper tears down the tooltip clone it parents; fall back to the clone
            // itself if the wrapper is somehow absent.
            if ((UnityEngine.Object)(object)_tooltipLayer != (UnityEngine.Object)null)
            {
                UnityEngine.Object.Destroy(_tooltipLayer);
            }
            else if ((UnityEngine.Object)(object)_tooltipGo != (UnityEngine.Object)null)
            {
                UnityEngine.Object.Destroy(_tooltipGo);
            }
            _tooltipLayer = null;
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
                OracleLog.Debug("[Oracle] PerkWikiPanel.ResolveTemplateCell failed: " + ex.Message);
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
                OracleLog.Debug("[Oracle] PerkWikiPanel.MeasureCellSize failed: " + ex.Message);
            }
            return CellSize;
        }

        /// <summary>
        /// Instantiate a single live <see cref="GeoRosterAbilityDetailTooltip"/> from any in-scene
        /// template, parented under a full-screen sorting WRAPPER (own overrideSorting Canvas at
        /// <paramref name="sortingOrder"/>) so the tooltip always draws above the host surface. The Canvas
        /// lives on the wrapper, NEVER on the tooltip root — a Canvas there breaks the native
        /// ContentSizeFitter word-wrap. The wrapper is full-stretch (its rect == the root-canvas rect), so
        /// the trigger's canvas-local positioning math is unchanged. Scaled to Vector3.one (full size).
        /// Wrapper stored in <see cref="_tooltipLayer"/>, clone in <see cref="_tooltip"/>; both torn down in
        /// <see cref="Close"/>. Fully guarded: a missing template just leaves icons tooltip-less.
        /// </summary>
        private static void CreateTooltipClone(Transform rootParent, int sortingOrder)
        {
            try
            {
                // Clone a PRISTINE native tooltip. Must skip the mod's own repurposed clones (see
                // WikiIconFactory.FindNativeTooltipTemplate): inside the dual-class picker the progression
                // tooltip GO is inactive, so a plain lookup could otherwise pick EventOutcomeTooltip's
                // title-less, wrap-disabled clone and the class-perk tooltip would overflow / look custom.
                var template = WikiIconFactory.FindNativeTooltipTemplate();
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    return;
                }

                // Full-screen sorting wrapper: overrideSorting lifts the tooltip above the panel, the modal
                // and any nested sorting canvas. Full-stretch => wrapper rect == root-canvas rect, so the
                // trigger keeps positioning the clone in the same (canvas-local) space.
                var layer = new GameObject("RolledPerkWikiTooltipLayer", typeof(RectTransform));
                layer.transform.SetParent(rootParent, false);
                var layerRt = layer.GetComponent<RectTransform>();
                layerRt.anchorMin = Vector2.zero;
                layerRt.anchorMax = Vector2.one;
                layerRt.offsetMin = Vector2.zero;
                layerRt.offsetMax = Vector2.zero;
                var layerCanvas = layer.AddComponent<Canvas>();
                layerCanvas.overrideSorting = true;
                layerCanvas.sortingOrder = sortingOrder;
                _tooltipLayer = layer;

                _tooltipGo = UnityEngine.Object.Instantiate(template.gameObject, layer.transform, false);
                var tipCg = _tooltipGo.GetComponent<CanvasGroup>() ?? _tooltipGo.AddComponent<CanvasGroup>();
                tipCg.blocksRaycasts = false; // purely visual; no GraphicRaycaster on the wrapper
                tipCg.interactable = false;
                _tooltipGo.name = "RolledPerkWikiAbilityTooltip";
                _tooltipGo.transform.localScale = Vector3.one; // full size (NOT 0.5)
                _tooltipGo.SetActive(false);
                _tooltip = _tooltipGo.GetComponent<GeoRosterAbilityDetailTooltip>();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.CreateTooltipClone failed: " + ex.Message);
                if ((UnityEngine.Object)(object)_tooltipLayer != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(_tooltipLayer);
                }
                _tooltipLayer = null;
                _tooltipGo = null;
                _tooltip = null;
            }
        }

        /// <summary>
        /// Fixed (non-scrolling) title bar pinned to the top of the panel. A thin dark header bar
        /// behind a centered game-styled label. Fully guarded; failure leaves the panel intact.
        /// </summary>
        private static void BuildTitle(Transform panel, string titleTerm, string titleFallback)
            => BuildTitleText(panel, Loc.Get(titleTerm, titleFallback));

        /// <summary>Same fixed title bar as <see cref="BuildTitle"/> but for an already-resolved string.</summary>
        private static void BuildTitleText(Transform panel, string titleText)
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
                text.text = titleText;
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
                OracleLog.Debug("[Oracle] PerkWikiPanel.BuildTitle failed: " + ex.Message);
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
                OracleLog.Debug("[Oracle] PerkWikiPanel.GetTitleFont failed: " + ex.Message);
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
