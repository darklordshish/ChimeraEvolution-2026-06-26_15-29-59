---
name: chimera-unity
description: Вести работу в Unity САМОСТОЯТЕЛЬНО через пайплайн живого редактора — поднять редактор (в том числе headless), пересоздать виды, прогнать тесты, выгрузить карту тел и схемы, СНЯТЬ КАДР существа или химеры и посмотреть на него глазами. Использовать при любой работе с данными видов, морфологией, тестами и отчётами CHIMERA — вместо того чтобы просить пользователя нажимать кнопки в редакторе.
---

# CHIMERA в Unity своими руками

**Главное правило: не просить пользователя делать то, что делается командой.** До 17.09 Claude не мог
ни запустить Unity, ни увидеть результат, и все правила тандема писались под это. Теперь может: пайплайн
живого редактора (`unity command`) даёт ~150 команд, включая **рендер камеры в PNG**, а картинку Claude
читает прямо из файла. Пользователь остаётся нужен для того, что не видно в статике: как ИГРАЕТСЯ.

## 1. Поднять редактор

**Сначала выяснить, кто уже живёт.** `unity status` показывает GUI-редакторы; **batch-headless может в нём
не появиться** — это официальная гоча Unity CLI (в 6000.4.7f1 после `Start-Process` он появился, но
полагаться на это нельзя). Проверять так:

```powershell
unity status --format json
unity pipeline list --format json
```

- **GUI пользователя уже открыт** — работаем в нём, второй редактор не поднимаем.
- **Ничего не открыто** — поднимаем **фоновый persistent headless** (без `-quit`;
  `unity run` без `--command` для этого НЕ годится — он завершается сам):

```powershell
$exe = (unity editors --installed --format json | ConvertFrom-Json).data |
  Where-Object version -eq '6000.4.7f1' | Select-Object -First 1 -ExpandProperty location
$log = Join-Path (Get-Location) 'Temp\agent-unity.log'
$proc = Start-Process -FilePath $exe -ArgumentList @(
  '-batchmode','-projectPath',(Get-Location).Path,'-logFile',$log
) -WindowStyle Hidden -PassThru
$proc.Id
```

- **Готовность** — `unity command editor_status --project-path .` (повторять до `ready`)
  или `unity list --project-path .`; **не** `unity status` для batch.
- `unity open .` — GUI, остаётся жить; если терминал блокируется, запускать
  `Start-Process unity -ArgumentList 'open','.'`.
- `unity run . --command <name> -- ...` — **one-shot**: одна команда, редактор выходит;
  для серии команд/кадров не подходит.
- После готовности **всегда**:

```bash
unity command set_autotick --enable true     # без autotick Unity душит тик неактивного окна
```

Батч-режим годится для всего, что здесь описано, включая кадры (рендер идёт камерой, GUI не нужен).
Если открыт живой редактор пользователя — команды идут в него, поднимать второй нельзя.

**ФОНОВЫЙ РЕДАКТОР ЖИВЁТ ТОЛЬКО ПОКА ИДЁТ РАБОТА** (22.10: брошенный headless после обрыва сессии по лимиту
за час съел всю оперативную память, и машина пользователя зависла намертво). Autotick крутит редактор без остановки,
а сборщик тела держит копии мешей в кэше до конца жизни редактора — каждый собранный ряд и тест остаётся в памяти.
Поэтому в конце каждого захода работы, а не «когда-нибудь»:

```bash
unity command set_autotick --enable false
unity close . --timeout 30          # свой headless; НЕ сохраняет — сначала save_all
# запасной путь, если close недоступен:
unity command run_script --file Tools/Agent/Quit.cs --entry Quit.Run
# `unity command quit` в режиме редактора падает с 400 (DontDestroyOnLoad) — не им
```

Перед ожиданием (ждёшь другую линию, дизайнера, ответ пользователя) — тоже закрыть. Поднять снова дёшево (минута), а
брошенный редактор стоит машины пользователя. Долгая серия сборок (сотни тел) — перезапускать редактор между сериями.

## 2. Наши команды проекта

Меню `Chimera` зарегистрировано в пайплайне: ручных кликов не требуется.

```bash
unity command chimera-species     # пересоздать 5 SpeciesSO в Data/ (после правок SpeciesBootstrap)
unity command chimera-map         # КАРТА ТЕЛ — детектор: строит тела билдером и меряет стыки
unity command chimera-diagrams    # схемы планов тела по видам
unity command chimera-reports     # и карту, и схемы разом — обычный порядок сверки
unity command chimera-matrix      # МАТРИЦА ХИМЕР: каждое шасси с каждым донором, целиком и по аугменту → Docs/Диаграммы/ХИМЕРЫ.md
unity command chimera-silhouette  # СВЕРКА СИЛУЭТОВ: тело билдером против маски листа (IoU) → Docs/Диаграммы/СИЛУЭТЫ.md + маски тел
```

## 3. Тесты

```bash
unity command run_tests --mode editor   --async_tests true   # затем опрос
unity command run_tests --mode playmode --async_tests true
unity command test_status                                    # пока не "completed"
```

> **Тесты перед кадрами — проверь каталог форм.** До 17.09 `FormBlocksTests` оставлял билдеру пустой каталог, и после
> прогона EditMode виды, карта тел и кадры рисовали ригблоки кубами без ошибок. Починено (`MorphBuilder.ResetCatalog`),
> но признак стоит помнить: `MorphBuilder.MissingBlocks` не пуст при целом `Resources/Формы.asset` — статику испортил тест.

Новые файлы — сперва импорт и компиляция: `run_script` любого безобидного скрипта с
`AssetDatabase.Refresh()`, затем `unity command recompile_status` до `idle`/`completed`.

## 4. Кадры: посмотреть на существо

Инструмент — `Tools/Agent/ShotSpecies.cs` (лежит ВНЕ `Assets/`: это код агента, не игры; пайплайн
компилирует его в памяти). Собирает тело **настоящим `MorphBuilder`** и ставит ортокамеру по оси.

```bash
# вид на родном составе: profile | front | top  (front — камера перед мордой, звери смотрят в +Z;
# до 17.09 ночи front снимался со спины — старые «анфасы» в истории это вид сзади)
unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Build \
  --args '["Assets/_Chimera/Data/Волк.asset","profile"]'

# ПОЛОСА ПО ВЫСОТЕ — крупный план лап: кадр сужен до yMin..yMax метров от земли
unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Band \
  --args '["Assets/_Chimera/Data/Волк.asset","front",0.0,0.75]'

# КРУПНО ПО ДЕТАЛЯМ — кадр наводится на рендереры с этими именами (голова в профиль: полоса по высоте
# захватывает спину, и морда уходит за край)
unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Focus \
  --args '["Assets/_Chimera/Data/Волк.asset","profile","голова,Пасть,нос,глаза,уши"]'

# химера: шасси + орган(ы) донора, слоты через запятую
unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Chimera \
  --args '["Assets/_Chimera/Data/Человек.asset","Assets/_Chimera/Data/Волк.asset","Пасть,Чутьё","profile"]'

# снять кадр (save_path ВСЕГДА внутри Assets/ — пайплайн не пишет за пределы)
unity command capture_game_view --camera "ПрофильCam" --width 1100 --height 800 \
  --save_path "Кадры/Волк-профиль.png"

# посмотреть: Read на Assets/Кадры/Волк-профиль.png — картинка читается напрямую

unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Wipe
```

**Кадры из `Assets/` унести** (`mv` в `Docs/Диаграммы/Кадры/`) и папку снести: мусор в `Assets/` тянет
за собой `.meta` и лишний импорт. Эталонные кадры пяти видов лежат в
[`Docs/Диаграммы/Кадры/`](../../../Docs/Диаграммы/Кадры/) — после правки данных пересними и сравни.

**Чем нарисовано тело** (кости против мест) — одной строкой:

```bash
unity command eval --code 'var r = GameObject.Find("~ШОТ"); return r.GetComponentsInChildren<SkinnedMeshRenderer>().Length + " / " + r.GetComponentsInChildren<MeshRenderer>().Length;'
```

### Ряд формы-ключа — одной командой (15.10)

```bash
bash Tools/key-row.sh                          # Человек, ключ «Волк», веса 0 0.35 0.7 1
bash Tools/key-row.sh Человек Волк "0 0.5 1"
```

Тело шасси при каждом весе в профиль и анфас + нога крупно на последнем весе; листы в
`Docs/Диаграммы/Кадры/Ключ-<ключ>/ряд-<коммит>.png` и `нога-<коммит>.png`, на каждом подпись «вид · ключ · коммит ·
дата» (`+правки` — снято с незакоммиченных `Assets`). **Снимает та линия, что правит ключ, в своей папке и шлёт лист
геймдизайнеру сама** — координатор в этой петле не нужен (14.10 шесть проходов шли через него, и письма дважды
разминулись по номеру прохода). Вес вручную без ряда — `Tools/Agent/KeyShot.cs` (`On("Волк", 0.7)` / `Off()`).

## 5. Полигон: ряд тел в одном кадре

Сцена `Assets/Scenes/Полигон.unity` — стенд для сравнения. **В ней сохранены только свет и якорь**:
существа, линейка и камера строятся командой и НЕ сохраняются. Сцена с запечёнными телами стала бы вторым
источником правды рядом с данными видов и разошлась бы с ними молча.

```bash
unity command open_scene --path Assets/Scenes/Полигон.unity

# все пять видов в ряд (profile | front), аргумент 2 — отношение ширины кадра к высоте
unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Species --args '["profile",1.6]'

# ТРОЙКА СРАВНЕНИЯ: чистое шасси · химера · чистый донор
unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Compare \
  --args '["Assets/_Chimera/Data/Человек.asset","Assets/_Chimera/Data/Волк.asset","Пасть,Чутьё","profile",1.6]'

unity command capture_game_view --camera "СтендCam" --width 1600 --height 1000 --save_path "Кадры/Ряд.png"
unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Wipe
```

**Зачем ряд, а не отдельные кадры.** Смешение читается ТОЛЬКО в сравнении: кадр химеры сам по себе
выглядит нормальным телом, и лишь рядом с чистым шасси и чистым донором видно, потянулась ли пропорция.
В кадре есть линия земли и метки через 0,5 м — дефект называется числом прямо с картинки («холка ниже
метки 1,5»), как того требует правило «где и насколько».

**Сцену после работы не сохранять** (`save_scene` не звать): построенное — мусор для git.

**Полоска по оси** — то же тело при `min · канон · max` одного параметра:

```bash
unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Axis \
  --args '["Assets/_Chimera/Data/Волк.asset","cell",0.5,4.0,"profile",1.7]'
```

Оси: `blend` (слияние), `cell` (клетка поля), `thickness`, `section`, `depth`, `length` (кости),
`sizeRel` (места), `organScale`. Ответ содержит **треугольники** каждого образца — так видно и где
генератор ломается, и **какая ось вообще ничего не меняет**. Данные вида не правятся: правится копия.

> **Гоча, стоившая первого прогона:** `BoneMesher` кэширует оболочку по ключу
> `speciesName # число костей # слои [# грани]` — содержимое костей и числа поля в ключ не входят. Копиям
> обязательно давать РАЗНЫЙ `speciesName`, иначе все образцы придут из кэша одинаковыми.

**Тройка затенения** — одно тело гладким, гранями и гранями на грубой клетке (аргумент 1.5 — во сколько раз
клетка крупнее). По ней 17.09 выбран стиль: оболочка поля в игре гранями (`BoneMesher.Flat`). Стенд строит все три
гладкими и разваривает плоские сам, так что кадр повторяет тот, по которому принималось решение:

```bash
unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Shading \
  --args '["Assets/_Chimera/Data/Волк.asset",1.5,2.6]'
unity command capture_game_view --camera "СтендCam" --width 2100 --height 800 --save_path "Кадры/Затенение.png"
```

**Замер вместо «выглядит странно».** Кадр говорит ГДЕ, число — НАСКОЛЬКО. Для формы поля годятся срезы
оболочки: треугольники рассечь плоскостью `y = const` и взять ширину и глубину точек пересечения. Так 17.09
найдено, что на клетке 0.084 предплечье волка — стебель 2.5 × 1.5 см. Окно «вершины в ±Δy» врёт: на крупной
клетке ряды вершин редкие, и в окно попадают вершины с другой высоты.

## 6. Приёмка формы от модельной линии

Силуэтный граф приходит ФАЙЛОМ `Docs/models/handoff/<вид>-graph.json` (формат и инварианты —
`Docs/models/SPEC-priyomka-formy.md`). Узел графа — это `Bone`: второго описания формы в проекте нет.

```bash
# образец формата из нынешних данных (для модельной линии)
unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Export --args '["Волк"]'

# проверка поставки — печатает ВСЕ нарушения разом, ничего не меняет
unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Check --args '["Docs/models/handoff/volk-graph.json"]'

# перенос в данные вида (только при чистой проверке)
unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Import --args '["Docs/models/handoff/volk-graph.json"]'

# чистый лист: снять кости и скрытия — вид рисуется только местами
unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Clear --args '["Волк"]'
```

**Файл сильнее ассета.** `SpeciesBootstrap` при пересоздании видов сам подхватывает поставку
(`SpeciesHandoff.Apply`): есть файл — форма из него, **нет файла — формы нет вовсе** (кости и скрытия
обнуляются явно; костей в коде бутстрапа нет с 17.09). Поэтому `Graph.Import`
в ассет не «победит» следующее `chimera-species`, а совпадёт с ним. Битая поставка НЕ применяется
молча — ошибка в консоль, прежнее остаётся.

Порядок приёмки: `Check` → `Import` (или сразу `chimera-species`) → `chimera-map` → кадр на полигоне →
ответ письмом с числами и картинкой.

## 7. Что ещё есть (полный список — `unity command` без имени)

| нужно | команда |
|---|---|
| правка сцены/объектов | `create_gameobject`, `set_transform`, `add_component`, `set_component_properties`, `set_serialized_field`, `find_gameobjects` |
| ассеты | `create_asset`, `find_assets`, `set_import_settings`, `set_material_properties`, `search` (Unity Search) |
| префабы | `create_prefab`, `save_prefab_contents`, `apply/revert_prefab_overrides` |
| код без домен-релоада | `run_script` (энтрипойнт), `eval` (выражение), hot reload |
| консоль и статика | `console`, `audit` (Project Auditor) |
| игра | `editor_play`, `editor_stop`, `capture_game_view --source screen` (в Play) |

**Правки сцен и ассетов — командой или скриптом, не правкой YAML руками.**

### CLI-пути без живого редактора

- `unity test . --mode EditMode --output test-results.xml` — one-shot тесты без `unity command` и пайплайна.
- `unity run . --command <name> -- <args>` — one-shot зарегистрированная команда; редактор выходит сам.
- `unity open .` — GUI; `unity close .` — закрыть (не сохраняет; сначала `save_all`).

## 8. Гочи пайплайна

- **`save_path` и пути ассетов confined в `Assets/`** — всё, что вне, отвергается с 400.
- **Кириллица в аргументах работает** (имена ассетов, камер, слотов) — экранировать не нужно.
- **Долгая команда может быть модальным диалогом**, а не зависанием: проверить `editor_status`.
- **Асинхронные команды опрашиваются**: `recompile`→`recompile_status`, `run_tests`→`test_status`,
  `build`→`build_status`.
- **Кадр стоит токенов** (картинка в контексте): снимать по делу, а не «на всякий случай».
- **«Детали есть, тела нет» — мёртвый кэш оболочки** (исправлен 17.09): после выхода из Play Unity уничтожал
  меши, а статический кэш `BoneMesher` их отдавал. Если симптом вернулся — первым делом проверить кэш:
  `typeof(BoneMesher).GetField("cache", NonPublic|Static)`, живых мешей должно быть столько же, сколько записей.
- **Сверяй числа кадра с ассетом, а не с ожиданием.** 17.09 ряд на полигоне показал «чистые» габариты
  четырёх видов, а в ассетах лежали кости: оболочки просто пропали из мёртвого кэша. Число, совпавшее
  с желаемым, — ещё не проверка.
- **Batch-headless может не появиться в `unity status`** (официальная гоча CLI). Готовность проверять
  `unity command editor_status --project-path .` или `unity list --project-path .`; `unity status`
  не единственный признак живого batch-редактора.
- **СВОЙ РЕДАКТОР ЗАКРЫТ — КОМАНДА УХОДИТ В ЧУЖОЙ** (поймано 07.10). `unity command` без `--project-path`, не найдя
  редактора своей папки, молча берёт единственный живой — например, редактор модельной линии в соседней папке. Признак:
  тесты «не видят» новых тестов, лог своего редактора стоит. Перед работой — `unity status` (колонка `Project`), а в
  скриптах и при нескольких линиях — **всегда `--project-path <своя папка>`**. Чужой редактор не трогать.

## 9. Где проходит граница с пользователем

**Claude делает сам:** сборку и пересборку видов, тесты, отчёты-детекторы, кадры и оценку силуэта,
стыков и пропорций, правки данных и сцены, коммиты по зелёным тестам.

**Остаётся пользователю:** как игра ИГРАЕТСЯ — отзывчивость, читаемость боя, ощущение веса, звук,
баланс на ощупь. Это плейтест, и он по-прежнему обязателен для таких изменений.
