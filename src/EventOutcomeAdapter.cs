using System;
using Base.Core;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape;
using PhoenixPoint.Geoscape.Events;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewModules;

namespace Morgott.Oracle
{
    /// <summary>
    /// The only engine-aware layer: reads a real <see cref="GeoEventChoiceOutcome"/>'s previewable def
    /// fields into the pure <see cref="EventOutcomeData"/> DTO. NEVER calls <c>GenerateFactionReward</c>
    /// (that applies side-effects); only reads serialized def fields. Fully guarded so a malformed outcome
    /// yields an empty DTO, never throws.
    ///
    /// EVERY line it emits is sourced from the game's OWN reward UI (<c>UIModuleSiteEncounters.ShowReward</c>,
    /// decompile lines in brackets) so the preview reads identically to the native post-choice reward text in
    /// the current language. Two channels only:
    ///   • <see cref="EventOutcomeData.Resources"/> — native resource line [417]: localized name + signed value.
    ///   • <see cref="EventOutcomeData.NativeLines"/> — complete native reward sentences built from the live
    ///     module's own <c>LocalizedTextBind</c> keys / native concatenation (site reveals, faction-party
    ///     diplomacy, granted items, soldier/aircraft damage, tiredness, faction skill points).
    ///
    /// Outcome kinds the native reward UI does NOT render, or that need apply-time data absent from a static
    /// preview, are intentionally SKIPPED (no row), never shown as a raw codename / invented "xN":
    ///   • Granted research (GiveResearches)         — no ShowReward branch.
    ///   • Mission variables (VariablesChange)       — no ShowReward branch.
    ///   • Sub-faction mission weight                — no ShowReward branch.
    ///   • Zone damage (DamageZones)                 — native [476] needs a resolved GeoHavenZone + absolute
    ///                                                 value; the def carries only a keyword + percentage.
    ///   • Site-leader diplomacy (PartyType==SiteLeader) — native party is the site's haven leader, unknown
    ///                                                 pre-apply.
    ///   • Encounter-tag site reveals (SiteTag set)  — native resolves a specific live site's encounter Title
    ///                                                 at apply time (distance-ordered, non-deterministic).
    ///   • Haven-type site reveals (Type==Haven)     — native name is the runtime owner's localized faction
    ///                                                 name + " Haven"; not reproducible from the def alone.
    /// </summary>
    public static class EventOutcomeAdapter
    {
        /// <summary>Read an engine outcome into a pure DTO. Returns an empty DTO on null/error.</summary>
        public static EventOutcomeData From(GeoEventChoiceOutcome o)
        {
            var data = new EventOutcomeData();
            if (o == null)
            {
                return data;
            }

            try
            {
                UIModuleSiteEncounters mod = FindEncounterModule();

                // Resources (rounded int per unit, skip zero/None) — native resource reward line [417].
                // The game scales the raw def amount before granting it: under TFTV every event-outcome
                // resource is multiplied by 0.8 * ResourceMultiplierSetting in a GenerateFactionReward
                // Prefix, then displayed via ResourceUnit.RoundedValue = Mathf.RoundToInt(Value). We mirror
                // that here so the preview matches the actual reward (e.g. 625 -> 500, 100 -> 80), scaling
                // the raw float Value and rounding ONCE the same way. Multiplier is 1.0 when TFTV is absent.
                if (o.Resources != null && o.Resources.Values != null)
                {
                    float resourceMultiplier = TftvConfigBridge.EventResourceRewardMultiplier;
                    foreach (ResourceUnit ru in o.Resources.Values)
                    {
                        if (ru.Type == ResourceType.None)
                        {
                            continue;
                        }
                        int scaled = UnityEngine.Mathf.RoundToInt(ru.Value * resourceMultiplier);
                        if (scaled != 0)
                        {
                            data.Resources.Add(new EventOutcomeData.ResourceEntry(ResourceName(ru.Type), scaled));
                        }
                    }
                }

                // Everything below is a complete native reward SENTENCE; it requires the live encounter
                // module's serialized loc keys. With no module present we cannot source native text, so we
                // emit nothing rather than fall back to a codename.
                if ((UnityEngine.Object)(object)mod != (UnityEngine.Object)null)
                {
                    // Site reveals — native GenerateSitesRewardStrings [516]. Only type-based, non-haven
                    // reveals are reproducible from the def (text = GeoSiteType.ToString(), exactly the
                    // untranslated token the game itself shows, e.g. "Scavenging"); singular/plural and count
                    // mirror native (SiteRevealed / MultipleSiteRevealed). Tag-based and haven-type reveals
                    // are skipped (see class remarks).
                    if (o.RevealSites != null)
                    {
                        foreach (OutcomeSiteTag st in o.RevealSites)
                        {
                            AddRevealSiteNativeLine(data, mod, st);
                        }
                    }

                    // Faction-party diplomacy — native EncounterFactionDiplomacyChangedTextKey [392]. Only
                    // PartyType==Faction is reproducible (party + target are both defs); site-leader party is
                    // resolved at apply time and is skipped.
                    if (o.Diplomacy != null)
                    {
                        foreach (OutcomeDiplomacyChange d in o.Diplomacy)
                        {
                            AddDiplomacyNativeLine(data, mod, d);
                        }
                    }

                    // Granted items — native item reward line [401]: localized item name + " x " + count.
                    if (o.Items != null)
                    {
                        foreach (ItemUnit iu in o.Items)
                        {
                            AddItemNativeLine(data, mod, iu);
                        }
                    }

                    // Flat scalar effects — each a full native "lost/gained {0}" sentence from the module's
                    // own LocalizedTextBind with the magnitude substituted. Field -> key mapping (decompile
                    // UIModuleSiteEncounters.cs lines in brackets):
                    //   DamageCurrentSoldiers -> AircraftSoldiersInjuredTextKey [440]
                    //   DamageAllSoldiers     -> AllSoldierInjuredTextKey       [451]
                    //   TireCurrentSoldiers   -> AircraftSoldiersTiredTextKey   [446]
                    //   TireAllSoldiers       -> AllSoldierTiredTextKey         [456]
                    //   DamageCurrentAircraft -> AircraftDamageTextKey          [434]
                    //   FactionSkillPoints    -> AddSkillPointsTextKey          [466]
                    // HavenPopulationChange (needs the haven name) and SDIChange (no native reward line) are
                    // intentionally skipped rather than rendered with an invented label.
                    AddNativeLine(data, mod.AircraftSoldiersInjuredTextKey, o.DamageCurrentSoldiers);
                    AddNativeLine(data, mod.AllSoldierInjuredTextKey, o.DamageAllSoldiers);
                    AddNativeLine(data, mod.AircraftSoldiersTiredTextKey, o.TireCurrentSoldiers);
                    AddNativeLine(data, mod.AllSoldierTiredTextKey, o.TireAllSoldiers);
                    AddNativeLine(data, mod.AircraftDamageTextKey, o.DamageCurrentAircraft);
                    AddNativeLine(data, mod.AddSkillPointsTextKey, o.FactionSkillPoints);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.From failed: " + ex.Message);
            }

            return data;
        }

        /// <summary>
        /// Locate the live <see cref="UIModuleSiteEncounters"/> instance whose serialized LocalizedTextBind
        /// keys + ResourcesList we read to source native strings: prefer an active in-scene module, else any
        /// inactive scene instance (never a prefab asset). Returns null when none is present.
        /// </summary>
        private static UIModuleSiteEncounters FindEncounterModule()
        {
            try
            {
                UIModuleSiteEncounters module = UnityEngine.Object.FindObjectOfType<UIModuleSiteEncounters>();
                if ((UnityEngine.Object)(object)module != (UnityEngine.Object)null)
                {
                    return module;
                }
                foreach (UIModuleSiteEncounters m in UnityEngine.Resources.FindObjectsOfTypeAll<UIModuleSiteEncounters>())
                {
                    if ((UnityEngine.Object)(object)m != (UnityEngine.Object)null && m.gameObject.scene.IsValid())
                    {
                        return m;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.FindEncounterModule failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Append one native reward sentence for a flat scalar effect, sourced from the encounter module's own
        /// <paramref name="key"/> (a serialized <c>LocalizedTextBind</c>) with <paramref name="value"/>
        /// substituted via <c>string.Format</c> — identical text + language to the native line. No row is
        /// added when the value is 0 or the key is missing/empty (we never invent a label).
        /// </summary>
        private static void AddNativeLine(EventOutcomeData data, Base.UI.LocalizedTextBind key, int value)
        {
            try
            {
                if (value == 0 || key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                if (string.IsNullOrEmpty(pattern))
                {
                    return;
                }
                data.NativeLines.Add(string.Format(pattern, value));
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddNativeLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the native site-reveal sentence for a single <see cref="OutcomeSiteTag"/>, reproducing
        /// <c>UIModuleSiteEncounters.GenerateSitesRewardStrings</c> [516] for the case we can resolve from the
        /// def alone: a type-based, non-haven reveal. The name text is <c>GeoSiteType.ToString()</c> — the
        /// SAME untranslated token the game itself shows (e.g. "Scavenging") — and the singular/plural key +
        /// count match native exactly (SiteRevealedTextKey / MultipleSiteRevealedTextKey). Encounter-tag
        /// reveals (SiteTag set) and haven-type reveals are skipped: their native name needs a specific live
        /// site / runtime owner that does not exist before the choice is applied.
        /// </summary>
        private static void AddRevealSiteNativeLine(EventOutcomeData data, UIModuleSiteEncounters mod, OutcomeSiteTag st)
        {
            try
            {
                // Tag-based reveal: native resolves a live site's encounter Title at apply time. Skip.
                if (!string.IsNullOrEmpty(st.SiteTag))
                {
                    return;
                }
                // Type-based reveal. Haven uses the runtime owner's localized faction name (apply-time); None
                // is a non-type. Only a concrete, non-haven site type is reproducible from the def.
                if (st.Type == GeoSiteType.None || st.Type == GeoSiteType.Haven)
                {
                    return;
                }
                int count = st.Count == int.MaxValue ? 1 : st.Count;
                if (count <= 0)
                {
                    return;
                }

                string text = st.Type.ToString();
                Base.UI.LocalizedTextBind key = (count != 1) ? mod.MultipleSiteRevealedTextKey : mod.SiteRevealedTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                if (string.IsNullOrEmpty(pattern))
                {
                    return;
                }
                string line = (count != 1) ? string.Format(pattern, count, text) : string.Format(pattern, text);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddRevealSiteNativeLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the native faction-diplomacy sentence for a single <see cref="OutcomeDiplomacyChange"/>,
        /// reproducing <c>UIModuleSiteEncounters.ShowReward</c> [392]:
        /// <c>Format(EncounterFactionDiplomacyChangedTextKey, partyName, targetName, colouredSignedValue)</c>.
        /// Party and target names use the SAME source as the native line — <c>GeoFaction.ToString()</c> resolves
        /// to <c>Def.GeoFactionViewDef.Name.Localize()</c>, which we read directly off the def. Only
        /// <c>PartyType==Faction</c> is rendered (both party and target are defs); a site-leader party is
        /// resolved at apply time and is skipped.
        /// </summary>
        private static void AddDiplomacyNativeLine(EventOutcomeData data, UIModuleSiteEncounters mod, OutcomeDiplomacyChange d)
        {
            try
            {
                if (d.Value == 0 || d.PartyType != OutcomeDiplomacyChange.ChangeTarget.Faction)
                {
                    return;
                }
                if ((UnityEngine.Object)(object)d.PartyFaction == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)d.TargetFaction == (UnityEngine.Object)null)
                {
                    return;
                }
                string party = FactionViewName(d.PartyFaction);
                string target = FactionViewName(d.TargetFaction);
                if (string.IsNullOrEmpty(party) || string.IsNullOrEmpty(target))
                {
                    return;
                }
                Base.UI.LocalizedTextBind key = mod.EncounterFactionDiplomacyChangedTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                if (string.IsNullOrEmpty(pattern))
                {
                    return;
                }
                string value = Colorize(mod, d.Value.ToString("+#;-#"), d.Value > 0);
                string line = string.Format(pattern, party, target, value);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddDiplomacyNativeLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the native granted-item line for a single <see cref="ItemUnit"/>, reproducing
        /// <c>UIModuleSiteEncounters.ShowReward</c> [401]: localized display name + " x " + count (the count
        /// wrapped in the native positive-reward colour when the module exposes it). The display name uses the
        /// item's own <c>ViewElementDef.DisplayName1</c> — the same source the native line reads. No row when
        /// the item has no resolvable name.
        /// </summary>
        private static void AddItemNativeLine(EventOutcomeData data, UIModuleSiteEncounters mod, ItemUnit iu)
        {
            try
            {
                string name = ItemName(iu);
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }
                int count = ItemCount(iu);
                string countText = Colorize(mod, count.ToString(), positive: true);
                data.NativeLines.Add(name + " x " + countText);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddItemNativeLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Wrap <paramref name="raw"/> in the module's native positive/negative reward colour pattern
        /// (<c>PositiveRewardTextPattern</c> / <c>NegativeRewardTextPattern</c>, e.g.
        /// <c>&lt;color="#hex"&gt;{0}&lt;/color&gt;</c>) exactly as the native reward UI does. When the pattern is
        /// unavailable (not yet initialised), returns the raw text so the native SENTENCE still renders.
        /// </summary>
        private static string Colorize(UIModuleSiteEncounters mod, string raw, bool positive)
        {
            try
            {
                string pattern = positive ? mod.PositiveRewardTextPattern : mod.NegativeRewardTextPattern;
                if (!string.IsNullOrEmpty(pattern))
                {
                    return string.Format(pattern, raw);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.Colorize failed: " + ex.Message);
            }
            return raw;
        }

        /// <summary>
        /// Resource display name resolved EXACTLY like the native reward line
        /// (<c>UIModuleSiteEncounters.ShowReward</c>, decompile line 417): the live encounter module's
        /// <c>ResourcesList</c> NamedListDef is keyed by the resource's enum name and returns a
        /// <see cref="ViewElementDef"/> whose <c>DisplayName1</c> carries the localized, already-upper-cased
        /// label ("МАТЕРИАЛЫ", "ТЕХНОЛОГИИ"). The geoscape def and the enum name are kept only as defensive
        /// fallbacks.
        /// </summary>
        private static string ResourceName(ResourceType type)
        {
            // Primary: same NamedListDef + ViewElementDef.DisplayName1 the native reward text uses.
            try
            {
                UIModuleSiteEncounters module = FindEncounterModule();
                if ((UnityEngine.Object)(object)module != (UnityEngine.Object)null
                    && (UnityEngine.Object)(object)module.ResourcesList != (UnityEngine.Object)null)
                {
                    ViewElementDef ved = module.ResourcesList.GetDef<ViewElementDef>(type.ToString());
                    if ((UnityEngine.Object)(object)ved != (UnityEngine.Object)null && ved.DisplayName1 != null)
                    {
                        string s = ved.DisplayName1.Localize();
                        if (!string.IsNullOrEmpty(s))
                        {
                            return s;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.ResourceName (native) failed: " + ex.Message);
            }

            // Fallback: legacy geoscape resource view-element map (DisplayName usually empty for rewards).
            try
            {
                ResourceViewElementDef view = GameUtl.CurrentLevel()
                    .GetComponent<GeoLevelController>()
                    .View.GetProperResourceViewElementDef(type);
                if ((UnityEngine.Object)(object)view != (UnityEngine.Object)null && view.DisplayName != null)
                {
                    string s = view.DisplayName.Localize();
                    if (!string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.ResourceName (geoscape) failed: " + ex.Message);
            }

            return type.ToString();
        }

        /// <summary>
        /// Localized faction name as the native diplomacy line resolves it: <c>GeoFaction.ToString()</c> ==
        /// <c>Def.GeoFactionViewDef.Name.Localize()</c> [GeoFaction.cs:143]. We read the same
        /// <c>GeoFactionViewDef.Name</c> straight off the def. Returns empty (row skipped) when it cannot be
        /// resolved — never an invented codename.
        /// </summary>
        private static string FactionViewName(GeoFactionDef faction)
        {
            try
            {
                if ((UnityEngine.Object)(object)faction != (UnityEngine.Object)null
                    && (UnityEngine.Object)(object)faction.GeoFactionViewDef != (UnityEngine.Object)null
                    && faction.GeoFactionViewDef.Name != null)
                {
                    string s = faction.GeoFactionViewDef.Name.Localize();
                    if (!string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.FactionViewName failed: " + ex.Message);
            }
            return string.Empty;
        }

        /// <summary>
        /// Item display name resolved EXACTLY like the native reward line: native uses
        /// <c>CommonItemData.GetDisplayName()</c> == <c>ItemDef.GetDisplayName().Localize()</c>
        /// [ItemDef.cs:154], which prefers <c>ViewElementDef.DisplayName2</c> and falls back to
        /// <c>DisplayName1</c> — and NEVER to the raw def codename. We mirror that and return empty (row
        /// skipped) when neither localizes, so we never show a codename crutch.
        /// </summary>
        private static string ItemName(ItemUnit iu)
        {
            try
            {
                if ((UnityEngine.Object)(object)iu.ItemDef != (UnityEngine.Object)null
                    && (UnityEngine.Object)(object)iu.ItemDef.ViewElementDef != (UnityEngine.Object)null)
                {
                    Base.UI.LocalizedTextBind name2 = iu.ItemDef.ViewElementDef.DisplayName2;
                    Base.UI.LocalizedTextBind name = (name2 != null && !string.IsNullOrEmpty(name2.LocalizationKey))
                        ? name2
                        : iu.ItemDef.ViewElementDef.DisplayName1;
                    if (name != null)
                    {
                        string s = name.Localize();
                        if (!string.IsNullOrEmpty(s))
                        {
                            return s;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.ItemName failed: " + ex.Message);
            }
            return string.Empty;
        }

        private static int ItemCount(ItemUnit iu)
        {
            try
            {
                return iu.Quantity > 0 ? iu.Quantity : 1;
            }
            catch
            {
                return 1;
            }
        }
    }
}
