using System;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Builds one candidate cell for the wiki grid by CLONING a live native
    /// <see cref="AbilityTrackSkillEntryElement"/> so the wiki matches the game's own progression UI.
    /// The clone is driven into the bright "owned/known" state via the game's own
    /// <c>SetSkill</c>, its native text tooltip + native click action are disabled, and our
    /// <see cref="WikiAbilityTooltipTrigger"/> is attached for the rich framed tooltip + left-click swap.
    /// </summary>
    public static class WikiIconFactory
    {
        /// <summary>
        /// Name prefix stamped on every cloned wiki cell. Used by the rolled-perk highlight postfix
        /// (<c>SetSkillStatePatch</c>) to early-return so wiki clones are never tinted as progression cells.
        /// </summary>
        public const string CloneNamePrefix = "RolledPerkWikiCell";

        /// <summary>
        /// Create a candidate cell under <paramref name="parent"/> for <paramref name="def"/> by cloning
        /// <paramref name="template"/> (a live native cell). <paramref name="slot"/> is the REAL personal
        /// slot of the opened level (same slot the swap targets) — it is stored on the clone so the cell's
        /// internals (and our swap) reference a valid slot. Returns null on failure or missing inputs.
        /// </summary>
        public static GameObject MakeNative(Transform parent, TacticalAbilityDef def,
            AbilityTrackSkillEntryElement template, AbilityTrackSlot slot,
            GeoRosterAbilityDetailTooltip tooltip, RectTransform canvasRect, Canvas rootCanvas,
            PerkSwapContext swapContext)
        {
            if ((UnityEngine.Object)(object)parent == (UnityEngine.Object)null || def == null
                || (UnityEngine.Object)(object)template == (UnityEngine.Object)null || slot == null
                || (UnityEngine.Object)(object)def.ViewElementDef == (UnityEngine.Object)null)
            {
                return null;
            }

            try
            {
                GameObject cloneGo = UnityEngine.Object.Instantiate(
                    ((Component)template).gameObject, parent, false);

                // Stamp the name BEFORE SetSkill (which runs SetSkillState -> our highlight postfix) so the
                // guard can recognize this as a wiki clone and skip tinting it.
                cloneGo.name = CloneNamePrefix;

                var cell = cloneGo.GetComponent<AbilityTrackSkillEntryElement>();
                if ((UnityEngine.Object)(object)cell == (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(cloneGo);
                    return null;
                }

                // Neutralize the native click BEFORE driving state: the native OnPointerClick is gated by
                // IsBuyableSkill (false for an owned cell) so it never fires, but clear the delegate and the
                // Button's listeners defensively so a clone can never trigger the game's buy/learn action.
                cell.TrackSlotPointerClick = null;
                cell.TrackSlotPointerEnter = null;
                cell.TrackSlotPointerExit = null;
                var nativeButton = cloneGo.GetComponent<Button>();
                if ((UnityEngine.Object)(object)nativeButton != (UnityEngine.Object)null)
                {
                    nativeButton.onClick.RemoveAllListeners();
                }

                // Drive the bright "known/owned" look via the game's own renderer:
                // isLocked:false, isAvailable:false, isBuyable:false, isLearnable:true -> KnownSkill ->
                // PrimaryUIColor. Pass the real personal slot so the cell internals stay valid.
                cell.SetSkill(AbilityTrackSource.Personal, slot, def,
                    isLocked: false, isAvailable: false, isBuyable: false, isLearnable: true);

                // Disable the native (text) tooltip; we provide the rich framed tooltip ourselves.
                cell.SetTooltip(null);

                // Let the GridLayoutGroup own placement/size regardless of the prefab's anchoring.
                var rt = cloneGo.GetComponent<RectTransform>();
                if ((UnityEngine.Object)(object)rt != (UnityEngine.Object)null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one;
                }

                AttachTooltip(cloneGo, def, tooltip, canvasRect, rootCanvas, swapContext);
                return cloneGo;
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] WikiIconFactory.MakeNative failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Fallback custom-icon cell (used only if no native template/slot is available). Builds a fixed-size
        /// transparent frame carrying the perk sprite + the hover trigger. Returns null on failure.
        /// </summary>
        public static GameObject Make(Transform parent, TacticalAbilityDef def,
            GeoRosterAbilityDetailTooltip tooltip, RectTransform canvasRect, Canvas rootCanvas,
            PerkSwapContext swapContext)
        {
            if ((UnityEngine.Object)(object)parent == (UnityEngine.Object)null || def == null)
            {
                return null;
            }

            try
            {
                var view = def.ViewElementDef;
                Sprite sprite = null;
                if ((UnityEngine.Object)(object)view != (UnityEngine.Object)null)
                {
                    sprite = view.LargeIcon != null ? view.LargeIcon : view.SmallIcon;
                }

                // Frame: a transparent raycast target that fills its grid cell and carries the tooltip.
                var frameGo = new GameObject("WikiPerkIcon", typeof(RectTransform), typeof(Image));
                frameGo.transform.SetParent(parent, false);
                var frame = frameGo.GetComponent<Image>();
                frame.sprite = null;
                ((Graphic)frame).color = new Color(1f, 1f, 1f, 0f); // invisible but raycastable for hover
                frame.raycastTarget = true;

                // Child image: the actual perk sprite, aspect-fit inside the frame.
                var imgGo = new GameObject("Img", typeof(RectTransform), typeof(Image));
                imgGo.transform.SetParent(frameGo.transform, false);
                var img = imgGo.GetComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
                var imgRt = imgGo.GetComponent<RectTransform>();
                imgRt.anchorMin = Vector2.zero;
                imgRt.anchorMax = Vector2.one;
                imgRt.offsetMin = new Vector2(4f, 4f);
                imgRt.offsetMax = new Vector2(-4f, -4f);

                AttachTooltip(frameGo, def, tooltip, canvasRect, rootCanvas, swapContext);
                return frameGo;
            }
            catch (Exception ex)
            {
                Debug.Log("[PerkOracle] WikiIconFactory.Make failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Attach the hover trigger that drives the game's RICH framed ability tooltip and the left-click
        /// swap. The trigger shows/positions/hides the shared panel-owned tooltip and routes clicks to the
        /// swap. Non-essential failures are logged; the cell still shows.
        /// </summary>
        private static void AttachTooltip(GameObject go, TacticalAbilityDef def,
            GeoRosterAbilityDetailTooltip tooltip, RectTransform canvasRect, Canvas rootCanvas,
            PerkSwapContext swapContext)
        {
            try
            {
                var trigger = go.AddComponent<WikiAbilityTooltipTrigger>();
                trigger.Init(def, tooltip, canvasRect, rootCanvas, swapContext);
            }
            catch (Exception ex)
            {
                // Tooltip/click is non-essential; the cell still shows.
                Debug.Log("[PerkOracle] WikiIconFactory tooltip failed: " + ex.Message);
            }
        }
    }
}
