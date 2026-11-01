# План S3 — интеграция и порядок: один пол, маски, места

**Дата:** 2026-11-01. **Спека:** [`2026-11-01-landshaft-s3-integraciya-i-poryadok.md`](../specs/2026-11-01-landshaft-s3-integraciya-i-poryadok.md).
**Ветка:** лесная (`ChimeraEvolution-les`). **Статус:** ЧЕРНОВИК · решения НЕ ПРИНЯТЫ.
**Исполнять после утверждения развилок Р-1…Р-3** (спека §12). Пока они открыты, шаги 3 и 7 идут в двух вариантах — это помечено.

**Жёсткие рамки исполнения:**
- `DomeGenConfigSO.version` **не менять** — он входит в H0 (`DomeHeightField.cs:57`, `:88`) и в каньон (`DomeCanyon.cs:85`) у всех сцен.
- Сцены `ForestSandbox` (A) и `ForestFragment` (B0) **не править**; в них только проверять, что числа прежние.
- Правки — только в `Assets/_Chimera/Scripts/Forest/` и `Assets/_Chimera/Tests/*/Forest/`; `Docs/` — на шаге 9.
- Прогон: `unity command set_autotick --enable true` → `run_tests --mode editor --async_tests true` → опрос `test_status`; затем PlayMode. Без autotick тесты висят.
- Каждый шаг заканчивается зелёными тестами; коммит — в точках §«Коммиты», на русском, тематически.

## Предусловия

- Текущая база: EditMode 424 (421 passed, 1 skipped `KeyPose`, 2 известных inconclusive), PlayMode 170/170 (01.11).
- Эталоны A/C/B0: A — 52 чанка, C — 9 змеиных + сова без спавна, B0 — 12 чанков (`ForestBaselineTests.cs:47`, `:60`, `:73`).
- Сцена павильона: `ForestPavilion.unity:743–771` — Ø2000, сид 1337, `chunkSize 250`, `terrainQuads 48`, `treeCount 1500`, `patchyFlora 1`, `buildTerrace/buildCanyon/buildStationHill/buildLabComplex/buildClimate/buildStationLoop 1`, `buildRavines 0`, `labEntrance Rock`.
- Сегодня `buildRelief` нет; P5-lite включается через `patchyFlora` (`DomeWorld.cs:240–241`); `worldVersion` нет — есть `cfg.version` и `ForestSpace.LayoutVersion` (`ForestSpace.cs:15`).

---

## Шаг 1. Зафиксировать базовую линию (сторож, без кода мира)

**Цель:** иметь чем доказать «A/C/B0 не изменились» и «тот же сид — тот же мир» до правок.

**Файлы:** `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestBaselineTests.cs` (+ константы), новый `Assets/_Chimera/Tests/EditMode/Forest/Worldgen/BaselineFingerprintTests.cs`.

**Действия:**
1. Записать `Fingerprint` для A (`ForestSandbox`, 1337), C (та же сцена с пулом змей), B0 (`ForestFragment`, 9), Pavilion (1337) — константами с датой.
2. Записать `SamplingContract` павильона (`meshStep`, `slopeEpsilon`, `routeCell`, `navVoxel`) и `PlatformY`.
3. Тест `Baseline_A_C_B0_Fingerprints` — сравнение с константами; тест `Baseline_Pavilion_Counts` — 52/1500/9 логовищ и т. д. (по факту).

**Проверка:** новый тест зелёный до правок; полный EditMode/PlayMode зелёный.

**Результат:** эталон в тестах, а не в пересказе.

## Шаг 2. `DomeTerrain`: вынести порядок и оставить один пол

**Цель:** шаг формы перестаёт быть двумя приватными функциями `DomeWorld` с разным смыслом.

**Файлы:** новый `Assets/_Chimera/Scripts/Forest/Generation/DomeTerrain.cs`; правки `DomeWorld.cs`; тесты `TerrainOrderTests.cs` (PlayMode).

**Действия:**
1. Перенести `BaseGround` (`DomeWorld.cs:363–427`) и `SampleGround` (`:357–361`) в `DomeTerrain` с явным списком слоёв **в сегодняшнем порядке**:
   `hydro → terrace → canyon → ravines → hill → landform → ramp → crevice → pad`.
   Это важно: слои пока пересекаются (масок нет), и перестановка `landform`/`hill` уже изменила бы пол. Целевой порядок §2.1 спеки (`landform → hill`, «широкое → местное») вводится на шаге 4 вместе с масками.
2. `DomeWorld.SampleGround` — фасад на `DomeTerrain.Ground`; `TerrainGround` удалить (её читает только сверка станции, `:220` — шаг 6 заменит её на `DomeTerrain.Shape`);
3. `BaseGround` наружу не выходит; `RawGround` остаётся диагностикой.
4. Тесты: `TerrainOrderTests.Flags_Off_Equals_Raw_Outside_Pad`, `TerrainOrderTests.Pad_Is_Last`, `TerrainOrderTests.Shape_Excludes_Pad` (площадка не входит в `Shape`).

**Проверка:** `Fingerprint` A/C/B0/Pavilion **до и после** совпадают побитово (шаг 1); `Geography_Off_IsNoOp` (`ForestPavilionTests.cs:283–292`) зелёный; EditMode/PlayMode без изменений.

**Результат:** один порядок в одном классе; поведение не изменилось (рефактор).

## Шаг 3. `buildRelief` и версия порядка (развилка Р-1)

**Цель:** P5-lite перестаёт быть привязанным к B-lite; появляется версия, которой можно двигать порядок, не двигая H0.

**Файлы:** `DomeGenConfigSO.cs` (новое поле), `ForestBuilder.cs` (`[Header]`, передача в мир), `DomeWorld.cs` (условие `:240`), `DomeTerrain.cs`, `WorldFingerprint.cs`, сцена `ForestPavilion.unity`; тесты `TerrainOrderTests.cs`.

**Действия:**
1. Поле `buildRelief` (по умолчанию `false`), проброс `ForestBuilder → ownedConfig → DomeWorld`; `patchyFlora` остаётся только B-lite.
2. Новое поле `terrainVersion = 1` в `DomeGenConfigSO` (не `version`); участвует в солях решений формы и в отпечатке.
3. Сцена павильона: `buildRelief: 1` явно; A/C/B0 не получают поле (0 = выключено, как гоча нового `SerializeField`).
4. **Вариант Б (если Р-1 решат «оставить связку»):** шаг не делается, вместо него в `DomeTerrain` документируется правило «P5-lite = часть B-lite», и шаг 8 обязательно смешивает флаг `patchyFlora`; тест «рельеф без арен» не пишется.

**Проверка:** A/C/B0 `Fingerprint` не изменились; `buildRelief = 0` → `SampleGround` равен шагу 2 (тождество); `buildRelief = 1` → отпечаток Pavilion другой, `ReliefHills ≥ 3` (`ForestPavilionTests.cs:246–261`). Сторож: `Relief_Needs_Flag_Not_Patchy`.

**Результат:** флаги развязаны, версия порядка отдельная.

## Шаг 4. Габариты и маски вместо расстояний между центрами

**Цель:** ни один слой формы и ни одно место не пересекаются «по центру».

**Файлы:** новый `Assets/_Chimera/Scripts/Forest/Generation/DomeFootprint.cs`; правки `DomeRelief.cs`, `DomeRavines.cs`, `DomeTerrace.cs`, `DomeCanyon.cs`; `ForestSpace.cs` (подготовка), `DomeAshSite.cs`; тесты `Assets/_Chimera/Tests/EditMode/Forest/Worldgen/DomeMaskTests.cs`.

**Действия:**
1. `Footprint`: `Circle`, `Capsule`, `OBB`, `Polygon` + `Overlaps` (спека §3.1); для полигона — растр `max(2 м, routeCell)`.
2. `MaskSet`: `station` (OBB 46×36 + `PadBlend 6` + рампа + щель), `entry` (R12 + портал), `ramp`, `water` (озеро/русло/плёс/полотно), `arena` (R30 + 2), `route` (капсула + 6), `ring` (`rin−30 … rout`).
3. Переписать запреты: `DomeRelief.cs:42–47`, `:65–70` → `Overlaps`; `DomeRavines.cs:39–40`, `:52`, `:65–71` → габарит оврага (центр + полуось вдоль `angleDeg`); `DomeAshSite.cs:58–65` → крона 140 и габарит; `DomeTerrace`/`DomeCanyon` уже считают `reach`/полилинию — привести к `Footprint`.
4. Отрицательные фикстуры: холм `R=70` с центром в 55 м от арены R30 и гряда с полуосью 70 — обязаны быть отвергнуты.
5. После масок переставить `landform` и `hill` в целевой порядок §2.1 («широкое → местное»). Габариты уже не пересекаются, поэтому `Fingerprint` обязан остаться прежним — это и есть проверка, что перестановка ничего не сдвинула.

**Проверка:** `DomeMaskTests` (пары + фикстуры); `Landform_NoFootprintOverlap` на 20 сидах (станция, вход, рампа, вода, кольцо, арены при включённых B-lite/relief); `ForestPavilionTests` зелёный.

**Результат:** «свободно» доказывается пересечением габаритов, а не расстоянием до центра.

## Шаг 5. Арены, коридоры и поды — на финальном полу

**Цель:** арена не встаёт на холм, коридор не упирается в склон.

**Файлы:** `DomeWorld.cs:272–285`, `ForestSpace.cs:29–59`, `:75–118`, `:190–226`; `ForestSpace.LayoutVersion`; тесты `ArenaFootprintTests.cs`, `CoverPodTests` (PlayMode).

**Действия:**
1. `ForestSpace.Build(...)` получает `ShapeGround` (не `BaseGround`); проверка уклона — 8 точек кромки + центр, порог 22°; вода — полигоном; кольцо — полосой; станция — OBB.
2. Коридоры строятся между принятыми аренами и валидируются: уклон вдоль ленты ≤ 22°, нет воды/кольца/станции, ширина ≥ 16 м; иначе ребро заменяется на следующее по длине (правило в `BuildCorridors`).
3. Поды: `PodSpotOk` — габарит 3 м + зазор, а не точка.
4. `LayoutVersion` → `2` (правило размещения изменилось). **В отпечатке смешивать `LayoutVersion` только при включённой разметке** (`patchyFlora`), иначе рост версии поедет и по A/C/B0, где разметки нет (`WorldFingerprint.cs:35` смешивает безусловно). Отпечаток обязан измениться у Pavilion и не измениться у A/C/B0.

**Проверка:** `ArenaFootprintTests` — 5 арен (4–6), уклон по кромке ≤ 22°, ≥ 2 выхода; `Corridors_Walkable` — NavMesh проходит по ленте (не только `ContainsCorridor`); `Clearings_ArePresent_AndDry` переписан на кромку; A/C/B0 фингерпринты прежние (у них B-lite выключен).

**Результат:** арены и коридоры живут на том же полу, что чанки и NavMesh.

## Шаг 6. Станция и вход: поиск на `ShapeGround`, площадка последней

**Цель:** станция не на склоне, геометрия не утоплена, дверь/рампа на месте.

**Файлы:** `DomeWorld.cs:157–166`, `:215–240`, `:264–267`, `:955`; `DomeLabSite.cs:104–181`, `:281–343`; `DomeTerrain.cs`; тесты `StationOnShapeTests.cs` (PlayMode).

**Действия:**
1. `DomeLabSite.Find` получает `Func ground` = `DomeTerrain.Shape` (hydro + terrace + canyon + ravines, **без landform и без hill**); `Accept` остаётся прежним (пятно + подход, `:303–333`).
2. Landform получает маску `station` по найденному пятну + `PadBlend` + рампа + подход 16/24 м — значит, маржи, посчитанные на `Shape`, остаются верными.
3. `platformY` — из того же `Shape` в центре (`site.groundY`), а не из `TerrainGround`; сверка `:215–230` удаляется (её роль забирает порядок).
4. Площадка/рампа/щель — последний `Blend`; `LabComplex.Build(... platformY, ...)` (`:955`) обязан совпасть с `SampleGround(LabCenter)` (допуск 0.05 м) с учётом `Plinth`.
5. Вход: XZ прежний (`:796–803`), высота `SampleGround`; маска `entry` не пускает к нему форму.

**Проверка:** `StationOnShapeTests` — 20 сидов: маржи `min ≥ 0`, перепад ≤ 4 м `SampleGround` по пятну, уклон ≤ 20°, плита плоская, `|SampleGround(LabCenter) − platformY| ≤ 0.05`; `LabDoor_To_Atrium_IsComplete` (`ForestPavilionTests.cs:113–132`); `StationLoop_Crevice_IsNotBuriedByGround` (`:263–280`); `Clear` пуст.

**Результат:** станция проходит приёмку на том полу, который строится.

## Шаг 7. Кольцо: маска и маршрут (развилка Р-3)

**Файлы:** `DomeSlope.cs`, `ForestBuilder.cs:478–490`, `:504–520`; `DomePass.cs:15–45`; `ForestRouteGraph.cs`; тесты `RouteParityTests.cs`.

**Действия (вариант А, рекомендация):**
1. `SampleGround` кольцо не содержит (как сегодня) — правило закрепляется тестом `Ring_Not_In_SampleGround`.
2. `ring` — маска для landform/ravines/arenas/маршрутов (§3.2).
3. Одна функция `RingFloor(x,z) = max(SampleGround, ring)` для устьев/гнёзд/ясеня; `BowlPad` и двойной `max` из `ForestBuilder.cs:406–407`, `:421–422`, `:517–518` сводятся к ней (или удаляются).
4. `DomePass.BlockedGrid` получает `ShapeGround` (не сырое поле) и блокирует кольцо полосой; пятно станции — **прямоугольником** (круги по деталям запечатывают дверь, это уже поймано в B0.1); `ForestRouteGraph` — тот же пол.
5. **Вариант Б (если Р-3 решат «в поле»):** отдельная спека-шип; шаг не делается, маски `ring` остаются.

**Проверка:** `Ring_Not_In_SampleGround`; `RouteRadius_Le_Rin` на 20 сидах; `DomePass` и NavMesh согласны у входа (`B0`: путь внутри `rin`); спавнер и адрес логова читают одну функцию.

**Результат:** кольцо — стена и маска, а не третий пол.

## Шаг 8. Отпечаток и след

**Цель:** два разных мира не могут иметь один `Fingerprint`; каждый слой объясняет отказ.

**Файлы:** `WorldFingerprint.cs:12–73`; `GenTrace` вызовы в `DomeWorld.cs`; тесты `FingerprintLayersTests.cs`, `GenTraceTests.cs`.

**Действия:**
1. Смешать: hills (центр/радиус/высота/ось/полуоси/`ridge`), terrace, canyon (полилиния/ширина/глубина/плёс/лип), ravines, hill, `platformY`, `padYaw`, `rin/rout`, `lakeLevel`, `terrainVersion`, `LayoutVersion` (только при включённой разметке), флаги `buildRelief/buildRavines/buildTerrace/buildCanyon/buildStationHill/buildStationLoop`.
2. Не полагаться на сетку 32×32: гряда полушириной 12.5–17.5 м проваливается между узлами 62.5 м (Ø2000) — проверить фикстурой.
3. `GenTrace`: `landform` (кандидаты/принятые + отказы: вода/станция/кольцо/габарит), `hill`, `pad`, `space`, `water`, `ring`, `entry`, `flora` (бюджет).
4. Эталоны A/C/B0 из шага 1 — сравнение; Pavilion — новый эталон с датой.

**Проверка:** `FingerprintLayersTests` — тот же сид/флаги → тот же хеш; флип каждого флага/слоя → другой; `GenTrace` содержит причины; A/C/B0 хеши равны эталону.

**Результат:** «тот же мир» проверяется, а не обещается.

## Шаг 9. Документы и приёмка

**Файлы:** `Docs/Диаграммы/Лес/ГЕОГРАФИЯ.md` (снимок чисел), `Docs/УКАЗАТЕЛЬ.md` (строка S3 — **обязательна для гейта** `spec_status.py`), `Docs/СТАТУС.md` §5, шапка спеки S3 (статус после утверждения), `Docs/Диаграммы/Лес/Кадры/ИНДЕКС.md`.

**Действия:**
1. Кадры «до/после»: арена на склоне, станция у двери, берег озера, край кольца, сверху с сеткой полян. Съёмка — `chimera-unity` (`capture_game_view`).
2. Числа в `ГЕОГРАФИЯ.md`: `SampleGround` в контрольных точках, число арен/коридоров/подов, отпечатки.
3. Прогон `python Docs/tools/spec_status.py` — до записи в `УКАЗАТЕЛЬ` он красный по строке «не упомянута»; после — зелёный.
4. Полный EditMode/PlayMode; плейтест — за геймдизайнером (третья строка приёмки).

**Проверка:** `spec_status.py` зелёный; тесты зелёные; кадры в индексе; плейтест отмечен как непройденный, пока его нет.

**Результат:** S3 принята по числам и кадрам, не по ощущению.

---

## Коммиты

| после шага | сообщение |
|---|---|
| 1–2 | `лес: S3 — один пол, порядок слоёв вынесен в DomeTerrain` |
| 3–4 | `лес: S3 — buildRelief отдельно и маски по габариту` |
| 5–6 | `лес: S3 — арены и станция на ShapeGround, площадка последней` |
| 7–8 | `лес: S3 — кольцо в масках, отпечаток и след по слоям` |
| 9 | `лес: S3 — документы, кадры, приёмка` |

Перед каждым коммитом: `git status`, `git diff` (только свои файлы), тесты зелёные. Push — после коммита. В `main` — через гейт `Tools/git-hooks/gate-main.sh`, `--no-verify` запрещён.

## Зависимости

| от кого | что нужно | что блокирует |
|---|---|---|
| **S1** (математика форм) | имена, параметры и **габариты** форм (холм, гряда, терраса, каньон, овраг); оператор `Lift/Carve/Blend` | шаг 4 (маски по габаритам) ждёт хотя бы типов габаритов; высоты/радиусы S3 не задаёт и не пересчитывает |
| **S2** (композиция) | сколько арен/коридоров/подов и какие связи обязательны | шаг 5: без S2 берётся текущий B-lite (5 арен, 6 коридоров, 24 пода) и `LayoutVersion 2`; S2 может поменять числа — порядок и маски от этого не меняются |
| **S4** (эрозия) | офлайн-результат и версия | слот в порядке уже назван (shape → erosion → POI); S3 ничего не делает, но слот не должен быть занят post-pad слоем |
| **S5** (верификация) | полный 20-сидовый прогон, кадры, сводка | S3 даёт контракт (`Fingerprint`, `GenTrace`, `SamplingContract`); S5 владеет приёмкой и не переписывает порядок |

## Риски плана

1. **Шаг 2 «рефактор без изменений»** — риск, что вынос в `DomeTerrain` уже поменяет порядок. Сторож: побитовое равенство отпечатков A/C/B0/Pavilion; нет равенства — откат шага.
2. **Шаг 5 меняет разметку** — арен станет меньше (отказ по кромке). Это ожидаемо; тест держит 4–6, но при 4 надо сообщить, а не «дотянуть» пятую хаком. `LayoutVersion` растёт — старые миры с тем же сидом дадут другую разметку, и это надо объявить.
3. **Шаг 6 меняет место станции** на части сидов (поиск на другом поле). A/C не трогаются (у них `buildLabComplex 0`), но B0/Pavilion могут переехать; тесты B0 ждут 12 чанков, а не конкретное место — место пишется в лог и в отпечаток.
4. **Порядок «площадка последней»** делает борт площадки резче (blend 6 м). Кадр обязателен; если борт читается обрывом — увеличивать `PadBlend` решением, а не возвратом landform после pad.
5. **Кольцо в масках на Ø400** может отрезать часть полян (там арифметика уже сейчас на грани: `0.78·120 + 30 = 124 > 120`). B0 B-lite не включает, но при включении — меньше арен. Это честный отказ, а не регресс.
6. **Отпечаток меняется у Pavilion** после шагов 5, 6, 8 — эталон пересъёмки один раз, датой; сравнивать «до/после» только для A/C/B0.

## YAGNI

Не делать: эрозию (S4), новые формы (S1), композицию (S2), B1 (места/связи), Indirect/LOD, физику воды, runtime-разрушения, правки тел/видов, пересборку `DomePreview` в игровой мир (достаточно одного пола или честной пометки «макет»), новых ассетов.
