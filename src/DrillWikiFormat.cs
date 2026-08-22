using System;
using System.Collections.Generic;

namespace Morgott.Oracle
{
    /// <summary>
    /// Pure ordering/requirement-wording core for the wiki's TFTV drills row. Has no Unity, TFTV or
    /// Harmony dependency: callers pass already-resolved display names, so the logic is unit-testable
    /// with fakes (same split as <see cref="ClassPerkResolver"/> / <c>PerkPoolResolver</c>). The
    /// game-side wiring (reading <see cref="TftvDrillsBridge.RequirementTable"/>, resolving names,
    /// building cells) lives in <c>PerkWikiPanel</c>.
    /// </summary>
    public static class DrillWikiFormat
    {
        /// <summary>Alphabetical (case-insensitive, stable) browse order; entries with no name drop out.</summary>
        public static List<T> SortByName<T>(IEnumerable<T> items, Func<T, string> name)
        {
            var result = new List<T>();
            if (items == null || name == null)
            {
                return result;
            }
            foreach (T item in items)
            {
                if (!string.IsNullOrEmpty(name(item)))
                {
                    result.Add(item);
                }
            }
            result.Sort((a, b) => string.Compare(name(a), name(b), StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// The class+level gate wording, mirroring TFTV's own line ("Level: 3 assault",
        /// DrillsUnlock.BuildClassRequirementMessage:482). A null/empty class name means "any class" and
        /// renders <paramref name="anyClassLabel"/>. TFTV requires EVERY gate
        /// (MeetsClassLevelRequirements:393 fails on the first unmet one), so gates join with ", ".
        /// Returns null when there is no gate.
        /// </summary>
        public static string ClassLevelLine(IReadOnlyList<KeyValuePair<string, int>> gates, string anyClassLabel)
        {
            if (gates == null || gates.Count == 0)
            {
                return null;
            }
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> gate in gates)
            {
                string cls = string.IsNullOrEmpty(gate.Key) ? anyClassLabel : gate.Key;
                parts.Add("Level: " + gate.Value + " " + cls);
            }
            return parts.Count == 0 ? null : string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// The weapon-proficiency wording: each group is an OR-set ("A or B"), and every group is
        /// required (MeetsWeaponProficiencyRequirements:464), so groups join with ", ". Empty groups and
        /// empty names are skipped; returns null when nothing is left.
        /// </summary>
        public static string ProficiencyLine(IEnumerable<IReadOnlyList<string>> groups)
        {
            if (groups == null)
            {
                return null;
            }
            var parts = new List<string>();
            foreach (IReadOnlyList<string> group in groups)
            {
                if (group == null)
                {
                    continue;
                }
                var names = new List<string>();
                foreach (string n in group)
                {
                    if (!string.IsNullOrEmpty(n) && !names.Contains(n))
                    {
                        names.Add(n);
                    }
                }
                if (names.Count > 0)
                {
                    parts.Add(string.Join(" or ", names.ToArray()));
                }
            }
            return parts.Count == 0 ? null : string.Join(", ", parts.ToArray());
        }
    }
}
