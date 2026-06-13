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
        public void Variable_Line_Uses_Range_Token()
        {
            // The adapter builds the variable line as Format1(pattern, Range(min, max)); assert the composed shape.
            string line = EventOutcomeFormat.Format1("Variable change: {0}", EventOutcomeFormat.Range(2, 5));
            Assert.Equal("Variable change: [2..5]", line);
        }

        [Fact]
        public void MissionWeight_Line_Uses_Range_Token()
        {
            string line = EventOutcomeFormat.Format1("Mission weight: {0}", EventOutcomeFormat.Range(1, 1));
            Assert.Equal("Mission weight: 1", line); // collapses when Min == Max
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
    }
}
