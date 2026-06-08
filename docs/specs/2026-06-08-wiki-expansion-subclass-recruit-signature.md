# PerkOracle — Wiki expansion: subclass preview, recruit full-page preview & merc gimmick in biography

Date: 2026-06-08 · Status: **road map — key design decisions resolved; Phase 1 spike next** (not yet scheduled) · Mod: PerkOracle (standalone, ns `Morgott.PerkOracle`, Dependencies `[]`)

> Road map for future updates, all shipping inside the **one** PerkOracle mod. Grounds three new surfaces in the mod's existing architecture, built strictly from **native game components, surgically modified** (clone/Harmony-patch native elements — no bespoke UI). API anchors taken from the current codebase are marked **[confirmed]**; anchors still to be located in the game/TFTV decompile before implementation are marked **[verify]**. Nothing here is locked — a starting point for the author to review.

## Process / resources

- **All work is delegated to sub-agents.** Investigation, decompile research, and implementation are performed by dispatched sub-agents returning compressed bullet reports; the author/LEAD stays out of the code to save context.
- **Primary source for [verify] anchors = the workspace's own assets.** The workspace contains a **fully decompiled game code tree** plus a couple of good reference mods — use these (decompile + mod patterns) as the primary source for resolving every `[verify]` anchor below. Prefer real source over guessing.

## Goal

- Extend the existing read-only perk wiki beyond the ability-progression screen onto three more surfaces, turning PerkOracle into a broader in-game "soldier wiki":
  - **Subclass preview (A)** — on the subclass-selection screen (when leveling a soldier), right-click any subclass to see its guaranteed perks; rolled cells stay highlighted with the existing candidate drill-down. Unresearched classes the game omits are shown **greyed** and stay clickable for preview.
  - **Recruit preview (B)** — on hiring/recruitment surfaces (haven recruits, base personnel), click a candidate soldier to open the **full character page** like the squad-management view, rendered **read-only** by stripping the mutation UI: only the 3D model, the progression panel, and the stat panels remain (no inventory/equipment/customization controls). The existing perk highlight + candidate wiki ride along on its progression screen.
  - **Merc gimmick in bio (C)** — for unique mercenaries in the merc shop, append their bespoke gimmick to the soldier's existing **text biography** so the hook reads inline where the player already looks (no new UI).
- Keep the mod's identity intact: **read-only, additive, fail-safe, TFTV-optional, class-mod-compatible**. No new perks created, no game balance changed.

## Design principles (carried over from the current mod)

- **Native components, surgically modified — never bespoke UI.** Every surface reuses the game's own UI elements: clone live native elements and/or Harmony-patch them, the way the current wiki clones native ability cells (`WikiIconFactory.MakeNative`) and the highlight rides a postfix on the cell's own populate seam. **[confirmed]** Do **not** hand-build UGUI panels from scratch where a native element can be cloned/patched. Keeps look, fonts, tooltips, and localization identical to the game and resilient to its updates.
- **One mod, phased updates.** A, B, C all ship inside PerkOracle itself (no separate mods), as successive updates. This document is the **road map**, not an approved build.
- **Reuse the existing wiki, don't rebuild it.** All features render through the same `PerkWikiPanel.Open(Canvas, List<TacticalAbilityDef>, PerkSwapContext)` overlay. **[confirmed]** `src/PerkWikiPanel.cs`.
- **View-only by default.** Pass `swapContext = null` so the panel is pure display — no swap affordance, no behavior. **[confirmed]** `PerkWikiPanel.Open` already accepts a null `swapContext`.
- **Data-driven, not hard-coded.** Enumerate classes/perks from `DefRepository` so the features automatically pick up TFTV and class-adding mods, exactly like `TftvConfigBridge.GetVanillaPersonalPool()` does today. **[confirmed]** `src/TftvConfigBridge.cs`.
- **Pure logic stays in testable cores.** New selection/ordering logic mirrors `PerkPoolResolver.OrderAndResolve<T>` (no Unity/TFTV/Harmony refs, unit-tested with fakes). **[confirmed]** `src/PerkPoolResolver.cs`.
- **Never break the host screen.** Every Harmony body wrapped in try/catch + `Debug.Log("[PerkOracle] …")`, matching the current patches. **[confirmed]** `src/AbilityTrackSkillEntryElementPatches.cs`.
- **Localize via CSV.** New titles/labels go through the existing 8-language CSV + I2 terms (`Loc.Get(term, fallback)`); no new loading code. **[confirmed]** `PerkOracleMain.LoadLocalization`, `PerkWikiPanel.TitleTerm`.

---

## Feature A — Subclass selection perk preview (+ show unresearched classes greyed)

### What the player sees

- When leveling a soldier the player can add a **subclass** (subclasses are just regular classes). On the subclass-selection screen it is unclear what perks each one grants. This feature makes it clear:
- **Right-click a subclass → banner of its guaranteed perks.** A wiki banner listing the fixed class-progression abilities that subclass grants, with the game's native icons and tooltips.
- **Rolled cells highlighted + drill-down (reuses the existing feature).** The slots where perks are randomly rolled are highlighted, and clicking one opens the candidate banner showing what could roll there — this is exactly the mod's current rolled-perk highlight + candidate wiki, applied on this screen.
- **Show unresearched classes too, greyed out.** Classes the game omits from the list because they are not researched yet are **still shown, in grey (inactive)** — and are **also right-clickable** to preview all their perks, same as available ones. (No need to explain *why* a class is greyed; grey = not yet available.)
- Informational only — grants nothing, selects nothing.

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
- **UI hook.**
  - Right-click any subclass entry (available **or** greyed) → open the guaranteed-perks banner via `PerkWikiPanel`. Reuse the right-click-to-open / right-click-to-close convention for muscle-memory consistency.
  - The rolled-cell highlight + candidate banner come for free from the existing patches wherever the screen renders progression cells.
- **Panel title term.** New I2 term, e.g. `PERKORACLE_WIKI_TITLE_CLASS` ("CLASS PERKS"), mirroring `PERKORACLE_WIKI_TITLE`.

### Grounding needed before implementation (**[verify]**)

- Specialization/class def type and the accessor for its **class ability track** (the per-level fixed abilities). The current code only reads the **Personal** track (`AbilityTrack.GetAbilityLevel`, `CharacterProgressionData.PersonalTrackTags`) — the class-track equivalent is not yet touched in this repo.
- The **subclass-selection UI controller** name + the per-subclass element/seam to (a) attach the right-click affordance and (b) clone for the greyed entries (vanilla and TFTV; TFTV's `PRMBetterClasses` reworks the class system, so the existing `TftvConfigBridge` reflection pattern is the model to follow).
- How to enumerate the **full** class/subclass set and tell which ones the screen omitted (to know which to inject as greyed).

---

## Feature B — Recruit full character preview (read-only)

### What the player sees

- Works on **all three hiring sources**: (1) the **global map** (third-party / haven recruiting), (2) **in-base** recruiting, and (3) the **merc shop**.
- On any of them, clicking a candidate soldier opens the **full character page** — 3D model, all panels, stats, equipment, ability progression — **exactly like the squad-management/roster view**, before committing to hire.
- The page is **read-only by construction**: the inventory/equipment, customization, and drag-drop UI is simply **not present** on the cloned screen — only the (non-interactive) 3D model, the perk/ability-progression panel, and the stat panels remain — so there is nothing to mutate.
- **Perk preview rides along for free.** That full character page already contains the ability-progression screen, where PerkOracle's existing rolled-perk highlight **and** right-click candidate wiki already work. So opening the page automatically gives the recruit's perk preview — no separate popup needed for the perk goal.

### Why this is a natural fit

- The perk-preview goal is satisfied by the **existing** features the moment the candidate is shown on the progression screen — the highlight patch (`SetSkillStatePatch`) and the wiki trigger fire on any `AbilityTrackSkillEntryElement`, regardless of whether the soldier is hired. **[confirmed]** `src/AbilityTrackSkillEntryElementPatches.cs`, `src/WikiAbilityTooltipTrigger.cs`.
- The class/personal-pool resolvers (`ClassPerkResolver` from Feature A, `TftvConfigBridge` pools) remain available if a candidate slot is unrolled. **[confirmed for personal pool]**

### Approach

- **GOAL: route the recruit into the full character/management view, rendered read-only.**
  - Open the same character screen the roster uses, populated from the candidate soldier (not yet in the roster).
  - **Read-only enforcement = hide/strip the mutation UI, not block it seam-by-seam.** On the cloned character screen, remove/disable the inventory & equipment widgets, the equip/unequip controls, the customization/repaint buttons, and any drag-drop affordances. Leave **only**: the character doll / 3D model (non-interactive, non-clickable), the perk/ability-progression panel, and the stat panels. Guiding principle: *everything is already native — we just need to surface it, not block it seam-by-seam.* The single source of truth is **"what to hide on the cloned screen"**, not a process-wide preview flag.
- **Plan B (emergency fallback only):** the lightweight perk-wiki popup from the original proposal (class perks + personal pool in one `PerkWikiPanel`) — used only if the full screen proves unworkable.

### New pieces

- **Recruit → character-screen launch (new).** Hook the candidate-card click on each of the three sources (global-map recruiting, in-base recruiting, merc shop) and open the character/management view for that candidate in preview mode. Route all three through one shared launcher.
- **Mutation-UI strip (new).** A single curated list of "what to hide on the cloned screen" — the inventory & equipment widgets, equip/unequip controls, customization/repaint buttons, and drag-drop affordances — removed/disabled when the preview screen is built. No process-wide flag and no per-handler no-op: if the mutating UI is not on screen, there is nothing to accidentally trigger. The list of stripped elements is the single source of truth.
- **Teardown.** Closing the preview restores normal state and disposes the temp character/actor/model cleanly (no leaked roster entry, no leaked 3D actor).

### Grounding needed before implementation (**[verify]**)

- The recruit/hire UI controller(s) + per-candidate element to hook on **all three** sources — global-map recruiting, in-base recruiting, merc shop — which are likely distinct screens needing separate hooks into one shared launcher.
- The **character/management view controller** and how to open it for a soldier that is **not in the roster** (it may assume a squad/roster/base context — a temp/sandbox setup or a cloned controller may be needed).
- **Every mutation-UI element** on that screen, so the strip list covers them all (inventory & equipment widgets, equip/unequip controls, customization/repaint buttons, drag-drop affordances) — leaving only the 3D model, progression panel, and stat panels.
- **3D model / actor instantiation** for a non-hired candidate, and its clean teardown on close.
- **Whether a recruit's personal perks are pre-rolled at list time or only at hire** **[verify — Phase-1 spike item]** — if pre-rolled, the progression screen shows exact perks; if not, unrolled slots fall back to the pool preview. This is the one remaining open design unknown; resolved during the Phase 1 investigation spike (decompile + reference mods).

---

## Feature C — Append merc gimmick to the biography text

### What the player sees

- Unique mercenaries (and other special soldiers) have a bespoke gimmick that defines them. Example: a half-robot engineer whose back-mounted mech-arms heal allies at the cost of **his own HP** instead of consumables.
- These soldiers already get a **text biography / description** at hire (and on any other description surface). Feature C simply **appends the gimmick trait to that existing biography text** — so the special trait reads inline, where the player already looks. No new UI panel, no banner.
- Surgical and minimal: extend the native biography/description string the game already renders, wherever it is shown (hire screen and any other description).

### Data source (hybrid — the game has no "signature" flag)

- A and B are **pure read-out**: the game forms the perks/pools, the mod only reads and displays them. C is different — the game does **not** tag any ability as "this is the merc's gimmick", so there is nothing native that *labels* it.
- **Hybrid source, in preference order:**
  1. **Prefer the native ability description** via a curated `characterDefId -> abilityId` map — pull the game's own localized ability description for that gimmick. Auto-localized, zero translation. Used **where the gimmick is expressible as one ability**.
  2. **Fall back to an authored CSV blurb** (`PERKORACLE_GIMMICK_<key>`, 8 languages) **only when** the gimmick is not expressible as a single ability description.
- The curated map/blurbs are maintained per known unique merc and live in the mod.
- Optional heuristic auto-detect (ability not in class track nor personal pool) can flag unlisted mercs, but it cannot pick the right ability or author a blurb, so the curated mapping stays primary.
- **The gimmick catalogue itself is a research task.** *Which merc has which gimmick* (and its `abilityId` or authored line) is to be sourced — during the Feature-C phase — from in-game data, the internet / community wikis, **and** the reference mods already bundled in this workspace.

### New pieces

- **Gimmick registry (curated, mod-owned).** Small data table keyed by `characterDefId`, holding **either** an `abilityId` (preferred — sources the game's own localized description) **or** an authored CSV-blurb key when no single ability captures the gimmick. Primary source of truth; easy to extend. Populated from the Feature-C catalogue research (in-game data + community wikis + bundled reference mods).
- **Biography-append hook — native, surgical.** Harmony-patch the biography/description text seam (hire screen + any other description surface): when the displayed soldier is in the registry, append the gimmick line to the native biography string. Modifies the game's own text element, builds nothing new.
- **Localization.** Preferred path needs **no** new translation — the appended line is the game's own localized ability description, pulled via the mapped `abilityId`. Only the fallback blurbs need authored per-language rows in the existing 8-language CSV (`PERKORACLE_GIMMICK_<key>`). Optional small prefix label term (e.g. `PERKORACLE_GIMMICK_PREFIX` → "Special: ").

### Grounding needed before implementation (**[verify]**)

- The **biography/description text seam(s)** to Harmony-patch (the control/getter that produces the displayed bio string) — at hire and any other description surface where these soldiers appear.
- How unique mercs are keyed (`characterDefId` or equivalent) so the registry can match them (vanilla DLC mercs vs TFTV unique recruits vs class-mod additions).
- Which gimmick belongs to each known merc — **catalogue research task** for the registry (the mapped `abilityId`, or an authored blurb where no single ability fits). Sourced during the Feature-C phase from in-game data, community wikis, and the bundled reference mods.

---

## Shared infrastructure changes

- **`PerkWikiPanel` native template on new screens (Feature A).** The panel prefers cloning a live native `AbilityTrackSkillEntryElement` and falls back to `WikiIconFactory.Make` custom icons only when none is present. **[confirmed]** Per the native-components principle, find a native ability cell to clone on the subclass screen (or pull one from a progression module) rather than defaulting to custom icons — the custom path is a last-resort fallback, not the goal. Feature B's full character page keeps the real progression cells, so the native-clone wiki works there unchanged.
- **Mutation-UI strip list (Feature B).** A single curated list of "what to hide on the cloned character screen" (inventory/equipment widgets, equip/unequip controls, customization/repaint buttons, drag-drop affordances). Not a process-wide flag — the preview screen is built read-only by *omitting* the mutating UI, so there is nothing to guard. The existing swap path is simply absent on the preview screen for the same reason. The strip list is the one source of truth.
- **Open-affordance convention.** Standardize on right-click-to-open for the subclass wiki (matches the existing progression screen). The recruit surface instead opens the full character page on click (Feature B), where the existing right-click perk wiki then applies inside it.
- **Localization.** Add the new title terms to `Assets/Localization/PerkOracle_Localization.csv` across all 8 languages (english, russian, german, french, spanish, italian, polish, schinese), imported via the existing `Import_CSV`/`AddNewTerms` path. **[confirmed]**

## Confirmed reusable anchors (this repo, today)

- `PerkWikiPanel.Open(Canvas canvas, List<TacticalAbilityDef> defs, PerkSwapContext swapContext = null)` — `src/PerkWikiPanel.cs`. View-only when `swapContext == null`.
- `PerkPoolResolver.OrderAndResolve<T>(rawNames, className, isExcluded, resolve)` — `src/PerkPoolResolver.cs`. Pure, generic, unit-tested.
- `TftvConfigBridge.TryGetTftvRandomPool(int level0, string className, out List<TacticalAbilityDef>)` and `GetVanillaPersonalPool()` — `src/TftvConfigBridge.cs`.
- `PerkClassification.Classify(...)` — non-`Personal` track sources classify as `Fixed` (class perks are deterministic) — `src/PerkClassification.cs`.
- `WikiIconFactory.Make(...)` (custom icon) / `MakeNative(...)` (native cell clone) — `src/WikiIconFactory.cs`.
- Localization: `Loc.Get(term, fallback)` + CSV import in `PerkOracleMain.LoadLocalization` — `src/PerkOracleMain.cs`, `src/Localization.cs`.
- Harmony postfix-on-UI-seam pattern with full try/catch guards — `src/AbilityTrackSkillEntryElementPatches.cs`.

## Open questions for the author

- **Recruit personal perks:** confirm pre-rolled vs rolled-at-hire — drives exact-vs-pool preview on the progression screen (the single biggest unknown for Feature B). **The one remaining open `[verify]` item; owned by the Phase 1 spike** (decompile + reference mods).

> **Resolved:** all three features ship inside the **one** PerkOracle mod as phased updates (no separate mods). This doc is the road map.
> **Resolved (Feature A):** unresearched classes the game omits **are** injected into the list, shown **greyed/inactive**, and stay right-clickable for perk preview. No need to explain *why* a class is greyed — grey just means not yet available.
> **Resolved (Feature B — scope):** the GOAL is the **full character page, read-only** (3D model + progression + stat panels). The lightweight perk-wiki popup is demoted to **Plan B, emergency fallback only**, not a co-equal option.
> **Resolved (Feature B — read-only mechanism):** read-only is achieved by **hiding/stripping the mutation UI** on the cloned screen (inventory/equipment widgets, equip/unequip controls, customization/repaint buttons, drag-drop), **not** a process-wide preview flag or per-seam mutation guards. *Everything is already native — we just surface it, not block it seam-by-seam.* This also covers the old "read-only strictness" question: there is no mutating UI present, so nothing to block selectively.
> **Resolved (Feature C — gimmick source):** **hybrid** — prefer the native game ability description via a curated `characterDefId -> abilityId` map (auto-localized, zero translation) where the gimmick is one ability; fall back to an authored CSV blurb (`PERKORACLE_GIMMICK_<key>`, 8 languages) only when it is not. The gimmick **catalogue** (which merc has which gimmick) is a research task for the Feature-C phase, sourced from in-game data, community wikis, and the bundled reference mods.

## Suggested phasing

- **Phase 1 — investigation spike.** Resolve every **[verify]** anchor (class-track accessor, full subclass enumeration + which the screen omits, recruit + merc-shop UI controllers, recruit roll timing, character-view-for-non-roster-soldier, the mutation-UI elements to strip on the cloned screen, biography text seam, merc keying + per-merc gimmick ability/blurb catalogue). Done by dispatched sub-agents against the decompile + bundled reference mods. No feature code; output a follow-up locked spec per feature.
- **Phase 2 — Feature A (subclass preview).** Build `ClassPerkResolver` + right-click hook on the subclass screen + greyed-entry injection for omitted classes. Class perks are deterministic and the rolled-cell drill-down already exists, so this is low risk; the only new UI work is the greyed clones.
- **Phase 3 — Feature C (merc gimmick in bio).** Smallest surface — one curated table + one Harmony patch appending to the native biography string. No new UI. Lowest risk after A.
- **Phase 4 — Feature B (recruit full-page preview).** Highest risk (opening a full management view for a non-roster soldier + correctly stripping the mutation UI to leave a read-only screen + 3D actor lifecycle). Do last, after the others prove the surrounding pieces. The perk highlight + wiki ride along inside it for free.

## Out of scope (this proposal)

- Any change to perks, classes, abilities, or balance — strictly read-only preview/highlight.
- Perk-swap or any mutation on the new surfaces (the existing swap stays confined to the post-hire progression screen; on the recruit preview its UI is simply not present, since the mutation UI is stripped from the cloned screen).
- Steam Workshop republish and in-game verification (manual, by the author, per the standard autonomy boundary in `docs/specs/2026-06-06-research-gated-perk-swap.md`).
