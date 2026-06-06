using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace Morgott.PerkOracle
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
                    LogOnce("[PerkOracle] TFTV config bridge resolved but data was empty; disabling highlight.");
                }
            }
            catch (Exception ex)
            {
                _available = false;
                LogOnce("[PerkOracle] TFTV config bridge unavailable: " + ex.Message);
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

            var keys = new System.Collections.Generic.List<string>();
            var randoms = new System.Collections.Generic.List<bool>();
            _randomNamesByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);

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
                Debug.Log("[PerkOracle] vanilla personal pool query failed: " + ex.Message);
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
                Debug.Log("[PerkOracle] def-name index build failed: " + ex.Message);
            }
        }

        private static void LogOnce(string message)
        {
            // EnsureInitialized only runs the resolution path once, so this fires at most once.
            Debug.Log(message);
        }
    }
}
