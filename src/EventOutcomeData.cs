using System.Collections.Generic;

namespace Morgott.Oracle
{
    /// <summary>
    /// One rendered preview line: an optional left label/header, a localized name, and a
    /// signed value string. Pure data — produced by <see cref="EventOutcomePreview"/>,
    /// consumed by the UI tooltip. No engine/Unity types so it links into the net8 test project.
    /// </summary>
    public sealed class EventOutcomeRow
    {
        /// <summary>Localized resource name, or a complete native reward sentence (already localized).</summary>
        public string Label;

        /// <summary>Pre-formatted value text (e.g. "+5", "-10"). Empty for name-only / native-sentence rows.</summary>
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
    ///
    /// Every field here traces to a line the game's OWN reward UI (<c>UIModuleSiteEncounters.ShowReward</c>)
    /// renders, in the current language:
    ///   • <see cref="Resources"/> — the native resource reward line (ShowReward line 417): localized
    ///     resource name + signed rounded value. Kept as a two-column name/value row so the tooltip can
    ///     apply the native reward colour to the value.
    ///   • <see cref="NativeLines"/> — fully-formatted, already-localized native reward SENTENCES (soldier/
    ///     aircraft damage, tiredness, faction skill points, site reveals, faction-party diplomacy, granted
    ///     items). Each is built by <see cref="EventOutcomeAdapter"/> from the live module's own
    ///     <c>LocalizedTextBind</c> keys / native concatenation with the magnitude already substituted, so it
    ///     reads identically to the native post-choice line. Rendered verbatim as a name-only row.
    ///
    /// Outcome kinds the native reward UI does NOT render, or that need apply-time data absent from a static
    /// preview (granted-research, mission variables, sub-faction mission-weight, zone damage, site-leader
    /// diplomacy, encounter-tag / haven-type site reveals), are intentionally NOT represented here: the
    /// adapter SKIPS them rather than inventing a non-native codename/format row.
    /// </summary>
    public sealed class EventOutcomeData
    {
        /// <summary>Resource deltas: (already-localized resource name, rounded signed int). Excludes zero/None.</summary>
        public List<ResourceEntry> Resources = new List<ResourceEntry>();

        /// <summary>
        /// Fully-formatted, already-localized native reward sentences. Each string is produced by
        /// <see cref="EventOutcomeAdapter"/> from the game's own reward-UI loc keys / native format with the
        /// magnitude already substituted, so it reads identically to the game's native post-choice reward
        /// line in any language. Rendered verbatim as a name-only row (no separate value column) by
        /// <see cref="EventOutcomePreview"/>.
        /// </summary>
        public List<string> NativeLines = new List<string>();

        public struct ResourceEntry
        {
            public string Name;
            public int Value;
            public ResourceEntry(string name, int value) { Name = name; Value = value; }
        }
    }
}
