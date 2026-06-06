using System.Collections.Generic;
using PhoenixPoint.Modding;

namespace Morgott.PerkOracle
{
    /// <summary>
    /// In-game mod settings for PerkOracle. The game auto-discovers every public instance
    /// field (see <see cref="ModConfig.GetConfigFields"/>), surfaces it in the mod-options UI and
    /// serializes it to ModConfig.json. Read at runtime via <c>PerkOracleMain.Instance.Config</c>.
    /// </summary>
    public class PerkOracleConfig : ModConfig
    {
        /// <summary>
        /// When true, left-clicking a candidate perk in the wiki replaces the soldier's learned perk
        /// in that slot with the clicked one (free, instant). Default OFF: the wiki is view-only.
        /// </summary>
        public bool AllowPerkSwap = false;

        /// <summary>
        /// When true (default), the perk swap is additionally gated behind the in-game
        /// "Operative Reconditioning" research project (see <see cref="PerkSwapResearch"/>): a swap is
        /// only applied once that research is completed for the soldier's faction. Turn OFF for "free play"
        /// (swap allowed as soon as <see cref="AllowPerkSwap"/> is on). Everything still sits under the
        /// <see cref="AllowPerkSwap"/> master toggle.
        /// </summary>
        public bool RequirePerkSwapResearch = true;

        /// <summary>
        /// STUB / placeholder. Reserved for a future iteration that makes the swap cost in-game resources.
        /// Currently has NO behavior: the swap is always free regardless of this value. Surfaced in the
        /// options UI so the intent is visible; the wiring point lives in <see cref="PerkSwapper"/>.
        /// </summary>
        public bool PerkSwapCostsResources = false;

        /// <summary>
        /// Localize the in-game options UI for <see cref="AllowPerkSwap"/>. The base implementation builds
        /// one <see cref="ModConfigField"/> per public field; we keep those (so value get/set still work)
        /// and only override the <c>GetText</c>/<c>GetDescription</c> delegates of the AllowPerkSwap field
        /// to read the current-language strings via <see cref="Loc"/>. English literals are the fallback.
        /// Mirrors TFTV's GetConfigFields override pattern.
        /// </summary>
        public override List<ModConfigField> GetConfigFields()
        {
            List<ModConfigField> fields = base.GetConfigFields();
            foreach (ModConfigField field in fields)
            {
                if (field.ID == nameof(AllowPerkSwap))
                {
                    field.GetText = () => Loc.Get("PERKORACLE_AllowPerkSwap", "Perk Swap");
                    field.GetDescription = () => Loc.Get(
                        "PERKORACLE_AllowPerkSwap_DESCRIPTION",
                        "Left-click a perk in the wiki to swap the soldier's learned perk for it, for free.");
                }
                else if (field.ID == nameof(RequirePerkSwapResearch))
                {
                    field.GetText = () => Loc.Get("PERKORACLE_RequirePerkSwapResearch", "Require Research");
                    field.GetDescription = () => Loc.Get(
                        "PERKORACLE_RequirePerkSwapResearch_DESCRIPTION",
                        "When on, perk swapping requires the \"Operative Reconditioning\" research to be completed first. Turn off for free play.");
                }
                else if (field.ID == nameof(PerkSwapCostsResources))
                {
                    field.GetText = () => Loc.Get("PERKORACLE_PerkSwapCostsResources", "Swap Costs Resources");
                    field.GetDescription = () => Loc.Get(
                        "PERKORACLE_PerkSwapCostsResources_DESCRIPTION",
                        "Placeholder for a future update: make perk swapping cost resources. Currently has no effect.");
                }
            }
            return fields;
        }
    }
}
