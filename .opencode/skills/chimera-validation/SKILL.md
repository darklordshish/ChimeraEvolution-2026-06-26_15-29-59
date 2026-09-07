---
name: chimera-validation
description: Use when verifying CHIMERA bodies, seams, or any morphology change. Builds with real MorphBuilder and measures renderer bounds, not socket boxes.
---

# Chimera Validation — детектор, а не описание

Карта тел не пересчитывает, а строит тело настоящим билдером и меряет (`CLAUDE.md:68`, `УКАЗАТЕЛЬ.md:2`).

## Команды (требуй от пользователя)
- `Chimera → Выгрузить карту тел` → `Docs/Диаграммы/КАРТА_ТЕЛ.md` (главный детектор: габариты, стыки, щели/нахлёсты)
- `Chimera → Выгрузить схемы тел` → `Docs/Диаграммы/README.md` + `Волк/Ёж/Змея/Лось/Человек.md` (дерево «кто к кому», кости волка)

Числа брать оттуда, не из текстов.

## Как меряет
- Стык по БЛИЖАЙШЕЙ ПАРЕ ДЕТАЛЕЙ — коробка места прощает щель (у рогов — метровая).
- Порог от детали шва, не фиксированный. Щель — толщиной, нахлёст — глубиной (разные масштабы, `BodyRules.cs`).
- Пороги из распределения фактов, валидатор не кричит на намеренное (`BodyRules`).

## Чеклист перед плейтестом (владелец morphologist, проверяет orchestrator)
- [ ] Реальные границы рендереров, не `baseSize`
- [ ] Та же СК где расставлял (`CLAUDE.md:53`)
- [ ] Ось `axisOf` не переключилась после правки `baseSize`
- [ ] `Telegraph.RebuildRenderers()` + `PlayerController.ReapplyFirstPerson()` после сборки (имена-контракт `CLAUDE.md:50`)

Не отдавать на плейтест пока краснота не чиста.
