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

namespace Morgott.Oracle
{
    /// <summary>
    /// Adds the item's dismantle yield (<see cref="ItemDef.ScrapPrice"/> = floor(ManufactureX / 2) per resource
    /// type; an item with an all-zero manufacture cost yields nothing and gets no row) to the item hover
    /// tooltip.
    ///
    /// Preferred rendering is NATIVE icon rows: a localized "Dismantle" header row followed by one native stat
    /// row per non-zero resource in the form <c>[SmallIcon] [resource name] [amount]</c> — identical to the
    /// game's own damage-keyword rows, which already pass a sprite through <see cref="StatData"/>'s icon slot.
    /// The resource sprite + name come from the same <see cref="NamedListDef"/> the manufacturing screen's
    /// <c>ResourceIconContainer</c> uses (resolved once off any loaded container — see
    /// <see cref="ResolveResourcesDef"/>). This reuses the tooltip's own row prefab, pooling and layout rebuild,
    /// so there are no custom GameObjects, no hand-built layout, and no duplicate-row cleanup (native
    /// <c>LinkToData</c> hides surplus rows on every show).
    ///
    /// FALLBACK: if that def cannot be resolved in the current scene (e.g. a pure tactical session where the
    /// geoscape UI prefabs are not loaded), we degrade to a single TEXT row "Dismantle: 12 Materials, 3 Tech"
    /// — the mod's original behaviour. Exactly one of the two forms is emitted per show, never both.
    ///
    /// Injection point is <see cref="UIItemTooltip.GetItemData"/>, NOT the two ShowStats methods: the list it
    /// returns is exactly what ShowStats hands to <c>InfoPanel.LinkToData</c>, so appending our
    /// <see cref="ComparableData"/> rows here makes the panel render them through its own native stat machinery
    /// (StatPrefab clone, icon+name+value columns) with no re-link and no reflection. Appended rows land at the
    /// END of the list → bottom of the tooltip; the tooltip's own <c>FadeInCrt</c> runs
    /// <c>ForceRebuildLayoutImmediate</c> AFTER this, so added rows never break the box height.
    ///
    /// GetItemData is shared by six tooltips (tactical, geoscape, the equip/loadout armory's
    /// <see cref="UIInventoryTooltip"/>, mutation, manufacturing, phoenixpedia), so we gate on the instance
    /// type with a BLACKLIST — show the dismantle yield everywhere EXCEPT the two where it does not belong:
    /// <see cref="UIManufacturingTooltip"/> (already lists scrap natively, so a row here would duplicate it)
    /// and <see cref="UIPhoenixpediaItemTooltip"/> (codex/reference view, not gear management). This is why the
    /// equip-screen tooltip now shows the rows: it was previously off a whitelist of only the two hover
    /// tooltips. Fully guarded so a failure can never break the tooltip. Live-gated by
    /// <see cref="OracleMain.ShowDismantleCompensation"/>.
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

        // NamedListDef mapping ResourceType.ToString() -> ViewElementDef (icon + display name), the same def
        // ResourceIconContainer reads. Defs are never destroyed, so once found it stays valid; null means "not
        // resolvable in this scene yet" and we retry (and use the text fallback) on the next show.
        private static NamedListDef _resourcesDef;

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

                // Blacklist, not whitelist: show the dismantle yield on EVERY item tooltip that routes through
                // GetItemData (tactical, geoscape, the equip/loadout armory's UIInventoryTooltip, mutation) —
                // the user wants it in the item-properties window generally — EXCEPT two:
                //   * UIManufacturingTooltip  — already lists scrap natively (ScrapMaterials/ScrapTech rows), a
                //                                row here would duplicate it.
                //   * UIPhoenixpediaItemTooltip — the codex/reference view, not gear management; out of scope.
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
                    // Nothing recovered (all-zero manufacture cost) → no row at all.
                    return;
                }

                // Preferred: native icon rows. Falls through to the text row only if the resource defs are not
                // resolvable in this scene, or (defensively) if no resource had a non-zero amount.
                NamedListDef resDefs = ResolveResourcesDef();
                if (resDefs != null && AppendIconRows(__result.Item1, scrap, resDefs))
                {
                    return;
                }

                // Fallback: single text row "Dismantle: 12 Materials, 3 Tech".
                string resources = BuildResourceList(scrap);
                if (string.IsNullOrEmpty(resources))
                {
                    return;
                }

                __result.Item1.Add(new ComparableData
                {
                    localization = new LocalizedTextBind("ORACLE_ITEM_SCRAP"),
                    primaryData = new StatData(resources, null, null)
                });
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ItemScrapTooltipPatch.Postfix failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append a localized "Dismantle" header row plus one native <c>[icon] [name] [amount]</c> stat row per
        /// non-zero resource (in <see cref="ScrapOrder"/>). Icon + name come from the game's own resource
        /// <see cref="ViewElementDef"/>, with the mod's ORACLE_RES_* key as a name fallback. Returns false (so
        /// the caller can fall back to the text row) if no resource had a rounded amount &gt;= 1.
        /// </summary>
        private static bool AppendIconRows(List<ComparableData> list, ResourcePack scrap, NamedListDef resDefs)
        {
            var rows = new List<ComparableData>();
            foreach (ResourceType type in ScrapOrder)
            {
                int amount = Mathf.RoundToInt(scrap.ByResourceType(type).Value);
                if (amount <= 0)
                {
                    continue;
                }
                ViewElementDef ve = resDefs.GetDef<ViewElementDef>(type.ToString());
                LocalizedTextBind name =
                    (ve != null && ve.DisplayName1 != null && !string.IsNullOrEmpty(ve.DisplayName1.LocalizationKey))
                        ? ve.DisplayName1
                        : new LocalizedTextBind(ResourceKey(type));
                rows.Add(new ComparableData
                {
                    localization = name,
                    // Native icon slot (like damage-keyword rows): [SmallIcon] [name] [amount].
                    primaryData = new StatData(amount, null, ve != null ? ve.SmallIcon : null)
                });
            }
            if (rows.Count == 0)
            {
                return false;
            }

            // Header first (name-only row = section label), then the resource rows, all at the tooltip bottom.
            list.Add(new ComparableData
            {
                localization = new LocalizedTextBind("ORACLE_ITEM_SCRAP"),
                primaryData = new StatData(null, null, null)
            });
            list.AddRange(rows);
            return true;
        }

        /// <summary>
        /// Locate the <see cref="NamedListDef"/> that maps resource-type names to their
        /// <see cref="ViewElementDef"/> (icon + display name) — the same def the manufacturing screen's
        /// ResourceIconContainer uses. Read off any loaded container (<see cref="Resources.FindObjectsOfTypeAll"/>
        /// finds inactive/prefab instances too). Cached across shows; re-resolved while still null.
        /// </summary>
        private static NamedListDef ResolveResourcesDef()
        {
            if (_resourcesDef != null)
            {
                return _resourcesDef;
            }
            ResourceIconContainer src = Resources.FindObjectsOfTypeAll<ResourceIconContainer>()
                .FirstOrDefault(c => c != null && c.ResourcesDef != null);
            _resourcesDef = (src != null) ? src.ResourcesDef : null;
            return _resourcesDef;
        }

        /// <summary>
        /// Compose the yield string in <see cref="ScrapOrder"/>, e.g. "12 Materials, 3 Tech". Each resource is
        /// included only when its rounded amount is &gt;= 1 (ScrapPrice carries all six types, most at 0).
        /// Used only by the text fallback.
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

        /// <summary>The mod's own I2 loc key per resource type — name fallback for the icon rows when the
        /// game's own <see cref="ViewElementDef.DisplayName1"/> is unavailable.</summary>
        private static string ResourceKey(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Materials: return "ORACLE_RES_MATERIALS";
                case ResourceType.Tech: return "ORACLE_RES_TECH";
                case ResourceType.Mutagen: return "ORACLE_RES_MUTAGEN";
                case ResourceType.LivingCrystals: return "ORACLE_RES_LIVINGCRYSTALS";
                case ResourceType.Orichalcum: return "ORACLE_RES_ORICHALCUM";
                case ResourceType.ProteanMutane: return "ORACLE_RES_PROTEANMUTANE";
                default: return "ORACLE_ITEM_SCRAP";
            }
        }
    }
}
