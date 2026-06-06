using PhoenixPoint.Geoscape.View.ViewControllers;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Find-or-create a single dark child Image covering a progression cell, drawn just BELOW the
    /// icon (above the cell's own background/frame) so it is actually visible. Idempotent: reused
    /// across UI refreshes (the child is created at most once per cell, sibling index re-asserted on
    /// show in case the cell rebuilt its children).
    /// </summary>
    public static class CellBackground
    {
        private const string ChildName = "RolledPerkBg";
        private const string HighlightName = "highlight";
        private const string BackgroundName = "UIElement_Background";

        // KNOB: recolor the rolled-cell tint here. Semi-transparent blue, visible over the
        // cell's opaque-black base.
        private static readonly Color RolledColor = new Color(0.20f, 0.45f, 1.00f, 0.55f);

        public static void Apply(AbilityTrackSkillEntryElement cell, bool show)
        {
            if (cell == null)
            {
                return;
            }

            Image icon = cell.SkillIcon;
            if ((Object)(object)icon == (Object)null)
            {
                return;
            }

            Transform iconT = ((Component)icon).transform;
            Transform parent = iconT.parent;
            if ((Object)(object)parent == (Object)null)
            {
                return;
            }

            Image overlay = FindOrCreate(parent);
            if ((Object)(object)overlay == (Object)null)
            {
                return;
            }

            if (show)
            {
                ApplyVisuals(overlay, parent, iconT);
                ((Component)overlay).gameObject.SetActive(true);
            }
            else
            {
                ((Component)overlay).gameObject.SetActive(false);
            }
        }

        private static Image FindOrCreate(Transform parent)
        {
            Transform existing = parent.Find(ChildName);
            if ((Object)(object)existing != (Object)null)
            {
                return ((Component)existing).GetComponent<Image>();
            }

            var go = new GameObject(ChildName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            ((Graphic)image).color = RolledColor;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// (Re)assert sprite, full-cell rect, and z-order so the overlay draws just below the icon
        /// and above the cell's opaque background.
        /// </summary>
        private static void ApplyVisuals(Image overlay, Transform parent, Transform iconT)
        {
            // The cell's background is the first child Image of the parent that is not our overlay
            // (typically child index 0). Use its sprite so the overlay definitely renders, and copy
            // its rect so we cover the whole cell.
            Image bg = FindBackgroundImage(parent);

            if ((Object)(object)bg != (Object)null && (Object)(object)bg.sprite != (Object)null)
            {
                overlay.sprite = bg.sprite;
                overlay.type = bg.type;
                ((Graphic)overlay).color = RolledColor;
            }
            else
            {
                // A sprite-less Image still renders a flat quad; bump alpha to compensate.
                overlay.sprite = null;
                ((Graphic)overlay).color = new Color(0.20f, 0.45f, 1.00f, 0.6f);
            }

            StretchToCell(((Component)overlay).GetComponent<RectTransform>(), bg);

            // Place the overlay just BELOW the hover "highlight" element so the animator-driven grey
            // hover still renders on top of our blue tint, while we sit above the opaque-black base.
            // Re-asserted on every show in case the cell rebuilt/reordered its children.
            ApplySiblingPlacement(overlay.transform, parent, iconT);
        }

        /// <summary>
        /// Move the overlay to sit immediately below the hover "highlight" element (so hover draws
        /// on top), else just above the opaque-black "UIElement_Background", else fall back to the
        /// icon's index. Scans the parent's current children each show; null-safe; ignores our own
        /// "RolledPerkBg".
        /// </summary>
        private static void ApplySiblingPlacement(Transform overlayT, Transform parent, Transform iconT)
        {
            Transform highlight = FindSiblingByName(parent, HighlightName);
            if ((Object)(object)highlight != (Object)null)
            {
                // SetSiblingIndex to the highlight's index pushes highlight up by one -> we land
                // immediately below it (above the black base).
                overlayT.SetSiblingIndex(highlight.GetSiblingIndex());
                return;
            }

            Transform bg = FindSiblingByName(parent, BackgroundName);
            if ((Object)(object)bg != (Object)null)
            {
                overlayT.SetSiblingIndex(bg.GetSiblingIndex() + 1);
                return;
            }

            overlayT.SetSiblingIndex(iconT.GetSiblingIndex());
        }

        private static Transform FindSiblingByName(Transform parent, string name)
        {
            if ((Object)(object)parent == (Object)null)
            {
                return null;
            }
            int count = parent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = parent.GetChild(i);
                string childName = ((Component)child).gameObject.name;
                if (childName == ChildName)
                {
                    continue;
                }
                if (childName == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static Image FindBackgroundImage(Transform parent)
        {
            int count = parent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = parent.GetChild(i);
                if (((Component)child).gameObject.name == ChildName)
                {
                    continue;
                }
                var img = ((Component)child).GetComponent<Image>();
                if ((Object)(object)img != (Object)null)
                {
                    return img;
                }
            }
            return null;
        }

        private static void StretchToCell(RectTransform rect, Image bg)
        {
            // Prefer copying the cell background's rect so we cover the whole cell; otherwise full
            // 0..1 stretch of the parent.
            RectTransform bgRect = null;
            if ((Object)(object)bg != (Object)null)
            {
                bgRect = ((Component)bg).GetComponent<RectTransform>();
            }

            if ((Object)(object)bgRect != (Object)null)
            {
                rect.anchorMin = bgRect.anchorMin;
                rect.anchorMax = bgRect.anchorMax;
                rect.pivot = bgRect.pivot;
                rect.anchoredPosition = bgRect.anchoredPosition;
                rect.sizeDelta = bgRect.sizeDelta;
                rect.offsetMin = bgRect.offsetMin;
                rect.offsetMax = bgRect.offsetMax;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }
    }
}
