# Лес s2: логова и ареалы (данные + правила + учёт, без хука спавнеров)

- Статус: ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (EditMode 134/134 + 1 старый inconclusive 18.09; связка учтена)
- Отменяет: —
- Отменена: —
- Ревью связки: `psyche-ecologist` (условно годно) + `tester` (принять) + `unity-integrator` (условно ОК)

## Проблема

Спавнеры сыплют по прямоугольнику без адреса: у стаи нет дома, у страха нет места,
у ареала нет границ. Нужно логово как объект с капом — но не ломая спавнеры и сцену.

## Решение (s2: только данные + математика, хук спавнеров — s2b)

`LairSite` (`Scripts/Forest/Habitats/`, `MonoBehaviour`, в сцену в ЭТОМ срезе не кладём):

- Кто/где: `speciesName`, `areaType` (`Den` логово / `Burrow` нора / `Nest` гнездо / `Rest` лёжка),
  `homeRadius` (дом), `spawnRadius` (разброс спавна), `fearRadius` (радиус страха — отдельно),
  `entrances` (локальные смещения входов: нора ежа — сеть, а не точка).
- Мощность: `tier` 1..3, `capacity`, `respawnSeconds`.
- Страх: `fearPerKill/decay/exhaustFear`; `ReportKill(pos)` — мимо `fearRadius` не пугает
  (килл через полкарты не кладёт логово); сила = `fearPerKill · 2/tier` (мелкое логово хрупко).
- Рантайм (`[NonSerialized]`, в YAML не оседает): `population/pendingSpawns/fear/timer`.
- `Tick(dt)` (dt явный, без `Time.*`): спад страха → очередь до капа → события `Exhausted/Recovered`
  на пересечении порога. `IsFull`, `IsNearHome(pos)`, `ConsumeSpawn` (pending→population).
- `LairRegistry`: `Register/Unregister/TotalCapacity(species)/NearestHome(species,pos)` (без статики).
- `LairRules` (чистые, без GO): `CheckSite/AddFear/DecayFear/IsExhausted`.
- `Reserve` для вернувшихся сытых — НЕ делаем (владелец — хук спавнера, s2b).

Принято от связки: адрес страха + tier-множитель + AreaType/входы/радиусы + события +
скрытие рантайма + long-tick assert. Отклонено: фолбэк-обёртки «0 = не настроено»
(единый механизм — `CheckSite`, а не двойная семантика).

## Инварианты (EditMode `Tests/EditMode/Forest/Habitats/LairRulesTests.cs`, 8 тестов)

Валидация (битый/чистый сайт), страх (кламп/спад/порог 0.69/0.7), очередь (до капа 2,
стоит на капе при долгих тиках), страх блокирует очередь и отпускает после спада,
события на пересечении, реестр (total 3+4=7, nearest, unregister чистит).

## Что НЕ делаем

- s2b: хук спавнера через публичный API (`SpawnerHook_DrainsQueue_SpawnsInRadius`,
  при `exhausted` спавна нет) + `Reserve`. Существующие спавнеры/сцена/префабы — только чтение.
- Лёжка как хилка, гнездо кладкой — отдельные типы поведения позже (сейчас общий учёт).
- `LairSite` в сцену руками — следующим шагом, не в этом срезе.

## Приёмка

`run_tests --mode editor` зелёный (8 новых) + `git diff --name-only` без `*.unity/*.prefab/*.asset`.
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
