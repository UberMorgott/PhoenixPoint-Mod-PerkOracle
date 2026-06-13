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
    }
}
