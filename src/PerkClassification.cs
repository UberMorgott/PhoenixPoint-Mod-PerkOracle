using System;
using PhoenixPoint.Common.Entities.Characters;

namespace Morgott.Oracle
{
    public enum PerkKind
    {
        Rolled,
        Fixed,
        Unknown
    }

    /// <summary>
    /// Pure decision logic: given a cell's track source and level slot, decide whether the
    /// perk in that cell was randomly rolled, is fixed, or is unknown/absent. No Unity, TFTV
    /// or Harmony types referenced, so it is unit-testable in isolation.
    /// </summary>
    public static class PerkClassification
    {
        /// <param name="source">Which of the three progression rows the cell belongs to.</param>
        /// <param name="level0">0-based slot index within the Personal track (ignored for class rows).</param>
        /// <param name="abilityPresent">False when the cell is empty (no ability to classify).</param>
        /// <param name="bridgeAvailable">
        /// True when the TFTV config bridge resolved. False means no TFTV (vanilla): the whole
        /// Personal track is rolled, so a present Personal perk is treated as Rolled.
        /// </param>
        /// <param name="isSlotRandom">Looks up whether a given Personal level slot is random (TFTV config).</param>
        /// <param name="abilityIsRolledPoolMember">
        /// True when the cell's ability is a member of the engine's random rolled-perk pool (its
        /// CharacterProgressionData carries the PersonalProgressionTag). Augmentation / custom-mod
        /// abilities (PersonalTrackTags empty) are never randomly rolled, so a present Personal cell
        /// is always Fixed for them. This is the engine's OWN pool-membership test, hierarchy- and
        /// owner-independent. Defaults to true, so callers that do not pass it (and the pure unit
        /// tests) keep the existing human behavior unchanged.
        /// </param>
        public static PerkKind Classify(
            AbilityTrackSource source,
            int level0,
            bool abilityPresent,
            bool bridgeAvailable,
            Func<int, bool> isSlotRandom,
            bool abilityIsRolledPoolMember = true)
        {
            if (!abilityPresent)
            {
                return PerkKind.Unknown;
            }

            // Class rows are always fixed.
            if (source != AbilityTrackSource.Personal)
            {
                return PerkKind.Fixed;
            }

            // A Personal cell can be Rolled ONLY if its ability is a member of the engine's random
            // rolled-perk pool (carries PersonalProgressionTag). Augmentation / custom-mod abilities
            // (PersonalTrackTags empty) are never rolled -> Fixed. Gates every Rolled path below.
            if (!abilityIsRolledPoolMember)
            {
                return PerkKind.Fixed;
            }

            // Personal row. Without TFTV the personal track is purely rolled.
            if (!bridgeAvailable)
            {
                return PerkKind.Rolled;
            }

            // Under TFTV the personal track mixes fixed and rolled slots; ask the config.
            return (isSlotRandom != null && isSlotRandom(level0)) ? PerkKind.Rolled : PerkKind.Fixed;
        }
    }
}
