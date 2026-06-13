using System.Collections.Generic;
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
        /// True when <paramref name="abilityDef"/> is a member of the engine's random rolled-perk pool
        /// for the slot at <paramref name="level0"/> (class <paramref name="className"/>, null = no class
        /// filter). UNION of two independent signals:
        ///   * tagPresent  — the def's CharacterProgressionData carries the live PersonalProgressionTag
        ///     (vanilla engine query at AbilityTrack.CreatePersonalAbilityTrack). Keeps TheTurned's
        ///     arm-marker highlighting: it is tagged but NOT in any by-name slot pool.
        ///   * poolMember  — the def is reference-equal to a member of the resolved per-slot rolled pool
        ///     (PerkWikiPool.ResolveForSlot). Catches TFTV's genuinely-rolled human perks, which arrive
        ///     on the cell WITHOUT the live tag reference (tagPresent=FALSE) but ARE in the slot pool.
        /// Fails CLOSED-as-Fixed: a null def, unresolved tag, and a null/empty pool all just contribute
        /// FALSE to their half, so the only failure mode is "stop highlighting", never "wrongly highlight".
        /// </summary>
        internal static bool IsRolledPoolMember(TacticalAbilityDef abilityDef, int level0, string className)
        {
            if (abilityDef == null)
            {
                return false;
            }

            return TagPresent(abilityDef) || PoolMember(abilityDef, level0, className);
        }

        /// <summary>
        /// tagPresent half: the def's PersonalTrackTags contain the live PersonalProgressionTag.
        /// Null-safe, fail-closed on any null def/prog/tags/tag.
        /// </summary>
        private static bool TagPresent(TacticalAbilityDef abilityDef)
        {
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

        /// <summary>
        /// poolMember half: the def is reference-equal to a member of the slot's resolved rolled pool.
        /// Uses the SAME resolver the wiki uses (PerkWikiPool.ResolveForSlot, never null but may be empty).
        /// </summary>
        private static bool PoolMember(TacticalAbilityDef abilityDef, int level0, string className)
        {
            List<TacticalAbilityDef> pool = PerkWikiPool.ResolveForSlot(level0, className);
            if (pool == null)
            {
                return false;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if ((object)pool[i] == (object)abilityDef)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
