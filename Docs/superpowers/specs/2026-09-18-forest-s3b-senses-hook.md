# Лес s3b: хук погоды/ночи в чувства (минимальные умножения)

- **Статус:** ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 91/91 18.09: 1 новый файл + 3 однострочника)
- **Отменяет:** —

- Продолжает: `2026-09-18-forest-s3-night-weather.md`
- Ревью связки: `combat-senses` (условно ЗА) + `tester` (принять)

## Проблема

Модификаторы s3 висят в воздухе: чувства их не читают. Нужен хук — минимальными
умножениями поверх, без замены логики.

## Решение (s3b: 1 новый файл + 3 однострочные правки)

Новый `ForestClimate` (`Scripts/Forest/Climate/`, `MonoBehaviour`): `state` (вид+ветер),
`timeMinutes` (тикает `Update`, `timeScale` игровых минут в секунду, wrap 30),
статики `CurrentState/CurrentTimeMinutes` (дефолт ясно/день 10:00) + `RangeMult(kind)` /
`CueDurationMult()` / `ScentLifetimeMult()` / `IsNight()` + `ResetStatic()` (сброс в `TearDown`
по прецеденту `MorphBuilder.ResetCatalog`).

Три правки существующего (только умножения):
- `Senses.Range`: `× ForestClimate.RangeMult(k)` — Sight: видимость (+ночь ×0.7, только глаза;
  Thermal исключён: змея ночью видит теплом, темнота ей не помеха), Scent: нюх, Hearing: слух.
  `Sees/PlayerPerceives` не трогаем — берут уже помноженный Range.
- `Noise.Spike`: длительность `× CueDurationMult` (сила клампится в 1 — бустим длительность,
  иначе вой в тумане не погромчеет). `Noise.Hear` не трогаем — range уже помножен, иначе слух
  режется дважды.
- `ScentField.Update`: `cutoff` через `lifetime × ScentLifetimeMult` (дождь смывает следы,
  а не только нос).

Принято от связки: без дубля слуха + ночь только зрению + cue в длительность +
статик со сбросом + попарные времена (10 vs 25, клок не тикает).
Outline телеграфов — s3c (нужен выбор техники). Дрейф спор — позже (трогает Deliveries).

## Инварианты (PlayMode `Tests/PlayMode/Forest/Climate/ClimateHookTests.cs`, 4 теста)

`Rain_KillsScent` (нюх 0 + след смыт за кадр), `Fog_Day_CutsSight` (туман/день: зрение ×0.45,
слух ×0.8), `Clear_Night_SightDown_ThermalKept` (ночь: зрение ×0.7, термо цел), `Clear_Day_NoOp
(хук прозрачен: всё как засеяно). Время выставляется явно, клок запрещён.

## Что НЕ делаем

- Outline/pulse на рендере (s3c), свет/VOLUME, дрейф, правки психик/доставок.
- `Reserve`/возврат (s2b-контекст, не здесь).

## Приёмка

`run_tests --mode playmode` зелёный (4 новых) + `run_tests --mode editor` без регрессий.
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
