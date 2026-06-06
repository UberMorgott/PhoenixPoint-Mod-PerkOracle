using System;
using System.IO;
using System.Reflection;
using System.Text;
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

        public override bool CanSafelyDisable => true;

        public override void OnModEnabled()
        {
            Instance = this;
            Logger.LogInfo("[PerkOracle] OnModEnabled");
            var harmony = (Harmony)HarmonyInstance;
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("[PerkOracle] PatchAll done");
            LoadLocalization();
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
                    Logger.LogWarning("[PerkOracle] Localization CSV not found: " + path);
                    return;
                }

                string csv = File.ReadAllText(path, Encoding.UTF8);
                if (!csv.EndsWith("\n"))
                {
                    csv += "\n";
                }

                if (LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0)
                {
                    Logger.LogWarning("[PerkOracle] No I2 localization sources available; skipping CSV import.");
                    return;
                }

                LocalizationManager.Sources[0].Import_CSV(string.Empty, csv, eSpreadsheetUpdateMode.AddNewTerms, ',');
                LocalizationManager.LocalizeAll(true);
                Logger.LogInfo("[PerkOracle] Localization loaded from " + path);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[PerkOracle] Localization load failed: " + ex);
            }
        }

        public override void OnModDisabled()
        {
            Logger.LogInfo("[PerkOracle] OnModDisabled");
        }
    }
}
