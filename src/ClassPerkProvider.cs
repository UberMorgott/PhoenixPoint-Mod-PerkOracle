using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Entities.Research.Reward;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Game-side adapter that turns a subclass <see cref="SpecializationDef"/> into the ordered,
    /// de-duplicated list of its guaranteed class-track perks, and enumerates the full subclass set
    /// so the picker patch can grey-inject the subclasses the screen omitted. The pure ordering/dedup
    /// lives in <see cref="ClassPerkResolver"/>; this class only reads the game's defs and resolves
    /// names. Every public method is guarded so a failure never breaks the host screen.
    /// </summary>
    public static class ClassPerkProvider
    {
        /// <summary>
        /// Ordered, de-duplicated guaranteed class-track perks for <paramref name="spec"/> (level order:
        /// the spec proficiency first, then each ability slot). Returns an empty list on null/missing
        /// inputs or any error. The result feeds <see cref="PerkWikiPanel.Open"/> directly.
        /// </summary>
        public static List<TacticalAbilityDef> GetClassPerks(SpecializationDef spec)
        {
            try
            {
                if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null
                    || (UnityEngine.Object)(object)spec.AbilityTrack == (UnityEngine.Object)null
                    || spec.AbilityTrack.AbilitiesByLevel == null)
                {
                    return new List<TacticalAbilityDef>();
                }

                // Build the ordered ability list directly from the class track. Proficiency first
                // (matches SpecializationDef.GetAbilitiesTillLevel), then each level slot's ability.
                var ordered = new List<TacticalAbilityDef>();
                ClassProficiencyAbilityDef prof = spec.GetSpecProficiency();
                if ((UnityEngine.Object)(object)prof != (UnityEngine.Object)null)
                {
                    ordered.Add(prof);
                }
                foreach (AbilityTrackSlot slot in spec.AbilityTrack.AbilitiesByLevel)
                {
                    if (slot != null && (UnityEngine.Object)(object)slot.Ability != (UnityEngine.Object)null)
                    {
                        ordered.Add(slot.Ability);
                    }
                }

                // Index the ordered defs by name, then run the pure resolver over the name order so the
                // dedup/skip logic stays in the tested core. name -> def via this local map.
                var byName = new Dictionary<string, TacticalAbilityDef>(StringComparer.Ordinal);
                var names = new List<string>(ordered.Count);
                foreach (TacticalAbilityDef def in ordered)
                {
                    string n = ((UnityEngine.Object)def).name;
                    if (string.IsNullOrEmpty(n))
                    {
                        continue;
                    }
                    if (!byName.ContainsKey(n))
                    {
                        byName[n] = def;
                    }
                    names.Add(n);
                }

                return ClassPerkResolver.Resolve(names,
                    n => byName.TryGetValue(n, out TacticalAbilityDef d) ? d : null);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetClassPerks failed: " + ex.Message);
                return new List<TacticalAbilityDef>();
            }
        }

        /// <summary>
        /// Every playable HUMAN SOLDIER class the wiki can browse: the selectable-subclass universe
        /// (faction initial specs + all class-research rewards), de-duplicated and filtered to specs that
        /// carry an icon (<see cref="SpecializationDef.ViewElementDef"/>) plus a valid ability track and
        /// proficiency. Non-soldier specs are excluded with the game's OWN discriminators: vehicle specs by
        /// <c>ClassTag == SharedGameTags.VehicleClassTag</c> (exactly how
        /// <c>SpecializedAbilityTrackPopupElement</c> strips the vehicle spec from
        /// <c>AvailableCharacterSpecializations</c>) and any spec the native dual-class/mutoid pickers
        /// refuse (<see cref="SpecializationDef.NotSecondClassSpecialization"/>). Empty list on any error.
        /// </summary>
        public static List<SpecializationDef> GetPlayableClasses()
        {
            try
            {
                ClassTagDef vehicleTag = null;
                try
                {
                    vehicleTag = GameUtl.GameComponent<SharedData>().SharedGameTags.VehicleClassTag;
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] VehicleClassTag lookup failed: " + ex.Message);
                }

                var seen = new HashSet<SpecializationDef>();
                var result = new List<SpecializationDef>();
                foreach (SpecializationDef spec in GetSelectableSubclassUniverse())
                {
                    if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null || !seen.Add(spec))
                    {
                        continue;
                    }
                    if ((UnityEngine.Object)(object)spec.ViewElementDef == (UnityEngine.Object)null
                        || (UnityEngine.Object)(object)spec.AbilityTrack == (UnityEngine.Object)null
                        || (UnityEngine.Object)(object)spec.GetSpecProficiency() == (UnityEngine.Object)null)
                    {
                        continue;
                    }
                    // Soldier-only: no vehicle specs (e.g. the Kaos Buggy class research reward), and
                    // nothing the game's own second-class pickers exclude.
                    if (spec.NotSecondClassSpecialization
                        || ((UnityEngine.Object)(object)vehicleTag != (UnityEngine.Object)null
                            && (UnityEngine.Object)(object)spec.ClassTag == (UnityEngine.Object)(object)vehicleTag))
                    {
                        continue;
                    }
                    result.Add(spec);
                }
                return result;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetPlayableClasses failed: " + ex.Message);
                return new List<SpecializationDef>();
            }
        }

        /// <summary>
        /// Subclasses present in the full game def set but NOT in <paramref name="shown"/> (the specs the
        /// picker actually displays). These are the "greyed/unresearched" entries to inject. Compared by
        /// reference. Returns an empty list on any error.
        /// </summary>
        public static List<SpecializationDef> GetOmittedSubclasses(IEnumerable<SpecializationDef> shown)
        {
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                if (repo == null)
                {
                    return new List<SpecializationDef>();
                }

                var shownSet = new HashSet<SpecializationDef>(shown ?? Enumerable.Empty<SpecializationDef>());

                // Already-unlocked/researched player classes (incl. the soldier's own main class). The
                // native picker offers a subset of these as the available SECOND classes; everything else
                // in this set is researched-but-not-offered (e.g. the soldier's main) and must NOT be
                // greyed-injected. Built from the faction's curated list; absent => empty (filter no-ops).
                HashSet<SpecializationDef> unlocked = GetUnlockedSpecializations();

                // AUTHORITATIVE universe of player second-class specializations: exactly the set the game
                // can add to a faction via AddSpecialization — the faction's InitialSpecializationDefs plus
                // every class-research reward (ClassResearchRewardDef.SpecializationDef). This is the same
                // pool that feeds AvailableCharacterSpecializations as classes are unlocked, so it cleanly
                // excludes non-player specs (Raider/Mutoid/Scum/Slug have neither initial nor research
                // entry) — fixing both the missing-class over-filtering AND the base-class duplicates that
                // the old GetAllDefs+heuristic approach produced.
                List<SpecializationDef> universe = GetSelectableSubclassUniverse();

                // Distinct by reference (singletons); the universe is already collision-free by class.
                var seen = new HashSet<SpecializationDef>();
                var omitted = new List<SpecializationDef>();
                foreach (SpecializationDef spec in universe)
                {
                    if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null
                        || shownSet.Contains(spec)        // already an active button
                        || unlocked.Contains(spec)        // researched (incl. the soldier's main) -> not "locked"
                        || !seen.Add(spec))               // de-dup the universe itself
                    {
                        continue;
                    }
                    // Mirror the native picker's filter (UIStateEditSoldier:608) + the InitSpecialization
                    // requirements (it dereferences the proficiency view element).
                    if (spec.NotSecondClassSpecialization
                        || (UnityEngine.Object)(object)spec.AbilityTrack == (UnityEngine.Object)null
                        || (UnityEngine.Object)(object)spec.GetSpecProficiency() == (UnityEngine.Object)null)
                    {
                        continue;
                    }
                    omitted.Add(spec);
                }
                return omitted;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetOmittedSubclasses failed: " + ex.Message);
                return new List<SpecializationDef>();
            }
        }

        /// <summary>
        /// The player faction's currently unlocked/researched specializations (its curated
        /// <c>AvailableCharacterSpecializations</c> list). Used to subtract already-available classes —
        /// including the edited soldier's own main class — from the greyed-injection candidates. Returns
        /// an empty set if the level/faction is not reachable (filter then simply does not subtract).
        /// </summary>
        private static HashSet<SpecializationDef> GetUnlockedSpecializations()
        {
            var set = new HashSet<SpecializationDef>();
            try
            {
                GeoLevelController level = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
                GeoFaction faction = (UnityEngine.Object)(object)level != (UnityEngine.Object)null
                    ? level.PhoenixFaction
                    : null;
                if (faction != null && faction.AvailableCharacterSpecializations != null)
                {
                    foreach (SpecializationDef spec in faction.AvailableCharacterSpecializations)
                    {
                        if ((UnityEngine.Object)(object)spec != (UnityEngine.Object)null)
                        {
                            set.Add(spec);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetUnlockedSpecializations failed: " + ex.Message);
            }
            return set;
        }

        /// <summary>
        /// The authoritative universe of player-selectable second-class specializations: the faction's
        /// <c>InitialSpecializationDefs</c> plus every <c>ClassResearchRewardDef.SpecializationDef</c>
        /// (the classes a research can unlock). This is exactly the set the game ever adds to a faction via
        /// <c>GeoFaction.AddSpecialization</c> (initial defs in Init + each <c>ClassResearchReward</c>),
        /// so it contains all real player classes and no Raider/Mutoid/Scum/Slug specs. Returns whatever is
        /// reachable; research rewards alone already cover every researchable class.
        /// </summary>
        private static List<SpecializationDef> GetSelectableSubclassUniverse()
        {
            var list = new List<SpecializationDef>();
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                if (repo != null)
                {
                    foreach (ClassResearchRewardDef rewardDef in repo.GetAllDefs<ClassResearchRewardDef>())
                    {
                        if (rewardDef != null
                            && (UnityEngine.Object)(object)rewardDef.SpecializationDef != (UnityEngine.Object)null)
                        {
                            list.Add(rewardDef.SpecializationDef);
                        }
                    }
                }

                // Initial classes (unlocked from the campaign start) — included for completeness.
                GeoLevelController level = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
                GeoFaction faction = (UnityEngine.Object)(object)level != (UnityEngine.Object)null
                    ? level.PhoenixFaction
                    : null;
                List<SpecializationDef> initial = faction?.Def?.InitialSpecializationDefs;
                if (initial != null)
                {
                    foreach (SpecializationDef spec in initial)
                    {
                        if ((UnityEngine.Object)(object)spec != (UnityEngine.Object)null)
                        {
                            list.Add(spec);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetSelectableSubclassUniverse failed: " + ex.Message);
            }
            return list;
        }
    }
}
