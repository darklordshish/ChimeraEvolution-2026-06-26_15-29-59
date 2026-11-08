# 17-Aqueduct: вода, лоток и водопад

Объект: `Aqueduct_AW-F_1337` (prefab `AqueductWaterfall`), seed **1337**, v2/Ø2000.
Посадка: `AqueductPlacement`; terrain под телом целый, canyon-cut не используется.

## A3 до/после

- **До:** `AW_WaterFall` — пять вертикальных полос-«балок» толщиной ~0.3–0.5 м, читались как стена.
- **После:** одно полотно `AqueductFallSheet`: ширина **3.35 м** сверху и **2.76 м** снизу, толщина **0.20 м**, изгиб по ширине **0.24 м** и наклон по высоте **0.14 м**. Поверх — **5 тонких лент** (ширина 0.07–0.13 м, разная длина, сходятся к плёсу).
- Вода — тёмная (`AqueductFallUnlit`, 0.045/0.13/0.19), отдельная пена `AqueductFoam`.
- Камень — две полки **за** полотном (z=49.15), не поперёк воды.
- Пена у плёса — кольцо `AW_FallFoamRing` + тонкая пена на губе `AW_FallLipFoam`.
- Водопад имеет толщину **0.20 м**; низ `0.410`, верх плёса `0.480`, перекрытие **0.070**.

## A3 камера

Съёмка сбоку/снизу от плёса:
`A3 (29.61, 9.97, −461.98) → (29.57, 10.75, −471.05)`, FOV 55.
В кадре: губа лотка → наклонное полотно → ленты → всплеск/пена → плёс.

## Причина старой чёрной полосы

`DomeWorld` для `buildCanyon && buildAqueduct` оставлял `Terrain.useHandoffCanyon = true`;
`DomeChunkMesh` выкидывал клетки handoff-контура, а `DomeTerrain` резал поле каньона. Под акведуком
получался разрыв ~18–20 м. Для `buildAqueduct` теперь `useHandoffCanyon=false`, `Terrain.canyon=default`;
старый `DomeCanyonAuthorMesh` не строится. Состояние `Canyon` остаётся для детекторов.

## Файлы

| файл | камера | type | seed |
|---|---|---|---|
| `A1-along-AW-F-1337.png` | A1, вдоль лотка: вода, 3 опоры, целый terrain | AW-F | 1337 |
| `A2-3-4-AW-F-1337.png` | A2, 3/4 | AW-F | 1337 |
| `A3-from-water-AW-F-1337.png` | A3, сбоку/снизу от плёса | AW-F | 1337 |
| `A4-supports-AW-F-1337.png` | A4, опоры и низ | AW-F | 1337 |

`AqueductAgent.VerifyAll`: River/Lake/Reservoir/Waterfall — PASS; вода −0.30.
Целевые тесты: `AqueductWaterTests`, `AqueductCameraTests`, `AqueductFrameTests`, `AqueductWaterCatalogTests` 4/4, `AqueductSeedTests` 2/2.
G0–G5/G8 — 11 passed, 0 failed, 2 inconclusive; v1 baseline — 6 passed, 0 failed, 1 inconclusive.
Это кадры акведука, **не каньона** и не G7-приёмки.
