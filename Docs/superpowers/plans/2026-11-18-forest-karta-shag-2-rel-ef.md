# Лес: карта как источник — шаг 2, рельеф в карте. План реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** рельеф v2 — слой карты: сериализуемый рецепт поля (`ForestMap.Relief`) с функцией `Height(x,z)`; 3D-чанки v2
строят меш по высоте карты, а не по полю мира.

**Architecture:** `DomeTerrain` уже чистый рецепт (конфиг, гидро, терраса, формы композиции, холм, эрозия, пады
площадки и коридоров). `Relief.From(DomeTerrain)` снимает его в сериализуемые поля и СОБИРАЕТ СВОЮ копию
`DomeTerrain` из них же (`ToTerrain()`), так что `Height` читает карту, а не мир. Чанки v2 получают `Relief.Height`.
Сетка эрозии — данные в JSON; в хеш карты входит ключ эрозии, а не сетка (ГеймБосс 18.11: float-разнобой машин не
должен менять хеш).

**Tech Stack:** Unity 6, C#, NUnit PlayMode, `JsonUtility` (`ScriptableObject` конфига — `ToJson`/`FromJsonOverwrite`).

**Spec:** `Docs/superpowers/specs/2026-11-18-forest-karta-kak-istochnik.md` (§5 сторожа 2–3, §9 растры производные,
§11 шаг 2)

## Global Constraints

- Отпечатки `ForestMapV2BaselineTests` и `ForestBaselineTests` не меняются.
- v1 не трогается: чанки v1 читают `SampleGround`, как раньше.
- Каньон, овраги и холм с дыркой (`DomeHill.State.hole`) в рецепт не входят: v2-павильон их не строит (каньон
  заморожен, `stationHill = false`). Встретив их, `Relief.From` бросает `NotSupportedException` с причиной, а не
  молча теряет слой.
- Хеш карты: рецепт и ключ эрозии `DomeErosion.Key(seed, worldVersion, erosionVersion, resolution)`; без сетки.

## Review Focus

1. **Поле мира меняется после снятия рецепта** (пады, площадка): высота карты разошлась бы с миром — Task 1,
   `Relief_HeightEqualsWorldGround` сверяет ВСЮ сетку 385×385 после сборки, побитно.
2. **JSON теряет точность float** (`JsonUtility` пишет float с потерей?) — Task 1, `Relief_JsonRoundTrip_SameHeights`
   побитно по той же сетке.
3. **Новый `[Serializable]` протекает в сериализацию сцены** (поле-состояние в компоненте) — Task 1, шаг проверки:
   `ForestPavilion.unity` не меняется после сборки и сохранения (`git diff` пуст).
4. **Хеш зависит от сетки эрозии** — Task 1, `Relief_HashIgnoresErosionGrid`.
5. **Чанк читает не карту** — Task 2, `Chunks_ReadMapRelief`: вершины чанков = `Relief.Height`, а у карты, собранной
   из JSON, тот же меш.

---

### Task 1: `ForestMap.Relief` — рецепт поля и его высота

**Files:**
- Create: `Assets/_Chimera/Scripts/Forest/Map/ForestMapRelief.cs` (вложенный в `ForestMap` partial или отдельный
  `[Serializable] public sealed class ForestMapRelief`; поле `public ForestMapRelief relief` у `ForestMap`)
- Modify: `DomeHydro.cs` (`State` — `[Serializable]`, `riverIndex` — `[NonSerialized]`), `DomeTerrace.cs`, `DomeHill.cs`
  (`State` — `[Serializable]`, `hole` — `[NonSerialized]`), `DomeErosion.cs` (`State` — `[Serializable]`),
  `DomeTerrain.cs` (`CorridorPad` — `[Serializable]`), `ForestMap.cs` (поле `relief`, хеш), `ForestMapHash.cs`
- Test: `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/ForestMapReliefTests.cs`

**Interfaces:**
- Produces: `ForestMapRelief.From(DomeTerrain t, long seed) : ForestMapRelief` (снимает рецепт и собирает копию);
  `float Height(float x, float z)` (= `SampleGround` копии); `float Shape(float x, float z)` (= `ShapeGround` копии);
  `DomeTerrain ToTerrain()`; `void Restore()` (после `FromJson`: конфиг из `cfgJson`, `RiverIndex`, копия террейна);
  `string erosionKey`.
- `ForestMap.FromWorld(world, seed, version, spawn)` берёт `world.MapRelief` (Task 2) или, если его нет, снимает
  `ForestMapRelief.From(world.Terrain, seed)`.

- [ ] **Step 1: тесты** (мир `PavilionV2Zone` раз на класс)
  - `Relief_HeightEqualsWorldGround`: на сетке шага `Contract.meshStep` от −1000, 385×385 —
    `map.relief.Height(x,z) == builder.SampleGround(x,z)` побитно (`BitConverter.SingleToInt32Bits`), счётчик расхождений 0.
  - `Relief_JsonRoundTrip_SameHeights`: `ForestMap.FromJson(map.ToJson())` → `Restore()` → та же сетка побитно; `hash` тот же.
  - `Relief_HashIgnoresErosionGrid`: копия карты с изменённым `relief.erosion.delta[0]` — тот же `ComputeHash`; с другим
    `erosionKey` — другой.
  - `Relief_RefusesUnsupportedLayers`: `DomeTerrain` с `hill.valid = true` и `hole != null` → `NotSupportedException`.
- [ ] **Step 2: прогнать — красное (нет типа)**
- [ ] **Step 3: реализовать.** Конфиг — `JsonUtility.ToJson(cfg)` в `cfgJson`, восстановление — `CreateInstance` +
  `FromJsonOverwrite`. Состояния копируются через JSON (глубокая копия, а не ссылка на объект мира).
- [ ] **Step 4: прогнать `ForestMapReliefTests`, `ForestMapTests`, `ForestMapV2BaselineTests`, `ForestBaselineTests`;
  собрать `ForestPavilion` и убедиться, что сцена не изменилась** (`git status` чист по `Assets/Scenes`).
- [ ] **Step 5: коммит** «Лес, карта шаг 2а: рельеф v2 — рецепт поля в карте, Height по своей копии; хеш — ключ эрозии, не сетка»

### Task 2: чанки v2 читают высоту карты

**Files:**
- Modify: `DomeWorld.cs` — свойство `public ForestMapRelief MapRelief { get; private set; }`; для v2 рельеф карты
  снимается в `EnsureChunks` перед `BuildChunks`, `BuildChunks` передаёт `DomeChunkMesh.BuildChunkMesh` высоту
  `MapRelief.Height` (v1 — `SampleGround`, как было); отметка трассы `t.map.relief`. `Clear` обнуляет.
- Modify: `ForestMap.cs` — `FromWorld` берёт `world.MapRelief`.
- Test: `ForestMapReliefTests.Chunks_ReadMapRelief`

**Interfaces:** Consumes Task 1 `ForestMapRelief`. Produces `DomeWorld.MapRelief`.

- [ ] **Step 1: тест `Chunks_ReadMapRelief`** — у трёх чанков (центр, у озера, у кольца) каждая вершина меша (в
  координатах плоскости) имеет `y == map.relief.Height(x,z)` побитно; и `ReferenceEquals(builder.Map.relief, world.MapRelief)`.
- [ ] **Step 2: прогнать — красное** (`MapRelief` нет).
- [ ] **Step 3: реализовать**.
- [ ] **Step 4: прогнать** `ForestMapReliefTests`, `ForestMapTests`, `ForestMapV2BaselineTests`, `ForestBaselineTests`,
  EditMode, `ForestPavilionFirstZoneTests`, `ForestLandscapeGateTests` (ворота G0 — навмеш против пола — на чанках из карты).
- [ ] **Step 5: коммит** «Лес, карта шаг 2б: чанки v2 строятся по высоте карты»
