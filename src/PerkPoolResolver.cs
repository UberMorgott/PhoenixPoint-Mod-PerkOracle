using System;
using System.Collections.Generic;

namespace Morgott.Oracle
{
    /// <summary>
    /// Pure ordering/dedup/filter core for the perk wiki pool. Has no Unity, TFTV or Harmony
    /// dependency: callers inject the raw candidate names, an exclusion predicate and a name->def
    /// resolver, so the selection logic is unit-testable with fakes. The TFTV/vanilla wiring lives
    /// in <see cref="TftvConfigBridge"/>; this class only orders and resolves what it is handed.
    /// </summary>
    public static class PerkPoolResolver
    {
        /// <summary>
        /// Resolve a candidate name list into an ordered, de-duplicated list of defs, dropping
        /// class-excluded names and names that do not resolve. Order follows <paramref name="rawNames"/>.
        /// </summary>
        /// <typeparam name="T">Def type (a real TacticalAbilityDef in game; a fake in tests).</typeparam>
        /// <param name="rawNames">Candidate upper-case display names for the slot; null => empty result.</param>
        /// <param name="className">Soldier class name (e.g. "Assault"); null disables the exclusion filter.</param>
        /// <param name="isExcluded">(name, className) -> true if the name is blacklisted for that class.</param>
        /// <param name="resolve">name -> def, or default(T)/null when the name has no def.</param>
        public static List<T> OrderAndResolve<T>(
            IReadOnlyList<string> rawNames,
            string className,
            Func<string, string, bool> isExcluded,
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
                if (className != null && isExcluded != null && isExcluded(name, className))
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
