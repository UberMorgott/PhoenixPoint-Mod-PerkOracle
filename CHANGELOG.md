# Changelog

All notable changes to Oracle are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6.0] - 2026-07-11

### Added
- **Class wiki.** Clicking a trained main-class skill cell on the ability-progression
  screen opens a popup with a tab per class, each showing that class's ability track
  and personal perk slots. Everything is resolved from live runtime data (what the game
  actually renders), so it matches TFTV and class-adding mods (e.g. Officer): fixed TFTV
  perk slots are shown as-is, while random slots are anonymized as "?" and can be clicked
  to reveal the exact per-slot candidate pool.

### Changed
- **Event outcome preview only on real choices.** The preview tooltip now appears only
  for events that offer two or more selectable answers; an event with a single inevitable
  outcome no longer shows a prediction.

### Fixed
- The item-tooltip **"DISMANTLE"** row now matches TFTV's prorated scrap refund.
- Wiki data is sourced runtime-first (from the native render path) for mod compatibility,
  with localization cleanup across all 8 languages.

## [1.5.0] - 2026-07-11

### Added
- **Per-feature settings overhaul.** Every helper now has its own on/off toggle:
  rolled-perk highlight, perk wiki, subclass-confirm preview, event outcome
  preview, and dismantle yield. The rolled-perk highlight also gets a **color
  picker** (Blue, Green, Gold, Red, Purple, White). All settings are localized in
  8 languages.
- **Dismantle yield in item tooltips.** Item tooltips on the equip/inventory,
  geoscape, tactical, and mutation screens now show a native-styled **"DISMANTLE"**
  stat row listing the resources recovered by scrapping the item — colored resource
  icons with gold digits, right-aligned and visually separated in the footer. The
  row is suppressed on manufacturing/Phoenixpedia tooltips (the game already shows
  it there).
- **Skill-point cost for perk swap.** When enabled (default), a perk swap now costs
  the soldier skill points (default 50, configurable), charged through the game's
  native progression spend flow. The cost is shown on the perk tooltip's SP row, and
  the swap is blocked with a message when the soldier can't afford it. Turn it off
  (or set the cost to 0) for free swaps.

### Changed
- The perk-swap research project ("Operative Reconditioning") now only exists while
  **Perk Swap** is enabled. Toggling the setting mid-campaign hides or reveals the
  project live, and is save-safe.

### Fixed
- Ability tooltips now render **above all windows and modal dialogs** (hard sorting
  order), so a hovered perk/ability tooltip is no longer hidden behind the subclass
  confirmation dialog.

## [1.4.0] - 2026-06-12

> ### Upgrade notes (read before updating from PerkOracle)
> Two one-time things happen when you update from the previous version. Both are
> harmless and never repeat on future updates:
>
> 1. **Mods may appear disabled after the update.** The mod's internal id changed
>    (`PerkOracle` to `Oracle`), so you may need to open the in-game **Mods** menu
>    once and re-enable your mods. This is a one-time activation refresh; nothing
>    is lost.
> 2. **The mod's own in-game research resets once.** The optional "perk swap"
>    unlock will show as not-yet-researched again. To restore it, either research
>    it again in-game or simply turn that feature off in the mod settings (it is
>    optional). **Your resources, perks, and soldiers are not affected**, only the
>    mod's own research-unlock flag resets, this one time. Future updates will not
>    do this again (the save key is now a stable neutral id; see below).

### Changed
- **Renamed the mod from "PerkOracle" to "Oracle"** (mod ID `Morgott.Oracle`,
  assembly `Oracle.dll`, namespace `Morgott.Oracle`). The Steam Workshop page is
  **unchanged** (same item); existing subscribers keep their subscription.
- The perk-swap research save key is now a stable neutral id (no longer derived
  from the mod name), so future renames will not reset the research again.

### Added
- **Event outcome-preview tooltip.** Hovering an event choice now shows a framed
  tooltip previewing that choice's outcomes (reputation, resources, soldier
  stamina and HP, items, sites revealed, and other rewards). Strings come from the
  game's own native localization keys (no invented labels), and values mirror
  TFTV's actual grant math: the conditional diplomacy multiplier and the
  resource-reward multiplier are applied so the preview matches what you actually
  receive. Resource names are localized and use the native reward colors. The
  tooltip is cached per hover (no lag) and is hidden as soon as a choice is
  selected or the event screen closes.

## [1.3.0] - 2026-06-08

### Added
- **Subclass selection screen.** Highlights and previews **all** subclasses,
  including unresearched ones (shown greyed out), with per-perk tooltips. A
  confirmation dialog previews the subclass's perks (native icons + tooltips)
  before you commit to the choice.

### Fixed
- Perk wiki / rolled-perk highlight no longer fires in the main menu
  (lifecycle/context guard).

## [1.2.1] - 2026-06-06

### Changed
- Improved German, French, Italian, Polish, and Simplified Chinese translations
  (in-game text and store descriptions).

### Fixed
- Polish grammar and German/Polish typographic quotes; unified the localized
  research name across languages.

## [1.2.0] - 2026-06-06

### Added
- Optional research gate for perk swap: a new geoscape research project,
  **"Operative Reconditioning"**, that — when the requirement is enabled — must be
  completed before a soldier's random personal perk can be changed. Toggle in the mod
  settings (`RequirePerkSwapResearch`, default **on**).
- Custom native research illustration for "Operative Reconditioning".
- Placeholder setting `PerkSwapCostsResources` (no effect yet — reserved for a future
  resource cost on swapping).

### Changed
- Tuned the "Operative Reconditioning" research cost to roughly 3 in-game days.
- Rewrote the research text to be fully in-universe (no UI references), with complete
  8-language localization.
- License is now **CC BY-NC 4.0** (was MIT).

### Fixed
- Russian (and other) research text showed the wrong language due to a CSV column
  shift — corrected.
- Mod manifest description corrected to "standalone" (was wrongly "requires TFTV");
  TFTV remains optional and fully compatible.

## [1.1.0] - 2026

### Changed
- Made the mod standalone (TFTV optional) and localized the in-game display name per
  language; documentation synced.

### Added
- CC BY-NC 4.0 license file and manual (non-Workshop) install instructions.

## [1.0.0] - 2026

### Added
- Initial release: rolled-perk highlight, in-game candidate wiki with native ability
  tooltips, optional free perk swap, and 8-language localization.

[1.6.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.6.0
[1.5.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.5.0
[1.4.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.4.0
[1.3.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.3.0
[1.2.1]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.2.1
[1.2.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.2.0
[1.1.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.1.0
[1.0.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.0.0
