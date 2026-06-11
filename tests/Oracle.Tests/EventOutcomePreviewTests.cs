using System.Collections.Generic;
using System.Linq;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure outcome-preview formatter. Outcome data is fabricated as plain
    /// <see cref="EventOutcomeData"/> (no engine types), mirroring how PerkPoolResolver is tested.
    /// Verifies one-row-per-effect, signed formatting "+#;-#", Min-Max ranges, % for zone damage,
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
        public void SoldierHpLoss_Renders_Negative()
        {
            var data = new EventOutcomeData { DamageCurrentSoldiers = 15, DamageAllSoldiers = 5 };
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            // Damage is rendered as a loss (negative).
            Assert.Contains(rows, s => s.EndsWith("-15"));
            Assert.Contains(rows, s => s.EndsWith("-5"));
        }

        [Fact]
        public void StaminaLoss_Renders_Negative()
        {
            var data = new EventOutcomeData { TireCurrentSoldiers = 7, TireAllSoldiers = 3 };
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.EndsWith("-7"));
            Assert.Contains(rows, s => s.EndsWith("-3"));
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
        public void Aircraft_SkillPoints_HavenPop_Sdi_Render_Signed()
        {
            var data = new EventOutcomeData
            {
                DamageCurrentAircraft = 12,
                FactionSkillPoints = 4,
                HavenPopulationChange = -3,
                SdiChange = 2,
            };
            var rows = EventOutcomePreview.Build(data).Select(Render).ToList();
            Assert.Contains(rows, s => s.EndsWith("-12")); // aircraft damage = loss
            Assert.Contains(rows, s => s.EndsWith("+4"));  // skill points
            Assert.Contains(rows, s => s.EndsWith("-3"));  // haven pop
            Assert.Contains(rows, s => s.EndsWith("+2"));  // SDI
        }

        [Fact]
        public void ZeroScalars_Produce_No_Rows()
        {
            var data = new EventOutcomeData
            {
                DamageCurrentSoldiers = 0,
                TireAllSoldiers = 0,
                FactionSkillPoints = 0,
                SdiChange = 0,
                HavenPopulationChange = 0,
                DamageCurrentAircraft = 0,
            };
            Assert.Empty(EventOutcomePreview.Build(data));
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
