# Oracle — Research-gated perk swap (+ stub cost toggle)

Date: 2026-06-06 · Status: approved, implementing · Mod: Oracle (standalone, ns `Morgott.Oracle`, Dependencies `[]`)

## Goal

Gate the existing (currently free) perk-swap behind a custom geoscape research project. Add a non-functional "costs resources" config checkbox as a placeholder for a future resource cost. No TFTV dependency — vanilla PhoenixPoint modding API only.

## Decisions (locked)

- `RequirePerkSwapResearch` config default = **ON**.
- Research is **available from game start**: `InitialStates[*].State = ResearchState.Unlocked`, **no reveal/unlock requirements** (clear cloned requirement containers).
- Research **cost** in research points = "expensive": ground a vanilla mid/high-tier project cost and set the const to ~1.5× of it. Single `const`/field, easy to tune.
- Swap denied when research incomplete → show a **short feedback message** to the player (not silent).
- `PerkSwapCostsResources` = **stub only** (visible + localized checkbox, no behavior).
- Custom **icon**: user supplies a PNG (see Image spec). Code loads PNG → Sprite at runtime. If the Addressable `ResearchIcon` field cannot accept a runtime Sprite, fall back to the template's stock icon for v1 and report the blocker.
- Autonomy boundary: implement → build-verify → commit → **push to GitHub main**. Do **NOT** republish to Steam Workshop (held for user's in-game test).

## Config (`OracleConfig`)

| Field | Type | Default | Status |
|---|---|---|---|
| `AllowPerkSwap` (existing) | bool | OFF | master toggle — unchanged |
| `RequirePerkSwapResearch` (new) | bool | ON | REAL — swap allowed only if research completed |
| `PerkSwapCostsResources` (new) | bool | OFF | STUB — no behavior, placeholder |

Master logic unchanged: nothing happens unless `AllowPerkSwap` is ON. When ON, swap also requires the research by default (toggle off for "free play"). Mod description notes everything sits under the master toggle.

## Custom ResearchDef

- Id: `ORACLE_PerkSwap_ResearchDef`.
- Registration: `PerkSwapResearch.EnsureRegistered(DefRepository repo)` called from `OnModEnabled` in the mod main class, resolving the repo via `GameUtl.GameComponent<DefRepository>()` (idempotent). NOTE: the spec originally cited `override ApplyDefRepoPatches(DefRepository)`, but that virtual exists only in the full game decompile, NOT on the ModSDK `ModMain` we compile against — confirmed by reflecting the ModSDK assembly. `OnModEnabled` + `GameComponent<DefRepository>()` is the verified pattern the Officer mod uses on this same SDK.
- Creation (vanilla, no TFTV): `repo.CreateDef<ResearchDef>(id, templateOriginal)` — clones, sets Guid=id, adds to `AllDefs` (tree sees it). Template: `PX_AtmosphericAnalysis_ResearchDef`.
- After clone: set `Faction` (keep cloned), `Id`, `ResearchCost`, clear `Unlocks = new ...[0]`, `Tags = new ...[0]`, clear requirement containers, set all `InitialStates[*].State = Unlocked`, attach `ViewElementDef`.
- Register in tree: `repo.GetAllDefs<ResearchDbDef>().First(d => d.name == "pp_ResearchDB").Researches.Add(myDef)`.
- Dedup: `if (repo.GetDef(GUID) != null) return;`.

### Grounded API anchors (verify at implementation)
- `DefRepository.CreateDef<T>(id, original)` (game decompile DefRepository.cs:254-279).
- `ResearchDbDef.Researches` (ResearchDbDef.cs:12); tree read `GetAllDefs<ResearchDbDef>().SelectMany(r => r.Researches)` (Research.cs:300-303).
- Hook `ModMain.ApplyDefRepoPatches(DefRepository)` (ModMain.cs:66).
- `ResearchState.Unlocked = 2` (available now; Research.cs:297 CanResearch). Empty requirements auto-OK (ResearchElement.cs:466-468).
- `ResearchViewElementDef.ResearchIcon : AssetReferenceSprite` (ResearchViewElementDef.cs:24) — Addressable. Custom PNG path: `ReadAllBytes → new Texture2D(2,2,RGBA32) → ImageConversion.LoadImage → Sprite.Create` (mirror of TFTV Helper.cs:167-183) routed to a Sprite-accepting field.
- Loc keys live on the view def: `DisplayName1` (ViewElementDef.cs:30), `Description` (:21), reveal/unlock/complete/benefits text (:16-22) — set `.LocalizationKey`.
- Completion check: `GeoFaction.Research.HasCompleted(string)` (Research.cs:535-546); event `Research.OnResearchCompleted` (:174).

## Gate enforcement

At the swap entry point (`WikiAbilityTooltipTrigger.OnPointerClick` → `PerkSwapper.TrySwap`), before applying:
- if `Config.RequirePerkSwapResearch && !character.Faction.Research.HasCompleted(ResearchId)` → deny + short on-screen feedback ("requires PerkSwap research", localized). Reuse existing deny/feedback path if one exists; otherwise minimal message.
- Prefer extending `PerkSwapDecision` with a new verdict (e.g. `DenyResearchLocked`) so the gate stays in the pure decision layer, and surface the message at the click handler.

## Stub cost

- `PerkSwapCostsResources` checkbox + localized label/description. Wired to a no-op hook point (a method/comment marking the future resource-cost insertion); always "free". No `Wallet`/`SkillPoints` access yet.

## Localization

- Add rows to the existing 8-lang CSV (imported via `Import_CSV`/`AddNewTerms`, OracleMain.cs:50-84) — **no new loading code**.
- New keys: `ORACLE_Research_Name`, `ORACLE_Research_Description`, `ORACLE_Research_Benefits`, `ORACLE_Research_Complete`, `ORACLE_SwapResearchLocked` (deny message), `ORACLE_PerkSwapCostsResources` + `_DESCRIPTION`, `ORACLE_RequirePerkSwapResearch` + `_DESCRIPTION`.
- Langs: english, russian, german, french, spanish, italian, polish, schinese.
- EN + RU source text below is authored (anti-AI tone). The other 6 are machine-translated by the implementer and **flagged for user review**.

### Research text — EN (source)
- Name: `Operative Reconditioning`
- Description: `Phoenix Project drill records survived the collapse. They describe how a trained operative can be put back through conditioning and brought out the other side with a different specialty. Old reflexes are not added to, they are overwritten. The work is dull and the soldier is off the line while it runs, but a squad is no longer stuck with the specialists it was handed.`
- Benefits: `Retrains a soldier so a personal perk gained at random can be relearned as a different one.`
- Complete: `Reconditioning protocols restored. Operatives can be retrained.`
- Deny message (`ORACLE_SwapResearchLocked`): `Soldiers can only be retrained once the "Operative Reconditioning" research is complete.`

### Research text — RU (source)
- Name: `Переподготовка оперативников`
- Description: `Уцелели тренировочные протоколы «Феникса». В них описано, как обученного бойца можно заново провести через подготовку и вывести из неё с другой специализацией. Старые рефлексы не дополняются, а переписываются. Работа муторная, и на это время боец выбывает из строя, но отряд больше не привязан к тому набору специалистов, что достался изначально.`
- Benefits: `Переучивает бойца так, что случайно полученный личный перк можно сменить на другой.`
- Complete: `Протоколы переподготовки восстановлены. Оперативников можно переучивать.`
- Deny message: `Бойцов можно переучивать только после завершения исследования «Переподготовка оперативников».`

## Implementation risks (verify during TDD/build)

- 3 template ids ([T]-confirmed strings, vanilla assets not enumerated) → null-check fail-safe; abort registration gracefully if any template missing.
- `ResearchCostDef`/`RewardDef`/`TagDef`/requirement-container ctor fields not deep-read → example uses empty arrays/cleared containers only; verify the cleared containers don't NPE in `Research.Initialize`.
- Cloned `InitialStates` length unverified → loop over all entries.
- Custom Sprite into `ResearchIcon` (Addressable) not cleanly supported → resolve the displayed Sprite field or Harmony-inject at display; fall back to stock icon for v1 if blocked.

## Out of scope (this iteration)

- Actual resource cost (the stub stays a no-op).
- Steam Workshop republish.
- In-game verification (manual, by user).

## Image (custom research icon — user-generated externally)

See the chat message for format/size + the generation prompt. Code must load the PNG from a fixed mod asset path (implementer picks/derives it from how the mod already loads assets) and assign as the research icon Sprite; placeholder until the user drops the final PNG in.
