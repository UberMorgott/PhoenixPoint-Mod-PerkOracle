using System;
using System.Collections.Generic;
using System.Globalization;

namespace Morgott.Oracle
{
    /// <summary>
    /// Remembers the ability a progression slot held the FIRST time PerkOracle changed it, so the player
    /// can put the slot back to its default. Engine-free on purpose (plain strings, no Unity/IO) so the
    /// whole keying / first-write-wins / clear-on-revert core unit-tests under net8.
    ///
    /// PERSISTENCE is not this class's business: <see cref="OracleGeoscapeMod"/> hands the map to the
    /// game's own mod-instance-data API, which stores it INSIDE the savegame. This type only owns the
    /// in-memory document — one flat string map, two entries per tracked slot:
    ///   "o:&lt;charId&gt;#&lt;level0&gt;#&lt;track&gt;" -> def name the slot ORIGINALLY had (written once, never overwritten)
    ///   "c:&lt;charId&gt;#&lt;level0&gt;#&lt;track&gt;" -> def name PerkOracle LAST wrote into that slot
    ///
    /// The "c:" half is a guard, not decoration. <c>GeoCharacter.Id</c> is a serialized int that is unique
    /// only WITHIN one campaign (Assembly-CSharp exposes no campaign/save id at all — verified), and TFTV
    /// or another mod can rewrite a slot behind us. So a stored entry is honoured only while its recorded
    /// "current" still equals what the slot actually holds; a stale id or an outside edit therefore
    /// silently offers nothing instead of offering the wrong perk.
    ///
    /// EXCEPTION, deliberate: <see cref="Observe"/> re-points "current" at what the slot actually holds
    /// whenever the wiki looks at a slot that already has a baseline. Without that, a slot rewritten by
    /// TFTV's own DrillSwapUI reads as stale forever and can never be put back — the case this store
    /// exists for.
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
        /// Diagnostic sink, wired to <c>OracleLog.Debug</c> by <c>OracleMain</c>. A delegate rather than a
        /// direct call so this file stays Unity-free and links straight into the net8 test project.
        /// </summary>
        public static Action<string> Log;

        private static Dictionary<string, string> _map;

        /// <summary>
        /// Drop the whole in-memory history. Called at geoscape start (before the save's own history is
        /// handed back), so a new campaign never inherits the previous one's map.
        /// </summary>
        public static void Clear()
        {
            _map = null;
        }

        /// <summary>Copy of the history, for the game to serialize into the savegame.</summary>
        public static Dictionary<string, string> Snapshot()
        {
            return new Dictionary<string, string>(EnsureLoaded(), StringComparer.Ordinal);
        }

        /// <summary>
        /// Replace the history with <paramref name="map"/> restored from the savegame. Only our own
        /// prefixed, non-empty entries are taken, so foreign or half-written
        /// content lands as an empty store instead of junk. Never throws.
        /// </summary>
        public static void LoadFrom(IDictionary<string, string> map)
        {
            Dictionary<string, string> loaded = NewMap();
            if (map != null)
            {
                foreach (KeyValuePair<string, string> kv in map)
                {
                    if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value))
                    {
                        continue;
                    }
                    if (kv.Key.StartsWith(OriginalPrefix, StringComparison.Ordinal)
                        || kv.Key.StartsWith(CurrentPrefix, StringComparison.Ordinal))
                    {
                        loaded[kv.Key] = kv.Value;
                    }
                }
            }
            _map = loaded;
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
                Record(EnsureLoaded(), BuildKey(characterId, level0, trackKey), originalDefName, newDefName);
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
                Observe(EnsureLoaded(), BuildKey(characterId, level0, trackKey), currentDefName);
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
            return _map ?? (_map = NewMap());
        }

        private static Dictionary<string, string> NewMap()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
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
    }
}
