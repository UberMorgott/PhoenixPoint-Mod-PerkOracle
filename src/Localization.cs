using System;

namespace Morgott.Oracle
{
    /// <summary>
    /// Tiny localization façade over I2's <see cref="I2.Loc.LocalizationManager"/>. The mod imports its
    /// own terms from a CSV on enable (see <c>OracleMain.LoadLocalization</c>); this helper
    /// reads them back for the current language with a hard-coded English fallback so the UI always shows
    /// readable text even if the CSV is missing or the term failed to import.
    /// </summary>
    internal static class Loc
    {
        // Returns the current-language translation for key; falls back to `fallback` if the term
        // is missing (I2 returns null/empty or a "<!-MISSING KEY ...->" string) or on any error.
        public static string Get(string key, string fallback)
        {
            try
            {
                string s = I2.Loc.LocalizationManager.GetTranslation(key);
                if (string.IsNullOrEmpty(s) || s.StartsWith("<!")) return fallback;
                return s;
            }
            catch { return fallback; }
        }

        // Normalize a raw .Localize() result: I2 returns the sentinel "<!-MISSING KEY [Term]-!>"
        // (neither null nor empty) for a missing term. Collapse that and empty to null so callers'
        // existing string.IsNullOrEmpty checks treat a missing/sentinel localization as absent
        // instead of leaking the sentinel into the UI.
        public static string Clean(string s) => string.IsNullOrEmpty(s) || s.StartsWith("<!") ? null : s;
    }
}
