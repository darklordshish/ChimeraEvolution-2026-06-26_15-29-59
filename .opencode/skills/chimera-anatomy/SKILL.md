---
name: chimera-anatomy
description: Use when working with Anatomy workshop, Blender cage, species refs, or any Python tools in CHIMERA. Handles Anaconda path and audit pipeline.
---

# Chimera Anatomy Workshop

## Где что
`Anatomy/README.md` — мастерская. `Anatomy/species/<вид>/{<вид>.py, ref/{photo,skeleton,muscle}, etalon/, out/}`, `Anatomy/tools/` 9 инструментов.

## Инструменты (Python — только Anaconda)
Bare `python` — сломанный stub. Рабочий: `C:\ProgramData\anaconda3\python.exe` (см. `using-anaconda-python`).

| tool | что |
|---|---|
| `skel.py` | координаты кости → Unity (Euler ZXY, ось +Y) |
| `speciesdata.py` | данные вида + эмуляция `MorphBuilder.Place` |
| `render.py` | рендер слоя как `BoneMesher` (`--part`, `--layer`) |
| `grid.py` | сетка для снятия эталона |
| `audit.py` | детектор щелей как в Unity |
| `commons.py`, `sheet.py`, `reroot.py` | общее, листы, пересадка корня |

## Blender-клетка
`Tools/Blender/README.md`, `Docs/models/SPEC-kletka-tela.md` — слот = таблица `M×N`, химера = среднее. Обмерщик: луч из оси до болвана. Порядок §11 свят.

## Референсы
Лежат локально, в git — паспорта (`Anatomy/README.md`). По видам — `Anatomy/species/<вид>/REFS.md` (инвентарь, отвергнутое, пробелы). Высокий приоритет закрыт у 4/5, остался канон человека в числах (`СТАТУС.md:94-105`).

## Правило
Правь `Anatomy/species/<вид>/*.py`, не `SpeciesBootstrap` числа. После — `Chimera → Создать дефолтные виды` + `Выгрузить карту тел` (`chimera-validation`).
