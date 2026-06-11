using System.Collections.Generic;
using System.Linq;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure outcome-preview formatter. Outcome data is fabricated as plain
    /// <see cref="EventOutcomeData"/> (no engine types), mirroring how PerkPoolResolver is tested.
    /// Verifies one-row-per-effect, signed formatting "+#;-#" for diplomacy/resources, "xN" for items,
    /// Min-Max ranges, % for zone damage, verbatim pass-through of pre-localized native reward sentences,
    /// and the empty-outcome -> no-rows case.
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
        public void Diplomacy_Positive_And_Negative_Are_Signed()
        {
            var data = new EventOutcomeData();
            data.Diplomacy.Add(new EventOutcomeData.DiplomacyEntry("New Jericho", 5));
            data.Diplomacy.Add(new EventOutcomeData.DiplomacyEntry("Synedrion", -10));

            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();

            Assert.Contains("New Jericho +5", rows);
            Assert.Contains("Synedrion -10", rows);
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
        public void NativeLines_Pass_Through_As_NameOnly_Rows()
        {
            // Soldier/aircraft/skill-point effects arrive from the adapter as fully-formatted, already-localized
            // native reward sentences; the pure formatter emits each verbatim as a label with no value column.
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
        public void Empty_NativeLines_Are_Skipped()
        {
            var data = new EventOutcomeData();
            data.NativeLines.Add(string.Empty);
            data.NativeLines.Add(null);
            Assert.Empty(EventOutcomePreview.Build(data));
        }

        [Fact]
        public void Items_Render_Name_And_Count()
        {
            var data = new EventOutcomeData();
            data.Items.Add(new EventOutcomeData.ItemEntry("Medkit", 2));
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.Contains("Medkit") && s.Contains("2"));
        }

        [Fact]
        public void Research_Renders_Name()
        {
            var data = new EventOutcomeData();
            data.Researches.Add("Mutoid Tech");
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.Contains("Mutoid Tech"));
        }

        [Fact]
        public void VariableChange_Renders_Min_Max_Range()
        {
            var data = new EventOutcomeData();
            data.VariableChanges.Add(new EventOutcomeData.RangeEntry("AncientThreat", 3, 7));
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.Contains("AncientThreat") && s.Contains("3-7"));
        }

        [Fact]
        public void MissionWeightChange_Renders_Min_Max_Range()
        {
            var data = new EventOutcomeData();
            data.MissionWeightChanges.Add(new EventOutcomeData.RangeEntry("Anu Raids", 1, 4));
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.Contains("Anu Raids") && s.Contains("1-4"));
        }

        [Fact]
        public void ZoneDamage_Renders_Percent()
        {
            var data = new EventOutcomeData();
            data.ZoneDamages.Add(new EventOutcomeData.ZoneDamageEntry("Living Quarters", 25));
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.Contains("Living Quarters") && s.Contains("25%"));
        }

        [Fact]
        public void RevealSites_Render_Name_And_Count()
        {
            var data = new EventOutcomeData();
            data.RevealSites.Add(new EventOutcomeData.ItemEntry("Alien Nest", 1));
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.Contains("Alien Nest"));
        }
    }
}
