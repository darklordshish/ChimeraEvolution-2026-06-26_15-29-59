# Лес s1b: аппликатор террейна + запекание NavMesh + связность

- Статус: ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 84/84 + EditMode без регрессий 18.09)
- Отменяет: —
- Отменена: —
- Продолжает: `2026-09-18-forest-s1-worldgen-seed-relief.md` (поле высот и правила оттуда)

## Проблема

Поле высот s1 висит в воздухе: ни один меш его не рисует, NavMesh о нём не знает.
Нужен мост «данные → сцена», который ничего не хранит в сцене (как стенд Полигона:
построенное — мусор, второй источник правды запрещён).

## Решение

`TerrainApplier` (`Scripts/Forest/Generation/`, `MonoBehaviour`, требует `NavMeshSurface`):

- `Configure(cfg, resolution, size)` — только параметры, клампы внутри.
- `Build()` — фасеточная сетка `resolution×resolution` квадов из `WorldHeightField.SampleHeight`
  (вершины продублированы под грани — тот же язык, что `BoneMesher.Flat`; порядок обхода
  `(p00,p11,p10)+(p00,p01,p11)` даёт нормали +Y — обратный смотрел вниз, поймал тест), `MeshFilter + MeshRenderer + MeshCollider` в ребенке `Terrain`,
  `collectObjects = Children` (паттерн `ArenaWalls.cs:50-52`, читали рид-онли).
- `BakeNavMesh()` — синхронный `Surface.BuildNavMesh()` (тот же вызов, что у арены).
- `TrySample/PathExists` — тонкие обёртки `NavMesh.SamplePosition/CalculatePath` для детекторов.
- `Clear()` — снос построенного (`DestroyImmediate` вне Play); сцена после работы чистая.
- Материал не назначаем (геометрия печётся без него) — свет/палитра за артом в s3.

Порядок обхода квада: `(p00, p10, p11) + (p00, p11, p01)` — front по часовой сверху, нормали +Y.

## Инварианты (PlayMode-тест `Tests/PlayMode/Forest/Worldgen/TerrainApplierTests.cs`)

- Меш построен: `vertexCount == res·res·6`, `normals[0].y > 0.9`.
- `BakeNavMesh()` → `IsBaked`.
- 8 сидированных точек стоят на NavMesh (`TrySample`).
- Угол связан путём с углом (`PathExists`, `PathComplete`).

## Что НЕ делаем

- Постоянный террейн в сцене, сохранение сцены (тест всё сносит; сцену не сохранять).
- Биомы/scatter/декор (s2), погода/свет (s3), LOD/SRP (после финальных мешей — см. бриф).
- Правки `Arena/` и существующих файлов — только чтение.

## Приёмка

`run_tests --mode playmode` зелёный по новому тесту + `run_tests --mode editor` без регрессий.
Плейтест не нужен (логика/инфра — по таблице приёмки `CLAUDE.md`).
