# Запросы: полные ряды ступеней · 29.10.2026

## Лось r4 — корректировка стойки разворота

Единственный вход — `chelovek-los-r4-razvorot1.png`.

```text
Refine this gray faceted clay turnaround of two upright moose creatures, two columns and three rows. Keep the top front views' continuous elongated moose breast, tall pointed withers and long thin hanging forelegs. Keep gray clay, white background, soft lighting, dark shorts, slender hands and cloven hooves. Focus the side and three-quarter views on a tall upright pose: the pelvis sits directly over the planted rear hooves, the deep chest rises high above the pelvis, the withers rise high above the chest. Left ST.1: a tall nearly upright trunk, the long line from pelvis up to withers rises at 70 degrees above the ground, making 20 degrees of forward lean from vertical. The head projects forward from this tall rising moose neck. Right ST.2: the line from pelvis up to withers rises at 55 degrees above the ground, making 35 degrees of forward lean from vertical. Its withers are distinctly higher and longer, its neck projects further, its long keel chest is deeper, its rear legs longer and drier. Both profiles balance as tall walking bipeds with chest stacked high over the hips, arms hanging toward the knees. Moose coat forms one continuous narrow hanging breast with a softly convex front and long sloping withers-to-foreleg contours. Left antlers small and short, right antlers broad and palmate. Top row both look straight at camera, middle row strict left profile, bottom row left three-quarter with head and trunk turned together. Preserve the distinct narrow moose-shaped front silhouettes through the turnaround. Keep all six figures complete at matching scale within each stage and ample white margins.
```

## Лось r4 — разворот после второй пробы

Входы: 1 — `chelovek-los-r4-proba2.png`; 2 — `chelovek-los-r4-ref-st3.png`; 3 — `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png`. Генерируются только две средние колонки. Ст.0 и ст.3 возвращаются пиксельно из r2 при сборке.

```text
Stylized low-poly gray clay creature turnaround sheet on pure white, soft even studio lighting. Two columns and three rows, six complete figures. Left column ST.1; right column ST.2. Top row straight FRONT, middle row strict LEFT PROFILE facing left, bottom row THREE-QUARTER facing left. Image 1 supplies the exact creature identity and the tall tent-shaped withers flowing into a forward neck, continuous elongated moose breast and thin hanging forelegs. Image 2 supplies pure moose clay coat and planes. Image 3 supplies side silhouette balance and forward lean of the two middle upright stages. Render the same creature at each angle, same scale between its views, feet on each row's shared ground line. A moose risen onto its rear legs, carrying its complete heavy moose front high. Narrow tall pointed withers like a steep roof behind the head. Forelegs emerge along its low sides and hang as long tapering sticks ending in slender hands with dark hoof fingertips. Chest is a single deep narrow moose keel, continuous softly convex coat-covered front, long in side view and slim head-on. Its lower end blends into a short tapered belly above dark charcoal short pants. Coarse short coat expressed through large clay facets. Long slim rear thighs and long twig-like shins, cloven hooves. Moose head, long overhanging muzzle, large ears and dewlap. ST.1: small short branching antlers, moderate withers ridge, torso 20 degrees forward from vertical. ST.2: wide palmate antlers, much higher and longer withers ridge, neck projecting further forward, torso 35 degrees forward from vertical, longer deeper chest and longer drier legs. The trunk and leg proportions show the progression in every view. The two front figures both look directly at camera. Three-quarter figures turn both head and trunk together. Figures occupy generous white cells with ample margin around antlers and hooves.
```

## chelovek-los-r3 · редактирование принятого тела · 30.10.2026

Инструмент: встроенный image_gen, редактирование локальных изображений. Один исходный проход и одна корректировка. Все входы открыты перед использованием.

Первый вход — цель редактирования: `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png`; второй — `chelovek-los-r2.png`, только крайние колонки и композиция.

```text
Use case: precise-object-edit / identity-preserve. IMAGE 1 IS THE ACTUAL EDIT TARGET: accepted CHIMERA body sheet. Do not redraw or reinvent these bodies. IMAGE 2 supplies ONLY endpoint columns and final grid convention. Edit image1 in place into a 4-column 3-row progression sheet. Columns ST.0 human, ST.1, ST.2, ST.3 moose; rows FRONT, LEFT PROFILE, THREE QUARTER. For middle columns use EXACT image1 column "НОВАЯ СТ.1" and "СТ.2 · УМЕРЕННО": first two rows WITHOUT ORGANS are LOCKED BODY ANATOMY. Preserve their existing shoulder contour, hump contour and height, neck base and projection, chest outline, waist, arms, shorts, pelvis, knee bend, dry legs and pose. Paint organs OVER these existing bodies, changing only head and feet: moose head with long muzzle mouth jaw, broad nose nostrils, eyes and large moose ears (Mouth and Senses), small immature antlers on stage1 and full palmate antlers stage2; cloven hooves replacing human feet. Keep hands as original body unless existing dark hoof tips. No human head remains. Do NOT enlarge shoulders into round deltoids, do NOT add abs or paired pectoral plates. The accepted body surface and broad facets must remain. Stage2 already has higher hump, deeper chest, more forward neck and leaner lower body than stage1: KEEP those differences independently of antlers. For row3 rotate these SAME bodies to a coherent left-facing three-quarter view, keeping their distinct anatomy; add same organs. Do not use body anatomy from image2 middle columns. Final endpoint columns stage0 and stage3 all three views come unchanged from image2. White background, same soft light, muted warm gray low-poly faceted surface, dark shorts, full figures. Compact antlers fit within cells, nothing clipped. This is a local organ overlay on accepted anatomy, NOT fresh body generation. Prioritize preserving accepted bare body silhouette over making a generic attractive humanoid.
```

Единственная корректировка: вход 1 — выход первого прохода, вход 2 — принятое тело, вход 3 — r2.

```text
undefined
```

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


# Лось r4 · 30.10.2026 · утвердительный образ

Инструмент: встроенный image_gen. Пробы: вход 1 — `chelovek-los-r4-ref-st3.png`, пиксельная вырезка колонки ст.3 из r2 (x=1130..1535, y=38..1023); вход 2 — `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png`. Средние фигуры r2 в генератор не передаются.

## Проба 1 — анфас двух ступеней

```text
Stylized low-poly clay creature sheet, pure white background, gray faceted planes, soft studio lighting. Two full-length figures side by side, straight front view, shared ground line. Left: Stage 1. Right: Stage 2. A moose that has risen onto its hind legs and walks upright: the heavy front of a moose carried high. Image 1 supplies the moose's withers, chest, coat and long tapering limbs. Image 2 supplies the upright stance and forward lean of the two middle profile silhouettes. The shoulders form a tall tent-shaped withers ridge flowing straight into a thick forward neck, like the roof of a steep narrow tent. Long thin forelimbs hang down the sides of a deep narrow keel-shaped chest. The front surface is one continuous long faceted egg tapering toward the waist, covered in coarse short coat suggested by broad facets. Slender hands with dark hoof-like fingertips. Long lean thighs, long dry shins with visible tendons, cloven hooves. Moose head with a long overhanging muzzle, large ears and a dewlap. Dark charcoal shorts. Stage 1: small antlers, moderate high withers, torso tilted about 20 degrees forward, slim upright silhouette. Stage 2: wide palmate antlers, withers higher and longer, neck further forward, torso tilted about 35 degrees, legs longer and drier, chest longer and narrower. The two stages show clearly different trunk silhouettes and limb proportions. Both figures face the camera directly, both feet fully visible. Small labels 'ST.1' and 'ST.2' above the figures.
```

## Проба 2 — усиление образа поднявшегося лося

```text
Stylized low-poly clay creature study, white background, gray faceted clay, soft even lighting. Two upright moose creatures in straight front view, full length side by side. Image 1 is the primary shape reference: its pure moose front assembly lifted upright onto the rear pair of legs. Image 2 supplies the forward lean and long-legged upright balance from the two middle side views. Carry the complete moose withers, moose neck, moose coat and moose breast together as one tall hanging teardrop. The high narrow pointed withers rise behind the head like a steep roof; the sides descend in long straight slopes into pencil-thin hanging forelegs. A single slim elongated pear-shaped breast hangs centrally between those forelegs, its front a continuous gently convex fur-covered surface, its lowest tip reaching the waist. The forelegs emerge low along the sides of this hanging moose breast and continue as long tapering sticks to small slender hands with dark hoof fingertips. The outline reads as a narrow tall moose seen head-on, lifted onto long stilt-like rear legs. Clay facets suggest a continuous short coarse coat over the entire trunk. Rear thighs long and slim, shins long and twig-like, cloven hooves. Dark short pants at the small pelvis. Moose muzzle long and drooping, large ears, hanging dewlap. Left ST.1: small antlers, moderate withers roof, trunk leaning forward 20 degrees. Right ST.2: broad palmate antlers, much taller and longer withers roof, a longer hanging breast and forward neck, lean of 35 degrees, longer thinner legs. Trunk width remains slim at both stages, with stage 2 distinctly taller and more elongated through the trunk. Both look directly toward the camera. Shared ground line and camera scale.
```



# Лось r5 · 30.10.2026 · правка позы

Встроенный image_gen. Одна проба профилей и одна корректировка с разворотом. Только утвердительный английский образ.

## Проба профилей

Вход 1 — витринное `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png` (главная опора позы); вход 2 — `chelovek-los-r4.png` (образ анфаса и мышц). Выход — `chelovek-los-r5-proba1.png`.

```text
Stylized low-poly clay creature study, white background, gray faceted clay, soft even lighting. Two upright moose creatures in strict left profile, full length side by side, facing left. Left ST.1, right ST.2, shared ground line. Image 1 is the PRIMARY POSE reference: the two middle upright bodies and their long-legged balance. Image 2 supplies the moose identity, continuous breast, coat and withers from its top front row. Carry the complete moose withers, moose neck, moose coat and moose breast together as one tall hanging teardrop. The high narrow pointed withers rise behind the head like a steep roof; the sides descend in long straight slopes into pencil-thin hanging forelegs. A single slim elongated pear-shaped breast hangs centrally between those forelegs, its front a continuous gently convex fur-covered surface, its lowest tip reaching the waist. The forelegs emerge low along the sides of this hanging moose breast and continue as long tapering sticks to small slender hands with dark hoof fingertips. Clay facets suggest a continuous short coarse coat over the entire trunk. Rear thighs long and slim, shins long and twig-like, cloven hooves. Dark short pants at the small pelvis. Moose muzzle long and drooping, large ears, hanging dewlap. Both stages stand upright on two long legs, balanced, the pelvis directly above the hooves, the weight carried by the hind legs like a creature that walks on two feet. Stage 1: small antlers, moderate withers roof; the torso leans forward gently, about 15 to 20 degrees from vertical, the hands hang at hip height beside the thighs. Stage 2: broad palmate antlers, much taller and longer withers roof, a longer hanging breast and longer drier legs; the torso leans about 35 degrees from vertical, the neck reaches forward, the hands hang at knee height, well above the ground. The profile reads as an upright walker with a heavy forward front. The line from pelvis to withers is a tall rising line, with the withers high above the pelvis. Both complete figures have generous white margins.
```

## Единственная корректировка

Вход 1 — проба r5; вход 2 — то же витринное тело; вход 3 — r4. Выход — `chelovek-los-r5-korrekciya.png`.

```text
Stylized low-poly gray clay creature turnaround, pure white background, soft even studio lighting. Two columns and two rows, four complete figures: left ST.1, right ST.2; top strict LEFT PROFILE, bottom LEFT THREE-QUARTER. Image 1 is the pose study to refine, image 2 supplies accepted upright balance, image 3 supplies the exact moose front anatomy and coat. Carry the complete moose withers, moose neck, moose coat and moose breast together as one tall hanging teardrop. The high narrow pointed withers rise behind the head like a steep roof; the sides descend in long straight slopes into thin hanging forelimbs. A single slim elongated pear-shaped breast hangs centrally between those forelimbs, its front a continuous gently convex fur-covered surface, its lowest tip reaching the waist. Clay facets suggest a continuous short coarse coat over the entire trunk. Rear thighs long and slim, shins long and twig-like, cloven hooves. Dark short pants at the small pelvis. Moose muzzle long and drooping, large ears, hanging dewlap. Both stages stand upright on two long legs, balanced, the pelvis directly above the hooves, the weight carried by the hind legs like a creature that walks on two feet. ST.1 keeps image1's gently leaning tall trunk, pelvis-to-withers line 18 degrees from vertical. Its slender hanging forelimbs finish HIGHER: the small hands rest alongside the upper thighs, fingertips level with the bottom edge of the shorts, halfway between pelvis and knees. Small antlers, moderate withers roof. ST.2 stands tall on two rear legs with a heavy forward front: its pelvis-to-withers line rises diagonally at 55 degrees above the ground, 35 degrees from vertical. The high withers move forward over the knees while the pelvis balances directly over the planted hooves. A long diagonal rising trunk, the neck reaches forward. The hands hang at knee height, well above the ground. Broad palmate antlers, much higher longer withers roof, longer deeper narrow chest and longer drier legs. The profile reads as an upright walker with a heavy forward front. The three-quarter views rotate these exact corrected bodies and their whole heads together toward the viewer. Same scale across both views for each stage, all hooves on shared ground line per row, generous white margins.
```


# Лось r6 · 30.10.2026 · анатомия и равновесие

Встроенный image_gen. Одна проба двух профилей и одна корректировка с дополнительным 3/4. Полный лист не собран: критерий кистей не пройден.

## Проба

Вход 1 — `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png` (поза); вход 2 — `chelovek-los-r5.png` (анфас, мышцы). Образ r4 взят из текста выше. Выход — `chelovek-los-r6-proba1.png`.

```text
Stylized low-poly gray clay creature study, pure white background, soft even studio lighting. Two full-length upright moose creatures side by side in strict left profile facing left, shared ground line. Left ST.1, right ST.2. Image 1 supplies the pose and forward lean of the middle stages; image 2 supplies moose identity, continuous breast, coat and withers from the front row. Carry the complete moose withers, neck, coat and breast together as one tall hanging teardrop. High narrow pointed withers like a steep roof flow into a thick forward neck. A single slim elongated pear-shaped breast, continuous gently convex fur-covered front, tapers into a short belly above dark charcoal shorts. Long lean thighs, dry shins, visible tendons, cloven hooves. Slender hands with dark hoof fingertips. Long overhanging moose muzzle, large ears, dewlap. Coarse short coat expressed by broad gray facets.
Anatomy and balance: the withers are formed by tall thoracic spinous processes, a high ridge from which the heavy neck ligament holds the head. The rib cage is flattened side to side and deep, a narrow keel. The shoulder girdle is suspended by muscles, with the shoulder blades lying flat and upright against the narrow sides of the ribs, creating narrow sloping shoulders. The upper arm is short and tucked close to the ribs, the elbow points backward, the forearm is long and straight. The pelvis stands upright beneath the spine like the base of a column; the hip joint, knee and hock line up so the hind leg forms a strong vertical support directly under the center of gravity. The heavy antlered head is balanced by the neck rising from the withers, so the whole figure stands stable and calm on two legs, weight settled on the hooves. Stage 1: small antlers, moderate withers, pelvis-to-withers axis about 18 degrees from vertical, hands at mid-thigh. Stage 2: broad palmate antlers, taller longer withers, deeper longer chest, pelvis-to-withers axis about 35 degrees from vertical, longer neck reaching forward, hands just above the knees, pelvis still directly above the hooves. The vertical dropped from each hip joint lands within the planted hoof contact area. Complete figures with generous white margins.
```

## Единственная корректировка

Вход 1 — `chelovek-los-r6-proba1.png`; вход 2 — то же витринное тело. Выход — `chelovek-los-r6-korrekciya.png`. Уточнены диагональная колонна ст.2 и разные уровни кистей.

```text
Stylized low-poly gray clay moose creature study on white, soft even lighting. Two columns and two rows: left ST.1, right ST.2; top strict LEFT PROFILE facing left, bottom LEFT THREE-QUARTER. Image 1 supplies the two moose identities, coat, narrow continuous keel breast and planted upright pelvis. Image 2 supplies the intended difference of torso lean. Each creature stands calmly on two long supporting hind legs, hooves planted beneath the pelvis. Keep complete figures in generous white cells.
Anatomy and balance: tall thoracic spinous processes form the steep withers; the neck ligament suspends the heavy antlered head. The deep rib cage is flattened side to side. Scapulae lie flat upright along its narrow sides. Short upper arms are tucked beside the ribs, elbows point backward, long straight forearms end in slender hands with dark hoof fingertips. Continuous coarse coat in broad clay facets. Long dry hind legs with tendons, cloven hooves, dark charcoal shorts. Long drooping muzzle, large ears, dewlap.
ST.1: moderate withers, small antlers. A tall trunk whose hip-to-withers line leans 18 degrees from vertical. Hands finish at the middle of the thighs: each entire hand rests beside the upper half of the thigh, its fingertips halfway between hip joint and knee. Compact hanging forelimbs fit the distance between low ribs and mid-thigh.
ST.2: broad palmate antlers, taller longer withers, longer forward neck and a deep elongated keel chest. A strongly diagonal rising trunk: hip-to-withers line leans 35 degrees from vertical, 55 degrees above the ground. Picture a solid inclined column anchored at the upright pelvis: for every 100 units of rise above the hip, the withers advance 70 units toward the muzzle. The withers stand far forward of the hip and high above it. The rear leg column stays beneath the hip, the hip joint's plumb line falls through the middle of the hoof contact. The pelvis and load-bearing hind legs form a steady grounded pedestal for the diagonal trunk; the neck rises from that inclined trunk, supporting the head. Hands finish just ABOVE the knees, fingertips distinctly above kneecaps. The two stages have visibly different hand levels and visibly different trunk inclinations. The hind legs stand long and extended, knees softly unlocked, weight settled over the planted hoof soles. Three-quarter row rotates these same corrected poses and heads together toward the viewer, preserving each stage's lean and hand level.
```
