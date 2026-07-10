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

## What's new in v1.5.0

- **Settings overhaul.** Every helper now has its own on/off toggle (highlight, perk wiki, subclass-confirm preview, event outcome preview, dismantle yield), and the rolled-perk highlight gets a color picker (Blue, Green, Gold, Red, Purple, White). All options are localized in 8 languages.
- **New: dismantle yield in item tooltips.** Item tooltips (equip/inventory, geoscape, tactical, and mutation screens) now show a native-styled "DISMANTLE" row with the resources you recover by scrapping the item — colored resource icons and gold digits, right-aligned in the footer.
- **Perk swap now costs Skill Points.** When enabled (default), a swap spends the soldier's skill points (default 50, configurable) through the native progression flow, is blocked when they can't afford it, and shows the cost on the perk tooltip's SP row. The perk-swap research now exists only while Perk Swap is enabled (toggling mid-campaign hides or reveals it live, save-safe).
- **Fix: ability tooltips render above everything.** A hovered ability/perk tooltip now draws above all windows and modal dialogs, so it is no longer hidden behind the subclass confirmation dialog.

See the full [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md).

## Links

- **Changelog:** [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md)
- **Issues / bug reports / questions:** [GitHub Issues](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)

## Features

- **Rolled-perk highlight.** Rolled perks get a colored tint on the ability-progression screen, so they stand out from fixed and class perks at a glance. The highlight can be turned off, and its color chosen from six presets (Blue, Green, Gold, Red, Purple, White), in settings.
- **Candidate wiki.** Right-click a rolled perk to open a popup listing every perk that could have landed in that slot. Candidates use the game's native ability cells and framed name/description tooltips. Right-click again to close. The popup changes nothing.
- **Event choice preview.** Hover an answer in a geoscape exploration event to preview its outcome (resources, reputation, soldier stamina/HP, items, sites revealed, and more) before you pick it. Labels use the game's own localized reward strings and values match the real grant, including TFTV's modifiers.
- **Dismantle yield.** Item tooltips (equip/inventory, geoscape, tactical, and mutation screens) show a native-styled "DISMANTLE" row listing the resources you recover by scrapping the item — colored resource icons and gold digits, right-aligned in the footer. Suppressed on manufacturing/Phoenixpedia tooltips, where the game already shows it.
- **Optional Perk Swap.** Off by default (`AllowPerkSwap`). When enabled, left-click a perk in the wiki to swap a soldier's already-learned perk in that slot for another existing perk. It creates no new perks. By default a swap costs the soldier 50 skill points (configurable, or free), charged through the native progression flow, blocked when the soldier can't afford it, with the cost shown on the perk's tooltip. Aimed at players who prefer an easier game.
- **Optional research gate.** A one-time **"Operative Reconditioning"** geoscape research can be required before retraining is allowed. It ships with its own custom illustration and fully localized in-universe text. Toggle it in settings (`RequirePerkSwapResearch`). The research project only exists while Perk Swap is on, and toggling it mid-campaign is save-safe.
- **Eight languages.** The UI, the in-game mod name, and the Steam Workshop description are all localized.

## Roadmap

Done:

- [x] Rolled-perk highlight and candidate wiki, with native icons and tooltips, on the ability-progression screen
- [x] Perk Swap (research-gated)
- [x] Subclass selection screen: highlight and preview of **all** subclasses, including unresearched ones (shown greyed out), with per-perk tooltips; a confirmation dialog that previews the subclass's perks (icons + native tooltips) before you commit to it
- [x] Event choice outcome-preview tooltip (resources, reputation, stamina/HP, items, sites revealed), accurate under TFTV
- [x] Dismantle-yield row in item tooltips (equip/inventory, geoscape, tactical, mutation screens), native-styled with colored resource icons
- [x] Per-feature settings (individual toggles + highlight color presets) and an optional skill-point cost for perk swap

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
| Highlight Rolled Perks | `on` | Tint randomly rolled personal perks on the progression screen. |
| Highlight Color | `Blue` | Tint used for the highlight: Blue, Green, Gold, Red, Purple, or White. |
| Perk Wiki | `on` | Allow opening the possible-skills wiki popup. |
| Subclass Confirm Preview | `on` | Confirm a subclass pick with its perks shown, and preview the subclasses you weren't offered. |
| Event Outcome Preview | `on` | Hover an event answer to preview its outcome. |
| Dismantle Yield | `on` | Show, in an item's tooltip, the resources you recover by scrapping it. |
| Perk Swap | `off` | Off: the wiki is view-only. On: left-clicking a wiki perk swaps the soldier's learned perk in that slot. |
| Require Research | `on` | Perk swap first needs the **"Operative Reconditioning"** research (only while Perk Swap is on). |
| Swap Costs Skill Points | `on` | A perk swap spends skill points; blocked if the soldier can't afford it. |
| Swap Skill-Point Cost | `50` | Skill-point cost per swap (0 = free). |

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

### Что нового в v1.5.0

- **Переработка настроек.** У каждого помощника теперь свой переключатель вкл/выкл (подсветка, вики перков, предпросмотр при выборе подкласса, предпросмотр исхода события, выход при разборе), а у подсветки роленых перков — выбор цвета (синий, зелёный, золотой, красный, фиолетовый, белый). Всё локализовано на 8 языков.
- **Новое: выход при разборе в подсказках предметов.** Подсказка предмета (экипировка/инвентарь, геоскейп, тактика, экраны мутаций) показывает строку «DISMANTLE» в нативном стиле с ресурсами за разбор — цветные иконки ресурсов и золотые цифры, справа в футере.
- **Замена перков теперь стоит очков навыков.** При включении (по умолчанию) замена тратит очки навыков бойца (по умолчанию 50, настраивается) через нативный поток прогрессии, блокируется при нехватке и показывает стоимость на подсказке перка. Исследование замены теперь существует только при включённой «Замене перков» (переключение по ходу кампании безопасно для сейвов).
- **Исправление: подсказки способностей поверх всего.** Подсказка способности/перка при наведении теперь рисуется поверх всех окон и диалогов, так что больше не прячется за окном подтверждения подкласса.

Полный список изменений см. в [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md).

### Ссылки

- **Список изменений:** [CHANGELOG.md](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/blob/main/CHANGELOG.md)
- **Issues / баг-репорты / вопросы:** [GitHub Issues](https://github.com/UberMorgott/PhoenixPoint-Mod-PerkOracle/issues)

### Возможности

- **Подсветка роленых перков.** Роленые перки подсвечиваются на экране прогрессии, чтобы отличать их от фиксированных и классовых. Подсветку можно выключить, а её цвет выбрать из шести пресетов (синий, зелёный, золотой, красный, фиолетовый, белый) в настройках.
- **Вики кандидатов.** Кликните правой кнопкой по роленому перку, чтобы открыть окно со всеми перками, которые могли выпасть в этот слот, с нативными иконками и подсказками. Повторный правый клик закрывает окно. Окно ничего не меняет.
- **Предпросмотр выбора в событиях.** Наведите курсор на вариант ответа в событии исследования на геоскейпе, чтобы увидеть его исход (ресурсы, репутацию, выносливость и здоровье бойцов, предметы, открытые точки и не только) до выбора. Названия берутся из родных локализованных строк наград игры, а значения совпадают с реальной выдачей, включая модификаторы TFTV.
- **Выход при разборе.** Подсказка предмета (экипировка/инвентарь, геоскейп, тактика, экраны мутаций) показывает строку «DISMANTLE» в нативном стиле с ресурсами за разбор — цветные иконки ресурсов и золотые цифры, справа в футере. На подсказках производства/Феникспедии скрыта, там игра уже это показывает.
- **Опциональная замена перков.** По умолчанию выключена (`AllowPerkSwap`). Когда включена, левый клик по перку в вики заменяет уже выученный перк бойца в этом слоте на другой существующий перк. Новых перков не создаёт. По умолчанию замена стоит бойцу 50 очков навыков (настраивается или бесплатно), списывается через нативный поток прогрессии, блокируется при нехватке, а стоимость показана на подсказке перка. Для тех, кто любит игру попроще.
- **Опциональное гейтирование исследованием.** Можно потребовать одноразовое исследование на геоскейпе **«Operative Reconditioning»** до переобучения. Оно идёт со своей кастомной иллюстрацией и полностью локализованным внутриигровым текстом. Включается в настройках (`RequirePerkSwapResearch`). Проект исследования существует только при включённой замене перков, переключение по ходу кампании безопасно для сейвов.
- **Восемь языков.** Локализованы интерфейс, название мода в игре и описание в Steam Workshop.

### Дорожная карта

Готово:

- [x] Подсветка роленых перков и вики кандидатов с нативными иконками и подсказками на экране прогрессии навыков
- [x] Замена перков (с гейтом по исследованию)
- [x] Экран выбора подкласса: подсветка и предпросмотр **всех** подклассов, включая неисследованные (показаны затенёнными), с подсказками по каждому перку; диалог подтверждения, который показывает перки подкласса (иконки + нативные подсказки) до того, как вы его возьмёте
- [x] Подсказка-предпросмотр исхода выбора в событиях (ресурсы, репутация, выносливость/здоровье, предметы, открытые точки), точная и под TFTV
- [x] Строка выхода при разборе в подсказках предметов (экипировка/инвентарь, геоскейп, тактика, экраны мутаций), в нативном стиле с цветными иконками ресурсов
- [x] Настройки по каждой функции (отдельные переключатели + пресеты цвета подсветки) и опциональная плата очками навыков за замену перков

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

- **Подсвечивать случайные перки** (по умолчанию вкл): подсветка роленых перков на экране прогрессии.
- **Цвет подсветки** (по умолчанию Blue): цвет подсветки — Blue, Green, Gold, Red, Purple или White.
- **Вики перков** (по умолчанию вкл): открытие окна-вики возможных перков.
- **Предпросмотр при выборе подкласса** (по умолчанию вкл): подтверждение выбора подкласса с показом его перков + предпросмотр непредложенных подклассов.
- **Предпросмотр исхода события** (по умолчанию вкл): наведение на вариант ответа показывает его исход.
- **Выход при разборе** (по умолчанию вкл): в подсказке предмета показаны ресурсы за разбор.
- **Замена перков** (по умолчанию выкл): при выключенном значении вики работает только на просмотр; при включённом левый клик по перку заменяет выученный перк бойца в этом слоте.
- **Требует исследования** (по умолчанию вкл): замена требует завершить исследование **«Operative Reconditioning»** на геоскейпе (только при включённой замене).
- **Замена стоит очков навыков** (по умолчанию вкл): замена тратит очки навыков бойца; блокируется при нехватке.
- **Стоимость замены в очках навыков** (по умолчанию `50`): сколько очков стоит одна замена (0 = бесплатно).

### Лицензия

Oracle © 2026 Morgott. Лицензия [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): свободно использовать и изменять в некоммерческих целях с указанием авторства.
