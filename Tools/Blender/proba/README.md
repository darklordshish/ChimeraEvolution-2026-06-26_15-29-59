# Тракт формы-ключа на шаблоне шасси (модельная линия)

Шаблон шасси (К2 человека) несёт формы-ключи видов, черты ступеней `@<ключ>`, органы на шаблоне `<слот>@<вид>` и ключи областей.
Правило конструктора — `Docs/CONSTRUCTOR_GUIDE.md`, «Перевести вид на меш…»; разбор тракта и план А–Е —
`Docs/models/FEEDBACK-2026-10-27-modelnyj-razbor-trakta.md`. Здесь — как собрать и проверить.

## Сборка одним заходом

    bash Tools/Blender/proba/sborka_k2.sh <шасси К2.blend> <рабочая папка> <выход.fbx>

Порядок шагов и почему он такой:

1. **подгонка в позе** (`podgonka_v_poze.py`) → ключ вида в покое шасси обратным скиннингом, поза ступени, суставы цели;
2. **куски-черты и органы** (`kusok_celi.py`, `slit_kuski.py`, `dobavit_kusok.py`, `organ_objekt.py`);
3. **форма** (`ogranka.py`, `formovka.py`, в том числе `--raspravit` — развёрнутые грани ключа);
4. **ВЕСА ФИНАЛЬНЫЕ С ПЕРЕВЯЗКОЙ** (`vesa_final.py`) — см. правило ниже;
5. **ключи областей** (`oblasti_k2.py`) — от финального ключа и финальных весов;
6. **выгрузка** (`eksport_k2.py`) — веса только проверяет; не финальные — выгрузка падает.

Любой шаг упал — сборка стоит (журнал шага — `<рабочая папка>/shag.log`).

## Правило: веса финальные до снятия ключа (27.10, пункт А)

Ключ снят обратным скиннингом: `ключ = M(веса, поза)⁻¹ · цель в позе`. **Сменил веса — ключ уже не даёт цель в позе.**
Опыт дизайн-линии 24.10 (`codex/morph-weights-experiment`): сглаживание весов после ключа уводило его в позе до 87 мм;
ограничение до 4 костей в выгрузке сдвигало ключ «Лось» на руках до 31 мм.

- Всё, что меняет веса (сглаживание полосы, до 4 костей, общие веса швов), — один шаг `vesa_final.py`, и он же тут же
  **перевязывает**: снимает поверхность в позе при старых весах и пересчитывает координаты при новых — ключ вида в позе
  своей ступени, базу черты `@<ключ>` в той же позе, ключ `рост` органа в позе ступени 2. Обращение с обусловленностью > 100
  запрещено; контроль — та же поверхность в позе, порог 0.1 мм.
- Позы для перевязки — **из поставки** (`Docs/models/handoff/klyuchi/*-poza*.json`): их показывает игра, поправки
  `poza_po_listu.py` живут там.
- После шага веса не трогает никто: ключи областей — после него, выгрузка проверяет (не больше 4 костей, сумма 1, швы равны).
- `vesa_gladit.py` отдельно не запускать — его сглаживание вошло в `vesa_final.py --gladit`.

## Новая цель по листу (целиком или только торс)

Ключ снимается с картинки-референса, а не лепится формовкой по глазу (ГеймБосс, 27.10). Цель — 3D Hunyuan3D-2mv по листу дизайн-линии:

    python Tools/Blender/proba/cel_vyrezki.py <лист.png> <папка вырезок> --imena front,left,34      # RGBA по ракурсам, right = зеркало left
    GRADIO_TEMP_DIR=<черновик> python Tools/Blender/proba/cel_gen.py <папка вырезок> <_refs-local/эксперименты/…> A front,left,right 1234
    blender -b --factory-startup -P Tools/Blender/proba/cel_norm.py -- cel_mv_A.glb cel_A_norm.obj 1.86 [префикс кадров с сеткой]
    blender -b --factory-startup -P Tools/Blender/proba/vkleit_tors.py -- старая.obj cel_A_norm.obj orientiry-celi.json вклейка.obj   # только торс
    CEL_TELO=вклейка.obj bash Tools/Blender/proba/sborka_k2.sh …                                     # дальше стенд

Целиком — новые ориентиры (`orientiry-celi.json`, по кадрам сетки `cel_norm`); только торс — ориентиры старой цели: вклейка
заменяет переднюю половину торса между `таз:head` и `шея:head` (|x| < 0.24) и сдвигает новую цель к старой по торсу.

## Стенд: проверка без Unity (пункт Б)

    blender -b --factory-startup -P Tools/Blender/proba/stend_k2.py -- chelovek_mesh.fbx --pokoj Docs/models/handoff/klyuchi/chelovek-pokoj.json \
        --kluch Волк --ves 1 --poza Docs/models/handoff/klyuchi/chelovek-volk-poza.json --stupen Docs/models/handoff/klyuchi/chelovek-volk-st2.json \
        --maski Референсы/работа/модельная/2026-10-27-maski-oborotnya/st2-front.png,Референсы/работа/модельная/2026-10-27-maski-oborotnya/st2-profile.png \
        --kadr <префикс> --otchet <отчёт.json>

Выгруженный FBX, поза по правилу `KeyPose.Apply`, `use_connect` снят, отсечение задних граней. Детекторы: развороты ключа,
складки и сжатия в позе (по шасси), швы, IoU силуэта с маской листа. ~15 с на ступень. Unity и критик — только на вехе.

## Окружение (поставить на чистой машине)

- Blender 5.2 (`C:/Program Files/Blender Foundation/Blender 5.2/`); Anaconda (numpy, scipy, Pillow) для `Docs/tools/*.py`.
- `pip install libigl` (2.6.3) — в Anaconda.
- Аддон **Robust Weight Transfer** (github.com/sentfromspacevr/robust-weight-transfer, исходник — `_refs-local/instrumenty/`) →
  `%APPDATA%/Blender Foundation/Blender/5.2/scripts/addons/robust_weight_transfer`, зависимости (scipy, libigl 2.6.3,
  robust-laplacian) — в `…/addons/robust_weight_transfer/deps/Python313/site-packages`; **`weighttransfer.py` пропатчен под
  API libigl 2.6** (`barycentric_coordinates_tri` → `barycentric_coordinates`, `min_quad_with_fixed` с csc-матрицами и
  обёрткой результата) — переустановка снимает патч. Оператор `object.skin_weight_transfer` (пункт Д — веса поз ступеней).
- Расширение **Pose Shape Keys** 1.1.1 (Blender Studio, extensions.blender.org) —
  `blender -b --command extension install-file -r user_default -e pose-shape-keys-v1.1.1.zip` (sha256 482bdc5c…3d571).
  Ручная перевязка ключей после правки весов в живой сцене (`object.posekey_push_all`); тракт делает это сам `vesa_final.py`.
- Установка расширения из командной строки перезаписывает настройки и выключает аддоны — после неё включить
  Robust Weight Transfer заново (`addon_utils.enable(..., persistent=True)` + `save_userpref`).
