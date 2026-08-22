using System.Collections.Generic;
using System.IO;
using System.Text;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure swap-history core: key building, first-write-wins on the original,
    /// clear-on-revert, the "current" staleness guard, and JSON/file tolerance. Def names are plain
    /// strings here exactly as they are on disk, so nothing Unity/TFTV is involved.
    /// </summary>
    public class OriginalPerkStoreTests
    {
        private const string Key = "42#3#Personal";

        private static Dictionary<string, string> NewMap() => new Dictionary<string, string>();

        [Fact]
        public void BuildKey_CombinesCharacterTrackAndSlot()
        {
            Assert.Equal("42#3#Personal", OriginalPerkStore.BuildKey(42, 3, "Personal"));
            Assert.NotEqual(OriginalPerkStore.BuildKey(4, 23, "Personal"),
                OriginalPerkStore.BuildKey(42, 3, "Personal"));
        }

        [Fact]
        public void BuildKey_SameLevelInDifferentTracks_DoesNotCollide()
        {
            Assert.NotEqual(OriginalPerkStore.BuildKey(42, 3, "Personal"),
                OriginalPerkStore.BuildKey(42, 3, "PrimaryClass"));

            // ...and the two entries stay independent in one map.
            var map = NewMap();
            OriginalPerkStore.Record(map, OriginalPerkStore.BuildKey(42, 3, "Personal"), "Alpha", "Beta");
            OriginalPerkStore.Record(map, OriginalPerkStore.BuildKey(42, 3, "PrimaryClass"), "Gamma", "Delta");
            Assert.Equal("Alpha",
                OriginalPerkStore.GetOriginal(map, OriginalPerkStore.BuildKey(42, 3, "Personal"), "Beta"));
            Assert.Equal("Gamma",
                OriginalPerkStore.GetOriginal(map, OriginalPerkStore.BuildKey(42, 3, "PrimaryClass"), "Delta"));
        }

        [Fact]
        public void BuildKey_UnresolvableTrack_StillProducesAKey()
        {
            Assert.Equal("42#3#", OriginalPerkStore.BuildKey(42, 3, null));
        }

        // ---- baseline-on-observe (a slot changed by TFTV's own UI must stay revertable) -------------

        [Fact]
        public void Observe_FirstSighting_RecordsTheBaseline_ButOffersNothingYet()
        {
            var map = NewMap();
            Assert.True(OriginalPerkStore.Observe(map, Key, "Alpha"));
            Assert.Null(OriginalPerkStore.GetOriginal(map, Key, "Alpha"));
        }

        [Fact]
        public void Observe_SlotChangedByAnotherMod_IsStillRevertable()
        {
            // Baseline seen by the wiki, then TFTV's DrillSwapUI writes a drill into the slot behind us.
            var map = NewMap();
            OriginalPerkStore.Observe(map, Key, "Alpha");
            Assert.True(OriginalPerkStore.Observe(map, Key, "TftvDrill"));
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(map, Key, "TftvDrill"));
        }

        [Fact]
        public void Observe_IsIdempotent_AndKeepsTheTrueBaselineAcrossChanges()
        {
            var map = NewMap();
            OriginalPerkStore.Observe(map, Key, "Alpha");
            OriginalPerkStore.Observe(map, Key, "Beta");
            Assert.False(OriginalPerkStore.Observe(map, Key, "Beta")); // nothing new to write
            OriginalPerkStore.Observe(map, Key, "Gamma");
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(map, Key, "Gamma"));
        }

        [Fact]
        public void Observe_BackAtTheBaseline_ForgetsTheSlot()
        {
            var map = NewMap();
            OriginalPerkStore.Observe(map, Key, "Alpha");
            OriginalPerkStore.Observe(map, Key, "Beta");
            Assert.True(OriginalPerkStore.Observe(map, Key, "Alpha"));
            Assert.Empty(map);
        }

        [Fact]
        public void Observe_DoesNotOverwriteASwapRecordedBaseline()
        {
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
            OriginalPerkStore.Observe(map, Key, "Beta");
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(map, Key, "Beta"));
        }

        [Fact]
        public void Observe_NullArguments_AreNoOps()
        {
            Assert.False(OriginalPerkStore.Observe(null, Key, "Alpha"));
            Assert.False(OriginalPerkStore.Observe(NewMap(), null, "Alpha"));
            Assert.False(OriginalPerkStore.Observe(NewMap(), Key, null));
        }

        [Fact]
        public void FirstSwap_RecordsOriginal_AndIsOffered()
        {
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(map, Key, "Beta"));
        }

        [Fact]
        public void SecondSwap_KeepsTrueOriginal_NotTheIntermediate()
        {
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
            OriginalPerkStore.Record(map, Key, "Beta", "Gamma");
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(map, Key, "Gamma"));
        }

        [Fact]
        public void SwapBackToOriginal_ClearsTheEntry()
        {
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
            OriginalPerkStore.Record(map, Key, "Beta", "Gamma");
            Assert.True(OriginalPerkStore.Record(map, Key, "Gamma", "Alpha"));
            Assert.Empty(map);
            Assert.Null(OriginalPerkStore.GetOriginal(map, Key, "Alpha"));
        }

        [Fact]
        public void StaleCurrent_OffersNothing()
        {
            // Another campaign's soldier reusing the same GeoCharacter.Id, or the slot rewritten by
            // someone else: the recorded "current" no longer matches, so no revert is offered.
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
            Assert.Null(OriginalPerkStore.GetOriginal(map, Key, "SomethingElse"));
        }

        [Fact]
        public void UnknownSlot_OffersNothing()
        {
            Assert.Null(OriginalPerkStore.GetOriginal(NewMap(), Key, "Beta"));
        }

        [Fact]
        public void NullArguments_AreNoOps()
        {
            Assert.False(OriginalPerkStore.Record(null, Key, "Alpha", "Beta"));
            Assert.False(OriginalPerkStore.Record(NewMap(), null, "Alpha", "Beta"));
            Assert.False(OriginalPerkStore.Record(NewMap(), Key, null, "Beta"));
            Assert.Null(OriginalPerkStore.GetOriginal(null, Key, "Beta"));
            Assert.Null(OriginalPerkStore.GetOriginal(NewMap(), Key, null));
        }

        [Fact]
        public void SerializeParse_RoundTrips_IncludingAwkwardNames()
        {
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "A\"quoted\\name", "Beta\nline");
            Dictionary<string, string> back = OriginalPerkStore.Parse(OriginalPerkStore.Serialize(map));
            Assert.Equal(map, back);
            Assert.Equal("A\"quoted\\name", OriginalPerkStore.GetOriginal(back, Key, "Beta\nline"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("{\"o:42#3\"")]                 // truncated mid-document
        [InlineData("{\"unrelated\":\"junk\"}")]    // valid JSON, none of our keys
        public void CorruptOrForeignContent_ParsesToEmpty(string text)
        {
            Assert.Empty(OriginalPerkStore.Parse(text));
        }

        [Fact]
        public void Load_MissingOrCorruptFile_StartsEmpty()
        {
            Assert.Empty(OriginalPerkStore.Load(null));
            Assert.Empty(OriginalPerkStore.Load(Path.Combine(Path.GetTempPath(), "oracle-no-such-file.json")));

            string path = Path.Combine(Path.GetTempPath(), "oracle-corrupt-" + Path.GetRandomFileName());
            try
            {
                File.WriteAllText(path, "\0\0 garbage {{{", Encoding.UTF8);
                Assert.Empty(OriginalPerkStore.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void SaveThenLoad_RoundTripsThroughDisk()
        {
            string path = Path.Combine(Path.GetTempPath(), "oracle-store-" + Path.GetRandomFileName());
            try
            {
                var map = NewMap();
                OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
                OriginalPerkStore.Save(path, map);
                Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(OriginalPerkStore.Load(path), Key, "Beta"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Save_WithNoPath_IsSilentNoOp()
        {
            OriginalPerkStore.Save(null, NewMap()); // must not throw
        }

        [Fact]
        public void Save_IsAtomic_LeavesNoTempFile_AndOverwritesCleanly()
        {
            string path = Path.Combine(Path.GetTempPath(), "oracle-atomic-" + Path.GetRandomFileName());
            try
            {
                var first = NewMap();
                OriginalPerkStore.Record(first, Key, "Alpha", "Beta");
                OriginalPerkStore.Save(path, first);
                Assert.False(File.Exists(path + ".tmp"));

                // A second, SHORTER document must fully replace the first (no leftover tail).
                var second = NewMap();
                OriginalPerkStore.Save(path, second);
                Assert.False(File.Exists(path + ".tmp"));
                Assert.Empty(OriginalPerkStore.Load(path));
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".tmp");
            }
        }

        [Fact]
        public void Save_SurvivesALeftoverTempFileFromAnInterruptedWrite()
        {
            string path = Path.Combine(Path.GetTempPath(), "oracle-atomic2-" + Path.GetRandomFileName());
            try
            {
                var map = NewMap();
                OriginalPerkStore.Record(map, Key, "Alpha", "Beta");
                OriginalPerkStore.Save(path, map);

                // Simulate a crash mid-write: the truncated content sits in the ".tmp", never in the
                // live file, so the previous good document is still readable...
                File.WriteAllText(path + ".tmp", "{\"o:42#3#Pers", Encoding.UTF8);
                Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(OriginalPerkStore.Load(path), Key, "Beta"));

                // ...and the next save overwrites that stale temp rather than failing.
                OriginalPerkStore.Record(map, Key, "Beta", "Gamma");
                OriginalPerkStore.Save(path, map);
                Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(OriginalPerkStore.Load(path), Key, "Gamma"));
                Assert.False(File.Exists(path + ".tmp"));
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".tmp");
            }
        }
    }
}
