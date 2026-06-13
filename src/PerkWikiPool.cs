using System.Collections.Generic;
using PhoenixPoint.Tactical.Entities.Abilities;

namespace Morgott.Oracle
{
    /// <summary>
    /// Game-side wiring that turns a (slot, class) into the candidate def list for the wiki: prefers
    /// the TFTV random pool, falls back to the global vanilla personal pool when TFTV is unavailable.
    /// The ordering/filter logic itself lives in the pure <see cref="PerkPoolResolver"/>; this layer
    /// only selects the source and references game types (so it stays out of the unit-tested unit).
    /// </summary>
    public static class PerkWikiPool
    {
        // Memo keyed by (level0, className). The underlying pools are static config (TFTV settings /
        // the def repository's PersonalProgressionTag set), so they never change within a session.
        // Resolving is now hit per cell on every progression-UI rebuild (the rolled-pool-membership
        // gate calls in), and GetVanillaPersonalPool does an uncached GetAllDefs().Where().ToList(),
        // so memoizing keeps it a one-time cost. No invalidation needed (config is immutable here).
        // The returned list is treated read-only by all callers (the wiki only enumerates it), so
        // handing back the cached reference is safe.
        private static readonly Dictionary<string, List<TacticalAbilityDef>> Cache =
            new Dictionary<string, List<TacticalAbilityDef>>();

        /// <summary>
        /// Candidate perks for the rolled slot at <paramref name="level0"/> for a soldier of
        /// <paramref name="className"/> (null disables the class filter). Never null; empty on a miss.
        /// Result is memoized per (level0, className).
        /// </summary>
        public static List<TacticalAbilityDef> ResolveForSlot(int level0, string className)
        {
            string key = level0 + "|" + (className ?? "<null>");
            if (Cache.TryGetValue(key, out List<TacticalAbilityDef> cached))
            {
                return cached;
            }

            List<TacticalAbilityDef> resolved = ResolveForSlotUncached(level0, className);
            Cache[key] = resolved;
            return resolved;
        }

        private static List<TacticalAbilityDef> ResolveForSlotUncached(int level0, string className)
        {
            if (TftvConfigBridge.Available
                && TftvConfigBridge.TryGetTftvRandomPool(level0, className, out List<TacticalAbilityDef> tftvDefs)
                && tftvDefs != null
                && tftvDefs.Count > 0)
            {
                return tftvDefs;
            }

            // No TFTV (or TFTV returned nothing for this slot): show the global vanilla personal pool.
            return TftvConfigBridge.GetVanillaPersonalPool();
        }
    }
}
