using System.Collections.Generic;
using PhoenixPoint.Modding;

namespace Morgott.Oracle
{
    /// <summary>
    /// Swap history that PerkOracle asks the GAME to persist, through the first-class modding hook
    /// (<c>ModGeoscape.RecordGeoscapeInstanceData</c> / <c>ProcessGeoscapeInstanceData</c>). The document
    /// lands in <c>GeoLevelInstanceData.ModData["Oracle"]</c>, i.e. inside the player's savegame, so the
    /// history travels with the campaign it belongs to and is rolled back by a reload exactly like every
    /// other piece of campaign state. Nothing of ours touches the save file itself.
    ///
    /// Flat string map, same shape it has always had (see <see cref="OriginalPerkStore"/>); the version
    /// stamp lets a future shape change ignore documents it cannot read instead of misreading them.
    /// </summary>
    public class OracleSwapHistory
    {
        public const int CurrentVersion = 1;

        public int Version;
        public Dictionary<string, string> Map;
    }

    /// <summary>
    /// The mod's geoscape half. MUST stay the only public direct <c>ModGeoscape</c> subclass in the
    /// assembly: the loader picks it with <c>ExportedTypes.FirstOrDefault(t =&gt; t.BaseType ==
    /// typeof(ModGeoscape))</c>, so a second one would make the choice arbitrary.
    ///
    /// Call order the engine guarantees (GeoLevelController / ModManager, verified in the decompile):
    ///   Level.State.Loading -> <see cref="Init"/>            (fresh instance per geoscape session)
    ///   loaded save only    -> <see cref="ProcessGeoscapeInstanceData"/>
    ///   on every save       -> <see cref="RecordGeoscapeInstanceData"/>
    /// </summary>
    public class OracleGeoscapeMod : ModGeoscape
    {
        /// <summary>
        /// Runs while the geoscape level is still LOADING, i.e. before the save hands its own history
        /// back. Dropping the map here is what stops a new campaign (or a second campaign in the same
        /// session) from inheriting the previous one's slots.
        /// </summary>
        public override void Init()
        {
            OriginalPerkStore.Clear();
        }

        /// <summary>
        /// Deliberately trivial: exceptions thrown in here are swallowed by the engine, so the only work
        /// is copying the map out.
        /// </summary>
        public override object RecordGeoscapeInstanceData()
        {
            OracleLog.Debug("[Oracle] RecordGeoscapeInstanceData");
            var data = new OracleSwapHistory
            {
                Version = OracleSwapHistory.CurrentVersion,
                Map = OriginalPerkStore.Snapshot(),
            };
            OracleLog.Debug("[Oracle] RecordGeoscapeInstanceData done, entries=" + data.Map.Count);
            return data;
        }

        /// <summary>
        /// Called ONLY when the loaded save actually carries our document — never on a new campaign.
        /// Trivial for the same swallowed-exception reason as <see cref="RecordGeoscapeInstanceData"/>.
        /// </summary>
        public override void ProcessGeoscapeInstanceData(object instanceData)
        {
            OracleLog.Debug("[Oracle] ProcessGeoscapeInstanceData");
            var data = instanceData as OracleSwapHistory;
            if (data == null || data.Version != OracleSwapHistory.CurrentVersion)
            {
                OracleLog.Debug("[Oracle] ProcessGeoscapeInstanceData ignored (unreadable version)");
                return;
            }
            OriginalPerkStore.LoadFrom(data.Map);
            OracleLog.Debug("[Oracle] ProcessGeoscapeInstanceData done, entries="
                + (data.Map != null ? data.Map.Count : 0));
        }
    }
}
