using System;
using System.Collections.Generic;
using System.Reflection;
using Base.Core;
using Base.UI.MessageBox.PromptControllers;
using HarmonyLib;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// Marker passed as the native message box's <c>UserData</c> for OUR "take this subclass?" confirm
    /// prompt. Carries the class-perk defs to render. Other game message boxes have different/no UserData,
    /// so the decorator below only ever touches ours.
    /// </summary>
    internal sealed class SubclassConfirmPromptData
    {
        public readonly List<TacticalAbilityDef> Perks;
        public SubclassConfirmPromptData(List<TacticalAbilityDef> perks) { Perks = perks; }
    }

    /// <summary>
    /// Embeds the class-perk icon row INTO the native confirm dialog so the perks and the question read as
    /// one window (instead of a separate floating banner behind the box).
    ///
    /// Seam: POSTFIX on <see cref="MessageBoxPromptController.Show"/>
    /// (Base.UI.MessageBox.PromptControllers/MessageBoxPromptController.cs:62). The live controller is
    /// <c>__instance</c>; the shown <c>data.UserData</c> tags our prompt. We build the icon row with the
    /// existing <see cref="WikiIconFactory.Make"/> (so look + native tooltip match the banner), parent it
    /// into the content panel (the parent of <c>TextContent</c>) ABOVE the question via sibling index, and
    /// best-effort widen the window. Fully guarded: any failure leaves the plain native Yes/No box intact.
    /// </summary>
    [HarmonyPatch(typeof(MessageBoxPromptController), "Show")]
    internal static class SubclassConfirmPopupDecorator
    {
        private const string RowName = "OracleConfirmPerkRow";
        private const float CellSize = 96f;
        private const float CellSpacing = 8f;
        private const float RowGap = 14f;        // vertical padding around the icon row

        // MessageBox.ModalData is an internal type, so we read the controller's shown data + its UserData
        // via reflection (field names verified from the decompile: MessageBoxPromptController._shownData,
        // MessageBox.ModalData.UserData).
        private static readonly FieldInfo ShownDataField =
            AccessTools.Field(typeof(MessageBoxPromptController), "_shownData");

        private static void Postfix(MessageBoxPromptController __instance)
        {
            try
            {
                SubclassConfirmPromptData marker = ReadMarker(__instance);
                if (marker == null || marker.Perks == null || marker.Perks.Count == 0)
                {
                    return; // not our prompt (or nothing to show) -> leave the native box untouched
                }

                // CONTEXT GUARD (defense in depth): the message box is a DontDestroyOnLoad singleton shared
                // with the main menu. Our marker can only be set from the geoscape dual-class flow, but never
                // decorate unless a geoscape level is actually active — so nothing of ours can render in the
                // menu's reuse of this box.
                if ((UnityEngine.Object)(object)(GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>())
                    == (UnityEngine.Object)null)
                {
                    return;
                }

                SnapshotTextHost host = ResolveTextHost(__instance);
                if (host.TextRt == null)
                {
                    return; // no text element -> show the plain box
                }

                // MEASURED layout (hierarchy dump): TextContent's parent 'Content' is a HorizontalLayoutGroup
                // (that's why a sibling row landed to the RIGHT of the text); its grandparent 'Dialog' is a
                // center-anchored VerticalLayoutGroup + ContentSizeFitter(v=PreferredSize) that stacks its
                // children top-to-bottom and AUTO-GROWS its height. So we add the icon row as the FIRST child
                // of the Dialog VLG -> it stacks ABOVE the question and the dialog grows to fit. No manual
                // resizing (that would fight the ContentSizeFitter).
                Transform dialog = FindVerticalLayoutAncestor(host.TextRt);
                if ((UnityEngine.Object)(object)dialog == (UnityEngine.Object)null)
                {
                    return; // no vertical stack found -> leave the native box untouched
                }

                // Avoid a duplicate row if Show runs twice for the same window.
                Transform existing = dialog.Find(RowName);
                if ((UnityEngine.Object)(object)existing != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                // The dialog's own canvas drives its sorting; the tooltip must beat it. Read the nearest
                // canvas for both its sortingOrder (z-order) and its root (tooltip positioning space).
                Canvas dialogCanvas = ((Component)__instance).GetComponentInParent<Canvas>();
                int dialogSortingOrder = (UnityEngine.Object)(object)dialogCanvas != (UnityEngine.Object)null
                    ? dialogCanvas.sortingOrder : 30000;
                Canvas rootCanvas = (UnityEngine.Object)(object)dialogCanvas != (UnityEngine.Object)null
                    ? dialogCanvas.rootCanvas : null;
                RectTransform canvasRect = (UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null
                    ? rootCanvas.transform as RectTransform : null;

                // One tooltip clone for this popup, parented to the root canvas; torn down with the row. A
                // sorting-wrapper Canvas (set inside) renders it above the dialog (dialogSortingOrder+100).
                GeoRosterAbilityDetailTooltip tooltip = CreateTooltip(
                    (UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null
                        ? rootCanvas.transform : dialog,
                    dialogSortingOrder,
                    out GameObject tooltipGo);

                // Build the row: a child of the Dialog VLG, placed at the top (sibling 0 = above the text).
                // The Dialog VLG stretches its children to full inner width (measured: Content/Buttons both
                // 1486.3 wide), so our row spans the dialog and its own HLG(MiddleCenter) centers the icons.
                var rowGo = new GameObject(RowName, typeof(RectTransform));
                rowGo.transform.SetParent(dialog, false);
                rowGo.transform.SetSiblingIndex(0); // top of the vertical stack -> above the question

                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = CellSpacing;
                hlg.childAlignment = TextAnchor.MiddleCenter; // center the large icons across the dialog width
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;

                // Reserve the row's height in the Dialog VLG so it grows the dialog to fit (height only;
                // width is driven by the parent VLG which expands children to full width).
                var rowLe = rowGo.AddComponent<LayoutElement>();
                rowLe.minHeight = CellSize + 2f * RowGap;
                rowLe.preferredHeight = CellSize + 2f * RowGap;

                // Z-ORDER + RAYCAST: as sibling-0 of the Dialog the row would render BEHIND the dialog's
                // background/scrim (icons look dimmed) and stop receiving pointer events (no tooltip). Give
                // the row its OWN sorting context ABOVE the dialog scrim but BELOW the tooltip, plus its own
                // GraphicRaycaster so the icons are hit again. Final chain: dialog bg (130) < icons (180) <
                // tooltip (230). The row keeps its layout position (sibling-0, top), only its draw/raycast
                // context changes — the Dialog VLG + ContentSizeFitter still place + grow it normally.
                var rowCanvas = rowGo.AddComponent<Canvas>();
                rowCanvas.overrideSorting = true;
                rowCanvas.sortingOrder = dialogSortingOrder + 50;
                rowGo.AddComponent<GraphicRaycaster>();

                foreach (TacticalAbilityDef def in marker.Perks)
                {
                    GameObject cell = WikiIconFactory.Make(rowGo.transform, def, tooltip, canvasRect, rootCanvas, null);
                    if ((UnityEngine.Object)(object)cell != (UnityEngine.Object)null)
                    {
                        var le = cell.GetComponent<LayoutElement>() ?? cell.AddComponent<LayoutElement>();
                        le.preferredWidth = CellSize;
                        le.preferredHeight = CellSize;
                        le.minWidth = CellSize;
                        le.minHeight = CellSize;
                    }
                }

                // Tie the tooltip clone's lifetime to the row (row dies when the box SetActive(false)s).
                var cleanup = rowGo.AddComponent<ConfirmRowCleanup>();
                cleanup.TooltipGo = tooltipGo;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SubclassConfirmPopupDecorator postfix failed: " + ex.Message);
            }
        }

        /// <summary>Read OUR marker off the controller's shown ModalData.UserData (reflection). Null if absent.</summary>
        private static SubclassConfirmPromptData ReadMarker(MessageBoxPromptController controller)
        {
            try
            {
                object shownData = ShownDataField?.GetValue(controller);
                if (shownData == null)
                {
                    return null;
                }
                FieldInfo userDataField = AccessTools.Field(shownData.GetType(), "UserData");
                object userData = userDataField?.GetValue(shownData);
                return userData as SubclassConfirmPromptData;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SubclassConfirmPopupDecorator.ReadMarker failed: " + ex.Message);
                return null;
            }
        }

        private struct SnapshotTextHost
        {
            public RectTransform TextRt;
        }

        /// <summary>Resolve the dialog's question-text RectTransform.</summary>
        private static SnapshotTextHost ResolveTextHost(MessageBoxPromptController controller)
        {
            var host = new SnapshotTextHost();
            try
            {
                Component text = controller.TextContent as Component;
                if ((UnityEngine.Object)(object)text != (UnityEngine.Object)null)
                {
                    host.TextRt = ((Component)text).GetComponent<RectTransform>();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SubclassConfirmPopupDecorator.ResolveTextHost failed: " + ex.Message);
            }
            return host;
        }

        /// <summary>
        /// The dialog's vertical-stack container = nearest ancestor of the text that has a
        /// <see cref="VerticalLayoutGroup"/>. Measured hierarchy: 'Snapshot Text' -> 'Content' (HLG) ->
        /// 'Dialog' (VLG + ContentSizeFitter v=PreferredSize) — adding a first child there stacks it above
        /// the question and the dialog auto-grows. Returns null if none found (then we leave the box plain).
        /// </summary>
        private static Transform FindVerticalLayoutAncestor(Transform start)
        {
            Transform t = start != null ? start.parent : null; // skip the text node itself
            for (int i = 0; i < 6 && t != null; i++)
            {
                if ((UnityEngine.Object)(object)t.GetComponent<VerticalLayoutGroup>() != (UnityEngine.Object)null)
                {
                    return t;
                }
                t = t.parent;
            }
            return null;
        }

        /// <summary>
        /// Clone one native ability tooltip for the row's hover triggers, hidden + non-raycasting. The clone
        /// is parented under a dedicated full-screen sorting WRAPPER (which carries the overrideSorting
        /// Canvas), NOT given a Canvas of its own — so its native ContentSizeFitter/layout keeps sizing and
        /// wrapping the description exactly as PerkWikiPanel's working clone does (a Canvas on the tooltip
        /// root breaks that auto-layout → the description overflows the frame). Outputs the wrapper in
        /// <paramref name="go"/> so the row's cleanup tears down wrapper + tooltip together. Null on failure
        /// (icons just lose their tooltip; the dialog still works).
        /// </summary>
        private static GeoRosterAbilityDetailTooltip CreateTooltip(Transform parent, int dialogSortingOrder,
            out GameObject go)
        {
            go = null;
            try
            {
                // Clone a PRISTINE native tooltip, skipping the mod's own repurposed clones
                // (WikiIconFactory.FindNativeTooltipTemplate) — otherwise this popup could pick up
                // EventOutcomeTooltip's title-less, wrap-disabled clone and the perk tooltip would overflow.
                var template = WikiIconFactory.FindNativeTooltipTemplate();
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    return null;
                }

                // Z-ORDER goes on a WRAPPER, never on the tooltip clone itself. The MessageBox overlay
                // sorts high (130), so a plain sibling tooltip draws under it; we need an overrideSorting
                // Canvas to lift it above. But the native tooltip root carries the layout components
                // (ContentSizeFitter/LayoutGroup) that size the framed body to the description and wrap it —
                // adding a Canvas directly onto that root breaks that auto-layout, so the description stops
                // wrapping and overflows the frame to the right. PerkWikiPanel's working clone adds NO Canvas
                // to the tooltip (see PerkWikiPanel.Open comment), and TFTV's recruit overlay sets
                // overrideSorting on an ANCESTOR canvas (GetComponentInParent), never on the tooltip. Mirror
                // that: a dedicated full-screen wrapper owns the sorting Canvas; the tooltip clone stays a
                // pristine direct child of a Canvas boundary — byte-identical topology to the roster clone.
                var layer = new GameObject("OracleConfirmTooltipLayer", typeof(RectTransform));
                layer.transform.SetParent(parent, false);
                var layerRt = layer.GetComponent<RectTransform>();
                layerRt.anchorMin = Vector2.zero;
                layerRt.anchorMax = Vector2.one;
                layerRt.offsetMin = Vector2.zero;
                layerRt.offsetMax = Vector2.zero; // full-stretch: wrapper rect == root-canvas rect (positioning unchanged)
                layer.transform.SetAsLastSibling(); // above the dialog within the same parent

                var layerCanvas = layer.AddComponent<Canvas>();
                layerCanvas.overrideSorting = true;
                layerCanvas.sortingOrder = dialogSortingOrder + 100;

                // The wrapper is what the row's cleanup destroys (tears down the tooltip child with it).
                go = layer;

                var tipGo = UnityEngine.Object.Instantiate(template.gameObject, layer.transform, false);
                var cg = tipGo.GetComponent<CanvasGroup>() ?? tipGo.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false; // purely visual + non-interactive (no GraphicRaycaster on the wrapper)
                cg.interactable = false;
                tipGo.name = "OracleConfirmTooltip";
                tipGo.transform.localScale = Vector3.one;
                tipGo.SetActive(false);

                WikiAbilityTooltipTrigger.ResetPriming();
                return tipGo.GetComponent<GeoRosterAbilityDetailTooltip>();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SubclassConfirmPopupDecorator.CreateTooltip failed: " + ex.Message);
                if ((UnityEngine.Object)(object)go != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(go);
                }
                go = null;
                return null;
            }
        }
    }

    /// <summary>
    /// Owns the lifecycle of the injected perk row + its tooltip clone. CRITICAL: the message box ('Dialog'
    /// under the DontDestroyOnLoad Game / SystemMessageCanvas) is a PERSISTENT singleton reused for prompts
    /// in EVERY scene incl. the main menu (GameUtl.GetMessageBox -> Game which is DontDestroyOnLoad). On
    /// close the box only SetActive(false)s — so without this, our row would survive as a child of the
    /// persistent Dialog and reappear (icons + GraphicRaycaster) on the next menu prompt. We therefore
    /// DESTROY the whole row (and its tooltip) the moment the dialog hides; it is rebuilt fresh next time.
    /// </summary>
    internal sealed class ConfirmRowCleanup : MonoBehaviour
    {
        public GameObject TooltipGo;
        private bool _destroying;

        // Dialog closed (SetActive(false)) -> destroy the row so nothing leaks onto the persistent box.
        private void OnDisable()
        {
            DestroyTooltip();
            if (!_destroying)
            {
                _destroying = true;
                try { UnityEngine.Object.Destroy(((Component)this).gameObject); }
                catch (Exception ex) { OracleLog.Debug("[Oracle] ConfirmRowCleanup destroy failed: " + ex.Message); }
            }
        }

        private void OnDestroy() { DestroyTooltip(); }

        private void DestroyTooltip()
        {
            try
            {
                if ((UnityEngine.Object)(object)TooltipGo != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(TooltipGo);
                    TooltipGo = null;
                    WikiAbilityTooltipTrigger.ResetPriming();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ConfirmRowCleanup failed: " + ex.Message);
            }
        }
    }
}
