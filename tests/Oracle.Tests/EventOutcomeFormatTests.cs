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
    }
}
