---
name: unity-gotchas
description: Use when touching Unity scenes, prefabs, Editor code, or lifecycle C# in CHIMERA. Checklist of mines already paid for, including mesh-era and forest-preview traps.
---

# Unity Gotchas (CHIMERA, проверено)

Этот чеклист — выжимка; источник — `CLAUDE.md`, разделы «Unity-гочи» и «Морфология».

## API и жизненный цикл

- `FindAnyObjectByType` (не `FindFirstObjectByType`), `FindObjectsByType<T>()` без сортировки.
- `InputAction` — без `expectedControlLayout`.
- Перекомпиляция в Play обнуляет несериализованные поля — не баг, стоп Play → пересборка, новый Play.
- Новое `[SerializeField]` у уже лежащего объекта приходит нулём (инициализатор — только для новых).
  Читать 0 как «не настроено», дефолт свойством-обёрткой, выводить в дев-панель.
- `Object.Destroy` отложен до конца кадра → сноси все одноимённые, `SetActive(false)` сразу, переименовывай.
- `CharacterController` перебивает `position` после `Instantiate` → позиция в `Instantiate(prefab,pos,rot)`
  или гаси CC на время телепорта.
- Высоты — ОТ ЗЕМЛИ; корень у видов разный (волк на земле, игрок — центр капсулы) → сдвигай к низу CC.

## Сцена, префабы, тюнинг

- `git mv .cs + .cs.meta` (GUID); файлы двигать в Unity или с `.meta`.
- `UnityEvent = new()` при `AddComponent`.
- Тинт у ВСЕХ; конфликт с `Telegraph` → `Telegraph.Rebase()` в конце `UpdateTint`.
- Имена морф-частей — контракт (`IsOwnFace`, `IsHeadName`); после сборки —
  `RebuildRenderers()` + `ReapplyFirstPerson()`.
- Неравномерный масштаб родителя плющит детей → вешай на пустой узел (масштаб 1), меш внутрь.
- Тюнящиеся системы — объектом в сцену; бутстрап только для ненастраиваемого.
- Префабы — генераторами (`Chimera → Создать префаб X`), виды — `chimera-species`, не руками.
- Editor-код — только в `Scripts/Editor/`; правки сцен/ассетов — командой/`run_script`, не YAML.

## Форма (с 13.10)

- `BodyMesh` кэширует импорт; переимпорт FBX сбрасывает кэш через `BodyMeshImport`.
- `PartAssembly.MeshPieces` по умолчанию `true`; ручка нужна для сравнения/тестов, не «выключено в игре».
- `BodyMesh.OrganObjectsOn` — ручка для сторожей; в игре объекты `<слот>@<вид>` видны с прививкой органа.
- `BodySize` масштабирует визуал целиком; корень с физикой остаётся 1, капсула/камера пересчитываются.
- Старый `BoneMesher`-кэш и «мёртвые меши» — уроки поля, всё ещё живые для непереведённых видов.

## Мир/лес (проверено на ветке леса)

- `ForestBuilder` строит мир в `Awake`; `ForestSandbox`/`ForestFragment` — игровые сцены. Старое «скелет» неверно.
- Мир живёт в редакторном `~DomePreview`: `DestroyImmediate`, `FindObjectsByType`, transient SO — это
  редакторный путь, не рантайм. Для игры нужен отдельный builder.
- `ForestClimate` — статики (`CurrentState`, `CurrentTimeMinutes`): при появлении объекта погода
  протекает на все сцены; сбрасывать или делать интерфейс климата.
- `ForestSpawner.Tick`/`LairSite.Tick` сами не тикаются: нужен менеджер/сцена, иначе логова молчат.
- `ForestSpawner.SpawnPos` семплит землю; Y логова не использовать (старый пункт устарел).
- Превью-флора — отдельные `GameObject` (700 деревьев и т.д.); это макет, не рантайм-плотность.

## Станция под куполом и камеры (27.10)

- **Три активные камеры в игровой сцене** — `TopCam`/`SandboxCam` с depth 0 перекрывают `Main Camera` (-1).
  В игре они выключены; `ForestScenicTool.SetupCameras` включает их для кадра.
- `CameraFollow` первое лицо: глаз считается **от земли**, не от корня. `BodySize.FromGround(y) = bottom + y*Scale`;
  `headHeight` в сцене 1.7, `lookHeight` 1.2, третье лицо offset `(0,4,-5)`.
- FPS проверяет стену через `ResolveWall`; буфер `SphereCast` 32 (8 терял стену за своими коллайдерами).
- `DomeSun.shadows = Soft`, крыша станции кастит тень; остальные детали — Off. Без этого солнце светит сквозь крышу.
- `DomeRain` следует за `Camera.main`; внутри `DomeLabComplex.Contains` дождь и туман выключаются.
- `DomeSkyRig.Reset` возвращает `RenderSettings.fog*` и базовые интенсивности внешних directional; `Clear` не оставляет вечную ночь.
- `DomeWeather.ResetWet` возвращает сухой цвет; материалы станции не входят в мокрость.
- `DomeLabDamage` в EditMode использует `DestroyImmediate`, в Play — `Destroy`.

Всегда давай пользователю пошаговую инструкцию «создай X, повесь Y, выставь Z — зачем».
