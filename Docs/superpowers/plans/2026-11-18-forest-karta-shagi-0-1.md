# Лес: карта как источник — шаги 0–1. План реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** закрепить мир павильона на v2 отпечатком (шаг 0) и ввести тип `ForestMap` — карту фактов в метрах, снятую
с мира v2, с JSON и хешем содержания (шаг 1).

**Architecture:** шаг 0 — профиль `ForestProfiles.PavilionV2Zone` и PlayMode-сторож `ForestMapV2BaselineTests`, который
держит отпечаток и хеши адресов, логов, разметки и стволов. Шаг 1 — сериализуемый `ForestMap` (Forest/Map) и
`ForestMap.FromWorld(DomeWorld, ForestBuilder)`: снимок фактов с уже собранного мира. Поток «мир читает карту» —
шаги 2–5, следующий план.

**Tech Stack:** Unity 6 (6000.4.7f1), C#, NUnit (EditMode/PlayMode), `JsonUtility`; прогон — `unity command run_tests`
(скрипт `runtests.sh` в scratchpad, `--include_explicit` для [Explicit]).

**Spec:** `Docs/superpowers/specs/2026-11-18-forest-karta-kak-istochnik.md` (§4, §5, §9, §11 шаги 0–1)

## Global Constraints

- Работа в `line/les`; павильон в `main` остаётся на v1. Сцена `ForestPavilion` в этом плане не меняется.
- v1 не трогается: `BaselineFingerprintTests`, `ForestBaselineTests` зелёные без правки констант.
- Каньон и акведук выключены в профиле v2 павильона (заморожены, ассеты в теге-архиве).
- Плоскость карты: центр (0,0), x — восток, z — север, метры; JSON через `JsonUtility`; поле `schema` = 1.
- Подлесок вне карты.
- Числа мира (плотности, вместимости) не меняются.

## Review Focus

1. **Отпечаток, не учитывающий v2-слои.** `WorldFingerprint` мерит пол сеткой 32×32 и адреса; стволы и дороги в него не
   входят. Сторож шага 0 добавляет свои хеши стволов и дорог — Task 0, тест `PavilionV2Zone_PinsTreesAndRoads`.
2. **Сериализация теряет точность или порядок.** JSON туда и обратно обязан дать тот же хеш карты — Task 1,
   `RoundTrip_KeepsHash`.
3. **Хеш зависит от времени сборки или от порядка словаря.** Две сборки одного сида — один хеш — Task 1,
   `SameSeed_SameMapHash`.
4. **Пустые слои мира** (нет лаборатории или озера на каком-то сиде): карта не падает, слой пуст, флаг `has*` = false —
   Task 1, `MissingLab_GivesEmptyLabLayer` (сборка с `buildLabComplex = false`).
5. **Площадь суши больше площади мира или отрицательна.** Task 1, `Areas_AreSane`: 0 < суша < всё, всё ≈ π·R² ±1 %.

---

### Task 0: профиль павильона v2 и его отпечаток

**Files:**
- Modify: `Assets/_Chimera/Scripts/Forest/Generation/ForestProfiles.cs` (новый метод рядом с `PavilionV2`)
- Modify: `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestBaselineTests.cs` (хеш-функции `GroundHash`, `AddressHash`,
  `LairHash`, `SpaceHash` — `private static` → `internal static`, тела не меняются)
- Create: `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestMapV2BaselineTests.cs`

**Interfaces:**
- Produces: `public static ForestBuilder ForestProfiles.PavilionV2Zone(GameObject host, long seed = 1337)` — это
  `PavilionV2(host, seed, erosion: false)` плюс `buildThermalErosion = true`, `buildCanyon = false`,
  `buildAqueduct = false`, `buildAnimalLairs = true`, `buildBeacon = true` (ровно профиль замера «v2 + тепловая»).
- Produces: `internal static ulong ForestMapV2BaselineTests.TreeHash(ForestBuilder)` — FNV по `TreeColliderPositions`
  (x, z квантованы как в `ForestBaselineTests`) и `Trees`; `internal static ulong RoadHash(ForestBuilder)` — FNV по
  точкам дорог (`b.Roads`) — если у билдера нет аксессора дорог, добавить `public ForestRoads.Network Roads` в
  `ForestBuilder` рядом с `Zones`.

- [ ] **Step 1: тест с пустыми константами**

`ForestMapV2BaselineTests`: `[UnityTest] PavilionV2Zone_PinsWorld` строит `PavilionV2Zone`, проверяет
`WorldVersion == 2`, `buildCanyon == false`, `Plan != null`, затем `Assert.AreEqual(Fingerprint…, builder.Fingerprint)`,
`GroundHash`, `AddressHash`, `LairHash`, `SpaceHash` с константами `0UL`. Второй тест `PavilionV2Zone_PinsTreesAndRoads` —
`TreeHash`, `RoadHash` против `0UL`. Тесты печатают фактические значения в `Debug.Log` формата
`const ulong X = 0x…UL;`. `CapturedAt = "2026-11-18 @ <HEAD>"`.

- [ ] **Step 2: прогнать — падает, в логе — фактические значения**

Run: `runtests.sh OUT "playmode|ForestMapV2BaselineTests"` · Expected: 2 failed, в `unity.log` строки `const ulong`.

- [ ] **Step 3: вписать значения и прогнать дважды**

Константы — из лога. Два прогона подряд зелёные (детерминизм). Затем `ForestBaselineTests` — зелёный (v1 не задет).

- [ ] **Step 4: ворота G0–G8 на профиле павильона v2 — исходное состояние**

`ForestLandscapeGateTests.BuildWorld` переводится на `ForestProfiles.PavilionV2Zone(host, 1337L)` (каньон заморожен —
прежний профиль на ветке не собирается). `Hydro_Gate` — тот же профиль с `buildHydraulicErosion = true`. Прогон; итог
записать в `Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-ворота.txt` (тест пишет сам). FAIL ворот — не чинить в этом плане, а
перечислить в описи шага 8 (это исходное состояние v2, а не поломка переносом).

- [ ] **Step 5: коммит**

```bash
git add Assets/_Chimera/Scripts/Forest/Generation/ForestProfiles.cs Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/*.cs* "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-*"
git commit -m "Лес, карта шаг 0: профиль павильона v2 без каньона и акведука, его отпечаток — сторож ForestMapV2BaselineTests; ворота на нём"
```

### Task 1: тип `ForestMap` — снимок фактов мира v2

**Files:**
- Create: `Assets/_Chimera/Scripts/Forest/Map/ForestMap.cs` (тип и слои — вложенные `[Serializable]` классы)
- Create: `Assets/_Chimera/Scripts/Forest/Map/ForestMapHash.cs` (FNV по содержанию)
- Modify: `Assets/_Chimera/Scripts/Forest/Generation/ForestBuilder.cs` — свойство `public ForestMap Map { get; private set; }`,
  заполняется в конце `Build()` (`ForestMap.FromWorld(world, this)`), обнуляется в `Clear()`
- Test: `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestMapTests.cs`

**Interfaces:**
- Consumes: `ForestProfiles.PavilionV2Zone` (Task 0).
- Produces:
  - `[Serializable] public sealed class ForestMap` с полями: `int schema = 1; long seed; int worldVersion; float diameter;
    string hash;` и слоями `Water water; Ring ring; Points points; Lab lab; Areas areas;`
  - `Water { bool hasLake; Vector2 lakeCenter; float lakeRadius, lakeLevel, lakeDepth; List<Vector2> river; float riverHalfWidth, riverDepth; }`
  - `Ring { float inner, outer; }` — из `DomeWorld.Masks.RingInner/RingOuter`
  - `Points { Vector3 entry, spawn; }` — `Entry`, `SpawnPoint` в координатах плоскости (не мировых)
  - `Lab { bool has; Vector2 center; float yawDeg; Vector3 door; Vector2 outward; }` — `outward` — как в `ForestBuilder.TryGetExit`
  - `Areas { float totalKm2, landKm2; }` — `totalKm2` = π·inner²; `landKm2` — подсчёт по сетке 4 м внутри `inner`, где
    `DomeAnimalLairs.WaterDistance(hydro, p) >= 0`
  - `public static ForestMap FromWorld(DomeWorld world, ForestBuilder b)`; `public string ToJson()`;
    `public static ForestMap FromJson(string)`; `public static string ComputeHash(ForestMap)` (хеш без поля `hash`)

- [ ] **Step 1: тесты**

`ForestMapTests` (PlayMode, мир `PavilionV2Zone` собирается раз на класс в `[OneTimeSetUp]`):
- `Map_IsBuiltForV2`: `builder.Map != null`, `schema == 1`, `worldVersion == 2`, `seed == 1337`, `diameter == 2000`, `hash` не пуст.
- `Map_MatchesWorld`: `map.lab.door` в мире = `builder.LabDoor` (через `FieldToWorld`/корень, допуск 1e-3);
  `map.points.spawn` ↔ `builder.SpawnPoint`; `map.water.lakeRadius == builder.Hydro.lakeRadius`.
- `RoundTrip_KeepsHash`: `ComputeHash(FromJson(map.ToJson())) == map.hash`.
- `SameSeed_SameMapHash`: вторая сборка того же профиля — тот же `hash`.
- `Areas_AreSane`: `0 < landKm2 < totalKm2`; `totalKm2` в пределах 1 % от `π·inner²/1e6`.
- `MissingLab_GivesEmptyLabLayer`: `PavilionV2Zone` с `buildLabComplex = false` → `map.lab.has == false`, исключений нет.
- Сторож шага 0 (`ForestMapV2BaselineTests`) — зелёный без правки констант.

- [ ] **Step 2: прогнать — не компилируется / падает**

Run: `runtests.sh OUT "playmode|ForestMapTests"` · Expected: ошибка компиляции (`ForestMap` нет).

- [ ] **Step 3: реализовать `ForestMap`, `ForestMapHash`, свойство `Map` у `ForestBuilder`**

Только чтение готовых полей мира; ни одного нового вычисления, кроме площади суши. Хеш — FNV-1a по
квантованным (×1000, `Mathf.RoundToInt`) числам слоёв в фиксированном порядке полей.

- [ ] **Step 4: прогнать `ForestMapTests`, `ForestMapV2BaselineTests`, `ForestBaselineTests`, EditMode целиком**

Expected: всё зелёное, известные неопределённые — те же два EditMode и `B0_PlanExpectsFiveSnakeLairs_KnownDrift`.

- [ ] **Step 5: коммит**

```bash
git add Assets/_Chimera/Scripts/Forest/Map Assets/_Chimera/Scripts/Forest/Generation/ForestBuilder.cs Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestMapTests.cs*
git commit -m "Лес, карта шаг 1: ForestMap — рамка, вода, кольцо, вход и появление, лаборатория, площади; JSON и хеш"
```

## Дальше (следующий план)

Шаг 2 — рельеф в карте: рецепт поля v2 (`DomeTerrain` с композицией, падами, эрозией) как `Height(x,z)` карты; 3D-чанки
читают карту. Нужен отдельный разбор: что из `DomeTerrain` сериализуется рецептом, а что (сетка эрозии) — данными с
ключом кеша.
