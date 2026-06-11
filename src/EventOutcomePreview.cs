using System.Collections.Generic;
using System.Globalization;

namespace Morgott.Oracle
{
    /// <summary>
    /// Pure, engine-free formatter that turns a previewable <see cref="EventOutcomeData"/> into an
    /// ordered list of display rows. Every row traces to a line the game's own reward UI renders:
    ///   • Resources render as a localized name + signed value ("+#"/"-#") — the native resource reward
    ///     line, kept two-column so the tooltip can colour the value natively.
    ///   • Everything else arrives as a fully-formed, already-localized native reward SENTENCE in
    ///     <see cref="EventOutcomeData.NativeLines"/> (built from the game's own loc keys / format and
    ///     number-substituted by <see cref="EventOutcomeAdapter"/>) and is emitted verbatim as a name-only row.
    /// No Unity, engine, Harmony or I2 dependency, so it unit-tests under net8 like PerkPoolResolver. All
    /// labels are already localized by <see cref="EventOutcomeAdapter"/>; this class only orders + number-formats.
    /// </summary>
    public static class EventOutcomePreview
    {
        /// <summary>Format an int as "+5"/"-10" (zero never reaches here for scalar rows).</summary>
        private static string Signed(int v) => v.ToString("+#;-#;0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Build the ordered preview rows for one outcome. Returns an empty list for a null or
        /// all-empty outcome (caller then shows no tooltip).
        /// </summary>
        public static List<EventOutcomeRow> Build(EventOutcomeData data)
        {
            var rows = new List<EventOutcomeRow>();
            if (data == null)
            {
                return rows;
            }

            // Resources (native resource reward line): localized name + signed rounded int.
            foreach (EventOutcomeData.ResourceEntry r in data.Resources)
            {
                if (r.Value != 0)
                {
                    rows.Add(new EventOutcomeRow(r.Name, Signed(r.Value)));
                }
            }

            // Native reward sentences (soldier/aircraft damage, tiredness, faction skill points, site
            // reveals, faction-party diplomacy, granted items): already localized + number-substituted from
            // the game's own reward-UI loc keys / format by EventOutcomeAdapter, so each is rendered verbatim
            // as a name-only row (the sentence already contains its value).
            foreach (string line in data.NativeLines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    rows.Add(new EventOutcomeRow(line, string.Empty));
                }
            }

            return rows;
        }
    }
}
