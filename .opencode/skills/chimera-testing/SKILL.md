---
name: chimera-testing
description: Use when writing or running tests for CHIMERA — EditMode/PlayMode, audit.py, BodyMap detector, cage blending, hit effects. Guards against regressions in morph, combat, and evolution.
---

# Chimera Testing

Тестов в проекте почти нет — единственный детектор `Anatomy/tools/audit.py` + `BodyRules` + `BodyMap` (`chimera-validation`). Тестер закрывает дыру.

## Где что
- **Unity Test Framework** `com.unity.test-framework@1.4` уже в `Packages/manifest.json` (EditMode + PlayMode, `NUnit`).
- Тесты живут в `Assets/_Chimera/Tests/` (создать): `EditMode/` (чистые функции, без сцены) + `PlayMode/` (сцена, `CC`, `Instantiate`), `asmdef` с `testables`.
- Python: `Anatomy/tools/audit.py --selftest`, `check.py wolf`, `Tools/Blender/check.py`.

## Что крыть первым (приоритет сквада)
| приоритет | что | тип | инвариант |
|---|---|---|---|
| P0 | `CageTable.SameTopology` + `Blend Σ=1` + `NoMetersInDonor` | EditMode | И4-И6, клетка смешивается без метров |
| P0 | `BodyRules` длинная ось, запас ≥5% | EditMode | `CLAUDE.md:49` |
| P0 | `Hit.Apply` + `MeleeBlow.Deliver` | EditMode | 9 EffectKind, без GC |
| P1 | `CreatureBody.Identity` веса выпуклы, `MostKin` | EditMode | `Identity.cs:39` Σ=1 |
| P1 | `PsycheDispatch` по доминанте, `Metamorph` смена | PlayMode | три оси `CLAUDE.md:64` |
| P1 | `MorphBuilder` — тождественность `вес=1` + `I8` шов | PlayMode | `SPEC-kletka:83` |
| P2 | `Telegraph.Rebuild` + `IsHeadName`/`IsOwnFace` контракт | PlayMode | `CLAUDE.md:50` |
| P2 | `Evolution` петля 25 киллов → графт | PlayMode | `EvolutionConfig` |

## Как писать
- **EditMode — без Unity API тяжёлого:** `BodySlots`, `CageTable`, `Hit`, `Identity` — чистые `struct`, `NUnit` + `Assert.That(..., Is.EqualTo(...).Within(1e-4))`. Никаких `FindObject`.
- **PlayMode — сцена-минималка:** `SampleScene` не грузить, создавай `new GameObject("Probe", typeof(CharacterController), typeof(CreatureBody))` + `MorphBuilder.Build` → `BodyProbe.Measure` → `Assert.Gap < 0.08*detail`.
- **Python — `C:\ProgramData\anaconda3\python.exe`:** `python Anatomy/tools/audit.py --selftest` + `python Tools/Blender/check.py wolf` в CI, не руками.
- **Детектор — не тест, но ворота:** `Chimera → Выгрузить карту тел` 0 щелей — зелёный билд, иначе красный, как `audit.py`.

## Запреты
- Не мокай `SpeciesSO` руками — бери `SpeciesBootstrap` `Data/*.asset`.
- Не пиши тесты ради покрытия — пиши ради инвариантов И1-И8 и гочей `CLAUDE.md:37-55`.
- Не коммить без `EditMode` прогнаных локально (`Window → General → Test Runner`).

Триггерит `tester`, но зовут все на кросс-чек.
