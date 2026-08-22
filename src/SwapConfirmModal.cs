using System;
using Base.Core;
using Base.Entities.Abilities;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Utils;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.ViewControllers.Modal;
using PhoenixPoint.Tactical.Entities.Abilities;

namespace Morgott.Oracle
{
    /// <summary>
    /// "Are you sure?" step in front of every wiki perk swap, drawn with the game's OWN confirm-buy
    /// dialog (<see cref="ConfirmBuyAbilityDataBind"/> behind
    /// <c>ModalType.CharacterProgressionConfirmCharacter</c>, the same modal vanilla uses at
    /// <c>UIStateEditSoldier.cs:690-708</c>). Nothing is drawn by the mod and nothing is borrowed from
    /// TFTV, so the popup looks native with or without TFTV installed.
    ///
    /// PRICE DISPLAY: the modal prints <c>data.Ability.CharacterProgressionData.SkillPointCost</c>
    /// (ConfirmBuyAbilityDataBind.cs:65) — a field of the SHARED ability def, not of our data struct. To
    /// show the price we will actually charge it must be overwritten for the lifetime of the popup and
    /// put back afterwards; leaving it overwritten would permanently misprice that ability for normal
    /// purchases. TFTV does the same dance (DrillsUI.Helpers.cs:173-179/199-203/218-221) behind a flag +
    /// catch; here the restore is throw-safe instead: a <c>finally</c> restores immediately if the modal
    /// never opened, and the dialog callback restores as its FIRST statement.
    /// </summary>
    internal static class SwapConfirmModal
    {
        /// <summary>
        /// True while our confirm popup is up. The wiki's Esc/RMB seam
        /// (<see cref="OnCancelInputHandlerPatch"/>) stands down while it is set, so a cancel goes to the
        /// modal (which owns the top of the geoscape state stack) instead of closing the wiki underneath it.
        /// </summary>
        internal static bool Pending;

        /// <summary>
        /// Show the confirmation for replacing <paramref name="ctx"/>'s slot perk with
        /// <paramref name="chosen"/> at <paramref name="spCost"/> skill points, running
        /// <paramref name="onConfirm"/> only if the player confirms. Returns FALSE when the modal could
        /// not be shown (no geoscape view, or the binder's unconditional dereferences
        /// — ConfirmBuyAbilityDataBind.cs:78-80 — would throw); the caller then swaps immediately, exactly
        /// as before this step existed.
        /// </summary>
        public static bool TryShow(PerkSwapContext ctx, TacticalAbilityDef chosen, int spCost, Action onConfirm)
        {
            AbilityCharacterProgressionDef progressionData = null;
            int originalCost = 0;
            bool restored = true;   // nothing overwritten yet
            bool opened = false;
            try
            {
                if (ctx == null || ctx.Character?.Progression == null || ctx.Slot == null
                    || (UnityEngine.Object)(object)chosen == (UnityEngine.Object)null || onConfirm == null)
                {
                    return false;
                }

                // The binder dereferences these with no null checks: Ability.ViewElementDef.LargeIcon /
                // .DisplayName1 (:78-79) and AbilitySlot.Ability (:80). A missing one would throw inside
                // the game's modal state — fall back to the immediate swap instead.
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

                // Overwrite the shared def's price; from here on every exit path must restore it.
                progressionData = chosen.CharacterProgressionData;
                originalCost = chosen.CharacterProgressionData.SkillPointCost;
                chosen.CharacterProgressionData.SkillPointCost = spCost > 0 ? spCost : 0;
                restored = false;

                Pending = true;
                geoView.OpenModal(ModalType.CharacterProgressionConfirmCharacter, res =>
                {
                    Restore(progressionData, originalCost, ref restored); // FIRST statement, always
                    Pending = false;
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
                // Restore NOW only if the popup never opened (the open path restores in its callback, so
                // the price must stay overwritten while the popup is on screen). A throw between the
                // overwrite and OpenModal therefore cannot leave the ability permanently mispriced.
                if (!opened)
                {
                    Pending = false;
                    Restore(progressionData, originalCost, ref restored);
                }
            }
        }

        /// <summary>Put the shared def's price back, exactly once. Guarded — never throws at a caller.</summary>
        private static void Restore(AbilityCharacterProgressionDef progressionData, int originalCost,
            ref bool restored)
        {
            if (restored)
            {
                return;
            }
            restored = true;
            try
            {
                if ((UnityEngine.Object)(object)progressionData != (UnityEngine.Object)null)
                {
                    progressionData.SkillPointCost = originalCost;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] SwapConfirmModal price restore failed: " + ex.Message);
            }
        }
    }
}
