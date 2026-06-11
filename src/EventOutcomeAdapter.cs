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
    /// fields into the pure <see cref="EventOutcomeData"/> DTO, resolving localized display names where
    /// the game exposes them. NEVER calls <c>GenerateFactionReward</c> (that applies side-effects); only
    /// reads serialized def fields. Fully guarded so a malformed outcome yields an empty DTO, never throws.
    /// Field names verified against decompile GeoEventChoiceOutcome.cs (see plan "Grounded facts").
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
                // Reputation / diplomacy.
                if (o.Diplomacy != null)
                {
                    foreach (OutcomeDiplomacyChange d in o.Diplomacy)
                    {
                        if (d.Value != 0)
                        {
                            data.Diplomacy.Add(new EventOutcomeData.DiplomacyEntry(FactionLabel(d.TargetFaction), d.Value));
                        }
                    }
                }

                // Resources (rounded int per unit, skip zero/None).
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

                // Items granted.
                if (o.Items != null)
                {
                    foreach (ItemUnit iu in o.Items)
                    {
                        string name = ItemName(iu);
                        int count = ItemCount(iu);
                        if (!string.IsNullOrEmpty(name))
                        {
                            data.Items.Add(new EventOutcomeData.ItemEntry(name, count));
                        }
                    }
                }

                // Granted research (raw id; best-effort display left to the game's own term, fall back to id).
                if (o.GiveResearches != null)
                {
                    foreach (string id in o.GiveResearches)
                    {
                        if (!string.IsNullOrEmpty(id))
                        {
                            data.Researches.Add(id);
                        }
                    }
                }

                // Site reveals.
                if (o.RevealSites != null)
                {
                    foreach (OutcomeSiteTag st in o.RevealSites)
                    {
                        string label = string.IsNullOrEmpty(st.SiteTag) ? st.Type.ToString() : st.SiteTag;
                        int count = st.Count == int.MaxValue ? 1 : st.Count;
                        data.RevealSites.Add(new EventOutcomeData.ItemEntry(label, count));
                    }
                }

                // Range-rolled variable changes.
                if (o.VariablesChange != null)
                {
                    foreach (OutcomeVariableChange vc in o.VariablesChange)
                    {
                        data.VariableChanges.Add(new EventOutcomeData.RangeEntry(
                            vc.VariableName ?? string.Empty, vc.Value.Min, vc.Value.Max));
                    }
                }

                // Range-rolled subfaction mission-weight changes.
                if (o.SubfactionFactionMissionWeight != null)
                {
                    foreach (OutcomeFactionMissionWeightChange mw in o.SubfactionFactionMissionWeight)
                    {
                        data.MissionWeightChanges.Add(new EventOutcomeData.RangeEntry(
                            SubFactionLabel(mw.SubFaction), mw.Value.Min, mw.Value.Max));
                    }
                }

                // Zone damage (% of zone max HP).
                if (o.DamageZones != null)
                {
                    foreach (OutcomeDamageZone dz in o.DamageZones)
                    {
                        if (dz.DamagePercentage != 0)
                        {
                            data.ZoneDamages.Add(new EventOutcomeData.ZoneDamageEntry(
                                dz.ZoneKeyword ?? string.Empty, dz.DamagePercentage));
                        }
                    }
                }

                // Flat scalars (real field names per decompile).
                data.DamageCurrentSoldiers = o.DamageCurrentSoldiers;
                data.DamageAllSoldiers = o.DamageAllSoldiers;
                data.TireCurrentSoldiers = o.TireCurrentSoldiers;
                data.TireAllSoldiers = o.TireAllSoldiers;
                data.DamageCurrentAircraft = o.DamageCurrentAircraft;
                data.FactionSkillPoints = o.FactionSkillPoints;
                data.HavenPopulationChange = o.HavenPopulationChange;
                data.SdiChange = o.SDIChange;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.From failed: " + ex.Message);
            }

            return data;
        }

        /// <summary>
        /// Resource display name resolved EXACTLY like the native reward line
        /// (<c>UIModuleSiteEncounters.ShowReward</c>, decompile line 417): the live encounter module's
        /// <c>ResourcesList</c> NamedListDef is keyed by the resource's enum name and returns a
        /// <see cref="ViewElementDef"/> whose <c>DisplayName1</c> carries the localized, already-upper-cased
        /// label ("МАТЕРИАЛЫ", "ТЕХНОЛОГИИ"). The OLD path used <c>GeoscapeView.GetProperResourceViewElementDef</c>,
        /// which returns the unrelated geoscape <c>ResourceViewElementDef</c> family whose <c>DisplayName</c>
        /// LocalizedTextBind is empty for these resources -> <c>.Localize()</c> yielded "" -> we fell back to
        /// the raw enum name. We now mirror the native source, with that geoscape def and finally the enum
        /// name kept only as defensive fallbacks.
        /// </summary>
        private static string ResourceName(ResourceType type)
        {
            // Primary: same NamedListDef + ViewElementDef.DisplayName1 the native reward text uses.
            try
            {
                UIModuleSiteEncounters module = UnityEngine.Object.FindObjectOfType<UIModuleSiteEncounters>();
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
        /// Faction display label. <see cref="GeoFactionDef"/> exposes no ViewElementDef; its
        /// <see cref="PPFactionDef"/> carries the human name via <c>GetName()</c> (strips the "_FactionDef"
        /// suffix). Falls back to the def's own name, then a generic "Reputation" label.
        /// </summary>
        private static string FactionLabel(GeoFactionDef faction)
        {
            try
            {
                if ((UnityEngine.Object)(object)faction != (UnityEngine.Object)null)
                {
                    if ((UnityEngine.Object)(object)faction.PPFactionDef != (UnityEngine.Object)null)
                    {
                        string name = faction.PPFactionDef.GetName();
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                    string defName = faction.name;
                    if (!string.IsNullOrEmpty(defName))
                    {
                        return defName;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.FactionLabel failed: " + ex.Message);
            }
            return Loc.Get("ORACLE_OUTCOME_REPUTATION", "Reputation");
        }

        private static string SubFactionLabel(PhoenixPoint.Geoscape.Levels.Factions.GeoSubFactionDef sub)
        {
            try
            {
                if ((UnityEngine.Object)(object)sub != (UnityEngine.Object)null)
                {
                    return sub.name;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventOutcomeAdapter.SubFactionLabel failed: " + ex.Message);
            }
            return Loc.Get("ORACLE_OUTCOME_MISSIONWEIGHT", "Mission Weight");
        }

        private static string ItemName(ItemUnit iu)
        {
            try
            {
                if ((UnityEngine.Object)(object)iu.ItemDef != (UnityEngine.Object)null)
                {
                    if ((UnityEngine.Object)(object)iu.ItemDef.ViewElementDef != (UnityEngine.Object)null
                        && iu.ItemDef.ViewElementDef.DisplayName1 != null)
                    {
                        string s = iu.ItemDef.ViewElementDef.DisplayName1.Localize();
                        if (!string.IsNullOrEmpty(s))
                        {
                            return s;
                        }
                    }
                    return iu.ItemDef.name;
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
