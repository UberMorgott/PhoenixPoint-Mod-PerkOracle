using System.Linq;
using Base.Core;
using Base.Entities.Abilities;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Tactical.Entities.Abilities;

namespace Morgott.Oracle
{
    /// <summary>
    /// Rolled-perk-pool membership discriminator. The engine's own definition of the random Personal
    /// pool (AbilityTrack.CreatePersonalAbilityTrack / FactionCharacterGenerator._personalAbilityPool):
    /// an ability can be randomly rolled into the Personal track iff its CharacterProgressionData
    /// carries the PersonalProgressionTag. This mirrors TftvConfigBridge.GetVanillaPersonalPool's filter
    /// for a single def. Engine-typed, so it lives outside the pure (unit-tested) classifier.
    /// </summary>
    internal static class RolledPoolMembership
    {
        /// <summary>
        /// True when <paramref name="abilityDef"/> is a member of the engine's random rolled-perk pool.
        /// Fails CLOSED-as-Fixed: any unresolved tag / null def returns false (suppress highlight), so the
        /// only failure mode is "stop highlighting", never "wrongly highlight".
        /// </summary>
        internal static bool IsRolledPoolMember(TacticalAbilityDef abilityDef)
        {
            if (abilityDef == null)
            {
                return false;
            }

            AbilityCharacterProgressionDef prog = abilityDef.CharacterProgressionData;
            if (prog == null || prog.PersonalTrackTags == null)
            {
                return false;
            }

            SkillTagDef tag = GameUtl.GameComponent<SharedData>()?.SharedGameTags?.PersonalProgressionTag;
            if (tag == null)
            {
                return false; // tag unresolved -> treat as not-rolled (suppress highlight)
            }

            // GameTagDef[].Contains(SkillTagDef): SkillTagDef IS-A GameTagDef, reference equality.
            // Identical to the engine query at AbilityTrack.CreatePersonalAbilityTrack.
            return prog.PersonalTrackTags.Contains(tag);
        }
    }
}
