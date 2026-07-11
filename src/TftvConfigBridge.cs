using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Geoscape.Events;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Reflection bridge into TFTV's PRMBetterClasses config. Resolves once, lazily, and caches
    /// the resolved OrderOfPersonalPerks array plus a per-slot IsRandom lookup. Any reflection
    /// failure is logged once and leaves the bridge unavailable (fail open: IsSlotRandom returns
    /// false), so the UI never breaks when TFTV is absent or its internals change.
    /// TFTV ships as a separate assembly, so types are resolved by full name at runtime.
    /// </summary>
    public static class TftvConfigBridge
    {
        private static bool _initialized;
        private static bool _available;

        // OrderOfPersonalPerks[level0] -> perk key string.
        private static string[] _order;
        // Parallel arrays from PersonalPerks: PerkKey -> IsRandom.
        private static string[] _perkKeys;
        private static bool[] _perkIsRandom;

        // Wiki pool data (captured alongside the IsRandom data, same reflection pass).
        // PerkKey -> the perk's UnrelatedRandomPerks list (upper-case display names); only random perks have one.
        private static Dictionary<string, List<string>> _randomNamesByKey;
        // PerkKey -> RelatedFixedPerks (faction key -> className -> display name); only FIXED perks have one.
        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> _fixedNamesByKey;
        // BCSettings.RadomSkillExclusionMap: display name -> class names it is excluded for.
        private static Dictionary<string, List<string>> _exclusionMap;
        // PRMBetterClasses.Helper.AbilityNameToDefMap: upper display name -> def name.
        private static Dictionary<string, string> _nameToDefName;
        // Built from the vanilla DefRepository once: def name -> TacticalAbilityDef. Lets us resolve
        // the def names from AbilityNameToDefMap without reflecting TFTV's generic DefCache method.
        private static Dictionary<string, TacticalAbilityDef> _defByName;

        public static bool Available
        {
            get
            {
                EnsureInitialized();
                return _available;
            }
        }

        /// <summary>
        /// True if the Personal perk at the given 0-based level slot is randomly rolled per TFTV
        /// config. Bounds-checked; returns false on any miss or if the bridge is unavailable.
        /// </summary>
        public static bool IsSlotRandom(int level0)
        {
            EnsureInitialized();
            if (!_available || _order == null)
            {
                return false;
            }
            if (level0 < 0 || level0 >= _order.Length)
            {
                return false;
            }

            string key = _order[level0];
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (int i = 0; i < _perkKeys.Length; i++)
            {
                if (_perkKeys[i] == key)
                {
                    return _perkIsRandom[i];
                }
            }
            return false;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            try
            {
                Initialize();
                _available = _order != null && _order.Length > 0
                             && _perkKeys != null && _perkIsRandom != null;
                if (!_available)
                {
                    LogOnce("[Oracle] TFTV config bridge resolved but data was empty; disabling highlight.");
                }
            }
            catch (Exception ex)
            {
                _available = false;
                LogOnce("[Oracle] TFTV config bridge unavailable: " + ex.Message);
            }
        }

        private static void Initialize()
        {
            // TFTV.TFTVMain.Main (static property getter) -> instance.
            Type tMain = AccessTools.TypeByName("TFTV.TFTVMain");
            if (tMain == null)
            {
                throw new Exception("type TFTV.TFTVMain not found");
            }
            MethodInfo mainGetter = AccessTools.PropertyGetter(tMain, "Main");
            object mainInstance = mainGetter?.Invoke(null, null);
            if (mainInstance == null)
            {
                throw new Exception("TFTVMain.Main was null");
            }

            // Instance field Settings : PRMBetterClasses.BCSettings.
            FieldInfo settingsField = AccessTools.Field(tMain, "Settings");
            object settings = settingsField?.GetValue(mainInstance);
            if (settings == null)
            {
                throw new Exception("TFTVMain.Settings was null");
            }
            Type tSettings = settings.GetType();

            // BCSettings.OrderOfPersonalPerks : string[].
            FieldInfo orderField = AccessTools.Field(tSettings, "OrderOfPersonalPerks");
            _order = orderField?.GetValue(settings) as string[];
            if (_order == null)
            {
                throw new Exception("OrderOfPersonalPerks was null or not string[]");
            }

            // BCSettings.PersonalPerks : List<PersonalPerksDef> (enumerate as boxed structs).
            FieldInfo perksField = AccessTools.Field(tSettings, "PersonalPerks");
            IEnumerable perks = perksField?.GetValue(settings) as IEnumerable;
            if (perks == null)
            {
                throw new Exception("PersonalPerks was null or not enumerable");
            }

            MethodInfo perkKeyGetter = null;
            MethodInfo isRandomGetter = null;
            MethodInfo randomPerksGetter = null;
            MethodInfo fixedPerksGetter = null;

            var keys = new System.Collections.Generic.List<string>();
            var randoms = new System.Collections.Generic.List<bool>();
            _randomNamesByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            _fixedNamesByKey = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.Ordinal);

            foreach (object boxedDef in perks)
            {
                if (boxedDef == null)
                {
                    continue;
                }
                // Resolve the struct's property getters once from the first element's runtime type.
                if (perkKeyGetter == null)
                {
                    Type tDef = boxedDef.GetType();
                    perkKeyGetter = AccessTools.PropertyGetter(tDef, "PerkKey");
                    isRandomGetter = AccessTools.PropertyGetter(tDef, "IsRandom");
                    // UnrelatedRandomPerks is a computed getter returning List<string> (null for fixed perks).
                    randomPerksGetter = AccessTools.PropertyGetter(tDef, "UnrelatedRandomPerks");
                    // RelatedFixedPerks is a computed getter returning Dictionary<string, Dictionary<string,
                    // string>> (faction key -> className -> display name); null for random perks.
                    fixedPerksGetter = AccessTools.PropertyGetter(tDef, "RelatedFixedPerks");
                    if (perkKeyGetter == null || isRandomGetter == null)
                    {
                        throw new Exception("PerkKey/IsRandom getters not found on PersonalPerksDef");
                    }
                }

                string key = perkKeyGetter.Invoke(boxedDef, null) as string;
                bool isRandom = (bool)isRandomGetter.Invoke(boxedDef, null);
                keys.Add(key);
                randoms.Add(isRandom);

                if (isRandom && randomPerksGetter != null && key != null)
                {
                    // Returned object is a List<string>; copy into our own list (boxed enumerable).
                    if (randomPerksGetter.Invoke(boxedDef, null) is IEnumerable names)
                    {
                        var copy = new List<string>();
                        foreach (object n in names)
                        {
                            if (n is string s)
                            {
                                copy.Add(s);
                            }
                        }
                        _randomNamesByKey[key] = copy;
                    }
                }
                else if (!isRandom && fixedPerksGetter != null && key != null)
                {
                    // Returned object is a Dictionary<string, Dictionary<string, string>>; deep-copy.
                    if (fixedPerksGetter.Invoke(boxedDef, null) is IDictionary byFaction)
                    {
                        var copy = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
                        foreach (DictionaryEntry fe in byFaction)
                        {
                            if (!(fe.Key is string factionKey) || !(fe.Value is IDictionary byClass))
                            {
                                continue;
                            }
                            var classCopy = new Dictionary<string, string>(StringComparer.Ordinal);
                            foreach (DictionaryEntry ce in byClass)
                            {
                                if (ce.Key is string cls && ce.Value is string perkName)
                                {
                                    classCopy[cls] = perkName;
                                }
                            }
                            copy[factionKey] = classCopy;
                        }
                        _fixedNamesByKey[key] = copy;
                    }
                }
            }

            _perkKeys = keys.ToArray();
            _perkIsRandom = randoms.ToArray();

            // BCSettings.RadomSkillExclusionMap : Dictionary<string, List<string>> (name -> excluded classes).
            _exclusionMap = CopyStringListDict(AccessTools.Field(tSettings, "RadomSkillExclusionMap")?.GetValue(settings) as IDictionary);

            // PRMBetterClasses.Helper.AbilityNameToDefMap : static Dictionary<string,string> (upper name -> def name).
            Type tHelper = AccessTools.TypeByName("PRMBetterClasses.Helper");
            IDictionary nameMap = tHelper != null
                ? AccessTools.Field(tHelper, "AbilityNameToDefMap")?.GetValue(null) as IDictionary
                : null;
            _nameToDefName = new Dictionary<string, string>(StringComparer.Ordinal);
            if (nameMap != null)
            {
                foreach (DictionaryEntry e in nameMap)
                {
                    if (e.Key is string k && e.Value is string v)
                    {
                        _nameToDefName[k] = v;
                    }
                }
            }
        }

        /// <summary>Deep-copy a reflected Dictionary&lt;string, List&lt;string&gt;&gt; into our own typed map.</summary>
        private static Dictionary<string, List<string>> CopyStringListDict(IDictionary src)
        {
            var dst = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (src == null)
            {
                return dst;
            }
            foreach (DictionaryEntry e in src)
            {
                if (!(e.Key is string key))
                {
                    continue;
                }
                var list = new List<string>();
                if (e.Value is IEnumerable values)
                {
                    foreach (object v in values)
                    {
                        if (v is string s)
                        {
                            list.Add(s);
                        }
                    }
                }
                dst[key] = list;
            }
            return dst;
        }

        /// <summary>
        /// Personal-track slot count: TFTV's <c>OrderOfPersonalPerks.Length</c> when the bridge is up,
        /// else vanilla's 7 (CharacterProgression builds the personal track with
        /// <c>LevelProgressionDef.MaxLevel</c> slots = the 7-entry XP table).
        /// </summary>
        public static int PersonalSlotCount
        {
            get
            {
                EnsureInitialized();
                return _available && _order != null ? _order.Length : 7;
            }
        }

        /// <summary>
        /// The FIXED additional perk TFTV grants at slot <paramref name="level0"/> for a soldier of
        /// <paramref name="className"/>. Mirrors PRMBetterClasses <c>PersonalPerksDef.GetPerk</c>'s
        /// non-random path (TFTV src ConfigHelpers.cs:129): <c>RelatedFixedPerks[faction][className]</c>
        /// with the Phoenix row first (Faction_1/Faction_2 keys), then the "All Factions" row (Class_1/
        /// Class_2 keys) — the wiki always shows the player's own (Phoenix) perspective. Returns false
        /// when the bridge is down, the slot is random, or the class has no entry.
        /// </summary>
        public static bool TryGetTftvFixedPerk(int level0, string className, out TacticalAbilityDef def)
        {
            def = null;
            EnsureInitialized();
            if (!_available || _order == null || _fixedNamesByKey == null || string.IsNullOrEmpty(className))
            {
                return false;
            }
            if (level0 < 0 || level0 >= _order.Length)
            {
                return false;
            }

            string key = _order[level0];
            if (string.IsNullOrEmpty(key)
                || !_fixedNamesByKey.TryGetValue(key, out Dictionary<string, Dictionary<string, string>> byFaction)
                || byFaction == null)
            {
                return false;
            }

            // FactionKeys.PX = "Phoenix", FactionKeys.All = "All Factions" (TFTV src ConfigHelpers.cs:233).
            string name = null;
            if (byFaction.TryGetValue("Phoenix", out Dictionary<string, string> px) && px != null)
            {
                px.TryGetValue(className, out name);
            }
            if (name == null && byFaction.TryGetValue("All Factions", out Dictionary<string, string> all) && all != null)
            {
                all.TryGetValue(className, out name);
            }
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            def = ResolveAbilityName(name);
            return (UnityEngine.Object)(object)def != (UnityEngine.Object)null;
        }

        /// <summary>
        /// TFTV candidate pool for the random slot at <paramref name="level0"/>, filtered to this
        /// soldier's class and resolved to defs (order preserved, duplicates dropped). Returns false
        /// when the bridge is unavailable or the slot is not a known random perk; on true,
        /// <paramref name="defs"/> is non-null (possibly empty).
        /// </summary>
        public static bool TryGetTftvRandomPool(int level0, string className, out List<TacticalAbilityDef> defs)
        {
            defs = null;
            EnsureInitialized();
            if (!_available || _order == null || _randomNamesByKey == null)
            {
                return false;
            }
            if (level0 < 0 || level0 >= _order.Length)
            {
                return false;
            }

            string key = _order[level0];
            if (string.IsNullOrEmpty(key) || !_randomNamesByKey.TryGetValue(key, out List<string> rawNames))
            {
                return false;
            }

            defs = PerkPoolResolver.OrderAndResolve(rawNames, className, IsExcludedForClass, ResolveAbilityName);
            return true;
        }

        /// <summary>
        /// Vanilla personal-perk pool: every TacticalAbilityDef tagged for the personal progression
        /// track (global, no class filter). Mirrors AbilityTrack.CreatePersonalAbilityTrack's query.
        /// Used when TFTV is absent. Returns an empty list on any failure.
        /// </summary>
        public static List<TacticalAbilityDef> GetVanillaPersonalPool()
        {
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                var sharedTags = GameUtl.GameComponent<SharedData>().SharedGameTags;
                var basicFilter = sharedTags.PersonalProgressionTag;
                return repo.GetAllDefs<TacticalAbilityDef>()
                    .Where(p => p.CharacterProgressionData != null
                                && p.CharacterProgressionData.PersonalTrackTags.Contains(basicFilter))
                    .ToList();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] vanilla personal pool query failed: " + ex.Message);
                return new List<TacticalAbilityDef>();
            }
        }

        /// <summary>True if <paramref name="name"/> is blacklisted for <paramref name="className"/>.</summary>
        private static bool IsExcludedForClass(string name, string className)
        {
            return _exclusionMap != null
                   && _exclusionMap.TryGetValue(name, out List<string> classes)
                   && classes != null
                   && classes.Contains(className);
        }

        /// <summary>Resolve an upper-case display name to its TacticalAbilityDef, or null.</summary>
        private static TacticalAbilityDef ResolveAbilityName(string name)
        {
            if (_nameToDefName == null || !_nameToDefName.TryGetValue(name, out string defName) || string.IsNullOrEmpty(defName))
            {
                return null;
            }
            EnsureDefIndex();
            return _defByName != null && _defByName.TryGetValue(defName, out TacticalAbilityDef def) ? def : null;
        }

        /// <summary>Lazily index every TacticalAbilityDef by its def name (built once, on first need).</summary>
        private static void EnsureDefIndex()
        {
            if (_defByName != null)
            {
                return;
            }
            _defByName = new Dictionary<string, TacticalAbilityDef>(StringComparer.Ordinal);
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                foreach (TacticalAbilityDef def in repo.GetAllDefs<TacticalAbilityDef>())
                {
                    if (def != null && !string.IsNullOrEmpty(def.name))
                    {
                        _defByName[def.name] = def;
                    }
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] def-name index build failed: " + ex.Message);
            }
        }

        private static void LogOnce(string message)
        {
            // EnsureInitialized only runs the resolution path once, so this fires at most once.
            OracleLog.Debug(message);
        }

        // ---- Event resource-reward multiplier (matches the actual grant under TFTV) ----------------

        /// <summary>
        /// TFTV's flat per-resource event-reward scalar, hardcoded in
        /// <c>TFTV.TFTVSpecialDifficulties.OnGeoscape.TFTV_GeoEventChoiceOutcome_GenerateFactionReward_*_patch.Prefix</c>
        /// ("Pre_Azozoth_base"): every event-outcome <c>ResourceUnit.Value *= 0.8f</c>, unconditionally,
        /// before the reward is generated. Verified in refs/TFTV-src TFTVSpecialDifficulties.cs (~line 434).
        /// </summary>
        private const float TftvBaseResourceRewardMultiplier = 0.8f;

        // Resolved TFTVNewGameOptions.ResourceMultiplierSetting field (player option, 0.5/1/1.25/1.5/2),
        // applied on top of the 0.8 base in the same TFTV Prefix when it differs from 1.
        private static FieldInfo _resourceMultiplierField;
        private static bool _resourceMultiplierResolved;

        /// <summary>
        /// The total multiplier the game applies to every event-outcome resource amount before granting it,
        /// so a preview can match the real reward. Under TFTV this is
        /// <c>0.8 * ResourceMultiplierSetting</c> (the campaign's chosen resource multiplier); when TFTV is
        /// absent the patch never runs, so this is <c>1.0</c> (raw def value). Fails open to <c>1.0</c> on any
        /// reflection error so the tooltip never breaks. Read-only: never invokes the apply path.
        /// </summary>
        public static float EventResourceRewardMultiplier
        {
            get
            {
                try
                {
                    EnsureInitialized();
                    if (!_available)
                    {
                        // TFTV not loaded (or its config didn't resolve) -> patch doesn't run -> no scaling.
                        return 1f;
                    }

                    float multiplier = TftvBaseResourceRewardMultiplier;
                    float setting = ReadResourceMultiplierSetting();
                    // TFTV only applies the player setting when it differs from 1; a non-positive value means
                    // the option was never initialized (no active campaign) -> ignore it, keep the 0.8 base.
                    if (setting > 0f)
                    {
                        multiplier *= setting;
                    }
                    return multiplier;
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] EventResourceRewardMultiplier failed, using 1.0: " + ex.Message);
                    return 1f;
                }
            }
        }

        /// <summary>Reflect the current <c>TFTVNewGameOptions.ResourceMultiplierSetting</c>; 0 on any miss.</summary>
        private static float ReadResourceMultiplierSetting()
        {
            if (!_resourceMultiplierResolved)
            {
                _resourceMultiplierResolved = true;
                Type tOptions = AccessTools.TypeByName("TFTV.TFTVNewGameOptions");
                _resourceMultiplierField = tOptions != null
                    ? AccessTools.Field(tOptions, "ResourceMultiplierSetting")
                    : null;
            }
            if (_resourceMultiplierField == null)
            {
                return 0f;
            }
            object value = _resourceMultiplierField.GetValue(null);
            return value is float f ? f : 0f;
        }

        // ---- Scrap-refund proration (matches the actual grant under TFTV) --------------------------

        // TFTV.TFTVVanillaFixes+ScrapRefundUtils.GetNextScrapRefundMultiplier(ICommonItem) -> float.
        // Resolved lazily and independently of the BCSettings config above (VanillaFixes ships even if
        // the perk-config bridge fails).
        private static MethodInfo _scrapRefundMultiplierMethod;
        private static bool _scrapRefundResolved;

        /// <summary>
        /// The multiplier TFTV applies to <c>ItemDef.ScrapPrice</c> when actually scrapping THIS item:
        /// its <c>GeoFaction.ScrapItem</c> prefix grants <c>ScrapPrice * GetNextScrapRefundMultiplier(item)</c>,
        /// prorating ammo (<c>ChargesMax &gt; 0</c>, not a combined stack) by
        /// <c>CurrentCharges / ChargesMax</c> (refs/TFTV-src TFTVVanillaFixes.cs:119-161). Reflected live per
        /// call so charge changes are always current. Returns <c>1.0</c> when TFTV is absent (vanilla grants
        /// raw ScrapPrice), <paramref name="item"/> is null (def-only view), or reflection fails — fail open,
        /// the tooltip never breaks.
        /// </summary>
        public static float ScrapRefundMultiplier(ICommonItem item)
        {
            if (item == null)
            {
                return 1f;
            }
            try
            {
                if (!_scrapRefundResolved)
                {
                    _scrapRefundResolved = true;
                    Type t = AccessTools.TypeByName("TFTV.TFTVVanillaFixes+ScrapRefundUtils");
                    _scrapRefundMultiplierMethod = t != null
                        ? AccessTools.Method(t, "GetNextScrapRefundMultiplier")
                        : null;
                }
                if (_scrapRefundMultiplierMethod == null)
                {
                    return 1f;
                }
                return (float)_scrapRefundMultiplierMethod.Invoke(null, new object[] { item });
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] ScrapRefundMultiplier failed, using 1.0: " + ex.Message);
                return 1f;
            }
        }

        // ---- Event diplomacy/reputation multiplier (matches the actual grant under TFTV) -----------

        // Resolved once, lazily, on first diplomacy preview. Each may stay null (member absent) without
        // disabling the others; the computation simply skips the branch that needs it.
        private static bool _diplomacyReflectionResolved;
        private static FieldInfo _diplomaticPenaltiesField;   // TFTVNewGameOptions.DiplomaticPenaltiesSetting (static bool)
        private static MethodInfo _voidOmensInPlayMethod;     // TFTVVoidOmens.CheckFordVoidOmensInPlay(GeoLevelController) -> int[]
        private static GeoFactionDef _phoenixFactionDef;      // base def "Phoenix_GeoPhoenixFactionDef"
        private static bool _phoenixFactionResolved;

        /// <summary>
        /// The reputation/diplomacy value the game will actually GRANT for one
        /// <see cref="OutcomeDiplomacyChange"/>, mirroring TFTV's
        /// <c>TFTV_GeoEventChoiceOutcome_GenerateFactionReward_SpecialDifficultiesAndVO2AndVO8_patch.Prefix</c>
        /// (refs/TFTV-src TFTVSpecialDifficulties.cs lines 324-427). TFTV mutates <c>Diplomacy[i].Value</c>
        /// in place before the base game reads it into the reward, so the previewed number must reproduce the
        /// SAME sequential transforms, in the SAME order, with the SAME rounding:
        /// <list type="number">
        ///   <item>Diplomatic-penalties option (<c>TFTVNewGameOptions.DiplomaticPenaltiesSetting</c>): a
        ///         Phoenix-faction penalty (<c>Value &lt;= 0</c>) doubles. The real patch also skips a fixed
        ///         list of story events (<c>ExcludedEventsDiplomacyPenalty</c>); the event id is not reachable
        ///         from the hovered choice, so we replicate the COMMON case (not excluded). See residual note.</item>
        ///   <item>Rookie/Easy geoscape (<c>CurrentDifficultyLevel.Order == 1</c>): a Phoenix-faction reward
        ///         (<c>Value &gt;= 0</c>) doubles.</item>
        ///   <item>Void Omen #2 (<c>TFTVVoidOmens.CheckFordVoidOmensInPlay</c> contains 2): any non-zero value
        ///         is halved via <c>Mathf.CeilToInt(v * 0.5f)</c> (this is the 4 -&gt; 2 case).</item>
        ///   <item>Void Omen #8 (contains 8): a non-Phoenix penalty (<c>Value &lt;= 0</c>) scales by 1.5 via
        ///         <c>Mathf.RoundToInt(v * 1.5f)</c>.</item>
        /// </list>
        /// Read-only: never invokes <c>GenerateFactionReward</c>/the apply path. Fails open to the raw
        /// <c>change.Value</c> when TFTV is absent or any reflected/engine member cannot be resolved, so the
        /// tooltip never throws and matches vanilla when the patch would not run.
        /// </summary>
        public static int EventDiplomacyFinalValue(OutcomeDiplomacyChange change)
        {
            int raw = change.Value;
            try
            {
                EnsureInitialized();
                if (!_available || raw == 0)
                {
                    // TFTV not loaded (patch never runs) or nothing to scale -> raw value as-is.
                    return raw;
                }

                GeoLevelController controller = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
                if ((UnityEngine.Object)(object)controller == (UnityEngine.Object)null)
                {
                    return raw;
                }

                EnsureDiplomacyReflection();

                bool isPhoenix = (UnityEngine.Object)(object)_phoenixFactionDef != (UnityEngine.Object)null
                                 && (UnityEngine.Object)(object)change.TargetFaction == (UnityEngine.Object)(object)_phoenixFactionDef;

                int v = raw;

                // 1. Diplomatic-penalties option: Phoenix penalty doubles (excluded-events residual aside).
                if (_diplomaticPenaltiesField != null
                    && _diplomaticPenaltiesField.GetValue(null) is bool penalties && penalties
                    && isPhoenix && v <= 0)
                {
                    v *= 2;
                }

                // 2. Rookie/Easy geoscape (difficulty Order == 1): Phoenix reward doubles.
                GameDifficultyLevelDef difficulty = controller.CurrentDifficultyLevel;
                if ((UnityEngine.Object)(object)difficulty != (UnityEngine.Object)null
                    && difficulty.Order == 1 && isPhoenix && v >= 0)
                {
                    v *= 2;
                }

                // Void Omens (3 & 4) need the in-play array; absence -> empty -> both branches skip.
                int[] voidOmens = ReadVoidOmensInPlay(controller);

                // 3. Void Omen #2: any non-zero value halved (ceil). This is the reported 4 -> 2 case.
                if (voidOmens != null && Array.IndexOf(voidOmens, 2) >= 0 && v != 0)
                {
                    v = Mathf.CeilToInt(v * 0.5f);
                }

                // 4. Void Omen #8: non-Phoenix penalty scaled by 1.5 (round).
                if (voidOmens != null && Array.IndexOf(voidOmens, 8) >= 0 && v <= 0 && !isPhoenix)
                {
                    v = Mathf.RoundToInt(v * 1.5f);
                }

                return v;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] EventDiplomacyFinalValue failed, using raw value: " + ex.Message);
                return raw;
            }
        }

        /// <summary>Resolve the TFTV diplomacy-multiplier reflection members once (each may stay null).</summary>
        private static void EnsureDiplomacyReflection()
        {
            if (!_diplomacyReflectionResolved)
            {
                _diplomacyReflectionResolved = true;

                Type tOptions = AccessTools.TypeByName("TFTV.TFTVNewGameOptions");
                _diplomaticPenaltiesField = tOptions != null
                    ? AccessTools.Field(tOptions, "DiplomaticPenaltiesSetting")
                    : null;

                Type tVoidOmens = AccessTools.TypeByName("TFTV.TFTVVoidOmens");
                _voidOmensInPlayMethod = tVoidOmens != null
                    ? AccessTools.Method(tVoidOmens, "CheckFordVoidOmensInPlay", new[] { typeof(GeoLevelController) })
                    : null;
            }

            if (!_phoenixFactionResolved)
            {
                _phoenixFactionResolved = true;
                try
                {
                    // Match by def name (same key TFTV's DefCache.GetDef uses), mirroring EnsureDefIndex's
                    // name-based lookup; the base GetDef(string) keys by GUID, not name.
                    DefRepository repo = GameUtl.GameComponent<DefRepository>();
                    foreach (GeoFactionDef def in repo.GetAllDefs<GeoFactionDef>())
                    {
                        if (def != null && def.name == "Phoenix_GeoPhoenixFactionDef")
                        {
                            _phoenixFactionDef = def;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OracleLog.Debug("[Oracle] Phoenix faction def lookup failed: " + ex.Message);
                    _phoenixFactionDef = null;
                }
            }
        }

        /// <summary>Reflect TFTV's current in-play Void Omen ids; empty array on any miss.</summary>
        private static int[] ReadVoidOmensInPlay(GeoLevelController controller)
        {
            if (_voidOmensInPlayMethod == null)
            {
                return Array.Empty<int>();
            }
            try
            {
                return _voidOmensInPlayMethod.Invoke(null, new object[] { controller }) as int[] ?? Array.Empty<int>();
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] Void Omens lookup failed: " + ex.Message);
                return Array.Empty<int>();
            }
        }
    }
}
