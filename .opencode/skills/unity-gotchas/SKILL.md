---
name: unity-gotchas
description: Use when touching Unity scene, prefabs, Editor code, or any C# that touches Unity lifecycle in CHIMERA. Checklist of 15 mines bought with debugging sessions.
---

# Unity Gotchas (CHIMERA, проверено)

Весь `CLAUDE.md:37-55` — чеклист `unity-integrator`, но остальные тоже обязаны знать.

## API
- `FindAnyObjectByType` (не `FindFirstObjectByType`), `FindObjectsByType<T>()` без сортировки.
- `InputAction` без `expectedControlLayout`.

## Жизненный цикл
- Перекомпиляция в Play обнуляет несериализованные поля — не баг, стоп Play → пересборка, не хардить.
- Новое `[SerializeField]` у уже лежащего объекта = 0 (инициализатор `=30f` только для новых). Читать 0 как «не настроено», дефолт пропертой, вывод в дев-панель.
- `Object.Destroy` отложен до конца кадра → сноси все одноимённые, `SetActive(false)` сразу, переименовывай.
- `CharacterController` перебивает `position` после `Instantiate` → задавай в `Instantiate(prefab,pos,rot)` или гаси CC.
- Высоты ОТ ЗЕМЛИ, корень разный (волк на земле, игрок центр капсулы) → сдвигай к низу CC.

## Сцена/префабы
- `git mv .cs + .cs.meta` (GUID), файлы двигать в Unity или с `.meta`.
- `UnityEvent = new()` при `AddComponent`.
- Тинт у ВСЕХ (`tintComposition`), конфликт с `Telegraph` → `Telegraph.Rebase()` в конце `UpdateTint` (`CLAUDE.md:45`).
- Имена морф-частей — контракт (`IsOwnFace`, `IsHeadName`) → после сборки `RebuildRenderers`+`ReapplyFirstPerson`.
- Неравномерный масштаб плющит детей → вешай на пустой узел масштаб 1, меш внутрь; капсула по Y вдвое выше куба — доворот.
- Торец капсулы → шар-затычка; мерить в той же СК; список имён-исключений = чини иерархию; Editor-код только в `Scripts/Editor/`.
- Тюнящиеся — объектом в сцену (инспектор), бутстрап только для ненастраиваемого (`CLAUDE.md:28`).
- Префабы только генераторами `Chimera → Создать префаб X`, `SpeciesBootstrap` → `Создать дефолтные виды` (`CLAUDE.md:29`).

Всегда давай пользователю пошаговую инструкцию «создай X, повесь Y, выставь Z — зачем».
