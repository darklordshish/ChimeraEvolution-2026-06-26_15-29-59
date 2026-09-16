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

```bash
unity status                     # порт/PID, если редактор уже отвечает
```

Пусто — редактор закрыт. Поднять headless (**без `-quit`**, иначе он выйдет сразу):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe" -batchmode -projectPath . -logFile <лог> &
```

Готовность — `unity status` (строка `ready`) или любая команда. Дальше **всегда**:

```bash
unity command set_autotick --enable true     # без autotick Unity душит тик неактивного окна
```

Батч-режим годится для всего, что здесь описано, включая кадры (рендер идёт камерой, GUI не нужен).
Если открыт живой редактор пользователя — команды идут в него, поднимать второй нельзя.

## 2. Наши команды проекта

Меню `Chimera` зарегистрировано в пайплайне: ручных кликов не требуется.

```bash
unity command chimera-species     # пересоздать 5 SpeciesSO в Data/ (после правок SpeciesBootstrap)
unity command chimera-map         # КАРТА ТЕЛ — детектор: строит тела билдером и меряет стыки
unity command chimera-diagrams    # схемы планов тела по видам
unity command chimera-reports     # и карту, и схемы разом — обычный порядок сверки
```

## 3. Тесты

```bash
unity command run_tests --mode editor   --async_tests true   # затем опрос
unity command run_tests --mode playmode --async_tests true
unity command test_status                                    # пока не "completed"
```

Новые файлы — сперва импорт и компиляция: `run_script` любого безобидного скрипта с
`AssetDatabase.Refresh()`, затем `unity command recompile_status` до `idle`/`completed`.

## 4. Кадры: посмотреть на существо

Инструмент — `Tools/Agent/ShotSpecies.cs` (лежит ВНЕ `Assets/`: это код агента, не игры; пайплайн
компилирует его в памяти). Собирает тело **настоящим `MorphBuilder`** и ставит ортокамеру по оси.

```bash
# вид на родном составе: profile | front | top
unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Build \
  --args '["Assets/_Chimera/Data/Волк.asset","profile"]'

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

## 6. Что ещё есть (полный список — `unity command` без имени)

| нужно | команда |
|---|---|
| правка сцены/объектов | `create_gameobject`, `set_transform`, `add_component`, `set_component_properties`, `set_serialized_field`, `find_gameobjects` |
| ассеты | `create_asset`, `find_assets`, `set_import_settings`, `set_material_properties`, `search` (Unity Search) |
| префабы | `create_prefab`, `save_prefab_contents`, `apply/revert_prefab_overrides` |
| код без домен-релоада | `run_script` (энтрипойнт), `eval` (выражение), hot reload |
| консоль и статика | `console`, `audit` (Project Auditor) |
| игра | `editor_play`, `editor_stop`, `capture_game_view --source screen` (в Play) |

**Правки сцен и ассетов — командой или скриптом, не правкой YAML руками.**

## 7. Гочи пайплайна

- **`save_path` и пути ассетов confined в `Assets/`** — всё, что вне, отвергается с 400.
- **Кириллица в аргументах работает** (имена ассетов, камер, слотов) — экранировать не нужно.
- **Долгая команда может быть модальным диалогом**, а не зависанием: проверить `editor_status`.
- **Асинхронные команды опрашиваются**: `recompile`→`recompile_status`, `run_tests`→`test_status`,
  `build`→`build_status`.
- **Кадр стоит токенов** (картинка в контексте): снимать по делу, а не «на всякий случай».
- Батч-редактор не видит `unity status` мгновенно — дать ему секунд десять на старт.

## 8. Где проходит граница с пользователем

**Claude делает сам:** сборку и пересборку видов, тесты, отчёты-детекторы, кадры и оценку силуэта,
стыков и пропорций, правки данных и сцены, коммиты по зелёным тестам.

**Остаётся пользователю:** как игра ИГРАЕТСЯ — отзывчивость, читаемость боя, ощущение веса, звук,
баланс на ощупь. Это плейтест, и он по-прежнему обязателен для таких изменений.
