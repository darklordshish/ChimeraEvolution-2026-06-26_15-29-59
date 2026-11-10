# PROMPTS — плитки кожи, 10.11.2026

Встроенный ImageGen. По одной исходной генерации и одной корректировке на вид; r2 — парный атлас: слева цвет, справа высота. Промпт — намерение, не доказательство бесшовности или точной регистрации.

## волк

### r1

```text
Create a single 1024x1024 seamless tileable grayscale HEIGHT MAP for a stylized low-poly game wolf skin. Short broad overlapping fur clumps, big flat angular polygon planes, about 4 clumps across and 5 down, pointed ends downward, all clumps similar scale, no individual hairs or fine noise. White means elevated fur ridge, medium gray flat clump faces, dark gray narrow recesses. Orthographic straight-on mathematical height encoding, NOT shaded beauty render, no directional lighting, no shadows, no perspective. Truly periodic wrapping left/right and top/bottom, features cross all boundaries, no border, no vignette, no text. Entire image occupied by surface. Range approximately 60 to 225. Save generated image.
```

### r2 — единственная корректировка

```text
Revise this wolf surface once for delivery. Output a precise two-panel horizontal technical texture atlas, aspect ratio 2:1, no margin or divider: LEFT HALF is a square seamless light-gray ALBEDO tile, RIGHT HALF is the EXACT SAME square pattern as a grayscale HEIGHT MAP (white higher). Both maps pixel-aligned feature for feature, identical scale and position. About 4 broad short angular wolf fur tufts across, do not look like scales. Albedo: light middle gray (around 180-210), restrained variation, no lighting. Height: bright raised central ridges and dark recessed overlapping fur roots, NO baked illumination. Each square must repeat seamlessly independently on left/right AND top/bottom: edges must match in motif positions and gray values. No text, no labels, no borders, no cast shadow, no noise. Preserve coarse low-poly flat facet style. Exactly two equal-sized square panels.
```

## лось

### r1

```text
Create one square 1024x1024 seamless repeating grayscale HEIGHT MAP for CHIMERA stylized low-poly game skin. Large planar angular forms, no photorealism or fine noise. White is highest, dark gray is lowest. Pure unlit height encoding, no cast shadows, no specular, no directional shading, no border, labels, perspective or vignette. Surface fills image and wraps perfectly on BOTH axes with cut features continuing across opposite edges. Moose coarse hide: sparse LONG broad stiff fur locks lying downward, considerably longer and more widely separated than wolf tufts, about 3 locks across and 2-3 down over simple gray skin. Broad faceted strips, angular tips. Range 65 to 220.
```

### r2 — единственная корректировка

```text
One correction of this MOOSE skin. Create a precise 2:1 horizontal atlas of TWO square panels without gutters or labels. Left: seamless neutral light-mid-gray ALBEDO, right: pixel-aligned same pattern HEIGHT MAP white=high. Sparse LONG coarse broad angular fur locks on hide, 3 locks across, 2-3 down, larger and rarer than wolf fur; dark gaps shall be narrow not large black cavities. Big flat facets, no individual fine hairs. Albedo light middle gray with restrained variation, NO illumination. Height bright raised ridge in EACH lock, medium-gray slopes, dark narrow recessed roots; no cast shadows. Both square panels individually repeat seamlessly on both axes: ensure cut shapes continue across opposite edges with identical tones. No whole-image brightness gradient. Same lock geometry and location in both panels. Full bleed, no border. Aspect 2:1 exactly.
```

## ёж

### r1

```text
Create one square 1024x1024 seamless repeating grayscale HEIGHT MAP for CHIMERA stylized low-poly game skin. Large planar angular forms, no photorealism or fine noise. White is highest, dark gray is lowest. Pure unlit height encoding, no cast shadows, no specular, no directional shading, no border, labels, perspective or vignette. Surface fills image and wraps perfectly on BOTH axes with cut features continuing across opposite edges. Hedgehog skin BETWEEN quills: short stiff bristle stubble only, absolutely NO long spines or quills. Chunky angular wedge-like stubble in irregular clusters, about 7 clusters across, broad exposed light-middle-gray skin gaps. Coarse readable groups rather than microscopic hairs. Range 80 to 220.
```

### r2 — единственная корректировка

```text
Correct this HEDGEHOG skin: current bristles look like large crystals, replace them with SHORT low stiff stubble wedges on exposed skin, no large quills or crystals. About 7 irregular groups across, broad flat angular shapes, no fine noise. Output 2:1 horizontal atlas with precisely two square panels edge-to-edge: LEFT neutral light middle gray ALBEDO tile (around 190, subtle variation, unlit), RIGHT the IDENTICAL registered pattern as HEIGHT MAP (white high wedge tips, gray skin, dark narrow roots). Same geometry and position in both panels. Both square tiles MUST individually repeat seamlessly horizontally AND vertically with matching opposite edge contours and values. Full bleed, no border, no text, no lighting, no directional shadows, no vignette. Big readable groups, not realistic hairs. This depicts skin between the actual quills, not quills.
```

## змея

### r1

```text
Create one square 1024x1024 seamless repeating grayscale HEIGHT MAP for CHIMERA stylized low-poly game skin. Large planar angular forms, no photorealism or fine noise. White is highest, dark gray is lowest. Pure unlit height encoding, no cast shadows, no specular, no directional shading, no border, labels, perspective or vignette. Surface fills image and wraps perfectly on BOTH axes with cut features continuing across opposite edges. Snake keeled scales: regular interlocking staggered rows of overlapping RHOMBUS diamond scales, about 4 scales across and 5 down. Each elongated diamond has a clear raised central longitudinal keel from upper tip toward lower pointed tip; two broad flat facets slope down on either side. Narrow recessed overlap seams, restrained grayscale range 55 to 230. No rounded cobblestones or hexagons.
```

### r2 — единственная корректировка

```text
One correction: preserve this snake diamond scale pattern and its clear LONGITUDINAL CENTRAL KEELS. Deliver a 2:1 horizontal atlas of TWO square texture tiles edge-to-edge, no divider. LEFT is neutral light-middle-gray ALBEDO (mean about 195, restrained contour variation, no light or dark directional shading). RIGHT is pixel-registered HEIGHT MAP of EXACT SAME scales (white raised central keel, gray slopes, dark recessed overlapping diamond edges). About 4 elongated rhombi across and 5 rows down. Flat large polygon facets, no fine noise or photorealism. Crucial: each square tile must repeat perfectly in BOTH directions, full motifs across opposite boundaries must match position AND value. Do not use a centered sample with arbitrary edge cuts: use an exact integer period of the staggered lattice. No whole-image gradient, text, border, shadow, lighting or perspective. Two equal square panels, identical motif layout.
```
