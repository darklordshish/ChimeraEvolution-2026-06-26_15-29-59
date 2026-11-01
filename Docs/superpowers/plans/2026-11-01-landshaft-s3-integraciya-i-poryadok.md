# План S3 — интеграция и порядок: один пол, маски, места

**Дата:** 2026-11-01. **Спека:** [`2026-11-01-landshaft-s3-integraciya-i-poryadok.md`](../specs/2026-11-01-landshaft-s3-integraciya-i-poryadok.md).
**Ветка:** лесная (`ChimeraEvolution-les`). **Статус:** **В РАБОТЕ · решения ПРИНЯТЫ и ДЕЙСТВУЮТ (01.11.2026).**
**Решения 01.11 (вход):** уклоны A (`p95 16–20°`, `>22° 2–4 %`, стены ≤2 %); `H/D = H/(D−бровка)`, 22°;
макро `P 50–70`; чаша да, вода ресурс, плотность 3000–3500, вертикаль механика; `buildRelief` отдельно;
станция на поле формы; кольцо вне поля; эрозия нужна. Развилки Р-1…Р-3 закрыты — двух вариантов в шагах больше нет.

**Жёсткие рамки исполнения:**
- `DomeGenConfigSO.version` **не менять** — он входит в H0 (`DomeHeightField.cs:57`, `:88`) и в каньон (`DomeCanyon.cs:85`) у всех сцен.
- Сцены `ForestSandbox` (A) и `ForestFragment` (B0) **не править**; в них только проверять, что числа прежние.
- Правки — только в `Assets/_Chimera/Scripts/Forest/` и `Assets/_Chimera/Tests/*/Forest/`; `Docs/` — на шаге 9.
- Прогон: `unity command set_autotick --enable true` → `run_tests --mode editor --async_tests true` → опрос `test_status`; затем PlayMode. Без autotick тесты висят.
- Каждый шаг заканчивается зелёными тестами; коммит — в точках §«Коммиты», на русском, тематически.

## Предусловия

- Текущая база: EditMode 424 (421 passed, 1 skipped `KeyPose`, 2 известных inconclusive), PlayMode 170/170 (01.11).
- Эталоны A/C/B0: A — 52 чанка, C — 9 змеиных + сова без спавна, B0 — 12 чанков (`ForestBaselineTests.cs:47`, `:60`, `:73`). **B0 — сид 9** (сцена `ForestFragment`); тестовый хелпер `ForestFragmentTests.cs:39` ещё стоит на `seed = 4` — привести к 9 на шаге 1, иначе эталон снимается с другого мира.
- Сцена павильона: `ForestPavilion.unity:743–771` — Ø2000, сид 1337, `chunkSize 250`, `terrainQuads 48`, `treeCount 1500`, `patchyFlora 1`, `buildTerrace/buildCanyon/buildStationHill/buildLabComplex/buildClimate/buildStationLoop 1`, `buildRavines 0`, `labEntrance Rock`. После шага 3 — `buildRelief: 1`, `worldVersion: 2`, `fingerprintVersion: 2` явно.
- Сегодня `buildRelief` нет; P5-lite включается через `patchyFlora` (`DomeWorld.cs:240–241`); `worldVersion`/`fingerprintVersion` нет — есть `cfg.version` и `ForestSpace.LayoutVersion` (`ForestSpace.cs:15`). Новые `int`-поля у старых сцен приходят **0**; `0` обязано читаться как **v1**, иначе A/C сдвинутся.

---

## Шаг 1. Зафиксировать базовую линию (сторож, без кода мира)

**Цель:** иметь чем доказать «A/C/B0 не изменились» и «тот же сид — тот же мир» до правок.

**Файлы:** `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestBaselineTests.cs` (+ константы), новый `Assets/_Chimera/Tests/EditMode/Forest/Worldgen/BaselineFingerprintTests.cs`.

**Действия:**
1. Записать **высотный хэш** `SampleGround` (сетка `SamplingContract`) и `Fingerprint` **v1** для A (`ForestSandbox`, 1337), C (та же сцена с пулом змей), B0 (`ForestFragment`, **9**), Pavilion (1337) — константами с датой. Высотный хэш — то, что нельзя менять у A/C ни при какой правке флагов.
2. Записать `SamplingContract` павильона (`meshStep`, `slopeEpsilon`, `routeCell`, `navVoxel`), `PlatformY` и `platformY + Plinth` (верх плиты).
3. Тест `Baseline_A_C_B0_Fingerprints` — сравнение v1 с константами; `Baseline_A_C_Heights` — высотные хэши; тест `Baseline_Pavilion_Counts` — 52/1500/9 логовищ и т. д. (по факту).

**Проверка:** новый тест зелёный до правок; полный EditMode/PlayMode зелёный.

**Результат:** эталон в тестах, а не в пересказе.

## Шаг 2. `DomeTerrain`: вынести порядок и оставить один пол

**Цель:** шаг формы перестаёт быть двумя приватными функциями `DomeWorld` с разным смыслом.

**Файлы:** новый `Assets/_Chimera/Scripts/Forest/Generation/DomeTerrain.cs`; правки `DomeWorld.cs`; тесты `TerrainOrderTests.cs` (PlayMode).

**Действия:**
1. Перенести `BaseGround` (`DomeWorld.cs:363–427`) и `SampleGround` (`:357–361`) в `DomeTerrain` с явным списком слоёв **в сегодняшнем порядке**:
   `hydro → terrace → canyon → ravines → hill → landform → ramp → crevice → pad`.
   Это важно: слои пока пересекаются (масок нет), и перестановка `landform`/`hill` уже изменила бы пол. Целевой порядок §2.1 спеки (`landform → hill`, «широкое → местное») вводится на шаге 4 вместе с масками.
2. `DomeWorld.SampleGround` — фасад на `DomeTerrain.SampleGround`; `TerrainGround` удалить (её читает только сверка станции, `:220` — шаг 6 заменит её на `DomeTerrain.ShapeGround`); `Shape0` — отдельная функция `H0 + hydro`;
3. `BaseGround` наружу не выходит; `RawGround` остаётся диагностикой.
4. Тесты: `TerrainOrderTests.Flags_Off_Equals_Raw_Outside_Pad`, `TerrainOrderTests.Pad_Is_Last`, `TerrainOrderTests.Shape_Excludes_Pad` (площадка не входит в `Shape`).

**Проверка:** `Fingerprint` A/C/B0/Pavilion **до и после** совпадают побитово (шаг 1); `Geography_Off_IsNoOp` (`ForestPavilionTests.cs:283–292`) зелёный; EditMode/PlayMode без изменений.

**Результат:** один порядок в одном классе; поведение не изменилось (рефактор).

## Шаг 3. `buildRelief`, `worldVersion` и паритет (Р-1 решён: отдельный флаг)

**Цель:** P5-lite перестаёт быть привязанным к B-lite; появляется селектор версии мира, которым можно двигать порядок, не двигая H0; павильон явно выбирает v2.

**Файлы:** `DomeGenConfigSO.cs` (новые поля), `ForestBuilder.cs` (`[Header]`, передача в мир), `DomeWorld.cs` (условие `:240`), `DomeTerrain.cs`, `WorldFingerprint.cs`, сцена `ForestPavilion.unity`; тесты `TerrainOrderTests.cs`, `ForestPavilionTests.cs` (хелпер `Build`), `ForestFragmentTests.cs` (сид 9).

**Действия:**
1. Поле `buildRelief` (по умолчанию `false`), проброс `ForestBuilder → ownedConfig → DomeWorld`; `patchyFlora` остаётся только B-lite. Связку «P5-lite = часть B-lite» **не сохраняем** (вариант B отклонён).
2. Новое поле `worldVersion` (не `cfg.version`): `0` читается как **1**. `ForestPavilion` ставит **`worldVersion: 2` явно**; A/C/B0 поле не получают → v1. Гоча: новое `int`-поле у старой сцены приходит нулём; `0` не должен означать «новая версия».
3. Хелперы: `ForestPavilionTests.Build` (`:28–57`) — `buildRelief = stationLoop`, `worldVersion = 2`; `ForestFragmentTests` (`:39`) — сид **9**; базлайновые хелперы A/C/B0 новых полей не трогают.
4. **Тест паритета:** `WorldVersion_Default_IsV1` — без поля высотный хэш равен шагу 1; `WorldVersion_Two_IsExplicit` — Pavilion даёт v2 в отпечатке и другой хеш только после того, как S1 v2 реально меняет поле; до этого высоты v2 = v1, но селектор виден. Это ловит «забыли записать 2 в сцену» и «0 прочитали как 2».

**Проверка:** A/C/B0 высотные хэши и `Fingerprint` v1 не изменились; `buildRelief = 0` → `SampleGround` равен шагу 2 (тождество); `buildRelief = 1` → отпечаток Pavilion другой, `ReliefHills ≥ 3` (`ForestPavilionTests.cs:246–261`); `worldVersion 0 = 1`; `ForestPavilion` читает 2. Сторожа: `Relief_Needs_Flag_Not_Patchy`, `WorldVersion_Default_IsV1`.

**Результат:** флаги развязаны, версия мира — явный селектор, павильон на v2, A/C/B0 на v1.

## Шаг 4. Габариты и маски вместо расстояний между центрами

**Цель:** ни один слой формы и ни одно место не пересекаются «по центру».

**Файлы:** новый `Assets/_Chimera/Scripts/Forest/Generation/DomeFootprint.cs`; правки `DomeRelief.cs`, `DomeRavines.cs`, `DomeTerrace.cs`, `DomeCanyon.cs`; `ForestSpace.cs` (подготовка), `DomeAshSite.cs`; тесты `Assets/_Chimera/Tests/EditMode/Forest/Worldgen/DomeMaskTests.cs`.

**Действия:**
1. `Footprint`: `Circle`, `Capsule`, `OBB`, `Polygon` + `Overlaps` (спека §3.1); для полигона — растр `max(2 м, routeCell)`.
2. `MaskSet`: `station` (OBB 46×36 + `PadBlend 6` + рампа + щель + подход), `entry` (R12 + портал), `ramp`, `water` (озеро/русло/плёс/полотно), **`mainRoute` R30** от осевой `Entry → LabDoor`, **`corridor` 28 м** (16 + 2×6), **`arena` R50**, **`ash` R75**, **`lair` R25**, `ring` (`rin−30 … rout`).
3. Переписать запреты: `DomeRelief.cs:42–47`, `:65–70` → `Overlaps`; `DomeRavines.cs:39–40`, `:52`, `:65–71` → габарит оврага (центр + полуось вдоль `angleDeg`); `DomeAshSite.cs:58–65` → `R75` и габарит; `DomeTerrace`/`DomeCanyon` уже считают `reach`/полилинию — привести к `Footprint`. Для `arena`/`ash`/`lair` — две стороны: форма не входит в маску, если якорь известен; POI отвергает пятно формы (§3.2 спеки).
4. Отрицательные фикстуры: холм `R=70` с центром в 60 м от арены R30 (габарит R50 пересекается) и гряда с полуосью 70 — обязаны быть отвергнуты.
5. После масок переставить `landform` и `hill` в целевой порядок §2.1 («широкое → местное»). Габариты уже не пересекаются, поэтому `Fingerprint` обязан остаться прежним — это и есть проверка, что перестановка ничего не сдвинула.

**Проверка:** `DomeMaskTests` (пары + фикстуры); `Landform_NoFootprintOverlap` на 20 сидах (станция, вход, рампа, вода, кольцо, арены при включённых B-lite/relief); `ForestPavilionTests` зелёный.

**Результат:** «свободно» доказывается пересечением габаритов, а не расстоянием до центра.

## Шаг 5. Арены, коридоры и поды — на финальном полу

**Цель:** арена не встаёт на холм, коридор не упирается в склон.

**Файлы:** `DomeWorld.cs:272–285`, `ForestSpace.cs:29–59`, `:75–118`, `:190–226`; `ForestSpace.LayoutVersion`; тесты `ArenaFootprintTests.cs`, `CoverPodTests` (PlayMode).

**Действия:**
1. `ForestSpace.Build(...)` получает `ShapeGround` (не `BaseGround`); проверка уклона — 8 точек кромки + центр **в R50**, порог **5°**; вода — полигоном; кольцо — полосой; станция/ясень/логова — габаритом.
2. Коридоры строятся между принятыми аренами и валидируются: **поперёк ≤5°, вдоль ≤8°**, нет воды/кольца/станции, ширина ≥ 16 м; иначе ребро заменяется на следующее по длине (правило в `BuildCorridors`).
3. Поды: `PodSpotOk` — габарит 3 м + зазор, а не точка.
4. `LayoutVersion` → `2` (правило размещения изменилось). **В отпечатке смешивать `LayoutVersion` только при включённой разметке** (`patchyFlora`), иначе рост версии поедет и по A/C/B0, где разметки нет (`WorldFingerprint.cs:35` смешивает безусловно). Отпечаток обязан измениться у Pavilion и не измениться у A/C/B0.

**Проверка:** `ArenaFootprintTests` — 5 арен (4–6), **≤5° в R50**, ≥ 2 выхода ≥6 м; `CorridorFootprintTests` — поперёк ≤5°, вдоль ≤8°; `Corridors_Walkable` — NavMesh проходит по ленте (не только `ContainsCorridor`); `Clearings_ArePresent_AndDry` переписан на кромку; A/C/B0 высотные хэши/фингерпринты v1 прежние (у них B-lite выключен).

**Результат:** арены и коридоры живут на том же полу, что чанки и NavMesh.

## Шаг 6. Станция и вход: поиск на поле формы, `platformY` из финального пола, площадка последней

**Цель:** станция не на склоне, плита не яма, дверь/рампа на месте.

**Файлы:** `DomeWorld.cs:157–166`, `:215–240`, `:264–267`, `:955`; `DomeLabSite.cs:104–181`, `:281–343`; `DomeTerrain.cs`; тесты `StationOnShapeTests.cs` (PlayMode).

**Действия:**
1. `DomeLabSite.Find` получает `Func ground` = **`DomeTerrain.Shape0`** (`H0 + hydro` = `SampleHydro`; **без terrace/canyon/ravines/landform/hill**); `Accept` остаётся прежним (пятно + подход, `:303–333`). Это и есть «станция на поле формы»: вода уже вырезана, станция не встанет в озеро/русло; финальный `ShapeGround` зависит от её маски, поэтому искать на нём нельзя.
2. `terrace`, `canyon`, `ravines`, `landform` получают маску `station` по найденному пятну + `PadBlend` + рампа + подход 16/24 м. **`hill` маску не получает** — плато поднимает и пятно, чтобы `ShapeGround(LabCenter)` дал его верх, а площадка последним blend'ом выровняла плиту. Терраса/каньон/овраги идут **сразу после станции** (им нужен её центр как keep-out), landform/hill — позже; цикла нет, потому что форме нужны XZ и габарит, а не `platformY`.
3. `platformY` — **из финального `ShapeGround(LabCenter)` после `hill` и `erosion`**: `platformY = max(ShapeGround(LabCenter) − DepthFor(entrance), lakeLevel + 0.5)`. `hill` при этом **не маскируется** площадкой (иначе центральная точка не увидит верх плато); площадка последняя. Старая сверка `:215–230` удаляется. **Не** из `Shape0`, **не** из `TerrainGround`, **не** из константы `+8` (иначе плита ниже верха плато на 8 м и больше — яма).
4. Площадка/рампа/щель — последний `Blend`; `LabComplex.Build(... platformY, ...)` (`:955`) обязан лечь на финальный пол: `|SampleGround(LabCenter) − platformY| ≤ 0.05` для всех входов (pad уплощён); для `Buried`/`Underground` дополнительно `|ShapeGround(LabCenter) − platformY − DepthFor| ≤ 0.05` (земля над крышей выше пола ровно на `DepthFor`). Верх плиты — `platformY + Plinth`, дверь/игрок на нём.
5. Вход: XZ прежний (`:796–803`), высота `SampleGround`; маска `entry` не пускает к нему форму.
6. Проверить запись `+8` vs `+14–18`: `+8` — превышение плато над базовым полем, `+14–18` — абсолютный верх на текущей базе; `platformY` не должен быть ни тем, ни другим.

**Проверка:** `StationOnShapeTests` — 20 сидов: поиск на `Shape0`, маржи `min ≥ 0` после landform, перепад ≤ 4 м `SampleGround` по пятну, уклон ≤ 20°, плита плоская, `platformY` из `ShapeGround`, отрицательная фикстура «`platformY = Shape0` → плита ниже плато на 8 м»; `LabDoor_To_Atrium_IsComplete` (`ForestPavilionTests.cs:113–132`); `StationLoop_Crevice_IsNotBuriedByGround` (`:263–280`); `Clear` пуст.

**Результат:** станция проходит приёмку на том полу, который строится.

## Шаг 7. Кольцо: маска и маршрут (Р-3 решён: вне поля)

**Файлы:** `DomeSlope.cs`, `ForestBuilder.cs:478–490`, `:504–520`; `DomePass.cs:15–45`; `ForestRouteGraph.cs`; тесты `RouteParityTests.cs`.

**Действия (вариант А, рекомендация):**
1. `SampleGround` кольцо не содержит (как сегодня) — правило закрепляется тестом `Ring_Not_In_SampleGround`.
2. `ring` — маска для landform/ravines/arenas/маршрутов (§3.2); `lair` — R25 вокруг устья/гнезда.
3. Одна функция `RingFloor(x,z) = max(SampleGround, ring)` для устьев/гнёзд/ясеня; `BowlPad` и двойной `max` из `ForestBuilder.cs:406–407`, `:421–422`, `:517–518` сводятся к ней (или удаляются).
4. `DomePass.BlockedGrid` получает `ShapeGround` (не сырое поле) и блокирует кольцо полосой; пятно станции — **прямоугольником** (круги по деталям запечатывают дверь, это уже поймано в B0.1); `ForestRouteGraph` — тот же пол.
5. **Вариант Б («кольцо в поле») отклонён** — отдельной спеки-шипа нет; кольцо остаётся мешем/стеной.

**Проверка:** `Ring_Not_In_SampleGround`; `RouteRadius_Le_Rin` на 20 сидах; `DomePass` и NavMesh согласны у входа (**B0 на сиде 9**: путь внутри `rin`); спавнер и адрес логова читают одну функцию.

**Результат:** кольцо — стена и маска, а не третий пол.

## Шаг 8. Отпечаток и след

**Цель:** два разных мира не могут иметь один `Fingerprint`; каждый слой объясняет отказ.

**Файлы:** `WorldFingerprint.cs:12–73`; `GenTrace` вызовы в `DomeWorld.cs`; тесты `FingerprintLayersTests.cs`, `GenTraceTests.cs`.

**Действия:**
1. Ввести `fingerprintVersion` (`0` = **v1**; A/C/B0 пинятся на v1; `ForestPavilion` — **v2**). **v1 — прежняя формула**: сетка 32×32 по `SampleGround` + прежние поля, без новых слоёв и без роста `LayoutVersion`; **v2** смешивает hills (центр/радиус/высота/ось/полуоси/`ridge`), terrace, canyon (полилиния/ширина/глубина/плёс/лип), ravines, hill, `platformY`, `padYaw`, `rin/rout`, `lakeLevel`, `worldVersion`, `LayoutVersion` (только при включённой разметке), флаги `buildRelief/buildRavines/buildTerrace/buildCanyon/buildStationHill/buildStationLoop`. Это и есть «A/C — только высотные хэши, либо fingerprintVersion»: выбрано второе, с v1 как замороженным эталоном.
2. Не полагаться на сетку 32×32: гряда полушириной 12.5–17.5 м проваливается между узлами 62.5 м (Ø2000) — проверить фикстурой.
3. `GenTrace`: `landform` (кандидаты/принятые + отказы: вода/станция/кольцо/габарит), `hill`, `pad`, `space`, `water`, `ring`, `entry`, `flora` (бюджет), `erosion` (вход/выход/причина).
4. Эталоны A/C/B0 из шага 1 (v1) — сравнение; Pavilion — новый эталон **v2** с датой.

**Проверка:** `FingerprintLayersTests` — тот же сид/флаги → тот же хеш; `fingerprintVersion 1` у A/C/B0 равен эталону; `2` у Pavilion; флип каждого флага/слоя меняет v2; `0` читается как `1`; `GenTrace` содержит причины.

**Результат:** «тот же мир» проверяется, а не обещается.

## Шаг 9. Документы и приёмка

**Файлы:** `Docs/Диаграммы/Лес/ГЕОГРАФИЯ.md` (снимок чисел), `Docs/УКАЗАТЕЛЬ.md` (строка S3 — **обязательна для гейта** `spec_status.py`), `Docs/СТАТУС.md` §5, шапка спеки S3 (**`В РАБОТЕ · решения ПРИНЯТЫ`**, дата 01.11), `Docs/Диаграммы/Лес/Кадры/ИНДЕКС.md`.

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
| **S1** (математика форм) | имена, параметры и **габариты** форм; оператор `Lift/Carve/Blend`; смысл `worldVersion` (v2 меняет H0) | шаг 4 (маски) ждёт типов габаритов; шаг 3 вводит только селектор `worldVersion`, поле v2 S3 не строит |
| **S2** (композиция) | сколько арен/коридоров/подов и какие связи обязательны; числа плато `+8` vs абсолют `+14–18` | шаг 5: без S2 берётся B-lite (5 арен, 6 коридоров, 24 пода) и `LayoutVersion 2`; S2 может поменять числа — порядок и маски от этого не меняются |
| **S4** (эрозия) | офлайн-результат, версия и **подтверждение входа** | слот: `hill → erosion → ShapeGround`; вход — поле до POI, маски `pad/ramp/crevice` не нужны. S4 не двигает эрозию после площадки (это нарушит И2) |
| **S5** (верификация) | полный 20-сидовый прогон, кадры, сводка; **пороги 5°/8° и `p95 16–20°`/`>22° 2–4 %`** | S3 даёт контракт (`Fingerprint`, `GenTrace`, `SamplingContract`); S5 владеет приёмкой и не переписывает порядок |

## Риски плана

1. **Шаг 2 «рефактор без изменений»** — риск, что вынос в `DomeTerrain` уже поменяет порядок. Сторож: побитовое равенство **высотных хэшей** и отпечатков v1 A/C/B0/Pavilion; нет равенства — откат шага.
2. **Шаг 5 меняет разметку** — арен станет меньше (отказ по 5°/R50). Это ожидаемо; тест держит 4–6, но при 4 надо сообщить, а не «дотянуть» пятую хаком. `LayoutVersion` растёт — старые миры с тем же сидом дадут другую разметку, и это надо объявить.
3. **Шаг 6 меняет `platformY` и маску станции, а не поле её поиска.** Поле остаётся `Shape0 = H0 + hydro` (то же `SampleHydro`); меняется то, что `terrace`/`canyon`/`ravines`/`landform` не входят в пятно, `hill` поднимает пятно, а `platformY` читается из `ShapeGround`. A/C не трогаются (у них `buildLabComplex 0`); B0/Pavilion могут сдвинуть плиту по высоте; тесты B0 ждут 12 чанков на **сиде 9**, а не конкретное место — место пишется в лог и в отпечаток.
4. **Порядок «площадка последней»** делает борт площадки резче (blend 6 м). Кадр обязателен; если борт читается обрывом — увеличивать `PadBlend` решением, а не возвратом landform после pad.
5. **Кольцо в масках на Ø400** может отрезать часть полян (там арифметика уже сейчас на грани: `0.78·120 + 30 = 124 > 120`). B0 B-lite не включает, но при включении — меньше арен. Это честный отказ, а не регресс.
6. **Отпечаток меняется у Pavilion** после шагов 5, 6, 8 — эталон v2 пересъёмки один раз, датой; сравнивать «до/после» только для A/C/B0 (v1).
7. **`platformY` — яма.** Если шаг 6 возьмёт `Shape0`/`site.groundY` или константу `+8`, плита окажется ниже плато на 8 м и больше. Сторож: `|SampleGround(LabCenter) − platformY| ≤ 0.05` для всех, `|ShapeGround(LabCenter) − platformY| ≤ 0.05` для `Rock`/`Bastion`, `|ShapeGround − platformY − DepthFor| ≤ 0.05` для подземных; отрицательная фикстура «плита ниже плато» обязательна.
8. **Новое поле = 0.** `worldVersion`/`fingerprintVersion` у старых сцен приходят нулём; `0` обязано читаться как v1, а `buildRelief` — как `false`. Сторож: `WorldVersion_Default_IsV1`; `ForestPavilion` пишет 2 в сцену явно, не полагаясь на дефолт.

## YAGNI

Не реализовывать эрозию здесь (S4; решение «нужна» — её вход, не код), новые формы (S1), композицию (S2), B1 (места/связи), Indirect/LOD, физику воды, runtime-разрушения, правки тел/видов, пересборку `DomePreview` в игровой мир (достаточно одного пола или честной пометки «макет»), новых ассетов.
