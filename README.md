![PerkOracle](image/banner.png)

# PerkOracle

> A read-only in-game preview of which random "personal" perks a soldier could have rolled. For Phoenix Point, standalone or with TFTV.

<!-- screenshot placeholder -->

That randomness is the game's own system (vanilla and TFTV), not something the mod adds. PerkOracle only reads it and shows it to you. It does not create, add, or change any perks.

On the ability-progression screen, rolled perks get a blue tint. Right-click a rolled (blue) perk to open a wiki popup of every perk that could have landed in that slot, shown with the game's native icons and tooltips. The popup changes nothing.

PerkOracle works standalone with the base game, is compatible with [Terror From The Void (TFTV)](https://github.com/Voland163/TFTV), and works alongside class-adding mods (for example an Officer or new-class mod). When TFTV is present, PerkOracle reads its per-slot data so the preview is exact; without it, the preview uses the base game's personal-perk pool.

> **Steam Workshop:** <https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434>

## Links

- **Changelog:** [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md)
- **Issues / bug reports / questions:** [GitHub Issues](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)

## Features

- **Rolled-perk highlight.** Rolled perks get a blue tint on the ability-progression screen, so they stand out from fixed and class perks at a glance.
- **Candidate wiki.** Right-click a rolled (blue) perk to open a popup listing every perk that could have landed in that slot. Candidates use the game's native ability cells and framed name/description tooltips. Right-click again to close. The popup changes nothing.
- **Optional Perk Swap.** Off by default (`AllowPerkSwap`). When enabled, left-click a perk in the wiki to swap a soldier's already-learned perk in that slot for another existing perk. It creates no new perks. Aimed at players who prefer an easier game.
- **Optional research gate.** A one-time **"Operative Reconditioning"** geoscape research can be required before retraining is allowed. It ships with its own custom illustration and fully localized in-universe text. Toggle it in settings (`RequirePerkSwapResearch`).
- **Eight languages.** The UI, the in-game mod name, and the Steam Workshop description are all localized.

## Requirements

- **Phoenix Point** (base game) is all you need.
- **Terror From The Void (TFTV)** is optional and compatible. With it installed, PerkOracle reads TFTV's per-slot data; without it, the base personal-perk pool. Also compatible with class-adding mods (for example Officer or new-class mods).

## Supported languages

English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

## Installation

The easiest route is to **subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)**.

To install by hand:

1. Download the latest release ZIP from [GitHub Releases](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest) (current asset: `PerkOracle-v1.2.0.zip`).
2. Extract it. You get a `PerkOracle` folder containing `PerkOracle.dll`, `meta.json`, and an `Assets/` folder.
3. Copy that folder into your Phoenix Point `Mods` folder. For a Steam install this is usually `…\steamapps\common\Phoenix Point\Mods\` (create `Mods` if it doesn't exist). The final path should be `Phoenix Point\Mods\PerkOracle\meta.json`.
4. Launch Phoenix Point and enable **PerkOracle** in the in-game mod manager. If you run TFTV, let PerkOracle load after it.

TFTV is optional. If you play with it, install it too (by any method); PerkOracle works standalone either way.

## Configuration

In the in-game mod settings:

| Setting | Default | Effect |
|---|---|---|
| `AllowPerkSwap` | `false` | Off: the wiki is view-only. On: left-clicking a perk in the wiki swaps the soldier's learned perk in that slot. |
| `RequirePerkSwapResearch` | `true` | On: perk swapping requires the **"Operative Reconditioning"** geoscape research first. Off: swapping is available as soon as `AllowPerkSwap` is on. Only relevant while `AllowPerkSwap` is on. |
| `PerkSwapCostsResources` | `false` | Placeholder for a future update (making swaps cost resources). No effect yet. |

## Building from source

Requires the .NET SDK and a Phoenix Point install (the project references the game's managed assemblies).

```powershell
# build the mod assembly in Release
dotnet build -c Release

# run the unit tests
dotnet test
```

## License

PerkOracle © 2026 Morgott. Licensed under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to use and modify for non-commercial purposes with attribution.

## Credits

- Built by **Morgott**.
- Compatible with, but not dependent on, the **TFTV** overhaul by Voland163 and contributors.
- Phoenix Point © Snapshot Games.

---

## Русский

> Внутриигровой предпросмотр того, какие случайные «персональные» перки мог получить боец. Только для чтения. Для Phoenix Point, автономно или вместе с TFTV.

Эта случайность есть собственная механика игры (ваниль и TFTV), а не что-то добавленное модом. PerkOracle лишь читает её и показывает вам. Он не создаёт, не добавляет и не меняет перки.

На экране прогрессии навыков роленые перки подсвечиваются синим. Кликните по роленому (синему) перку правой кнопкой мыши, чтобы открыть окно-вики со всеми перками, которые могли выпасть в этот слот, с нативными иконками и подсказками игры. Окно ничего не меняет.

PerkOracle работает автономно с базовой игрой, совместим с [Terror From The Void (TFTV)](https://github.com/Voland163/TFTV) и работает вместе с модами, добавляющими классы (например, Officer или другой мод на класс). Если установлен TFTV, PerkOracle читает его данные по слотам, и предпросмотр точен; без него используется базовый пул персональных перков.

> **Steam Workshop:** <https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434>

### Ссылки

- **Список изменений:** [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md)
- **Issues / баг-репорты / вопросы:** [GitHub Issues](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)

### Возможности

- **Подсветка роленых перков.** Роленые перки подсвечиваются синим на экране прогрессии, чтобы отличать их от фиксированных и классовых.
- **Вики кандидатов.** Кликните правой кнопкой по роленому (синему) перку, чтобы открыть окно со всеми перками, которые могли выпасть в этот слот, с нативными иконками и подсказками. Повторный правый клик закрывает окно. Окно ничего не меняет.
- **Опциональная замена перков.** По умолчанию выключена (`AllowPerkSwap`). Когда включена, левый клик по перку в вики заменяет уже выученный перк бойца в этом слоте на другой существующий перк. Новых перков не создаёт. Для тех, кто любит игру попроще.
- **Опциональное гейтирование исследованием.** Можно потребовать одноразовое исследование на геоскейпе **«Operative Reconditioning»** до переобучения. Оно идёт со своей кастомной иллюстрацией и полностью локализованным внутриигровым текстом. Включается в настройках (`RequirePerkSwapResearch`).
- **Восемь языков.** Локализованы интерфейс, название мода в игре и описание в Steam Workshop.

### Требования

- **Phoenix Point** (базовая игра). Это всё, что нужно.
- **Terror From The Void (TFTV)** опционален и совместим. С ним PerkOracle читает данные TFTV по слотам, без него использует базовый пул персональных перков. Также совместим с модами, добавляющими классы (например, Officer).

### Установка

Проще всего **подписаться в [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)**.

Для ручной установки:

1. Скачайте ZIP последнего релиза со страницы [GitHub Releases](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest) (текущий файл: `PerkOracle-v1.2.0.zip`).
2. Распакуйте его. Вы получите папку `PerkOracle` с файлами `PerkOracle.dll`, `meta.json` и папкой `Assets/`.
3. Скопируйте эту папку в каталог `Mods` игры Phoenix Point. Для установки через Steam это обычно `…\steamapps\common\Phoenix Point\Mods\` (создайте папку `Mods`, если её нет). Итоговый путь: `Phoenix Point\Mods\PerkOracle\meta.json`.
4. Запустите Phoenix Point и включите **PerkOracle** во внутриигровом менеджере модов. Если используете TFTV, пусть PerkOracle загружается после него.

TFTV опционален. Если играете с ним, установите его тоже (любым способом); PerkOracle в любом случае работает автономно.

### Настройка

В настройках мода внутри игры:

- `AllowPerkSwap` (по умолчанию `false`): при выключенном значении вики работает только на просмотр; при включённом левый клик по перку заменяет выученный перк бойца в этом слоте.
- `RequirePerkSwapResearch` (по умолчанию `true`): при включённом значении замена требует завершить исследование **«Operative Reconditioning»** на геоскейпе; при выключенном замена доступна сразу, как только включён `AllowPerkSwap`.
- `PerkSwapCostsResources` (по умолчанию `false`): задел на будущее обновление (плата ресурсами за замену). Пока ни на что не влияет.

### Лицензия

PerkOracle © 2026 Morgott. Лицензия [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): свободно использовать и изменять в некоммерческих целях с указанием авторства.
