using System;
using System.Collections.Generic;
using System.Text;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.View.ViewControllers.Inventory;
using PhoenixPoint.Geoscape.View.ViewControllers.Inventory;
using PhoenixPoint.Tactical.View.ViewControllers.Inventory;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Adds a "Dismantle: N Materials, N Tech" row to the item hover tooltip showing what the item yields
    /// when scrapped (<see cref="ItemDef.ScrapPrice"/> = floor(ManufactureX / 2) per resource type; an
    /// item with an all-zero manufacture cost yields nothing and gets no row).
    ///
    /// Injection point is <see cref="UIItemTooltip.GetItemData"/>, NOT the two ShowStats methods: the list it
    /// returns is exactly what ShowStats hands to <c>InfoPanel.LinkToData</c>, so appending one
    /// <see cref="ComparableData"/> here makes the panel render our row through its own native stat machinery
    /// (StatPrefab clone, name+value columns) with no re-link and no reflection. The tooltip's own
    /// <c>FadeInCrt</c> runs <c>ForceRebuildLayoutImmediate</c> AFTER this, so the added row never breaks the
    /// box height. The stat NAME is a live I2 term (localized "Dismantle" label); the stat VALUE is the
    /// composed resource list — matching the native two-column stat layout ("Weight" | "3").
    ///
    /// GetItemData is shared by six tooltips, so we gate on the instance type and act ONLY for the two hover
    /// tooltips the feature targets (<see cref="UITacItemTooltip"/>, <see cref="UIGeoItemTooltip"/>). The
    /// other four are deliberately excluded: the manufacturing tooltip already shows scrap natively (a row
    /// here would duplicate it), and the phoenixpedia / mutation / equip-inventory tooltips are out of scope.
    /// Every other tooltip type derives from <see cref="UIItemTooltip"/> directly, so the two-branch type
    /// check cannot accidentally catch them.
    ///
    /// Resource names use the mod's own ORACLE_RES_* keys (with English fallback via <see cref="Loc"/>): the
    /// game's native resource-name source (UIModuleSiteEncounters.ResourcesList) only exists during an event
    /// screen, so it is unreachable from a plain item hover in tactical combat or the geoscape. Fully guarded
    /// so a failure can never break the tooltip.
    /// </summary>
    [HarmonyPatch(typeof(UIItemTooltip), nameof(UIItemTooltip.GetItemData))]
    internal static class ItemScrapTooltipPatch
    {
        // Fixed display order for the yielded resources: Materials, then Tech, then the rarer types. Matches
        // the native manufacturing tooltip (Materials before Tech) and the feature's "Materials, Tech" example.
        private static readonly ResourceType[] ScrapOrder =
        {
            ResourceType.Materials,
            ResourceType.Tech,
            ResourceType.Mutagen,
            ResourceType.LivingCrystals,
            ResourceType.Orichalcum,
            ResourceType.ProteanMutane
        };

        // __result is the returned tuple; its Item1 is the very List<ComparableData> the caller links to the
        // panel (a reference), so Add-ing to it here is visible to ShowStats without a `ref`. `item` and
        // `__instance` are injected by name/role from the original signature.
        [HarmonyPostfix]
        private static void Postfix(UIItemTooltip __instance, ItemDef item,
            (List<ComparableData>, List<List<ComparableData>>) __result)
        {
            try
            {
                if (!OracleMain.ShowDismantleCompensation)
                {
                    return; // feature disabled -> no Dismantle row
                }

                // Only the two item hover tooltips — never manufacturing (native scrap already shown) or the
                // phoenixpedia / mutation / equip-inventory tooltips.
                if (!(__instance is UITacItemTooltip) && !(__instance is UIGeoItemTooltip))
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
                    // Nothing recovered (all-zero manufacture cost) → no row at all.
                    return;
                }

                string resources = BuildResourceList(scrap);
                if (string.IsNullOrEmpty(resources))
                {
                    return;
                }

                __result.Item1.Add(new ComparableData
                {
                    // Name column: live I2 term → localized "Dismantle" / "Разбор" label (imported on enable).
                    localization = new LocalizedTextBind("ORACLE_ITEM_SCRAP"),
                    // Value column: the composed resource list (raw text, like the native "120/120" HP value).
                    primaryData = new StatData(resources, null, null)
                });
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ItemScrapTooltipPatch.Postfix failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Compose the yield string in <see cref="ScrapOrder"/>, e.g. "12 Materials, 3 Tech". Each resource is
        /// included only when its rounded amount is &gt;= 1 (ScrapPrice carries all six types, most at 0).
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
}
