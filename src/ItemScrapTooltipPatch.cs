using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Base.Defs;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Common.View.ViewControllers.Inventory;
using PhoenixPoint.Common.View.ViewModules;
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
    /// manufacturing screen's scrap strip. No resource names. The shown pack is the ACTUAL grant: under TFTV,
    /// ammo is prorated by the live item's remaining charges via
    /// <see cref="TftvConfigBridge.ScrapRefundMultiplier"/> (raw ScrapPrice in vanilla), amounts floored like
    /// the native scrap-zone display.
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

                // Show what the game will actually GRANT: under TFTV, ammo refunds are prorated by the LIVE
                // item's remaining charges (its GeoFaction.ScrapItem prefix grants ScrapPrice * multiplier) —
                // the def alone can't tell a fresh clip from a spent one. Vanilla / def-only view -> 1.
                float refundMult = TftvConfigBridge.ScrapRefundMultiplier(ResolveLiveItem(__instance, item));
                if (refundMult < 1f)
                {
                    scrap = scrap * refundMult;
                }
                if (!HasDisplayableAmount(scrap))
                {
                    return; // prorated below 1 of everything -> the grant displays as nothing -> no row
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

        // Live-item lookup per tooltip type (private view fields, resolved once, null on a miss — AccessTools
        // .Field logs instead of throwing). Needed because TFTV's proration depends on the ITEM's CurrentCharges.
        private static readonly FieldInfo TacDisplayedItem = AccessTools.Field(typeof(UITacItemTooltip), "_displayedItem");
        private static readonly FieldInfo GeoDisplayedItem = AccessTools.Field(typeof(UIGeoItemTooltip), "_displayedItem");
        private static readonly FieldInfo InvHoveredSlot = AccessTools.Field(typeof(UIInventoryTooltip), "_hoveredSlot");
        private static readonly FieldInfo InvEquipModule = AccessTools.Field(typeof(UIInventoryTooltip), "_soldierEquipModule");

        /// <summary>
        /// The LIVE <see cref="ICommonItem"/> whose def the tooltip is currently showing, or null (mutation /
        /// unknown tooltip = def-only view). Tactical/geoscape tooltips stash it in <c>_displayedItem</c> before
        /// calling GetItemData; the equip tooltip's primary item is the SELECTED slot's when comparing, else the
        /// hovered slot's — checked in that order, matched by def.
        /// </summary>
        private static ICommonItem ResolveLiveItem(UIItemTooltip tooltip, ItemDef def)
        {
            try
            {
                switch (tooltip)
                {
                    case UITacItemTooltip tac:
                        return AsLive(TacDisplayedItem?.GetValue(tac) as ICommonItem, def);
                    case UIGeoItemTooltip geo:
                        return AsLive(GeoDisplayedItem?.GetValue(geo) as ICommonItem, def);
                    case UIInventoryTooltip inv:
                    {
                        var module = InvEquipModule?.GetValue(inv) as UIModuleSoldierEquip;
                        ICommonItem selected = AsLive(module?.SelectedSlot?.Item, def);
                        if (selected != null)
                        {
                            return selected;
                        }
                        var hovered = InvHoveredSlot?.GetValue(inv) as UIInventorySlot;
                        return AsLive(hovered?.Item, def);
                    }
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ResolveLiveItem failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>The item itself when it is showing exactly this def, else null.</summary>
        private static ICommonItem AsLive(ICommonItem item, ItemDef def)
        {
            return (item != null && item.ItemDef == def) ? item : null;
        }

        /// <summary>True if at least one resource survives display flooring (&gt;= 1 after proration).</summary>
        private static bool HasDisplayableAmount(ResourcePack scrap)
        {
            foreach (ResourceType type in ScrapOrder)
            {
                if (Mathf.FloorToInt(scrap.ByResourceType(type).Value) >= 1)
                {
                    return true;
                }
            }
            return false;
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
        /// included only when its FLOORED amount is &gt;= 1 — floor matches both the native scrap-zone display
        /// (<c>ResourceIconContainer.SetResource(..., floor: true)</c>) and the whole part of the float pack the
        /// grant actually deposits. Used only by the text fallback.
        /// </summary>
        private static string BuildResourceList(ResourcePack scrap)
        {
            var sb = new StringBuilder();
            foreach (ResourceType type in ScrapOrder)
            {
                int amount = Mathf.FloorToInt(scrap.ByResourceType(type).Value);
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
        private const string GapName = "OracleScrapGap";

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

                // Always clear the prior row + its spacer first: dedup on repeat hovers AND drop the previous
                // item's strip (the panel is pooled/reused and its LinkToData does not manage our GameObjects).
                Transform old = parent.Find(RowName);
                if (old != null)
                {
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                Transform oldGap = parent.Find(GapName);
                if (oldGap != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldGap.gameObject);
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

            // Native labels render UPPERCASE (Damage, Ammo, ...) via I2 — mirror that with the row's own
            // Localize modifier (applied on every (re)localize), set BEFORE SetData resolves the term.
            if (row.StatNameTextComp != null)
            {
                I2.Loc.Localize lc = row.StatNameTextComp.GetComponent<I2.Loc.Localize>();
                if (lc != null)
                {
                    lc.PrimaryTermModifier = I2.Loc.Localize.TermModification.ToUpper;
                }
            }
            row.SetData(new StatData(null, null, null), new LocalizedTextBind("ORACLE_ITEM_SCRAP"));

            // Native value metrics: the digits of every other row (golden color, font, size).
            Text sv = row.StatValueTextComp;
            int digitSize = (sv != null && sv.fontSize > 0) ? sv.fontSize : 20;
            Color gold = (sv != null) ? sv.color : Color.white;

            // Footer gap: thin spacer row above ours, so the dismantle line sits visually separated from the
            // stat block. ponytail: gap = 60% of the value font size; the "looks native" knob.
            var gap = new GameObject(GapName, typeof(RectTransform));
            gap.transform.SetParent(parent, worldPositionStays: false);
            LayoutElement gapLe = gap.AddComponent<LayoutElement>();
            gapLe.minHeight = gapLe.preferredHeight = Mathf.Round(digitSize * 0.6f);

            gap.transform.SetAsLastSibling();
            row.transform.SetAsLastSibling(); // always LAST: rebuilt after every LinkToData repopulation

            // Icon strip overlaid on the row's value area — the exact rect where sibling rows draw their numeric
            // value — children packed to the RIGHT edge with zero padding, so the last digit ends flush where
            // sibling values end (Text preferredWidth is exact glyph width -> no trailing space).
            var strip = new GameObject("OracleScrapStrip", typeof(RectTransform));
            var stripRt = (RectTransform)strip.transform;
            RectTransform valueRt = (sv != null) ? sv.rectTransform : null;
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

            // ponytail: icon ~= line height ~= 1.4x font size; THE icon-vs-digit visual-match knob.
            float iconSize = digitSize * 1.4f;

            foreach (ResourceType type in ItemScrapTooltipPatch.ScrapOrder)
            {
                // Floor, like the native scrap-zone display (SetResource floor:true) and the granted whole part.
                int amount = Mathf.FloorToInt(scrap.ByResourceType(type).Value);
                if (amount <= 0)
                {
                    continue;
                }

                // Clone the native container DETACHED + inactive, let its SetResource fill icon sprite, digits
                // and the native RESOURCE colors, then extract ONLY the Icon and Value into our own layout pair
                // and discard the rest — per-screen prefab decoration (stray dividers) and the container's own
                // internal rect (source of the earlier right-edge indent) never enter the strip.
                ResourceIconContainer ric = UnityEngine.Object.Instantiate(template);
                ric.gameObject.SetActive(value: false);
                ric.SetResource(type, amount);
                Image icon = ric.Icon;
                Text val = ric.Value;
                if (icon == null || val == null)
                {
                    UnityEngine.Object.DestroyImmediate(ric.gameObject);
                    continue;
                }

                var pair = new GameObject("OracleScrapPair", typeof(RectTransform));
                pair.transform.SetParent(strip.transform, worldPositionStays: false);
                HorizontalLayoutGroup phlg = pair.AddComponent<HorizontalLayoutGroup>();
                phlg.spacing = 4f;
                phlg.childAlignment = TextAnchor.MiddleCenter;
                phlg.childControlWidth = true;
                phlg.childControlHeight = true;
                phlg.childForceExpandWidth = false;
                phlg.childForceExpandHeight = false;

                // [icon] sized to match the digits' visual height; keeps its NATIVE resource color (user asked
                // for colored icons) — only the digits go golden below.
                icon.transform.SetParent(pair.transform, worldPositionStays: false);
                icon.gameObject.SetActive(value: true);
                icon.preserveAspect = true;
                LayoutElement ile = icon.GetComponent<LayoutElement>();
                if (ile == null)
                {
                    ile = icon.gameObject.AddComponent<LayoutElement>();
                }
                ile.ignoreLayout = false;
                ile.minWidth = -1f;
                ile.flexibleWidth = -1f;
                ile.preferredWidth = iconSize;
                ile.minHeight = -1f;
                ile.flexibleHeight = -1f;
                ile.preferredHeight = iconSize;

                // [digits] exactly like a native value: same font/size/golden color; exact-size (no best-fit),
                // overflow instead of clip; any prefab LayoutElement neutralized so preferredWidth = glyph width.
                val.transform.SetParent(pair.transform, worldPositionStays: false);
                val.gameObject.SetActive(value: true);
                if (sv != null)
                {
                    val.font = sv.font;
                    val.fontStyle = sv.fontStyle;
                }
                val.fontSize = digitSize;
                val.color = gold;
                val.resizeTextForBestFit = false;
                val.horizontalOverflow = HorizontalWrapMode.Overflow;
                val.verticalOverflow = VerticalWrapMode.Overflow;
                LayoutElement vle = val.GetComponent<LayoutElement>();
                if (vle != null)
                {
                    vle.ignoreLayout = false;
                    vle.minWidth = -1f;
                    vle.preferredWidth = -1f;
                    vle.flexibleWidth = -1f;
                    vle.minHeight = -1f;
                    vle.preferredHeight = -1f;
                    vle.flexibleHeight = -1f;
                }

                UnityEngine.Object.DestroyImmediate(ric.gameObject); // rest of the clone (decor, container rect)
            }
        }
    }
}
