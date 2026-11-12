---
name: morphology-by-layers
description: "⛔ ОТМЕНЕНО 10.09.2026. Метод «кости → мышцы → покровы» и клетка M×N больше не действуют. Актуальный источник формы — цельный меш вида + куски: читай CONSTRUCTOR_GUIDE и Docs/models/SPEC-priyomka-formy.md."
---

# Morphology by Layers — ⛔ ОТМЕНЕНО 10.09.2026

Метод «кости → мышцы → покровы» и клетка M×N отменены. Кости остались только ригом; форма вида
с 13.10 — **цельный меш по листу**, химера — **куски тех же мешей по швам**.

## Что действует

- `Docs/CONSTRUCTOR_GUIDE.md`, раздел «Перевести вид на меш и нарезать на куски».
- `Docs/superpowers/specs/2026-10-09-chistyj-vid-meshem.md` — решение 09–13.10.
- `Docs/models/SPEC-priyomka-formy.md` и `SPEC-model-contract.md` — контракт и приёмка.
- `Docs/models/handoff/` и `Assets/_Chimera/Models/<вид>_mesh.fbx` — поставка и меши.
- В opencode тот же смысл несёт скилл `body-form` (`.opencode/skills/body-form`).

## Что пережило отмену

- Эталон снимается с референса-картинки, а не собирается из промеров.
- Оценку референсу ставить, только открыв картинку.
- «Где и насколько» вместо «плохо».

Старые `Tools/Blender/README.md`/`STATUS.md` и `SPEC-konstruktor-formy.md` — исторический слой,
не действующий пайплайн.
