using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Base.UI.MessageBox.PromptControllers;
using HarmonyLib;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.PerkOracle
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
        private const string RowName = "PerkOracleConfirmPerkRow";
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

                // One tooltip clone for this popup, parented to the root canvas; torn down with the row. Its
                // own overrideSorting Canvas (set inside) renders it above the dialog (dialogSortingOrder+100).
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
                PerkOracleLog.Debug("[PerkOracle] SubclassConfirmPopupDecorator postfix failed: " + ex.Message);
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
                PerkOracleLog.Debug("[PerkOracle] SubclassConfirmPopupDecorator.ReadMarker failed: " + ex.Message);
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
                PerkOracleLog.Debug("[PerkOracle] SubclassConfirmPopupDecorator.ResolveTextHost failed: " + ex.Message);
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
        /// Clone one native ability tooltip parented under <paramref name="parent"/> (canvas-local), hidden
        /// + non-raycasting, for the row's hover triggers. Mirrors PerkWikiPanel's tooltip clone but owned
        /// locally so it never collides with a banner's static instance. Null on failure (icons just lose
        /// their tooltip; the dialog still works).
        /// </summary>
        private static GeoRosterAbilityDetailTooltip CreateTooltip(Transform parent, int dialogSortingOrder,
            out GameObject go)
        {
            go = null;
            try
            {
                var template = UnityEngine.Object.FindObjectsOfType<GeoRosterAbilityDetailTooltip>().FirstOrDefault();
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    template = Resources.FindObjectsOfTypeAll<GeoRosterAbilityDetailTooltip>()
                        .FirstOrDefault(t => (UnityEngine.Object)(object)t != (UnityEngine.Object)null
                            && t.gameObject.scene.IsValid());
                }
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    return null;
                }

                go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
                var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
                go.name = "PerkOracleConfirmTooltip";
                go.transform.localScale = Vector3.one;
                go.transform.SetAsLastSibling(); // above the dialog within the same parent

                // Z-ORDER: the MessageBox overlay (and its prompt window) sorts high; a plain sibling still
                // draws under it. Give the tooltip its OWN sorting context above the dialog's canvas so the
                // ability description renders IN FRONT of the confirm window. No GraphicRaycaster is added
                // (the CanvasGroup already blocks raycasts), so it stays purely visual + non-interactive.
                var tipCanvas = go.GetComponent<Canvas>() ?? go.AddComponent<Canvas>();
                tipCanvas.overrideSorting = true;
                tipCanvas.sortingOrder = dialogSortingOrder + 100;

                go.SetActive(false);
                WikiAbilityTooltipTrigger.ResetPriming();
                return go.GetComponent<GeoRosterAbilityDetailTooltip>();
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] SubclassConfirmPopupDecorator.CreateTooltip failed: " + ex.Message);
                if ((UnityEngine.Object)(object)go != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(go);
                }
                go = null;
                return null;
            }
        }
    }

    /// <summary>Destroys the popup's tooltip clone when the embedded perk row is torn down.</summary>
    internal sealed class ConfirmRowCleanup : MonoBehaviour
    {
        public GameObject TooltipGo;

        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }

        private void Cleanup()
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
                PerkOracleLog.Debug("[PerkOracle] ConfirmRowCleanup failed: " + ex.Message);
            }
        }
    }
}
