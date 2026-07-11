<div align="center">

![Oracle](image/banner.png)

# Oracle

[![English](https://img.shields.io/badge/lang-English-1f6feb?style=for-the-badge)](README.md)
[![Русский](https://img.shields.io/badge/lang-%D0%A0%D1%83%D1%81%D1%81%D0%BA%D0%B8%D0%B9-6e7681?style=for-the-badge)](README.ru.md)

[![Stars](https://img.shields.io/github/stars/UberMorgott/PhoenixPoint-Mod-PerkOracle?style=flat-square)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/stargazers)
[![Forks](https://img.shields.io/github/forks/UberMorgott/PhoenixPoint-Mod-PerkOracle?style=flat-square)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/network/members)
[![Issues](https://img.shields.io/github/issues/UberMorgott/PhoenixPoint-Mod-PerkOracle?style=flat-square)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)
[![Last commit](https://img.shields.io/github/last-commit/UberMorgott/PhoenixPoint-Mod-PerkOracle?style=flat-square)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/commits)
[![Version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FUberMorgott%2FPhoenixPoint-Mod-PerkOracle%2Fmain%2Fmeta.json&query=%24.Version&label=version&color=1f6feb&style=flat-square)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest)
[![License](https://img.shields.io/badge/license-CC%20BY--NC%204.0-blue?style=flat-square)](LICENSE)

[![Steam Workshop](https://img.shields.io/badge/Steam-Workshop-1b2838?style=for-the-badge&logo=steam&logoColor=white)](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)
[![Report a bug](https://img.shields.io/badge/%F0%9F%90%9E%20report-a%20bug-d1242f?style=for-the-badge)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues/new?labels=bug)
[![Request a feature](https://img.shields.io/badge/%E2%9C%A8%20request-a%20feature-8957e5?style=for-the-badge)](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues/new?labels=enhancement)

</div>

An in-game advisor for Phoenix Point that surfaces information the game normally hides — as on-hover hints and tooltips, right where you make decisions. It changes no game content (it only previews it), except one optional, off-by-default perk swap. Standalone on the base game, fully compatible with TFTV.

## Features

- **Rolled-perk highlight** — random ("rolled") personal perks get a colored tint on the ability-progression screen, so they stand out from fixed and class perks.
- **Candidate wiki** — right-click a perk to open a popup of every perk that could have landed in that slot, using the game's own ability cells and framed tooltips.
- **Class wiki** — click a trained class skill on the progression screen to open a wiki of every class: each class's ability track and personal perk slots, read live from your game (so it matches TFTV and class-adding mods like Officer). Fixed TFTV slots are shown; random slots appear as `?` and expand to their exact per-slot pool on click.
- **Subclass preview** — picking a soldier's second class confirms with that subclass's perks shown, and lets you preview the subclasses you weren't offered (including unresearched, shown greyed out).
- **Event outcome preview** — hover a geoscape event answer to see its outcome (resources, reputation, soldier HP/stamina, items, sites revealed) before you pick it; accurate under TFTV. Shown only when the event actually offers a choice (2+ options).
- **Dismantle yield** — item tooltips (equip, geoscape, tactical, mutation) show a native "DISMANTLE" row with the resources you recover by scrapping.
- **Optional perk swap** — off by default; left-click a wiki perk to swap a soldier's learned perk in that slot for another existing one (spends skill points, optionally gated behind an in-game research).

## Settings (in-game Mods menu)

Listed in menu order. Boolean fields are toggles, the color is a picker, and the cost is a text box.

| Setting | Default | Effect |
|---|---|---|
| `EnableRolledPerkHighlight` | `true` | Tint randomly rolled personal perks on the progression screen |
| `RolledPerkHighlightColor` | `Blue` | Highlight tint; presets Blue, Green, Gold, Red, Purple, White |
| `EnablePerkWiki` | `true` | Allow opening the candidate-perk wiki popup |
| `EnableSubclassConfirmDecoration` | `true` | Confirm a subclass pick with its perks shown, and preview the subclasses you weren't offered |
| `ShowEventOutcomePreview` | `true` | Hover an event answer to preview its outcome |
| `ShowDismantleCompensation` | `true` | Show, in an item's tooltip, the resources you recover by scrapping it |
| `AllowPerkSwap` | `false` | Master toggle; on = left-click a wiki perk to swap the soldier's learned perk in that slot |
| `RequirePerkSwapResearch` | `true` | Gate swapping behind the "Operative Reconditioning" research (which exists only while swap is on) |
| `PerkSwapCostsResources` | `true` | A swap spends skill points; blocked if the soldier can't afford it |
| `PerkSwapSkillPointCost` | `50` | Skill-point cost per swap (0 = free) |
| `EnableDebugLogging` | `false` | Write Oracle diagnostic lines to the player log |

## Installation

**Steam Workshop:** [subscribe here](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434) (id 3739613434).

**Manual:** download `Oracle-*.zip` from the [latest release](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest), extract, and copy the `Oracle` folder (`Oracle.dll`, `meta.json`, `Assets/`) into `Phoenix Point\Mods\`. Enable **Oracle** in the in-game mod manager; if you run TFTV, let Oracle load after it.

**Requirements:** Phoenix Point base game. No dependencies. TFTV optional and fully compatible (Oracle then reads TFTV's per-slot data and reward math); also compatible with class-adding mods (e.g. Officer).

## Building from source

```powershell
dotnet build -c Release
dotnet test
```

## Localization

English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

## License

Oracle © 2026 Morgott. [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) — free to use and modify for non-commercial purposes with attribution.

## Credits

Built by **Morgott**. Compatible with **TFTV** (Voland163). Phoenix Point © Snapshot Games.
