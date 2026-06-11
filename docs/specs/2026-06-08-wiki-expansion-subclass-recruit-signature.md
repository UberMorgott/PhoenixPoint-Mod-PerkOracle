# Oracle — Wiki expansion: subclass preview, recruit full-page preview & merc gimmick in merc-shop description

Date: 2026-06-08 · Status: **road map — design decisions resolved & Phase-1 investigation spike COMPLETE; implementation plans next** (not yet scheduled) · Mod: Oracle (standalone, ns `Morgott.Oracle`, Dependencies `[]`)

> Road map for future updates, all shipping inside the **one** Oracle mod. Grounds three new surfaces in the mod's existing architecture, built strictly from **native game components, surgically modified** (clone/Harmony-patch native elements — no bespoke UI). API anchors taken from the current codebase are marked **[confirmed]**; anchors located during the Phase-1 investigation spike (against the game/TFTV decompile + reference mods) are marked **[verified — spike]** with the concrete type/path/symbol grounding. The Phase-1 spike is **complete** — all `[verify]` anchors are now resolved; implementation plans/specs per remaining feature come next.

## Process / resources

- **All work is delegated to sub-agents.** Investigation, decompile research, and implementation are performed by dispatched sub-agents returning compressed bullet reports; the author/LEAD stays out of the code to save context.
- **Primary source for [verify] anchors = the workspace's own assets.** The workspace contains a **fully decompiled game code tree** plus a couple of good reference mods — use these (decompile + mod patterns) as the primary source for resolving every `[verify]` anchor below. Prefer real source over guessing.
- **Concrete resource paths (used by the Phase-1 spike).**
  - Decompiled game: `E:\DEV\PhoenixPoint\decompiled\AssemblyCSharp\Assembly-CSharp\src\` (namespaces = folders).
  - Decompiled TFTV/Officer: `decompiled\TFTV\`, `decompiled\Officer\`.
  - **REAL mod source (prefer over decompiled):** `refs\TFTV-src\TFTV\` — TFTV files are **FLAT** here (e.g. `MainSpecModification.cs`, `PersonalSpecModification.cs`, `TFTVMarketplace\TFTVMercenaries.cs`, `TFTVMarketplace\TFTVMarketPlaceUI.cs`, `TFTVMarketplace\Various.cs`); `refs\Officer-src\Officer\`.

## Goal

- Extend the existing read-only perk wiki beyond the ability-progression screen onto three more surfaces, turning Oracle into a broader in-game "soldier wiki":
  - **Subclass preview (A)** — on the subclass-selection screen (when leveling a soldier), see each subclass's guaranteed perks before committing; rolled cells stay highlighted with the existing candidate drill-down. Unresearched classes the game omits are shown **greyed** and stay clickable for preview. **[SHIPPED — gesture diverged: greyed = LEFT-click banner; available = LEFT-click → native yes/no confirm with the perk row injected on top; RMB is the modal cancel. See the "AS SHIPPED" block in Feature A.]**
  - **Recruit preview (B)** — on hiring/recruitment surfaces (haven recruits, base personnel), click a candidate soldier to open the **full character page** like the squad-management view, rendered **read-only** by stripping the mutation UI: only the 3D model, the progression panel, and the stat panels remain (no inventory/equipment/customization controls). The existing perk highlight + candidate wiki ride along on its progression screen.
  - **Merc gimmick in description (C)** — for unique mercenaries in the merc shop, append their bespoke gimmick to the merc's existing **merc-shop description text** so the hook reads inline where the player already looks (no new UI). *(There is no vanilla soldier-biography prose surface; the merc-shop item description is the seam.)*
- Keep the mod's identity intact: **read-only, additive, fail-safe, TFTV-optional, class-mod-compatible**. No new perks created, no game balance changed.

## Design principles (carried over from the current mod)

- **Native components, surgically modified — never bespoke UI.** Every surface reuses the game's own UI elements: clone live native elements and/or Harmony-patch them, the way the current wiki clones native ability cells (`WikiIconFactory.MakeNative`) and the highlight rides a postfix on the cell's own populate seam. **[confirmed]** Do **not** hand-build UGUI panels from scratch where a native element can be cloned/patched. Keeps look, fonts, tooltips, and localization identical to the game and resilient to its updates.
- **One mod, phased updates.** A, B, C all ship inside Oracle itself (no separate mods), as successive updates. This document is the **road map**, not an approved build.
- **Reuse the existing wiki, don't rebuild it.** All features render through the same `PerkWikiPanel.Open(Canvas, List<TacticalAbilityDef>, PerkSwapContext)` overlay. **[confirmed]** `src/PerkWikiPanel.cs`.
- **View-only by default.** Pass `swapContext = null` so the panel is pure display — no swap affordance, no behavior. **[confirmed]** `PerkWikiPanel.Open` already accepts a null `swapContext`.
- **Data-driven, not hard-coded.** Enumerate classes/perks from `DefRepository` so the features automatically pick up TFTV and class-adding mods, exactly like `TftvConfigBridge.GetVanillaPersonalPool()` does today. **[confirmed]** `src/TftvConfigBridge.cs`.
- **Pure logic stays in testable cores.** New selection/ordering logic mirrors `PerkPoolResolver.OrderAndResolve<T>` (no Unity/TFTV/Harmony refs, unit-tested with fakes). **[confirmed]** `src/PerkPoolResolver.cs`.
- **Never break the host screen.** Every Harmony body wrapped in try/catch + `Debug.Log("[Oracle] …")`, matching the current patches. **[confirmed]** `src/AbilityTrackSkillEntryElementPatches.cs`.
- **Localize via CSV.** New titles/labels go through the existing 8-language CSV + I2 terms (`Loc.Get(term, fallback)`); no new loading code. **[confirmed]** `OracleMain.LoadLocalization`, `PerkWikiPanel.TitleTerm`.

---

## Feature A — Subclass selection perk preview (+ show unresearched classes greyed)

> **STATUS: SHIPPED (HEAD 6e4678f).** The implemented gesture/flow **diverged from the original
> right-click-banner design below**. The block immediately under this note is the authoritative
> account of the shipped behavior; the older subsections are retained for design history but are
> superseded where they conflict.

### What the player sees (AS SHIPPED)

- When leveling a soldier the player adds a **subclass**. Feature A makes each subclass's guaranteed
  perks visible **before** committing, and surfaces the classes the game hides:
- **GREYED (locked / unresearched) subclasses → LEFT-click preview banner.** Subclasses the picker
  omits (not yet researched) are injected as **greyed, non-selectable** clones. Their native `Button`
  is disabled, so a left-click has no native action — we use it to open the floating "CLASS PERKS"
  `PerkWikiPanel` banner of that subclass's guaranteed perks (native icons + tooltips). Left-click again
  toggles the banner closed.
- **AVAILABLE (selectable) subclasses → LEFT-click opens a NATIVE yes/no confirm.** A native
  `MessageBox` Yes/No prompt ("Take {0} as a subclass?") is raised, with the subclass's **perk-icon row
  injected on top of the dialog** (the icons read as part of the same window, not a separate banner). YES
  re-runs the native selection (the modal confirms/closes); NO/cancel just dismisses the box and stays in
  the picker — no subclass selected.
- **Why LEFT-click for both, not right-click:** inside this modal **RMB is the geoscape cancel** — it is
  routed through `UIStateGeoModal.OnCancel` and is **never** delivered as a UGUI pointer-click (confirmed
  by runtime diag: only `button=Left` ever arrives). So right-click could not be the preview gesture; the
  greyed clones use left-click (they have no native left action), and available buttons keep their native
  left-click "select" but gate it through the confirm dialog.
- **Two-stage cancel for the greyed-banner.** While the floating banner is open, the **first** cancel
  (RMB/Esc) closes only the banner and keeps the picker open; a **second** cancel exits the picker. An
  `ExitState` postfix is the orphan fail-safe — any still-open banner is torn down whenever the modal
  closes by any route.
- **Authoritative subclass universe.** The full set of player second-classes is
  `GeoFactionDef.InitialSpecializationDefs` ∪ every `ClassResearchRewardDef.SpecializationDef` (the exact
  set the game ever adds via `GeoFaction.AddSpecialization`). Greyed = that universe − already-shown −
  already-unlocked (`GeoFaction.AvailableCharacterSpecializations`), minus `NotSecondClassSpecialization`
  / no-track / no-proficiency entries. This **replaced** the earlier `GetAllDefs<SpecializationDef>` −
  available heuristic, which over-filtered real classes and duplicated base classes.
- **Logging.** All `[Oracle]` diagnostics now route through `OracleLog.Debug`, gated by the
  default-OFF `EnableDebugLogging` mod-config toggle — the shipped mod is silent unless the user opts in.
- Informational/confirm only — preview grants nothing; the confirm just gates the native selection.

### Why this is a natural fit

- Class-row perks are **always `Fixed`** and fully deterministic — `PerkClassification.Classify` already treats any non-`Personal` track source as `Fixed`. **[confirmed]** So a subclass's guaranteed perks are just its ordered class ability track; no randomness to resolve.
- The existing wiki already renders a `List<TacticalAbilityDef>` and already does the rolled-cell highlight + candidate banner; the new work is (1) producing the guaranteed-perk list from a class def and (2) injecting greyed entries for omitted classes.

### New pieces

- **`ClassPerkResolver` (pure core, new).**
  - Input: a specialization/class def. Output: ordered, de-duplicated `List<TacticalAbilityDef>` of its class-track abilities (level order).
  - Same shape as `PerkPoolResolver` — inject a `resolve`/`order` source so it is unit-testable with fakes, no Unity dependency.
- **Inject greyed entries for omitted classes.**
  - Enumerate all subclasses from `DefRepository` (data-driven → TFTV/class mods included); add the ones the screen omitted as **greyed, non-selectable** entries.
  - Build these greyed entries by cloning the screen's native subclass element and tinting it grey (native-components principle) — do not invent a new widget.
- **UI hook (AS SHIPPED — differs from the right-click design here).**
  - **Greyed clones:** `SubclassWikiClickHandler` (an `IPointerClickHandler` on the clone) opens the
    floating "CLASS PERKS" `PerkWikiPanel` banner on **LEFT-click** (the clone has no native left action;
    RMB never reaches a pointer-click inside this modal). Left-click again toggles it closed.
  - **Available subclasses:** `SelectSpecializationConfirmPatch` (prefix on
    `SelectSpecializationDataBind.SelectSpecializationElement`) intercepts the native select on the first
    click and raises a native `MessageBox.ShowSimplePrompt` Yes/No; `SubclassConfirmPopupDecorator`
    (postfix on `MessageBoxPromptController.Show`) injects the perk-icon row into the dialog. YES re-enters
    the native selection through a one-shot guard.
  - **Cancel:** `SelectSpecializationCancelPatch` gives the floating banner a two-stage cancel (first
    cancel closes the banner, second exits the picker) plus an `ExitState` orphan fail-safe.
  - The rolled-cell highlight + candidate banner come for free from the existing patches wherever the screen renders progression cells.
- **Panel title term.** New I2 term, e.g. `ORACLE_WIKI_TITLE_CLASS` ("CLASS PERKS"), mirroring `ORACLE_WIKI_TITLE`.

### Resolved anchors (**[verified — spike]**)

- **Class-def type + class-track accessor.** `SpecializationDef.AbilityTrack` (an `AbilityTrackDef`); ordered class perks via `AbilityTrackDef.AbilitiesByLevel[]` (each `AbilityTrackSlot.Ability` is a `TacticalAbilityDef`); convenience helper `SpecializationDef.GetAbilitiesTillLevel(int)`. @ `PhoenixPoint.Common.Entities\SpecializationDef.cs`, `PhoenixPoint.Common.Entities.Characters\AbilityTrackDef.cs`. **[conf:H]** vanilla✓ / TFTV✓ — TFTV `MainSpecModification.GenerateMainSpec` rewrites the SAME `AbilityTrackDef.AbilitiesByLevel` in place, so the accessor is identical. (Current code only reads the **Personal** track via `AbilityTrack.GetAbilityLevel` / `CharacterProgressionData.PersonalTrackTags`; the class-track accessor above is the new read.)
- **Subclass-selection UI.** Modal `ModalType.DualClassPicker`; controller `SelectSpecializationDataBind` (populate seam `ModalShowHandler`); per-subclass element `SpecializationOptionElementController` (carries `.SpecializationDef`, fields `ClassIcon` / `ClassTitleLabel` / `ClassDescriptionLabel`); opened from `UIStateEditSoldier.OnSelectSecondaryClass`. The right-click hook + greyed-clone attach to `SpecializationOptionElementController`. @ `PhoenixPoint.Geoscape.View.ViewControllers.Modal\SelectSpecializationDataBind.cs`, `PhoenixPoint.Geoscape.View.ViewControllers\SpecializationOptionElementController.cs`, `...ViewStates\UIStateEditSoldier.cs:605`. **[conf:H]**
- **TFTV coverage CONFIRMED:** TFTV does **not** replace/patch the picker modal or its controllers — Feature A targets the **vanilla** types unchanged. The only TFTV touch is a transpiler on `UIStateEditSoldier.OnSelectSecondaryClass` (`refs\TFTV-src\TFTV\TFTVMarketplace\Various.cs:214`, helper `RemoveTech` :237/:250) that **trims** the input `List<SpecializationDef>` (removes Technician for the Slug-class). **Caveat:** Feature A clones whatever the modal actually displays, so it sees the **post-filter** set — do **not** assume raw `AvailableCharacterSpecializations` equals what's shown.
- **Full set vs omitted (greyed) — AS SHIPPED.** ~~Full = `DefRepository.GetAllDefs<SpecializationDef>()`~~ The shipped universe is the **authoritative** player-second-class set: `GeoFactionDef.InitialSpecializationDefs` ∪ every `ClassResearchRewardDef.SpecializationDef` (the exact set the game adds via `GeoFaction.AddSpecialization`). Greyed = that universe − already-shown − already-unlocked (`GeoFaction.AvailableCharacterSpecializations`), minus `NotSecondClassSpecialization` / no-`AbilityTrack` / no-proficiency entries (mirrors `UIStateEditSoldier.cs:608` + the `InitSpecialization` requirements). The old `GetAllDefs − available` heuristic was dropped because it over-filtered real classes and pulled in non-player specs (Raider/Mutoid/Scum/Slug). See `ClassPerkProvider.GetSelectableSubclassUniverse` / `GetOmittedSubclasses`. @ `PhoenixPoint.Geoscape.Levels\GeoFaction.cs`, `...Research.Reward\ClassResearchRewardDef.cs`. **[shipped]**
- **WARNING — wrong screen:** `SpecializationSelectorController` / `SpecializationTileController` (`...BaseRecruits\`) is a **different** screen (mutoid/class purchase), **NOT** the level-up subclass picker — do not target it for Feature A.

---

## Feature B — Recruit full character preview (read-only)

### What the player sees

- Works on **all three hiring sources**: (1) the **global map** (third-party / haven recruiting), (2) **in-base** recruiting, and (3) the **merc shop**.
- On any of them, clicking a candidate soldier opens the **full character page** — 3D model, all panels, stats, equipment, ability progression — **exactly like the squad-management/roster view**, before committing to hire.
- The page is **read-only by construction**: the inventory/equipment, customization, and drag-drop UI is simply **not present** on the cloned screen — only the (non-interactive) 3D model, the perk/ability-progression panel, and the stat panels remain — so there is nothing to mutate.
- **Perk preview rides along for free.** That full character page already contains the ability-progression screen, where Oracle's existing rolled-perk highlight **and** right-click candidate wiki already work. So opening the page automatically gives the recruit's perk preview — no separate popup needed for the perk goal.

### Why this is a natural fit

- The perk-preview goal is satisfied by the **existing** features the moment the candidate is shown on the progression screen — the highlight patch (`SetSkillStatePatch`) and the wiki trigger fire on any `AbilityTrackSkillEntryElement`, regardless of whether the soldier is hired. **[confirmed]** `src/AbilityTrackSkillEntryElementPatches.cs`, `src/WikiAbilityTooltipTrigger.cs`.
- The class/personal-pool resolvers (`ClassPerkResolver` from Feature A, `TftvConfigBridge` pools) remain available if a candidate slot is unrolled. **[confirmed for personal pool]**

### Approach

- **GOAL: route the recruit into the full character/management view, rendered read-only.**
  - **Base screen RESOLVED (spike):** build on **`UIStateEditSoldier` (the full management view) + strip the mutation UI** — the perk-highlight/wiki ride along on its progression ability-track cells (the whole point of the feature). The mutation-free `UIStateGeoCharacterStatus` path is **rejected** as the base because it lacks the progression ability-track cells (highlight/wiki would not ride along); keep it only as a footnote alternative. **[verified — spike]**
  - Open the same character screen the roster uses (`UIStateEditSoldier`), populated from the candidate soldier (not yet in the roster).
  - **Read-only enforcement = hide/strip the mutation UI, not block it seam-by-seam.** On the cloned character screen, remove/disable the inventory & equipment widgets, the equip/unequip controls, the customization/repaint buttons, and any drag-drop affordances. Leave **only**: the character doll / 3D model (non-interactive, non-clickable), the perk/ability-progression panel, and the stat panels. Guiding principle: *everything is already native — we just need to surface it, not block it seam-by-seam.* The single source of truth is **"what to hide on the cloned screen"**, not a process-wide preview flag.
- **Plan B (emergency fallback only):** the lightweight perk-wiki popup from the original proposal (class perks + personal pool in one `PerkWikiPanel`) — used only if the full screen proves unworkable.

### New pieces

- **Recruit → character-screen launch (new).** Hook the candidate-card click on each of the three sources (global-map recruiting, in-base recruiting, merc shop) and open the character/management view for that candidate in preview mode. Route all three through one shared launcher.
- **Mutation-UI strip (new).** A single curated list of "what to hide on the cloned screen" — the inventory & equipment widgets, equip/unequip controls, customization/repaint buttons, and drag-drop affordances — removed/disabled when the preview screen is built. No process-wide flag and no per-handler no-op: if the mutating UI is not on screen, there is nothing to accidentally trigger. The list of stripped elements is the single source of truth.
- **Teardown.** Closing the preview restores normal state and disposes the temp character/actor/model cleanly (no leaked roster entry, no leaked 3D actor).

### Resolved anchors (**[verified — spike]**)

- **Roll timing — RESOLVED:** recruits are **PRE-ROLLED at generation, not at hire.** `GeoFaction.GenerateRecruits()` → `GeoHaven.SpawnNewRecruit`; `GeoUnitDescriptor.ProgressionDescriptor.PersonalAbilities` is a `[SerializeMember]` `Dictionary<int,TacticalAbilityDef>`; track built via `GeoUnitDescriptor.GetPersonalAbilityTrack()` (`AbilityTrack.CreateFromDictionary`). @ `PhoenixPoint.Geoscape.Levels\GeoFaction.cs:1555`, `PhoenixPoint.Geoscape.Entities\GeoUnitDescriptor.cs:410,107-126`. **[conf:H]** vanilla✓ / TFTV✓ — TFTV `TFTVHavenRecruitsGenerationAdjustments` only tweaks population, keeps the spawn-time roll. ⇒ the progression screen shows **exact** perks; pool-fallback rarely needed.
- **Three recruit surfaces + per-candidate hook.**
  1. **In-base:** `UIStateRosterRecruits` + `UIModuleRecruitsList` + row `RecruitsListElementController` (select handler `UIStateRosterRecruits.OnRecruitEntrySelected` → `_actorCycleModule.SelectSoldier(recruit.Recruit)`). @ `...BaseRecruits\RecruitsListElementController.cs`, `...ViewStates\UIStateRosterRecruits.cs:284`.
  2. **Global-map haven:** `HavenInteractionController` (single `GeoHaven.AvailableRecruit`, `PurchaseRecruitButton` / `OnRecruitSoldier`). @ `...HavenDetails\HavenInteractionController.cs:21-167,255`.
  3. **Merc shop:** `UIModuleTheMarketplace`.
  - **[conf:H]** All three route into ONE shared launcher → open `UIStateEditSoldier` in preview for that `GeoUnitDescriptor`.
- **Char view for a NON-roster soldier.** `UIStateEditSoldier` accepts a `GeoUnitDescriptor` but assumes roster/faction context, so the launcher must **supply that context** (implementation detail to confirm in build). 3D actor is native: `UIModuleActorCycle.DisplaySoldier(GeoUnitDescriptor,…)` (builds via `_charBuilder` + `CommonCharacterUtils.DisplayCharacter` / `RebuildCharacter`) — the recruit screen already renders the candidate this way; lifecycle native, no leaked roster entry. @ `PhoenixPoint.Common.View.ViewModules\UIModuleActorCycle.cs:597-700`. **[conf:H]**
- **Mutation-UI strip list** **[conf:M — each needs in-game confirmation]:**
  - **HIDE:** `UIModuleSoldierEquip` entirely (inventory/equip lists, drag-drop, `ScrapDialog`, manufacture); `EditUnitButtonsController.DismissButton` + augmentation/context buttons; set `_actorCycleModule.RenameEnabled = false`; suppress ability-BUY / stat-spend / dual-class-pick affordances in `UIModuleCharacterProgression` (keep DISPLAY).
  - **KEEP:** `UIModuleActorCycle` (3D model, non-interactive) + `UIModuleCharacterProgression` ability tracks (display) + stat panels.
  - @ `PhoenixPoint.Common.View.ViewModules\{UIModuleSoldierEquip,UIModuleActorCycle,EditUnitButtonsController,UIModuleCharacterProgression}.cs`, wiring in `UIStateEditSoldier.cs:115-185`.

---

## Feature C — Append merc gimmick to the merc-shop description text

> **IMPORTANT REFRAME (spike):** there is **NO vanilla soldier biography/backstory prose surface** in Assembly-CSharp. The merc gimmick surface is the **merc-shop item description**, **not** a recruit/edit "biography". Feature C appends the gimmick to the **merc-shop description text**.

### What the player sees

- Unique mercenaries (and other special soldiers) have a bespoke gimmick that defines them. Example: a half-robot engineer whose back-mounted mech-arms heal allies at the cost of **his own HP** instead of consumables.
- In the **merc shop**, each merc already gets a **text description** when offered for hire. Feature C simply **appends the gimmick trait to that existing merc-shop description text** — so the special trait reads inline, where the player already looks. No new UI panel, no banner.
- Surgical and minimal: extend the native merc-shop description string the game already renders.

### Data source (hybrid — the game has no "signature" flag)

- A and B are **pure read-out**: the game forms the perks/pools, the mod only reads and displays them. C is different — the game does **not** tag any ability as "this is the merc's gimmick", so there is nothing native that *labels* it.
- **Hybrid source, in preference order:**
  1. **Prefer the native ability description** via a curated `characterDefId -> abilityId` map — pull the game's own localized ability description for that gimmick. Auto-localized, zero translation. Used **where the gimmick is expressible as one ability**.
  2. **Fall back to an authored CSV blurb** (`ORACLE_GIMMICK_<key>`, 8 languages) **only when** the gimmick is not expressible as a single ability description.
- The curated map/blurbs are maintained per known unique merc and live in the mod.
- Optional heuristic auto-detect (ability not in class track nor personal pool) can flag unlisted mercs, but it cannot pick the right ability or author a blurb, so the curated mapping stays primary.
- **The gimmick catalogue itself is a research task.** *Which merc has which gimmick* (and its `abilityId` or authored line) is to be sourced — during the Feature-C phase — from in-game data, the internet / community wikis, **and** the reference mods already bundled in this workspace.

### New pieces

- **Gimmick registry (curated, mod-owned).** Small data table keyed by `characterDefId`, holding **either** an `abilityId` (preferred — sources the game's own localized description) **or** an authored CSV-blurb key when no single ability captures the gimmick. Primary source of truth; easy to extend. Populated from the Feature-C catalogue research (in-game data + community wikis + bundled reference mods).
- **Description-append hook — native, surgical.** Harmony-POSTFIX the merc-shop description seam (`UIModuleTheMarketplace.SetupChoiceInfoBlock`): when the displayed merc is in the registry, append the gimmick line to the native `ResearchInfo.Description`. Modifies the game's own text element, builds nothing new.
- **Localization.** Preferred path needs **no** new translation — the appended line is the game's own localized ability description, pulled via the mapped `abilityId`. Only the fallback blurbs need authored per-language rows in the existing 8-language CSV (`ORACLE_GIMMICK_<key>`). Optional small prefix label term (e.g. `ORACLE_GIMMICK_PREFIX` → "Special: ").

### Resolved anchors (**[verified — spike]**)

- **Description seam.** `UIModuleTheMarketplace.SetupChoiceInfoBlock(GeoEventChoice)` writes the merc bio into `ResearchInfo` (`.Title` / `.Description`) from `tacCharacterDef.Data.ViewElementDef.Description.LocalizationKey`. **Plan:** Harmony-**POSTFIX** `SetupChoiceInfoBlock`, append the gimmick line to `ResearchInfo.Description`. @ vanilla `PhoenixPoint.Geoscape.View.ViewModules\UIModuleTheMarketplace.cs:258` (`ResearchInfo` ~L75). TFTV proves the seam: `refs\TFTV-src\TFTV\TFTVMarketplace\TFTVMarketPlaceUI.cs:76,92` patches the same method/field. **[conf:H]** TFTV✓.
- **Merc keying.** `GeoUnitDescriptor.UnitType.TemplateDef` = a `TacCharacterDef`; key the registry by def name/GUID (e.g. `"Mercenary_Ghost"`) and/or the `Mercenary` GameTag (GUID `{49BDADBC-A411-48B2-8773-533EE9247F4C}`). @ `GeoUnitDescriptor.cs:34-86,177`; TFTV merc defs `refs\TFTV-src\TFTV\TFTVMarketplace\TFTVMercenaries.cs:142`. **[conf:H]** vanilla DLC mercs + TFTV uniques both = `TacCharacterDef`.
- **abilityId → native-description path VIABLE [conf:H].** Pull `ability.ViewElementDef.GetInterpolatedDescription(ability)` (pattern from `UIStateGeoCharacterStatus.GetAbilityData`). Example: Slug (half-robot engineer) → `slugMechArms` + `SlugTechnicianRepair` / `SlugTechnicianHeal`, desc key `SLUG_KEY_MECH_ARMS_DESCRIPTION`. Seeded TFTV unique mercs (`Mercenary` tag): Ghost (priest), Doom/Heavy, Slug (technician), SpyMaster (infiltrator), Sectarian (berserker), Exile (assault), each + a `_Vet` lvl-5 variant; defs/loc keys @ `refs\...\TFTVMarketplace\TFTVMercenaries.cs:553,592-630,648-748,761-800`.
- **Deferred to build phase (not a blocker):** the **full gimmick catalogue** — which merc → which `abilityId`/blurb (incl. vanilla DLC mercs + community-wiki cases). A Feature-C build-phase research task, not a spike anchor.

---

## Shared infrastructure changes

- **`PerkWikiPanel` native template on new screens (Feature A).** The panel prefers cloning a live native `AbilityTrackSkillEntryElement` and falls back to `WikiIconFactory.Make` custom icons only when none is present. **[confirmed]** Per the native-components principle, find a native ability cell to clone on the subclass screen (or pull one from a progression module) rather than defaulting to custom icons — the custom path is a last-resort fallback, not the goal. Feature B's full character page keeps the real progression cells, so the native-clone wiki works there unchanged.
- **Mutation-UI strip list (Feature B).** A single curated list of "what to hide on the cloned character screen" (inventory/equipment widgets, equip/unequip controls, customization/repaint buttons, drag-drop affordances). Not a process-wide flag — the preview screen is built read-only by *omitting* the mutating UI, so there is nothing to guard. The existing swap path is simply absent on the preview screen for the same reason. The strip list is the one source of truth.
- **Open-affordance convention.** Standardize on right-click-to-open for the subclass wiki (matches the existing progression screen). The recruit surface instead opens the full character page on click (Feature B), where the existing right-click perk wiki then applies inside it.
- **Localization.** Add the new title terms to `Assets/Localization/Oracle_Localization.csv` across all 8 languages (english, russian, german, french, spanish, italian, polish, schinese), imported via the existing `Import_CSV`/`AddNewTerms` path. **[confirmed]**

## Confirmed reusable anchors (this repo, today)

- `PerkWikiPanel.Open(Canvas canvas, List<TacticalAbilityDef> defs, PerkSwapContext swapContext = null)` — `src/PerkWikiPanel.cs`. View-only when `swapContext == null`.
- `PerkPoolResolver.OrderAndResolve<T>(rawNames, className, isExcluded, resolve)` — `src/PerkPoolResolver.cs`. Pure, generic, unit-tested.
- `TftvConfigBridge.TryGetTftvRandomPool(int level0, string className, out List<TacticalAbilityDef>)` and `GetVanillaPersonalPool()` — `src/TftvConfigBridge.cs`.
- `PerkClassification.Classify(...)` — non-`Personal` track sources classify as `Fixed` (class perks are deterministic) — `src/PerkClassification.cs`.
- `WikiIconFactory.Make(...)` (custom icon) / `MakeNative(...)` (native cell clone) — `src/WikiIconFactory.cs`.
- Localization: `Loc.Get(term, fallback)` + CSV import in `OracleMain.LoadLocalization` — `src/OracleMain.cs`, `src/Localization.cs`.
- Harmony postfix-on-UI-seam pattern with full try/catch guards — `src/AbilityTrackSkillEntryElementPatches.cs`.

## Open questions for the author

- **None remaining as blockers.** The Phase-1 spike resolved every prior open item: roll timing = **pre-rolled at generation** (Feature B), Feature B base screen = **`UIStateEditSoldier` + strip mutation UI**, Feature C surface = **merc-shop description** (no vanilla biography surface exists).
- **Deferred (build-phase, not a blocker):** the **full merc gimmick catalogue** (which merc → `abilityId`/authored blurb) — sourced during the Feature-C build phase from in-game data, community wikis, and the bundled reference mods.
- **Low-confidence items to confirm in-game during the build (flagged, not blockers):**
  1. Feature-B **mutation-strip list is [conf:M]** — confirm each hidden element doesn't break the screen in-game.
  2. Confirm `UIStateEditSoldier` can be opened for a **non-roster** `GeoUnitDescriptor` with a supplied faction/roster context.

> **Resolved:** all three features ship inside the **one** Oracle mod as phased updates (no separate mods). This doc is the road map.
> **Resolved (Feature A):** unresearched classes the game omits **are** injected into the list, shown **greyed/inactive**, and stay right-clickable for perk preview. No need to explain *why* a class is greyed — grey just means not yet available.
> **Resolved (Feature B — scope):** the GOAL is the **full character page, read-only** (3D model + progression + stat panels), built on **`UIStateEditSoldier` + strip the mutation UI** (the mutation-free `UIStateGeoCharacterStatus` is rejected — no progression cells for highlight/wiki to ride). The lightweight perk-wiki popup is demoted to **Plan B, emergency fallback only**, not a co-equal option.
> **Resolved (Feature B — roll timing):** recruits are **pre-rolled at generation** (`GeoFaction.GenerateRecruits` → `GeoHaven.SpawnNewRecruit`, `GeoUnitDescriptor.ProgressionDescriptor.PersonalAbilities`) — the progression screen shows **exact** perks; pool-fallback rarely needed.
> **Resolved (Feature B — read-only mechanism):** read-only is achieved by **hiding/stripping the mutation UI** on the cloned screen (inventory/equipment widgets, equip/unequip controls, customization/repaint buttons, drag-drop), **not** a process-wide preview flag or per-seam mutation guards. *Everything is already native — we just surface it, not block it seam-by-seam.* This also covers the old "read-only strictness" question: there is no mutating UI present, so nothing to block selectively.
> **Resolved (Feature C — surface):** there is **no vanilla soldier biography prose surface**; the gimmick is appended to the **merc-shop description** via a POSTFIX on `UIModuleTheMarketplace.SetupChoiceInfoBlock` (writes `ResearchInfo.Description`). Mercs keyed by `TacCharacterDef` (def name/GUID and/or `Mercenary` GameTag).
> **Resolved (Feature C — gimmick source):** **hybrid** — prefer the native game ability description (`ability.ViewElementDef.GetInterpolatedDescription(ability)`) via a curated `characterDefId -> abilityId` map (auto-localized, zero translation) where the gimmick is one ability; fall back to an authored CSV blurb (`ORACLE_GIMMICK_<key>`, 8 languages) only when it is not. The gimmick **catalogue** (which merc has which gimmick) is a build-phase research task for the Feature-C phase, sourced from in-game data, community wikis, and the bundled reference mods.

## Suggested phasing

- **Phase 1 — investigation spike. ✅ DONE.** Resolved every **[verify]** anchor (class-track accessor, full subclass enumeration + which the screen omits, recruit + merc-shop UI controllers, recruit roll timing, character-view-for-non-roster-soldier, the mutation-UI elements to strip on the cloned screen, merc-shop description seam, merc keying). All anchors above are now **[verified — spike]** with concrete type/path/symbol grounding; only the full per-merc gimmick catalogue is deferred to the Feature-C build phase. **Each remaining phase (A, C, B) gets its own implementation plan + spec as needed** before code.
- **Phase 2 — Feature A (subclass preview).** Build `ClassPerkResolver` + right-click hook on the subclass screen + greyed-entry injection for omitted classes. Class perks are deterministic and the rolled-cell drill-down already exists, so this is low risk; the only new UI work is the greyed clones.
- **Phase 3 — Feature C (merc gimmick in merc-shop description).** Smallest surface — one curated table + one Harmony POSTFIX on `UIModuleTheMarketplace.SetupChoiceInfoBlock` appending to the native `ResearchInfo.Description`. No new UI. Lowest risk after A.
- **Phase 4 — Feature B (recruit full-page preview).** Highest risk (opening a full management view for a non-roster soldier + correctly stripping the mutation UI to leave a read-only screen + 3D actor lifecycle). Do last, after the others prove the surrounding pieces. The perk highlight + wiki ride along inside it for free.

## Out of scope (this proposal)

- Any change to perks, classes, abilities, or balance — strictly read-only preview/highlight.
- Perk-swap or any mutation on the new surfaces (the existing swap stays confined to the post-hire progression screen; on the recruit preview its UI is simply not present, since the mutation UI is stripped from the cloned screen).
- Steam Workshop republish and in-game verification (manual, by the author, per the standard autonomy boundary in `docs/specs/2026-06-06-research-gated-perk-swap.md`).
