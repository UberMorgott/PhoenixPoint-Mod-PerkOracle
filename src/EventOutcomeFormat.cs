using System.Collections.Generic;
using System.Globalization;

namespace Morgott.Oracle
{
    /// <summary>
    /// Pure, engine-free formatting primitives for authored event-outcome preview lines. Holds the
    /// only substitution logic with edge cases worth unit-testing (RNG range collapse, signed values,
    /// single-arg pattern fill, name joining), so it links + tests under net8 like EventOutcomePreview.
    /// The engine-aware <see cref="EventOutcomeAdapter"/> resolves the loc PATTERNS and def names, then
    /// calls these to produce the final sentence. No Unity / engine / Harmony / I2 dependency.
    /// </summary>
    public static class EventOutcomeFormat
    {
        /// <summary>
        /// Render an RNG value as a range "[Min..Max]", collapsing to a single value when Min == Max.
        /// Used for RangeDataInt-valued outcomes (variable changes, sub-faction mission weight) so the
        /// preview NEVER shows a fabricated single roll. Invariant culture for stable digits.
        /// </summary>
        public static string Range(int min, int max)
        {
            if (min == max)
            {
                return min.ToString(CultureInfo.InvariantCulture);
            }
            return "[" + min.ToString(CultureInfo.InvariantCulture) + ".." + max.ToString(CultureInfo.InvariantCulture) + "]";
        }

        /// <summary>Format an int as "+5"/"-10"/"0" (invariant), matching the native reward sign style.</summary>
        public static string Signed(int value) => value.ToString("+#;-#;0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Fill a single-placeholder localized pattern with <paramref name="arg0"/>. Returns empty when the
        /// pattern is null/empty (the caller then adds no row), so a missing loc term never yields a stray
        /// "{0}" line. The pattern is supplied by the engine layer (native key or Loc.Get); this stays pure.
        /// </summary>
        public static string Format1(string pattern, object arg0)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return string.Empty;
            }
            return string.Format(pattern, arg0);
        }

        /// <summary>
        /// Fill a two-placeholder localized pattern with <paramref name="arg0"/> and <paramref name="arg1"/>.
        /// Returns empty when the pattern is null/empty (the caller then adds no row), so a missing loc term
        /// never yields a stray "{0}: {1}" line. The pattern is supplied by the engine layer; this stays pure.
        /// </summary>
        public static string Format2(string pattern, object arg0, object arg1)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return string.Empty;
            }
            return string.Format(pattern, arg0, arg1);
        }

        /// <summary>
        /// Join already-resolved display names with <paramref name="separator"/>, skipping null/empty
        /// entries. Returns empty when nothing remains (caller adds no row). Used for multi-research /
        /// multi-phoenixpedia / multi-event lines so a single sentence lists all granted names.
        /// </summary>
        public static string JoinNames(IReadOnlyList<string> names, string separator)
        {
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder();
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n))
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append(separator);
                }
                sb.Append(n);
            }
            return sb.ToString();
        }
    }
}
