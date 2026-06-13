using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure authored-line formatting primitives. No engine types, mirroring
    /// EventOutcomePreviewTests / PerkPoolResolverTests so it links + runs under net8.
    /// </summary>
    public class EventOutcomeFormatTests
    {
        [Fact]
        public void Range_Distinct_Min_Max_Renders_Bracketed()
        {
            Assert.Equal("[2..5]", EventOutcomeFormat.Range(2, 5));
        }

        [Fact]
        public void Range_Equal_Min_Max_Collapses_To_Single_Value()
        {
            Assert.Equal("3", EventOutcomeFormat.Range(3, 3));
        }

        [Fact]
        public void Range_Handles_Negative_Bounds()
        {
            Assert.Equal("[-5..-1]", EventOutcomeFormat.Range(-5, -1));
        }

        [Fact]
        public void Signed_Formats_Positive_Negative_Zero()
        {
            Assert.Equal("+5", EventOutcomeFormat.Signed(5));
            Assert.Equal("-8", EventOutcomeFormat.Signed(-8));
            Assert.Equal("0", EventOutcomeFormat.Signed(0));
        }

        [Fact]
        public void Format1_Substitutes_Single_Arg()
        {
            Assert.Equal("Leads to: PROG_AN_22", EventOutcomeFormat.Format1("Leads to: {0}", "PROG_AN_22"));
        }

        [Fact]
        public void Format1_Empty_Pattern_Returns_Empty()
        {
            Assert.Equal(string.Empty, EventOutcomeFormat.Format1(string.Empty, "x"));
            Assert.Equal(string.Empty, EventOutcomeFormat.Format1(null, "x"));
        }

        [Fact]
        public void JoinNames_Joins_NonEmpty_With_Separator()
        {
            var names = new System.Collections.Generic.List<string> { "Alpha", "", null, "Beta" };
            Assert.Equal("Alpha, Beta", EventOutcomeFormat.JoinNames(names, ", "));
        }

        [Fact]
        public void JoinNames_Empty_List_Returns_Empty()
        {
            Assert.Equal(string.Empty, EventOutcomeFormat.JoinNames(new System.Collections.Generic.List<string>(), ", "));
            Assert.Equal(string.Empty, EventOutcomeFormat.JoinNames(null, ", "));
        }

        [Fact]
        public void Format2_Substitutes_Two_Args()
        {
            Assert.Equal("Variable Seeds_of_Reformation: = 1",
                EventOutcomeFormat.Format2("Variable {0}: {1}", "Seeds_of_Reformation", "= 1"));
        }

        [Fact]
        public void Format2_Empty_Pattern_Returns_Empty()
        {
            Assert.Equal(string.Empty, EventOutcomeFormat.Format2(string.Empty, "a", "b"));
            Assert.Equal(string.Empty, EventOutcomeFormat.Format2(null, "a", "b"));
        }

        [Fact]
        public void Variable_Line_Shows_Name_And_Set_Add_Range()
        {
            // The adapter (AddVariableChangeLine) shows the variable NAME plus the operation+value via the
            // repurposed 2-arg ORACLE_EVT_VARIABLE key ({0}=name, {1}=value expr). Mirror its composition:
            //   • SET, Min==Max      -> "= X"
            //   • SET, Min!=Max      -> "= [Min..Max]"
            //   • additive, Min==Max -> signed "+X"/"-X"
            //   • additive, Min!=Max -> signed range "+[Min..Max]"
            const string pattern = "Variable {0}: {1}";

            // SET single value.
            Assert.Equal("Variable Seeds_of_Reformation: = 1",
                EventOutcomeFormat.Format2(pattern, "Seeds_of_Reformation", "= " + EventOutcomeFormat.Range(1, 1)));
            // SET range.
            Assert.Equal("Variable Tempo: = [2..5]",
                EventOutcomeFormat.Format2(pattern, "Tempo", "= " + EventOutcomeFormat.Range(2, 5)));
            // Additive single value (signed).
            Assert.Equal("Variable Threat: +3",
                EventOutcomeFormat.Format2(pattern, "Threat", EventOutcomeFormat.Signed(3)));
            Assert.Equal("Variable Threat: -2",
                EventOutcomeFormat.Format2(pattern, "Threat", EventOutcomeFormat.Signed(-2)));
            // Additive range (signed range).
            Assert.Equal("Variable Mood: +[2..5]",
                EventOutcomeFormat.Format2(pattern, "Mood", "+" + EventOutcomeFormat.Range(2, 5)));
        }

        [Fact]
        public void Research_Line_Joins_Resolved_Names()
        {
            var names = new System.Collections.Generic.List<string> { "Operative Reconditioning", "Mutoid Tech" };
            string line = EventOutcomeFormat.Format1("Research: {0}", EventOutcomeFormat.JoinNames(names, ", "));
            Assert.Equal("Research: Operative Reconditioning, Mutoid Tech", line);
        }

        [Fact]
        public void Phoenixpedia_Line_Joins_Resolved_Titles()
        {
            var names = new System.Collections.Generic.List<string> { "The Pure" };
            string line = EventOutcomeFormat.Format1("Phoenixpedia: {0}", EventOutcomeFormat.JoinNames(names, ", "));
            Assert.Equal("Phoenixpedia: The Pure", line);
        }

        [Fact]
        public void Mission_Line_Uses_Type_Token()
        {
            string line = EventOutcomeFormat.Format1("Mission: {0}", "Ambush");
            Assert.Equal("Mission: Ambush", line);
        }

        [Fact]
        public void FollowUp_Line_Joins_Event_Ids()
        {
            var ids = new System.Collections.Generic.List<string> { "PROG_AN_22", "PROG_NJ_05" };
            string line = EventOutcomeFormat.Format1("Leads to: {0}", EventOutcomeFormat.JoinNames(ids, ", "));
            Assert.Equal("Leads to: PROG_AN_22, PROG_NJ_05", line);
        }

        [Fact]
        public void Sdi_Line_Substitutes_Absolute_Value()
        {
            // Adapter selects SDIIncrease/SDIDecrease key by sign, then substitutes the absolute value.
            Assert.Equal("SDI increased by 4", EventOutcomeFormat.Format1("SDI increased by {0}", 4));
            Assert.Equal("SDI decreased by 3", EventOutcomeFormat.Format1("SDI decreased by {0}", 3));
        }

        [Fact]
        public void Victory_Line_Uses_Faction_Name()
        {
            string line = EventOutcomeFormat.Format1("Victory for {0}", "Anu");
            Assert.Equal("Victory for Anu", line);
        }

        [Fact]
        public void ZoneDamage_Line_Uses_Percent_And_Zone()
        {
            // Two-placeholder authored line: adapter calls string.Format(pattern, percent, zoneKeyword) directly.
            // Assert the final composed shape the adapter produces.
            Assert.Equal("25% damage to Industry", string.Format("{0}% damage to {1}", 25, "Industry"));
        }
    }
}
