---
name: chimera-anatomy
description: Use when working with CHIMERA references, passports, or Python tools under Anatomy — not as the source of current body form. Current form lives in meshes and handoff.
---

# Chimera Anatomy — референсы и паспорта

`Anatomy/` — мастерская **референсов и эталонов**, а не источник формы тела с 13.10.
Форма вида сейчас — цельный меш + куски (`body-form`), числа берутся из `Docs/models/handoff`,
а не из анатомических костей.

## Где что

- `Anatomy/README.md` — правила мастерской.
- `Anatomy/species/<вид>/` — `ref/` (фото, скелет, мышцы), `etalon/`, `out/`, `REFS.md`.
- `Anatomy/tools/` — Python-инструменты для референсов/промеров.
- Паспорта — в git; тяжёлые фото/3D — локально, вне git (`Chimera_game/_refs-local/`).

## Что чем является

- **Референс/эталон** — картинка и её паспорт: что видно, чему верить. Оценку ставить, только открыв картинку.
- **Кости** — риг для меша, не источник силуэта. Старые cage-обмеры (`cagerun.py`, `make.py --cage`,
  `SPEC-kletka`) отменены; не выдавать их числа за текущую форму.
- **Текущая форма** — `Docs/models/handoff/` и `Assets/_Chimera/Models/<вид>_mesh.fbx`; правки — в ветке
  модельной линии, не в `SpeciesBootstrap`.

## Python

Только Anaconda: `C:\ProgramData\Anaconda3\python.exe`. Bare `python` — сломанная заглушка.
Проверки референсов — по `Anatomy/README.md`; старые `audit.py`/`render.py` не заменяют
`chimera-map`/`chimera-silhouette`/`Кадры`.

## Правило

Не переписывать числа формы в `SpeciesBootstrap`. После правок поставки — `SpeciesHandoff` при
пересоздании видов; после правок данных видов — `Chimera → Создать дефолтные виды`.
