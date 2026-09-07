---
name: blender-chimera
description: Use when modeling, sculpting, or generating CHIMERA creatures in Blender — low-poly faceted, armature, cage M×N, FBX per slot. Covers Tools/Blender pipeline, species data, and Unity hand-off.
---

# Blender CHIMERA — low-poly модельная линия

Зона: `Tools/Blender/` → `Assets/_Chimera/Models/`. В `Scripts/` и `Anatomy/` не пишет.

## Пайплайн (Tools/Blender/README.md)
```
cd Tools/Blender
python check.py wolf                                  # остеометрия без Blender
blender -b -P make.py -- --species wolf --layer 1 --views profile,front,head
blender -b -P make.py -- --species wolf --layer 1 --fbx --blend
python compare.py out/wolf_L1_profile.png             # оверлей на фото
blender -b -P make.py -- --species wolf --layer 3 --close 0.034 --fur 0.010 --fbx
python skullprofile.py                                # сечения черепа с ортографий
```
`--layer` = `SpeciesSO.buildLayers` (1 кости, 2+мышцы, 3+признаки), `--slots` красит слоты.

## Из чего состоит
| файл | роль |
|---|---|
| `chimera/skel.py` | кость в конвенции Unity (Euler ZXY, +Y), без `bpy` — считается без Blender |
| `chimera/mesh.py` | лофт кости/мышцы, оболочка черепа по 49 сечениям |
| `chimera/build.py` | арматура (81 кость волка), сборка по слотам, `use_self` Boolean, `Bake Axis -90,0,0`, скиннинг, FBX 9 мешей |
| `chimera/views.py` | ортокамеры профиль/анфас/верх |
| `chimera/cage.py` + `cagemesh.py` + `volume.py` | клетка M×N: луч из оси до болвана (`STEP 0.002`), `PAD` послотно, `frames()` без крутки, `BURY 1.6` |
| `species/wolf.py` | **данные волка** — точки суставов → `from_points` → кости. Тут правится форма |
| `species/wolf_skull_data.py` | **генерируется** `skullprofile.py`, руками не править |
| `check.py` / `compare.py` | детектор + оверлей на фото |

## Три закона (README)
1. **Суставы, не углы** — `from_points` считает длины/повороты, углы на фото не видно.
2. **Сустав общий** — начало дочери = точка на родителе, иначе «висячая деталь».
3. **Эталон с натуры** — контур/сечения с фото/ортографий попиксельно, литература — проверка остеометрией (доля холки).

## Low-poly фасетка (art-direction)
- Бюджет `SPEC-kletka:108` 324 квада ~830 трис/зверь, 25×≈20.7k (было 310/800/20k; ADR-1 хребет 8×10). Фасетка — стиль, не упрощение (`SPEC-kletka:164`). Излом на шве — ок.
- Смус/сабдив — **запрещено**. Shade flat, не smooth. Экономия → свет/туман/VFX (VOIN/Valheim).
- Пасть — отдельная клетка без общего кольца (`SPEC-kletka:105`), зубы `FEATURE` деталями. Иглы/шерсть — оператор над поверхностью торса (`SPEC-kletka:294`), не слот.
- Клетка `M×N` фикс на слот (хребет 8×10, голова 6×8…), `SURFACE_BONE` пояс→хребет, `PAD` (клетка 30мм, голова 5мм, ухо 2мм).
- Оси Blender→Unity `Bake Axis -90,0,0`, `bake_space_transform=False`, хиральность `_L/_R` инверт.

## Что стоило итераций (не повторять)
`AXIS·R` не `AXIS·R·AXISᵀ`, целиться из начала кости (лопатка +55мм), `use_self` для самопересечений, пластина черепа без нижней челюсти, дуга отдельно от черепа (височная яма), замыкание `катящийся шар` `r=34мм` (щель 45мм vs просвет 200мм), послотный `PAD`.

## Состояние
Волк L1-L3 чист, 112 костей, 25 мышц, 9 мешей `wolf_L3.fbx` + арматура, `out/wolf_L3_vs_photo.png` сходится кроме живот 3.5см/лоб 3см/поза хвоста. 4 вида не начаты — пайплайн готов. Глаза/нос/когти нет.

Триггерит `designer-artdirector` на любую модель, `morphologist` на швы.
