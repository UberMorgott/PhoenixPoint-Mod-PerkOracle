using System.Collections.Generic;

namespace Morgott.Oracle
{
    /// <summary>
    /// One rendered preview line: an optional left label/header, a localized name, and a
    /// signed/ranged value string. Pure data — produced by <see cref="EventOutcomePreview"/>,
    /// consumed by the UI tooltip. No engine/Unity types so it links into the net8 test project.
    /// </summary>
    public sealed class EventOutcomeRow
    {
        /// <summary>Localized name of the thing changing (e.g. resource name, "Reputation", item name).</summary>
        public string Label;

        /// <summary>Pre-formatted value text (e.g. "+5", "-10", "3-7", "25%", "x2"). May be empty for name-only rows.</summary>
        public string Value;

        public EventOutcomeRow(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    /// <summary>
    /// Plain transport of one <c>GeoEventChoiceOutcome</c>'s previewable fields, already reduced to
    /// primitives + localized strings by <see cref="EventOutcomeAdapter"/> (the only engine-aware layer).
    /// Kept free of Unity/engine/Harmony/I2 types so <see cref="EventOutcomePreview"/> stays net8-testable.
    /// </summary>
    public sealed class EventOutcomeData
    {
        /// <summary>Reputation deltas: (already-localized target-faction label, signed int value).</summary>
        public List<DiplomacyEntry> Diplomacy = new List<DiplomacyEntry>();

        /// <summary>Resource deltas: (already-localized resource name, rounded int value). Excludes zero/None.</summary>
        public List<ResourceEntry> Resources = new List<ResourceEntry>();

        /// <summary>Granted items: (already-localized item name, count).</summary>
        public List<ItemEntry> Items = new List<ItemEntry>();

        /// <summary>Granted research project display names (already localized).</summary>
        public List<string> Researches = new List<string>();

        /// <summary>Range-rolled variable changes: (localized variable label, Min, Max).</summary>
        public List<RangeEntry> VariableChanges = new List<RangeEntry>();

        /// <summary>Range-rolled subfaction mission-weight changes: (localized subfaction label, Min, Max).</summary>
        public List<RangeEntry> MissionWeightChanges = new List<RangeEntry>();

        /// <summary>Zone-damage entries: (localized zone label, percent of zone max HP).</summary>
        public List<ZoneDamageEntry> ZoneDamages = new List<ZoneDamageEntry>();

        /// <summary>Site reveals: (localized site label, count).</summary>
        public List<ItemEntry> RevealSites = new List<ItemEntry>();

        /// <summary>
        /// Fully-formatted, already-localized native reward sentences (soldier/aircraft damage, tiredness,
        /// faction skill points). Each string is produced by <see cref="EventOutcomeAdapter"/> from the live
        /// encounter module's own <c>LocalizedTextBind</c> keys with the magnitude already substituted, so it
        /// reads identically to the game's native post-choice reward line in any language. Rendered verbatim as
        /// a name-only row (no separate value column) by <see cref="EventOutcomePreview"/>.
        /// </summary>
        public List<string> NativeLines = new List<string>();

        public struct DiplomacyEntry
        {
            public string TargetLabel;
            public int Value;
            public DiplomacyEntry(string targetLabel, int value) { TargetLabel = targetLabel; Value = value; }
        }

        public struct ResourceEntry
        {
            public string Name;
            public int Value;
            public ResourceEntry(string name, int value) { Name = name; Value = value; }
        }

        public struct ItemEntry
        {
            public string Name;
            public int Count;
            public ItemEntry(string name, int count) { Name = name; Count = count; }
        }

        public struct RangeEntry
        {
            public string Label;
            public int Min;
            public int Max;
            public RangeEntry(string label, int min, int max) { Label = label; Min = min; Max = max; }
        }

        public struct ZoneDamageEntry
        {
            public string ZoneLabel;
            public int Percent;
            public ZoneDamageEntry(string zoneLabel, int percent) { ZoneLabel = zoneLabel; Percent = percent; }
        }
    }
}
