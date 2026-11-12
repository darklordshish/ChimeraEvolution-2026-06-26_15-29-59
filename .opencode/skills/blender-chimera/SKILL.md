---
name: blender-chimera
description: HISTORICAL since 13.10.2026 — the cage M×N Blender pipeline on main is canceled. Current form deliveries are whole meshes, parts, keys and poses under Docs/models/handoff.
---

# Blender CHIMERA — исторический пайплайн (не действующий)

`Tools/Blender/README.md` и `STATUS.md` на `main` описывают **отменённый** метод: клетку M×N,
слои кости→мышцы→покровы, `cagerun.py`, `make.py --cage`, бюджет 324 квада/800 трис.
Не использовать это как текущий пайплайн и не переносить его числа в код.

## Что действует с 13.10

- **Форма вида — цельный меш по листу** (`Assets/_Chimera/Models/<вид>_mesh.fbx`), химера — из кусков
  тех же мешей. Правило — `CONSTRUCTOR_GUIDE.md`, «Перевести вид на меш и нарезать на куски».
- **Поставка** — `Docs/models/handoff/`: `<вид>-graph.json`, `parts/`, `cherty/`, `klyuchi/`,
  `organy/<вид>.json`, раскладки. Числа в код не переписываются; читает `SpeciesHandoff`.
- **Контракт** — `Docs/models/SPEC-model-contract.md`; приёмка — `Docs/models/SPEC-priyomka-formy.md`;
  текущие ТЗ по видам — `Docs/models/species/`.
- **Модельная линия работает в своей папке/ветке** (`ChimeraEvolution-modeli`); на `main` старые
  Blender-скрипты — исторический слой. Не править их как источник формы.

## Что пережило отмену

- Сустав общий по построению, эталон снимается с картинки, а не из таблиц.
- Сначала кандидаты картинкой геймдизайнеру, потом риг: числа IoU/критика привлекательность не ловят.
- Low-poly фасетка, `shade flat`; смус/сабдив запрещены.

## Куда смотреть

`body-form` (действующий источник), `Docs/models/SPEC-priyomka-formy.md`,
`Docs/models/SPEC-model-contract.md` §7.4, `Docs/superpowers/specs/2026-10-09-chistyj-vid-meshem.md`.
