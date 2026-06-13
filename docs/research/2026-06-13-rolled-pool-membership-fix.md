# Handoff — rolled-highlight gate on `PersonalProgressionTag` pool membership

> Fix record + reusable engine finding. Why a Personal cell is "rolled" is now decided purely by the
> ability's **pool membership** (carries `PersonalProgressionTag`), not by slot level-index or
> owner identity. Compressed bullets; `type` / `path:line` anchors kept exact.

## Status (2026-06-13)

- Code: DONE. `dotnet build -c Release` = **0 warnings / 0 errors**; `dotnet test` = **59/59 pass**.
- Deployed fresh via `deploy.ps1` to `D:\Steam\steamapps\common\Phoenix Point\Mods\Oracle`.
- **IN-GAME VERIFICATION PENDING — user tests 2026-06-14.**

## Symptom

- On the user's CUSTOM mod monster-soldiers, the bottom (**Personal**) "mutoid-like" ability row was
  wrongly highlighted as **rolled** perks (partial highlight pattern).
- User has **no real vanilla Mutoid**; these are custom units. The highlight was simply wrong for them.

## Root cause

- PerkOracle classified a Personal cell as **Rolled** based on the slot **level-index**:
  - With TFTV: the `OrderOfPersonalPerks[level0].IsRandom` table — a **fixed human layout**.
  - Without TFTV: treated the **whole Personal track** as Rolled.
- Neither path checked whether the **actual ability** is a rollable-pool perk.
- An earlier attempted fix (owner-is-Mutoid gate via `GetComponentInParent<AbilityTrackContainerElement>`
  + reflected `_character`) **FAILED** because the owner never resolves on these paths (container = NULL).

## Runtime evidence (in-game `[OracleMutoidDiag]`, since removed)

- 5 cells, all `popupAncestor = NULL` (main-grid path), `container = NULL`, `owner = NULL`.
- `PersonalTrackTags = []` (**EMPTY**) on every one of them.
- Ability defs seen:
  - `VirusResistant/FireResistant/PoisonImmunity_DamageMultiplierAbilityDef`
  - `Mutoid_PainChameleon_AbilityDef`
  - `Fishman_Regeneration_Passive_AbilityDef`
- `MutagenCost` 10 / 15 / 20 / 25 / 30.
- Conclusion: owner-based gating is unworkable here; the only reliable signal is on the **def itself**.

## The fix (fully dynamic, hierarchy/owner-independent)

- **Rule:** a Personal cell is **Rolled** ONLY if its ability is a member of the engine's random
  rolled-perk pool — i.e. `AbilityDef.CharacterProgressionData.PersonalTrackTags` contains
  `SharedData.SharedGameTags.PersonalProgressionTag`.
- This is the **verbatim engine filter** from `AbilityTrack.CreatePersonalAbilityTrack`, and mirrors
  the mod's own `TftvConfigBridge.GetVanillaPersonalPool` (`src/TftvConfigBridge.cs:291`).
- Tagless abilities (Mutoid augmentations, custom-mod abilities) -> **Fixed** -> not highlighted.

### Files changed

- `src/PerkClassification.cs` — `Classify`: param `ownerIsMutoid` (default `false`) ->
  `abilityIsRolledPoolMember` (default `true`); precondition `if (!abilityIsRolledPoolMember) return Fixed;`
  placed **after** the `source != Personal` check and **before** both the no-bridge whole-track-Rolled
  branch and the TFTV `IsSlotRandom` branch. (`Classify` @ `src/PerkClassification.cs:35`.)
- `src/RolledPoolMembership.cs` — **NEW** engine-typed helper `IsRolledPoolMember(TacticalAbilityDef)`,
  **fail-closed** (any null def / prog / tags / tag -> `false`). (`@ src/RolledPoolMembership.cs:23`.)
- `src/AbilityTrackSkillEntryElementPatches.cs` — highlight postfix: removed owner resolution +
  `ownerIsMutoid` + the entire `[OracleMutoidDiag]` diagnostic block; now computes `isPoolMember` from
  `__instance.AbilityDef` and passes it to `Classify`; dead usings cleaned.
  (`isPoolMember` @ `:63`, passed @ `:71`.)
- `src/OnCancelInputHandlerPatch.cs` — wiki `IsRolled` gate: computes pool membership from
  `cell.AbilityDef`; dropped the now-unused `GeoCharacter` owner param from `IsRolled` /
  `FindRolledCellUnderPointer`. (`isPoolMember` @ `:160`, passed @ `:167`.)
- `src/MutoidDetection.cs` — **DELETED** (was untracked; helper no longer used).
- `tests/Oracle.Tests/PerkClassificationTests.cs` — rewrote the 3 `ownerIsMutoid` tests as
  pool-membership tests + added 2 precedence tests (gates the no-TFTV and the TFTV branches).

## Safety rationale

- Vanilla + TFTV human rolled perks **carry** `PersonalProgressionTag` (TFTV never touches the tag;
  its random Personal slots resolve to **stock vanilla** personal-track defs), so their highlighting
  is **unchanged**.
- By construction the precondition **cannot drop a genuinely-rollable perk** — if it carries the tag,
  it passes.

## Verify tomorrow (checklist)

1. Custom monster units' bottom **Personal** row is **NOT** highlighted.
2. A normal human soldier's rolled perks are **STILL** highlighted, and **right-click opens the perk
   wiki** as before.
3. If a custom unit has a genuinely **tagged** rolled perk, it **SHOULD** still highlight (correct).

## If still wrong — how to continue

- Re-add an unconditional `[OracleMutoidDiag]` log in the **highlight postfix** of
  `src/AbilityTrackSkillEntryElementPatches.cs`, logging: `AbilityDef` name + `PersonalTrackTags` +
  `isPoolMember` + final `kind`.
- Redeploy via `deploy.ps1`.
- Capture `C:\Users\Morgott\AppData\LocalLow\Snapshot Games Inc\Phoenix Point\Player.log`.
- Key files to inspect:
  - `src/PerkClassification.cs` (`Classify`) — the gating logic.
  - `src/RolledPoolMembership.cs` — the membership helper (fail-closed).
  - `src/TftvConfigBridge.cs` (`GetVanillaPersonalPool` @ `:291`) — the **reference filter** the rule mirrors.

## Build / deploy / test commands

- Build + deploy: `deploy.ps1` (builds Release + copies to game `Mods\Oracle`).
- Tests: `dotnet test tests\Oracle.Tests\Oracle.Tests.csproj`
  (set `$env:DOTNET_ROLL_FORWARD = "LatestMajor"` if the test host needs a newer runtime).
