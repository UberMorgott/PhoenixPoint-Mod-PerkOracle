using System.Linq;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure outcome-preview formatter. Outcome data is fabricated as plain
    /// <see cref="EventOutcomeData"/> (no engine types), mirroring how PerkPoolResolver is tested.
    ///
    /// The pure layer now has exactly two channels — both already native-sourced by
    /// <see cref="EventOutcomeAdapter"/>:
    ///   • <see cref="EventOutcomeData.Resources"/>: localized name + signed "+#"/"-#" value.
    ///   • <see cref="EventOutcomeData.NativeLines"/>: complete native reward sentences (site reveals,
    ///     faction-party diplomacy, granted items, soldier/aircraft damage, tiredness, skill points),
    ///     emitted verbatim as name-only rows.
    /// All other outcome kinds are skipped by the adapter (no native reward line), so the pure layer never
    /// sees them and there is nothing to format for them here.
    /// </summary>
    public class EventOutcomePreviewTests
    {
        private static string Render(EventOutcomeRow r) => (r.Label + " " + r.Value).Trim();

        [Fact]
        public void Empty_Outcome_Yields_No_Rows()
        {
            var rows = EventOutcomePreview.Build(new EventOutcomeData());
            Assert.Empty(rows);
        }

        [Fact]
        public void NullData_Yields_No_Rows()
        {
            var rows = EventOutcomePreview.Build(null);
            Assert.Empty(rows);
        }

        [Fact]
        public void Resources_Positive_And_Negative_Are_Signed()
        {
            var data = new EventOutcomeData();
            data.Resources.Add(new EventOutcomeData.ResourceEntry("Materials", 20));
            data.Resources.Add(new EventOutcomeData.ResourceEntry("Tech", -8));

            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();

            Assert.Contains("Materials +20", rows);
            Assert.Contains("Tech -8", rows);
        }

        [Fact]
        public void Resources_Zero_Are_Skipped()
        {
            var data = new EventOutcomeData();
            data.Resources.Add(new EventOutcomeData.ResourceEntry("Materials", 0));
            Assert.Empty(EventOutcomePreview.Build(data));
        }

        [Fact]
        public void NativeLines_Pass_Through_As_NameOnly_Rows()
        {
            // Soldier/aircraft/skill-point/reveal/diplomacy/item effects arrive from the adapter as
            // fully-formatted, already-localized native reward sentences; the pure formatter emits each
            // verbatim as a label with no value column.
            var data = new EventOutcomeData();
            data.NativeLines.Add("Your soldiers lost stamina: 7");
            data.NativeLines.Add("Aircraft took 12 damage");

            var rows = EventOutcomePreview.Build(data);

            Assert.Equal(2, rows.Count);
            Assert.Equal("Your soldiers lost stamina: 7", rows[0].Label);
            Assert.Equal(string.Empty, rows[0].Value);
            Assert.Equal("Aircraft took 12 damage", rows[1].Label);
            Assert.Equal(string.Empty, rows[1].Value);
        }

        [Fact]
        public void RevealSite_NativeLine_Passes_Through_Verbatim()
        {
            // A type-based site reveal is pre-formatted by the adapter into the game's own native sentence
            // (MultipleSiteRevealedTextKey, e.g. "Points added to geoscape: 3 Scavenging") and reaches the
            // pure layer as a NativeLine — rendered verbatim, never as a codename + "xN".
            var data = new EventOutcomeData();
            data.NativeLines.Add("Points added to geoscape: 3 Scavenging");

            var rows = EventOutcomePreview.Build(data);

            Assert.Single(rows);
            Assert.Equal("Points added to geoscape: 3 Scavenging", rows[0].Label);
            Assert.Equal(string.Empty, rows[0].Value);
        }

        [Fact]
        public void Empty_NativeLines_Are_Skipped()
        {
            var data = new EventOutcomeData();
            data.NativeLines.Add(string.Empty);
            data.NativeLines.Add(null);
            Assert.Empty(EventOutcomePreview.Build(data));
        }

        [Fact]
        public void Resources_Then_NativeLines_Order_Is_Preserved()
        {
            var data = new EventOutcomeData();
            data.Resources.Add(new EventOutcomeData.ResourceEntry("Materials", 20));
            data.NativeLines.Add("Aircraft took 12 damage");

            var rows = EventOutcomePreview.Build(data);

            Assert.Equal(2, rows.Count);
            Assert.Equal("Materials", rows[0].Label);
            Assert.Equal("+20", rows[0].Value);
            Assert.Equal("Aircraft took 12 damage", rows[1].Label);
            Assert.Equal(string.Empty, rows[1].Value);
        }
    }
}
