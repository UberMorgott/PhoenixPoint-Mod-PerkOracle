using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Single gated entry point for all of the mod's <c>[Oracle] ...</c> diagnostic logging.
    /// Nothing is written unless the user turns on the "Enable debug logging" option in the mod's
    /// settings (<see cref="OracleConfig.EnableDebugLogging"/>, default OFF), so a shipped mod is
    /// silent until someone is actively diagnosing an issue. Reads the flag through
    /// <see cref="OracleMain.DebugLoggingEnabled"/>, which is null-safe: if the config is somehow
    /// unavailable the call is simply a no-op rather than throwing.
    /// </summary>
    internal static class OracleLog
    {
        /// <summary>Write a diagnostic line via <c>Debug.Log</c> only when debug logging is enabled.</summary>
        public static void Debug(string message)
        {
            if (OracleMain.DebugLoggingEnabled)
            {
                UnityEngine.Debug.Log(message);
            }
        }
    }
}
