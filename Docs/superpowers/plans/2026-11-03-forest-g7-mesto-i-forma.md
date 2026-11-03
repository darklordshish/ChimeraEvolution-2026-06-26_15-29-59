# План G7 — место и форма: дерево решений, три схемы, станция на рельефе

**Дата:** 2026-11-03. **Спека:** [`2026-11-03-forest-g7-mesto-i-forma.md`](../specs/2026-11-03-forest-g7-mesto-i-forma.md).
**Ветка:** `les/runtime-builder`. **HEAD базы:** `b85e3ec6`.
**Статус:** **В РАБОТЕ · решения ПРИНЯТЫ и ДЕЙСТВУЮТ (03.11.2026); P0 второго ревью закрыт,
код не начат.**

**Решения геймдизайнера 03.11.2026 — вход, не переоткрывать:**

- архитектура **B**: авторские скелеты + дерево решений + CSP;
- три схемы: «два мыса + седло», «чаша с грядой», «озеро в котле»; схему выбирает дерево по сиду;
- ветви: **ближняя форма, каньон, станция**; **ясень, кольцо, вода** — параметры/ограничения;
- герой — часть схемы; станция не герой;
- **1A:** `LabDoor` — неподвижный якорь на **909 м** (центр плато 927 м, дверь на 18 м к входу);
  станция ищется **внутри плато `LabDoor`**; «плечо мыса» = край этого плато, а не отдельный мыс;
  S2/З1/M2 не пересчитываются;
- **`stationReserve`:** провизорный регион станции до `landform`; финальная посадка после
  `ShapeGround` внутри резерва; `terrace/canyon/ravines` резерв уважают, `landform` строит плато;
  цикл «формы ↔ станция» разорван;
- порядок v2: `H0 → hydro → тип/герой → stationReserve → формы → Shape0 → erosion → ShapeGround →
  станция внутри резерва → вход по рельефу → площадка/рампа/щель → SampleGround`;
- станция: сухое плечо/склон, 46×36, **≥150 м от `Entry` по `LabDoor`** (при 1A — тавтология,
  факт — `Entry → LabCenter`), вход `Auto` → Rock/Underground/Buried по рельефу, **не `Bastion`**;
- `Bastion` — только v1; в v2 станция не коробка в поле; v2-профиль `ForestProfiles.PavilionV2` и
  `G7Shot` — `Auto`, без станционного холма и жёсткого `Rock`;
- нет места — откат кандидата/резерва/схемы, не дефолт в центр; в v2 `found = false` →
  `HasStation = false`, **без `LabCenter = zero`**;
- **2A:** приёмка G7 — по S5 §4: **0/50/100 + Р1–Р4 + три смотрящих + геймдизайнер**;
  **станция ① — `Entry`** (S2 §5.1), `Entry +120 м` — exemplar/Р3, не гейт; 6/19 и три станции —
  ART, не S5; S5 §4/§6 не отменяются;
- детерминизм `seed + worldVersion + treeVersion + порядок слоёв`; v1 бит-в-бит;
- **`treeVersion` — в `WorldFingerprint`, не в ключе эрозии**; поле вводится **шагом 1**, не шагом 0,
  и записывается в `ForestBuilder`; ключ остаётся
  `(seed, worldVersion, erosionVersion, resolution)`; S3 §2.5/И20 и S4 §4.2 не отменяются.

**Жёсткие рамки исполнения:**

- `DomeGenConfigSO.version` **не менять** — он входит в H0 (`DomeHeightField.cs:57`, `:88`) и в
  каньон (`DomeCanyon.cs:85`) у всех сцен.
- Сцены `ForestSandbox` (A/C) и `ForestFragment` (B0) **не править**; в них только проверять, что
  числа прежние. `ForestPavilion` (v2) правится **только** в шаге 2 и только по станции/входу.
- Правки — только в `Assets/_Chimera/Scripts/Forest/`, `Assets/_Chimera/Tests/*/Forest/`,
  `Tools/Agent/G7Shot.cs`, `Docs/Диаграммы/Лес/`, `Docs/les/`; `Docs/superpowers/specs/S1–S5` —
  только письмом владельцу.
- `agentClimb` (`ProjectSettings/NavMeshAreas.asset:79`) **не трогать**: лазание — не шаг пешехода.
- Эрозию и дорожный граф **не реализовывать здесь** (S4): G7 даёт слот, вход, адреса и форму.
- Прогон: `unity command set_autotick --enable true` → `run_tests --mode editor --async_tests true`
  → опрос `test_status`; затем PlayMode. Без autotick тесты висят.
- Каждый шаг заканчивается зелёными тестами; коммит — в точках §«Коммиты», на русском, тематически.

## Предусловия

- База 03.11: EditMode 424 (421 passed, 1 skipped `KeyPose`, 2 известных inconclusive), PlayMode
  170/170; G7 FAIL — `0/3` сектор, `0/3` направление (`15-G7`).
- **A/C/B0/Pavilion v1 заморожены.** Эталоны: A — 52 чанка, C — 9 змеиных + сова без спавна,
  **B0 — 12 чанков, 6 змеиных логовищ (2 устья + 4 гнезда) + сова = 7**, Pavilion v1 — прежний
  пол/отпечаток. `ForestBaselineTests`, `ForestSceneParityTests`, `GenTraceFingerprintTests`
  обязаны быть зелёными **без правок**.
- **S3 reciprocal — учёт, не ворота.** Спека G7 несёт `Отменяет:` **точные пункты** (§2.1 п.1 и
  `Hydro0`, §2.1 диаграмма, §2.1 п.2/п.3, §2.2 п.2/п.3, §3.3 п.2, §4, §7.2 п.2/п.5, §12 Р-2,
  §9 И2/И4/И13/И20); reciprocal `Отменена:` ставит docs-keeper. **Шаг 2 не ждёт пометки:** пока её
  нет, действует этот файл, а не два порядка.
- **`ForestPavilion` пока v1:** **единственный источник v1/v2 для игры — сцена.** В сцене нет
  `worldVersion` (значит 1), стоят `labStyle: 0` (Bastion), `buildStationHill: 1`, `labEntrance: 0`
  (Rock). Это **v1-хвост**: в v2 `Bastion` и станционный холм запрещены, вход должен быть `Auto`.
  **Запись `worldVersion: 2` в сцену — S1 шаг 8, не этот план.** До неё v2 проверяется
  **`ForestProfiles.PavilionV2`** и **`G7Shot`** (в них **должны стоять** `labStyle = Auto`,
  `labEntrance = Auto`, `buildStationHill = false`, `treeVersion = 2`), но профили **не делают сцену
  v2**. При включении v2 сцена обязана избавиться от Bastion/холма/Rock — это условие шага 2, а не
  отдельная правка мира.
- **`DomeHill` как станционный подъём** в v2 не участвует; **плато строит `landform`** (форма S2 №4).
  Сухое плечо — часть схемы/форм до `ShapeGround`; если схема не даёт плеча, ветка `Hill` не годится.
  **`stationReserve`** — провизорный регион до форм; финальная станция — внутри него после
  `ShapeGround`. **Габарит:** пятно + blend 58×48 — по верху плато 70×55; подход 16/24 м и резерв
  целиком — по низу 144×129.
- **`treeVersion` пока нет.** Новое поле вводится **шагом 1**, не шагом 0; `0` читается как v1,
  явная двойка — только v2-профиль (`ForestProfiles.PavilionV2`, `G7Shot`) и `ForestBuilder`. Новое
  `int`-поле у старой сцены приходит нулём — не прочитать его как v2. **В ключ эрозии `treeVersion`
  не входит**; ключ остаётся `(seed, worldVersion, erosionVersion, resolution)`.
- **`DomeLabSite` fallback.** В v1 (`A/C/B0/Pavilion v1`) запасная точка текущего `Find` сохраняется
  бит-в-бит. В v2 `found = false` с причиной; запасной точки в центр/напротив входа **нет**;
  `HasStation = false` и **никакого `LabCenter = zero`** — «ноль» это не «станции нет», а скрытый
  дефолт в центр.

---

## Шаг 0. База, протокол A/B и манифест кадров

**Цель:** иметь чем доказать «A/C/B0/Pavilion v1 не изменились» и «A/B — один мир» до правок формы.

**Файлы:**

- `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestBaselineTests.cs` — эталонные хэши/счётчики;
- новый `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/G7ParityTests.cs` — **без ссылок на
  `Tools/Agent`** (скрытие POI — по публичным полям `ForestBuilder`/слою, не через `G7Shot`);
- новый `Assets/_Chimera/Tests/EditMode/Forest/Worldgen/G7FrameMetadataTests.cs`;
- `Tools/Agent/G7Shot.cs` — поля подписи, **один** `PoiWhitelist`, хэш пола и pixel diff;
- `Docs/Диаграммы/Лес/Кадры/15-G7/МАНИФЕСТ.md` — манифест 6/19 (новый файл **рядом с кадрами**;
  кадры не переименовывать).

**Действия:**

1. Записать **высотный хэш** `SampleGround` (сетка `SamplingContract`) и `Fingerprint` v1 для A
   (`ForestSandbox`, 1337), C (та же сцена с пулом змей), B0 (`ForestFragment`, 9), Pavilion v1
   (1337) — константами с датой. Высотный хэш — то, что нельзя менять у A/C ни при какой правке
   флагов.
2. Записать `SamplingContract` павильона (`meshStep`, `slopeEpsilon`, `routeCell`, `navVoxel`),
   `platformY`, `platformY + Plinth`. Это снимок «до», а не норма: шаг 2 законно его изменит у v2.
3. Завести `G7-метаданные.txt`: seed, `worldVersion`, **`treeVersion = absent`** (поля ещё нет —
   оно вводится шагом 1), path, scheme, signature, профиль (`terrainQuads`, `treeCount`,
   `patchyFlora`, `buildRelief`, `buildTerrace`, `buildCanyon`, `buildAsh`, `buildLabComplex`,
   `labEntrance`, `buildStationLoop`), камера (xyz, цель, yaw, pitch, FOV, высота 0/50/100),
   время/погода (`10.5`, `Clear`, `timeScale 0`), туман (цвет, плотность, начало, высотный
   множитель), скрытые объекты списком, pixel diff, что кадр должен доказать (П1–П7/К1–К7).
4. `G7ParityTests`: собрать мир **один раз**; `HidePoi()` — только `SetActive(false)`; снять A;
   `ShowPoi()` — только `SetActive(true)`; снять B. Сравнить `Fingerprint`, хэш пола в 100 точках,
   камеру/FOV, `RenderSettings`, `ForestClimate`, список активных объектов. Diff — только
   **единственный** `ForestBuilder.PoiWhitelist` (его же читает `G7Shot`; второго списка нет;
   `LabShell` **назван POI** — остов лаборатории, S3 §4.2). **Тест не ссылается на `Tools/Agent`:**
   скрытие — по публичным полям `ForestBuilder` (`Zones`/`Links`/`CoverPods`/`BuiltRoot`) и слою.
5. Зафиксировать **манифест кадров** файлом `Docs/Диаграммы/Лес/Кадры/15-G7/МАНИФЕСТ.md`
   (**рядом с кадрами**): минимум 6 exemplar (3 сигнатуры с входа + 3 сверху), полный 19 (9 станций
   чтения × 3 схемы + 3 сверху + 3 обратных + 1 абляция + 3 сигнатуры на разных сидах). Тур 10:
   каньон, водопад, озеро/водопой, станция, лаборатория/POI, ясень, гряда/мыс, коридор, дорога, вход.
6. Зафиксировать **три станции чтения** (ART, не S5): **① `Entry`** — гейт S2 §5.1; `Entry +120 м` —
   **exemplar/Р3**, не гейт; ② середина ~450 м; ③ за 150 м до станции — exemplar/диагностика. Кадр
   0 м G7 снимается **на `Entry`** (S2 §5.1), а **гейт — 0/50/100 + Р1–Р4 по S5 §4**, не «только 0 м».
7. Зафиксировать **`worldVersion 0` читается как 1**; B0 — **6 змеиных + сова** (не «5 змеиных» из
   плана S5). **`treeVersion` здесь ещё не пишется** — поле появляется шагом 1.

**Проверка:** новый `G7ParityTests` зелёный; `G7FrameMetadataTests` требует все поля; полный
EditMode/PlayMode зелёный; A/C/B0/Pavilion v1 хэши совпадают с константами.

**Результат:** эталон и протокол в тестах, а не в пересказе. **Площадка павильона зафиксирована как
дефект «до»**, не как образец.

**Коммит:** `docs(forest): G7 baseline, A/B protocol and frame manifest`.

---

## Шаг 1. План как данные (`MapPlan`), нулевое изменение мира

**Цель:** недостающий слой — **план** — становится данными с путём, `identity` и `signature`;
текущий мир выражается этим планом, но **не меняется**.

**Файлы:**

- новый `Assets/_Chimera/Scripts/Forest/Generation/MapPlan.cs` — `MapPlan`, `DecisionNode`,
  `Variant`, `FormRecipe`, `PoiIntent`, `MaskIntent`, `ParamBlock`;
- новый `Assets/_Chimera/Scripts/Forest/Generation/MapPlanRules.cs` — `requires`/`forbids`,
  стабильные id, один родитель у ветви, fallback;
- `DomeComposition.cs` — адаптер: текущий расклад выражается `MapPlan` с путём `scheme=legacy`;
- `GenTrace.cs` — слот `plan` (кандидаты/принято/причина/откат/путь);
- **`ForestBuilder.cs` — здесь вводится поле `treeVersion`** (не шагом 0) и пишется в мир/профиль;
- `WorldFingerprint.cs` — `treeVersion` только при `worldVersion > 1`; **поле помечено как v2-поле**
  и **не** подмешивается в ключ эрозии; `MixRelief` при `<=1` — тождество;
- `DomeErosion.cs` — **ключ НЕ меняется**: остаётся `(seed, worldVersion, erosionVersion,
  resolution)`; `treeVersion` в ключ не входит (S3 §2.5/И20, S4 §4.2 действуют);
- `ForestProfiles.cs` — `PavilionV2` получает **`treeVersion = 2` явно**; A/C/B0/Pavilion v1 — без
  поля (0 читается как v1);
- тесты: новый `Assets/_Chimera/Tests/EditMode/Forest/Worldgen/MapPlanTests.cs`, новый
  `MapPlanRulesTests.cs`, новый `TreeFingerprintTests.cs`.

**Действия:**

1. **Ввести `treeVersion`** в `ForestBuilder` (и v2-профиль): `0` читается как v1, `2` — явно у
   v2-профиля; в `WorldFingerprint` — только v2; в ключ эрозии **не входит**. Это шаг 1, не шаг 0:
   до него поля нет и подпись пишет `treeVersion = absent`.
2. `MapPlan` — **рецепты, не позиции**: `path` (`scheme=.../near=.../station=.../canyon=...`),
   `identity` = `hash(path + params + form names)`, `signature` = `path + семейства + категориальные
   параметры` без метров.
3. `DecisionNode` — стабильный `id`, `parent`, `variants` в стабильном порядке. `Variant` —
   `id`, `requires`, `forbids`, `weight`, `forms`/`poi`, `fallback`. **Один родитель у ветви**;
   второй родитель — либо `forbids`/`requires`, либо отдельное корневое решение. DAG, названный
   деревом, запрещён.
4. Текущий `DomeComposition` выразить путём `scheme=legacy`, `near=current`, `canyon=current`,
   `station=current`; `identity`/`signature` пишутся в trace, но **геометрия не меняется**.
5. `TreeFingerprintTests`: v1 без `treeVersion`; v2 с `treeVersion`; смена `treeVersion` меняет
   `Fingerprint` v2 и **не меняет ключ эрозии**; смена `worldVersion` меняет и ключ; v1 не меняется;
   `fingerprintVersion` не поднят.
6. `MapPlanRulesTests`: у ветви один родитель; параметр не притворяется ветвью; ветвь с одним
   вариантом — параметр; fallback назван; стабильный порядок вариантов не зависит от порядка в
   массиве.

**Проверка:** A/C/B0/Pavilion v1 `Fingerprint` и высотные хэши **побитово** прежние; v2-мир
собирается тем же, что и до шага (нулевое изменение); `MapPlanTests` — 20 сидов дают тот же путь
при тех же версиях; `GenTrace` содержит `plan`.

**Результат:** путь объясняет «почему», параметры — «сколько»; мир пока прежний, но объяснимый.
**Это не рефактор ради красоты:** без этого слоя шаги 2–5 снова будут «первое годное».

**Коммит:** `feat(forest): MapPlan data layer with stable path and trace`.

---

## Шаг 2. Порядок v2 и станция на рельефе

**Цель:** станция перестаёт быть коробкой на сыром поле: до форм выбирается **`stationReserve`**,
формы его уважают, финальная станция ищется **после** формы, на `ShapeGround`, **внутри резерва**;
`Bastion` и станционный холм остаются в v1.

**Файлы:**

- `Assets/_Chimera/Scripts/Forest/Generation/DomeWorld.cs` — порядок v2: `stationReserve` до форм,
  поиск станции после `ShapeGround`; **`HasStation`/`station=none` вместо `LabCenter = zero`**;
- `Assets/_Chimera/Scripts/Forest/Generation/DomeTerrain.cs` — имена полей и порядок;
- `Assets/_Chimera/Scripts/Forest/Facility/DomeLabSite.cs` — `stationReserve` до форм; поиск внутри
  резерва на `ShapeGround`, сухое плечо/склон, 46×36, **≥150 м по `LabDoor`** (при 1A — тавтология,
  факт — `Entry → LabCenter`), `Auto` по рельефу, **не `Bastion`**; в v2 — `found = false` без
  запасной точки;
- `Assets/_Chimera/Scripts/Forest/Facility/DomeLabComplex.cs` — плита/рампа/щель, `platformY` из
  `ShapeGround`;
- `Assets/_Chimera/Scripts/Forest/Generation/DomeHill.cs` — в v2 не строит станционный подъём;
- `Assets/_Chimera/Scripts/Forest/Generation/DomeMasks.cs` — `stationReserve` до форм, маска станции
  после поиска;
- `Assets/_Chimera/Scripts/Forest/Facility/LabStyle.cs` — `Bastion` только v1;
- `Assets/_Chimera/Scripts/Forest/Generation/ForestProfiles.cs` — `PavilionV2`: `labStyle = Auto`,
  `labEntrance = Auto`, `buildStationHill = false`; `PavilionV1` не трогать;
- `Tools/Agent/G7Shot.cs` — тот же v2-профиль (`Auto`, без холма, без жёсткого `Rock`);
- `Assets/Scenes/ForestPavilion.unity` — **только при включённом v2** (S1 шаг 8): `labStyle` (не
  Bastion), `buildStationHill: 0`, `labEntrance: 2` (`Auto`); до этого сцена остаётся v1, а v2
  проверяется `ForestProfiles.PavilionV2`/`G7Shot`. `ForestSandbox`/`ForestFragment` не трогать;
- тесты: `TerrainOrderTests.cs` (новый/обновлённый), новый `StationReserveTests.cs`, новый
  `StationOnReliefTests.cs`, **`TerrainStationTests.cs` — переписать/пометить под v2** (старый файл
  ещё проверяет `Hydro0`/Bastion/холм), `ForestBaselineTests.cs` (только чтение).

**Действия:**

1. В v2 порядок: `H0 → hydro → (plan: scheme/hero, без геометрии) → stationReserve → формы →
   Shape0 → erosion → ShapeGround → станция внутри резерва → вход → POI/дороги/флора →
   pad/ramp/crevice → SampleGround`. `DomeHill` как станционный подъём в v2 **не строится**;
   **плато строит `landform`** (форма S2 №4), а не `DomeHill`; сухое плечо — часть формы.
2. `stationReserve` выбирается **до `landform`**: пятно 46×36 + blend + подход **внутри плато
   `LabDoor`**, ≥150 м по `LabDoor` (при 1A — тавтология, факт — `Entry → LabCenter`), сухо,
   уклон ≤20°, перепад ≤4 м. Резерв — адрес региона, не плита: `platformY` не задаёт, геометрию не
   строит. **Габарит:** пятно + blend 58×48 — по верху плато 70×55; подход 16/24 м и резерв
   целиком — по низу 144×129. `terrace`/`canyon`/`ravines` и прочие формы резерв не пересекают;
   `landform` его несёт.
3. `DomeLabSite.Find` получает `ShapeGround` и предикат места **внутри резерва**: сухо
   (`> lakeLevel + 1.0`), не в воде/русле/кольце/логове/ясене, перепад ≤4 м, уклон ≤20° по пятну и
   подходу, **≥150 м от `Entry` по `LabDoor`** (при 1A — тавтология), 15 м LOS. «Самое ровное
   место» — вето; нужно плечо/склон/лицо.
4. **1A:** `LabDoor` — неподвижный якорь на **909 м** (центр плато 927 м, дверь на 18 м к входу);
   станция ищется **внутри его плато**; «плечо мыса» = край этого плато, а не отдельный мыс.
   S2/З1/M2 не пересчитывать.
5. `LabEntrance.Auto` разрешается по рельефу: `Rock` (скальная толща/лицо), `Underground` (спуск
   между подпорными стенами), `Buried` (плита ниже земли, крыша на уровне земли, расщелина).
   Профиль сцены не диктует вход; **`Auto` не разрешается в `Bastion`** — это v1-стиль, откат.
6. `platformY` читается из `ShapeGround` в точке найденного места; площадка/рампа/щель — последний
   blend. Маска `station`/`ramp`/`entry` появляется после поиска; POI/дороги её видят, формы — нет.
7. Нет места — откат: следующий кандидат в резерве → другой резерв → другая роль → другая схема;
   **не** центр и не «старый лабиринт» молча. В v2 `DomeLabSite.Find` возвращает `found = false` с
   причиной; **`DomeWorld` ставит `HasStation = false`, а не `LabCenter = zero`**; станционные шаги
   и потребители пропускаются, `GenTrace` пишет `station=none`. Запасная точка текущего `Find`
   остаётся только v1.
8. `ForestPavilion`: **когда S1 шаг 8 включит v2** — `labStyle` не `Bastion`,
   `buildStationHill: 0`, `labEntrance: 2` (`Auto`). До этого сцена v1 и не правится; v2
   проверяется `ForestProfiles.PavilionV2`/`G7Shot`. **Единственный источник v1/v2 для игры —
   сцена**; профили — тестовые. `ForestSandbox` (Bastion/InSlope v1) и `ForestFragment` (InSlope v1)
   не трогать.
9. `TerrainOrderTests` обновить: v2 не содержит `station → landform`; v1 — прежний исторический
   порядок; `buildRelief 0` — тождество; A/C/B0 фингерпринты прежние.
10. `StationReserveTests` — резерв до `landform`; `terrace/canyon/ravines` его не пересекают,
    `landform` строит плато; станция только внутри резерва; резерв не задаёт `platformY`;
    габарит 58×48 — по верху 70×55, подход/резерв — по низу 144×129.
11. `StationOnReliefTests` — 20 сидов: станция после `ShapeGround` внутри резерва, сухо, 46×36,
    **≥150 м по `LabDoor`** (при 1A — тавтология, факт — `Entry → LabCenter`), вход `Auto` →
    Rock/Underground/Buried, **не Bastion**, нет дефолта в центр, в v2 нет `fallback=true`;
    `!site.found` → `HasStation = false`, **без `LabCenter = zero`**; плита/рампа/щель.
12. **`TerrainStationTests` переписать/пометить под v2:** старый файл проверяет `Hydro0`/Bastion/
    холм и `plateauApplied = false`; новый — `ShapeGround`/`Auto`/`landform`, «центра нет».
13. **Ветка версии:** всё новое — только при `WorldVersion >= 2`; `worldVersion <= 1` —
    `A/C/B0/Pavilion v1` старым путём бит-в-бит.

**Проверка:** A/C/B0/Pavilion v1 — высотные хэши и `Fingerprint` прежние; v2 — станция на
`ShapeGround` внутри резерва, `Bastion` не строится, `platformY` не из `Hydro0`/константы;
`TerrainOrderTests`, `StationReserveTests`, `StationOnReliefTests` зелёные; `ForestPavilionTests`
зелёный; G1/G2/G4 не хуже.

**Результат:** станция — место в рельефе, а не коробка; порядок S3 заменён в v2, v1 не сдвинут.
**Стоп:** reciprocal `Отменена:` в S3 — учёт docs-keeper, **не ворота** шага; шаг 2 не ждёт пометки.

**Коммит:** `feat(forest): v2 order — station on relief after ShapeGround`.

---

## Шаг 3. Три схемы, герой и ветвь ближней формы

**Цель:** G7 перестаёт читать «поле» на 0–50 м: схема выбирает героя и ближнюю форму, а не
«сколько форм встало».

**Файлы:**

- новый `Assets/_Chimera/Scripts/Forest/Generation/DomeSchemes.cs` — данные трёх схем
  (роли, рецепты форм, регионы, обязательные связи);
- `MapPlan.cs` / `MapPlanRules.cs` — узлы `scheme`, `near`;
- `DomeComposition.cs` — размещение форм по схеме (CSP), без переигрывания стороны;
- `DomeMasks.cs` — маски `near`, `mainRoute`, `zoneReserve` (если решено S2/S3; иначе только
  откат), `stationReserve` до форм, `station` после поиска;
- `DomeTerrain.cs` / `DomeWorld.cs` — формы из схемы до `ShapeGround`;
- `GenTrace.cs` — путь схемы, кандидаты, откаты;
- тесты: новый `TreeSchemeTests.cs`, новый `G7NearFieldTests.cs`, `TreeSeedMatrixTests.cs`
  (20 сидов), `ForestLandscapeGateTests.cs` (G1/G2/G3).

**Действия:**

0. **Ветка версии:** всё новое — только при `WorldVersion >= 2`; `worldVersion <= 1` —
   `A/C/B0/Pavilion v1` старым путём бит-в-бит.
1. Три схемы — **слово силуэта**, не набор новых форм: «два мыса + седло», «чаша с грядой»,
   «озеро в котле». Герой: пара мысов/проём, дальний борт, кромка котла. Станция — не герой; она
   садится на **край плато `LabDoor`** (внутри `stationReserve`).
2. `scheme` — корневой узел; домен 3; `requires` — поле даёт слово, вода не на плите, кольцо не
   ломается. Селектор по сиду, но проверяет совместимость; не «монетка».
3. `near` — ветвь: `ridge`/`hollow`/`cape`/`hybrid` внутри схемы. Требования: 2–3 формы на
   0–150 м; на 0 м H 2–6 м (**у `cape` — H 4–8 м**), 20–60 % ширины, подошва и бровка в кадре,
   ≥1 грань 22–28°; 0 форм >22° на осевой; 0 стволов на бровке; 2 подхода ≥6 м.
4. `DomeComposition`: `flipV` следует из схемы/входа/воды/кольца, **не** из «больше форм встало».
   4–5 форм на 9 ролей; асимметрия ≥1,6 : 1; одна детерминированная «случайность».
5. Станция (из шага 2) ищется **после** форм **внутри `stationReserve`**; если места нет — откат
   формы/резерва/схемы, не центр.
6. `TreeSeedMatrixTests`: 20 сидов; каждая схема ≥2/20; fallback ≤1/20 (`[предложение]`, **не
   assert**); `SignatureDiversity` 3–6 (`[предложение]`, **не assert**); `p10` назван.
7. `G7NearFieldTests`: 0–150 м; 2–3 формы; на 0 м H **2–6 м**, у `cape` — **4–8 м**; локальный
   подъём ≥3 м в окне 40–60 м; 0 форм >22° на осевой; 0 стволов на бровке; формы вне масок и
   `stationReserve`.
8. Кадры: A/B 0/50/100 **по S5 §4 на `Entry`** (станция ①, S2 §5.1), не только 0 м; `Entry +120 м`
   — exemplar/Р3, не гейт; exemplar-минимум **6** (ART: 3 сигнатуры с входа + 3 сверху с
   подписанными ролями).

**Проверка:** `G7NearFieldTests`, `TreeSchemeTests`, `TreeSeedMatrixTests` зелёные; G1/G2/G3 не
хуже S5; A/C/B0/Pavilion v1 прежние; на 0 м есть форма, а не поле; `GenTrace` показывает путь
схемы.

**Результат:** мир называется схемой; ближняя форма читается на 0–50 м; станция садится на край
плато `LabDoor` внутри `stationReserve`.

**Коммит:** `feat(forest): three schemes, hero and near-form branch`.

---

## Шаг 4. Каньон: ветвь `wet/dry/absent`

**Цель:** каньон и водопад перестают быть `invalid`; **`absent` — законная ветвь с `valid = true`**
(мир цел, каньона нет), а `invalid` — сломанная сборка/непройденная маска. Политика каньона —
ветвь дерева, а не «повезло/не повезло».

**Файлы:**

- `Assets/_Chimera/Scripts/Forest/Generation/DomeCanyon.cs` — `wet/dry/absent`, маски, `valid`;
- `DomeTerrain.cs` / `DomeWorld.cs` — каньон до `ShapeGround`;
- `DomeWaterMesh.cs` — плёс/полотно/ручей;
- `DomeMasks.cs` — коридор каньона, `water`, `ring`;
- `MapPlan.cs` — узел `canyon`, `forbids` (каньон через станцию), fallback `dry`/`absent`;
- `Tools/Agent/G7Shot.cs` — тур `canyon`/`waterfall`;
- тесты: `Canyon_Gate` (PlayMode), `TreeRollbackTests`, `G7SeedMatrixTests`.

**Действия:**

0. **Ветка версии:** всё новое — только при `WorldVersion >= 2`; `worldVersion <= 1` —
   `A/C/B0/Pavilion v1` старым путём бит-в-бит.
1. Ветвь `canyon`: `wet` — река/плёс/водопад; `dry` — сухая ложбина; `absent` — нет. Каждая
   ветвь названа и покрыта на 20 сидах; `absent` не прячется и **не считается `invalid`**: это
   выбранная ветвь с `valid = true`. `invalid` — только дефект сборки/маски. **На каноническом 1337
   каньон не `absent`** — только `wet`/`dry`; `absent` допустим на других сидах и только если схема
   не обещает каньон.
2. Каньон строится **до** `ShapeGround`, вне масок станции/входа/воды/кольца/зон и
   `stationReserve`; ось 150–600 м, полуширина 7–12 м, глубина 8–18 м, перепад 12–18 м, плёс R6–9,
   дно ≤12°.
3. `wet` требует воды и полотна; `dry` — сухое дно; `absent` — только если схема не обещает
   каньон. Если каньон — обещание схемы, `absent` режет схему.
4. Тур: `TourCanyon`/`TourWaterfall` не возвращают `canyon invalid`; кадры входят в тур 10.
5. `TreeRollbackTests`: каньон не проходит маски → `dry` → `absent`; причина в `GenTrace`; нет
   «похожего» каньона.

**Проверка:** `Canyon_Gate` — 20 сидов по политике; на 1337 `valid` (`wet` или `dry`); 0
пересечений масок и `stationReserve`; G1/G2/G4 не хуже; A/C/B0/Pavilion v1 прежние.

**Результат:** вода и перепад в мире; каньон — решение, а не случайность.

**Коммит:** `feat(forest): canyon branch and waterfall`.

---

## Шаг 5. Ясень, кольцо, вода и полосы — параметры/ограничения

**Цель:** три параметра и материал перестают давать «наклейки» и «нарисованную тропу»; A-кадр
становится чистым.

**Файлы:**

- `DomeAshSite.cs` — корни/поляна/регион, параметр масштаба;
- `DomeRockRing.cs` — ярусы/полки/осыпь, `rin/rout` не двигать;
- `DomeWaterMesh.cs` — полка/мокрый след/водопои/броды (форма; G5 — отчёт);
- `DomeSlopeBands.cs` — пороги/шум/длина волны; `Path` — только дороги;
- `DomeMasks.cs` — `ash R75`, `ring`, `water`;
- тесты: `Ash_Gate`, `Ring_Gate`, `Water_Gate`, `SlopeBands_Gate`, `G7BudgetTests`.

**Действия:**

0. **Ветка версии:** всё новое — только при `WorldVersion >= 2`; `worldVersion <= 1` —
   `A/C/B0/Pavilion v1` старым путём бит-в-бит.
1. **Ясень:** параметр масштаба (дефолт 1.0 = 85/140/16/50; 0.6–0.7 — параметр стенда/листа), не
   ветвь. Ограничения: 100–400 м от лаборатории, R75, вне кольца/воды/станции/логова, виден с
   `Entry` + 2 точек; корни/поляна; крона не обрезана.
2. **Кольцо:** `rin/rout` — граница, не выбирается; ярусы/полки/осыпь — параметр силуэта. Седла/
   разрывы — отдельное решение containment, здесь не делать.
3. **Вода:** один уровень; озеро не пересекается; полка 3–5 м ≤6°, мокрый след 6–10 м, 2 водопоя,
   2–3 брода ≤0.6 м, дно ≤5°, берег ≤8°. Это форма/берег, доступ — G5/B1.
4. **Полосы:** 5 полос на реальных склонах; на <10° нет непрерывной тропоподобной полосы
   (>30 м длиной и >4 м шириной); `Path` — только дороги. A-кадр без «тропы».
5. `G7BudgetTests`: +≤0.5 с, +≤8 % тр, +≤300 draw calls; `Clear` пуст; кэш не течёт.

**Проверка:** `Ash_Gate`, `Ring_Gate`, `Water_Gate`, `SlopeBands_Gate` зелёные; A-кадр чист;
`G7BudgetTests` в рамках; G5 — отчёт, не блок; A/C/B0/Pavilion v1 прежние.

**Результат:** ориентир, граница, берег и чистый A-кадр без «нарисованной тропы».

**Коммит:** `feat(forest): ash, ring, water and slope-band parameters`.

---

## Шаг 6. Приёмка G7: 6/19 кадров, 20 сидов, три смотрящих + геймдизайнер

**Цель:** G7 PASS или названный FAIL; не «переснять, пока не угадают».

**Файлы:**

- `Tools/Agent/G7Shot.cs` — A/B, тур 10, exemplar-режимы, метаданные, pixel diff, абляция героя;
- новый `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/G7SeedMatrixTests.cs` (или расширение
  существующего);
- `G7FrameMetadataTests.cs` — проверка подписей;
- `Docs/Диаграммы/Лес/Кадры/16-G7-место-и-форма/` — папка кадров (имя — предложение, не решение);
- `Docs/Диаграммы/Лес/ГЕОГРАФИЯ.md` — снимок чисел после приёмки;
- `Docs/Диаграммы/Лес/Кадры/ИНДЕКС.md` — индекс.

**Действия:**

1. Собрать **3 схемы** на каноническом 1337 и на принятых подписях.
2. Снять **G7 A/B 0/50/100 + Р1–Р4 по S5 §4 на `Entry`** (станция ①, S2 §5.1): гейт — все три
   высоты и роли, не «только станция ① на 0 м»; `Entry +120 м` — **exemplar/Р3**, не гейт; станции
   ②/③ — exemplar/диагностика (обратный проход, разнообразие маршрута), не замена гейта. A — гейт,
   B — сравнение; один мир, только `SetActive`.
3. Снять **тур 10**: каньон, водопад, озеро/водопой, станция, лаборатория/POI, ясень, гряда/мыс,
   коридор, дорога, вход.
4. Снять **exemplar 6 минимум** (ART: 3 сигнатуры с входа + 3 сверху) и **19 полный** (9 станций × 3
   схемы + 3 сверху + 3 обратных + 1 абляция + 3 сигнатуры на разных сидах); манифест — **в папке
   кадров**: `15-G7/МАНИФЕСТ.md` (baseline) и `16-G7-место-и-форма/МАНИФЕСТ.md` (приёмка).
5. Прогнать **20 сидов**: покрытие ветвей ≥2/20; fallback ≤1/20 и `SignatureDiversity` 3–6 —
   `[предложение]`, **не assert**; **G0–G4 — на `p10`** (S5 §6/G6), не «20/20»; на 1337 каньон
   `wet`/`dry`, не `absent`.
6. Смотрят **трое независимо** (арт, модельная линия, геймдизайнер) — как в S5 §4; геймдизайнер —
   финальный приёмщик. Слепо, до обсуждения, с фразой «по чему». Проход: **3/3 сектор**, **≥2/3
   направление**. Снимающий не голосует; `model-critic` — совещательно.
7. Записать `G7-метаданные.txt` для каждого кадра; пиксельных ассертов нет.
8. Обновить отчёт: G7 PASS/FAIL, худший сид, цена сборки, что осталось (B1, G8, плейтест).

**Проверка:** G0–G4/G6 на `p10` зелёные; G8 не хуже; `ForestBaselineTests` зелёные; 3/3 и ≥2/3;
кадры и метаданные на месте; `spec_status.py` — после пометок docs-keeper.

**Результат:** G7 принят кадром или назван FAIL с числом и причиной; «поле» больше не проходит.

**Коммит:** `docs(forest): G7 acceptance — frames, 20 seeds, verdict`.

---

## Точки коммита

| шаг | коммит |
|---|---|
| 0 | `docs(forest): G7 baseline, A/B protocol and frame manifest` |
| 1 | `feat(forest): MapPlan data layer with stable path and trace` |
| 2 | `feat(forest): v2 order — station on relief after ShapeGround` |
| 3 | `feat(forest): three schemes, hero and near-form branch` |
| 4 | `feat(forest): canyon branch and waterfall` |
| 5 | `feat(forest): ash, ring, water and slope-band parameters` |
| 6 | `docs(forest): G7 acceptance — frames, 20 seeds, verdict` |

## Стоп-правила

- Шаг требует числа из S1/S2/S3/S4 — **письмо владельцу**, не правка его спеки.
- Шаг требует выбора — **вопрос геймдизайнеру**, не «механики решили».
- **S3 reciprocal — учёт, не ворота:** `Отменена:` ставит docs-keeper; шаг 2 не ждёт. В шапке
  названы §2.1 п.1 и `Hydro0`, §2.1 диаграмма/п.2/п.3, §2.2 п.2/п.3, §3.3 п.2, §4, §7.2 п.2/п.5,
  §12 Р-2, И2/И4/И13/И20.
- **Ключ эрозии не меняется:** `treeVersion` не входит в `(seed, worldVersion, erosionVersion,
  resolution)`; S3 §2.5/И20 и S4 §4.2 не правятся. **Поле `treeVersion` вводится шагом 1, не 0.**
- **Дефолт в центр запрещён:** в v2 нет `DomeLabSite` fallback; нет места — `found = false` →
  `HasStation = false`, **без `LabCenter = zero`**, и откат резерва/схемы.
- **`Auto` не разрешается в `Bastion`:** если `Auto` даёт `Bastion`, это откат, а не v2.
- **Всё новое — только `WorldVersion >= 2`;** `worldVersion <= 1` — A/C/B0/Pavilion v1 бит-в-бит.
- **G7-гейт — S5 §4:** 0/50/100 + Р1–Р4 + три смотрящих + геймдизайнер; **станция ① — `Entry`**
  (S2 §5.1); `Entry +120 м` — exemplar/Р3; 6/19 и три станции — ART, не S5.
- G0–G4 красные — шаг не коммитится; G7 не «перекрывает» числа.
- G8 красный — уменьшать **площадь** новой формы/пятна, не плотность.
- `--no-verify` запрещён; в `main` — через гейт.
- A/C/B0/Pavilion v1 изменились — **стоп**, это ошибка, а не «побочный эффект».
- Кадр переснимается только при нарушении протокола; «пока не угадают» — подкрутка ворот.
- Пиксельные ассерты не вводить; метаданные — да. `[предложение]` не assert.

## Зависимости

```
0 → 1 → 2 → 3 → 4
              ↘ 5
всё → 6
```

Каньон (4) может идти параллельно параметрам (5) после схем (3). Если каньон выбран `dry`/`absent`,
шаг 4 не блокирует остальные сиды, но набор сидов называется в шаге 0.

## Что этот план НЕ делает

- Не пишет код в этом заходе.
- Не правит тела S1–S5; S3 — только письмом/шапкой docs-keeper.
- Не реализует эрозию/дороги (S4), B1, интерьер бункера, тела/виды, физику воды, Indirect/LOD,
  новые внешние ассеты.
- Не назначает порогов M1–M16; числа — S5.
- Не заменяет плейтест (Л3).

## P0 второго ревью 03.11 — закрытие

| # | P0 | статус | где закрыто |
|---|---|---|---|
| 1 | **G7-гейт:** не менять S2 §5.1; станция ① — `Entry`; 0/50/100 + Р1–Р4 + три смотрящих + геймдизайнер по S5; `Entry +120 м` — только exemplar/Р3; 6/19 и три станции — ART, не S5 | **закрыт** | спека §0 п.10, §5.2, §6 шаги 3/6; ART-пометки; план шаг 6, стоп-правила |
| 2 | **S3-отмена:** §2.1 п.1 + `Hydro0`, §2.2 п.2/п.3, §3.3 п.2, §4, §7.2 п.2/п.5; тест станции — `TerrainStationTests` | **закрыт** | шапка `Отменяет`, §3.5–§3.6, §5.1; план шаг 2 |
| 3 | **Плато:** строит `landform`, не `DomeHill`; резерв — по низу 144×129, пятно 58×48 — по верху 70×55 | **закрыт** | спека §3.4, §3.6, §5.1; план шаг 2 |
| 4 | **ART-схемы:** станция — край плато `LabDoor`, вход `Auto`, не «плечо/скала/под землёй по схеме» | **закрыт** | пометка в `ART-2026-11-03-shemy.md` |
| 5 | **Нет станции:** `!site.found` → `HasStation = false`, без `LabCenter = zero`; тест «центра нет» | **закрыт в спеке/плане; код шага 2** | спека §3.6, §5.1; план шаг 2, стоп-правила |
| 6 | **`treeVersion`/`Auto`/v2-тест:** поле — шаг 1 (не 0), запись в `ForestBuilder`; `Auto` не `Bastion`; `TerrainStationTests` — под v2 | **закрыт в спеке/плане; код шагов 1–2** | спека §4.1, §5.1, §6; план шаги 0–2 |

**P1 — закрыто в спеке/плане:** 150 м по `LabDoor` — тавтология при 1A (факт — `Entry → LabCenter`);
`G7Parity` — один `PoiWhitelist`; манифест рядом с кадрами; `absent` ≠ `invalid`; `near` H 2–6 /
`cape` 4–8; `LabShell` назван POI; `WorldFingerprint` помечает `treeVersion`; `ForestPavilion` —
единственный источник v1/v2 (профили — тестовые); хорда — 909 м (`LabDoor`), центр плато — 927 м;
B0 = 6 змеиных + сова; `G0–G4` на `p10`, не 20/20; `[предложение]` не assert.

**Осталось (не P0):** reciprocal-пометки S3 и запись спеки в `УКАЗАТЕЛЬ` — docs-keeper; `worldVersion:
2` в сцену — S1 шаг 8; код шагов 0–6 — не начат; G8/G6/плейтест — вне этой спеки. **Не коммичено.**
