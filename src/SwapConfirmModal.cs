using System;
using System.Globalization;
using Base.Core;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Utils;
using PhoenixPoint.Common.View.ViewControllers;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.ViewControllers.Modal;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// "Are you sure?" step in front of every wiki perk swap, drawn with the game's OWN confirm-buy
    /// dialog (<see cref="ConfirmBuyAbilityDataBind"/> behind
    /// <c>ModalType.CharacterProgressionConfirmCharacter</c>, the same modal vanilla uses at
    /// <c>UIStateEditSoldier.cs:690-708</c>). Nothing is drawn by the mod and nothing is borrowed from
    /// TFTV, so the popup looks native with or without TFTV installed.
    ///
    /// PRICE DISPLAY: the binder prints <c>data.Ability.CharacterProgressionData.SkillPointCost</c>
    /// (ConfirmBuyAbilityDataBind.cs:65) — a field of the SHARED ability def. We do NOT overwrite it.
    /// The value is read exactly once, in <c>ModalShowHandler</c>, straight into
    /// <c>SPCostText.text</c> (:77), so <see cref="PriceLabelPatch"/> simply re-writes that one label
    /// afterwards, using the binder's own <c>SpTextPattern</c>. The shared def is therefore never
    /// mutated and there is no restore path to get wrong — no scene unload, stack replacement,
    /// destroyed modal or disabled mod can leave an ability permanently mispriced.
    /// (TFTV mutates the def instead — DrillsUI.Helpers.cs:173-179/199-203/218-221 — and needs a flag
    /// plus a catch to put it back.)
    /// </summary>
    internal static class SwapConfirmModal
    {
        private static bool _pending;
        private static TacticalAbilityDef _ability;   // the def OUR popup is showing
        private static int _cost;                     // SP we will actually charge
        private static UIModal _modal;                // captured once the popup is really on screen

        /// <summary>
        /// True while our confirm popup is up. The wiki's Esc/RMB seam
        /// (<see cref="OnCancelInputHandlerPatch"/>) stands down while it is set, so a cancel goes to the
        /// modal (which owns the top of the geoscape state stack) instead of closing the wiki underneath it.
        ///
        /// SELF-HEALING: once the popup has actually been shown we hold its <see cref="UIModal"/>, so a
        /// read that finds it hidden or destroyed clears the flag on the spot. A modal that goes away
        /// without our callback running (scene unload, stack teardown) therefore cannot leave cancel
        /// input swallowed forever.
        /// </summary>
        internal static bool Pending
        {
            get
            {
                // (object) test = "we captured a modal"; the Unity test = "...and it has been destroyed".
                if (_pending && (object)_modal != null
                    && ((UnityEngine.Object)_modal == (UnityEngine.Object)null || !_modal.IsShown()))
                {
                    Clear();
                }
                return _pending;
            }
        }

        /// <summary>Forget the pending popup. Idempotent, never throws.</summary>
        internal static void Clear()
        {
            _pending = false;
            _ability = null;
            _modal = null;
        }

        /// <summary>
        /// Show the confirmation for replacing <paramref name="ctx"/>'s slot perk with
        /// <paramref name="chosen"/> at <paramref name="spCost"/> skill points, running
        /// <paramref name="onConfirm"/> only if the player confirms. Returns FALSE when the modal could
        /// not be shown (no geoscape view, or the binder's unconditional dereferences
        /// — ConfirmBuyAbilityDataBind.cs:65/78-80 — would throw); the caller then swaps immediately,
        /// exactly as before this step existed.
        /// </summary>
        public static bool TryShow(PerkSwapContext ctx, TacticalAbilityDef chosen, int spCost, Action onConfirm)
        {
            bool opened = false;
            try
            {
                if (ctx == null || ctx.Character?.Progression == null || ctx.Slot == null
                    || (UnityEngine.Object)(object)chosen == (UnityEngine.Object)null || onConfirm == null)
                {
                    return false;
                }

                // The binder dereferences these with no null checks: Ability.CharacterProgressionData
                // (:65), Ability.ViewElementDef.LargeIcon / .DisplayName1 (:78-79) and
                // AbilitySlot.Ability (:80). A missing one would throw inside the game's modal state —
                // fall back to the immediate swap instead.
                var view = chosen.ViewElementDef;
                if ((UnityEngine.Object)(object)view == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)view.LargeIcon == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)ctx.Slot.Ability == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)chosen.CharacterProgressionData == (UnityEngine.Object)null)
                {
                    return false;
                }

                GeoscapeView geoView = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>()?.View;
                if ((UnityEngine.Object)(object)geoView == (UnityEngine.Object)null)
                {
                    return false;
                }

                var data = new ConfirmBuyAbilityDataBind.Data
                {
                    Progression = ctx.Character.Progression,
                    AbilitySlot = ctx.Slot,
                    Ability = chosen,
                    CostType = ResourceType.None,
                };

                _ability = chosen;
                _cost = spCost > 0 ? spCost : 0;
                _modal = null;
                _pending = true;
                geoView.OpenModal(ModalType.CharacterProgressionConfirmCharacter, res =>
                {
                    Clear();
                    if (res == ModalResult.Confirm)
                    {
                        onConfirm();
                    }
                }, data, 100, forceOnTop: true, replaceTop: false);
                opened = true;
                return true;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SwapConfirmModal.TryShow failed: " + ex.Message);
                return false;
            }
            finally
            {
                if (!opened)
                {
                    Clear();
                }
            }
        }

        /// <summary>
        /// True when <paramref name="modal"/> is the popup we opened, i.e. its data carries the very def
        /// we are pending on. Reference identity, so another mod's confirm-buy popup is never touched.
        /// </summary>
        private static bool Owns(UIModal modal, out int cost)
        {
            cost = _cost;
            if (!_pending || modal == null || !(modal.Data is ConfirmBuyAbilityDataBind.Data data))
            {
                return false;
            }
            return (object)data.Ability != null && ReferenceEquals(data.Ability, _ability);
        }

        /// <summary>
        /// Re-label the SP cost of OUR confirm popup right after the binder wrote the shared def's price
        /// into it (ConfirmBuyAbilityDataBind.cs:65/77). Keeps the def untouched; uses the binder's own
        /// <c>SpTextPattern</c> so the colouring stays native. Any other confirm-buy popup is left alone.
        /// </summary>
        [HarmonyPatch(typeof(ConfirmBuyAbilityDataBind), "ModalShowHandler")]
        internal static class PriceLabelPatch
        {
            private static void Postfix(ConfirmBuyAbilityDataBind __instance, UIModal modal)
            {
                try
                {
                    if (!Owns(modal, out int cost))
                    {
                        return;
                    }
                    _modal = modal; // from here on Pending can verify the popup is still on screen
                    Text label = __instance.SPCostText;
                    if ((UnityEngine.Object)(object)label != (UnityEngine.Object)null)
                    {
                        label.text = string.Format(__instance.SpTextPattern,
                            cost.ToString(CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] SwapConfirmModal price label failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// The popup went away (UIModal.Hide, reached from UIStateGeoModal.ExitState:120 on EVERY exit
        /// path). Second, callback-independent clear of <see cref="Pending"/>.
        /// </summary>
        [HarmonyPatch(typeof(ConfirmBuyAbilityDataBind), "ModalHideHandler")]
        internal static class HidePatch
        {
            private static void Postfix()
            {
                Clear();
            }
        }
    }
}
