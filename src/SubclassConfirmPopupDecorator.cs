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
        private const float RowGap = 14f;        // gap between the icon row bottom and the question top
        private const float MinWindowWidth = 640f;

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
                if (host.TextRt == null || host.Parent == null)
                {
                    return; // can't find a safe insert point -> show the plain box
                }

                DumpHierarchy(__instance, host); // TEMP diag: measure the real runtime layout

                // Avoid a duplicate row if Show runs twice for the same window.
                Transform existing = host.Parent.Find(RowName);
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
                        ? rootCanvas.transform : host.Parent,
                    dialogSortingOrder,
                    out GameObject tooltipGo);

                // Build the row container: a horizontally-laid, self-sizing strip of large icons. The
                // content panel is NOT a vertical layout group, so we place the row by EXPLICIT anchoring
                // relative to the question text (guaranteed above it + horizontally centered) rather than
                // by sibling order.
                var rowGo = new GameObject(RowName, typeof(RectTransform));
                rowGo.transform.SetParent(host.Parent, false); // same coordinate space as the text

                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = CellSpacing;
                hlg.childAlignment = TextAnchor.MiddleCenter; // center the icons within the row
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;

                // Self-size the row to its icons so the pivot-centered placement stays centered.
                var fitter = rowGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

                // Grow the dialog frame (taller + wider) so the row has room above the text, then anchor
                // the row centered just above the question. Both guarded; failure leaves the native box.
                GrowFrame(host, marker.Perks.Count);
                AnchorRowAboveText(rowGo.GetComponent<RectTransform>(), host.TextRt);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator postfix failed: " + ex.Message);
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
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.ReadMarker failed: " + ex.Message);
                return null;
            }
        }

        // TEMP diag (layout investigation): dump the full ancestor chain of TextContent up to the root
        // Canvas, with RectTransform metrics + layout/sizing/canvas components, so we can build the correct
        // layout from measured truth instead of guessing the prefab. Remove once the layout is finalized.
        private static void DumpHierarchy(MessageBoxPromptController controller, SnapshotTextHost host)
        {
            try
            {
                RectTransform frame = host.Frame;
                Debug.Log("[PerkOracle][diag] hierarchy: === confirm dialog dump START ===");
                Debug.Log("[PerkOracle][diag] hierarchy: FindWindowRect(frame)="
                    + ((UnityEngine.Object)(object)frame != (UnityEngine.Object)null
                        ? (((UnityEngine.Object)frame).name + " rect=" + frame.rect.width + "x" + frame.rect.height)
                        : "<null>"));

                int depth = 0;
                Transform t = host.TextRt;
                while (t != null && depth < 16)
                {
                    Debug.Log("[PerkOracle][diag] hierarchy: [" + depth + "] " + DescribeNode(t));
                    var c = t.GetComponent<Canvas>();
                    if ((UnityEngine.Object)(object)c != (UnityEngine.Object)null)
                    {
                        // Reached a canvas in the chain — note it but keep walking to the very root.
                    }
                    t = t.parent;
                    depth++;
                }

                // The Yes/No buttons live under the controller's Buttons linkers; log the first active one's
                // container so we know where the button row sits relative to the text.
                Transform btnContainer = FindButtonsContainer(controller);
                Debug.Log("[PerkOracle][diag] hierarchy: buttonsContainer="
                    + ((UnityEngine.Object)(object)btnContainer != (UnityEngine.Object)null
                        ? DescribeNode(btnContainer) : "<null>"));
                Debug.Log("[PerkOracle][diag] hierarchy: === confirm dialog dump END ===");
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.DumpHierarchy failed: " + ex.Message);
            }
        }

        private static string DescribeNode(Transform t)
        {
            try
            {
                var go = t.gameObject;
                string s = "'" + go.name + "' active=" + go.activeSelf;
                var rt = t as RectTransform;
                if ((UnityEngine.Object)(object)rt != (UnityEngine.Object)null)
                {
                    s += " | anchMin=" + V(rt.anchorMin) + " anchMax=" + V(rt.anchorMax)
                       + " pivot=" + V(rt.pivot) + " sizeDelta=" + V(rt.sizeDelta)
                       + " anchPos=" + V(rt.anchoredPosition) + " rect=" + rt.rect.width + "x" + rt.rect.height;
                }
                // Layout / sizing components.
                if ((UnityEngine.Object)(object)t.GetComponent<VerticalLayoutGroup>() != (UnityEngine.Object)null) s += " | VLG";
                if ((UnityEngine.Object)(object)t.GetComponent<HorizontalLayoutGroup>() != (UnityEngine.Object)null) s += " | HLG";
                if ((UnityEngine.Object)(object)t.GetComponent<GridLayoutGroup>() != (UnityEngine.Object)null) s += " | GLG";
                var csf = t.GetComponent<ContentSizeFitter>();
                if ((UnityEngine.Object)(object)csf != (UnityEngine.Object)null)
                {
                    s += " | CSF(h=" + csf.horizontalFit + ",v=" + csf.verticalFit + ")";
                }
                var le = t.GetComponent<LayoutElement>();
                if ((UnityEngine.Object)(object)le != (UnityEngine.Object)null)
                {
                    s += " | LE(min=" + le.minWidth + "x" + le.minHeight
                       + ",pref=" + le.preferredWidth + "x" + le.preferredHeight + ")";
                }
                if ((UnityEngine.Object)(object)t.GetComponent<Image>() != (UnityEngine.Object)null) s += " | Image";
                var canvas = t.GetComponent<Canvas>();
                if ((UnityEngine.Object)(object)canvas != (UnityEngine.Object)null)
                {
                    s += " | Canvas(order=" + canvas.sortingOrder + ",render=" + canvas.renderMode
                       + ",override=" + canvas.overrideSorting + ")";
                }
                return s;
            }
            catch (Exception ex)
            {
                return "<describe failed: " + ex.Message + ">";
            }
        }

        private static string V(Vector2 v)
        {
            return "(" + v.x + "," + v.y + ")";
        }

        /// <summary>Find the parent container of the dialog's Yes/No buttons via the controller's Buttons list.</summary>
        private static Transform FindButtonsContainer(MessageBoxPromptController controller)
        {
            try
            {
                var buttonsField = AccessTools.Field(typeof(MessageBoxPromptController), "Buttons");
                var buttons = buttonsField?.GetValue(controller) as System.Collections.IEnumerable;
                if (buttons == null)
                {
                    return null;
                }
                foreach (object linker in buttons)
                {
                    if (linker == null) continue;
                    var btnField = AccessTools.Field(linker.GetType(), "Button");
                    var btn = btnField?.GetValue(linker) as Component;
                    if ((UnityEngine.Object)(object)btn != (UnityEngine.Object)null)
                    {
                        return ((Component)btn).transform.parent; // the buttons row container
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.FindButtonsContainer failed: " + ex.Message);
            }
            return null;
        }

        private struct SnapshotTextHost
        {
            public RectTransform TextRt;
            public Transform Parent;
            public RectTransform Frame;
        }

        /// <summary>Resolve the question-text RectTransform, its parent, and the bounded dialog frame.</summary>
        private static SnapshotTextHost ResolveTextHost(MessageBoxPromptController controller)
        {
            var host = new SnapshotTextHost();
            try
            {
                Component text = controller.TextContent as Component;
                if ((UnityEngine.Object)(object)text != (UnityEngine.Object)null)
                {
                    host.TextRt = ((Component)text).GetComponent<RectTransform>();
                    host.Parent = host.TextRt != null ? host.TextRt.parent : null;
                    host.Frame = FindWindowRect(host.Parent);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.ResolveTextHost failed: " + ex.Message);
            }
            return host;
        }

        /// <summary>
        /// Grow the bounded dialog frame so a large centered icon row fits ABOVE the question without
        /// crowding: widen to at least <see cref="MinWindowWidth"/> (scaled up for many icons) and add the
        /// row's height to the frame. Best-effort; failure leaves the native size.
        /// </summary>
        private static void GrowFrame(SnapshotTextHost host, int iconCount)
        {
            try
            {
                RectTransform frame = host.Frame;
                if ((UnityEngine.Object)(object)frame == (UnityEngine.Object)null)
                {
                    return;
                }

                float neededWidth = Mathf.Max(MinWindowWidth,
                    iconCount * CellSize + Mathf.Max(0, iconCount - 1) * CellSpacing + 80f);
                float extraHeight = CellSize + RowGap + 12f;

                Vector2 sd = frame.sizeDelta;
                // Only adjust axes that are fixed (non-stretched); a stretched axis is driven by anchors.
                bool widthFixed = !(frame.anchorMin.x == 0f && frame.anchorMax.x == 1f);
                bool heightFixed = !(frame.anchorMin.y == 0f && frame.anchorMax.y == 1f);
                float newW = widthFixed ? Mathf.Max(sd.x, neededWidth) : sd.x;
                float newH = heightFixed ? sd.y + extraHeight : sd.y;
                frame.sizeDelta = new Vector2(newW, newH);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.GrowFrame failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Anchor <paramref name="row"/> horizontally CENTERED on the question text and just ABOVE it,
        /// using the text's own measured rect — independent of the prefab's (non-vertical) layout. Pivot is
        /// bottom-center so the row sits on top of the text with a gap.
        /// </summary>
        private static void AnchorRowAboveText(RectTransform row, RectTransform text)
        {
            try
            {
                if ((UnityEngine.Object)(object)row == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)text == (UnityEngine.Object)null)
                {
                    return;
                }
                // Share the text's anchor reference so anchoredPosition is in the same frame.
                row.anchorMin = text.anchorMin;
                row.anchorMax = text.anchorMax;
                row.pivot = new Vector2(0.5f, 0f); // bottom-center: grows upward from its anchored point

                float textTopFromCenter = text.rect.height * (1f - text.pivot.y);
                if (textTopFromCenter <= 0f)
                {
                    textTopFromCenter = 20f; // fallback if the text rect is not laid out yet
                }
                row.anchoredPosition = new Vector2(
                    text.anchoredPosition.x,
                    text.anchoredPosition.y + textTopFromCenter + RowGap);
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.AnchorRowAboveText failed: " + ex.Message);
            }
        }

        /// <summary>Nearest ancestor that looks like the bounded dialog frame (not a full-stretch container).</summary>
        private static RectTransform FindWindowRect(Transform start)
        {
            Transform t = start;
            for (int i = 0; i < 6 && t != null; i++)
            {
                var rt = t as RectTransform;
                if ((UnityEngine.Object)(object)rt != (UnityEngine.Object)null)
                {
                    // A bounded frame has a finite, non-trivial width and is not anchored full-stretch.
                    bool stretched = rt.anchorMin.x == 0f && rt.anchorMax.x == 1f;
                    if (!stretched && rt.rect.width > 1f)
                    {
                        return rt;
                    }
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
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.CreateTooltip failed: " + ex.Message);
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
                Debug.Log("[PerkOracle] ConfirmRowCleanup failed: " + ex.Message);
            }
        }
    }
}
