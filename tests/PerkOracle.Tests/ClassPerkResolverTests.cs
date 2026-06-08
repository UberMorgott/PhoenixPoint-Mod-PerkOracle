using System;
using System.Collections.Generic;
using Morgott.PerkOracle;
using Xunit;

namespace Morgott.PerkOracle.Tests
{
    /// <summary>
    /// Unit tests for the pure class-perk core. Defs are faked with strings ("name" -> "DEF:name")
    /// so the ordering/dedup/skip logic is exercised without Unity/TFTV. Mirrors PerkPoolResolverTests.
    /// </summary>
    public class ClassPerkResolverTests
    {
        // name -> def fake: every name resolves to "DEF:" + name, except names containing "MISSING".
        private static readonly Func<string, string> Resolve =
            name => name.Contains("MISSING") ? null : "DEF:" + name;

        [Fact]
        public void OrderedNames_ResolveInOrder()
        {
            var raw = new List<string> { "PROFICIENCY", "PERK A", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PROFICIENCY", "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void DuplicateNames_AreDeduped()
        {
            var raw = new List<string> { "PERK A", "PERK A", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void ResolverMiss_NamesAreSkipped()
        {
            var raw = new List<string> { "PERK A", "MISSING ONE", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void NullOrEmptyNamesInList_AreSkipped()
        {
            var raw = new List<string> { "PERK A", null, "", "PERK B" };
            var result = ClassPerkResolver.Resolve(raw, Resolve);
            Assert.Equal(new[] { "DEF:PERK A", "DEF:PERK B" }, result);
        }

        [Fact]
        public void NullRawNames_ReturnsEmpty()
        {
            var result = ClassPerkResolver.Resolve(null, Resolve);
            Assert.Empty(result);
        }

        [Fact]
        public void NullResolver_ReturnsEmpty()
        {
            var raw = new List<string> { "PERK A" };
            var result = ClassPerkResolver.Resolve<string>(raw, null);
            Assert.Empty(result);
        }
    }
}
