using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
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

        // Class-wiki (3-row native-cell tab panel) layout.
        private const int MaxRowColumns = 10;   // wrap a row onto a new line past this many cells
        private const float RowSpacing = 12f;    // vertical gap between the tabs row / title / sections
        private const float RowLabelHeight = 26f;

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
        /// Build and show the READ-ONLY class wiki for <paramref name="character"/>: a CENTERED panel of
        /// THREE rows of native ability cells — (row 1) every playable class as a clickable TAB (the
        /// soldier's own main/secondary classes lit bright, the rest greyed), (row 2) the selected class's
        /// main ability track, (row 3) its additional/random personal-perk pool (TFTV-aware). Every cell is
        /// a verbatim clone of the game's own <see cref="AbilityTrackSkillEntryElement"/>. Reuses the
        /// single-instance machinery of <see cref="Open"/> (shared tooltip clone, backdrop, <see cref="Close"/>).
        /// Clicking a tab switches rows 2-3 in place (no reopen). View-only (no swap). Self-heals on any error.
        /// </summary>
        public static void OpenClassWiki(Canvas canvas, GeoCharacter character)
        {
            try
            {
                Close();
                if (!OracleMain.EnablePerkWiki)
                {
                    return; // feature disabled -> no panel
                }
                if ((UnityEngine.Object)(object)canvas == (UnityEngine.Object)null
                    || character == null || character.Progression == null)
                {
                    return;
                }
                SpecializationDef mainSpec = character.Progression.MainSpecDef;
                if ((UnityEngine.Object)(object)mainSpec == (UnityEngine.Object)null)
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

                // Backdrop: full-screen transparent button; clicking outside the panel closes the wiki.
                var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
                backdropGo.transform.SetParent(_root.transform, false);
                StretchFull(backdropGo.GetComponent<RectTransform>());
                var backdropImg = backdropGo.GetComponent<Image>();
                ((Graphic)backdropImg).color = new Color(0f, 0f, 0f, 0.35f);
                backdropImg.raycastTarget = true;
                backdropGo.GetComponent<Button>().onClick.AddListener(Close);

                BuildClassTabWiki(_root.transform, character, mainSpec, rootCanvas);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.OpenClassWiki failed: " + ex.Message);
                Close();
            }
        }

        /// <summary>
        /// The centered three-row native-cell panel. Row 1 = class tabs (owned classes bright, others grey,
        /// all clickable); rows 2-3 = the selected class's main track + random pool, rebuilt in place on a tab
        /// switch. The panel auto-sizes to its content (VerticalLayoutGroup + ContentSizeFitter) so each
        /// class's differing row lengths center cleanly. Needs a live native cell to clone; no-op if none is
        /// reachable (the progression screen isn't open). Fully guarded.
        /// </summary>
        private static void BuildClassTabWiki(Transform parent, GeoCharacter character, SpecializationDef mainSpec,
            Canvas rootCanvas)
        {
            AbilityTrackSkillEntryElement template = FindTemplateCell();
            if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
            {
                return; // no native cell to clone -> nothing to show
            }
            float cellSize = MeasureCellSize(template);

            // Owned classes (main + secondary) render bright; the rest greyed. Reference identity.
            var owned = new HashSet<SpecializationDef>();
            foreach (SpecializationDef s in character.Progression.GetSpecializations())
            {
                if ((UnityEngine.Object)(object)s != (UnityEngine.Object)null)
                {
                    owned.Add(s);
                }
            }

            // All playable classes as tabs; make sure the soldier's own classes are present even if the
            // universe omitted them.
            List<SpecializationDef> classes = ClassPerkProvider.GetPlayableClasses();
            foreach (SpecializationDef s in owned)
            {
                if ((UnityEngine.Object)(object)s != (UnityEngine.Object)null && !classes.Contains(s))
                {
                    classes.Insert(0, s);
                }
            }
            if (classes.Count == 0)
            {
                return;
            }

            RectTransform canvasRect = ((UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null)
                ? rootCanvas.transform as RectTransform
                : null;

            // Centered, auto-sizing panel: a VerticalLayoutGroup stacks the tabs row + the (rebuilt) body.
            var panelGo = new GameObject("ClassWikiPanel", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(parent, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panelGo.GetComponent<Image>();
            ((Graphic)panelImg).color = new Color(0f, 0.05f, 0.086f, 0.96f);
            panelImg.raycastTarget = true; // eat clicks so they don't fall through to the backdrop
            var panelVlg = panelGo.GetComponent<VerticalLayoutGroup>();
            panelVlg.spacing = RowSpacing;
            panelVlg.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
            panelVlg.childAlignment = TextAnchor.UpperCenter;
            panelVlg.childControlWidth = true;
            panelVlg.childControlHeight = true;
            panelVlg.childForceExpandWidth = true;  // stretch rows to panel width so each row centers its cells
            panelVlg.childForceExpandHeight = false;
            var panelFitter = panelGo.GetComponent<ContentSizeFitter>();
            panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Row 1 (persistent): the class tabs grid + a per-tab selection marker map.
            var markerMap = new Dictionary<SpecializationDef, GameObject>();
            GameObject tabsRow = BuildRowGrid(panelGo.transform, classes.Count, cellSize);

            // Body (rebuilt on tab switch): holds the class title + rows 2-3.
            var bodyGo = new GameObject("Body",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyVlg = bodyGo.GetComponent<VerticalLayoutGroup>();
            bodyVlg.spacing = RowSpacing;
            bodyVlg.childAlignment = TextAnchor.UpperCenter;
            bodyVlg.childControlWidth = true;
            bodyVlg.childControlHeight = true;
            bodyVlg.childForceExpandWidth = true;
            bodyVlg.childForceExpandHeight = false;
            var bodyFitter = bodyGo.GetComponent<ContentSizeFitter>();
            bodyFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Switch rows 2-3 to `spec` in place and mark its tab selected.
            void SelectTab(SpecializationDef spec)
            {
                try
                {
                    foreach (var kv in markerMap)
                    {
                        if ((UnityEngine.Object)(object)kv.Value != (UnityEngine.Object)null)
                        {
                            kv.Value.SetActive((UnityEngine.Object)(object)kv.Key == (UnityEngine.Object)(object)spec);
                        }
                    }
                    ClearChildren(bodyGo.transform);
                    BuildBodyTitle(bodyGo.transform, ResolveClassTitle(spec));
                    BuildPerkRow(bodyGo.transform, Loc.Get(SectionClassTerm, SectionClassFallback),
                        ClassPerkProvider.GetClassPerks(spec), template, cellSize, canvasRect, rootCanvas);
                    BuildPerkRow(bodyGo.transform, Loc.Get(SectionRandomTerm, SectionRandomFallback),
                        ClassPerkProvider.GetClassRandomPool(spec), template, cellSize, canvasRect, rootCanvas);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] PerkWikiPanel.SelectTab failed: " + ex.Message);
                }
            }

            foreach (SpecializationDef spec in classes)
            {
                BuildClassTab(tabsRow.transform, spec, owned.Contains(spec), template, markerMap, SelectTab);
            }

            SelectTab(mainSpec); // default selected tab = the soldier's own main class
        }

        /// <summary>Create a row container with a wrapping fixed-column <see cref="GridLayoutGroup"/>.</summary>
        private static GameObject BuildRowGrid(Transform parent, int count, float cellSize)
        {
            var gridGo = new GameObject("Row", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(parent, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, Mathf.Min(count, MaxRowColumns));
            grid.childAlignment = TextAnchor.MiddleCenter; // center cells inside the panel-wide row
            return gridGo;
        }

        /// <summary>
        /// One class tab: a native cell clone showing the class icon (bright if owned, else grey), a hidden
        /// selection-marker underline, and a click that switches the wiki to that class. Guarded.
        /// </summary>
        private static void BuildClassTab(Transform parent, SpecializationDef spec, bool bright,
            AbilityTrackSkillEntryElement template, Dictionary<SpecializationDef, GameObject> markerMap,
            Action<SpecializationDef> onSelect)
        {
            try
            {
                Sprite icon = null;
                var view = spec.ViewElementDef;
                if ((UnityEngine.Object)(object)view != (UnityEngine.Object)null)
                {
                    icon = view.SmallIcon != null ? view.SmallIcon : view.LargeIcon;
                }

                GameObject tab = WikiIconFactory.MakeCell(parent, template, icon, null, bright, null, null, null);
                if ((UnityEngine.Object)(object)tab == (UnityEngine.Object)null)
                {
                    return;
                }

                // Selection marker: a thin accent underline, hidden until this tab is the selected one.
                var markGo = new GameObject("SelMark", typeof(RectTransform), typeof(Image));
                markGo.transform.SetParent(tab.transform, false);
                var mrt = markGo.GetComponent<RectTransform>();
                mrt.anchorMin = new Vector2(0f, 0f);
                mrt.anchorMax = new Vector2(1f, 0f);
                mrt.pivot = new Vector2(0.5f, 0f);
                mrt.offsetMin = new Vector2(2f, -2f);
                mrt.offsetMax = new Vector2(-2f, 4f);
                var mimg = markGo.GetComponent<Image>();
                ((Graphic)mimg).color = new Color(0.36f, 0.72f, 1f, 1f);
                mimg.raycastTarget = false;
                markGo.SetActive(false);
                markerMap[spec] = markGo;

                var btn = tab.GetComponent<Button>();
                if ((UnityEngine.Object)(object)btn != (UnityEngine.Object)null)
                {
                    SpecializationDef captured = spec;
                    btn.onClick.AddListener(() => onSelect(captured));
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.BuildClassTab failed: " + ex.Message);
            }
        }

        /// <summary>Centered class-name title above rows 2-3 (rebuilt on tab switch). Guarded.</summary>
        private static void BuildBodyTitle(Transform parent, string titleText)
        {
            try
            {
                var go = new GameObject("ClassTitle",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                var text = go.GetComponent<Text>();
                text.text = titleText;
                text.font = GetTitleFont();
                text.fontSize = 22;
                ((Graphic)text).color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                var le = go.GetComponent<LayoutElement>();
                le.minHeight = TitleHeight;
                le.preferredHeight = TitleHeight;
                le.flexibleHeight = 0f;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.BuildBodyTitle failed: " + ex.Message);
            }
        }

        /// <summary>
        /// One labeled perk row: a centered header label plus a wrapping grid of native ability cells (bright
        /// "known" look, native hover tooltip, read-only — swapContext null). Skipped when <paramref name="defs"/>
        /// is empty. Guarded.
        /// </summary>
        private static void BuildPerkRow(Transform parent, string label, List<TacticalAbilityDef> defs,
            AbilityTrackSkillEntryElement template, float cellSize, RectTransform canvasRect, Canvas rootCanvas)
        {
            if (defs == null || defs.Count == 0)
            {
                return;
            }
            try
            {
                var headerGo = new GameObject("RowLabel",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
                headerGo.transform.SetParent(parent, false);
                var htext = headerGo.GetComponent<Text>();
                htext.text = label;
                htext.font = GetTitleFont();
                htext.fontSize = 18;
                ((Graphic)htext).color = new Color(0.72f, 0.85f, 1f, 1f);
                htext.alignment = TextAnchor.MiddleCenter;
                htext.horizontalOverflow = HorizontalWrapMode.Overflow;
                htext.verticalOverflow = VerticalWrapMode.Overflow;
                htext.raycastTarget = false;
                var hle = headerGo.GetComponent<LayoutElement>();
                hle.minHeight = RowLabelHeight;
                hle.preferredHeight = RowLabelHeight;
                hle.flexibleHeight = 0f;

                GameObject grid = BuildRowGrid(parent, defs.Count, cellSize);
                foreach (TacticalAbilityDef def in defs)
                {
                    if ((UnityEngine.Object)(object)def == (UnityEngine.Object)null
                        || (UnityEngine.Object)(object)def.ViewElementDef == (UnityEngine.Object)null)
                    {
                        continue;
                    }
                    Sprite icon = def.ViewElementDef.SmallIcon != null
                        ? def.ViewElementDef.SmallIcon
                        : def.ViewElementDef.LargeIcon;
                    WikiIconFactory.MakeCell(grid.transform, template, icon, def, true,
                        _tooltip, canvasRect, rootCanvas);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.BuildPerkRow failed: " + ex.Message);
            }
        }

        /// <summary>Detach + destroy every child of <paramref name="t"/> so a rebuild starts clean.</summary>
        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform c = t.GetChild(i);
                c.SetParent(null, false); // detach now so sibling order is clean before the deferred Destroy
                UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        /// <summary>
        /// Find a live native <see cref="AbilityTrackSkillEntryElement"/> in the open progression screen to
        /// clone (any active cell with a SkillIcon, excluding our own clones). Returns null if none is active.
        /// </summary>
        private static AbilityTrackSkillEntryElement FindTemplateCell()
        {
            try
            {
                foreach (AbilityTrackSkillEntryElement c in
                         UnityEngine.Object.FindObjectsOfType<AbilityTrackSkillEntryElement>())
                {
                    if ((UnityEngine.Object)(object)c != (UnityEngine.Object)null
                        && (UnityEngine.Object)(object)c.SkillIcon != (UnityEngine.Object)null
                        && !((Component)c).gameObject.name.StartsWith(
                            WikiIconFactory.CloneNamePrefix, StringComparison.Ordinal))
                    {
                        return c;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkWikiPanel.FindTemplateCell failed: " + ex.Message);
            }
            return null;
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
