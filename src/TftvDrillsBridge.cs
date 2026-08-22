using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Tactical.Entities.Abilities;

namespace Morgott.Oracle
{
    /// <summary>
    /// One drill's static unlock requirement, converted out of TFTV's
    /// <c>DrillsUnlock+DrillUnlockCondition</c> into a type PerkOracle owns, so no TFTV type ever
    /// leaves this file. Drills TFTV never registered a condition for are reported as
    /// <see cref="AlwaysAvailable"/> — that is exactly how <c>IsDrillUnlocked</c> treats them
    /// (refs/TFTV-src TFTVDrills/DrillsUnlock.cs:104).
    /// </summary>
    public sealed class DrillRequirementInfo
    {
        public TacticalAbilityDef Drill;
        public bool AlwaysAvailable;
        /// <summary>Research def ids that must all be completed.</summary>
        public List<string> RequiredResearchIds = new List<string>();
        /// <summary>Class/level gates; a null <c>ClassTag</c> means "any class at this level".</summary>
        public List<DrillClassLevelInfo> ClassLevels = new List<DrillClassLevelInfo>();
        /// <summary>Each group is an OR-set of proficiency abilities; the soldier needs one from every group.</summary>
        public List<List<TacticalAbilityDef>> ProficiencyGroups = new List<List<TacticalAbilityDef>>();
    }

    /// <summary>Class/level gate of a drill. <c>ClassTag == null</c> ⇒ any class.</summary>
    public sealed class DrillClassLevelInfo
    {
        public ClassTagDef ClassTag;
        public int MinimumLevel;
    }

    /// <summary>
    /// Reflection bridge into TFTV's Drills system (<c>TFTV.TFTVDrills.DrillsDefs</c> /
    /// <c>DrillsUnlock</c>, both <c>internal</c>). Resolves once, lazily, caches the members and
    /// the converted requirement table, and degrades gracefully: with TFTV absent — or with its
    /// API moved — every accessor returns null/empty/false and the failure is logged once at
    /// debug level. Same shape and conventions as <see cref="TftvConfigBridge"/>.
    /// </summary>
    public static class TftvDrillsBridge
    {
        private static bool _resolved;
        private static bool _available;

        private static FieldInfo _drillsField;                 // DrillsDefs.Drills : List<TacticalAbilityDef>
        private static FieldInfo _conditionsField;             // DrillsUnlock.DrillUnlockConditions (private static)
        private static MethodInfo _getAvailableDrills;
        private static MethodInfo _isDrillUnlocked;
        private static MethodInfo _characterHasDrill;
        private static MethodInfo _hasTrainingFacility;
        private static MethodInfo _wouldBreakProficiency;
        private static MethodInfo _missingRequirements;
        private static MethodInfo _tryGetResearchName;      // cosmetic: research id -> display name
        private static MethodInfo _targetDrillLoses;        // target-drill direction of the proficiency check
        private static MethodInfo _setStaminaToZero;        // TFTVCommonMethods.SetStaminaToZero(GeoCharacter)
        private static FieldInfo _staminaPenaltyOption;     // TFTVNewGameOptions.StaminaPenaltyFromInjurySetting
        private static int _drillSwapSpCost = PerkSwapDecision.TftvDrillSwapSpCostFallback;

        private static List<DrillRequirementInfo> _requirements;

        /// <summary>True when TFTV's drills types resolved and the drill list is non-empty.</summary>
        public static bool DrillsAvailable
        {
            get
            {
                EnsureResolved();
                return _available && AllDrills.Count > 0;
            }
        }

        /// <summary>TFTV's full drill list (<c>DrillsDefs.Drills</c>); empty when unavailable.</summary>
        public static List<TacticalAbilityDef> AllDrills
        {
            get
            {
                EnsureResolved();
                try
                {
                    return _drillsField?.GetValue(null) as List<TacticalAbilityDef> ?? new List<TacticalAbilityDef>();
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] drills list read failed: " + ex.Message);
                    return new List<TacticalAbilityDef>();
                }
            }
        }

        /// <summary>
        /// Static requirement table: one entry per drill, converted to PerkOracle's own DTOs.
        /// Built once on first use (TFTV fills its condition map during its own init, long before
        /// any UI can ask). Empty when unavailable.
        /// </summary>
        public static List<DrillRequirementInfo> RequirementTable
        {
            get
            {
                EnsureResolved();
                if (_requirements == null)
                {
                    _requirements = BuildRequirementTable();
                }
                return _requirements;
            }
        }

        /// <summary>Live per-soldier pool: drills this soldier can take right now. Empty when unavailable.</summary>
        public static List<TacticalAbilityDef> GetAvailableDrills(GeoPhoenixFaction faction, GeoCharacter viewer)
        {
            return Invoke(_getAvailableDrills, new object[] { faction, viewer }) as List<TacticalAbilityDef>
                   ?? new List<TacticalAbilityDef>();
        }

        /// <summary>
        /// True when <paramref name="ability"/> is one of TFTV's drills. Reference-matched against the
        /// LIVE <c>DrillsDefs.Drills</c> list, so drills TFTV adds/renames/removes are picked up with no
        /// code change and nothing is hardcoded. False when TFTV is absent.
        /// </summary>
        public static bool IsDrill(TacticalAbilityDef ability)
        {
            if (ability == null || !DrillsAvailable)
            {
                return false;
            }
            List<TacticalAbilityDef> drills = AllDrills;
            for (int i = 0; i < drills.Count; i++)
            {
                if ((object)drills[i] == (object)ability)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>TFTV's own unlock check for one drill; false when unavailable.</summary>
        public static bool IsDrillUnlocked(GeoPhoenixFaction faction, GeoCharacter viewer, TacticalAbilityDef ability)
        {
            return Invoke(_isDrillUnlocked, new object[] { faction, viewer, ability }) as bool? ?? false;
        }

        /// <summary>
        /// True if the soldier already has this drill. SAFETY CHECK: returns <c>null</c> when the answer
        /// could not be obtained (member unresolved, or TFTV threw) so callers can fail CLOSED instead of
        /// reading an error as "no, they don't have it".
        /// </summary>
        public static bool? CharacterHasDrill(GeoCharacter soldier, TacticalAbilityDef drill)
        {
            return Invoke(_characterHasDrill, new object[] { soldier, drill }) as bool?;
        }

        /// <summary>True if the base has a working training facility; false when unavailable.</summary>
        public static bool HasFunctioningTrainingFacility(GeoPhoenixFaction faction)
        {
            return Invoke(_hasTrainingFacility, new object[] { faction }) as bool? ?? false;
        }

        /// <summary>
        /// Collision guard for a swap: true if removing <paramref name="abilityToRemove"/> would strip
        /// a weapon proficiency some already-owned drill requires; <paramref name="blockingDrills"/>
        /// then names them. SAFETY CHECK: returns <c>null</c> when the answer could not be obtained
        /// (member unresolved, or TFTV threw) so callers can fail CLOSED — reading an error as "no, it
        /// breaks nothing" would let a swap silently invalidate a drill the soldier already paid for.
        /// </summary>
        public static bool? WouldBreakWeaponProficiencyRequirement(GeoCharacter soldier, TacticalAbilityDef abilityToRemove, out List<string> blockingDrills)
        {
            blockingDrills = new List<string>();
            EnsureResolved();
            if (_wouldBreakProficiency == null)
            {
                return null;
            }
            try
            {
                object[] args = { soldier, abilityToRemove, null };
                bool result = (bool)_wouldBreakProficiency.Invoke(null, args);
                if (args[2] is List<string> names)
                {
                    blockingDrills = names;
                }
                return result;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] WouldBreakWeaponProficiencyRequirement failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Target-drill direction of the proficiency check (<c>DrillsUnlock.cs:202</c>): true when
        /// removing <paramref name="abilityToRemove"/> would strip a weapon proficiency the CHOSEN drill
        /// itself requires — the one guard PerkOracle had no equivalent of. Same SAFETY convention as
        /// <see cref="WouldBreakWeaponProficiencyRequirement"/>: <c>null</c> when the answer could not be
        /// obtained, so callers fail CLOSED.
        /// </summary>
        public static bool? TargetDrillLosesWeaponProficiencyRequirement(GeoCharacter soldier,
            TacticalAbilityDef targetDrill, TacticalAbilityDef abilityToRemove, out string blockingDrill)
        {
            blockingDrill = null;
            EnsureResolved();
            if (_targetDrillLoses == null)
            {
                return null;
            }
            try
            {
                object[] args = { soldier, targetDrill, abilityToRemove, null };
                bool result = (bool)_targetDrillLoses.Invoke(null, args);
                blockingDrill = args[3] as string;
                return result;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] TargetDrillLosesWeaponProficiencyRequirement failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// TFTV's flat "replace a learned ability with a drill" price (<c>DrillsUI.SwapSpCost</c>, a
        /// private const read out of the INSTALLED TFTV so a rebalance there follows automatically).
        /// Falls back to <see cref="PerkSwapDecision.TftvDrillSwapSpCostFallback"/> when unreadable.
        /// </summary>
        public static int DrillSwapSpCost
        {
            get
            {
                EnsureResolved();
                return _drillSwapSpCost;
            }
        }

        /// <summary>
        /// TFTV's "stamina drain after injury" campaign option
        /// (<c>TFTVNewGameOptions.StaminaPenaltyFromInjurySetting</c>), read LIVE. False without TFTV.
        /// </summary>
        public static bool StaminaPenaltyEnabled
        {
            get
            {
                EnsureResolved();
                try
                {
                    return _staminaPenaltyOption?.GetValue(null) as bool? ?? false;
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] StaminaPenaltyFromInjurySetting read failed: " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Apply TFTV's own post-swap stamina penalty (<c>TFTVCommonMethods.SetStaminaToZero</c>,
        /// DrillsUI.Helpers.cs:344-348). No-op without TFTV or when the member is unresolved.
        /// </summary>
        public static void SetStaminaToZero(GeoCharacter soldier)
        {
            Invoke(_setStaminaToZero, new object[] { soldier });
        }

        /// <summary>TFTV's ready-made "why is this locked" lines; empty when unavailable.</summary>
        public static List<string> GetMissingRequirementDescriptions(GeoPhoenixFaction faction, GeoCharacter viewer, TacticalAbilityDef ability)
        {
            var lines = new List<string>();
            if (Invoke(_missingRequirements, new object[] { faction, viewer, ability }) is IEnumerable raw)
            {
                foreach (object line in raw)
                {
                    if (line is string s)
                    {
                        lines.Add(s);
                    }
                }
            }
            return lines;
        }

        // ---- resolution ---------------------------------------------------------------------

        private static void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;

            try
            {
                Type tDefs = AccessTools.TypeByName("TFTV.TFTVDrills.DrillsDefs");
                Type tUnlock = AccessTools.TypeByName("TFTV.TFTVDrills.DrillsUnlock");
                if (tDefs == null || tUnlock == null)
                {
                    OracleLog.Debug("[Oracle] TFTV drills bridge unavailable: TFTVDrills types not found.");
                    return;
                }

                // Both types are internal and the condition map is private static — AccessTools
                // already searches non-public members. Every method is resolved with an EXPLICIT
                // parameter-type array (signatures read from refs/TFTV-src TFTVDrills/DrillsUnlock.cs
                // :53 /:90 /:144 /:155 /:241 /:330) so a future TFTV overload cannot make the
                // name-only lookup throw AmbiguousMatchException and take the whole bridge down.
                _drillsField = AccessTools.Field(tDefs, "Drills");
                _conditionsField = AccessTools.Field(tUnlock, "DrillUnlockConditions");
                _getAvailableDrills = AccessTools.Method(tUnlock, "GetAvailableDrills",
                    new[] { typeof(GeoPhoenixFaction), typeof(GeoCharacter) });
                _isDrillUnlocked = AccessTools.Method(tUnlock, "IsDrillUnlocked",
                    new[] { typeof(GeoPhoenixFaction), typeof(GeoCharacter), typeof(TacticalAbilityDef) });
                _characterHasDrill = AccessTools.Method(tUnlock, "CharacterHasDrill",
                    new[] { typeof(GeoCharacter), typeof(TacticalAbilityDef) });
                _hasTrainingFacility = AccessTools.Method(tUnlock, "HasFunctioningTrainingFacility",
                    new[] { typeof(GeoPhoenixFaction) });
                _wouldBreakProficiency = AccessTools.Method(tUnlock, "WouldBreakWeaponProficiencyRequirement",
                    new[] { typeof(GeoCharacter), typeof(TacticalAbilityDef), typeof(List<string>).MakeByRefType() });
                _missingRequirements = AccessTools.Method(tUnlock, "GetMissingRequirementDescriptions",
                    new[] { typeof(GeoPhoenixFaction), typeof(GeoCharacter), typeof(TacticalAbilityDef) });
                // Cosmetic only (drill flyout research names) — deliberately NOT part of the
                // availability gate; a miss just falls back to printing the raw research id.
                _tryGetResearchName = AccessTools.Method(tUnlock, "TryGetResearchName",
                    new[] { typeof(string) });
                _targetDrillLoses = AccessTools.Method(tUnlock, "TargetDrillLosesWeaponProficiencyRequirement",
                    new[]
                    {
                        typeof(GeoCharacter), typeof(TacticalAbilityDef), typeof(TacticalAbilityDef),
                        typeof(string).MakeByRefType(),
                    });

                // Cost + stamina parity with TFTV's own drill swap. Cosmetic-adjacent, NOT part of the
                // availability gate: an older TFTV without them keeps working (flat fallback price, no
                // stamina penalty) instead of silently disabling every drill gate.
                ReadDrillSwapSpCost();
                Type tOptions = AccessTools.TypeByName("TFTV.TFTVNewGameOptions");
                Type tCommon = AccessTools.TypeByName("TFTV.TFTVCommonMethods");
                _staminaPenaltyOption = tOptions != null
                    ? AccessTools.Field(tOptions, "StaminaPenaltyFromInjurySetting")
                    : null;
                _setStaminaToZero = tCommon != null
                    ? AccessTools.Method(tCommon, "SetStaminaToZero", new[] { typeof(GeoCharacter) })
                    : null;

                // Availability requires the drill list AND every member a GATE depends on. Gating on
                // the field alone would leave DrillsAvailable true while the accessors silently return
                // false — which disables the acquired-drill gate and the hard proficiency guard.
                _available = ReportMissing("DrillsDefs.Drills", _drillsField)
                             & ReportMissing("IsDrillUnlocked", _isDrillUnlocked)
                             & ReportMissing("CharacterHasDrill", _characterHasDrill)
                             & ReportMissing("WouldBreakWeaponProficiencyRequirement", _wouldBreakProficiency)
                             & ReportMissing("TargetDrillLosesWeaponProficiencyRequirement", _targetDrillLoses)
                             & ReportMissing("GetAvailableDrills", _getAvailableDrills);
            }
            catch (Exception ex)
            {
                _available = false;
                OracleLog.Debug("[Oracle] TFTV drills bridge unavailable: " + ex.Message);
            }
        }

        /// <summary>
        /// Read TFTV's <c>DrillsUI.SwapSpCost</c> literal. It is a private const, so the value lives in
        /// metadata and <see cref="FieldInfo.GetRawConstantValue"/> reads it without an instance. Any miss
        /// leaves the fallback in place.
        /// </summary>
        private static void ReadDrillSwapSpCost()
        {
            try
            {
                Type tUi = AccessTools.TypeByName("TFTV.TFTVDrills.DrillsUI");
                FieldInfo f = tUi != null ? AccessTools.Field(tUi, "SwapSpCost") : null;
                if (f != null && f.IsLiteral && f.GetRawConstantValue() is int cost && cost >= 0)
                {
                    _drillSwapSpCost = cost;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] DrillsUI.SwapSpCost read failed, using fallback: " + ex.Message);
            }
        }

        /// <summary>True when <paramref name="member"/> resolved; otherwise logs exactly which one failed.</summary>
        private static bool ReportMissing(string name, MemberInfo member)
        {
            if (member != null)
            {
                return true;
            }
            OracleLog.Debug("[Oracle] TFTV drills bridge unavailable: " + name + " not found.");
            return false;
        }

        /// <summary>
        /// TFTV's own research-id -> display-name resolution (DrillsUnlock.TryGetResearchName:830), so the
        /// wiki prints exactly the wording TFTV prints. Returns the raw id when unavailable.
        /// </summary>
        public static string ResearchName(string researchId)
        {
            EnsureResolved();
            if (_tryGetResearchName == null || string.IsNullOrEmpty(researchId))
            {
                return researchId;
            }
            try
            {
                return _tryGetResearchName.Invoke(null, new object[] { researchId }) as string ?? researchId;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] TryGetResearchName failed: " + ex.Message);
                return researchId;
            }
        }

        /// <summary>Invoke a resolved static method, or return null (resolving first if needed).</summary>
        private static object Invoke(MethodInfo method, object[] args)
        {
            EnsureResolved();
            if (method == null)
            {
                return null;
            }
            try
            {
                return method.Invoke(null, args);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] TFTV drills call " + method.Name + " failed: " + ex.Message);
                return null;
            }
        }

        private static List<DrillRequirementInfo> BuildRequirementTable()
        {
            var table = new List<DrillRequirementInfo>();
            try
            {
                var conditions = _conditionsField?.GetValue(null) as IDictionary;
                foreach (TacticalAbilityDef drill in AllDrills)
                {
                    if (drill == null)
                    {
                        continue;
                    }
                    object condition = conditions != null && conditions.Contains(drill) ? conditions[drill] : null;
                    table.Add(Convert(drill, condition));
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] drill requirement table build failed: " + ex.Message);
            }
            return table;
        }

        /// <summary>Convert one TFTV condition object into our DTO. A null condition ⇒ always available.</summary>
        private static DrillRequirementInfo Convert(TacticalAbilityDef drill, object condition)
        {
            var info = new DrillRequirementInfo { Drill = drill };
            if (condition == null)
            {
                info.AlwaysAvailable = true;
                return info;
            }

            info.AlwaysAvailable = ReadMember(condition, "AlwaysAvailable") as bool? ?? false;

            if (ReadMember(condition, "RequiredResearchIds") is IEnumerable ids)
            {
                foreach (object id in ids)
                {
                    if (id is string s)
                    {
                        info.RequiredResearchIds.Add(s);
                    }
                }
            }

            if (ReadMember(condition, "ClassLevelRequirements") is IEnumerable classReqs)
            {
                foreach (object req in classReqs)
                {
                    if (req == null)
                    {
                        continue;
                    }
                    info.ClassLevels.Add(new DrillClassLevelInfo
                    {
                        ClassTag = ReadMember(req, "ClassTag") as ClassTagDef,
                        MinimumLevel = ReadMember(req, "MinimumLevel") as int? ?? 1,
                    });
                }
            }

            if (ReadMember(condition, "WeaponProficiencyRequirements") is IEnumerable profReqs)
            {
                foreach (object req in profReqs)
                {
                    if (req == null)
                    {
                        continue;
                    }
                    var group = new List<TacticalAbilityDef>();
                    if (ReadMember(req, "ProficiencyAbilities") is IEnumerable abilities)
                    {
                        foreach (object ability in abilities)
                        {
                            if (ability is TacticalAbilityDef def)
                            {
                                group.Add(def);
                            }
                        }
                    }
                    info.ProficiencyGroups.Add(group);
                }
            }

            return info;
        }

        /// <summary>Read a field or auto-property by name off a reflected instance; null on any miss.</summary>
        private static object ReadMember(object instance, string name)
        {
            try
            {
                Type t = instance.GetType();
                FieldInfo field = AccessTools.Field(t, name);
                if (field != null)
                {
                    return field.GetValue(instance);
                }
                return AccessTools.PropertyGetter(t, name)?.Invoke(instance, null);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] drill condition member '" + name + "' read failed: " + ex.Message);
                return null;
            }
        }
    }
}
