using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure story-variable -> narrative-line resolver. No engine types, mirroring
    /// EventOutcomeFormatTests so it links + runs under net8.
    /// </summary>
    public class VariableNarrativesTests
    {
        [Fact]
        public void Flag_Set_To_One_Shows_Phrase_With_No_Number()
        {
            // A 0/1 milestone: the value is noise, so the suffix is empty and the up key is used.
            var line = VariableNarratives.Resolve("BehemothEggHatched", 1, 1, isSetOperation: true);
            Assert.Equal(VariableNarratives.Status.Show, line.Status);
            Assert.Equal("ORACLE_VAR_BEHEMOTH_EGG", line.LocKey);
            Assert.Equal(string.Empty, line.Suffix);
        }

        [Fact]
        public void Flag_Additive_Plus_One_Also_Reaches_Milestone_No_Number()
        {
            var line = VariableNarratives.Resolve("ThirdActStarted", 1, 1, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.Show, line.Status);
            Assert.Equal("ORACLE_VAR_THIRD_ACT", line.LocKey);
            Assert.Equal(string.Empty, line.Suffix);
        }

        [Fact]
        public void Flag_Reset_With_No_Down_Key_Is_Hidden_Unmapped()
        {
            // Setting a one-way flag to 0 has no authored phrase -> hidden and logged by the caller.
            var line = VariableNarratives.Resolve("ThirdActStarted", 0, 0, isSetOperation: true);
            Assert.Equal(VariableNarratives.Status.HiddenUnmapped, line.Status);
        }

        [Fact]
        public void Counter_Additive_Increase_Shows_Signed_Suffix()
        {
            var line = VariableNarratives.Resolve("BehemothRoamings", 1, 1, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.Show, line.Status);
            Assert.Equal("ORACLE_VAR_BEHEMOTH_ROAM", line.LocKey);
            Assert.Equal(" (+1)", line.Suffix);
        }

        [Fact]
        public void Counter_Additive_Range_Shows_Signed_Range_Suffix()
        {
            var line = VariableNarratives.Resolve("ProteanMutaneResearched", 2, 5, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.Show, line.Status);
            Assert.Equal(" (+[2..5])", line.Suffix);
        }

        [Fact]
        public void Counter_Set_With_No_Set_Key_Is_Hidden_Unmapped()
        {
            // The mapped counters are only ever incremented in event defs, so none authors a set phrase;
            // an outright set therefore has no line and is logged as a gap by the caller.
            var line = VariableNarratives.Resolve("BC_SDI", 10, 10, isSetOperation: true);
            Assert.Equal(VariableNarratives.Status.HiddenUnmapped, line.Status);
        }

        [Fact]
        public void Counter_Decrease_Uses_Down_Key_When_Present()
        {
            var line = VariableNarratives.Resolve("BC_SDI", -2, -2, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.Show, line.Status);
            Assert.Equal("ORACLE_VAR_BC_SDI_DOWN", line.LocKey);
            Assert.Equal(" (-2)", line.Suffix);
        }

        [Fact]
        public void Counter_Increase_Uses_Up_Key()
        {
            var line = VariableNarratives.Resolve("BC_SDI", 3, 3, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.Show, line.Status);
            Assert.Equal("ORACLE_VAR_BC_SDI_UP", line.LocKey);
            Assert.Equal(" (+3)", line.Suffix);
        }

        [Fact]
        public void Counter_Decrease_With_No_Down_Key_Is_Hidden_Unmapped()
        {
            // BehemothRoamings only ever climbs; a decrease has no phrase.
            var line = VariableNarratives.Resolve("BehemothRoamings", -1, -1, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.HiddenUnmapped, line.Status);
        }

        [Fact]
        public void Unknown_Variable_Is_Hidden_Unmapped()
        {
            var line = VariableNarratives.Resolve("VoidOmen_3", 1, 1, isSetOperation: true);
            Assert.Equal(VariableNarratives.Status.HiddenUnmapped, line.Status);
        }

        [Fact]
        public void Additive_NoOp_Is_Hidden_Silently()
        {
            // 0..0 additive is a no-op even for a mapped variable -> hidden, but NOT a coverage gap.
            var line = VariableNarratives.Resolve("BehemothRoamings", 0, 0, isSetOperation: false);
            Assert.Equal(VariableNarratives.Status.HiddenNoOp, line.Status);
        }

        [Fact]
        public void Null_Or_Empty_Name_Is_Hidden_Unmapped()
        {
            Assert.Equal(VariableNarratives.Status.HiddenUnmapped,
                VariableNarratives.Resolve(null, 1, 1, isSetOperation: true).Status);
            Assert.Equal(VariableNarratives.Status.HiddenUnmapped,
                VariableNarratives.Resolve(string.Empty, 1, 1, isSetOperation: true).Status);
        }

        [Fact]
        public void English_Fallback_Matches_Authored_Master()
        {
            var line = VariableNarratives.Resolve("CyclopsBuiltVariable", 1, 1, isSetOperation: true);
            Assert.Equal("The Cyclops walks.", line.English);
        }
    }
}
