using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Morgott.Oracle
{
    /// <summary>
    /// Remembers the ability a progression slot held the FIRST time PerkOracle changed it, so the player
    /// can put the slot back to its default. Engine-free on purpose (plain strings + System.IO) so the
    /// whole keying / first-write-wins / clear-on-revert core unit-tests under net8.
    ///
    /// On-disk shape: one flat JSON string map, two entries per tracked slot —
    ///   "o:&lt;charId&gt;#&lt;level0&gt;" -> def name the slot ORIGINALLY had (written once, never overwritten)
    ///   "c:&lt;charId&gt;#&lt;level0&gt;" -> def name PerkOracle LAST wrote into that slot
    ///
    /// The "c:" half is a guard, not decoration. <c>GeoCharacter.Id</c> is a serialized int that is unique
    /// only WITHIN one campaign (Assembly-CSharp exposes no campaign/save id at all — verified), and TFTV
    /// or another mod can rewrite a slot behind us. So a stored entry is honoured only while its recorded
    /// "current" still equals what the slot actually holds; a cross-campaign id collision or an outside
    /// edit therefore silently offers nothing instead of offering the wrong perk.
    ///
    /// Nothing here is keyed on TFTV content: def NAMES are stored and resolved against the live def
    /// repository at use time. A def TFTV renamed/removed simply fails to resolve, no revert is offered,
    /// and the entry is left alone (a later load may have that def again).
    /// </summary>
    public static class OriginalPerkStore
    {
        private const string OriginalPrefix = "o:";
        private const string CurrentPrefix = "c:";

        /// <summary>
        /// Absolute path of the JSON file. Set once by <c>OracleMain</c> from the mod's own install
        /// directory (same place the localization CSV is read from). Null disables the store entirely.
        /// </summary>
        public static string FilePath;

        /// <summary>
        /// Diagnostic sink, wired to <c>OracleLog.Debug</c> by <c>OracleMain</c>. A delegate rather than a
        /// direct call so this file stays Unity-free and links straight into the net8 test project.
        /// </summary>
        public static Action<string> Log;

        // Lazily loaded on first use, kept in memory, written through on every change.
        private static Dictionary<string, string> _map;

        private static void Warn(string message)
        {
            Action<string> sink = Log;
            if (sink != null)
            {
                sink(message);
            }
        }

        // ---- runtime surface ------------------------------------------------------------------

        /// <summary>
        /// Record that PerkOracle just changed the slot at <paramref name="level0"/> of soldier
        /// <paramref name="characterId"/> from <paramref name="originalDefName"/> to
        /// <paramref name="newDefName"/>. First write wins for the ORIGINAL (a second swap still reverts
        /// to the true default); the entry is dropped entirely once the slot is back to its original.
        /// Never throws.
        /// </summary>
        public static void RecordSwap(int characterId, int level0, string originalDefName, string newDefName)
        {
            try
            {
                if (level0 < 0 || string.IsNullOrEmpty(originalDefName) || string.IsNullOrEmpty(newDefName))
                {
                    return;
                }
                Dictionary<string, string> map = EnsureLoaded();
                Record(map, BuildKey(characterId, level0), originalDefName, newDefName);
                Save(FilePath, map);
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.RecordSwap failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Def name this slot originally held, or null when nothing is stored, the stored "current" no
        /// longer matches <paramref name="currentDefName"/> (other campaign / edited behind us), or the
        /// slot is already back to its original. Never throws.
        /// </summary>
        public static string GetOriginalDefName(int characterId, int level0, string currentDefName)
        {
            try
            {
                if (level0 < 0 || string.IsNullOrEmpty(currentDefName))
                {
                    return null;
                }
                return GetOriginal(EnsureLoaded(), BuildKey(characterId, level0), currentDefName);
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.GetOriginalDefName failed: " + ex.Message);
                return null;
            }
        }

        private static Dictionary<string, string> EnsureLoaded()
        {
            return _map ?? (_map = Load(FilePath));
        }

        // ---- pure core (unit-tested) ----------------------------------------------------------

        /// <summary>Stable per-(soldier, slot) key. The soldier id comes from <c>GeoCharacter.Id</c>.</summary>
        public static string BuildKey(int characterId, int level0)
        {
            return characterId.ToString(CultureInfo.InvariantCulture) + "#"
                   + level0.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Apply one swap to <paramref name="map"/>: remember the original on the first write only, keep
        /// the "current" pointer up to date, and clear the whole entry once the slot is back to the
        /// original. Returns true when the entry was cleared (slot restored).
        /// </summary>
        public static bool Record(IDictionary<string, string> map, string key, string originalName, string newName)
        {
            if (map == null || string.IsNullOrEmpty(key)
                || string.IsNullOrEmpty(originalName) || string.IsNullOrEmpty(newName))
            {
                return false;
            }

            string oKey = OriginalPrefix + key;
            string cKey = CurrentPrefix + key;

            if (!map.TryGetValue(oKey, out string stored) || string.IsNullOrEmpty(stored))
            {
                stored = originalName;
                map[oKey] = stored; // first write wins; a later swap must not overwrite it
            }

            if (string.Equals(stored, newName, StringComparison.Ordinal))
            {
                map.Remove(oKey);   // back to the default -> forget the slot
                map.Remove(cKey);
                return true;
            }

            map[cKey] = newName;
            return false;
        }

        /// <summary>
        /// The original def name to offer as a revert target, or null. Requires a complete entry whose
        /// recorded "current" still matches <paramref name="currentName"/> and differs from the original.
        /// </summary>
        public static string GetOriginal(IDictionary<string, string> map, string key, string currentName)
        {
            if (map == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(currentName))
            {
                return null;
            }
            if (!map.TryGetValue(OriginalPrefix + key, out string original) || string.IsNullOrEmpty(original))
            {
                return null;
            }
            if (!map.TryGetValue(CurrentPrefix + key, out string current)
                || !string.Equals(current, currentName, StringComparison.Ordinal))
            {
                return null; // stale / another campaign's soldier / someone else rewrote the slot
            }
            return string.Equals(original, currentName, StringComparison.Ordinal) ? null : original;
        }

        // ---- file + JSON (deliberately tiny; the document is a flat string->string map) --------

        /// <summary>Read the map from <paramref name="path"/>. Missing, unreadable or corrupt file =&gt; empty.</summary>
        public static Dictionary<string, string> Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return NewMap();
                }
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.Load failed: " + ex.Message);
                return NewMap();
            }
        }

        /// <summary>Write the map to <paramref name="path"/>. A null path or any IO error is a silent no-op.</summary>
        public static void Save(string path, IDictionary<string, string> map)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || map == null)
                {
                    return;
                }
                File.WriteAllText(path, Serialize(map), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.Save failed: " + ex.Message);
            }
        }

        public static string Serialize(IDictionary<string, string> map)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            if (map != null)
            {
                foreach (KeyValuePair<string, string> kv in map)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                    {
                        continue;
                    }
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    Quote(sb, kv.Key).Append(':');
                    Quote(sb, kv.Value);
                }
            }
            return sb.Append('}').ToString();
        }

        /// <summary>
        /// Parse the flat <c>{"k":"v",...}</c> document. Only OUR prefixed keys are kept, so anything
        /// unexpected in the file is ignored rather than trusted. Never throws: garbage in =&gt; empty map.
        /// </summary>
        public static Dictionary<string, string> Parse(string json)
        {
            Dictionary<string, string> map = NewMap();
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    return map;
                }
                string trimmed = json.Trim();
                if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                {
                    return map; // not our document at all
                }

                // The document has string keys and string values only, so the quoted tokens alternate
                // key, value, key, value... Reading them pairwise beats dragging in a JSON parser.
                int i = 1;
                while (true)
                {
                    string key = NextString(trimmed, ref i);
                    if (key == null)
                    {
                        break;
                    }
                    string value = NextString(trimmed, ref i);
                    if (value == null)
                    {
                        break;
                    }
                    if (key.StartsWith(OriginalPrefix, StringComparison.Ordinal)
                        || key.StartsWith(CurrentPrefix, StringComparison.Ordinal))
                    {
                        map[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.Parse failed: " + ex.Message);
                return NewMap();
            }
            return map;
        }

        private static Dictionary<string, string> NewMap()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>Next double-quoted token from <paramref name="i"/> on, unescaped; null at end of input.</summary>
        private static string NextString(string s, ref int i)
        {
            while (i < s.Length && s[i] != '"')
            {
                i++;
            }
            if (i >= s.Length)
            {
                return null;
            }
            i++; // opening quote
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(e); break; // covers \" \\ \/ and anything exotic
                    }
                    continue;
                }
                sb.Append(c);
            }
            if (i >= s.Length)
            {
                return null; // unterminated
            }
            i++; // closing quote
            return sb.ToString();
        }

        private static StringBuilder Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"');
        }
    }
}
