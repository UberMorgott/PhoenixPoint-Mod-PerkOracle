using System;
using System.Collections.Generic;
using System.Linq;
using Morgott.PerkOracle;
using Xunit;

namespace Morgott.PerkOracle.Tests
{
    /// <summary>
    /// Unit tests for the pure pool core. Defs are faked with strings ("name" -> "DEF:name") so the
    /// logic is exercised without Unity/TFTV. Mirrors how PerkClassification is tested.
    /// </summary>
    public class PerkPoolResolverTests
    {
        // name -> def fake: every name resolves to "DEF:" + name, except names containing "MISSING".
        private static readonly Func<string, string> Resolve =
            name => name.Contains("MISSING") ? null : "DEF:" + name;

        // Exclude SHOTGUN/ASSAULT-RIFLE proficiencies for the "Assault" class (mirrors the real map).
        private static readonly Func<string, string, bool> IsExcluded =
            (name, className) =>
                (name == "ASSAULT RIFLE PROFICIENCY" || name == "SHOTGUN PROFICIENCY") && className == "Assault";

        [Fact]
        public void RandomSlot_FullPool_InOrder()
        {
            var raw = new List<string> { "SURVIVOR", "NURSE", "SCAV" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Sniper", IsExcluded, Resolve);
            Assert.Equal(new[] { "DEF:SURVIVOR", "DEF:NURSE", "DEF:SCAV" }, result);
        }

        [Fact]
        public void ClassBlacklistedName_IsRemoved()
        {
            var raw = new List<string> { "HANDGUN PROFICIENCY", "ASSAULT RIFLE PROFICIENCY", "SHOTGUN PROFICIENCY" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Assault", IsExcluded, Resolve);
            // The two Assault-excluded proficiencies are dropped; handgun stays.
            Assert.Equal(new[] { "DEF:HANDGUN PROFICIENCY" }, result);
        }

        [Fact]
        public void Blacklist_NotApplied_ForOtherClass()
        {
            var raw = new List<string> { "ASSAULT RIFLE PROFICIENCY", "SHOTGUN PROFICIENCY" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Heavy", IsExcluded, Resolve);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void NullClassName_DisablesExclusion()
        {
            var raw = new List<string> { "ASSAULT RIFLE PROFICIENCY" };
            var result = PerkPoolResolver.OrderAndResolve(raw, null, IsExcluded, Resolve);
            Assert.Single(result); // no className -> no filtering
        }

        [Fact]
        public void ResolverMiss_NamesAreSkipped()
        {
            var raw = new List<string> { "SURVIVOR", "MISSING ONE", "SCAV" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Sniper", IsExcluded, Resolve);
            Assert.Equal(new[] { "DEF:SURVIVOR", "DEF:SCAV" }, result);
        }

        [Fact]
        public void DuplicateNames_AreDeduped()
        {
            var raw = new List<string> { "SURVIVOR", "SURVIVOR", "NURSE" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Sniper", IsExcluded, Resolve);
            Assert.Equal(new[] { "DEF:SURVIVOR", "DEF:NURSE" }, result);
        }

        [Fact]
        public void NullOrEmptyNamesInList_AreSkipped()
        {
            var raw = new List<string> { "SURVIVOR", null, "", "NURSE" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Sniper", IsExcluded, Resolve);
            Assert.Equal(new[] { "DEF:SURVIVOR", "DEF:NURSE" }, result);
        }

        [Fact]
        public void NullRawNames_ReturnsEmpty()
        {
            var result = PerkPoolResolver.OrderAndResolve(null, "Sniper", IsExcluded, Resolve);
            Assert.Empty(result);
        }

        [Fact]
        public void NullResolver_ReturnsEmpty()
        {
            var raw = new List<string> { "SURVIVOR" };
            var result = PerkPoolResolver.OrderAndResolve<string>(raw, "Sniper", IsExcluded, null);
            Assert.Empty(result);
        }

        [Fact]
        public void NullExclusionPredicate_IsIgnored()
        {
            var raw = new List<string> { "SURVIVOR", "NURSE" };
            var result = PerkPoolResolver.OrderAndResolve(raw, "Assault", null, Resolve);
            Assert.Equal(2, result.Count);
        }
    }
}
