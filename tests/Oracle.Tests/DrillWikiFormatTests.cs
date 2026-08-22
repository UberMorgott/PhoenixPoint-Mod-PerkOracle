using System.Collections.Generic;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure drills-wiki core: browse ordering, the class+level gate wording
    /// (including the "any class" case) and the proficiency OR-group wording. Mirrors
    /// ClassPerkResolverTests — no Unity/TFTV types are involved.
    /// </summary>
    public class DrillWikiFormatTests
    {
        private static KeyValuePair<string, int> Gate(string cls, int level)
            => new KeyValuePair<string, int>(cls, level);

        [Fact]
        public void SortByName_IsAlphabeticalAndCaseInsensitive()
        {
            var items = new[] { "Shock Drop", "adaptive Aim", "Bombardier" };
            var result = DrillWikiFormat.SortByName(items, s => s);
            Assert.Equal(new[] { "adaptive Aim", "Bombardier", "Shock Drop" }, result);
        }

        [Fact]
        public void SortByName_DropsEntriesWithoutAName()
        {
            var items = new[] { "Shock Drop", null, "" };
            var result = DrillWikiFormat.SortByName(items, s => s);
            Assert.Equal(new[] { "Shock Drop" }, result);
        }

        [Fact]
        public void SortByName_NullInputs_ReturnEmpty()
        {
            Assert.Empty(DrillWikiFormat.SortByName<string>(null, s => s));
            Assert.Empty(DrillWikiFormat.SortByName(new[] { "a" }, null));
        }

        [Fact]
        public void ClassLevelLine_SingleGate_UsesTftvWording()
        {
            var gates = new List<KeyValuePair<string, int>> { Gate("assault", 3) };
            Assert.Equal("Level: 3 assault", DrillWikiFormat.ClassLevelLine(gates, "Any class"));
        }

        [Fact]
        public void ClassLevelLine_NullClassTag_RendersAnyClassLabel()
        {
            var gates = new List<KeyValuePair<string, int>> { Gate(null, 5), Gate("", 2) };
            Assert.Equal("Level: 5 Any class, Level: 2 Any class",
                DrillWikiFormat.ClassLevelLine(gates, "Any class"));
        }

        [Fact]
        public void ClassLevelLine_MultipleGates_AreAllRequired()
        {
            var gates = new List<KeyValuePair<string, int>> { Gate("assault", 3), Gate("heavy", 5) };
            Assert.Equal("Level: 3 assault, Level: 5 heavy",
                DrillWikiFormat.ClassLevelLine(gates, "Any class"));
        }

        [Fact]
        public void ClassLevelLine_NoGates_ReturnsNull()
        {
            Assert.Null(DrillWikiFormat.ClassLevelLine(null, "Any class"));
            Assert.Null(DrillWikiFormat.ClassLevelLine(new List<KeyValuePair<string, int>>(), "Any class"));
        }

        [Fact]
        public void ProficiencyLine_GroupIsOr_GroupsAreAnd()
        {
            var groups = new List<IReadOnlyList<string>>
            {
                new List<string> { "Assault Rifles", "Sniper Rifles" },
                new List<string> { "Heavy Weapons" },
            };
            Assert.Equal("Assault Rifles or Sniper Rifles, Heavy Weapons",
                DrillWikiFormat.ProficiencyLine(groups));
        }

        [Fact]
        public void ProficiencyLine_SkipsEmptyGroupsNamesAndDuplicates()
        {
            var groups = new List<IReadOnlyList<string>>
            {
                new List<string> { "Pistols", "Pistols", null, "" },
                new List<string>(),
                null,
            };
            Assert.Equal("Pistols", DrillWikiFormat.ProficiencyLine(groups));
        }

        [Fact]
        public void ProficiencyLine_NothingToShow_ReturnsNull()
        {
            Assert.Null(DrillWikiFormat.ProficiencyLine(null));
            Assert.Null(DrillWikiFormat.ProficiencyLine(new List<IReadOnlyList<string>>()));
        }
    }
}
