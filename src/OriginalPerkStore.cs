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
    /// EXCEPTION, deliberate: <see cref="Observe"/> re-points "current" at what the slot actually holds
    /// whenever the wiki looks at a slot that already has a baseline. Without that, a slot rewritten by
    /// TFTV's own DrillSwapUI reads as stale forever and can never be put back — the case this store
    /// exists for. The trade is that a cross-campaign id collision on a slot with a baseline can now be
    /// offered the OTHER campaign's default as its "default" cell; clicking it is still an ordinary,
    /// fully-gated swap, so the worst case is one misleading marker, not a broken soldier.
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

        /// <summary>
        /// Set when the document on disk could not be understood. The store then runs from an EMPTY
        /// in-memory map but NEVER writes, so an unreadable file is quarantined for inspection instead
        /// of being silently replaced with a fresh empty one (which would erase the player's history).
        /// </summary>
        private static bool _readOnly;

        /// <summary>Drop the in-memory cache so the next call re-reads <see cref="FilePath"/>.</summary>
        public static void Reset()
        {
            _map = null;
            _readOnly = false;
        }

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
        public static void RecordSwap(int characterId, int level0, string trackKey,
            string originalDefName, string newDefName)
        {
            try
            {
                if (level0 < 0 || string.IsNullOrEmpty(originalDefName) || string.IsNullOrEmpty(newDefName))
                {
                    return;
                }
                Dictionary<string, string> map = EnsureLoaded();
                Record(map, BuildKey(characterId, level0, trackKey), originalDefName, newDefName);
                if (!_readOnly)
                {
                    Save(FilePath, map);
                }
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.RecordSwap failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Note what a slot holds the FIRST time the wiki looks at it, and keep the "current" pointer in
        /// step afterwards. This is what makes a slot changed by SOMEONE ELSE (TFTV's own DrillSwapUI is
        /// the normal way a player takes a drill) revertable: without a baseline there is no entry at all
        /// and no default cell can be offered. Never throws.
        /// </summary>
        public static void ObserveSlot(int characterId, int level0, string trackKey, string currentDefName)
        {
            try
            {
                if (level0 < 0 || string.IsNullOrEmpty(currentDefName))
                {
                    return;
                }
                Dictionary<string, string> map = EnsureLoaded();
                if (Observe(map, BuildKey(characterId, level0, trackKey), currentDefName) && !_readOnly)
                {
                    Save(FilePath, map);
                }
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.ObserveSlot failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Def name this slot originally held, or null when nothing is stored, the stored "current" no
        /// longer matches <paramref name="currentDefName"/> (other campaign / edited behind us), or the
        /// slot is already back to its original. Never throws.
        /// </summary>
        public static string GetOriginalDefName(int characterId, int level0, string trackKey, string currentDefName)
        {
            try
            {
                if (level0 < 0 || string.IsNullOrEmpty(currentDefName))
                {
                    return null;
                }
                return GetOriginal(EnsureLoaded(), BuildKey(characterId, level0, trackKey), currentDefName);
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.GetOriginalDefName failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// True when this slot has a recorded baseline, i.e. PerkOracle or its wiki has already observed
        /// or changed it. READ-ONLY on purpose: unlike <see cref="ObserveSlot"/> it never writes, so the
        /// right-click eligibility gate cannot baseline a slot just by hovering it. Never throws.
        /// </summary>
        public static bool HasBaseline(int characterId, int level0, string trackKey)
        {
            try
            {
                return level0 >= 0 && HasBaseline(EnsureLoaded(), BuildKey(characterId, level0, trackKey));
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.HasBaseline failed: " + ex.Message);
                return false;
            }
        }

        private static Dictionary<string, string> EnsureLoaded()
        {
            if (_map == null)
            {
                _map = Load(FilePath, out _readOnly);
            }
            return _map;
        }

        // ---- pure core (unit-tested) ----------------------------------------------------------

        /// <summary>
        /// Stable per-(soldier, track, slot) key. The soldier id comes from <c>GeoCharacter.Id</c>;
        /// <paramref name="trackKey"/> is the slot's track source, without which two slots at the same
        /// level in different tracks share one entry. Entries written before the track dimension existed
        /// use the 2-part shape and are simply never matched again (they are inert, not migrated).
        /// </summary>
        public static string BuildKey(int characterId, int level0, string trackKey)
        {
            return characterId.ToString(CultureInfo.InvariantCulture) + "#"
                   + level0.ToString(CultureInfo.InvariantCulture) + "#"
                   + (trackKey ?? string.Empty);
        }

        /// <summary>
        /// Baseline-on-observe: remember what the slot holds when it is first seen, and refresh the
        /// "current" pointer when it has changed since (someone else — e.g. TFTV's DrillSwapUI — wrote
        /// the slot), so the baseline stays offerable instead of reading as stale. Drops the entry once
        /// the slot is back at its baseline. Returns true when <paramref name="map"/> changed.
        /// </summary>
        public static bool Observe(IDictionary<string, string> map, string key, string currentName)
        {
            if (map == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(currentName))
            {
                return false;
            }

            string oKey = OriginalPrefix + key;
            string cKey = CurrentPrefix + key;

            if (!map.TryGetValue(oKey, out string original) || string.IsNullOrEmpty(original))
            {
                map[oKey] = currentName; // first sighting IS the baseline
                map[cKey] = currentName;
                return true;
            }

            bool hasCurrent = map.TryGetValue(cKey, out string current) && !string.IsNullOrEmpty(current);
            bool atBaseline = string.Equals(original, currentName, StringComparison.Ordinal);
            bool unchanged = hasCurrent && string.Equals(current, currentName, StringComparison.Ordinal);

            if (atBaseline)
            {
                // Forget the slot ONLY when a transition was actually recorded and has now been undone.
                // Simply LOOKING at an untouched slot again (original == current == what it holds) must
                // be a no-op: dropping the entry there would throw away the true baseline, so a later
                // TFTV rewrite would be mistaken for the default and HasBaseline would stop offering
                // the right-click on an already-swapped slot.
                if (unchanged)
                {
                    return false;
                }
                if (!hasCurrent)
                {
                    map[cKey] = currentName; // half-written legacy entry -> complete it, keep the baseline
                    return true;
                }
                map.Remove(oKey);
                map.Remove(cKey);
                return true;
            }

            if (unchanged)
            {
                return false;           // already up to date
            }
            map[cKey] = currentName;
            return true;
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
        /// True when <paramref name="map"/> carries an original for <paramref name="key"/>. Says only
        /// "this slot is tracked", NOT "a revert is offerable" — that is <see cref="GetOriginal"/>.
        /// </summary>
        public static bool HasBaseline(IDictionary<string, string> map, string key)
        {
            return map != null && !string.IsNullOrEmpty(key)
                   && map.TryGetValue(OriginalPrefix + key, out string original)
                   && !string.IsNullOrEmpty(original);
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
            return Load(path, out bool _);
        }

        /// <summary>
        /// Read the map from <paramref name="path"/>, reporting in <paramref name="corrupt"/> whether the
        /// document was there but could not be understood. A corrupt document is NOT a fresh empty store:
        /// the caller must go read-only so the unreadable file is kept for the player instead of being
        /// overwritten by the next observation. Recovery order: the live file, then the rotated
        /// <c>.bak</c>; when the live file is missing entirely, an interrupted write's <c>.tmp</c> and the
        /// <c>.bak</c> are tried before giving up. Never throws.
        /// </summary>
        public static Dictionary<string, string> Load(string path, out bool corrupt)
        {
            corrupt = false;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return NewMap();
                }
                if (!File.Exists(path))
                {
                    // A crash between "write .tmp" and "swap it in" can leave the document only there.
                    return Recover(path + ".tmp") ?? Recover(path + ".bak") ?? NewMap();
                }
                if (TryParse(File.ReadAllText(path, Encoding.UTF8), out Dictionary<string, string> map))
                {
                    return map;
                }
                Dictionary<string, string> fromBak = Recover(path + ".bak");
                if (fromBak != null)
                {
                    Warn("[Oracle] OriginalPerkStore: '" + path
                         + "' is corrupt; recovered the previous document from '" + path + ".bak'.");
                    return fromBak;
                }
                corrupt = true;
                Warn("[Oracle] OriginalPerkStore: '" + path + "' could not be parsed and no usable backup"
                     + " exists. The file is left UNTOUCHED and swap history will not be written this"
                     + " session — move or delete it to start a fresh store.");
                return NewMap();
            }
            catch (Exception ex)
            {
                corrupt = true;
                Warn("[Oracle] OriginalPerkStore.Load failed: " + ex.Message);
                return NewMap();
            }
        }

        /// <summary>Parse <paramref name="path"/> if it exists and is valid, else null. Never throws.</summary>
        private static Dictionary<string, string> Recover(string path)
        {
            try
            {
                return File.Exists(path)
                       && TryParse(File.ReadAllText(path, Encoding.UTF8), out Dictionary<string, string> map)
                    ? map
                    : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Write the map to <paramref name="path"/>. A null path or any IO error is a silent no-op.
        /// Writes a sibling ".tmp" first and then swaps it in, so a crash mid-write can never leave a
        /// TRUNCATED document behind, and keeps the document it replaced as a sibling ".bak" that
        /// <see cref="Load(string,out bool)"/> falls back to. No path here ever deletes the only
        /// known-good copy of the store.
        /// </summary>
        public static void Save(string path, IDictionary<string, string> map)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || map == null)
                {
                    return;
                }
                string tmp = path + ".tmp";
                string bak = path + ".bak";
                File.WriteAllText(tmp, Serialize(map), Encoding.UTF8);
                if (!File.Exists(path))
                {
                    File.Move(tmp, path);
                    return;
                }
                try
                {
                    File.Replace(tmp, path, bak); // atomic on NTFS, and rotates a real backup
                }
                catch (Exception ex)
                {
                    // Replace is unsupported on a few filesystems. Fall back to rename-aside + move, which
                    // never deletes the only good copy: the live document survives as ".bak" until the new
                    // one is proven in place, and is renamed straight back if the move fails.
                    Warn("[Oracle] OriginalPerkStore.Save atomic replace failed: " + ex.Message);
                    try
                    {
                        File.Delete(bak);
                    }
                    catch
                    {
                        // a stale backup we cannot remove is not a reason to lose the write
                    }
                    File.Move(path, bak);
                    try
                    {
                        File.Move(tmp, path);
                    }
                    catch
                    {
                        File.Move(bak, path); // roll back: the known-good document goes home
                        throw;
                    }
                }
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
        /// Parse the flat <c>{"k":"v",...}</c> document, or an empty map when it is not valid. Kept for
        /// callers that cannot act on the difference; anything that WRITES must use
        /// <see cref="TryParse"/>, because "invalid" and "empty" mean opposite things on disk.
        /// </summary>
        public static Dictionary<string, string> Parse(string json)
        {
            return TryParse(json, out Dictionary<string, string> map) ? map : NewMap();
        }

        /// <summary>
        /// Strictly validate and read the flat <c>{"k":"v",...}</c> document: whitespace, then either
        /// <c>{}</c> or comma-separated <c>"string" : "string"</c> pairs, then end of input. ANY
        /// structural defect — a truncated tail, a trailing comma, a missing colon, a non-string value,
        /// trailing junk — rejects the WHOLE document instead of returning the part that happened to
        /// scan, which is what let a half-readable file overwrite a good one. Only OUR prefixed keys are
        /// kept, so foreign-but-valid content reads as an empty (valid) store. Never throws.
        /// </summary>
        public static bool TryParse(string json, out Dictionary<string, string> map)
        {
            if (Scan(json, out Dictionary<string, string> parsed))
            {
                map = parsed;
                return true;
            }
            map = NewMap(); // a rejected document yields NOTHING, never the part that happened to scan
            return false;
        }

        private static bool Scan(string json, out Dictionary<string, string> map)
        {
            map = NewMap();
            try
            {
                if (json == null)
                {
                    return false;
                }
                int i = 0;
                SkipWhite(json, ref i);
                if (i >= json.Length)
                {
                    return true; // an empty file is an empty store, not a corrupt one
                }
                if (json[i++] != '{')
                {
                    return false;
                }

                SkipWhite(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                }
                else
                {
                    while (true)
                    {
                        string key = ReadString(json, ref i);
                        if (key == null)
                        {
                            return false;
                        }
                        SkipWhite(json, ref i);
                        if (i >= json.Length || json[i++] != ':')
                        {
                            return false;
                        }
                        SkipWhite(json, ref i);
                        string value = ReadString(json, ref i);
                        if (value == null)
                        {
                            return false;
                        }
                        if (key.StartsWith(OriginalPrefix, StringComparison.Ordinal)
                            || key.StartsWith(CurrentPrefix, StringComparison.Ordinal))
                        {
                            map[key] = value;
                        }
                        SkipWhite(json, ref i);
                        if (i >= json.Length)
                        {
                            return false; // truncated: no closing brace
                        }
                        if (json[i] == ',')
                        {
                            i++;
                            SkipWhite(json, ref i);
                            continue;
                        }
                        if (json[i] == '}')
                        {
                            i++;
                            break;
                        }
                        return false;
                    }
                }

                SkipWhite(json, ref i);
                if (i != json.Length)
                {
                    return false; // trailing junk after the document
                }
                return true;
            }
            catch (Exception ex)
            {
                Warn("[Oracle] OriginalPerkStore.TryParse failed: " + ex.Message);
                map = NewMap();
                return false;
            }
        }

        private static void SkipWhite(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }

        private static Dictionary<string, string> NewMap()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// The double-quoted token that MUST start at <paramref name="i"/>, unescaped; null when there is
        /// no opening quote there or the token is unterminated. Strict about position on purpose — the
        /// old "scan forward to the next quote" behaviour is what made malformed documents look parseable.
        /// </summary>
        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
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
