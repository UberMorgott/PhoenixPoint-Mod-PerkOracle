using System;
using System.Collections.Generic;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// Pure ordering/dedup/resolve core for a subclass's guaranteed class-track perks. Has no Unity,
    /// TFTV or Harmony dependency: callers inject the ordered candidate names (read from the class
    /// ability track) and a name->def resolver, so the selection logic is unit-testable with fakes.
    /// Class perks are deterministic, so unlike <see cref="PerkPoolResolver"/> there is no class
    /// exclusion filter — only ordering, de-duplication and resolver-miss skipping. The game-side
    /// wiring (reading the track, resolving defs) lives in <c>ClassPerkProvider</c>.
    /// </summary>
    public static class ClassPerkResolver
    {
        /// <summary>
        /// Resolve an ordered class-track name list into an ordered, de-duplicated list of defs,
        /// dropping empty names and names that do not resolve. Order follows <paramref name="rawNames"/>.
        /// </summary>
        /// <typeparam name="T">Def type (a real TacticalAbilityDef in game; a fake in tests).</typeparam>
        /// <param name="rawNames">Ordered candidate names for the class track; null => empty result.</param>
        /// <param name="resolve">name -> def, or default(T)/null when the name has no def.</param>
        public static List<T> Resolve<T>(
            IReadOnlyList<string> rawNames,
            Func<string, T> resolve)
        {
            var result = new List<T>();
            if (rawNames == null || resolve == null)
            {
                return result;
            }

            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in rawNames)
            {
                if (string.IsNullOrEmpty(name) || !seenNames.Add(name))
                {
                    continue;
                }

                T def = resolve(name);
                // Reference types: skip nulls. Value types never null, kept as-is.
                if (def == null)
                {
                    continue;
                }
                result.Add(def);
            }

            return result;
        }
    }
}
