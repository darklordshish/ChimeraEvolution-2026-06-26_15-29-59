# План: BSP-план лаборатории

**Спека:** [`2026-10-29-lab-bsp-plan.md`](../specs/2026-10-29-lab-bsp-plan.md).
**Ветка:** `les/runtime-builder`. **A/C — `labPlanVersion=1` — не трогаются.**

## P3b.1 — данные BSP

- [x] `LabPlanBsp.Create(seed, version)` — нарезка 46×36, функции, двери.
- [x] `LabPlanRules` для `version>=2`: диапазоны размеров, дверь на стене, глубина, `MD(hall)`.
- [x] `LabPlanGenerator.Generate` для `version>=2` вызывает BSP; fallback — fixed с причиной.
- [x] Тесты: детерминизм, отсутствие пересечений, двери, глубина, fallback.
- [x] `labPlanVersion=2` в тестовом профиле; A/C остаются на 1.

## P3b.2 — геометрия из плана

- [ ] `DomeLabComplex` для `version>=2` строит стены/проёмы из `plan.rooms`/`plan.doors`.
- [ ] `DomeLabDamage` и мебель читают комнаты плана, а не жёсткие ±16/17.5.
- [ ] `LabDoor`/`LabAtrium` — из плана.
- [ ] Кадры и NavMesh: все функции достижимы.

## Проверки

- EditMode: `LabPlanBspTests`, `LabPlanRulesTests`.
- PlayMode: `ForestLabComplexTests` на версии 1 и отдельный профиль версии 2.
- `spec_status.py`; A/C baseline зелёный.
