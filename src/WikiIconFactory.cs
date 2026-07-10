using System;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.Oracle
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
        /// Hard sortingOrder for the mod's overrideSorting tooltip-layer wrappers. Fixed constant (not a
        /// live scan of scene canvases) so the ability tooltip renders above EVERY UI surface — including the
        /// subclass-confirm dialog, which lives on a DontDestroyOnLoad SystemMessageCanvas that a
        /// FindObjectsOfType&lt;Canvas&gt; snapshot can miss or read stale, and which finalizes its own canvas
        /// order AFTER our decorate-time snapshot. 32000 is just under Unity's short.MaxValue clamp (32767) so
        /// nothing native can outsort it. TFTV uses the same hard-constant approach (5000) for its recruit
        /// stat tooltip. Applied on EVERY show, so a later-activated modal can never win.
        /// </summary>
        public const int TooltipSortingOrder = 32000;

        // Cached handle to the cell's private state renderer. Driving it directly lets a wiki cell show any
        // ability/class icon in the bright "known" or dim "unavailable" look WITHOUT needing a real
        // AbilityTrackSlot (the public SetSkill overloads dereference the slot). Our own SetSkillState
        // postfixes recognize the clone by name (CloneNamePrefix) and skip it, so this never tints the clone.
        private static readonly MethodInfo SetSkillStateMethod = AccessTools.Method(
            typeof(AbilityTrackSkillEntryElement), "SetSkillState",
            new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });

        /// <summary>
        /// Find a PRISTINE native <see cref="GeoRosterAbilityDetailTooltip"/> to clone, skipping any of the
        /// mod's OWN clones. Prefer a live (active) native in-scene instance; else any inactive native scene
        /// instance (e.g. inside the dual-class modal, where the progression tooltip GO is inactive).
        ///
        /// CRITICAL: this exclusion is what keeps the class-selection skill tooltip looking native. Other
        /// Oracle features clone this same widget and REPURPOSE their copy — notably
        /// <see cref="EventOutcomeTooltip"/>, which deactivates the title/icon/cost groups AND turns OFF the
        /// description's word-wrap, then keeps that clone as a persistent inactive scene instance. A plain
        /// FirstOrDefault() lookup can pick THAT mangled clone as the template on screens where no pristine
        /// native tooltip is the first hit (the dual-class picker / confirm), yielding a title-less,
        /// non-wrapping box whose text overflows the frame — exactly the "custom-looking" tooltip reported.
        /// Skipping our own clones (by the name prefixes we stamp) guarantees a real native template, the same
        /// one the roster screen clones. Mirrors TFTV's HavenRecruits tooltip finder, which likewise excludes
        /// its own overlay clone.
        /// </summary>
        public static GeoRosterAbilityDetailTooltip FindNativeTooltipTemplate()
        {
            foreach (GeoRosterAbilityDetailTooltip t in
                     UnityEngine.Object.FindObjectsOfType<GeoRosterAbilityDetailTooltip>())
            {
                if ((UnityEngine.Object)(object)t != (UnityEngine.Object)null && !IsOwnTooltipClone(t))
                {
                    return t;
                }
            }

            foreach (GeoRosterAbilityDetailTooltip t in
                     Resources.FindObjectsOfTypeAll<GeoRosterAbilityDetailTooltip>())
            {
                if ((UnityEngine.Object)(object)t != (UnityEngine.Object)null
                    && t.gameObject.scene.IsValid() // a scene instance, not a prefab asset
                    && !IsOwnTooltipClone(t))
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>
        /// True if the tooltip is one of the mod's OWN clones (never a valid clone template). Every Oracle
        /// clone stamps a name starting with "Oracle" or "RolledPerk" (EventOutcomeTooltip -> "OracleEvent…",
        /// PerkWikiPanel -> "RolledPerkWikiAbilityTooltip", SubclassConfirmPopupDecorator -> "OracleConfirm…").
        /// Native tooltips never use those prefixes, so this excludes exactly our clones.
        /// </summary>
        private static bool IsOwnTooltipClone(GeoRosterAbilityDetailTooltip t)
        {
            string n = t.gameObject.name;
            return n.StartsWith("Oracle", StringComparison.Ordinal)
                || n.StartsWith("RolledPerk", StringComparison.Ordinal);
        }

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
                OracleLog.Debug("[Oracle] WikiIconFactory.MakeNative failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Clone a live native <paramref name="template"/> ability cell into a read-only class-wiki cell that
        /// shows <paramref name="icon"/> (an ability icon for a perk cell, or a class icon for a tab). The
        /// cell's own frame/background/animator render natively; state is driven through its private
        /// SetSkillState so <paramref name="bright"/> gives the "known/owned" bright look and !bright the dim
        /// "unavailable" grey. Native pointer delegates + Button listeners are cleared and any inherited
        /// rolled-perk tint child is stripped. When <paramref name="def"/> is non-null the rich framed ability
        /// tooltip trigger is attached (perk cells); a null def leaves the cell tooltip-less (class tabs, which
        /// the caller wires with its own click). Returns the clone GO, or null on failure/missing inputs.
        /// </summary>
        public static GameObject MakeCell(Transform parent, AbilityTrackSkillEntryElement template, Sprite icon,
            TacticalAbilityDef def, bool bright, GeoRosterAbilityDetailTooltip tooltip, RectTransform canvasRect,
            Canvas rootCanvas)
        {
            if ((UnityEngine.Object)(object)parent == (UnityEngine.Object)null
                || (UnityEngine.Object)(object)template == (UnityEngine.Object)null)
            {
                return null;
            }

            try
            {
                GameObject cloneGo = UnityEngine.Object.Instantiate(
                    ((Component)template).gameObject, parent, false);
                // Stamp the name BEFORE any SetSkillState so our highlight/wiki postfixes skip this clone.
                cloneGo.name = CloneNamePrefix;

                var cell = cloneGo.GetComponent<AbilityTrackSkillEntryElement>();
                if ((UnityEngine.Object)(object)cell == (UnityEngine.Object)null)
                {
                    UnityEngine.Object.Destroy(cloneGo);
                    return null;
                }

                // Neutralize native interaction so a clone can never fire the game's buy/learn action.
                cell.TrackSlotPointerClick = null;
                cell.TrackSlotPointerEnter = null;
                cell.TrackSlotPointerExit = null;
                var nativeButton = cloneGo.GetComponent<Button>();
                if ((UnityEngine.Object)(object)nativeButton != (UnityEngine.Object)null)
                {
                    nativeButton.onClick.RemoveAllListeners();
                }

                // The template may be the soldier's first main-class cell (it carries our own
                // ClassWikiClickHandler) or a rolled Personal cell (dark tint child). Instantiate copies BOTH
                // onto the clone -> strip them so a wiki cell can't reopen/close the wiki or show a stray tint.
                StripOwnBehaviour<ClassWikiClickHandler>(cloneGo);
                StripOwnBehaviour<WikiAbilityTooltipTrigger>(cloneGo);
                RemoveRolledTint(cloneGo);

                // Show the requested icon on the cell's OWN SkillIcon so the state visuals stay native.
                // A null icon means an anonymized cell (random slot): keep the native frame, hide the icon.
                if ((UnityEngine.Object)(object)cell.SkillIcon != (UnityEngine.Object)null)
                {
                    cell.SkillIcon.gameObject.SetActive(icon != null);
                    cell.SkillIcon.sprite = icon;
                }
                cell.AbilityDef = def; // null for class tabs (no ability)
                cell.SetTooltip(null); // native text tooltip off; the rich tooltip is driven by our trigger

                // Drive the native look via the cell's own state machine (no slot needed):
                //   bright: known/owned -> (locked:F, avail:F, buyable:F, learn:T) => KnownSkill => PrimaryUIColor
                //   grey:   unavailable -> (locked:F, avail:T, buyable:F, learn:T) => AvailableSkill, and since
                //           buyable is F the "Available" pulse image stays off AND the native click is inert
                //           => SecondaryUIColor (the game's own dim "unknown skill" tint).
                if ((object)SetSkillStateMethod != null)
                {
                    SetSkillStateMethod.Invoke(cell, bright
                        ? new object[] { false, false, false, true }
                        : new object[] { false, true, false, true });
                }

                // Let the GridLayoutGroup own placement/size regardless of the prefab's anchoring.
                var rt = cloneGo.GetComponent<RectTransform>();
                if ((UnityEngine.Object)(object)rt != (UnityEngine.Object)null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one;
                }

                if (def != null)
                {
                    AttachTooltip(cloneGo, def, tooltip, canvasRect, rootCanvas, null);
                }
                return cloneGo;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiIconFactory.MakeCell failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Remove a mod-added MonoBehaviour the template carried (disable first so the EventSystem drops it
        /// this frame, then Destroy). Guarded.
        /// </summary>
        private static void StripOwnBehaviour<T>(GameObject cloneGo) where T : MonoBehaviour
        {
            try
            {
                var comp = cloneGo.GetComponent<T>();
                if ((UnityEngine.Object)(object)comp != (UnityEngine.Object)null)
                {
                    comp.enabled = false;
                    UnityEngine.Object.Destroy(comp);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiIconFactory.StripOwnBehaviour failed: " + ex.Message);
            }
        }

        /// <summary>Strip any cloned rolled-perk tint child (see <see cref="CellBackground"/>) from a clone.</summary>
        private static void RemoveRolledTint(GameObject cloneGo)
        {
            try
            {
                foreach (Transform t in cloneGo.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if ((UnityEngine.Object)(object)t != (UnityEngine.Object)null
                        && ((Component)t).gameObject.name == CellBackground.ChildName)
                    {
                        UnityEngine.Object.Destroy(((Component)t).gameObject);
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WikiIconFactory.RemoveRolledTint failed: " + ex.Message);
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
                OracleLog.Debug("[Oracle] WikiIconFactory.Make failed: " + ex.Message);
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
                OracleLog.Debug("[Oracle] WikiIconFactory tooltip failed: " + ex.Message);
            }
        }
    }
}
