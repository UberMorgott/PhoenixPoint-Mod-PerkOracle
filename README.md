![Oracle](image/banner.png)

# Oracle

> An in-game advisor for Phoenix Point. It surfaces information the game normally hides, right where you make decisions, as on-hover hints and tooltips. Standalone on the base game, fully compatible with TFTV.

Oracle reads what the game already knows and shows it to you on hover. It does not create, add, or change game content; it only previews it. There is one exception, an optional and off-by-default perk swap (see below), for players who want more control.

Oracle has three helpers:

- **Perk foresight.** On the ability-progression screen, the random ("rolled") personal perks are highlighted, so you can tell at a glance which came from the roll and which from the fixed class track. Open a perk wiki popup to see every perk that could have landed in a slot, with the game's own icons and tooltips. An optional, research-gated perk swap can change a learned perk for another existing one.
- **Event choice preview.** Hover an answer option in a geoscape exploration event and a tooltip shows that choice's outcome *before* you pick it: resources, reputation, soldier stamina and HP, items, sites revealed, and more. Values match what the game will actually grant, including TFTV's modifiers, and the labels use the game's own localized text.
- **Subclass preview.** When you pick a soldier's second class at level-up, clicking a subclass first shows its full perks and abilities right in the confirmation, with the game's own icons and tooltips, so you see what you are getting before you commit. Unresearched subclasses are shown greyed out and can be previewed the same way.

> **Steam Workshop:** <https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434>

## Upgrading from PerkOracle (v1.4.0)

This mod used to be called **PerkOracle**. Version 1.4.0 renames it to **Oracle** on the **same Steam page** (same item, same subscription). Two one-time things happen when you update; both are harmless and never repeat on future updates:

1. **Mods may appear disabled after the update.** Because the mod's internal id changed (`PerkOracle` to `Oracle`), you may need to open the in-game **Mods** menu once and re-enable your mods. This is a one-time activation refresh; nothing is lost.
2. **The mod's own in-game research resets once.** The optional "perk swap" unlock will show as not-yet-researched again. To restore it, either research it again in-game or simply turn that feature off in the mod settings (it is optional). **Your resources, perks, and soldiers are not affected.** Only the mod's own research-unlock flag resets, this one time. Future updates will not do this again (the research save key is now a stable neutral id).

## What's new in v1.4.0

- **Renamed from PerkOracle to Oracle** (same Steam page, same item).
- **New: event choice outcome preview.** A framed tooltip on each event answer, built from the game's own native localized reward strings, with values accurate to the real grant (TFTV multipliers included).
- **Rename-safe saves.** The mod's research save key is now a stable neutral id, so future renames will not reset it again.

See the full [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md).

## Links

- **Changelog:** [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md)
- **Issues / bug reports / questions:** [GitHub Issues](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)

## Features

- **Rolled-perk highlight.** Rolled perks get a blue tint on the ability-progression screen, so they stand out from fixed and class perks at a glance.
- **Candidate wiki.** Right-click a rolled (blue) perk to open a popup listing every perk that could have landed in that slot. Candidates use the game's native ability cells and framed name/description tooltips. Right-click again to close. The popup changes nothing.
- **Event choice preview.** Hover an answer in a geoscape exploration event to preview its outcome (resources, reputation, soldier stamina/HP, items, sites revealed, and more) before you pick it. Labels use the game's own localized reward strings and values match the real grant, including TFTV's modifiers.
- **Optional Perk Swap.** Off by default (`AllowPerkSwap`). When enabled, left-click a perk in the wiki to swap a soldier's already-learned perk in that slot for another existing perk. It creates no new perks. Aimed at players who prefer an easier game.
- **Optional research gate.** A one-time **"Operative Reconditioning"** geoscape research can be required before retraining is allowed. It ships with its own custom illustration and fully localized in-universe text. Toggle it in settings (`RequirePerkSwapResearch`).
- **Eight languages.** The UI, the in-game mod name, and the Steam Workshop description are all localized.

## Roadmap

Done:

- [x] Rolled-perk highlight and candidate wiki, with native icons and tooltips, on the ability-progression screen
- [x] Perk Swap (research-gated)
- [x] Subclass selection screen: highlight and preview of **all** subclasses, including unresearched ones (shown greyed out), with per-perk tooltips; a confirmation dialog that previews the subclass's perks (icons + native tooltips) before you commit to it
- [x] Event choice outcome-preview tooltip (resources, reputation, stamina/HP, items, sites revealed), accurate under TFTV

Planned:

- [ ] Recruiting on the global map: a full read-only preview of a recruit before you hire them (3D model, stats, perks, and equipment)
- [ ] Unique mercenaries: their full unique description and signature gimmick, plus a preview of their perks, model, and equipment

## Requirements

- **Phoenix Point** (base game) is all you need.
- **Terror From The Void (TFTV)** is optional and compatible. With it installed, Oracle reads TFTV's per-slot data and matches TFTV's reward math; without it, the base game's data. Also compatible with class-adding mods (for example Officer or new-class mods).

## Supported languages

English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

## Installation

The easiest route is to **subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)**.

To install by hand:

1. Download the `Oracle-*.zip` from the [latest release page](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest).
2. Extract it. You get a `Oracle` folder containing `Oracle.dll`, `meta.json`, and an `Assets/` folder.
3. Copy that folder into your Phoenix Point `Mods` folder. For a Steam install this is usually `…\steamapps\common\Phoenix Point\Mods\` (create `Mods` if it doesn't exist). The final path should be `Phoenix Point\Mods\Oracle\meta.json`.
4. Launch Phoenix Point and enable **Oracle** in the in-game mod manager. If you run TFTV, let Oracle load after it.

TFTV is optional. If you play with it, install it too (by any method); Oracle works standalone either way.

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

Oracle © 2026 Morgott. Licensed under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to use and modify for non-commercial purposes with attribution.

## Credits

- Built by **Morgott**.
- Compatible with, but not dependent on, the **TFTV** overhaul by Voland163 and contributors.
- Phoenix Point © Snapshot Games.

---

## Русский

> Внутриигровой помощник для Phoenix Point. Показывает информацию, которую игра обычно скрывает, прямо там, где вы принимаете решения, в виде подсказок и всплывающих окон при наведении. Работает отдельно на базовой игре, полностью совместим с TFTV.

Oracle читает то, что игра уже знает, и показывает вам при наведении. Он не создаёт, не добавляет и не меняет игровой контент, а лишь показывает его заранее. Есть одно исключение: необязательная и по умолчанию выключенная замена перков (см. ниже) для тех, кому нужно больше контроля.

У Oracle три помощника:

- **Предвидение перков.** На экране прогрессии способностей случайные («роленые») персональные перки подсвечены, чтобы сразу было видно, что выпало случайно, а что взято из фиксированной классовой ветки. Окно-вики показывает все перки, которые могли выпасть в слот, с родными иконками и подсказками игры. Опциональная замена перков (с гейтом по исследованию) может сменить выученный перк на другой существующий.
- **Предпросмотр выбора в событиях.** Наведите курсор на вариант ответа в событии исследования на геоскейпе, и подсказка покажет его исход *до* того, как вы выберете: ресурсы, репутацию, выносливость и здоровье бойцов, предметы, открытые точки и не только. Значения совпадают с тем, что игра реально выдаст, включая модификаторы TFTV, а названия берутся из родного локализованного текста игры.
- **Предпросмотр подкласса.** Когда на повышении вы выбираете второй класс бойца, клик по подклассу сразу показывает все его перки и способности прямо в подтверждении, с родными иконками и подсказками игры, чтобы вы видели, что получаете, до того как согласитесь. Неисследованные подклассы показаны затенёнными, и их можно так же предпросмотреть.

> **Steam Workshop:** <https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434>

### Обновление с PerkOracle (v1.4.0)

Раньше мод назывался **PerkOracle**. В версии 1.4.0 он переименован в **Oracle** на той же странице Steam (тот же предмет, та же подписка). При обновлении происходят две разовые вещи; обе безвредны и в будущих обновлениях не повторятся:

1. **После обновления моды могут показаться отключёнными.** Из-за смены внутреннего идентификатора (`PerkOracle` в `Oracle`), возможно, потребуется один раз открыть внутриигровое меню **«Моды»** и заново включить ваши моды. Это разовая переактивация; ничего не теряется.
2. **Собственное внутриигровое исследование мода сбросится один раз.** Опциональная разблокировка «замены перков» снова покажется неисследованной. Чтобы вернуть её, либо исследуйте её заново в игре, либо просто отключите эту функцию в настройках мода (она необязательная). **Ваши ресурсы, перки и бойцы не затрагиваются.** Сбрасывается только флаг исследования самого мода, и только в этот раз. В будущих обновлениях такого не повторится (ключ сохранения исследования теперь стабильный нейтральный идентификатор).

### Что нового в v1.4.0

- **Переименование из PerkOracle в Oracle** (та же страница Steam, тот же предмет).
- **Новое: предпросмотр исхода выбора в событиях.** Оформленная подсказка на каждом варианте ответа, собранная из родных локализованных строк наград игры, со значениями, точными для реальной выдачи (с учётом множителей TFTV).
- **Сейвы, устойчивые к переименованию.** Ключ сохранения исследования теперь стабильный нейтральный идентификатор, так что будущие переименования его больше не сбросят.

Полный список изменений см. в [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md).

### Ссылки

- **Список изменений:** [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md)
- **Issues / баг-репорты / вопросы:** [GitHub Issues](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)

### Возможности

- **Подсветка роленых перков.** Роленые перки подсвечиваются синим на экране прогрессии, чтобы отличать их от фиксированных и классовых.
- **Вики кандидатов.** Кликните правой кнопкой по роленому (синему) перку, чтобы открыть окно со всеми перками, которые могли выпасть в этот слот, с нативными иконками и подсказками. Повторный правый клик закрывает окно. Окно ничего не меняет.
- **Предпросмотр выбора в событиях.** Наведите курсор на вариант ответа в событии исследования на геоскейпе, чтобы увидеть его исход (ресурсы, репутацию, выносливость и здоровье бойцов, предметы, открытые точки и не только) до выбора. Названия берутся из родных локализованных строк наград игры, а значения совпадают с реальной выдачей, включая модификаторы TFTV.
- **Опциональная замена перков.** По умолчанию выключена (`AllowPerkSwap`). Когда включена, левый клик по перку в вики заменяет уже выученный перк бойца в этом слоте на другой существующий перк. Новых перков не создаёт. Для тех, кто любит игру попроще.
- **Опциональное гейтирование исследованием.** Можно потребовать одноразовое исследование на геоскейпе **«Operative Reconditioning»** до переобучения. Оно идёт со своей кастомной иллюстрацией и полностью локализованным внутриигровым текстом. Включается в настройках (`RequirePerkSwapResearch`).
- **Восемь языков.** Локализованы интерфейс, название мода в игре и описание в Steam Workshop.

### Дорожная карта

Готово:

- [x] Подсветка роленых перков и вики кандидатов с нативными иконками и подсказками на экране прогрессии навыков
- [x] Замена перков (с гейтом по исследованию)
- [x] Экран выбора подкласса: подсветка и предпросмотр **всех** подклассов, включая неисследованные (показаны затенёнными), с подсказками по каждому перку; диалог подтверждения, который показывает перки подкласса (иконки + нативные подсказки) до того, как вы его возьмёте
- [x] Подсказка-предпросмотр исхода выбора в событиях (ресурсы, репутация, выносливость/здоровье, предметы, открытые точки), точная и под TFTV

В планах:

- [ ] Найм на глобальной карте: полный предпросмотр рекрута до найма, только для чтения (3D-модель, характеристики, перки и снаряжение)
- [ ] Уникальные наёмники: их полное уникальное описание и фирменная фишка, а также предпросмотр перков, модели и снаряжения

### Требования

- **Phoenix Point** (базовая игра). Это всё, что нужно.
- **Terror From The Void (TFTV)** опционален и совместим. С ним Oracle читает данные TFTV по слотам и совпадает с его расчётом наград, без него использует данные базовой игры. Также совместим с модами, добавляющими классы (например, Officer).

### Установка

Проще всего **подписаться в [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3739613434)**.

Для ручной установки:

1. Скачайте `Oracle-*.zip` со [страницы последнего релиза](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/releases/latest).
2. Распакуйте его. Вы получите папку `Oracle` с файлами `Oracle.dll`, `meta.json` и папкой `Assets/`.
3. Скопируйте эту папку в каталог `Mods` игры Phoenix Point. Для установки через Steam это обычно `…\steamapps\common\Phoenix Point\Mods\` (создайте папку `Mods`, если её нет). Итоговый путь: `Phoenix Point\Mods\Oracle\meta.json`.
4. Запустите Phoenix Point и включите **Oracle** во внутриигровом менеджере модов. Если используете TFTV, пусть Oracle загружается после него.

TFTV опционален. Если играете с ним, установите его тоже (любым способом); Oracle в любом случае работает автономно.

### Настройка

В настройках мода внутри игры:

- `AllowPerkSwap` (по умолчанию `false`): при выключенном значении вики работает только на просмотр; при включённом левый клик по перку заменяет выученный перк бойца в этом слоте.
- `RequirePerkSwapResearch` (по умолчанию `true`): при включённом значении замена требует завершить исследование **«Operative Reconditioning»** на геоскейпе; при выключенном замена доступна сразу, как только включён `AllowPerkSwap`.
- `PerkSwapCostsResources` (по умолчанию `false`): задел на будущее обновление (плата ресурсами за замену). Пока ни на что не влияет.

### Лицензия

Oracle © 2026 Morgott. Лицензия [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): свободно использовать и изменять в некоммерческих целях с указанием авторства.
