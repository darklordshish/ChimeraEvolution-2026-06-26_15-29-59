# Лес s2b: хук спавнера логовищ (очередь → живые звери)

- **Статус:** ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 87/87 18.09; lastSpawn вместо поиска по имени)
- **Отменяет:** —

- Продолжает: `2026-09-18-forest-s2-lairs-areas.md`
- Ревью связки: `psyche-ecologist` (принять как хук) + `tester` (принять с доработкой: 3 теста)

## Проблема

Очередь логова никого не рожает: `pendingSpawns` копится, а тел в сцене не прибавляется.
Нужен мост «очередь → фабрика», не трогая спавнеры, сцену и префабы.

## Решение (s2b: спавнер поверх публичного API)

`ForestSpawner` (`Scripts/Forest/Habitats/`, `MonoBehaviour`):

- `Configure(registry, speciesPool)`: реестр + инжект списка видов (в сцене — из
  `EvolutionConfig.AllSpecies` руками, в тесте — `CreateInstance`; поиск вида по
  `speciesName` внутри, наружу строкой не торчит).
- `Tick(dt)`: тикает сайты + `DrainOnce` по каждому (сцена; тест дёргает `DrainOnce`
  напрямую, без тиков — иначе дозаполнение флакает).
- `DrainOnce(site)`: guards (null, pending 0, популяция у капа, `IsExhausted`, вида нет
  в пуле — очередь цела) → позиция = центр + детерминированное смещение в `spawnRadius`
  (счётчик + `SeededHash`, без `Random`) → `ChimeraFactory.Spawn` (шасси = вид,
  доноры = весь пул — как обычные NPC; выводок = обычный NPC, не каста) →
  `site.ConsumeSpawn()` → подписка `onDeath`.
- Смерти: `OnSpawnedDeath` → `home.ReportDeath()` + все логовища того же вида в их
  `fearRadius` от точки смерти получают `ReportKill` («запах смерти», не память о хищнике;
  падёж тоже глушит — честно записано, сторожится дистанцией).
- `Reserve` для вернувшихся сытых — НЕ делаем (возврат домой — уровень психик + минимум
  `body.home/TryReserve`, позже).
- Единственная правка s2-файла: `LairRegistry.Sites` (аддитивный геттер перечисления,
  поведения не меняет).

Принято от связки: donors=пул (насос ускоряет существующую эволюцию — не баг) +
смерть→страх географией + 3 теста вместо 1 + `trash`/`TearDown` cleanup.

## Инварианты (PlayMode `Tests/PlayMode/Forest/Habitats/SpawnerHookTests.cs`, 3 теста)

`DrainOnce_DrainsQueue` (pending−1, population+1, живой Health в радиусе, имя выводка);
`Spawned_InRadius_NotWhenExhausted` (точка в spawnRadius; при fear=1 — отказ, очередь цела);
`Death_ReportsAndScares` (убийство → population 0, fear>0).

## Что НЕ делаем

- Правки спавнеров/сцены/префабов/психик — только чтение. Слой логовищ на карте (ждёт s2b?
  нет — ждёт реальные сайты в сцене; карта сейчас, точки позже).
- Чистопородные логова (`donors=[chassis]`) — флагом позже.
- `LairSite` в сцену руками — следующим шагом.

## Приёмка

`run_tests --mode playmode` зелёный (3 новых) + `run_tests --mode editor` без регрессий.
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
