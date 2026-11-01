# План S4 — эрозия и дороги (P5 full + P6)

**Дата:** 2026-11-01. **HEAD:** `85e2e1bb`.
**Спека:** [`2026-11-01-landshaft-s4-eroziya-i-dorogi.md`](../specs/2026-11-01-landshaft-s4-eroziya-i-dorogi.md).
**Ветка:** `les/runtime-builder`. **Статус:** ЧЕРНОВИК; решения не приняты.
**Сейчас создан только этот документ; код и другие документы не тронуты.**

Правило: каждый шаг — вертикальный срез, играбельный после шага. Сначала данные/детектор,
потом поведение. A/C/B0 не меняются без `worldVersion` и отдельного профиля.

## Зависимости (без них не начинать)

| зависимость | что нужно | кто даёт |
|---|---|---|
| **S1** | `HeightField`, `SamplingContract`, `worldVersion`, детерминированный `SeededHash` | S1 |
| **S2** | семь форм и их footprint (входят в `preErosion` и маску); S4 их не двигает | S2 |
| **S3** | крюк `SampleGround`: `preErosion → Erosion → потребители`; поляны/коридоры/флора/NavMesh читают `SampleGround` | S3 |
| **B1** | `ForestPlace`, `ForestLink`, `ForestPlaces`: primary POI и связи | B1 (в коде пока нет) |
| **S5** | общий гейт 20 сидов и отчёт | S5 |

**Стоп-условие:** если S3 не даёт крюк, S4 делает только офлайн-прототип и не трогает
`DomeWorld`. Если B1 не дал POI, P6 не начинается — дороги не подменяются `Entry/LabDoor`.

## Task 1 — контракт слоя и маска (S4, без картинки)

- [ ] `ErosionConfig` (ScriptableObject): `enabled`, `version`, `grid 512`, `talus 30`,
      `iterations 6`, `rate 0.4`, `clamp 0.4`, `hydraulicBake`.
- [ ] `ErosionMask`: зоны каньона (`Contains + 6`), террасы (`+blend+5`), площадки станции
      (`PadHalfWidth/Depth + PadBlend`), рампы (`RampLength/RampWidth + 1`), расщелины
      (`CreviceLength/Width + 1`), холма, **форм S2 (footprint + blend)**, входа R12,
      воды (озеро/русло +2), кольца (−20).
- [ ] `preErosion` снимок: `BaseGround + Relief`, **не** `SampleGround`.
- [ ] `DomeErosion.Apply(state, h, x, z)`: билинейно, вне сетки/null — тождество.
- [ ] `ErosionFieldTests.Off_IsIdentity`, `ProtectedMask_IsZero`.
- [ ] `GenTrace`: слой `erosion`, кандидатов/принято/время.
- **Проверка:** EditMode зелёный; сборка без флага не меняет ни одного числа A/C.
- **Коммит:** `лес: контракт эрозии и защищённые зоны`.

## Task 2 — офлайн-прототип тепловой эрозии (Python/Anaconda)

- [ ] `Tools/Erosion/thermal.py`: 512², 8 соседей, talus 24/28/30/32, 4/6/8 итераций,
      сохранение массы; вход — поле из `SampleGround` (снимок), не сырое.
- [ ] Отчёт «до/после»: p50/p95/p99, доля 10–22°, >35°, закрытые ямы, время.
- [ ] Сравнение 3 сидов (1337, 4, 9), не 20: цель — выбрать параметры, не доказать мир.
- **Гейт:** если p95 и доля 10–22° не лучше P5-lite — **остановить P5 full**,
  зафиксировать в `GenTrace`/доке и закрыть P5 на lite. Это допустимый исход.
- **Коммит:** `лес: офлайн-метрика тепловой эрозии` (только инструмент и числа, без игры).

## Task 3 — тепловая эрозия в сборке

- [ ] `DomeErosion.Build(cfg, preErosion, mask, params)`; фиксированный обход, без Job-редукции.
- [ ] `ForestBuilder.buildThermalErosion` (по умолчанию `false`), `erosionVersion`.
- [ ] Вызов в `SampleGround` — **через крюк S3**; `preErosion` не читает `SampleGround`.
- [ ] `ErosionFieldTests.Thermal_IsDeterministic`, `Thermal_ConservesMass`,
      `Thermal_RespectsTalus`, `Drainage_HasNoClosedPits`, `TwentySeeds_SlopeDistribution`.
- [ ] `WorldFingerprint`: `worldVersion`, `erosionVersion`, `Erosion`-дельты не входят в A/C.
- **Проверка:** 20 полевых сидов ≤60 с; p95/ямы в допуске; A/C — 52/700/10.
- **Коммит:** `лес: тепловая эрозия в сборке за флагом`.

## Task 4 — защита каньона, рампы, станции (играбельный срез)

- [ ] `ErosionProtectedZonesTests.CanyonFloor_IsUnchanged` (дно ±0,05 м, не выше
      `lakeLevel+0.45`), `RampGrade_IsUnchanged`, `StationPad_IsFlat`, `Crevice_NotFilled`,
      `LakeAndRiver_NotRaised`.
- [ ] `Erosion_DoesNotMoveEntry` / `Ring_IsUnchanged` (в том же файле).
- [ ] Если `ForestSpace.Build` всё ещё на `BaseGround` — тест-сторож: уклон поляны/коридора
      на `SampleGround` ≤22°, иначе падение с trace (дефект S3, не S4).
- [ ] `ForestPavilionTests.Relief_IsPresent`, `Geography_Off_IsNoOp` — без правок.
- **Проверка:** PlayMode на `ForestPavilion` (сид 1337); кадры каньона/станции/рампы.
- **Коммит:** `лес: защита каньона и станции от эрозии`.

## Task 5 — гидроэрозия офлайн-ассетом

- [ ] `Tools/Erosion/hydraulic.py`: droplets 60–120 тыс., 32–48 шагов, 512²; дельта R16,
      квант 0,02 м, кламп ±1,5 м.
- [ ] `ErosionBake` + библиотека: ключ `(seed, worldVersion, erosionVersion, resolution)`;
      ссылка на `ForestBuilder`, без `Resources` для всех сидов.
- [ ] `DomeErosion` читает дельту ассета; **нет ассета** — гидро выключена, в trace
      `hydraulic=missing`, тепловая остаётся.
- [ ] `HydraulicBakeTests.BakeKey_IncludesSeedAndVersion`, `MissingBake_FallsBackToThermal`,
      `Bake_DoesNotLowerWater`, `Bake_IsDeterministic`.
- [ ] Запекание для 1337/4/9; случайный сид — без гидро (честно, не «тихо»).
- **Проверка:** EditMode + 3 полные сборки; вода не висит, дно не ниже `lakeLevel+0.35`.
- **Коммит:** `лес: гидроэрозия офлайн-ассетом`.

## Task 6 — приёмка P5 full (решение, а не код)

- [ ] 5 полных сборок (`terrainQuads 8`, сиды 1–5) + 20 полевых: уклоны, дренаж, вода.
- [ ] `Fingerprint` стабилен на `(seed, version, erosionVersion)`; `GenTrace` с временем.
- [ ] Кадры: каньон, рампа, станция, холм — до/после; `ГЕОГРАФИЯ.md` (генерируемый).
- [ ] Сравнение с P5-lite: если нет выигрыша по метрике — **откат флага**, P5 закрыт на lite.
- **Коммит:** `лес: приёмка P5 full — метрика до/после` (или `лес: P5 закрыт на lite`).
- **Стоп:** без решения геймдизайнера P6 не начинать.

## Task 7 — POI от B1 (ожидание, не код S4)

- [ ] B1 отдаёт `ForestPlace`/`ForestLink`/`ForestPlaces`; S4 читает, не создаёт.
- [ ] Заморозить список primary POI: `Entry`, `LabDoor`, 5 арен, вода/брод, терраса.
- [ ] Логова — spur'ы, не узлы MST; `LabAtrium` — не POI дороги.
- [ ] Снимок POI в `GenTrace`; без B1 — `roads=blocked`, P6 не стартует.
- **Коммит:** нет (S4 не пишет код B1); решение — в спеке B1.

## Task 8 — граф дорог: MST + 1–2 петли

- [ ] `ForestRoad` (точки, ширина, `ford`, `primary`), `ForestRoads.Build(places, ground,
      hydro, mask, ...)`.
- [ ] Least-cost по сетке 4 м; стоимость `L·(1+0.08·slope^1.5+3·water+0.4·forest+10·protected)`.
- [ ] MST по стоимостям; петли: 1–2 ребра, `μ=E−V+1`, каждая петля содержит POI.
- [ ] `RoadGraphTests.Mst_ConnectsAllPrimaryPois`, `Loops_AreOneOrTwo`,
      `NoRoad_ThroughProtected`, `Ford_IsShallow`, `TwentySeeds_Connected`.
- **Проверка:** 20 полевых сидов; все primary в одной компоненте; сеть 2–4 км.
- **Коммит:** `лес: граф дорог — MST и петли`.

## Task 9 — поверхность дорог и стык с B-lite

- [ ] `DomeRoadMesh`/`DomeRoads`: лента 4 м / spur 3 м по `RenderedGround`, подъём 0,10 м;
      без коллайдера (или тонкий ≤0,05 м); брод ≤0,6 м; уклон ≤12°.
- [ ] Вырубка деревьев на дороге — до `DomeFlora` (порядок S3); дорога не режет центр арены.
- [ ] Совпадение с `Corridors`: одна лента (дорога), `DomeSpacePaths` не дублируется;
      `Corridors`/`Clearings` не переопределяются.
- [ ] `RoadSurfaceTests.Road_FollowsGround`, `NoTreesOnRoad`, `NavMesh_RoadToLab`,
      `CorridorOverlap_NoDoubleRibbon`.
- **Проверка:** PlayMode `ForestPavilion`; `Entry → LabDoor`, 5 арен, брод — `PathComplete`.
- **Коммит:** `лес: лента дорог и стык с коридорами B-lite`.

## Task 10 — приёмка S4 и документы

- [ ] 20 полевых + 5 полных; уклоны/дренаж/вода/петли/NavMesh/A/C — таблица в спеке.
- [ ] `Fingerprint` с версиями и дорогами; `GenTrace` времени сборки.
- [ ] Кадры: арена, дорога, брод, каньон, станция; `ГЕОГРАФИЯ.md`/`ИНДЕКС.md`.
- [ ] `СТАТУС`/`УКАЗАТЕЛЬ` — только при принятии (сейчас не правились по указанию).
- [ ] `python Docs/tools/spec_status.py` — зелёный (после внесения в указатель).
- **Коммит:** `лес: S4 — эрозия и дороги, приёмка по тестам и кадрам`.
- **Плейтест:** за геймдизайнером; без него S4 — «собрано», не «принято».

## Точки коммита (сводка)

| # | коммит | что можно откатить |
|---|---|---|
| 1 | `лес: контракт эрозии и защищённые зоны` | маска, конфиг |
| 2 | `лес: офлайн-метрика тепловой эрозии` | прототип |
| 3 | `лес: тепловая эрозия в сборке за флагом` | флаг/слой |
| 4 | `лес: защита каньона и станции от эрозии` | зоны |
| 5 | `лес: гидроэрозия офлайн-ассетом` | ассет/loader |
| 6 | `лес: приёмка P5 full — метрика до/после` | решение P5 |
| 8 | `лес: граф дорог — MST и петли` | граф |
| 9 | `лес: лента дорог и стык с коридорами B-lite` | лента |
| 10 | `лес: S4 — эрозия и дороги, приёмка` | весь срез |

## Гейты

- **Г1:** S3-крюк есть; иначе Task 3 не начинается.
- **Г2:** офлайн-метрика P5 оправдала эрозию; иначе P5 закрыт на lite.
- **Г3:** B1 отдал POI; иначе P6 не начинается.
- **Г4:** A/C-базлайн 52/700/10 и B0 12 — зелёные.
- **Г5:** 20 полевых + 5 полных; NavMesh `PathComplete`; `Fingerprint` стабилен.
- **Г6:** плейтест — последним, за геймдизайнером.

## YAGNI

Без stream-power в рантайме, WFC, физики/плавания, мостов/тоннелей, выемки/насыпи дорог
в первом срезе, Indirect/LOD, правок тел, плана, климата, зверей, новых видов.
S4 не переписывает `SampleGround` — это S3; не выбирает POI — это S2/B1.
