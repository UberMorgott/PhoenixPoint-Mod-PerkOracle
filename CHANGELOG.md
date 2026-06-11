# Changelog

All notable changes to Oracle are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.2.1]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.2.1
[1.2.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.2.0
[1.1.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.1.0
[1.0.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/tag/v1.0.0
