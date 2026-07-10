![Oracle](image/banner.png)

# Oracle

An in-game advisor for Phoenix Point that surfaces information the game normally hides — as on-hover hints and tooltips, right where you make decisions. It changes no game content (it only previews it), except one optional, off-by-default perk swap. Standalone on the base game, fully compatible with TFTV.

## Features

- **Rolled-perk highlight** — random ("rolled") personal perks get a colored tint on the ability-progression screen, so they stand out from fixed and class perks.
- **Candidate wiki** — right-click a perk to open a popup of every perk that could have landed in that slot, using the game's own ability cells and framed tooltips.
- **Subclass preview** — picking a soldier's second class confirms with that subclass's perks shown, and lets you preview the subclasses you weren't offered (including unresearched, shown greyed out).
- **Event outcome preview** — hover a geoscape event answer to see its outcome (resources, reputation, soldier HP/stamina, items, sites revealed) before you pick it; accurate under TFTV.
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

---

## Русский

Внутриигровой помощник для Phoenix Point: показывает информацию, которую игра обычно скрывает, в виде подсказок при наведении — прямо там, где вы принимаете решения. Игровой контент не меняет (лишь показывает заранее), кроме одной опциональной, по умолчанию выключенной замены перков. Работает отдельно на базовой игре, полностью совместим с TFTV.

### Возможности

- **Подсветка роленых перков** — случайные («роленые») персональные перки получают цветную подсветку на экране прогрессии, чтобы отличать их от фиксированных и классовых.
- **Вики кандидатов** — правый клик по перку открывает окно со всеми перками, которые могли выпасть в этот слот, с родными ячейками способностей и подсказками игры.
- **Предпросмотр подкласса** — при выборе второго класса бойца подтверждение показывает перки этого подкласса и даёт предпросмотр непредложенных подклассов (включая неисследованные, показанные затенёнными).
- **Предпросмотр исхода события** — наведите курсор на вариант ответа в событии на геоскейпе, чтобы увидеть его исход (ресурсы, репутация, здоровье/выносливость бойцов, предметы, открытые точки) до выбора; точно под TFTV.
- **Выход при разборе** — подсказки предметов (экипировка, геоскейп, тактика, мутации) показывают строку «DISMANTLE» в нативном стиле с ресурсами за разбор.
- **Опциональная замена перков** — по умолчанию выключена; левый клик по перку в вики заменяет выученный перк бойца в этом слоте на другой существующий (тратит очки навыков, опционально с гейтом по исследованию).

### Настройки (меню модов в игре)

В порядке меню. Логические поля — переключатели, цвет — выбор из пресетов, стоимость — текстовое поле.

| Настройка | По умолч. | Действие |
|---|---|---|
| `EnableRolledPerkHighlight` | `true` | Подсветка случайно выпавших персональных перков на экране прогрессии |
| `RolledPerkHighlightColor` | `Blue` | Цвет подсветки; пресеты Blue, Green, Gold, Red, Purple, White |
| `EnablePerkWiki` | `true` | Разрешить открытие окна-вики кандидатов |
| `EnableSubclassConfirmDecoration` | `true` | Подтверждение выбора подкласса с показом его перков + предпросмотр непредложенных подклассов |
| `ShowEventOutcomePreview` | `true` | Наведение на вариант ответа показывает его исход |
| `ShowDismantleCompensation` | `true` | Показать в подсказке предмета ресурсы за его разбор |
| `AllowPerkSwap` | `false` | Главный переключатель; вкл = левый клик по перку в вики заменяет выученный перк бойца в слоте |
| `RequirePerkSwapResearch` | `true` | Гейт замены по исследованию «Operative Reconditioning» (существует только при включённой замене) |
| `PerkSwapCostsResources` | `true` | Замена тратит очки навыков; блокируется при нехватке |
| `PerkSwapSkillPointCost` | `50` | Стоимость одной замены в очках навыков (0 = бесплатно) |
| `EnableDebugLogging` | `false` | Писать диагностические строки Oracle в лог игры |

### Установка

**Steam Workshop:** [подписаться](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434) (id 3739613434).

**Вручную:** скачайте `Oracle-*.zip` со [страницы последнего релиза](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest), распакуйте и скопируйте папку `Oracle` (`Oracle.dll`, `meta.json`, `Assets/`) в `Phoenix Point\Mods\`. Включите **Oracle** во внутриигровом менеджере модов; если играете с TFTV, пусть Oracle загружается после него.

**Требования:** базовая игра Phoenix Point. Без зависимостей. TFTV опционален и полностью совместим (тогда Oracle читает данные TFTV по слотам и его расчёт наград); также совместим с модами, добавляющими классы (например, Officer).

### Сборка из исходников

```powershell
dotnet build -c Release
dotnet test
```

### Локализация

English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

### Лицензия

Oracle © 2026 Morgott. [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) — свободно использовать и изменять в некоммерческих целях с указанием авторства.

### Благодарности

Сделано **Morgott**. Совместимо с **TFTV** (Voland163). Phoenix Point © Snapshot Games.
