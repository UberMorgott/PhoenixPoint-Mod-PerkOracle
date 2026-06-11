using System.Collections.Generic;
using System.Globalization;

namespace Morgott.Oracle
{
    /// <summary>
    /// Pure, engine-free formatter that turns a previewable <see cref="EventOutcomeData"/> into an
    /// ordered list of display rows. One row per non-empty effect; zero/empty fields are skipped.
    /// Diplomacy/resources render signed ("+#"/"-#"); items/site-reveals render "xN"; the two range-rolled
    /// fields render "Min-Max"; zone damage renders "N%". Soldier/aircraft/skill-point effects arrive as
    /// fully-formed native reward sentences in <see cref="EventOutcomeData.NativeLines"/> (localized +
    /// number-substituted by <see cref="EventOutcomeAdapter"/>) and are emitted verbatim as name-only rows.
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

            // Reputation / diplomacy (signed int).
            foreach (EventOutcomeData.DiplomacyEntry d in data.Diplomacy)
            {
                if (d.Value != 0)
                {
                    rows.Add(new EventOutcomeRow(d.TargetLabel, Signed(d.Value)));
                }
            }

            // Resources (signed rounded int).
            foreach (EventOutcomeData.ResourceEntry r in data.Resources)
            {
                if (r.Value != 0)
                {
                    rows.Add(new EventOutcomeRow(r.Name, Signed(r.Value)));
                }
            }

            // Items granted ("x N").
            foreach (EventOutcomeData.ItemEntry it in data.Items)
            {
                if (it.Count != 0)
                {
                    rows.Add(new EventOutcomeRow(it.Name, "x" + it.Count.ToString(CultureInfo.InvariantCulture)));
                }
            }

            // Granted research (name only).
            foreach (string research in data.Researches)
            {
                if (!string.IsNullOrEmpty(research))
                {
                    rows.Add(new EventOutcomeRow(research, string.Empty));
                }
            }

            // Site reveals ("x N").
            foreach (EventOutcomeData.ItemEntry s in data.RevealSites)
            {
                rows.Add(new EventOutcomeRow(s.Name, s.Count > 0 ? "x" + s.Count.ToString(CultureInfo.InvariantCulture) : string.Empty));
            }

            // Native reward sentences (soldier/aircraft damage, tiredness, faction skill points): already
            // localized + number-substituted from the game's own loc keys by EventOutcomeAdapter, so they are
            // rendered verbatim as a name-only row (the sentence already contains its value).
            foreach (string line in data.NativeLines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    rows.Add(new EventOutcomeRow(line, string.Empty));
                }
            }

            // Range-rolled variable changes ("Min-Max").
            foreach (EventOutcomeData.RangeEntry v in data.VariableChanges)
            {
                rows.Add(new EventOutcomeRow(v.Label, Range(v.Min, v.Max)));
            }

            // Range-rolled subfaction mission-weight changes ("Min-Max").
            foreach (EventOutcomeData.RangeEntry w in data.MissionWeightChanges)
            {
                rows.Add(new EventOutcomeRow(w.Label, Range(w.Min, w.Max)));
            }

            // Zone damage ("N%").
            foreach (EventOutcomeData.ZoneDamageEntry z in data.ZoneDamages)
            {
                if (z.Percent != 0)
                {
                    rows.Add(new EventOutcomeRow(z.ZoneLabel, z.Percent.ToString(CultureInfo.InvariantCulture) + "%"));
                }
            }

            return rows;
        }

        /// <summary>Render a rolled range as "Min-Max", or just the single value when Min==Max.</summary>
        private static string Range(int min, int max)
        {
            if (min == max)
            {
                return min.ToString(CultureInfo.InvariantCulture);
            }
            return min.ToString(CultureInfo.InvariantCulture) + "-" + max.ToString(CultureInfo.InvariantCulture);
        }
    }
}
