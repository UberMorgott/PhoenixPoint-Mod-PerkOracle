using System.Collections.Generic;
using PhoenixPoint.Modding;

namespace Morgott.Oracle
{
    /// <summary>
    /// Tint presets for the rolled-perk highlight. Names are shown raw in the in-game arrow picker
    /// (the settings UI does not localize enum values), so keep them short and clear. The actual
    /// <c>UnityEngine.Color</c> for each is resolved in <see cref="CellBackground"/>.
    /// </summary>
    public enum HighlightColor
    {
        Blue,
        Green,
        Gold,
        Red,
        Purple,
        White
    }

    /// <summary>
    /// In-game mod settings for Oracle. The game auto-discovers every public instance
    /// field (see <see cref="ModConfig.GetConfigFields"/>), surfaces it in the mod-options UI and
    /// serializes it to ModConfig.json. Read at runtime via <c>OracleMain.Instance.Config</c>.
    /// Field declaration order = the order shown in the options menu; keep related toggles together.
    /// </summary>
    public class OracleConfig : ModConfig
    {
        // --- Rolled-perk highlight ---------------------------------------------------------------

        /// <summary>
        /// When true (default), personal perks that were rolled at random are tinted in the character
        /// progression screen. Read live by <see cref="CellBackground"/>, so toggling applies on the next
        /// cell repaint (reopen the progression screen).
        /// </summary>
        public bool EnableRolledPerkHighlight = true;

        /// <summary>
        /// Which tint preset the rolled-perk highlight uses. Resolved to a concrete color in
        /// <see cref="CellBackground"/> on every apply, so a change takes effect on the next repaint.
        /// </summary>
        public HighlightColor RolledPerkHighlightColor = HighlightColor.Blue;

        // --- Perk wiki ---------------------------------------------------------------------------

        /// <summary>
        /// When true (default), the "possible skills" wiki panel can be opened (from the progression
        /// screen and the dual-class picker). Read live by <see cref="PerkWikiPanel.Open"/>.
        /// </summary>
        public bool EnablePerkWiki = true;

        // --- Subclass confirm preview ------------------------------------------------------------

        /// <summary>
        /// When true (default), picking a subclass first asks for confirmation with that subclass's perks
        /// shown in the dialog, and the picker also lists the subclasses you were not offered (view-only).
        /// Read live by the SelectSpecialization patches and <see cref="SubclassConfirmPopupDecorator"/>.
        /// </summary>
        public bool EnableSubclassConfirmDecoration = true;

        // --- Event outcome preview ---------------------------------------------------------------

        /// <summary>
        /// When true (default), hovering an answer-choice button in a geoscape site-encounter event
        /// shows a framed tooltip previewing that choice's outcome (reputation, resources, soldier
        /// HP/stamina loss, items, research, etc.). Read straight from the choice's outcome def WITHOUT
        /// applying it. Turn off to hide the preview.
        /// </summary>
        public bool ShowEventOutcomePreview = true;

        // --- Item dismantle ----------------------------------------------------------------------

        /// <summary>
        /// When true (default), an item's hover tooltip shows a "Dismantle" row listing the resources
        /// recovered by scrapping it (<see cref="ItemScrapTooltipPatch"/>). Read live.
        /// </summary>
        public bool ShowDismantleCompensation = true;

        // --- Perk swap ---------------------------------------------------------------------------

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
        /// When true (default), a perk swap costs the soldier <see cref="PerkSwapSkillPointCost"/> skill
        /// points: the swap is blocked when the soldier can't afford it, otherwise the points are spent.
        /// Turn OFF to make swaps free. Wired in <see cref="PerkSwapper"/>; the cost is shown natively on
        /// the perk's hover tooltip (the game's SP-cost row).
        /// </summary>
        public bool PerkSwapCostsResources = true;

        /// <summary>
        /// Skill-point cost of a single perk swap when <see cref="PerkSwapCostsResources"/> is on. Default 50.
        /// Values &lt;= 0 are treated as free. Read live, so a change applies on the next swap.
        /// </summary>
        public int PerkSwapSkillPointCost = 50;

        // --- Perk swap: TFTV drills (no effect without TFTV) --------------------------------------

        /// <summary>
        /// When true, a TFTV drill can be swapped in even though its unlock requirements (class level,
        /// research, weapon proficiency, functioning Training Facility) are not met. Default OFF: TFTV's
        /// own <c>IsDrillUnlocked</c> verdict is honoured. Dead without TFTV — the drill gate is skipped
        /// entirely when TFTV is absent (see <see cref="PerkSwapDecision"/> step 4).
        /// </summary>
        public bool IgnoreDrillRequirements = false;

        /// <summary>
        /// When true, a drill the soldier has already acquired may be swapped back to a normal perk or to
        /// another drill — something TFTV itself does not allow. Default OFF. Dead without TFTV.
        /// Never lifts the acquired-drill proficiency guard (that stays a hard deny).
        /// </summary>
        public bool AllowDrillReSwap = false;

        // --- Diagnostics -------------------------------------------------------------------------

        /// <summary>
        /// When true, the mod writes its <c>[Oracle] ...</c> diagnostic lines to the player log
        /// (routed through <see cref="OracleLog"/>). Default OFF: a shipped mod is silent. Turn this
        /// on only to diagnose an issue, then send the log.
        /// </summary>
        public bool EnableDebugLogging = false;

        /// <summary>
        /// Localize the in-game options UI. The base implementation builds one <see cref="ModConfigField"/>
        /// per public field; we keep those (so value get/set still work) and only override the
        /// <c>GetText</c>/<c>GetDescription</c> delegates to read the current-language strings via
        /// <see cref="Loc"/>. English literals are the fallback. Mirrors TFTV's GetConfigFields pattern.
        /// </summary>
        public override List<ModConfigField> GetConfigFields()
        {
            List<ModConfigField> fields = base.GetConfigFields();
            foreach (ModConfigField field in fields)
            {
                if (field.ID == nameof(EnableRolledPerkHighlight))
                {
                    field.GetText = () => Loc.Get("ORACLE_EnableRolledPerkHighlight", "Highlight Rolled Perks");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_EnableRolledPerkHighlight_DESCRIPTION",
                        "When on, randomly rolled personal perks are tinted in the progression screen so you can spot them at a glance.");
                }
                else if (field.ID == nameof(RolledPerkHighlightColor))
                {
                    field.GetText = () => Loc.Get("ORACLE_RolledPerkHighlightColor", "Highlight Color");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_RolledPerkHighlightColor_DESCRIPTION",
                        "The tint color used to highlight rolled perks. Choose Blue, Green, Gold, Red, Purple or White.");
                }
                else if (field.ID == nameof(EnablePerkWiki))
                {
                    field.GetText = () => Loc.Get("ORACLE_EnablePerkWiki", "Perk Wiki");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_EnablePerkWiki_DESCRIPTION",
                        "When on, you can open the possible-skills wiki panel to browse every perk a soldier or subclass could gain.");
                }
                else if (field.ID == nameof(EnableSubclassConfirmDecoration))
                {
                    field.GetText = () => Loc.Get("ORACLE_EnableSubclassConfirmDecoration", "Subclass Confirm Preview");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_EnableSubclassConfirmDecoration_DESCRIPTION",
                        "When on, picking a subclass asks you to confirm and shows that subclass's perks in the dialog, plus lets you preview the subclasses you weren't offered.");
                }
                else if (field.ID == nameof(ShowEventOutcomePreview))
                {
                    field.GetText = () => Loc.Get("ORACLE_ShowEventOutcomePreview", "Event Outcome Preview");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_ShowEventOutcomePreview_DESCRIPTION",
                        "When on, hovering an event answer shows a tooltip previewing its outcome (reputation, resources, soldier damage, items, research, etc.).");
                }
                else if (field.ID == nameof(ShowDismantleCompensation))
                {
                    field.GetText = () => Loc.Get("ORACLE_ShowDismantleCompensation", "Dismantle Yield");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_ShowDismantleCompensation_DESCRIPTION",
                        "When on, an item's hover tooltip shows the resources you recover by scrapping it.");
                }
                else if (field.ID == nameof(AllowPerkSwap))
                {
                    field.GetText = () => Loc.Get("ORACLE_AllowPerkSwap", "Perk Swap");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_AllowPerkSwap_DESCRIPTION",
                        "Left-click a perk in the wiki to swap the soldier's learned perk for it, for free.");
                }
                else if (field.ID == nameof(RequirePerkSwapResearch))
                {
                    field.GetText = () => Loc.Get("ORACLE_RequirePerkSwapResearch", "Require Research");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_RequirePerkSwapResearch_DESCRIPTION",
                        "When on, perk swapping requires the \"Operative Reconditioning\" research to be completed first. Turn off for free play.");
                }
                else if (field.ID == nameof(PerkSwapCostsResources))
                {
                    field.GetText = () => Loc.Get("ORACLE_PerkSwapCostsResources", "Swap Costs Skill Points");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_PerkSwapCostsResources_DESCRIPTION",
                        "When on, a perk swap spends skill points. The swap is blocked if the soldier can't afford it. Turn off to make swaps free.");
                }
                else if (field.ID == nameof(PerkSwapSkillPointCost))
                {
                    field.GetText = () => Loc.Get("ORACLE_PerkSwapSkillPointCost", "Swap Skill-Point Cost");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_PerkSwapSkillPointCost_DESCRIPTION",
                        "How many skill points a perk swap costs when \"Swap Costs Skill Points\" is on. Set to 0 for free swaps.");
                }
                else if (field.ID == nameof(IgnoreDrillRequirements))
                {
                    field.GetText = () => Loc.Get("ORACLE_IgnoreDrillRequirements", "Ignore Drill Requirements (TFTV)");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_IgnoreDrillRequirements_DESCRIPTION",
                        "TFTV only. When on, a drill can be taken even without the required level, research, weapon proficiency or working Training Facility. No effect without TFTV.");
                }
                else if (field.ID == nameof(AllowDrillReSwap))
                {
                    field.GetText = () => Loc.Get("ORACLE_AllowDrillReSwap", "Allow Re-Swapping Drills (TFTV)");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_AllowDrillReSwap_DESCRIPTION",
                        "TFTV only. When on, a drill the soldier already has can be swapped back to a normal perk or to another drill. No effect without TFTV.");
                }
                else if (field.ID == nameof(EnableDebugLogging))
                {
                    field.GetText = () => Loc.Get("ORACLE_EnableDebugLogging", "Enable Oracle debug logging");
                    field.GetDescription = () => Loc.Get(
                        "ORACLE_EnableDebugLogging_DESCRIPTION",
                        "When on, Oracle writes diagnostic messages to the player log. Leave off for normal play; turn on only to help diagnose an issue.");
                }
            }
            return fields;
        }
    }
}
