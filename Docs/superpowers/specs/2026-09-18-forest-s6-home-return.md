# Лес s6: возврат сытых домой (крыша + пилот волка)

- **Статус:** ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 102/102 18.09; декой нужен для target)
- **Отменяет:** —
- **Продолжает:** `2026-09-18-forest-s2-lairs-areas.md`
- **Ревью связки:** `psyche-ecologist` (принять в минимуме) + `tester` (условно годен)

## Проблема

У логова есть адрес, но никто домой не возвращается: сытый волк бродит, крыша никому
не держится. Нужны крыша (место под вернувшегося) и пилот (решение идти).

## Решение (s6: механизм + 1 пилот, остальные психики — позже)

- `LairSite` += отдельный счётчик крыш (`reservedHolders: HashSet<int>`, НЕ в population —
  иначе врёт учёт выводка): `TryReserve(id)` (идемпотентен)/`Release(id)`/`ReservedCount`.
  Крыша: `population + reserved + queue <= capacity` — `IsFull` и `ConsumeSpawn` считают всё
  (иначе Tick/DrainOnce разойдутся). `ReportDeath` крыш не трогает (смерть вернувшегося —
  только `Release`).
- `CreatureBody.home: LairSite` — точка возврата (ставит спавнер при рождении или тест).
  `ForestSpawner` (s2b-правка тем же заходом): `body.home = site` после спавна.
- Пилот — `WolfPsyche` (одного вида достаточно: петля сыт→дом→отдых→голод/бой целиком
  проверяется): флаг `returnHomeEnabled` (по механике, дефолт false), ридонли `Homing`/
  `homeTarget` для тестов. Ветка ПЕРВОЙ в сытой ветке, до `Wander`, гейты `!Engaged`
  (ветка) + `mooseTarget == null` + `!Routing`: не дома → идём (`DirTo` + `Settle`),
  дома → `TryReserve` + отдых (`Settle(zero)`). `Release` при уходе/бое/панике/смерти
  (`OnDestroy` + проверка в `Update`), гистерезис не нужен — `IsNearHome` радиуса хватает.
  Идём к СВОЕМУ `home`, не к `NearestHome`.

Принято от связки: счётчик отдельно + пилот на волке + радиуса достаточно +
флаг по механике + инвариант крыши в `IsFull`/`ConsumeSpawn`.

## Инварианты

EditMode `Tests/EditMode/Forest/Habitats/HomeRoofTests.cs` (5 тестов): резерв идемпотентен,
полная крыша блокирует `ConsumeSpawn`, `Release` освобождает, `IsFull` считает резерв,
`ReportDeath` резерв не трогает.
PlayMode `Tests/PlayMode/Forest/Habitats/HomeHomingTests.cs` (2 теста, изолированная сцена):
`SatedWolf_HomesToOwnLair` (сыт + дом + флаг → `Homing` и цель = дом),
`NoHome_NoHoming` (без дома — брожение как раньше).

## Что НЕ делаем

- Пилоты змеи/лося/ежа, деспавн вернувшихся (нужны правила видимости), возврат через
  `NearestHome` (только свой дом), чистопородные логова.

## Приёмка

`run_tests --mode playmode` зелёный (2 новых) + `run_tests --mode editor` без регрессий
(правка `IsFull`/`ConsumeSpawn` покрыта s2-тестами).
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
