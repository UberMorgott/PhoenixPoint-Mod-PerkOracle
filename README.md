![PerkOracle](image/banner.png)

# PerkOracle

> An in-game wiki of rollable perks for Phoenix Point — standalone, TFTV-compatible — with optional perk swapping.

<!-- screenshot placeholder -->

PerkOracle shows you which random **"personal"** perks a soldier could roll into each
ability slot, presented as an in-game popup wiki with the game's native ability tooltips.
Optionally, it can let you swap an already-learned perk in that slot for any other perk
from its pool.

PerkOracle works standalone with base Phoenix Point and is fully compatible with
[Terror From The Void (TFTV)](https://github.com/Voland163/TFTV). When TFTV is present,
PerkOracle reads its per-slot perk data so the preview is exact; without it, the preview
falls back to the base game's personal-perk pool. It also works alongside class-adding mods
(e.g. an Officer / new-class mod). PerkOracle does **not** add perks, change perk generation,
or add ability rows — it only previews (and optionally swaps) what the game already rolls.

> **Steam Workshop:** <https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434>

## Features

- **Rolled-perk highlight** — on the ability-progression grid, cells whose perk was
  randomly rolled get a distinct background, so rolled perks stand out from fixed / class
  perks at a glance.
- **Candidate wiki** — hover the blue / rolled-highlighted perk cell and **right-click** it
  to open a popup listing *every* perk that could roll into that slot. Candidates use the
  game's native ability cell and the native framed name/description tooltip on hover.
  Right-click again to close.
- **Optional perk swap** — controlled by the `AllowPerkSwap` config toggle (**off by
  default**). When enabled, **left-click** any perk in the wiki to replace the soldier's
  already-learned perk in that slot. Free and reversible. Aimed at players who prefer an
  easier game.
- **Full localization** — UI strings ship in eight languages. The in-game mod **name** is
  localized per language, and the Steam Workshop / store description is localized into all
  eight languages too.

## Requirements

- **Phoenix Point** (base game) — that's all that's required.
- **Terror From The Void (TFTV)** — *optional.* Fully compatible: when installed, PerkOracle
  reads TFTV's per-slot rolled-perk data; when absent, it falls back to the base personal-perk
  pool. Also compatible with class-adding mods (e.g. Officer / new-class mods).

## Supported languages

English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

## Installation

The easiest route is to **subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)**.
For a manual install:

1. Download the latest release from the
   [Releases page](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases).
2. Extract the `PerkOracle` folder into your Phoenix Point `Mods\` directory, e.g.
   `…\Phoenix Point\Mods\PerkOracle\` (the folder must contain `PerkOracle.dll` and
   `meta.json`).
3. Enable the mod in the in-game mod manager. If you run TFTV, let PerkOracle load after it.

## Manual installation (without Steam Workshop)

If you don't use the Steam Workshop, you can install PerkOracle by hand:

1. Download the **latest release** ZIP from
   [GitHub Releases](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest)
   (current asset: `PerkOracle-v1.1.0.zip`).
2. Extract the ZIP — you'll get a `PerkOracle` folder containing `PerkOracle.dll`,
   `meta.json`, and an `Assets/` folder.
3. Copy that `PerkOracle` folder into your Phoenix Point `Mods` folder. For a Steam install
   this is typically `…\steamapps\common\Phoenix Point\Mods\` (create the `Mods` folder if it
   doesn't exist). The final path should be `Phoenix Point\Mods\PerkOracle\meta.json`.
4. Launch Phoenix Point and enable **PerkOracle** in the in-game mod manager.

> TFTV is optional. If you play with it, install it too (by any method); PerkOracle works
> standalone either way.

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
- Compatible with (but not dependent on) the **TFTV** overhaul by Voland163 and contributors.
- Phoenix Point © Snapshot Games.

---

## Русский

> Внутриигровая вики случайных («роленых») перков для Phoenix Point — автономный мод,
> совместимый с TFTV — с опциональной заменой перков.

PerkOracle показывает, какие случайные **«персональные»** перки боец может получить в
каждую ячейку прогрессии, в виде внутриигрового всплывающего вики с нативными подсказками
навыков. Опционально позволяет заменить уже выученный перк в этой ячейке на любой другой
из его пула.

### Возможности

- **Подсветка роленых перков** — в сетке прогрессии случайно выпавшие ячейки получают
  отдельный фон, чтобы отличать их от фиксированных/классовых перков.
- **Вики кандидатов** — наведите курсор на подсвеченную (синюю/роленую) ячейку и нажмите по
  ней **правую кнопку мыши**, чтобы открыть окно со *всеми* перками, которые могли выпасть в
  этот слот, с нативными подсказками. Повторный правый клик закрывает окно.
- **Опциональная замена перков** — управляется настройкой `AllowPerkSwap` (**по умолчанию
  выключено**). Когда включено, левый клик по перку в вики заменяет выученный перк бойца в
  этом слоте. Бесплатно и обратимо. Для тех, кто любит игру попроще.
- **Полная локализация** — интерфейс на восьми языках; название мода в игре локализовано
  для каждого языка, описание в Steam Workshop тоже переведено на все восемь языков.

### Требования

- **Phoenix Point** (базовая игра) — это всё, что нужно.
- **Terror From The Void (TFTV)** — *опционально.* Полностью совместим: при установленном
  TFTV PerkOracle читает его данные о роленых перках по слотам, без него — использует базовый
  пул персональных перков. Также совместим с модами, добавляющими классы (например, Officer).

### Установка

Проще всего **подписаться в [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)**.
Для ручной установки:

1. Скачайте последний релиз со страницы
   [Releases](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases).
2. Распакуйте папку `PerkOracle` в каталог `Mods\` игры Phoenix Point (в папке должны быть
   `PerkOracle.dll` и `meta.json`).
3. Включите мод во внутриигровом менеджере модов. Если используете TFTV — пусть PerkOracle
   загружается после него.

### Ручная установка (без Steam Workshop)

Если вы не пользуетесь Steam Workshop, PerkOracle можно установить вручную:

1. Скачайте ZIP **последнего релиза** со страницы
   [GitHub Releases](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest)
   (текущий файл: `PerkOracle-v1.1.0.zip`).
2. Распакуйте ZIP — вы получите папку `PerkOracle`, содержащую `PerkOracle.dll`,
   `meta.json` и папку `Assets/`.
3. Скопируйте эту папку `PerkOracle` в каталог `Mods` игры Phoenix Point. Для установки
   через Steam это обычно `…\steamapps\common\Phoenix Point\Mods\` (создайте папку `Mods`,
   если её нет). Итоговый путь должен быть `Phoenix Point\Mods\PerkOracle\meta.json`.
4. Запустите Phoenix Point и включите **PerkOracle** во внутриигровом менеджере модов.

> TFTV опционален. Если играете с ним, установите его тоже (любым способом); PerkOracle
> в любом случае работает автономно.

### Настройка

`AllowPerkSwap` (по умолчанию `false`): при выключенном значении вики работает только на
просмотр; при включённом — левый клик по перку заменяет выученный перк бойца в этом слоте.

### Лицензия

[MIT](LICENSE).
