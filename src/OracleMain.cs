using System;
using System.IO;
using System.Reflection;
using System.Text;
using Base.Core;
using Base.Defs;
using HarmonyLib;
using I2.Loc;
using PhoenixPoint.Modding;

namespace Morgott.Oracle
{
    public class OracleMain : ModMain
    {
        public static new OracleMain Instance { get; private set; }

        /// <summary>Strongly-typed view of the mod's config (declared public fields auto-exposed in-game).</summary>
        public new OracleConfig Config => (OracleConfig)base.Config;

        /// <summary>
        /// Runtime read of the perk-swap toggle. Defaults to false if the config is somehow unavailable,
        /// so the feature stays opt-in and a missing config can never accidentally enable swaps.
        /// </summary>
        public static bool AllowPerkSwap
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.AllowPerkSwap;
            }
        }

        /// <summary>
        /// Runtime read of the "require research" sub-toggle. Defaults to TRUE if the config is unavailable
        /// so a missing config keeps the swap gated (the safe, opt-in default) rather than free.
        /// </summary>
        public static bool RequirePerkSwapResearch
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg == null || cfg.RequirePerkSwapResearch;
            }
        }

        /// <summary>
        /// Runtime read of the "swap costs skill points" toggle. When on, a swap spends
        /// <see cref="PerkSwapSkillPointCost"/> SP (see <c>PerkSwapper</c>). Defaults to false when the config
        /// is unavailable so a missing config keeps swaps free rather than blocking them.
        /// </summary>
        public static bool PerkSwapCostsResources
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.PerkSwapCostsResources;
            }
        }

        /// <summary>
        /// Runtime read of the per-swap skill-point cost, clamped to &gt;= 0 (negative config = free).
        /// Defaults to 0 when the config is unavailable. Read live by <see cref="PerkSwapper"/> and the wiki.
        /// </summary>
        public static int PerkSwapSkillPointCost
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                int v = cfg != null ? cfg.PerkSwapSkillPointCost : 0;
                return v > 0 ? v : 0;
            }
        }

        /// <summary>
        /// Runtime read of the debug-logging toggle. Defaults to false when the config is unavailable,
        /// so the mod stays silent unless the user explicitly opts in. Read by <see cref="OracleLog"/>.
        /// </summary>
        public static bool DebugLoggingEnabled
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.EnableDebugLogging;
            }
        }

        /// <summary>
        /// Runtime read of the event-outcome-preview toggle. Defaults to false when the config is
        /// unavailable, so a missing config simply hides the preview rather than throwing. Read by
        /// <see cref="EventChoiceHoverPatch"/>.
        /// </summary>
        public static bool ShowEventOutcomePreview
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.ShowEventOutcomePreview;
            }
        }

        /// <summary>
        /// Runtime read of the rolled-perk-highlight toggle. Defaults to TRUE when the config is
        /// unavailable so the mod's headline feature still shows. Read by <see cref="CellBackground"/>.
        /// </summary>
        public static bool EnableRolledPerkHighlight
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg == null || cfg.EnableRolledPerkHighlight;
            }
        }

        /// <summary>
        /// Runtime read of the highlight-color preset. Defaults to Blue (the original tint) when the
        /// config is unavailable. Read live by <see cref="CellBackground"/> on every apply.
        /// </summary>
        public static HighlightColor RolledPerkHighlightColor
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null ? cfg.RolledPerkHighlightColor : HighlightColor.Blue;
            }
        }

        /// <summary>
        /// Runtime read of the perk-wiki toggle. Defaults to TRUE when the config is unavailable.
        /// Read by <see cref="PerkWikiPanel.Open"/>.
        /// </summary>
        public static bool EnablePerkWiki
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg == null || cfg.EnablePerkWiki;
            }
        }

        /// <summary>
        /// Runtime read of the subclass-confirm-decoration toggle. Defaults to TRUE when the config is
        /// unavailable. Read by the SelectSpecialization patches and <see cref="SubclassConfirmPopupDecorator"/>.
        /// </summary>
        public static bool EnableSubclassConfirmDecoration
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg == null || cfg.EnableSubclassConfirmDecoration;
            }
        }

        /// <summary>
        /// Runtime read of the dismantle-yield toggle. Defaults to TRUE when the config is unavailable.
        /// Read by <see cref="ItemScrapTooltipPatch"/>.
        /// </summary>
        public static bool ShowDismantleCompensation
        {
            get
            {
                OracleMain inst = Instance;
                OracleConfig cfg = inst != null ? inst.Config : null;
                return cfg == null || cfg.ShowDismantleCompensation;
            }
        }

        public override bool CanSafelyDisable => true;

        /// <summary>
        /// Build marker for verifying WHICH build the game actually loaded (logged at enable + in the
        /// class-wiki diagnostics). Bump per RCA iteration so a stale DLL is immediately visible in the log.
        /// </summary>
        public const string BuildMarker = "wiki-rca10";

        public override void OnModEnabled()
        {
            Instance = this;
            // UNCONDITIONAL (Info-level, not debug-gated): the marker must identify the loaded build even
            // when debug logging is off at game start (it gets toggled mid-session at the earliest).
            try
            {
                Logger.LogInfo("[Oracle] BUILD " + BuildMarker + " asm="
                    + Assembly.GetExecutingAssembly().GetName().Version);
            }
            catch
            {
                UnityEngine.Debug.Log("[Oracle] BUILD " + BuildMarker);
            }
            var harmony = (Harmony)HarmonyInstance;
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            OracleLog.Debug("[Oracle] PatchAll done");
            LoadLocalization();
            RegisterPerkSwapResearch();
        }

        /// <summary>
        /// Keep the custom perk-swap research in sync with <see cref="AllowPerkSwap"/>. When the swap is
        /// enabled, inject the research def (idempotent; dedups on Guid) so it exists on fresh games and
        /// existing saves. Then flip its UI visibility to match the toggle: hidden while the swap is off,
        /// visible while on. We NEVER remove the def once registered (it may be referenced by a save); hiding
        /// via HideInUI is the safe, reversible way to make it "not exist" in the research UI. Called from
        /// OnModEnabled and OnConfigChanged so both start-state and mid-campaign toggles (either direction)
        /// are covered. Fully guarded: a missing template/repo logs and skips rather than throwing.
        /// </summary>
        private void RegisterPerkSwapResearch()
        {
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                if (AllowPerkSwap)
                {
                    PerkSwapResearch.EnsureRegistered(repo);
                }
                // Sync visibility to the toggle (idempotent; no-op when the def is not registered).
                PerkSwapResearch.SetVisible(repo, AllowPerkSwap);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] RegisterPerkSwapResearch failed: " + ex);
            }
        }

        /// <summary>
        /// Live-apply config changes from the in-game options UI. Re-runs the perk-swap research sync so a
        /// mid-campaign toggle of <see cref="AllowPerkSwap"/> either registers + reveals the research (off→on)
        /// or hides it (on→off) without a reload. Everything else in the config is read live at use sites.
        /// </summary>
        public override void OnConfigChanged()
        {
            base.OnConfigChanged();
            OracleLog.Debug("[Oracle] OnConfigChanged");
            RegisterPerkSwapResearch();
        }

        /// <summary>
        /// Import the mod's localization terms from <c>Assets/Localization/Oracle_Localization.csv</c>
        /// (UTF-8) into I2's primary language source, mirroring TFTV's AddLocalizationFromCSV. Fully
        /// guarded: a missing file or any import error is logged and never thrown out of OnModEnabled,
        /// so the mod still loads (the UI then falls back to its English literals via <see cref="Loc"/>).
        /// </summary>
        private void LoadLocalization()
        {
            try
            {
                // base.Instance is the framework ModInstance (ModMain.Instance); the static
                // OracleMain.Instance shadows it, so qualify with base to reach the install dir.
                string dir = base.Instance.Entry.Directory;
                string path = Path.Combine(dir, "Assets", "Localization", "Oracle_Localization.csv");
                if (!File.Exists(path))
                {
                    OracleLog.Debug("[Oracle] Localization CSV not found: " + path);
                    return;
                }

                string csv = File.ReadAllText(path, Encoding.UTF8);
                if (!csv.EndsWith("\n"))
                {
                    csv += "\n";
                }

                if (LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0)
                {
                    OracleLog.Debug("[Oracle] No I2 localization sources available; skipping CSV import.");
                    return;
                }

                LocalizationManager.Sources[0].Import_CSV(string.Empty, csv, eSpreadsheetUpdateMode.AddNewTerms, ',');
                LocalizationManager.LocalizeAll(true);
                OracleLog.Debug("[Oracle] Localization loaded from " + path);
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] Localization load failed: " + ex);
            }
        }

        public override void OnModDisabled()
        {
            OracleLog.Debug("[Oracle] OnModDisabled");
        }
    }
}
