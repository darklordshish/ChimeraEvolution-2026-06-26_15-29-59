# Точные запросы image_gen

К запросу корректировки zmeya в конце добавлено дословно: `Reduce mouth opening height 25% and broaden rear of upper muzzle wedge 20%. Extend one narrowing row of back scale plates halfway farther down back; halve plate rim thickness.`

## Единственная корректировка каждого листа

Передан только соответствующий лист первого прохода как edit target. Все последующие изменения изображений — машинный обвод границ тонов по растру, без повторной генерации.

### volk

```text
Edit this sheet, keep FOUR rows and THREE columns, NO text. Preserve same lean bald human base, dark shorts, human body proportions, all row assignments and neutral gray broad-flat-face style. One allowed correction pass. Make the muzzle more wolf-like: upper nasal wedge slightly longer, lower jaw 20% lighter, few large canine teeth, keep human upper face and scalp. Row 1 MIDDLE must be genuine rear three-quarter (back visible, slight muzzle profile around edge), not side view. Row 3 shorten finger segments 25% and broaden palm/digit bases 20%, keep five fingers and opposed thumb at human wrist, simple short wolf claws, no fur. Row 4 widen upper dorsal fur wedge bases 50%, 6–8 broad overlapping uneven tapered wedges down spine; upper wedges have lateral roots and slope DOWN AND BACK, not a uniform thin saw or armor. Front view shows only natural silhouette glimpses. Keep closeup and fullbody graft shapes consistent. Entire body still human and unchanged. Each planar face one gray, no grain or gradients. Output a larger high-resolution landscape sheet if possible.
```

### los

```text
Edit this sheet, preserve FIVE rows and THREE columns, NO text anywhere, same lean bald human with dark shorts and unchanged proportions, same gray broad-flat polygon language. One allowed correction pass. Main fix ROW 5: moose withers and mane must be a continuous fleshy raised mass at nape transitioning into 4–5 massive backward-sloping mane wedges along upper spine, upper third dominates, lower plates 35% thinner and less protruding. No stacked transverse stairs, no rigid backpack or pauldrons. Human shoulder width and torso retained; local wither only. Row 2 ears 25% narrower, keep long leaf shape, stronger narrow root and tips lifted. Row 3 preserve unmistakable broad palmate antler outline but simplify each palm center into 2–3 large dominant tonal planes; perimeter tines remain. Other rows keep lower-face moose muzzle and split cloven hoof-hands. All front/rear/fullbody/closeup shapes must agree, fully in frame. White background, neutral lighting, hard tone boundaries and no fine texture. Output larger high-resolution landscape sheet if possible.
```

### ezh

```text
Edit this FIVE-row THREE-column sheet, NO text. Preserve same lean bald human proportions and dark shorts, all organs separate by row, calm stance, grayscale broad flat planes and no texture. ONE allowed correction. Mandatory row 4: REMOVE ALL ROUND ANIMAL EARS from both fullbody figures and closeup: head entirely human. Quill wedges on shoulders and back are 8–11 large pointed triangular prisms, 3 faces each; angle back/down, reduce sideways spread 25%, use two unequal overlapping tiers, not radial star or feathers, no launcher tubes here. Mandatory row 5: EXACTLY FOUR STRAIGHT hollow faceted tubes in EVERY view including closeup, grow from behind nape, angled UP AND FORWARD over head; four dark open bores. Not curved tentacles, no bends, no backpack; match their orientation and number in all cells. Row 3 compact paw-like five-digit hand at human wrist: shorten fingers 25%, thicken palm and finger roots, SHORT pronounced hooked claws. Row 1 preserve requested broad nose pad and two tusks but slightly bevel nostril walls. Row 2 keep round ears alone, remove redundant human ears if visible. No animal organs bleed into other rows. Full bodies fully visible. Output larger high-resolution landscape sheet if possible.
```

### zmeya

```text
Edit this FOUR-row THREE-column sheet, NO text. Preserve same lean bald human proportions, dark shorts, human limbs, same layout and broad flat gray planes. ONE allowed correction. Main fix ROW 2: BOTH full-body heads must visibly carry the same snake eye with VERTICAL slit pupil and separate recessed heat pit beneath/forward of it as the closeup; closeup pit must have polygonal 3–4 flat inner walls, not smoothly airbrushed bowl. Row 1 lower face a broad FLATTENED snake wedge rather than canine muzzle, exactly two upper venom fangs, human eyes/brow/scalp retained, no nose whiskers, no whole animal head. Row 3 about 10–14 large RHOMBIC scale plates rooted organically on shoulders/back, low relief and contiguous skin transitions, no raised armored shoulder-pad rims or straps; right closeup must show all upper back plates consistently with full-body. Row 4 long tapered tail from rear pelvis, separate from BOTH HUMAN LEGS, terminal rattle in all views, 30–50 broad planes, tail curve behind and to side fully inside cell; closeup includes actual root and rattle. Tail taper continuous, no dense scale texture. Neutral light, each facet a single uniform tone. Output larger high-resolution landscape sheet if possible.
```


Источники — актуальная соседняя витрина `../ChimeraEvolution/Референсы/витрина/`. В каждой генерации переданы: 1 — человек/огранка.png, 2 — <вид>/огранка.png, 3 — <вид>/вид.png. Ниже общий префикс и точное продолжение каждого запроса.

## Общий префикс

```text
Use case: stylized-concept. Asset type: CHIMERA donor-organ modeling target sheet.
Create a clean landscape reference sheet on white, grayscale matte low-poly sculpture, broad flat polygon planes each a single uniform gray, hard tonal boundaries, quiet even studio light, no texture, no hair strands, no grain, no tiny crumpled triangles. Style must match input 1 human body. Same lean bald adult male human, dark gray shorts, human proportions, human legs and feet and arms UNCHANGED throughout, relaxed standing A pose. Never make a whole animal or werewolf. Each row is an INDEPENDENT isolated graft on otherwise unmodified human.
Strict three-column grid: LEFT full-body front three-quarter view; MIDDLE full-body rear three-quarter view of the SAME graft; RIGHT enlarged closeup of that row's organ with its anatomical attachment. All figures fully inside page, equal body height across rows, generous white spacing, no overlapping cells. No lettering, numbers, labels, title, frames, watermark or text anywhere. Use large enough canvas for all rows. Small detail ear/claw 2–5 planes each; muzzle about 25–40 broad planes. Graft and human carved by same sculptor, no armor seams or accessory straps. References 2 and 3 give donor organ character, NOT full-body anatomy or fine triangulation.
```

## volk — первый проход

```text
FOUR rows, exactly:
1 canine muzzle with slightly open mouth and a few large teeth replaces ONLY human lower face, human brow, scalp, eyes and upper head preserved; no animal ears in this row.
2 pair of pointed wolf ears on otherwise human bald head, no muzzle.
3 wolf clawed hands at human wrists, five functional digits including thumb, broad palm and simple 2–3 plane claws; otherwise human arms, head and feet.
4 a low continuous ridge of 6–8 broad tapered fur wedges rooted along human spine from nape to lumbar back, no fur texture, no shoulder armor, no tail; right closeup rear oblique spine showing skin attachment.
```

## los — первый проход

```text
FIVE rows, exactly:
1 elongated blunt moose muzzle with broad split nostrils replaces ONLY human lower face; human upper face and bald scalp unchanged; no ears/antlers here.
2 pair of large moose leaf ears on human bald head, no muzzle or antlers.
3 pair of unmistakable moose PALMATE antlers, broad flat paddles with few perimeter tines, 20–30 planes per paddle, naturally rooted above human temples; otherwise fully human head.
4 split cloven hoof-hand replaces hand at human wrist, short compact keratin end with two large hoof lobes, NOT equine single hoof, human forearm unchanged; detail wrist attachment.
5 moose raised withers and short mane as 4–6 massive contiguous plates along upper human back, slight localized rise at nape without changing whole torso, no head changes, not shoulder pauldrons; detail rear oblique anatomical attachment.
```

## ezh — первый проход

```text
FIVE rows, exactly:
1 short broad snout with flat piglike nose pad and two protruding tusklike canine teeth replaces ONLY human lower face; human upper face and scalp unchanged.
2 pair of small round dish-shaped hedgehog ears on human bald head; no snout.
3 compact five-digit human-wrist hands with SHORT hooked claws, broad palm, no long talons, rest of human unmodified.
4 a sparse set of about 8–11 LARGE pointed triangular-prism quill wedges rooted on human upper back and shoulders, only three broad faces per quill, total about 20–35 visible planes, NOT hundreds of needles or feathers; no tubes in this row.
5 exactly FOUR hollow faceted organic launcher tubes grow from upper back just behind nape, pointing UP AND FORWARD over head; clearly dark open bores, polygon tube walls, not solid horns, no quill blanket, no armor backpack, human head unchanged. Closeup shows four hollow openings and roots near nape.
```

## zmeya — первый проход

```text
FOUR rows, exactly:
1 snake lower face, broad wedge muzzle replacing ONLY human lower face, mouth slightly open with TWO long upper venom fangs, human upper face and bald scalp unchanged, no snake neck.
2 a snake eye with vertical pupil plus clearly separate recessed heat-sensing pit between eye and nostril, integrated in otherwise human upper cheek/temple; show actual recessed pit in closeup, not a decorative dot; mouth remains human.
3 about 10–14 LARGE rhombic scale plates grow on human shoulders and back; broad single-tone diamond masses, few planes each, no dense scales, no armor straps, no chest armor.
4 long tapering snake TAIL grows from rear pelvis above buttocks and between shorts waistband, both human legs and feet fully retained; tail extends backward then a broad side curve resting near ground, ENDING IN segmented RATTLE, total roughly 30–50 broad planes along tail, no dense scale texture. Right closeup shows tail root and whole tapered tail with rattle, not a severed snake with head. Keep tail fully inside each cell.
```

