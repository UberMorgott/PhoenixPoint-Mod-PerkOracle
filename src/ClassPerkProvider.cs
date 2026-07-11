using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Entities;
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
        /// The class ability track EXACTLY as the native progression row renders it for a soldier of
        /// <paramref name="spec"/>: one entry per level slot (index = level-1), NULLS PRESERVED.
        ///
        /// SOURCE ORDER — runtime first, def as fallback. The native screen renders the soldier's
        /// RUNTIME track (<c>_character.Progression.AbilityTracks</c>,
        /// AbilityTrackContainerElement.cs:147/251-261), a per-soldier SNAPSHOT of the spec's track def
        /// cloned at soldier creation (CharacterProgression.cs:104) and SERIALIZED into the save. The
        /// def layer is a mod battleground rewritten at enable time (TFTV MainSpecModification rewrites
        /// all class tracks; the Officer mod writes Assault[5]/Sniper[6] — ModHandler.cs:69-72), so
        /// def != runtime for existing soldiers (e.g. Sniper L7: runtime MarkedForDeath vs def Deadeye).
        /// Composition:
        ///   1. the viewing soldier's own runtime track when spec is their main (PrimaryClass track) or
        ///      secondary (SecondaryClass track) spec;
        ///   2. else the first faction soldier whose main (then secondary) spec matches — their snapshot;
        ///   3. else the CURRENT def track (what a soldier created right now would snapshot).
        /// Read live on every call — no caching. <paramref name="source"/> reports the layer used (for
        /// the RCA diagnostics). Empty list on any error.
        /// </summary>
        public static List<TacticalAbilityDef> GetClassTrackCells(SpecializationDef spec, GeoCharacter viewer,
            out string source)
        {
            source = "none";
            var cells = new List<TacticalAbilityDef>();
            try
            {
                if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null)
                {
                    return cells;
                }

                AbilityTrackSlot[] slots = ResolveRuntimeSlots(spec, viewer, ref source);
                if (slots == null
                    && (UnityEngine.Object)(object)spec.AbilityTrack != (UnityEngine.Object)null)
                {
                    slots = spec.AbilityTrack.AbilitiesByLevel;
                    source = "def";
                }
                if (slots == null)
                {
                    return cells;
                }
                foreach (AbilityTrackSlot slot in slots)
                {
                    cells.Add(slot?.Ability); // null kept: the native row shows an EMPTY cell there
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetClassTrackCells failed: " + ex.Message);
                cells.Clear();
            }
            return cells;
        }

        /// <summary>
        /// True when <paramref name="c"/> is a hand-placed story/DLC unit (e.g. DLC3 "Eileen",
        /// <c>S_SY_Eileen_CharacterTemplateDef</c>) rather than a generic recruit. Such a unit carries an
        /// individually authored personal/class track, so it must NOT stand in as the GENERIC class
        /// preview when picked as a roster representative. Signal is the game's own template flag: every
        /// recruitable soldier template has <c>AvailableForGenericDeployment == true</c> (verified: all
        /// 345 recruit templates), while unique story units (plus mutoids and the base template) have it
        /// false (extracted defs — Eileen = false). Cheap, exact, and no name/tag pattern guessing.
        ///
        /// NB — TFTV mercenaries (<c>Mercenary_*</c> templates) and Project Osiris revenants are NOT
        /// special: they are ordinary RECRUITED roster soldiers that level on the standard class track and
        /// keep <c>AvailableForGenericDeployment == true</c> (wiki-rca7 log: ТЯЖ/МИЛИШНИК/РАЗВЕДЧИК were
        /// caught only by the tag, never by the flag). An earlier build also flagged them via their
        /// <c>Mercenary_GameTagDef</c>/<c>OCPProduct_GameTagDef</c> template tag, which wrongly listed them
        /// as row-4 "unique units" and, worse, skipped every one of them as a class representative — so
        /// classes whose only roster soldiers were mercenaries (Heavy/Berserker in that log) collapsed to
        /// the def/config fallback. The tag branch is removed; the flag is the sole, precise signal.
        ///
        /// A false skip is safe: the caller then falls back to the class def/config. The VIEWER is exempt
        /// (the caller resolves the viewer first, so their own screen truth wins even if special).
        /// </summary>
        private static bool IsSpecialUnit(GeoCharacter c, out string reason)
        {
            reason = null;
            var tpl = c?.TemplateDef;
            if ((UnityEngine.Object)(object)tpl == (UnityEngine.Object)null)
            {
                return false;
            }
            if (!tpl.AvailableForGenericDeployment)
            {
                reason = "AvailableForGenericDeployment=false";
                return true;
            }
            return false;
        }

        /// <summary>Debug-only: note a roster representative skipped because it is a special/unique unit.
        /// Gated by <see cref="OracleMain.DebugLoggingEnabled"/> so it rides the same Row2/Row3 dumps.</summary>
        private static void LogSpecialSkip(GeoCharacter c, string row, string reason)
        {
            if (!OracleMain.DebugLoggingEnabled)
            {
                return;
            }
            string nm;
            try { nm = c?.GetName() ?? "[null]"; } catch { nm = "[?]"; }
            OracleLog.Debug("[Oracle] " + row + " skip representative '" + nm
                + "': special/unique unit (" + (reason ?? "special") + ")");
        }

        /// <summary>
        /// Find the runtime (per-soldier snapshot) track slots for <paramref name="spec"/>: the viewer's
        /// own tracks first, then any faction soldier with that main spec (exact native primary row),
        /// then any with it as secondary. Null when no live soldier carries the spec. Sets
        /// <paramref name="source"/> on a hit. Special/unique story units are skipped as representatives
        /// (see <see cref="IsSpecialUnit"/>) — the viewer above is the one exception.
        /// </summary>
        private static AbilityTrackSlot[] ResolveRuntimeSlots(SpecializationDef spec, GeoCharacter viewer,
            ref string source)
        {
            try
            {
                AbilityTrackSlot[] FromSoldier(GeoCharacter c)
                {
                    CharacterProgression p = c?.Progression;
                    if (p == null)
                    {
                        return null;
                    }
                    if ((UnityEngine.Object)(object)p.MainSpecDef == (UnityEngine.Object)(object)spec)
                    {
                        return p.GetAbilityTrack(AbilityTrackSource.PrimaryClass)?.AbilitiesByLevel;
                    }
                    if ((UnityEngine.Object)(object)p.SecondarySpecDef == (UnityEngine.Object)(object)spec)
                    {
                        return p.GetAbilityTrack(AbilityTrackSource.SecondaryClass)?.AbilitiesByLevel;
                    }
                    return null;
                }

                AbilityTrackSlot[] own = FromSoldier(viewer);
                if (own != null)
                {
                    source = "runtime:viewer";
                    return own;
                }

                GeoLevelController level = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
                GeoFaction faction = (UnityEngine.Object)(object)level != (UnityEngine.Object)null
                    ? level.PhoenixFaction
                    : null;
                if (faction == null)
                {
                    return null;
                }
                // Main-spec matches are the exact native primary row; prefer them across all soldiers
                // before settling for a secondary-track snapshot.
                foreach (GeoCharacter c in faction.HumanSoldiers)
                {
                    if (c?.Progression != null
                        && (UnityEngine.Object)(object)c.Progression.MainSpecDef == (UnityEngine.Object)(object)spec)
                    {
                        if (IsSpecialUnit(c, out string why))
                        {
                            LogSpecialSkip(c, "Row2", why);
                            continue;
                        }
                        AbilityTrackSlot[] slots = c.Progression
                            .GetAbilityTrack(AbilityTrackSource.PrimaryClass)?.AbilitiesByLevel;
                        if (slots != null)
                        {
                            source = "runtime:main";
                            return slots;
                        }
                    }
                }
                foreach (GeoCharacter c in faction.HumanSoldiers)
                {
                    if (IsSpecialUnit(c, out string why))
                    {
                        LogSpecialSkip(c, "Row2", why);
                        continue;
                    }
                    AbilityTrackSlot[] slots = FromSoldier(c);
                    if (slots != null)
                    {
                        source = "runtime:secondary";
                        return slots;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.ResolveRuntimeSlots failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// The runtime PERSONAL ability track of a soldier whose MAIN spec is <paramref name="spec"/>,
        /// EXACTLY as the native progression screen renders row 3. It is a per-soldier SNAPSHOT baked at
        /// creation and serialized into the save: TFTV's PersonalSpecModification writes
        /// <c>Progression.PersonalAbilities[i]</c> for <c>OrderOfPersonalPerks[i]</c>
        /// (refs/TFTV-src PersonalSpecModification.cs:90-125), so <c>AbilitiesByLevel[i].Ability</c> is the
        /// FIXED perk at row-3 slot <c>i</c>. The wiki row 3 shows a CLASS's fixed personal perks, which are
        /// class-derived at creation — a representative soldier of the class carries them verbatim, and that
        /// snapshot can diverge from the current TFTV config for existing (older-vintage) soldiers.
        /// Composition mirrors the class row: (1) the viewing soldier when their main spec matches
        /// (runtime:viewer), (2) else the first faction soldier with that main spec (runtime:roster), (3)
        /// else null so the caller falls back to the TFTV config. Matched by MAIN spec only (the personal
        /// track is one per soldier; its fixed slots come from the soldier's class). <paramref name="source"/>
        /// reports the layer used. Null when no live soldier of the class exists or on any error.
        /// </summary>
        public static AbilityTrackSlot[] GetPersonalTrackSlots(SpecializationDef spec, GeoCharacter viewer,
            out string source)
        {
            source = "none";
            try
            {
                if ((UnityEngine.Object)(object)spec == (UnityEngine.Object)null)
                {
                    return null;
                }

                AbilityTrackSlot[] FromSoldier(GeoCharacter c)
                {
                    CharacterProgression p = c?.Progression;
                    if (p == null
                        || (UnityEngine.Object)(object)p.MainSpecDef != (UnityEngine.Object)(object)spec)
                    {
                        return null;
                    }
                    return p.GetAbilityTrack(AbilityTrackSource.Personal)?.AbilitiesByLevel;
                }

                AbilityTrackSlot[] own = FromSoldier(viewer);
                if (own != null)
                {
                    source = "runtime:viewer";
                    return own;
                }

                GeoLevelController level = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
                GeoFaction faction = (UnityEngine.Object)(object)level != (UnityEngine.Object)null
                    ? level.PhoenixFaction
                    : null;
                if (faction == null)
                {
                    return null;
                }
                foreach (GeoCharacter c in faction.HumanSoldiers)
                {
                    if (IsSpecialUnit(c, out string why))
                    {
                        LogSpecialSkip(c, "Row3", why);
                        continue;
                    }
                    AbilityTrackSlot[] slots = FromSoldier(c);
                    if (slots != null)
                    {
                        source = "runtime:roster";
                        return slots;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ClassPerkProvider.GetPersonalTrackSlots failed: " + ex.Message);
            }
            return null;
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
