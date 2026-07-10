using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Base.Defs;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Common.View.ViewControllers.Inventory;
using PhoenixPoint.Geoscape.View.ViewControllers.Inventory;
using PhoenixPoint.Geoscape.View.ViewControllers.PhoenixBase;
using PhoenixPoint.Tactical.View.ViewControllers.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Morgott.Oracle
{
    /// <summary>
    /// Shows an item's dismantle yield (<see cref="ItemDef.ScrapPrice"/> = floor(ManufactureX / 2) per resource;
    /// an all-zero manufacture cost yields nothing and gets no row) as ONE native-styled stat row at the bottom
    /// of the item hover tooltip: localized "Dismantle" label on the left (pixel-identical to the other stat-row
    /// labels — it IS a cloned StatPrefab row) and a right-aligned strip of the game's own COLORED resource
    /// icons + amounts in the value area — e.g. <c>Разбор    [materials]12 [tech]3</c>, icons as on the
    /// manufacturing screen's scrap strip. No resource names.
    ///
    /// Two cooperating patches, because the two pieces live at different levels:
    ///   * DATA gate (this class, postfix on <see cref="UIItemTooltip.GetItemData"/>): GetItemData is the one
    ///     point that sees BOTH the owning tooltip instance (for the blacklist) AND the <see cref="ItemDef"/>
    ///     (for <see cref="ItemDef.ScrapPrice"/>) — the panel's <c>LinkToData</c> receives neither reliably
    ///     (tactical/mutation call it with a null item). It runs immediately before the panel is populated, so it
    ///     stashes the dismantle <see cref="ResourcePack"/> in <see cref="_pendingScrap"/> for the panel patch to
    ///     consume that same show. When the native colored-icon template cannot be found in the current scene
    ///     (e.g. a pure tactical session with no geoscape UI prefabs loaded), it instead appends the mod's
    ///     original single TEXT row "Dismantle: 12 Materials, 3 Tech" to the returned stat list (rendered by the
    ///     panel's own stat machinery) and stashes nothing.
    ///   * VIEW build (<see cref="ItemScrapTooltipRowPatch"/>, postfix on
    ///     <see cref="UIInventoryTooltipItemPanel.LinkToData"/> for the PRIMARY panel only): GameObjects are
    ///     needed for the row + icon strip, so they are built there where the panel instance (hence
    ///     <c>StatEntries</c> and <c>StatPrefab</c>) is in hand. On every primary populate it first destroys any
    ///     prior "OracleScrapRow" (dedup on repeat hovers + drops the previous item's strip, since the panel is
    ///     pooled/reused and LinkToData does not manage our custom row), then rebuilds from the stashed pack: a
    ///     cloned StatPrefab row (native label styling for "Dismantle", blank value) + one cloned
    ///     <see cref="ResourceIconContainer"/> per non-zero resource overlaid right-aligned on the value area,
    ///     each rendering the native colored icon + amount through its own <c>ResourcesDef</c>.
    ///
    /// GetItemData is shared by six tooltips; we BLACKLIST only <see cref="UIManufacturingTooltip"/> (already
    /// lists scrap natively, a row here would duplicate it) and <see cref="UIPhoenixpediaItemTooltip"/> (codex
    /// view, not gear management) — the strip therefore shows on the equip/loadout, geoscape, tactical and
    /// mutation tooltips. Live-gated by <see cref="OracleMain.ShowDismantleCompensation"/>. Both patches are fully
    /// guarded so a failure can never break the tooltip.
    /// </summary>
    [HarmonyPatch(typeof(UIItemTooltip), nameof(UIItemTooltip.GetItemData))]
    internal static class ItemScrapTooltipPatch
    {
        // Fixed display order: Materials, then Tech, then the rarer types. Matches the native manufacturing
        // tooltip (Materials before Tech) and the feature's "[materials]12 [tech]3" example.
        internal static readonly ResourceType[] ScrapOrder =
        {
            ResourceType.Materials,
            ResourceType.Tech,
            ResourceType.Mutagen,
            ResourceType.LivingCrystals,
            ResourceType.Orichalcum,
            ResourceType.ProteanMutane
        };

        // Handoff from the GetItemData gate to the LinkToData row-builder: the dismantle pack to render as an
        // icon strip on the NEXT primary panel populate, or null for "no strip" (feature off / blacklisted /
        // nothing to recover / text-fallback path). Reset at the start of every GetItemData, so a strip can
        // never leak from one hovered item to the next.
        private static ResourcePack _pendingScrap;

        // A live/prefab ResourceIconContainer to clone for each strip icon. Defs are never destroyed and the
        // template is a serialized object, but a scene unload can Unity-null it, so we re-resolve while null.
        private static ResourceIconContainer _iconTemplate;

        [HarmonyPostfix]
        private static void Postfix(UIItemTooltip __instance, ItemDef item,
            (List<ComparableData>, List<List<ComparableData>>) __result)
        {
            try
            {
                _pendingScrap = null; // default: no strip; also tells the row patch to clear any stale strip

                if (!OracleMain.ShowDismantleCompensation)
                {
                    return; // feature disabled
                }
                // Blacklist: show on every item tooltip routing through GetItemData EXCEPT the manufacturing
                // tooltip (already lists scrap) and the phoenixpedia codex view.
                if (__instance is UIManufacturingTooltip || __instance is UIPhoenixpediaItemTooltip)
                {
                    return;
                }
                if (item == null || __result.Item1 == null)
                {
                    return;
                }

                ResourcePack scrap = item.ScrapPrice;
                if (scrap == null || scrap.IsEmpty)
                {
                    return; // nothing recovered (all-zero manufacture cost)
                }

                // Preferred: native colored-icon strip — stash the pack for the LinkToData patch to build.
                if (ResolveIconTemplate() != null)
                {
                    _pendingScrap = scrap;
                    return;
                }

                // Fallback (no icon template loaded in this scene): single text row "Dismantle: 12 Materials, 3 Tech".
                string resources = BuildResourceList(scrap);
                if (!string.IsNullOrEmpty(resources))
                {
                    __result.Item1.Add(new ComparableData
                    {
                        localization = new LocalizedTextBind("ORACLE_ITEM_SCRAP"),
                        primaryData = new StatData(resources, null, null)
                    });
                }
            }
            catch (Exception ex)
            {
                _pendingScrap = null;
                OracleLog.Debug("[Oracle] ItemScrapTooltipPatch.Postfix failed: " + ex.Message);
            }
        }

        /// <summary>Consume the stashed dismantle pack (single-use): returns it and clears the stash so a later
        /// unrelated populate can never rebuild a strip from a previous item.</summary>
        internal static ResourcePack ConsumePendingScrap()
        {
            ResourcePack s = _pendingScrap;
            _pendingScrap = null;
            return s;
        }

        /// <summary>Locate a <see cref="ResourceIconContainer"/> (icon + value + ResourcesDef wired) to clone per
        /// strip icon — the same widget the manufacturing screen uses. <see cref="Resources.FindObjectsOfTypeAll"/>
        /// finds inactive/prefab instances too. Cached; re-resolved while Unity-null.</summary>
        internal static ResourceIconContainer ResolveIconTemplate()
        {
            if (_iconTemplate != null)
            {
                return _iconTemplate;
            }
            _iconTemplate = Resources.FindObjectsOfTypeAll<ResourceIconContainer>()
                .FirstOrDefault(c => c != null && c.ResourcesDef != null && c.Icon != null && c.Value != null);
            return _iconTemplate;
        }

        /// <summary>
        /// Compose the yield string in <see cref="ScrapOrder"/>, e.g. "12 Materials, 3 Tech". Each resource is
        /// included only when its rounded amount is &gt;= 1. Used only by the text fallback.
        /// </summary>
        private static string BuildResourceList(ResourcePack scrap)
        {
            var sb = new StringBuilder();
            foreach (ResourceType type in ScrapOrder)
            {
                int amount = Mathf.RoundToInt(scrap.ByResourceType(type).Value);
                if (amount <= 0)
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(amount).Append(' ').Append(ResourceName(type));
            }
            return sb.ToString();
        }

        /// <summary>Localized resource-type name via the mod's own keys, with an English literal fallback.</summary>
        private static string ResourceName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Materials: return Loc.Get("ORACLE_RES_MATERIALS", "Materials");
                case ResourceType.Tech: return Loc.Get("ORACLE_RES_TECH", "Tech");
                case ResourceType.Mutagen: return Loc.Get("ORACLE_RES_MUTAGEN", "Mutagen");
                case ResourceType.LivingCrystals: return Loc.Get("ORACLE_RES_LIVINGCRYSTALS", "Living Crystals");
                case ResourceType.Orichalcum: return Loc.Get("ORACLE_RES_ORICHALCUM", "Orichalcum");
                case ResourceType.ProteanMutane: return Loc.Get("ORACLE_RES_PROTEANMUTANE", "Protean Mutane");
                default: return type.ToString();
            }
        }
    }

    /// <summary>
    /// View-level half of the dismantle feature: builds the single colored-icon strip on the primary tooltip
    /// panel. See <see cref="ItemScrapTooltipPatch"/> for the full design.
    /// </summary>
    [HarmonyPatch(typeof(UIInventoryTooltipItemPanel), nameof(UIInventoryTooltipItemPanel.LinkToData))]
    internal static class ItemScrapTooltipRowPatch
    {
        private const string RowName = "OracleScrapRow";

        [HarmonyPostfix]
        private static void Postfix(UIInventoryTooltipItemPanel __instance, bool secondItem)
        {
            try
            {
                if (secondItem)
                {
                    return; // primary panel only; the comparison panel never gets a strip
                }
                Transform parent = (__instance != null) ? __instance.StatEntries : null;
                if (parent == null)
                {
                    return;
                }

                // Always clear a prior strip first: dedup on repeat hovers AND drop the previous item's strip
                // (the panel is pooled/reused and its LinkToData does not manage our custom GameObject).
                Transform old = parent.Find(RowName);
                if (old != null)
                {
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }

                ResourcePack scrap = ItemScrapTooltipPatch.ConsumePendingScrap();
                if (scrap == null)
                {
                    return; // this item has no strip (feature off / blacklisted / nothing / text fallback)
                }
                ResourceIconContainer template = ItemScrapTooltipPatch.ResolveIconTemplate();
                if (template == null)
                {
                    return; // defensive; the gate only stashes when a template resolved
                }

                BuildRow(__instance, parent, scrap, template);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ItemScrapTooltipRowPatch.Postfix failed: " + ex.Message);
            }
        }

        private static void BuildRow(UIInventoryTooltipItemPanel panel, Transform parent, ResourcePack scrap,
            ResourceIconContainer template)
        {
            if (panel.StatPrefab == null)
            {
                return;
            }

            // Clone the panel's own StatPrefab row so the label font/size/color, row height and paddings are
            // pixel-identical to the sibling stat rows (Damage, Weight, ...): its native SetData renders the
            // localized "Dismantle" label through the row's own Localize component; value/icon left blank.
            UIInventoryTooltipItemStat row = UnityEngine.Object.Instantiate(panel.StatPrefab, parent);
            row.gameObject.name = RowName; // dedup key; also strips "(Clone)"
            row.SetData(new StatData(null, null, null), new LocalizedTextBind("ORACLE_ITEM_SCRAP"));
            row.transform.SetAsLastSibling();

            // Icon strip overlaid on the row's value area — the exact rect where sibling rows draw their numeric
            // value — children packed to the RIGHT edge so [icon]12 [icon]3 sits where a value would. Separate
            // overlay GameObject (not a LayoutGroup added to the value Text itself) to avoid fighting whatever
            // components the native prefab carries.
            var strip = new GameObject("OracleScrapStrip", typeof(RectTransform));
            var stripRt = (RectTransform)strip.transform;
            RectTransform valueRt = (row.StatValueTextComp != null) ? row.StatValueTextComp.rectTransform : null;
            stripRt.SetParent((valueRt != null) ? valueRt.parent : row.transform, worldPositionStays: false);
            if (valueRt != null)
            {
                stripRt.anchorMin = valueRt.anchorMin;
                stripRt.anchorMax = valueRt.anchorMax;
                stripRt.pivot = valueRt.pivot;
                stripRt.anchoredPosition = valueRt.anchoredPosition;
                stripRt.sizeDelta = valueRt.sizeDelta;
            }
            else
            {
                // Defensive: prefab without a value Text -> stretch over the whole row, still right-packed.
                stripRt.anchorMin = Vector2.zero;
                stripRt.anchorMax = Vector2.one;
                stripRt.offsetMin = Vector2.zero;
                stripRt.offsetMax = Vector2.zero;
            }

            HorizontalLayoutGroup hlg = strip.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            foreach (ResourceType type in ItemScrapTooltipPatch.ScrapOrder)
            {
                int amount = Mathf.RoundToInt(scrap.ByResourceType(type).Value);
                if (amount <= 0)
                {
                    continue;
                }
                ResourceIconContainer ric = UnityEngine.Object.Instantiate(template, strip.transform);
                ric.gameObject.SetActive(value: true);
                CleanClone(ric);
                ric.SetResource(type, amount); // native colored icon + amount via the container's own ResourcesDef
            }
        }

        /// <summary>
        /// The template can come from any screen, and some instances carry prefab-only decoration the C# class
        /// never references (e.g. a vertical divider Image that showed up next to the gears icon) —
        /// <see cref="ResourceIconContainer"/> only serializes Icon + Value, so decoration is invisible in the
        /// decompile. Whitelist those two: every other Graphic in the clone is turned off (whole GameObject when
        /// it holds neither Icon nor Value beneath it, so its layout space is freed too; render-only disable when
        /// it shares a GameObject/branch with a kept element). Also bumps the amount digits ~20% for readability,
        /// keeping the best-fit cap in sync and letting wide numbers overflow instead of clip.
        /// </summary>
        private static void CleanClone(ResourceIconContainer ric)
        {
            foreach (Graphic g in ric.GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                if (g == null || g == ric.Icon || g == ric.Value)
                {
                    continue;
                }
                bool holdsKept =
                    (ric.Icon != null && ric.Icon.transform.IsChildOf(g.transform)) ||
                    (ric.Value != null && ric.Value.transform.IsChildOf(g.transform));
                if (holdsKept)
                {
                    g.enabled = false;
                }
                else
                {
                    g.gameObject.SetActive(value: false);
                }
            }

            Text v = ric.Value;
            if (v != null)
            {
                v.fontSize = Mathf.RoundToInt(v.fontSize * 1.2f);
                if (v.resizeTextForBestFit)
                {
                    v.resizeTextMaxSize = Mathf.RoundToInt(v.resizeTextMaxSize * 1.2f);
                }
                v.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }
    }
}
