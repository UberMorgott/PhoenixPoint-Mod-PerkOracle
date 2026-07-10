using System;
using Base.Core;
using Base.UI.MessageBox;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Morgott.Oracle
{
    /// <summary>
    /// Hover trigger on a wiki perk icon: on pointer-enter it shows the game's RICH framed ability
    /// tooltip (<see cref="GeoRosterAbilityDetailTooltip"/>) -- the same background/framed tooltip the
    /// game uses for normal skills -- positioned near the cursor; on pointer-exit it hides it.
    ///
    /// The tooltip instance itself is a single clone owned by <see cref="PerkWikiPanel"/> (created on
    /// Open, destroyed on Close) and shared across all icon triggers. Everything is wrapped in try/catch
    /// so a UI hiccup can never throw into the game's event system.
    /// </summary>
    public sealed class WikiAbilityTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>The ability this icon represents; its name/description/icon fill the tooltip.</summary>
        public TacticalAbilityDef Def;

        /// <summary>
        /// Swap context for this icon (soldier/slot/module). Null when the wiki was opened without a
        /// swap target (then left-click does nothing). Shared across all icons of one wiki instance.
        /// </summary>
        public PerkSwapContext SwapContext;

        /// <summary>Shared native tooltip clone (owned by the panel). Null disables the trigger.</summary>
        public GeoRosterAbilityDetailTooltip Tooltip;

        /// <summary>RectTransform of the root canvas, used to map the cursor into canvas-local space.</summary>
        public RectTransform CanvasRect;

        /// <summary>Root canvas, used only to pick the right camera for the screen->local mapping.</summary>
        public Canvas RootCanvas;

        /// <summary>Vertical offset above the cursor, in canvas units. In-game tunable.</summary>
        private const float CursorYOffset = 24f;

        // Defensive: the very first Show after a clone can lay out mis-positioned. Prime once
        // (Show->Hide->Show) the first time any trigger shows a tooltip for the CURRENT tooltip clone.
        // The clone is recreated on every PerkWikiPanel.Open/Close, so this must be reset per panel
        // (see ResetPriming, called from PerkWikiPanel.Close) -- otherwise a later panel's fresh clone
        // is never re-primed and its first tooltip can lay out mis-positioned.
        private static bool _primed;

        /// <summary>Re-arm priming for the next tooltip clone. Call when the shared clone is destroyed.</summary>
        public static void ResetPriming()
        {
            _primed = false;
        }

        /// <summary>Wire the shared tooltip + canvas references (and optional swap context) into this trigger.</summary>
        public void Init(TacticalAbilityDef def, GeoRosterAbilityDetailTooltip tooltip, RectTransform canvasRect,
            Canvas rootCanvas, PerkSwapContext swapContext)
        {
            Def = def;
            Tooltip = tooltip;
            CanvasRect = canvasRect;
            RootCanvas = rootCanvas;
            SwapContext = swapContext;
        }

        /// <summary>
        /// Left-click a candidate icon: if the swap feature is enabled and a swap context is present,
        /// replace the soldier's perk in this slot with <see cref="Def"/> and close the wiki. All gating
        /// (toggle, learned, same, already-owned) is enforced downstream; a denied/failed swap leaves
        /// the wiki open. Wrapped so a UI hiccup never throws into the event system.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                {
                    return; // only left-click swaps; RMB/Esc routing closes the wiki elsewhere
                }
                if (!OracleMain.AllowPerkSwap || SwapContext == null || Def == null)
                {
                    return;
                }

                // Research gate (pure verdict surfaced here): when the "require research" toggle is on and
                // the soldier's faction has not completed the perk-swap research, deny with a localized
                // on-screen-equivalent feedback message and leave the wiki open (no swap, no close).
                if (PerkSwapResearch.GateVerdict(OracleMain.RequirePerkSwapResearch, SwapContext.Character)
                    == PerkSwapVerdict.DenyResearchLocked)
                {
                    string msg = Loc.Get(
                        "ORACLE_SwapResearchLocked",
                        "Perk reassignment requires the \"Operative Reconditioning\" research.");
                    OracleLog.Debug("[Oracle] PerkSwap denied (research locked): " + msg);
                    ShowDenyMessage(msg);
                    return; // wiki stays open so the player sees the candidates again
                }

                GeoRosterAbilityDetailTooltip tip = Tooltip;
                if ((UnityEngine.Object)(object)tip != (UnityEngine.Object)null)
                {
                    tip.Hide(); // drop the hover tooltip before the grid is torn down/rebuilt
                }

                if (PerkSwapper.TrySwap(SwapContext, Def))
                {
                    PerkWikiPanel.Close();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiAbilityTooltipTrigger.OnPointerClick failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Surface a transient, visible "can't do that" notice via the game's native modal message box —
        /// the same mechanism the base game uses for simple notices (e.g. SerializationCommands.cs:202 uses
        /// <c>GameUtl.GetMessageBox().ShowSimplePrompt(..., MessageBoxButtons.OK, ...)</c>). An info-icon
        /// OK-only prompt fits this geoscape roster context and needs no UI plumbing of our own. Fully
        /// wrapped + null-guarded: <see cref="GameUtl.GetMessageBox"/> returns null early in startup
        /// (GameUtl.cs:114), and a UI hiccup must never throw back into the click handler.
        /// </summary>
        private static void ShowDenyMessage(string msg)
        {
            try
            {
                MessageBox box = GameUtl.GetMessageBox();
                if ((UnityEngine.Object)(object)box != (UnityEngine.Object)null)
                {
                    box.ShowSimplePrompt(msg, MessageBoxIcon.Information, MessageBoxButtons.OK, callback: null);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiAbilityTooltipTrigger.ShowDenyMessage failed: " + ex.Message);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            try
            {
                GeoRosterAbilityDetailTooltip tip = Tooltip;
                if ((UnityEngine.Object)(object)tip == (UnityEngine.Object)null || Def == null
                    || (UnityEngine.Object)(object)Def.ViewElementDef == (UnityEngine.Object)null)
                {
                    return;
                }

                // TacticalAbilityDef overload; cost <= 0 hides the SP cost row. Pass ViewElementDef
                // explicitly because Show() dereferences `view` for title/description/icon.
                tip.Show(Def, Def.ViewElementDef, false, 0);

                if (!_primed)
                {
                    _primed = true;
                    tip.Hide();
                    tip.Show(Def, Def.ViewElementDef, false, 0);
                }

                // Z-ORDER at SHOW time. The tooltip rides an overrideSorting WRAPPER (see
                // PerkWikiPanel.CreateTooltipClone / SubclassConfirmPopupDecorator.CreateTooltip). Its
                // sortingOrder is snapshotted when the wrapper is built, but the host surface can finalize
                // its own canvas order LATER: the native message box is raised onto its DontDestroyOnLoad
                // system canvas via a view-state + WindowShowEvent (and a deferred Update frame) AFTER our
                // decorate-time Postfix, so an early snapshot goes stale and the tooltip renders BEHIND the
                // confirm window. Re-stamp the hard TooltipSortingOrder constant onto the wrapper on EVERY
                // show (the wrapper — never the tooltip root — carries the Canvas, so ContentSizeFitter
                // word-wrap is untouched; the wrapper rect is unchanged, so Position() math holds) so a
                // later-activated modal on the DontDestroyOnLoad SystemMessageCanvas can never outsort it.
                // Mirrors TFTV's HavenRecruitAbilityTooltipTrigger, which likewise fixes its ancestor canvas.
                Canvas layerCanvas = tip.GetComponentInParent<Canvas>();
                if ((UnityEngine.Object)(object)layerCanvas != (UnityEngine.Object)null && layerCanvas.overrideSorting)
                {
                    layerCanvas.sortingOrder = WikiIconFactory.TooltipSortingOrder;
                }
                tip.transform.SetAsLastSibling();
                Position(eventData);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiAbilityTooltipTrigger.OnPointerEnter failed: " + ex.Message);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            try
            {
                GeoRosterAbilityDetailTooltip tip = Tooltip;
                if ((UnityEngine.Object)(object)tip != (UnityEngine.Object)null)
                {
                    tip.Hide();
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiAbilityTooltipTrigger.OnPointerExit failed: " + ex.Message);
            }
        }

        private void OnDisable()
        {
            // If the icon is torn down while hovered, make sure the shared tooltip doesn't linger.
            try
            {
                GeoRosterAbilityDetailTooltip tip = Tooltip;
                if ((UnityEngine.Object)(object)tip != (UnityEngine.Object)null)
                {
                    tip.Hide();
                }
            }
            catch
            {
                // Teardown path; nothing actionable.
            }
        }

        private void Position(PointerEventData eventData)
        {
            try
            {
                GeoRosterAbilityDetailTooltip tip = Tooltip;
                if ((UnityEngine.Object)(object)tip == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)CanvasRect == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)RootCanvas == (UnityEngine.Object)null)
                {
                    return;
                }

                Camera cam = RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : RootCanvas.worldCamera;
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, eventData.position, cam, out local))
                {
                    var tipRt = tip.transform as RectTransform;
                    if ((UnityEngine.Object)(object)tipRt != (UnityEngine.Object)null)
                    {
                        tipRt.anchoredPosition = local + new Vector2(0f, CursorYOffset);
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiAbilityTooltipTrigger.Position failed: " + ex.Message);
            }
        }
    }
}
