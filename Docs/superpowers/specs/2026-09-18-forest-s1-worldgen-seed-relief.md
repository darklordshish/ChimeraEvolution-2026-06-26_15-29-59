# Лес s1: сид + рельеф (высотное поле, без аппликатора)

- Статус: ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (код + 5 тестов зелены 18.09, EditMode 130/130 + 1 старый inconclusive)
- Отменяет: —
- Отменена: —

## Проблема

Лес должен быть генерируемым из сида (п.1 брифа), но в проекте нет ни сида, ни источника высот:
арена плоская, спавнеры сыплют по прямоугольнику. Первый шаг — детерминированное поле высот
как данные + инварианты, без привязки к сцене (сцену и NavMesh трогать нельзя: чужая зона,
да и аппликатор — отдельный слайс s1b в тех же папках `Scripts/Forest/`).

## Решение

Мир = чистая функция `высота = FBM(seed, x·freq, z·freq) · amplitude`:

- `WorldGenConfigSO` (`Scripts/Forest/Generation/`) — только данные: `seed/version/octaves/
  baseFrequency/amplitude/mapHalfExtent/maxSlopeDegrees`. Ассет живёт в `Data/Forest/Worldgen`
  (создаётся в Unity пунктом меню, руками YAML не правим).
- `SeededHash` (`Scripts/Forest/Common/`) — детерминированный 2D-хэш (splitmix64), без Unity.
- `ValueNoise2D` — value-noise + FBM, нормированный на сумму амплитуд → строго `[-1, 1]`.
- `WorldHeightField` — `SampleHeight/SampleGridHash/MaxSlopeDegrees` в мировых координатах.
- `WorldRules` — инварианты как у `BodyRules`: `CheckConfig` (диапазоны) + `CheckGrid`
  (конечные высоты в `±amplitude`, макс. уклон ≤ лимита; лимит по умолчанию 35° — под CC/NavMesh).
- Потоки RNG раздельные по смыслу (октавы — сдвигом сида): добавление травы позже не сдвинет зайцев.
- Смена алгоритма = смена `version`: тот же сид на новом `version` даёт другую карту (урок Minecraft);
  `version` участвует в хэше сетки, тест это сторожит.

## Инварианты (сторожат тесты `Tests/EditMode/Forest/Worldgen/`)

- Один сид дважды → тот же хэш сетки (`SameSeed_SameGridHash`).
- Разные сиды → разное поле (`DifferentSeeds_DifferentGridHash`).
- Сетка 32×32 шаг 1м чиста по `WorldRules` (`Heights_WithinAmplitude`).
- `octaves=0` ловится правилами, а не молчит (`BadConfig_IsReported`).
- Смена `version` меняет поле (`Version_ParticipatesInHash`).

## Что НЕ делаем (следующие слайсы, те же папки — пересечений нет)

- s1b: аппликатор террейна + NavMesh-bake + PlayMode-проверка связности (`Sample+CalcPath`).
- s2 (`forest/s2-lairs-areas`): логова/капы. s3: климат/погода. s4: руки/инструменты.
- Биомы, scatter растительности, LOD/SRP — после финальных мешей (см. бриф: бюджет лося ×3.6).

## Приёмка

`run_tests --mode editor` зелёный (5 новых тестов) + `WorldRules.CheckGrid` пуст на дефолтном конфиге.
Плейтест не нужен (логика/числа — по таблице приёмки `CLAUDE.md`).
Обновление `Docs/СТАТУС.md` — за `docs-keeper` при мердже в `infra/forest`, не здесь.
