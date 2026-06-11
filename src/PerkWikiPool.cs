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
        /// <summary>
        /// Candidate perks for the rolled slot at <paramref name="level0"/> for a soldier of
        /// <paramref name="className"/> (null disables the class filter). Never null; empty on a miss.
        /// </summary>
        public static List<TacticalAbilityDef> ResolveForSlot(int level0, string className)
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
