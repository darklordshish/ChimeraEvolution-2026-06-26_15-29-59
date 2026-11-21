# Новая ст.2 оборотня — четыре направления

## Ортопара новой ст.2 — 29.10.2026

![БВ-1 · 45° · мышцы — профиль, анфас, сзади](novaya-st2-orto.png)

Одна принятая фигура «БВ-1 · 45° · мышцы» (третья на `novaya-st2-BV1b.png`) в трёх ортографически заданных видах: **ПРОФИЛЬ влево | АНФАС | СЗАДИ**. Общий масштаб и земля, серый гранёный low-poly, тёмные плавки, гребень на шее, без хвоста — хвост является отдельным органом. Витрина использована только как образец раскладки, её более прямая поза не является целью.

**Чему верить:** профилю как главному ориентиру наклона и выноса головы; анфас — распределению крупных масс поперёк тела: плечевой пояс шире таза, грудь сужается к центру, ноги стоят раздельно, стопы слегка развёрнуты наружу. Кисти свободны и выше колен, голова ниже холки, таз над опорой, ноги пальцеходящие. Виды спереди и сзади дополняют профиль, но не являются отдельно утверждённым новым обликом.

Тонкие горизонтали задают **общие целевые уровни** холки, головы, таза, колена и скакательного. «Голова» в запросе определена как центр глаз, «таз» — центр тазобедренного сочленения, суставные линии — центры суставов, не края мышечных масс. Сзади опущенная голова закрыта верхом спины.

**Ограничения сверки:** финальный PNG открыт после одной корректировки тем же встроенным image_gen. Земля и общий пояс колен согласованы визуально, кисти подняты. Точное совпадение всех ориентиров инструмент не обеспечил: глаза анфас остаются примерно на 20 пикселей ниже линии головы на листе шириной 1942 пикселя; выступы скакательных в профиле примерно на 20 пикселей выше направляющей. Поэтому горизонтали читать как целевые уровни для согласования тела, а не доказательство точной регистрации рисунков. Фронтальные плечевые массы ещё округлее профильного клина; по форме лопатки и груди приоритет у принятого профиля исходного листа. Точные длины костей, оси суставов и мышечные прикрепления с изображения не снимать. Обозначение 45° — имя принятого варианта, не инструментальный замер.

Независимая сверка по `model-critic`: **7/10 как рабочий концепт**, ниже проектного порога готовности 8.5/10. Отмечены округлые фронтальные плечи, разброс скакательных 15–25 пикселей и концов когтей до 25 пикселей между видами. Лист сохранён как рабочий результат; точной согласованной ортопарой для снятия суставов его считать нельзя. Дополнительные проходы после разрешённой одной корректировки не выполнялись.

Встроенный image_gen: первый проход и одна корректировка. Витрина не изменялась. Коммит не создавался.

<details>
<summary>Запросы image_gen: ортопара БВ-1 · 45° · мышцы</summary>

Первый проход: принятый лист — источник третьей фигуры; витрина — только раскладка.

```text
Use case: identity-preserve / stylized-concept. Create a production orthographic turnaround sheet of ONE SAME accepted CHIMERA werewolf stage2 character. Input 1 is authoritative anatomy: ONLY THIRD FIGURE labelled "БВ-1 · 45° · мышцы" from novaya-st2-BV1b.png. Ignore its other 3 figures entirely. Input 2 is LAYOUT/style reference only, NOT anatomy or pose (that character is too upright).
Output a wide white landscape sheet with exactly THREE full bodies in a row: "ПРОФИЛЬ" facing LEFT | "АНФАС" straight front | "СЗАДИ" straight rear. True orthographic camera at identical scale, no perspective or three-quarter. The same frozen pose rotated around vertical axis; do NOT straighten the torso for front/rear! Reproduce source third figure's forward inclined torso about 45 degrees from vertical, high withers, low forward wolf head BELOW withers, tucked abdomen, pelvis over hindfeet, digitigrade bent hindlegs and medium metatarsus. Preserve exact relative vertical heights and proportions from source third figure. The profile is the anchor and must closely match it.
Flat long scapular wedge from withers toward elbow, no round deltoid ball. Deep narrow canine sternum KEEL with flat sides, no paired human pectoral plates. Strong healthy intact muscle volumes, broad restrained polygon facets, NO protruding ribs/no emaciation/no six pack/no flayed ribbons. Neck ridge mane as source, same muzzle ears nose. Gray faceted low-poly, soft consistent studio light, dark charcoal briefs, black claws, no tail at all (tail is separate organ). Free bent arms forward, clawed dexterous fingers and thumbs, wrists and lowest fingertips ABOVE knee level as in source. No weightbearing arms.
Front AND rear: same shoulder width, same pelvis width, symmetrical feet turned slightly outward, identical arm abduction and same body depths implied by profile; shoulders broad but animal and planar, pelvis narrower. In rear the lowered forward head is largely OCCLUDED by withers/upper back: do NOT put an upright head above shoulder line to make it visible. Front lowered head in front of chest, maintaining source height. Same knee and hock heights in all 3 images.
Add five very thin light gray straight horizontal guide lines THROUGH ENTIRE SHEET, aligned to SAME anatomical landmarks in all 3 projections. Small Cyrillic labels in left margin exactly "холка", "голова", "таз", "колено", "скакательный". Define голова line as EYE CENTER (not ear tips); холка at top shoulder/withers below mane spikes; таз at hip joint center; колено at knee center; скакательный at hock joint center. If occluded, guide remains at its true height. Guides must be horizontal not slanted and align with figures rather than arbitrary grid. Shared thin ground baseline at soles. Generous left margin for labels, no clipping. Exact three view titles above columns, no other headings or figures. All figures full body. This is one character in three simultaneous orthographic projections, not three redesigned variants.
```

Одна корректировка: первый результат и принятый лист.

```text
Correct this exact orthographic three-view sheet with ONE targeted consistency pass. Second image is authoritative accepted anatomy, ONLY its THIRD figure БВ-1 · 45° · мышцы.
Keep layout, labels, white background, gray low-poly, dark briefs, no tail, identical scale and ground. Keep current profile torso inclination and source identity. Enforce SAME FROZEN POSE all 3 views.
1. Raise/bend BOTH ARMS in all views so lowest claw tips end clearly ABOVE knee horizontal guide (about 30 pixels above at this image scale), like accepted reference. Preserve arm lengths, articulate elbows; no hanging below knees.
2. Align EYE CENTERS in profile and front exactly to "голова" guide. Front eyes now about 20px too low: raise head only by that amount. Keep head below withers; rear head remains occluded.
3. Align hock centers of all views exactly to "скакательный" guide; profile hocks now too high: raise guide to actual profile hock center, and match front/rear hocks to it. Shared knees/hip/withers and ground stay fixed.
4. FRONT shoulders currently spherical deltoid caps and paired breast plates: replace with accepted elongated FLAT scapular wedge and single DEEP NARROW STERNAL KEEL, broad continuous flat animal muscles. No oval shoulder groove, no paired pectoral slabs, no sixpack. Do NOT reduce shoulder power. Rear matching long flat scapular planes.
5. Profile rear leg and near leg must be at SAME knee height and SAME hock height (no stagger in depth vertical position). Straight flat horizontal guides pass true centers. Front/rear same shoulder width, pelvis width, feet turn-out and hand spacing.
Exact text ПРОФИЛЬ | АНФАС | СЗАДИ and left холка / голова / таз / колено / скакательный. Thin pale guides unobtrusive. Preserve source 45-degree lean and head below withers, not upright. No other changes.
```

</details>


## БВ-1 · 45° · мышцы — проход 29.10.2026

![БВ-1: уточнение мускулатуры](novaya-st2-BV1b.png)

Геймдизайнер принял позу и наклон «БВ-1 · 45°» на предыдущем листе. Здесь изменена мускулатура этой фигуры: округлая дельта заменена длинным плоским диагональным клином от высокой холки к локтю; грудная выпуклость сглажена в глубокий суженный киль. Бока цельные, без выступающих рёбер, кубиков пресса и рельефа содранной кожи; живот подобран, сильные проксимальные массы переходят в сухие дистальные участки.

Четыре фигуры: «НОВАЯ СТ.1» | «БВ-1 (было)» | «БВ-1 · 45° · мышцы» | «СТ.3 — ВОЛК». Вариант 40° исключён. Сохранены серый low-poly, профиль влево, белый фон, общий свет и линия земли.

**Чему верить:** направлению лопаточного клина и плоским массам под кожей у третьей фигуры, отсутствию рёбер и культуристской сегментации. Наклон, высота головы и рисунок конечностей визуально сохранены относительно принятого 45°; число — имя варианта, не измеренный угол. Длинные сухие переходы не означают истощение. Финал открыт после одной корректировки: отдельного плеча-шара и грудных пластин у третьей фигуры не видно, рёбра не выпирают.

**Ограничения:** это профильный концепт, узость груди поперёк тела здесь не проверяется. Не снимать с него мышечные прикрепления, длины костей или оси суставов. Крайние фигуры и «было» запрошены без изменений, но инструмент перерисовал лист и перераспределил интервалы: пиксельная идентичность и точная метрическая неизменность опор не гарантированы. Их анатомию по этому проходу не переутверждать; при расхождении пользоваться исходным листом и витриной.

Встроенный image_gen: первый проход и одна корректировка тем же инструментом. Витрина не изменялась, коммит не создавался.

<details>
<summary>Запросы image_gen: мускулатура БВ-1 · 45°</summary>

Первый проход: цель — `novaya-st2-BV1.png`.

```text
Use case: precise-object-edit. Edit attached novaya-st2-BV1.png. Deliver a FOUR figure comparison sheet, removing ONLY the fourth figure labelled 40 degrees and closing that gap. Same canvas height, figure scale, left-facing profile camera, gray faceted low-poly clay, white background, light and ground.
Exact headings left to right: "НОВАЯ СТ.1" | "БВ-1 (было)" | "БВ-1 · 45° · мышцы" | "СТ.3 — ВОЛК".
Copy original figure1, figure2 and last wolf unchanged. The ONLY anatomy edit is original THIRD figure "БВ-1 · 45°". Preserve its exact pose, torso angle, head height and location relative to withers, proportions, arm and leg positions, fingers, feet, medium metatarsus, charcoal briefs. Do not straighten or lower torso.
Change only its muscle architecture under intact skin:
Replace round spherical human deltoid shoulder cap entirely with a LONG FLAT SCAPULAR WEDGE descending diagonally from HIGH WITHERS toward ELBOW. Use original SECOND figure's elongated blade architecture as guide, but NO rib ridges from that figure. Shoulder must be elongated planar wedge integrated into back and upper arm, not ball on arm.
Replace inflated human pectoral plates with a DEEP NARROW CANINE STERNAL KEEL like wolf or greyhound. No paired pectoral slabs, no chest hemispheres.
Flanks are continuous broad flat healthy muscle volumes under intact skin. NO protruding ribs, NO parallel rib grooves, NO flayed anatomy texture, NO six-pack. Abdomen tucked. Strong upper arm and thigh taper gradually into long dry distal tendons. Healthy powerful animal with adequate mass, never emaciated or bodybuilder.
Maintain existing contour/pose except minimal local muscle surface changes necessary to eliminate round shoulder and pecs. No tails. Do not alter three comparison anchors. Full bodies uncropped. No new panels or annotations. Four figures only.
```

Одна корректировка: первое изображение — результат первого прохода; второе — исходный `novaya-st2-BV1.png` для сохранения позы.

```text
Edit the FIRST image (four figure muscles sheet), using SECOND original five-figure sheet only to lock the target's original third-figure 45-degree pose. ONE targeted correction ONLY to muscle surfaces of THIRD figure in first image. All other figures, headings, canvas, scale, camera, light, briefs and ground unchanged. Third figure pose, head height, torso lean, limbs and feet unchanged.
The third figure still has an OVAL SHOULDER CAP and hanging rounded PECTORAL BULGE. Eliminate both visibly. Scapula must be a LONG FLAT THIN DIAGONAL BLADE from high withers toward elbow, a tapering wedge at least twice as long as wide, continuous with upper arm, NO circular or oval isolated deltoid mass, no encircling groove. Refer to elongated flat architecture of second figure but do NOT copy its ribs. Large flat elongated sloping planes, not a faceted ball. Chest: remove low rounded pec blob entirely, contour a single narrow deep sternum keel integrated into ribcage with flat sides, canine greyhound anatomy. Healthy thick skin over solid muscles, broad continuous smooth flank, zero individual rib ridges, no six-pack, no flayed grooves. Keep strong healthy mass and tucked abdomen. Do not change torso angle or head/arm/leg placement to accomplish muscle edit. Exact four labels unchanged.
```

</details>

29.10.2026. По выбранному геймдизайнером варианту 1 («сдвиг»): нынешняя ст.2 становится новой ст.1; между ней и чистым волком ищем новую ст.2. Это варианты одной ступени, а не последовательные стадии. Лист для выбора направления; витрина не изменена.

![Профильное сравнение](novaya-st2-varianty.png)

А — Поджарый вытянутый охотник: вынесенная вперёд голова, протяжённая шея, узкая глубокая грудь и подобранный живот; платит меньшим ощущением силы, а расстояние от новой ст.1 здесь самое осторожное.
Б — Низкий тяжёлый охотник: самый сильный наклон, низкая голова, короткая шея, массивные плечи и предплечья при свободных кистях; платит риском гориллоподобного силуэта и впечатления, что тяжёлому переду нужна опора руками.
В — Высокий пружинистый охотник: длинная плюсна, высокий скакательный сустав и поднятый таз при относительно лёгком корпусе; платит более вертикальным общим впечатлением и риском слишком ходульных ног, поэтому звериность корпуса надо удерживать отдельно.
Г — Компактный зверь с гривой: короткая толстая шея, низкая посадка головы и крупный покров вокруг плеч, затылка и внешних сторон конечностей; платит сокрытием анатомии и зависимостью читаемости от покрова — голое тело ещё потребует отдельного сравнения.

Слева — опорная новая ст.1 по среднему профилю [тела витрины](../../../витрина/оборотень/тело.png). Справа — ст.3 по профилю [ортопары волка](../../../витрина/волк/орто.png). Все смотрят влево, стоят на общей линии земли; серый low-poly, белый фон, единый свет, тёмные плавки у двуногих. В сохраняет большую высоту, Б — меньшую: фигуры не подогнаны каждая под одну высоту. У всех четырёх кандидатов двуногая опора, руки с отдельными пальцами и кистями над землёй. Волк справа — отдельное четвероногое шасси.

Чему верить: различию крупных направлений в профиле — выносу шеи и головы, распределению массы, рисунку задней ноги и роли покрова. Сначала сравнивать каждого кандидата с левой опорой, затем кандидатов между собой. Ни один вариант пока не выбран.

Чего не мерить: это генеративный концепт, не проекции существующих мешей. Обе крайние опоры перерисованы, а не вклеены пиксель в пиксель; при расхождении верна витрина. Общий масштаб и свет заданы для визуального сравнения, метрическая калибровка не выполнялась. Наклоны 35° / 50° / 35° / 45° в запросе — художественные ориентиры от вертикали, не замеры результата и не требования к ригу. Пальцы, плечевые суставы и колени требуют анатомической доводки после выбора; по листу не назначать длины костей. Варианты покрова не задают обязательные органы или процент шерсти на ступени.

Сделано встроенным image_gen с двумя изображениями витрины как референсами. Коммит не создавался.

<details>
<summary>Полный запрос генерации</summary>

```text
Use case: stylized-concept. Create ONE wide landscape comparison sheet, ideally 3600x1500 or similarly wide, six full-body figures in a single horizontal row on pure white. CHIMERA game design exploration, gray faceted low-poly clay renders exactly in the visual language of the supplied reference sheets, charcoal gray swimming briefs on ALL FIVE bipedal figures, soft identical studio lighting, black nose and claws. Strict orthographic LEFT-facing side profile for EVERY figure; no front views, no three-quarter, no perspective. Common ground baseline, identical camera scale throughout (do NOT normalize each subject to same height), clear generous spaces, no overlaps or crops.
Input image 1 is the approved werewolf body sheet: reproduce its MIDDLE profile as the LEFTMOST anchor, preserving its anatomy, posture, head, briefs, raised heel and proportions as faithfully as possible. Label it exactly "НОВАЯ СТ.1". Do not beef it up or redesign this anchor.
Input image 2 is the approved wolf: reproduce its LEFT profile as the RIGHTMOST quadrupedal comparison anchor, same head, neck ruff, faceted fur, tail, body proportions. Label it exactly "СТ.3 — ВОЛК". Wolf on all four legs, no clothes. Its withers approximately hip height of the bipedal anchor, much lower than biped heads, normal wolf size relative to humanoids.
Between anchors show FOUR genuinely DISTINCT alternative designs of NEW stage 2, NOT four successive stages. Label these four individually with large exact CYRILLIC letters "А", "Б", "В", "Г". All remain unequivocally BIPEDAL humanoid predators with pelvis balanced over two hind legs and two FREE ARMS with elbows, articulated wrists, separate long grasping clawed FINGERS and thumb. Hands visibly suspended above ground and available for use, not weightbearing forepaws. No tails on bipeds, no weapons. Same wolf muzzle length and ears across five bipeds; make differences in body architecture, neck insertion, shoulders, legs. No human bodybuilder pectoral plates. All stage2 proposals clearly more bestial in whole-body architecture than stage1, even ignoring fur. Standing neutral comparative stance rather than action or snarling.
А: lean forward-reaching hunter, torso axis approx 35 degrees from vertical, deep narrow rib cage and strongly tucked belly, long forward neck flowing out of spine, head clearly ahead of chest, comparatively slim shoulders and arms, bent free elbows, moderate raised hocks, sparse short faceted fur only neck ridge. The lean long silhouette is primary.
Б: low stalking powerhouse, torso axis 50 degrees from vertical, low forward head seated into short thick neck, greatly enlarged high scapular shoulder mass and deeper rib cage, strong longer forearms and large grasping hands held forward of knees but well off ground, low bent hindleg stance, moderate metatarsus, minimal fur so structure is visible. Broad low heavy front-biased silhouette BUT pelvis remains over hind feet, NEVER a rearing wolf.
В: high-legged springy hunter, torso axis 35 degrees from vertical, slender deep chest, narrow waist, notably LONG metatarsus with visibly HIGH hock above ground, knee points forward and ankle/hock points backward below knee; taller pelvis and relatively tall overall silhouette compared to other variants AT SAME CAMERA SCALE, slender hands and arms bent and held freely, long neck and low forward head, minimal fur. The long rear-leg zigzag must be obvious and must not be confused with merely crouching.
Г: compact neck-and-ruff driven beast, torso axis 45 degrees from vertical, shortened thick forward neck with low head partly nestled into a MASSIVE coarse faceted mane flowing from occiput over high shoulders and upper back, deep compact ribs tucked belly, shorter robust hind legs and medium raised hock, bulky upper arms but articulated narrow wrists and long dexterous claw fingers, denser angular fur over outer forearms and calves. Maintain visible briefs and readable leg joints, no fur obscuring everything. Distinct compact and bristling silhouette.
Text only six headings, placed consistently above each figure: НОВАЯ СТ.1 / А / Б / В / Г / СТ.3 — ВОЛК. Crisp readable typography. No diagrams or measurement annotations. Render at high detail with large clean triangular planes, restrained surface detail and consistent gray material. This is a shape comparison sheet, do not create a montage of separate camera shots.
```

</details>


## Второй лист — Б × В, звериная мускулатура

29.10.2026. По отзыву геймдизайнера: «вариант Б хорош по силуэту, вариант В хорош по звериной сухости, но сделал бы более звериные мышцы». Три альтернативы одной новой ст.2, не последовательность превращения. Первый лист выше сохранён.

![Б × В — профильное сравнение](novaya-st2-BV.png)

БВ-1 — Ближе к Б: самая большая масса высокой лопатки и холки, глубокая грудь и низкая голова; платит тяжестью переда и наибольшим риском, что поза кажется требующей опоры руками.
БВ-2 — Середина: умеренная масса плеч, длинные плоские мышечные ленты и сухие переходы в конечности при низкой голове; платит менее выраженным характером обоих полюсов — тяжёлой мощи Б и пружинистости В.
БВ-3 — Ближе к В: самый лёгкий и сухой корпус, длинная плюсна и высокий скакательный, вытянутые сухожилия; платит меньшим ощущением силы плеч и риском ходульности задних ног.

Общее: сильный наклон корпуса с ориентиром около 50° от вертикали, голова ниже холки, глубокая грудь, подобранный живот, двуногая опора с тазом над областью стоп; свободные кисти подняты к уровню над коленями. Вместо округлых человеческих дельт и грудных пластин — вытянутая лопаточная масса и косые плоские мышечные ленты; плечо и бедро сужаются в длинные сухие сухожилия. Это художественная звериная анатомия, не атлас мышечных прикреплений. Хвостов нет у всех пяти фигур, включая волка: хвост — отдельный орган.

Слева — та же опорная новая ст.1, справа — четвероногий волк ст.3 без хвоста. Все смотрят влево; сохранены белый фон, серый low-poly, тёмные плавки двуногих, общий свет и линия земли. Опоры и масштаб перенесены генеративно из первого листа, без метрической калибровки; при расхождении анатомии опор верна витрина (кроме явно заказанного отсутствия хвоста). Чему верить: низкому силуэту, распределению плечевой массы и переходу от тяжёлого к сухому варианту. Не назначать по листу длины костей, углы рига или точные мышечные прикрепления; 50° — ориентир запроса, не замер результата. Ни один БВ-вариант не утверждён.

Сделано встроенным image_gen по первому листу, затем одной корректировкой наклона и мышечных масс. Коммит не создавался.

<details>
<summary>Запросы генерации второго листа</summary>

Первый проход:

```text
Use case: stylized-concept. Create a second CHIMERA comparison sheet based on the supplied reference sheet. ONE wide landscape image, same aspect ratio, gray faceted low-poly clay style, pure white background, identical soft studio lighting and camera scale as reference. FIVE full body figures only, in a single row, all strictly LEFT-facing SIDE PROFILE, shared ground baseline, no overlap, no cropping. Exact bold black Cyrillic headings above figures from left to right: "НОВАЯ СТ.1", "БВ-1", "БВ-2", "БВ-3", "СТ.3 — ВОЛК".
Reference: leftmost figure is the NEW STAGE1 anchor; reproduce this SAME anchor, posture, proportions, size and charcoal briefs. Rightmost reference wolf is stage3 anchor; reproduce its size and quadrupedal stance BUT REMOVE ITS TAIL COMPLETELY. NO TAILS anywhere on this new sheet. Same scale means anchors retain relative heights to each other, do not normalize figures to equal height.
Replace the four interior reference alternatives with THREE new alternatives combining reference Б's low heavy forward-inclined silhouette and reference В's lean dry sinewy anatomy. All THREE have about 50 degree torso lean FROM VERTICAL, low forward wolf head, high scapular and withers mass above the head base, deep ribcage, strongly tucked abdomen, pelvis balanced above hind feet. Upright BIPEDAL support on two digitigrade hind legs, two free grasping arms, hands held ABOVE KNEE LEVEL by naturally bent elbows. Long separate clawed fingers and thumbs, hands well off ground. Never gorilla arm lengths, never knuckle walking. Same neutral comparative stance, same muzzle and ears, dark charcoal briefs on all bipeds.
CRITICAL ANATOMY CHANGE FOR ALL THREE: animal musculature like a wolf or greyhound adapted to a biped. Long flat muscle ribbons flowing along ribs and limbs, prominent elongated scapular blade and withers rather than rounded deltoid balls and pectoral plates. Deep narrow rib cage, oblique flat shoulder muscles sweeping from high scapula toward upper arm. Upper arm and thigh taper into LONG DRY TENDONS and narrow wrists and hocks. No square human pectorals, no six-pack abdominal tiles, no bodybuilder bulges, no inflated biceps or spherical shoulder caps, no fat. Sparse fur only along neck ridge so the animal architecture remains visible. Natural closed mouths.
БВ-1: closest to Б shoulder MASS, largest high scapular/withers wedge and deepest chest, strong low forward silhouette, but all mass expressed as long flat animal muscular sheets, slender tendon-dominant forearms and lower legs, tightly tucked waist. Medium metatarsus.
БВ-2: balanced middle, visibly reduced scapular/upperarm mass relative to БВ-1, elongated flat lean animal muscle ribbons, deep chest and lean tucked flank, moderate-long metatarsus, same low head and strong 50-degree lean.
БВ-3: closest to В in DRYNESS and LIGHTNESS, narrowest and most sinewy shoulders but still a high withers wedge, long flat minimal muscle volume, exceptionally dry limbs with long tendons, LONG METATARSUS and visibly HIGH HOCK. Clearly longer springy hind-leg zigzag at same camera scale; preserve the low head and 50-degree forward torso lean, do not stand vertically. Hands above knees. Tall pelvis balanced over hind feet.
Maintain clear differences in shoulder mass across three candidates. Restrained large clean polygonal planes, no anatomy illustration colors, no diagrams, no extra text. This is a shape comparison, not successive stages. Exact five headings only.
```

Корректировка (финальный файл):

```text
Edit this comparison sheet. Preserve exact canvas, style, five labels, common ground line, leftmost НОВАЯ СТ.1 anchor and rightmost tailless wolf unchanged. Change ONLY the THREE CENTER БВ figures. Main correction: make ALL THREE much more forward inclined, like variant Б of the earlier reference: torso leaning FIFTY DEGREES FROM VERTICAL, almost diagonal toward horizontal. Lower their heads substantially, noses around upper chest height of the unchanged leftmost anchor, head and neck extending forward LOW below a very high scapular withers ridge. Their heads must visibly be lower than the left anchor's head, NOT all heads aligned as now. Keep pelvis balanced above hindfeet, no quadrupedal support, keep hands ABOVE KNEES by bending elbows more strongly. Keep highest shoulder MASS in БВ-1, middle БВ-2, lightest and driest БВ-3 with long metatarsus and high hock.
Second correction is crucial: remove spherical HUMAN deltoid caps, remove breast-shaped pectoral bulges and segmented abdominal bumps from all three center figures. Sculpt long FLAT elongated ANIMAL MUSCLE RIBBONS, wolf/greyhound shoulder anatomy, large sloping elongated scapular blade under skin high on the back, lean oblique flat sheets covering long deep ribcage, one smooth deeply tucked abdominal flank. The shoulder blade is a LONG FLAT WEDGE running diagonally from high withers toward elbow, NOT a round human shoulder ball. Long narrow muscle bellies merge gradually into long taut tendons on arms and thighs, extremely dry distal limbs. No square pecs, no six pack, no bodybuilder, no gorilla. Sparse fur only on dorsal neck ridge. No tails. Preserve gray large faceted low-poly material, studio light, dark briefs, clear left-facing full body profile. БВ-1 maintains big withers mass but it is long and flat, БВ-2 balanced, БВ-3 lean and light. Same camera scale, no crops.
```

</details>




## Третий лист — выбранный БВ-1, подъём корпуса и здоровые мышечные массы

29.10.2026. Геймдизайнер выбрал БВ-1 второго листа («лучше всех») и принял две поправки координатора: поднять почти горизонтальный корпус и убрать истощённый рельеф боков. Выбор исходного БВ-1 принят; два новых наклона ниже — сравнение для следующего решения, не новые последовательные ступени.

![БВ-1 — было, 45° и 40°](novaya-st2-BV1.png)

Пять фигур слева направо: «НОВАЯ СТ.1» | «БВ-1 (было)» | «БВ-1 · 45°» | «БВ-1 · 40°» | «СТ.3 — ВОЛК». Крайние опоры и старый БВ-1 перенесены из `novaya-st2-BV.png` с сохранением облика и позы; перенос генеративный, не попиксельная вклейка. Все смотрят влево, общий профильный ракурс, серый гранёный low-poly, белый фон, общий свет и линия земли. Хвостов нет, на двуногих — тёмные плавки.

В обоих новых вариантах подняты корпус и голова: голова впереди груди и ниже холки, но уже не у пояса. Сохранены тяжёлая холка, длинная лопаточная масса, глубокая грудь, подобранный живот и средняя плюсна. Бока закрыты цельными плоскими мышечными массами под кожей, без выступающих рёбер и рельефа «содранной кожи»; сухие переходы к кистям и скакательным оставлены. Руки свободны, кисти подняты над областью колен, таз расположен над областью стоп.

- **45°:** сильнее сохраняет низкую подачу головы и тяжесть переда выбранного БВ-1. Плата — меньший отрыв от четвероногого силуэта и более сильное ощущение переднего перевеса.
- **40°:** голова выше, двуногая стойка и промежуточное положение между ст.1 и волком читаются яснее. Плата — часть низкого крадущегося характера БВ-1 и больший риск сближения со ст.1.

**Чему верить:** сравнительному порядку наклонов («было» сильнее наклонено, затем 45°, затем более прямые 40°), высоте головы относительно холки, цельной поверхности боков и свободной двуногой опоре. Новые фигуры задуманы одним телосложением с разным наклоном корпуса и высотой головы; небольшой генеративный дрейф формы и позы не является отдельным дизайн-решением.

**Чему не верить:** числа углов — ориентир запроса, не замер результата; лист не задаёт длины костей, баланс центра масс, суставные оси или мышечные прикрепления. У новых фигур сохранилась некоторая округлость плечевой массы: её не переносить как человеческую шарообразную дельту, целевой принцип — высокая длинная лопатка-клин и плоские массы под кожей. Для опор при расхождении верна витрина; витрина этим листом не обновляется.

Сделано встроенным image_gen: первый проход и одна корректировка тем же инструментом. Финальный PNG открыт и проверен: новые наклоны различимы и меньше старого, рёбра не выпирают, руки не опираются, порядок подписей верен. Старые изображения сохранены. Коммит не создавался.

<details>
<summary>Запросы генерации листа БВ-1</summary>

Первый проход. Цель редактирования — `novaya-st2-BV.png`; `novaya-st2-varianty.png` — дополнительный ориентир поднятого корпуса варианта Б.

```text
Use case: precise-object-edit. Edit the FIRST reference sheet novaya-st2-BV.png into ONE final five-figure comparison sheet. The second reference novaya-st2-varianty.png only guides the raised torso posture of its Б figure, NOT its rounded human musculature.
Maintain exact first-reference canvas proportions, camera, scale, gray faceted low-poly clay, white background, soft studio light, shared thin ground line. All full-body figures face LEFT in side profile. Five headings, exact Cyrillic text in this order: "НОВАЯ СТ.1" | "БВ-1 (было)" | "БВ-1 · 45°" | "БВ-1 · 40°" | "СТ.3 — ВОЛК".
Figure 1: copy first reference's leftmost stage1 UNCHANGED. Figure 2: copy first reference's БВ-1 (second figure) UNCHANGED in anatomy, almost horizontal low torso, low head, dry muscle ridges, stance, size, briefs. Only change its label to БВ-1 (было). Figure 5: copy first reference's rightmost TAILLESS quadrupedal wolf UNCHANGED.
Replace figures 3 and 4 with TWO variations of exactly the SAME БВ-1 character and mass, not БВ-2 or БВ-3. Preserve БВ-1 huge elongated scapular wedge/high withers, deep chest, tucked abdomen, medium metatarsus, pelvis over hindfeet, free bent arms with grasping clawed fingers and thumbs held ABOVE THE KNEES, charcoal briefs, NO TAIL.
Critical posture change: raise both new torsos substantially from old near-horizontal slope. Torso axis from pelvis to shoulder is about 45 DEGREES FROM VERTICAL in figure 3 and 40 DEGREES FROM VERTICAL in figure 4. Thus figure 4 is slightly MORE UPRIGHT with slightly HIGHER HEAD than figure 3. Both are clearly more inclined than unchanged stage1 (15–25 from vertical), clearly more upright and higher-headed than old БВ-1. Heads stay BELOW WITHERS and IN FRONT OF CHEST, never down at belt level. Raise shoulder/head by rotating torso up, do NOT shrink torso or change camera scale. Keep same legs and anatomical proportions across both new versions; only torso inclination and corresponding head height differ. Distinction between new angles must be visually readable.
Critical surface correction for BOTH NEW figures ONLY: remove protruding ribs, repeated rib ridges and flayed-muscle ribbons on flanks. Healthy strong beast with smooth continuous FLAT muscular masses UNDER intact skin, large restrained polygon facets. Preserve beast architecture, elongated high scapula, deep rib cage, tucked belly, long distal tendons into wrists and hocks. No emaciation, no ghoul or hyena starvation, no exposed tendons on torso, no human bodybuilder spherical deltoids/pecs, no six-pack. Keep large shoulder MASS, not skinny.
Hands are clearly free above knees, not resting on knees or ground. No arm support. Feet on shared ground, no tails anywhere. Three source comparison anchors must retain original identity, posture, relative heights and anatomy. No extra figures, no inset, no angle arrows, no text beyond exact five labels.
```

Одна корректировка первого результата (финальный файл):

```text
Edit this exact five-figure comparison sheet with ONE targeted correction to ONLY figures 3 and 4. Preserve first stage1, second БВ-1 (было), fifth tailless wolf, canvas, labels, scale, camera, light and ground unchanged.
The two NEW figures are currently too similar in inclination, hands too low, shoulder too round. Correct BOTH while keeping same БВ-1 body anatomy and healthy smooth skin: 
Figure 3 "БВ-1 · 45°": lean its torso slightly farther FORWARD than now, a definite diagonal 45 degrees FROM VERTICAL from pelvis to withers, lower its head slightly, but head still clearly higher than original second figure. 
Figure 4 "БВ-1 · 40°": rotate its torso visibly UPWARD by another small increment so it is clearly more upright than figure3, head noticeably higher than figure3 but BELOW WITHERS and ahead of chest. No raised human neck, retain massive high scapula. These are identical character builds with only torso lean and corresponding head height changed, same pelvis height, leg stance, chest mass and medium metatarsus. Avoid making stage2 as upright as stage1. 
BOTH new figures: bend elbows more, lift entire clawed grasping hands so LOWEST FINGERTIPS are clearly ABOVE TOP OF KNEECAPS, visible free space to knees. Hands float freely in front, never rest on knees or ground. Keep forearm lengths, change elbow articulation only.
Flatten the round deltoid caps into long strong sloping scapular wedges under intact skin, large continuous planar muscle masses. Preserve shoulder mass and deep chest. No individual protruding ribs or flayed ribbon muscles. Healthy strong beast, no bodybuilder. No tail, charcoal briefs.
Exact five headings unchanged: НОВАЯ СТ.1 | БВ-1 (было) | БВ-1 · 45° | БВ-1 · 40° | СТ.3 — ВОЛК. Use centered middle dot · between БВ-1 and angle. Add a very faint continuous horizontal ground baseline at soles if missing. No other changes.
```

</details>


## Ортографический лист r2 — сухая масса анфас и сзади

29.10.2026. Исправление по сверке координатора. Исходный `novaya-st2-orto.png` сохранён. Цель — принятая третья фигура «БВ-1 · 45° · мышцы» из `novaya-st2-BV1b.png`; профиль исходной ортопары оставлен опорой без намеренной перерисовки. Перенос генеративный, попиксельная идентичность профиля не гарантируется.

![Ступень 2 — ортографический лист r2](novaya-st2-orto-r2.png)

В анфас и сзади сужен плечевой пояс: визуальное отношение наружной ширины плеч к ширине таза около 1.4 (без кистей и разведённых рук; ориентировочная оценка по изображению, не калибр меша). Убраны круглые дельтовидные шары, широкие грудные пластины и культуристский развал спины. Лопаточная масса вытянута вдоль рёбер плоскими клиньями; грудь узкая поперёк и глубокая в профиле, живот цельный, без кубиков. Руки сухие, с длинными переходами к запястьям. Сохранены серые крупные грани, белый фон, свет, тёмные плавки, подписи, отсутствие хвоста и двуногая опора.

Сверка после открытия финального PNG: плечи умеренно шире таза; отдельных шаров дельт и грудных пластин нет. Сохранены общие горизонтали холки, головы, таза, колена, скакательного и земли. Во второй, единственной корректировке подняты глаза анфас и уточнены уровни суставов. Остаточная неточность порядка нескольких–десяти пикселей у глаз и суставных центров остаётся: лист не является точным ортографическим чертежом. Не выдавать непрерывность направляющих за доказательство полной анатомической регистрации.

**Чему верить:** поджарой поперечной массе анфас и сзади, плоскому плечу, узкой груди, сухим рукам. **При расхождении ракурсов верны принятый профиль и третья фигура BV1b**, а не новая интерпретация спины. Не снимать с листа точные суставные координаты, длины костей и мышечные прикрепления.

Сделано встроенным image_gen: редактирование исходной ортопары с BV1b как анатомическим ориентиром, затем одна корректировка. Витрина не менялась, коммит не создавался.

<details>
<summary>Задание image_gen для r2</summary>

Первый проход: сохранить левый профиль, весь макет, свет, подписи и направляющие; перерисовать только анфас и спину как того же поджарого зверя. Ширина плеч к тазу 1.3–1.5; убрать шарообразные дельты, гору человеческой трапеции, грудные пластины и кубики. Длинные плоские лопаточные клинья вдоль узкой глубокой грудной клетки, цельная здоровая кожа, сухие руки и длинные сухожилия. Уровни всех ракурсов свести по неизменному профилю.

Единственная корректировка: сохранить достигнутую ширину и профиль; сгладить отдельные плечевые колпаки в плоские косые лопаточные массы; поднять глаза анфас примерно на 20 px к линии головы, уточнить колени и скакательные относительно соответствующих горизонталей. Не менять подписи, фон, свет и землю.

</details>
