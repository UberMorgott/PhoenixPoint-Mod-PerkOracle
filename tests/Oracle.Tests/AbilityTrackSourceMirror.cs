// Mirror of the base game enum PhoenixPoint.Common.Entities.Characters.AbilityTrackSource
// (AbilityTrackSource.cs:3-8). Same namespace + member order so the linked production
// PerkClassification.cs compiles against this under net8 without referencing Unity's
// Assembly-CSharp. Keep in sync if the real enum ever changes.
namespace PhoenixPoint.Common.Entities.Characters
{
    public enum AbilityTrackSource
    {
        PrimaryClass,
        SecondaryClass,
        Personal
    }
}
