using System;
using System.Collections.Generic;
using Base.Core;
using Base.Defs;
using Base.Utils;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Geoscape.Events;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities;

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

                    // VariablesChange — no native ShowReward branch. Author "Variable change: [Min..Max]"
                    // per entry, NEVER a fabricated single roll (RangeDataInt is rolled at apply time).
                    if (o.VariablesChange != null)
                    {
                        foreach (OutcomeVariableChange vc in o.VariablesChange)
                        {
                            AddVariableRangeLine(data, vc);
                        }
                    }

                    // SubfactionFactionMissionWeight — List<OutcomeFactionMissionWeightChange>; no native
                    // branch. Author "Mission weight: [Min..Max]" per entry from its RangeDataInt.
                    if (o.SubfactionFactionMissionWeight != null)
                    {
                        foreach (OutcomeFactionMissionWeightChange wc in o.SubfactionFactionMissionWeight)
                        {
                            AddMissionWeightRangeLine(data, wc);
                        }
                    }

                    // GiveResearches — list of research id strings; no native branch. Resolve each id to its
                    // ResearchDef display name and author "Research: <name>, <name>". Ids that don't resolve
                    // are dropped (never shown as a raw codename).
                    if (o.GiveResearches != null && o.GiveResearches.Count > 0)
                    {
                        AddResearchNamesLine(data, o.GiveResearches);
                    }

                    // GivePhoenixpediaEntries — list of GeoPhoenixpediaEntryDef; no native branch. Author
                    // "Phoenixpedia: <title>, <title>" from each entry's localized Entry.Title.
                    if (o.GivePhoenixpediaEntries != null && o.GivePhoenixpediaEntries.Count > 0)
                    {
                        AddPhoenixpediaNamesLine(data, o.GivePhoenixpediaEntries);
                    }

                    // StartMission — OutcomeStartMission; no native branch. Author "Mission: <type>" using the
                    // mission-type def name (a def-derived token, not a live mission instance name).
                    if (o.StartMission != null)
                    {
                        AddMissionLine(data, o.StartMission);
                    }

                    // Follow-up event chain — TriggerEncounterID (string) + SetEvents[].EventID (string).
                    // No native branch. Author "Leads to: <id>, <id>" (raw event id; no localized title is
                    // resolvable from the choice pre-apply, and the id is the same token the game keys on).
                    AddFollowUpEventsLine(data, o.TriggerEncounterID, o.SetEvents);

                    // SDIChange — int. Native SDIIncreaseTextKey/SDIDecreaseTextKey exist but ShowReward
                    // never renders them; we BORROW them (resolved design): pick by sign, substitute the
                    // absolute value. No row when the change is 0 or the chosen key is missing.
                    AddSdiLine(data, mod, o.SDIChange);

                    // GameOverVictoryFaction — GeoFactionDef; means "win the game". Author "Victory for
                    // <faction>" using the same GeoFactionViewDef.Name source the diplomacy lines use.
                    if ((UnityEngine.Object)(object)o.GameOverVictoryFaction != (UnityEngine.Object)null)
                    {
                        AddVictoryLine(data, o.GameOverVictoryFaction);
                    }

                    // DamageZones — List<OutcomeDamageZone> { ZoneKeyword(string), DamagePercentage(int) }.
                    // The native HavenZoneDamageTextKey needs a live zone's absolute HP; the def carries only
                    // a percentage, so we AUTHOR "<pct>% damage to <zoneKeyword>" (a % is the only def-true
                    // figure). No absolute HP (resolved at apply time) is shown.
                    if (o.DamageZones != null)
                    {
                        foreach (OutcomeDamageZone dz in o.DamageZones)
                        {
                            AddZoneDamageLine(data, dz);
                        }
                    }

                    // Units + CustomCharacters — List<TacCharacterDef>; recruited soldiers. Live name is
                    // apply-time, so substitute a def-derived token (count + class/template display) into the
                    // native RecruitSingleSoldierTextKey ({0}=name). Resolved design: count + class, no name.
                    AddRecruitLines(data, mod, o.Units);
                    AddRecruitLines(data, mod, o.CustomCharacters);

                    // SetMaxDiplomacyStates — List<OutcomeMaxDiplomacyStateChange>; native
                    // NewDiplomaticStateTextKey ({0}=faction name). Read the entry's faction def. NOTE: the
                    // decompiled struct names the GeoFactionDef member "FacionDef" (engine typo), not "Faction".
                    if (o.SetMaxDiplomacyStates != null)
                    {
                        foreach (OutcomeMaxDiplomacyStateChange ms in o.SetMaxDiplomacyStates)
                        {
                            AddFactionKeyLine(data, mod.NewDiplomaticStateTextKey, ms.FacionDef);
                        }
                    }

                    // SetDiplomaticObjectives — List<OutcomeSetDiplomaticObjective>; native
                    // NewDiplomaticObjectiveTextKey ({0}=faction name). The GeoFactionDef member is
                    // "WithFaction" (confirmed at the decompiled struct), not "Faction".
                    if (o.SetDiplomaticObjectives != null)
                    {
                        foreach (OutcomeSetDiplomaticObjective dobj in o.SetDiplomaticObjectives)
                        {
                            AddFactionKeyLine(data, mod.NewDiplomaticObjectiveTextKey, dobj.WithFaction);
                        }
                    }

                    // ConvertSiteToPhoenixBase — bool. Native NewPhoenixBaseTextKey ({0}=live base site name).
                    // The site is apply-time; substitute a generic authored token.
                    if (o.ConvertSiteToPhoenixBase)
                    {
                        AddConvertBaseLine(data, mod);
                    }

                    // HavenPopulationChange — int. Native HavenPopulationChangeTextKey ({0}=live haven name,
                    // {1}=int delta). Haven is apply-time; substitute a generic haven token + the signed delta.
                    if (o.HavenPopulationChange != 0)
                    {
                        AddHavenPopulationLine(data, mod, o.HavenPopulationChange);
                    }

                    // SpawnAlienHavenAttacksNearPalace — int (count). Native MultipleHavensAttackedTextKey
                    // takes ZERO placeholders (a plain .Localize() presence line). Emit it when count > 0.
                    if (o.SpawnAlienHavenAttacksNearPalace > 0)
                    {
                        AddHavenAttacksLine(data, mod);
                    }
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
                if (d.Value == 0)
                {
                    return;
                }
                bool isLeader = d.PartyType == OutcomeDiplomacyChange.ChangeTarget.SiteLeader;
                if ((UnityEngine.Object)(object)d.TargetFaction == (UnityEngine.Object)null
                    || (!isLeader && (UnityEngine.Object)(object)d.PartyFaction == (UnityEngine.Object)null))
                {
                    return;
                }
                // The game scales the raw def reputation before granting it: under TFTV a GenerateFactionReward
                // Prefix conditionally multiplies OutcomeDiplomacyChange.Value (Void Omen #2 halves it, e.g.
                // 4 -> 2; easy/penalties double it; VO#8 scales penalties). Mirror that here so the preview
                // matches the actual grant. Multiplier is identity when TFTV is absent or no flag applies.
                int scaled = TftvConfigBridge.EventDiplomacyFinalValue(d);
                if (scaled == 0)
                {
                    return;
                }
                string value = Colorize(mod, scaled.ToString("+#;-#"), scaled > 0);
                string target = FactionViewName(d.TargetFaction);
                if (string.IsNullOrEmpty(target))
                {
                    return;
                }

                string line;
                if (isLeader)
                {
                    // Site-leader party is the live haven leader (unknown pre-apply); substitute a generic
                    // authored token into the native 2-arg EncounterLeaderDiplomacyChangedTextKey
                    // ({0}=leader, {1}=signed value). The target name is itself the live leader, so the native
                    // key's single name slot receives the generic token + colored value.
                    Base.UI.LocalizedTextBind leaderKey = mod.EncounterLeaderDiplomacyChangedTextKey;
                    if (leaderKey == null || string.IsNullOrEmpty(leaderKey.LocalizationKey))
                    {
                        return;
                    }
                    string leaderPattern = leaderKey.Localize();
                    if (string.IsNullOrEmpty(leaderPattern))
                    {
                        return;
                    }
                    string leaderToken = Loc.Get("ORACLE_EVT_TOK_HAVEN_LEADER", "haven leader");
                    line = string.Format(leaderPattern, leaderToken, value);
                }
                else
                {
                    string party = FactionViewName(d.PartyFaction);
                    if (string.IsNullOrEmpty(party))
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
                    line = string.Format(pattern, party, target, value);
                }
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
        /// Append an authored variable-change line "Variable change: [Min..Max]" using the
        /// <c>RangeDataInt</c> bounds straight off the def. Shows a single value when Min == Max.
        /// Skips zero-only changes where Min == Max == 0. Localized pattern via Loc (English fallback).
        /// </summary>
        private static void AddVariableRangeLine(EventOutcomeData data, OutcomeVariableChange vc)
        {
            try
            {
                int min = vc.Value.Min;
                int max = vc.Value.Max;
                if (min == 0 && max == 0)
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_VARIABLE", "Variable change: {0}");
                string line = EventOutcomeFormat.Format1(pattern, EventOutcomeFormat.Range(min, max));
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddVariableRangeLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append an authored sub-faction mission-weight line "Mission weight: [Min..Max]" from the
        /// entry's RangeDataInt. Collapses when Min == Max; skips a 0..0 change. The range field is read
        /// inside the try so an unexpected struct shape simply yields no row rather than throwing.
        /// Confirmed at the decompiled struct: the RangeDataInt member is <c>Value</c> (struct also has
        /// <c>SubFaction</c>), NOT <c>Weight</c> as an earlier draft assumed.
        /// </summary>
        private static void AddMissionWeightRangeLine(EventOutcomeData data, OutcomeFactionMissionWeightChange wc)
        {
            try
            {
                RangeDataInt range = wc.Value;
                int min = range.Min;
                int max = range.Max;
                if (min == 0 && max == 0)
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_MISSIONWEIGHT", "Mission weight: {0}");
                string line = EventOutcomeFormat.Format1(pattern, EventOutcomeFormat.Range(min, max));
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddMissionWeightRangeLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append an authored "Research: name, name" line by resolving each research id to its
        /// <c>ResearchDef.ViewElementDef.DisplayName1</c> (the same localized name the research UI shows).
        /// Unresolvable ids are skipped; no row when none resolve. Read-only def lookup, never the apply path.
        /// </summary>
        private static void AddResearchNamesLine(EventOutcomeData data, List<string> researchIds)
        {
            try
            {
                var names = new List<string>();
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                foreach (string id in researchIds)
                {
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }
                    string name = ResearchDisplayName(repo, id);
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
                }
                string joined = EventOutcomeFormat.JoinNames(names, ", ");
                if (string.IsNullOrEmpty(joined))
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_RESEARCH", "Research: {0}");
                string line = EventOutcomeFormat.Format1(pattern, joined);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddResearchNamesLine failed: " + ex.Message);
            }
        }

        /// <summary>Resolve a research id to its localized display name; empty if not found / no name.</summary>
        private static string ResearchDisplayName(DefRepository repo, string id)
        {
            try
            {
                foreach (ResearchDef rd in repo.GetAllDefs<ResearchDef>())
                {
                    if (rd != null && rd.Id == id
                        && (UnityEngine.Object)(object)rd.ViewElementDef != (UnityEngine.Object)null
                        && rd.ViewElementDef.DisplayName1 != null)
                    {
                        string s = rd.ViewElementDef.DisplayName1.Localize();
                        if (!string.IsNullOrEmpty(s))
                        {
                            return s;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.ResearchDisplayName failed: " + ex.Message);
            }
            return string.Empty;
        }

        /// <summary>
        /// Append an authored "Phoenixpedia: title, title" line from each entry's localized
        /// <c>GeoPhoenixpediaEntryDef.Entry.Title</c>. Entries without a resolvable title are skipped.
        /// </summary>
        private static void AddPhoenixpediaNamesLine(EventOutcomeData data, List<GeoPhoenixpediaEntryDef> entries)
        {
            try
            {
                var names = new List<string>();
                foreach (GeoPhoenixpediaEntryDef e in entries)
                {
                    if ((UnityEngine.Object)(object)e == (UnityEngine.Object)null
                        || e.Entry == null || e.Entry.Title == null)
                    {
                        continue;
                    }
                    string s = e.Entry.Title.Localize();
                    if (!string.IsNullOrEmpty(s))
                    {
                        names.Add(s);
                    }
                }
                string joined = EventOutcomeFormat.JoinNames(names, ", ");
                if (string.IsNullOrEmpty(joined))
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_PHOENIXPEDIA", "Phoenixpedia: {0}");
                string line = EventOutcomeFormat.Format1(pattern, joined);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddPhoenixpediaNamesLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append an authored "Mission: <type>" line from <c>OutcomeStartMission.MissionTypeDef.name</c>
        /// (the def-derived mission-type token; no live mission instance exists pre-apply). No row when the
        /// mission type def is missing.
        /// </summary>
        private static void AddMissionLine(EventOutcomeData data, OutcomeStartMission mission)
        {
            try
            {
                if ((UnityEngine.Object)(object)mission.MissionTypeDef == (UnityEngine.Object)null)
                {
                    return;
                }
                string token = mission.MissionTypeDef.name;
                if (string.IsNullOrEmpty(token))
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_MISSION", "Mission: {0}");
                string line = EventOutcomeFormat.Format1(pattern, token);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddMissionLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append an authored "Leads to: id, id" line from the follow-up event chain: the single
        /// <c>TriggerEncounterID</c> plus every <c>OutcomeEncounterSet.EventID</c>. Both are raw event id
        /// strings (no def / localized title is resolvable from the choice pre-apply). Empty ids are skipped;
        /// no row when none are present.
        /// </summary>
        private static void AddFollowUpEventsLine(EventOutcomeData data, string triggerId, List<OutcomeEncounterSet> setEvents)
        {
            try
            {
                var ids = new List<string>();
                if (!string.IsNullOrEmpty(triggerId))
                {
                    ids.Add(triggerId);
                }
                if (setEvents != null)
                {
                    foreach (OutcomeEncounterSet es in setEvents)
                    {
                        if (!string.IsNullOrEmpty(es.EventID))
                        {
                            ids.Add(es.EventID);
                        }
                    }
                }
                string joined = EventOutcomeFormat.JoinNames(ids, ", ");
                if (string.IsNullOrEmpty(joined))
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_FOLLOWUP", "Leads to: {0}");
                string line = EventOutcomeFormat.Format1(pattern, joined);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddFollowUpEventsLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the SDI-change line by borrowing the native <c>SDIIncreaseTextKey</c> /
        /// <c>SDIDecreaseTextKey</c> (selected by sign) with the absolute value substituted. These keys
        /// exist on the module but its ShowReward never renders them; reusing them keeps the wording + language
        /// native with no new authored string. No row when the value is 0 or the key is missing/empty.
        /// </summary>
        private static void AddSdiLine(EventOutcomeData data, UIModuleSiteEncounters mod, int sdi)
        {
            try
            {
                if (sdi == 0)
                {
                    return;
                }
                Base.UI.LocalizedTextBind key = sdi > 0 ? mod.SDIIncreaseTextKey : mod.SDIDecreaseTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                string line = EventOutcomeFormat.Format1(pattern, System.Math.Abs(sdi));
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddSdiLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append an authored "Victory for &lt;faction&gt;" line for a <c>GameOverVictoryFaction</c> (the
        /// win-the-game outcome), reusing <see cref="FactionViewName"/> (the localized GeoFactionViewDef.Name).
        /// No row when the faction name cannot be resolved.
        /// </summary>
        private static void AddVictoryLine(EventOutcomeData data, GeoFactionDef faction)
        {
            try
            {
                string name = FactionViewName(faction);
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_VICTORY", "Victory for {0}");
                string line = EventOutcomeFormat.Format1(pattern, name);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddVictoryLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append an authored "&lt;pct&gt;% damage to &lt;zone&gt;" line for one <see cref="OutcomeDamageZone"/>.
        /// Uses the def's <c>DamagePercentage</c> (the only def-true figure; native absolute HP needs a live
        /// zone) and the raw <c>ZoneKeyword</c> token. No row when the percentage is 0 or the keyword is empty.
        /// </summary>
        private static void AddZoneDamageLine(EventOutcomeData data, OutcomeDamageZone dz)
        {
            try
            {
                if (dz.DamagePercentage == 0 || string.IsNullOrEmpty(dz.ZoneKeyword))
                {
                    return;
                }
                string pattern = Loc.Get("ORACLE_EVT_ZONE_DAMAGE", "{0}% damage to {1}");
                if (string.IsNullOrEmpty(pattern))
                {
                    return;
                }
                string line = string.Format(pattern, dz.DamagePercentage, dz.ZoneKeyword);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddZoneDamageLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append a recruit line per <see cref="TacCharacterDef"/> using the native
        /// <c>RecruitSingleSoldierTextKey</c> ({0}=name). The live soldier name does not exist pre-apply, so
        /// we substitute a def-derived token: the character's <c>GetViewElementDef().DisplayName1</c> (class /
        /// template display). Defs without a resolvable display name are skipped (never a raw codename).
        /// </summary>
        private static void AddRecruitLines(EventOutcomeData data, UIModuleSiteEncounters mod, System.Collections.Generic.List<TacCharacterDef> chars)
        {
            try
            {
                if (chars == null || chars.Count == 0)
                {
                    return;
                }
                Base.UI.LocalizedTextBind key = mod.RecruitSingleSoldierTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                if (string.IsNullOrEmpty(pattern))
                {
                    return;
                }
                foreach (TacCharacterDef def in chars)
                {
                    if ((UnityEngine.Object)(object)def == (UnityEngine.Object)null)
                    {
                        continue;
                    }
                    string token = CharacterDisplayToken(def);
                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }
                    string line = string.Format(pattern, token);
                    if (!string.IsNullOrEmpty(line))
                    {
                        data.NativeLines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddRecruitLines failed: " + ex.Message);
            }
        }

        /// <summary>Def-derived display token for a recruited character: ViewElementDef.DisplayName1; empty if none.</summary>
        private static string CharacterDisplayToken(TacCharacterDef def)
        {
            try
            {
                ViewElementDef ved = def.GetViewElementDef();
                if ((UnityEngine.Object)(object)ved != (UnityEngine.Object)null && ved.DisplayName1 != null)
                {
                    string s = ved.DisplayName1.Localize();
                    if (!string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.CharacterDisplayToken failed: " + ex.Message);
            }
            return string.Empty;
        }

        /// <summary>
        /// Append a 1-arg native line whose only placeholder is a faction display name, for the
        /// new-diplomatic-state / new-diplomatic-objective outcomes. Resolves the name via
        /// <see cref="FactionViewName"/> (same source the native lines use). No row when the key or the
        /// faction name is missing.
        /// </summary>
        private static void AddFactionKeyLine(EventOutcomeData data, Base.UI.LocalizedTextBind key, GeoFactionDef faction)
        {
            try
            {
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string name = FactionViewName(faction);
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }
                string pattern = key.Localize();
                string line = EventOutcomeFormat.Format1(pattern, name);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddFactionKeyLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the convert-to-Phoenix-base line using the native <c>NewPhoenixBaseTextKey</c> ({0}=base
        /// site name). The actual site is resolved at apply time, so a generic authored token ("a Phoenix
        /// base") fills the name slot. No row when the key is missing.
        /// </summary>
        private static void AddConvertBaseLine(EventOutcomeData data, UIModuleSiteEncounters mod)
        {
            try
            {
                Base.UI.LocalizedTextBind key = mod.NewPhoenixBaseTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                string token = Loc.Get("ORACLE_EVT_TOK_BASE", "a Phoenix base");
                string line = EventOutcomeFormat.Format1(pattern, token);
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddConvertBaseLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the haven-population line using the native 2-arg <c>HavenPopulationChangeTextKey</c>
        /// ({0}=haven name, {1}=delta). The haven is resolved at apply time, so a generic authored token fills
        /// the name slot; the delta is the signed def value. No row when the value is 0 or the key is missing.
        /// </summary>
        private static void AddHavenPopulationLine(EventOutcomeData data, UIModuleSiteEncounters mod, int delta)
        {
            try
            {
                if (delta == 0)
                {
                    return;
                }
                Base.UI.LocalizedTextBind key = mod.HavenPopulationChangeTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string pattern = key.Localize();
                if (string.IsNullOrEmpty(pattern))
                {
                    return;
                }
                string token = Loc.Get("ORACLE_EVT_TOK_HAVEN", "a haven");
                string line = string.Format(pattern, token, EventOutcomeFormat.Signed(delta));
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddHavenPopulationLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Append the "multiple havens attacked" presence line. The native
        /// <c>MultipleHavensAttackedTextKey</c> is rendered by ShowReward with NO placeholders (a plain
        /// <c>.Localize()</c>), so we emit the localized string verbatim — no value substitution. No row when
        /// the key is missing.
        /// </summary>
        private static void AddHavenAttacksLine(EventOutcomeData data, UIModuleSiteEncounters mod)
        {
            try
            {
                Base.UI.LocalizedTextBind key = mod.MultipleHavensAttackedTextKey;
                if (key == null || string.IsNullOrEmpty(key.LocalizationKey))
                {
                    return;
                }
                string line = key.Localize();
                if (!string.IsNullOrEmpty(line))
                {
                    data.NativeLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.AddHavenAttacksLine failed: " + ex.Message);
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
