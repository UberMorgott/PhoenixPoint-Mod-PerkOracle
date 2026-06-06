![PerkOracle](image/banner.png)

# PerkOracle

> An in-game wiki of rollable perks for Phoenix Point (TFTV) — with optional perk swapping.

<!-- screenshot placeholder -->

PerkOracle shows you which random **"personal"** perks a soldier could roll into each
ability slot, presented as an in-game popup wiki with the game's native ability tooltips.
Optionally, it can let you swap an already-learned perk in that slot for any other perk
from its pool.

Under [TFTV (Terror From The Void)](https://github.com/Voland163/TFTV) some personal perks
are randomly rolled per soldier. PerkOracle reads TFTV's own configuration to know exactly
which perks each slot can produce, so the preview is accurate.

## Features

- **Rolled-perk highlight** — on the ability-progression grid, cells whose perk was
  randomly rolled get a distinct background, so rolled perks stand out from fixed / class
  perks at a glance.
- **Candidate wiki** — hover a rolled cell and press **cancel** (right-click or **Esc**) to
  open a popup listing *every* perk that could roll into that slot. Candidates use the
  game's native ability cell and the native framed name/description tooltip on hover. Cancel
  again to close.
- **Optional perk swap** — controlled by the `AllowPerkSwap` config toggle (**off by
  default**). When enabled, **left-click** any perk in the wiki to replace the soldier's
  already-learned perk in that slot. Free and reversible. Aimed at players who prefer an
  easier game.
- **Full localization** — UI strings ship in eight languages.

## Requirements

- **Phoenix Point**
- **Terror From The Void (TFTV)** overhaul — `phoenixrising.tftv`

## Supported languages

English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

## Installation

1. Install and enable the **TFTV** overhaul.
2. Download the latest release from the
   [Releases page](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases).
3. Extract the `PerkOracle` folder into your Phoenix Point `Mods\` directory, e.g.
   `…\Phoenix Point\Mods\PerkOracle\` (the folder must contain `PerkOracle.dll` and
   `meta.json`).
4. Enable the mod in the in-game mod manager. It loads after TFTV.

## Configuration

In the in-game mod settings:

| Setting | Default | Effect |
|---|---|---|
| `AllowPerkSwap` | `false` | When **off**, the wiki is a pure preview (view-only). When **on**, left-clicking a perk in the wiki replaces the soldier's learned perk in that slot. |

## Building from source

Requires the .NET SDK and a Phoenix Point install (the project references the game's managed
assemblies).

```powershell
# build the mod assembly in Release
dotnet build -c Release

# run the unit tests
dotnet test
```

## License

[MIT](LICENSE).

## Credits

- Built by **Morgott**.
- Depends on and targets the **TFTV** overhaul by Voland163 and contributors.
- Phoenix Point © Snapshot Games.

---

## Русский

> Внутриигровая вики случайных («роленых») перков для Phoenix Point (TFTV) — с
> опциональной заменой перков.

PerkOracle показывает, какие случайные **«персональные»** перки боец может получить в
каждую ячейку прогрессии, в виде внутриигрового всплывающего вики с нативными подсказками
навыков. Опционально позволяет заменить уже выученный перк в этой ячейке на любой другой
из его пула.

### Возможности

- **Подсветка роленых перков** — в сетке прогрессии случайно выпавшие ячейки получают
  отдельный фон, чтобы отличать их от фиксированных/классовых перков.
- **Вики кандидатов** — наведите курсор на роленую ячейку и нажмите **отмену** (правый клик
  или **Esc**), чтобы открыть окно со *всеми* перками, которые могли выпасть в этот слот, с
  нативными подсказками. Повторная отмена закрывает окно.
- **Опциональная замена перков** — управляется настройкой `AllowPerkSwap` (**по умолчанию
  выключено**). Когда включено, левый клик по перку в вики заменяет выученный перк бойца в
  этом слоте. Бесплатно и обратимо. Для тех, кто любит игру попроще.
- **Полная локализация** — интерфейс на восьми языках.

### Требования

- **Phoenix Point**
- Оверхаул **Terror From The Void (TFTV)** — `phoenixrising.tftv`

### Установка

1. Установите и включите оверхаул **TFTV**.
2. Скачайте последний релиз со страницы
   [Releases](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases).
3. Распакуйте папку `PerkOracle` в каталог `Mods\` игры Phoenix Point (в папке должны быть
   `PerkOracle.dll` и `meta.json`).
4. Включите мод во внутриигровом менеджере модов. Он загружается после TFTV.

### Настройка

`AllowPerkSwap` (по умолчанию `false`): при выключенном значении вики работает только на
просмотр; при включённом — левый клик по перку заменяет выученный перк бойца в этом слоте.

### Лицензия

[MIT](LICENSE).
