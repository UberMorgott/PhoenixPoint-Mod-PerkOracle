using System.Collections.Generic;
using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure swap-history core: key building, first-write-wins on the original,
    /// clear-on-revert, the "current" staleness guard, and the snapshot/restore pair the savegame
    /// round-trips through. Def names are plain strings, so nothing Unity/TFTV is involved.
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
        public void Observe_RepeatedOnAnUnchangedSlot_KeepsTheBaseline()
        {
            // Looking at the same untouched slot twice must not be read as "restored to default":
            // dropping the entry here would let a LATER change (e.g. TFTV) become the new "original".
            var map = NewMap();
            OriginalPerkStore.Observe(map, Key, "Alpha");
            Assert.False(OriginalPerkStore.Observe(map, Key, "Alpha"));
            Assert.True(OriginalPerkStore.HasBaseline(map, Key));

            OriginalPerkStore.Observe(map, Key, "TftvDrill");
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(map, Key, "TftvDrill"));
        }

        [Fact]
        public void Observe_ClearsOnlyAfterARealTransition()
        {
            var map = NewMap();
            OriginalPerkStore.Record(map, Key, "Alpha", "Beta");   // a swap actually happened
            Assert.True(OriginalPerkStore.Observe(map, Key, "Alpha")); // ...and was undone
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

        // ---- savegame round trip (what OracleGeoscapeMod hands to the game and gets back) -----------

        [Fact]
        public void SnapshotThenLoadFrom_RoundTripsTheLiveHistory()
        {
            OriginalPerkStore.Clear();
            OriginalPerkStore.RecordSwap(42, 3, "Personal", "Alpha", "Beta");
            Dictionary<string, string> saved = OriginalPerkStore.Snapshot();

            OriginalPerkStore.Clear(); // geoscape start: a new campaign inherits nothing
            Assert.False(OriginalPerkStore.HasBaseline(42, 3, "Personal"));

            OriginalPerkStore.LoadFrom(saved);
            Assert.True(OriginalPerkStore.HasBaseline(42, 3, "Personal"));
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginalDefName(42, 3, "Personal", "Beta"));
        }

        [Fact]
        public void Snapshot_IsACopy_SoLaterSwapsDoNotMutateIt()
        {
            OriginalPerkStore.Clear();
            OriginalPerkStore.RecordSwap(42, 3, "Personal", "Alpha", "Beta");
            Dictionary<string, string> saved = OriginalPerkStore.Snapshot();

            OriginalPerkStore.RecordSwap(42, 3, "Personal", "Beta", "Alpha"); // reverted -> entry dropped
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginal(saved, Key, "Beta"));
        }

        [Fact]
        public void LoadFrom_TakesOnlyOurOwnEntries_AndToleratesNull()
        {
            OriginalPerkStore.Clear();
            OriginalPerkStore.LoadFrom(null); // must not throw
            Assert.False(OriginalPerkStore.HasBaseline(42, 3, "Personal"));

            OriginalPerkStore.LoadFrom(new Dictionary<string, string>
            {
                { "o:" + Key, "Alpha" },
                { "c:" + Key, "Beta" },
                { "unrelated", "junk" },
                { "o:9#9#Personal", "" },
            });
            Assert.Equal("Alpha", OriginalPerkStore.GetOriginalDefName(42, 3, "Personal", "Beta"));
            Assert.False(OriginalPerkStore.HasBaseline(9, 9, "Personal"));
            Assert.Equal(2, OriginalPerkStore.Snapshot().Count);
        }
    }
}
