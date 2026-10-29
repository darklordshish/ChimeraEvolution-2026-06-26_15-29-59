# Запросы: полные ряды ступеней · 29.10.2026

## Корректировки по сверке координатора · 30.10.2026

Встроенный image_gen, по одному вызову на каждый r2. Последующих генераций нет.
Все четыре входных изображения открыты перед вызовами. Генераторные выходы 1536×1024.
После генерации собраны разрешённые колонки с неизменными исходными областями;
это сохранение ст.0/ст.3, а не ещё один генеративный проход. Подробности и дефекты — README.

### chelovek-los-r2.png

Вход 1 (цель редактирования): `chelovek-los.png` в этой папке.
Вход 2 (анатомический референс): `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png`.

```text
Use case: precise-object-edit. Edit IMAGE 1 CHIMERA moose progression sheet. IMAGE 2 is authoritative anatomical body r3 reference, NOT a replacement layout. Keep the entire first and fourth columns ST.0 and ST.3 absolutely unchanged, same pixels if possible. Keep layout, labels, background, lighting, scale, all 3 camera rows FRONT / LEFT PROFILE / THREE QUARTER. Change ONLY middle two columns stages1 and2: replace human bodybuilder anatomy with the animal musculature in image2. Eliminate paired pectoral plates, sixpack abs, spherical deltoids and bulky human thighs/calves. Narrow deep elongated rib cage, continuous smooth elongated animal muscle masses rendered in broad low-poly facets, withers hump ABOVE scapulae, neck emerging forward from hump, light pelvis, long dry lean legs. Stage1 moderate hump and forward neck with long lean legs. Stage2 visibly stronger and higher scapular hump, longer forward neck projection, deeper narrower chest and even drier longer-looking legs, moderate progression like reference r3 not exaggerated monster. This distinction MUST be visible in naked body silhouette in ALL THREE VIEWS independently of antler size. Front and threequarter especially must be animal torso, no fitness human chest or abs. Preserve moose heads muzzle ears antlers, hand hoof tips and cloven feet on both stages, dark shorts, bipedal stance. Image2 lower middle columns show profile WITH organs; use that body silhouette. No changes to endpoint columns, no extra figures. Single corrective pass.
```

### chelovek-zmeya-r2.png

Вход 1 (цель редактирования): `chelovek-zmeya.png` в этой папке.
Вход 2 (тело и внешний облик органов): `../ChimeraEvolution/Референсы/витрина/человек-змея/тело.png`.

```text
Use case: precise-object-edit. IMAGE1 is target CHIMERA human-snake progression sheet. IMAGE2 is supporting snake r3 anatomy and external organ reference. Edit ONLY stage1, second column, all THREE views (front, left profile, threequarter). Add missing external organs: replace human head with an unmistakable elongated SNAKE HEAD and snake mouth/jaws, like bottom row 'С ОРГАНАМИ' in IMAGE2 (especially variant A). Snake eyes, elongated reptile muzzle, snake jaw silhouette, no human ears or human nose. Add visible subtle gray overlapping SCALES of Skin over head, neck, torso, arms and legs, small coherent scales following anatomy, not armor plates. Keep healthy slender long bipedal body of image1 or slightly closer to reference A/C: smooth elongated muscle masses, long narrow torso and legs, soft S profile neck forward chest back pelvis forward; narrow shoulders without shoulder shelf. Shoulder-joint width / hip-joint width target <=1.10; neck-side sweep toward shoulder target 30–35 degrees from horizontal when readable; prioritize adding organs without distorting body. Keep human hands feet and dark shorts. Absolutely NO tail, rattle, extra limbs or internal organs on stage1. Do not add standalone organs. Preserve first stage0 column, third unreachable stage2 column and fourth pure-snake stage3 column EXACTLY UNCHANGED, including every figure and all text and grid. Same original 1536x1024 layout composition scale white background grayscale lighting. Only second column may change. Single corrective pass.
```


Инструмент: встроенный image_gen; по одной первичной генерации и одной корректировке на лист. Источники прочитаны из соседней рабочей папки ChimeraEvolution, без изменения витрины. Все входные изображения открыты перед генерацией.

## oboroten

Первичные входы (порядок изображений):

- `../ChimeraEvolution/Референсы/витрина/оборотень/тело.png`
- `../ChimeraEvolution/Референсы/витрина/оборотень/тело-ст2.png`
- `../ChimeraEvolution/Референсы/витрина/оборотень/ступени.png`

Первичный запрос:

```text
Use case: stylized-concept. CHIMERA production concept sheet, grayscale faceted low-poly, white background, soft identical studio lighting, thin ground lines, 4 columns and 3 rows: FRONT, LEFT PROFILE facing left, THREE-QUARTER facing left. Same scale consistently between views, no per-cell zoom. Full bodies unobscured, no clipping. Column headings verbatim: «СТ.0 · ЧЕЛОВЕК», «СТ.1 · 65–85%», «СТ.2 · ОТ 85%», «СТ.3 · ЧИСТЫЙ ВИД». Stage0 bald adult male in dark shorts. Stages1-2 bipedal human chassis with animal mass distribution, dark shorts; stage3 animal on its own chassis. NEW BODY references are authoritative for stages1-2: copy their torso outline, pose, limb lengths, lean, musculature, not the obsolete middle columns of OLD SHEET. OLD SHEET is only composition/style/organ and endpoints reference. Add organs over new bodies without concealing the torso silhouette. This is a revised full sheet, not a collage of naked-body rows. Image1 authoritative STAGE1 body in front/profile/three-quarter. Image2 authoritative STAGE2 body profile/front/back: lean torso 45 degrees FROM VERTICAL, head below withers and ahead of chest, flat wedge scapula toward elbow, deep keeled chest without human pectoral plates, tucked belly, lean animal muscles, free arms hands ABOVE knees, digitigrade feet, pelvis above feet. Front shoulders/pelvis about1.4. Image3 OLD SHEET for endpoints/organ appearance only. Stage1 must already have image1 wolf body, 15–25 degree lean, wolf muzzle and ears, clawed wolf hands and feet; add tail; no large mane. Stage2 preserve image2 silhouette, add tail and modest angular mane that follows withers without hiding muscles. Stage3 quadrupedal wolf. Clearly distinct stage1 versus lower more forward-leaning stage2 in ALL views.
```

Корректировка: изображение 1 — первичный результат; далее тела в прежнем порядке, без старого ряда.

```text
undefined
```

## chelovek-los

Первичные входы (порядок изображений):

- `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png`
- `../ChimeraEvolution/Референсы/витрина/человек-лось/ступени.png`

Первичный запрос:

```text
Use case: stylized-concept. CHIMERA production concept sheet, grayscale faceted low-poly, white background, soft identical studio lighting, thin ground lines, 4 columns and 3 rows: FRONT, LEFT PROFILE facing left, THREE-QUARTER facing left. Same scale consistently between views, no per-cell zoom. Full bodies unobscured, no clipping. Column headings verbatim: «СТ.0 · ЧЕЛОВЕК», «СТ.1 · 65–85%», «СТ.2 · ОТ 85%», «СТ.3 · ЧИСТЫЙ ВИД». Stage0 bald adult male in dark shorts. Stages1-2 bipedal human chassis with animal mass distribution, dark shorts; stage3 animal on its own chassis. NEW BODY references are authoritative for stages1-2: copy their torso outline, pose, limb lengths, lean, musculature, not the obsolete middle columns of OLD SHEET. OLD SHEET is only composition/style/organ and endpoints reference. Add organs over new bodies without concealing the torso silhouette. This is a revised full sheet, not a collage of naked-body rows. Image1 authoritative new bodies: columns2 and3 stages1 and2, first two rows naked anatomy, bottom row profile with organs. Image2 OLD SHEET layout and endpoints only. Both stages must have image1 elongated narrow deep chest, muscular withers hump (not human slouch), neck projecting forward from hump, light pelvis, long dry legs, whole animal muscle masses no bodybuilder abs. Stage2 moderately stronger than stage1, not extreme. Apply moose mouth/muzzle, large ears, palmate antlers, cloven hooves on feet and hoof tips on hands to BOTH stage1 and2, as image1 bottom row. Preserve image1 profile silhouette closely. Stage3 quadrupedal moose.
```

Корректировка: изображение 1 — первичный результат; далее тела в прежнем порядке, без старого ряда.

```text
undefined
```

## chelovek-ezh

Первичные входы (порядок изображений):

- `../ChimeraEvolution/Референсы/витрина/человек-ёж/тело.png`
- `../ChimeraEvolution/Референсы/витрина/человек-ёж/ступени.png`

Первичный запрос:

```text
Use case: stylized-concept. CHIMERA production concept sheet, grayscale faceted low-poly, white background, soft identical studio lighting, thin ground lines, 4 columns and 3 rows: FRONT, LEFT PROFILE facing left, THREE-QUARTER facing left. Same scale consistently between views, no per-cell zoom. Full bodies unobscured, no clipping. Column headings verbatim: «СТ.0 · ЧЕЛОВЕК», «СТ.1 · 65–85%», «СТ.2 · ОТ 85%», «СТ.3 · ЧИСТЫЙ ВИД». Stage0 bald adult male in dark shorts. Stages1-2 bipedal human chassis with animal mass distribution, dark shorts; stage3 animal on its own chassis. NEW BODY references are authoritative for stages1-2: copy their torso outline, pose, limb lengths, lean, musculature, not the obsolete middle columns of OLD SHEET. OLD SHEET is only composition/style/organ and endpoints reference. Add organs over new bodies without concealing the torso silhouette. This is a revised full sheet, not a collage of naked-body rows. Image1 authoritative bodies: col2 NEW STAGE1 compact digging animal, short lower body, deep broad chest, low forward head, strong rounded back. Col3 NEW STAGE2 full DOME of back and shoulders in one arc, short thick neck, low squat posture. Whole dense smooth muscle masses NOT human bodybuilder pecs or abs. Copy silhouette of naked rows and add organs like bottom row. Image2 OLD SHEET only endpoints and organ appearance. Stage1 already thick compact beast, stage2 even lower dome; BOTH have pointed hedgehog muzzle, small ears, skin spines following back, clearly visible three hollow organic quill-launcher tubes on upper back, claws hands/feet. Stage3 pure quadrupedal Hedgehulk with quills/launchers. Quills should not replace the body dome, preserve volume beneath.
```

Корректировка: изображение 1 — первичный результат; далее тела в прежнем порядке, без старого ряда.

```text
undefined
```

## chelovek-zmeya

Первичные входы (порядок изображений):

- `../ChimeraEvolution/Референсы/витрина/человек-змея/тело.png`
- `../ChimeraEvolution/Референсы/витрина/человек-змея/ступени.png`

Первичный запрос:

```text
Use case: stylized-concept. CHIMERA production concept sheet, grayscale faceted low-poly, white background, soft identical studio lighting, thin ground lines, 4 columns and 3 rows: FRONT, LEFT PROFILE facing left, THREE-QUARTER facing left. Same scale consistently between views, no per-cell zoom. Full bodies unobscured, no clipping. Column headings verbatim: «СТ.0 · ЧЕЛОВЕК», «СТ.1 · 65–85%», «СТ.2 · ОТ 85%», «СТ.3 · ЧИСТЫЙ ВИД». Stage0 bald adult male in dark shorts. Stages1-2 bipedal human chassis with animal mass distribution, dark shorts; stage3 animal on its own chassis. NEW BODY references are authoritative for stages1-2: copy their torso outline, pose, limb lengths, lean, musculature, not the obsolete middle columns of OLD SHEET. OLD SHEET is only composition/style/organ and endpoints reference. Add organs over new bodies without concealing the torso silhouette. This is a revised full sheet, not a collage of naked-body rows. Image1 authoritative stage1 body: MASS and healthy slenderness from variant A col2, shoulder slope from variant C col4 with explicit correction: shoulder joint width / pelvis width <=1.10, ear to shoulder joint slopes 30–35 degrees from horizontal with no shoulder shelf. Long thin torso and legs, healthy long smooth muscles not emaciated, no pec plates/abs. Soft S profile neck forward, chest back, pelvis forward. Add snake mouth/head and subtle gray scales over this elongated biped. Human arms and legs remain. ABSOLUTELY NO TAIL OR RATTLE ON STAGE1. Image2 old sheet ONLY endpoints/layout; discard old stage1 body and all middle-column tails. Column3 STAGE2 is EMPTY in ALL3 rows, mark «НЕДОСТИЖИМА НА ЧЕЛОВЕКЕ» and «предел доли 0.69». Column4 pure rattlesnake on own chassis: simple uncoiled elongated curve, snake head and rattle, no limbs. Keep human and hybrid on same scale in every view, pure snake low to ground.
```

Корректировка: изображение 1 — первичный результат; далее тела в прежнем порядке, без старого ряда.

```text
undefined
```


