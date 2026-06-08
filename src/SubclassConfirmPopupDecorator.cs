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
        private const float CellSize = 64f;
        private const float CellSpacing = 6f;
        private const float MinWindowWidth = 560f;

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

                // Avoid a duplicate row if Show runs twice for the same window.
                Transform existing = host.Parent.Find(RowName);
                if ((UnityEngine.Object)(object)existing != (UnityEngine.Object)null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                // Root canvas for tooltip positioning (popup lives on the message-box canvas).
                Canvas rootCanvas = ((Component)__instance).GetComponentInParent<Canvas>();
                rootCanvas = (UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null
                    ? rootCanvas.rootCanvas : null;
                RectTransform canvasRect = (UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null
                    ? rootCanvas.transform as RectTransform : null;

                // One tooltip clone for this popup, parented to the root canvas; torn down with the row.
                GeoRosterAbilityDetailTooltip tooltip = CreateTooltip(
                    (UnityEngine.Object)(object)rootCanvas != (UnityEngine.Object)null
                        ? rootCanvas.transform : host.Parent,
                    out GameObject tooltipGo);

                // Build the row container: a horizontally-laid, self-sizing strip placed above the text.
                var rowGo = new GameObject(RowName, typeof(RectTransform));
                rowGo.transform.SetParent(host.Parent, false);
                rowGo.transform.SetSiblingIndex(host.TextRt.GetSiblingIndex()); // directly above the question

                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = CellSpacing;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;

                var fitter = rowGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Reserve height in the (assumed vertical) content layout so the text is pushed down.
                var rowLe = rowGo.AddComponent<LayoutElement>();
                rowLe.minHeight = CellSize;
                rowLe.preferredHeight = CellSize;

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

                // Best-effort widen so the icon row fits harmoniously (guarded; never required for correctness).
                TryWidenWindow(__instance, host);
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

        private struct SnapshotTextHost
        {
            public RectTransform TextRt;
            public Transform Parent;
        }

        /// <summary>Resolve the dialog's question-text RectTransform + its parent content panel.</summary>
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
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.ResolveTextHost failed: " + ex.Message);
            }
            return host;
        }

        /// <summary>
        /// Best-effort widening: walk up from the content panel to the nearest ancestor with a sized
        /// (non-stretched) RectTransform — the dialog frame — and raise its min width via a LayoutElement
        /// (or sizeDelta). Wrapped: a failure just leaves the native width.
        /// </summary>
        private static void TryWidenWindow(MessageBoxPromptController controller, SnapshotTextHost host)
        {
            try
            {
                RectTransform window = FindWindowRect(host.Parent);
                if ((UnityEngine.Object)(object)window == (UnityEngine.Object)null)
                {
                    return;
                }
                if (window.rect.width >= MinWindowWidth)
                {
                    return; // already wide enough
                }
                var le = window.GetComponent<LayoutElement>();
                if ((UnityEngine.Object)(object)le != (UnityEngine.Object)null)
                {
                    le.minWidth = Mathf.Max(le.minWidth, MinWindowWidth);
                    le.preferredWidth = Mathf.Max(le.preferredWidth, MinWindowWidth);
                }
                else
                {
                    // No layout element: nudge sizeDelta width directly (works for a fixed-size frame).
                    Vector2 sd = window.sizeDelta;
                    if (sd.x > 0f && sd.x < MinWindowWidth)
                    {
                        window.sizeDelta = new Vector2(MinWindowWidth, sd.y);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] SubclassConfirmPopupDecorator.TryWidenWindow failed: " + ex.Message);
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
        private static GeoRosterAbilityDetailTooltip CreateTooltip(Transform parent, out GameObject go)
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
                go.transform.SetAsLastSibling(); // above the dialog
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
