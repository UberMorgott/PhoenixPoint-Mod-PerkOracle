using System;
using System.IO;
using System.Reflection;
using System.Text;
using Base.Core;
using Base.Defs;
using HarmonyLib;
using I2.Loc;
using PhoenixPoint.Modding;

namespace Morgott.PerkOracle
{
    public class PerkOracleMain : ModMain
    {
        public static new PerkOracleMain Instance { get; private set; }

        /// <summary>Strongly-typed view of the mod's config (declared public fields auto-exposed in-game).</summary>
        public new PerkOracleConfig Config => (PerkOracleConfig)base.Config;

        /// <summary>
        /// Runtime read of the perk-swap toggle. Defaults to false if the config is somehow unavailable,
        /// so the feature stays opt-in and a missing config can never accidentally enable swaps.
        /// </summary>
        public static bool AllowPerkSwap
        {
            get
            {
                PerkOracleMain inst = Instance;
                PerkOracleConfig cfg = inst != null ? inst.Config : null;
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
                PerkOracleMain inst = Instance;
                PerkOracleConfig cfg = inst != null ? inst.Config : null;
                return cfg == null || cfg.RequirePerkSwapResearch;
            }
        }

        /// <summary>
        /// Runtime read of the STUB "swap costs resources" toggle. Has no behavior yet (see
        /// <c>PerkSwapper.ApplyResourceCostStub</c>); exposed only so the future hook can read it. Defaults
        /// to false when the config is unavailable.
        /// </summary>
        public static bool PerkSwapCostsResources
        {
            get
            {
                PerkOracleMain inst = Instance;
                PerkOracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.PerkSwapCostsResources;
            }
        }

        /// <summary>
        /// Runtime read of the debug-logging toggle. Defaults to false when the config is unavailable,
        /// so the mod stays silent unless the user explicitly opts in. Read by <see cref="PerkOracleLog"/>.
        /// </summary>
        public static bool DebugLoggingEnabled
        {
            get
            {
                PerkOracleMain inst = Instance;
                PerkOracleConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.EnableDebugLogging;
            }
        }

        public override bool CanSafelyDisable => true;

        public override void OnModEnabled()
        {
            Instance = this;
            PerkOracleLog.Debug("[PerkOracle] OnModEnabled");
            var harmony = (Harmony)HarmonyInstance;
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            PerkOracleLog.Debug("[PerkOracle] PatchAll done");
            LoadLocalization();
            RegisterPerkSwapResearch();
        }

        /// <summary>
        /// Inject the custom perk-swap research into the live def repository. The ModSDK ModMain does not
        /// expose an ApplyDefRepoPatches override, so we register from OnModEnabled via the global
        /// DefRepository game component — the same path the Officer mod uses. EnsureRegistered is idempotent
        /// (dedups on the def Guid) and fully guarded, so a missing template logs and skips rather than
        /// throwing out of mod enable.
        /// </summary>
        private void RegisterPerkSwapResearch()
        {
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                PerkSwapResearch.EnsureRegistered(repo);
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] RegisterPerkSwapResearch failed: " + ex);
            }
        }

        /// <summary>
        /// Import the mod's localization terms from <c>Assets/Localization/PerkOracle_Localization.csv</c>
        /// (UTF-8) into I2's primary language source, mirroring TFTV's AddLocalizationFromCSV. Fully
        /// guarded: a missing file or any import error is logged and never thrown out of OnModEnabled,
        /// so the mod still loads (the UI then falls back to its English literals via <see cref="Loc"/>).
        /// </summary>
        private void LoadLocalization()
        {
            try
            {
                // base.Instance is the framework ModInstance (ModMain.Instance); the static
                // PerkOracleMain.Instance shadows it, so qualify with base to reach the install dir.
                string dir = base.Instance.Entry.Directory;
                string path = Path.Combine(dir, "Assets", "Localization", "PerkOracle_Localization.csv");
                if (!File.Exists(path))
                {
                    PerkOracleLog.Debug("[PerkOracle] Localization CSV not found: " + path);
                    return;
                }

                string csv = File.ReadAllText(path, Encoding.UTF8);
                if (!csv.EndsWith("\n"))
                {
                    csv += "\n";
                }

                if (LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0)
                {
                    PerkOracleLog.Debug("[PerkOracle] No I2 localization sources available; skipping CSV import.");
                    return;
                }

                LocalizationManager.Sources[0].Import_CSV(string.Empty, csv, eSpreadsheetUpdateMode.AddNewTerms, ',');
                LocalizationManager.LocalizeAll(true);
                PerkOracleLog.Debug("[PerkOracle] Localization loaded from " + path);
            }
            catch (Exception ex)
            {
                PerkOracleLog.Debug("[PerkOracle] Localization load failed: " + ex);
            }
        }

        public override void OnModDisabled()
        {
            PerkOracleLog.Debug("[PerkOracle] OnModDisabled");
        }
    }
}
