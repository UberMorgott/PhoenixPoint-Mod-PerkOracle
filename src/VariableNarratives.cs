using System.Collections.Generic;

namespace Morgott.Oracle
{
    /// <summary>
    /// Pure, engine-free mapping of raw engine story-variable ids (<c>OutcomeVariableChange.VariableName</c>)
    /// to narrative advisor prose. Replaces the old mechanical "Variable X: +1" line: a mapped change reads
    /// as an in-world sentence (loc key + English fallback) with the amount tucked into an unobtrusive suffix;
    /// an UNMAPPED variable — or a mapped one changed in a direction/operation we have no phrase for — is
    /// HIDDEN entirely (the engine layer logs the raw name once so coverage gaps get reported).
    ///
    /// Two variable shapes:
    ///   • <see cref="Kind.Flag"/>    — a 0/1 milestone. Shows the phrase with NO number (the value is noise):
    ///     "reached" (set to ≥1 / additive +N) uses <c>UpKey</c>; a reset (set to 0 / additive −N) uses
    ///     <c>DownKey</c> when one exists, else hidden.
    ///   • <see cref="Kind.Counter"/> — a progress tally. Additive up → <c>UpKey</c> " (+N)", additive down →
    ///     <c>DownKey</c> " (−N)", a set → <c>SetKey</c> " (=N)"; RNG ranges render "[a..b]" via
    ///     <see cref="EventOutcomeFormat"/>, never a fabricated single roll. A variant with no key is hidden.
    ///
    /// The English text in the table IS the authored master (the <see cref="Loc.Get"/> fallback); the 8-language
    /// translations live in <c>Oracle_Localization.csv</c> under the same <c>ORACLE_VAR_*</c> keys. No Unity /
    /// engine / Harmony / I2 dependency, so it links + unit-tests under net8 like <see cref="EventOutcomeFormat"/>.
    /// </summary>
    public static class VariableNarratives
    {
        public enum Kind { Flag, Counter }

        /// <summary>Outcome of <see cref="Resolve"/>: render the line, or hide it (silently, or as a logged gap).</summary>
        public enum Status
        {
            /// <summary>A phrase was resolved — show <see cref="Line.LocKey"/>/<see cref="Line.English"/> + suffix.</summary>
            Show,
            /// <summary>Additive 0..0 no-op — hide silently (not a coverage gap, never logged).</summary>
            HiddenNoOp,
            /// <summary>Unknown variable, or a direction/operation with no mapped phrase — hide and log once.</summary>
            HiddenUnmapped,
        }

        /// <summary>Result of resolving one variable change into a preview line.</summary>
        public readonly struct Line
        {
            public readonly Status Status;
            public readonly string LocKey;
            public readonly string English;
            public readonly string Suffix;

            private Line(Status status, string locKey, string english, string suffix)
            {
                Status = status;
                LocKey = locKey;
                English = english;
                Suffix = suffix;
            }

            public static Line Shown(string locKey, string english, string suffix)
                => new Line(Status.Show, locKey, english, suffix);

            public static readonly Line NoOp = new Line(Status.HiddenNoOp, null, null, null);
            public static readonly Line Unmapped = new Line(Status.HiddenUnmapped, null, null, null);
        }

        private sealed class Entry
        {
            public Kind Kind;
            public string UpKey;
            public string UpEn;
            public string DownKey;
            public string DownEn;
            public string SetKey;
            public string SetEn;
        }

        /// <summary>
        /// Resolve one variable change into a preview line. Returns <see cref="Status.HiddenNoOp"/> for an
        /// additive 0..0 no-op (silent), <see cref="Status.HiddenUnmapped"/> for an unknown variable or an
        /// unhandled direction/operation (caller logs), else a <see cref="Status.Show"/> line.
        /// </summary>
        public static Line Resolve(string variableName, int min, int max, bool isSetOperation)
        {
            // Additive 0..0 is a no-op regardless of mapping — hide silently, it's not a coverage gap.
            if (!isSetOperation && min == 0 && max == 0)
            {
                return Line.NoOp;
            }
            if (string.IsNullOrEmpty(variableName) || !Map.TryGetValue(variableName, out Entry e))
            {
                return Line.Unmapped;
            }

            if (e.Kind == Kind.Flag)
            {
                // Milestone: "reached" when set to ≥1 or added to; "reset" when set to 0 or subtracted from.
                bool up = isSetOperation ? max >= 1 : max > 0;
                string key = up ? e.UpKey : e.DownKey;
                string en = up ? e.UpEn : e.DownEn;
                if (string.IsNullOrEmpty(key))
                {
                    return Line.Unmapped;
                }
                // Flags never show a number — the 0/1 value carries no player-facing meaning.
                return Line.Shown(key, en, string.Empty);
            }

            // Counter.
            if (isSetOperation)
            {
                if (string.IsNullOrEmpty(e.SetKey))
                {
                    return Line.Unmapped;
                }
                return Line.Shown(e.SetKey, e.SetEn, " (=" + EventOutcomeFormat.Range(min, max) + ")");
            }

            // Additive: signed delta, or the signed range when the RNG bounds differ (mirrors the old adapter).
            string signedExpr = (min == max)
                ? EventOutcomeFormat.Signed(min)
                : (max < 0 ? "-" : "+") + EventOutcomeFormat.Range(min, max);
            bool increase = max > 0;
            string upDownKey = increase ? e.UpKey : e.DownKey;
            string upDownEn = increase ? e.UpEn : e.DownEn;
            if (string.IsNullOrEmpty(upDownKey))
            {
                return Line.Unmapped;
            }
            return Line.Shown(upDownKey, upDownEn, " (" + signedExpr + ")");
        }

        private static readonly Dictionary<string, Entry> Map = Build();

        private static void Flag(Dictionary<string, Entry> m, string name, string upKey, string upEn,
            string downKey = null, string downEn = null)
        {
            m[name] = new Entry { Kind = Kind.Flag, UpKey = upKey, UpEn = upEn, DownKey = downKey, DownEn = downEn };
        }

        private static void Counter(Dictionary<string, Entry> m, string name, string upKey, string upEn,
            string downKey = null, string downEn = null, string setKey = null, string setEn = null)
        {
            m[name] = new Entry
            {
                Kind = Kind.Counter,
                UpKey = upKey, UpEn = upEn,
                DownKey = downKey, DownEn = downEn,
                SetKey = setKey, SetEn = setEn,
            };
        }

        private static Dictionary<string, Entry> Build()
        {
            var m = new Dictionary<string, Entry>();

            // --- Vanilla ---
            Counter(m, "w", "ORACLE_VAR_W", "Strange finds keep surfacing across the wastes — something out there is stirring.");
            Counter(m, "Polyphonic", "ORACLE_VAR_POLYPHONIC", "The Polyphonic voices gain sway within Synedrion.");
            Counter(m, "Terraformers", "ORACLE_VAR_TERRAFORMERS", "The Terraformers' vision takes hold within Synedrion.");
            Counter(m, "BC_SDI", "ORACLE_VAR_BC_SDI_UP", "The Pandoran threat deepens.",
                "ORACLE_VAR_BC_SDI_DOWN", "The Pandoran threat recedes, for now.");
            Flag(m, "BehemothEggHatched", "ORACLE_VAR_BEHEMOTH_EGG", "Somewhere, a Behemoth egg has hatched.");
            Flag(m, "ODI_Complete", "ORACLE_VAR_ODI_COMPLETE", "The alien evolution has reached its terrible summit.");
            Flag(m, "ThirdActStarted", "ORACLE_VAR_THIRD_ACT", "The war enters its final act.");
            Flag(m, "Pandorans_Researched_Citadel", "ORACLE_VAR_CITADEL", "The Pandorans have learned to raise citadels.");

            // --- TFTV story / encounters ---
            Counter(m, "Umbra_Encounter_Variable", "ORACLE_VAR_UMBRA_STAGE", "The Umbra strain grows more monstrous.");
            Flag(m, "Infestation_Encounter_Variable", "ORACLE_VAR_INFESTATION", "Infestation takes root among the havens.");
            Counter(m, "Ancients_Encounter_Global_Variable", "ORACLE_VAR_ANCIENTS", "The trail of the Ancients grows warmer.");
            Flag(m, "FiveDeliriumReached", "ORACLE_VAR_DELIRIUM", "One of your soldiers slips deep into delirium.");
            Flag(m, "CorruptedLairDestroyed", "ORACLE_VAR_CORRUPTED_LAIR", "A corrupted lair has been burned out.");
            Flag(m, "FavorForAFriend", "ORACLE_VAR_FAVOR", "An old debt calls in a favor.");
            Flag(m, "Oglethorpe", "ORACLE_VAR_OGLETHORPE", "Oglethorpe's records point to something buried in time.");
            Flag(m, "Photographer", "ORACLE_VAR_PHOTOGRAPHER", "The photographer's images reveal more than they should.");
            Flag(m, "Sphere", "ORACLE_VAR_SPHERE", "The strange sphere is yours to study.");
            Flag(m, "SymesAlternativeCompleted", "ORACLE_VAR_SYMES", "Symes' alternative path has run its course.");
            Flag(m, "ManufacturedImpossibleWeapon", "ORACLE_VAR_IMPOSSIBLE_WEAPON", "An impossible weapon has been forged.");
            Flag(m, "CyclopsBuiltVariable", "ORACLE_VAR_CYCLOPS", "The Cyclops walks.");
            Counter(m, "ProteanMutaneResearched", "ORACLE_VAR_PROTEAN_MUTANE", "Understanding of protean mutane deepens.");
            Flag(m, "UmbraResearched", "ORACLE_VAR_UMBRA_RESEARCHED", "The Umbra's secrets lie open on the slab.");

            // --- Behemoth / Pandoran flyers (TFTV) ---
            Flag(m, "FirstPandoranFlyerSpawned", "ORACLE_VAR_FIRST_FLYER", "The first winged horror takes to the skies.");
            Flag(m, "BehemothTargettedFirstHaven", "ORACLE_VAR_BEHEMOTH_TARGET", "A Behemoth fixes its course on a haven.");
            Flag(m, "BehemothAttackedFirstHaven", "ORACLE_VAR_BEHEMOTH_ATTACK", "A Behemoth falls upon its first haven.");
            Flag(m, "BehemothPatternEventTriggered", "ORACLE_VAR_BEHEMOTH_PATTERN", "A pattern emerges in the Behemoths' roaming.");
            Counter(m, "BehemothRoamings", "ORACLE_VAR_BEHEMOTH_ROAM", "The Behemoths range ever wider.");
            Flag(m, "CharunAreComing", "ORACLE_VAR_CHARUN", "The Charun are coming.");
            Flag(m, "BerithResearchVariable", "ORACLE_VAR_BERITH", "Word spreads of a new flying breed — the Berith.");
            Flag(m, "AbbadonResearchVariable", "ORACLE_VAR_ABBADON", "The Abbadon's shadow is studied and named.");

            // --- Revenants (TFTV) ---
            Counter(m, "Revenant_Spotted", "ORACLE_VAR_REVENANT_SPOTTED", "Revenant sightings keep mounting.");
            Counter(m, "RevenantsDestroyed", "ORACLE_VAR_REVENANT_DESTROYED", "Another Revenant is put down for good.");
            Flag(m, "RevenantCapturedVariable", "ORACLE_VAR_REVENANT_CAPTURED", "A Revenant has been taken alive.");

            // --- Infestation (TFTV) ---
            Counter(m, "Number_of_Infested_Havens", "ORACLE_VAR_INFESTED_HAVENS_UP", "The infestation spreads to another haven.",
                "ORACLE_VAR_INFESTED_HAVENS_DOWN", "A haven is cleansed of infestation.");
            Flag(m, "TrappedInTheMistTriggered", "ORACLE_VAR_TRAPPED_MIST", "Something in the mist closes in.");

            // --- Background anger / promise chains (TFTV) ---
            Flag(m, "BG_Anu_Pissed_Over_Bionics", "ORACLE_VAR_ANU_BIONICS_1", "The Disciples of Anu bristle at your embrace of bionics.");
            Flag(m, "BG_Anu_Really_Pissed_Over_Bionics", "ORACLE_VAR_ANU_BIONICS_2", "Anu's contempt for your bionics hardens into fury.");
            Flag(m, "BG_Anu_Pissed_Made_Promise", "ORACLE_VAR_ANU_PROMISE_MADE", "You give Anu your word to mend the rift.");
            Flag(m, "BG_Anu_Pissed_Broke_Promise", "ORACLE_VAR_ANU_PROMISE_BROKE", "Your promise to Anu lies broken.");
            Flag(m, "BG_NJ_Pissed_Over_Mutations", "ORACLE_VAR_NJ_MUTATIONS_1", "New Jericho scorns your dabbling in mutations.");
            Flag(m, "BG_NJ_Really_Pissed_Over_Mutations", "ORACLE_VAR_NJ_MUTATIONS_2", "New Jericho's scorn for your mutations turns to open anger.");
            Flag(m, "BG_NJ_Pissed_Made_Promise", "ORACLE_VAR_NJ_PROMISE_MADE", "You give New Jericho your word to change course.");
            Flag(m, "BG_NJ_Pissed_Broke_Promise", "ORACLE_VAR_NJ_PROMISE_BROKE", "Your promise to New Jericho lies broken.");

            // --- Pure / Forsaken ---
            Counter(m, "PU13", "ORACLE_VAR_PURE_CAMPAIGN", "The campaign against the Pure grinds on.");
            Flag(m, "PU10", "ORACLE_VAR_BIONIC_FORTRESS", "The bionic fortress meets its fate.");

            // --- Misc ---
            Counter(m, "FoodPoisoning", "ORACLE_VAR_FOOD_POISONING", "Tainted rations sicken the crew once more.");
            Counter(m, "ScyllaCounter", "ORACLE_VAR_SCYLLA", "Another Scylla stalks the earth.");

            return m;
        }
    }
}
