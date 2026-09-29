# Cheap generative variation, grotesque exaggeration and soft size blending for procedural creature bodies

Scope: (a) per-individual variation from a seed, (b) deliberate caricature/"mutant" exaggeration of species traits, (c) soft blending of overall body size between species, (d) how these compose. Research date 2026-09-29. Web access was partly blocked (Google "sorry" redirect on one blog, 403 on ScienceDirect, auth redirect on Springer), so some items are marked as unverified.

## 1. How shipped games generate per-individual variation cheaply; what reads as variation vs noise

### Takeaway
Shipped games get variety mostly from **discrete part choice + continuous per-part scale/colour within designer-authored ranges, all driven by a seed**, and they keep results "not random-looking" by constraining generation with per-species templates/rules. Statistical body models show that most meaningful variation lives in **a few correlated global factors** (overall size, bulk). Independent per-bone noise is what reads as noise.

### Cited Findings
- No Man's Sky (Hello Games, GDC 2015 talk by art director Grant Duncan): same base model yields different objects under rules the generator must follow, which "allows for variations in the same models without making them look completely random"; no video, slides only. — [No Man's Sky blog, 2015](https://www.nomanssky.com/2015/02/no-mans-sky-at-gdc/); [NeoGAF thread on the talk, 2015](https://www.neogaf.com/threads/no-mans-sky-gdc-talk-how-i-learned-to-love-procedural-art.1005373/)
- NMS builds creatures from "blueprints" based on real Earth templates (per Game Informer interview), combining parts from assets with a predetermined chance value. — [NMS wiki / Hello Games summary](https://en.wikipedia.org/wiki/Hello_Games); [Kotaku, 2016](https://kotaku.com/a-look-at-how-no-mans-skys-procedural-generation-works-1787928446)
- Datamined analysis (2016): parts selected by weighted probability; multiple procedural textures per part give different colours/marks on identical geometry; size/proportion parameters are implied by "descriptors" but not detailed. — [Game Developer, 2016](https://www.gamedeveloper.com/programming/what-the-code-of-i-no-man-s-sky-i-says-about-procedural-generation)
- Planet Zoo (Frontier, 2019+): each animal has a small set of genes; Size is an **additive** stat (e.g. +42% and +33% sequences sum to 75%); offspring size is randomised roughly between the parents' values; colour/pattern varies "through tens of barely perceptible individual steps" around the parental average; rare discrete mutations (colour morphs). — [Planet Zoo wiki: Genetics](https://planetzoo.fandom.com/wiki/Genetics); [Steam guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2877772538) (fan sources, not developer docs)
- Spore (Maxis, 2008): the hard part of highly varied bodies was animation — motion recorded body-independently and retargeted per creature via IK at runtime (SIGGRAPH 2008, Hecker et al.). Relevant as the cost of varying topology/proportions for animation. — [Chris Hecker site](https://www.chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies); [AWN, 2008](https://www.awn.com/news/spore-experts-share-animation-techniques-siggraph-2008)
- Hobby/academic procedural creatures: e.g. a 3×3×11 lattice of control points around a mesh with each slice randomly scaled in x/y/z. — [Orangepixel devlog, 2020](https://orangepixel.net/2020/10/03/the-start-of-procedural-creatures/); [Christo, Bournemouth MSc thesis, 2022](https://nccastaff.bournemouth.ac.uk/jmacey/MastersProject/MSc22/01/ProceduralCreatureGenerationandAnimationforGames.pdf)
- SMPL human body model: shape parameters β are PCA coefficients learned from thousands of scans; β₁ correlates strongly with height, β₂ with body mass — i.e. the dominant variation is two global correlated factors, not per-part independent changes. — [Choutas et al., arXiv 2206.07036, 2022](https://arxiv.org/pdf/2206.07036); [Human Body Measurement Estimation w/ Adversarial Augmentation, arXiv 2210.05667, 2022](https://arxiv.org/pdf/2210.05667)
- In morphable models, random coefficients can give noisy/unrealistic results; practice is to clamp to k standard deviations (k=2 for body shape, k=4 for face expressions), or ±3σ in statistical shape models; deliberately sampling with 3σ spread produces "new and sometimes extreme body features". — [search summary of SSM/3DMM literature incl. arXiv 1806.06098 (2018), IJCV 2019](https://link.springer.com/article/10.1007/s11263-019-01260-7); [ScienceDirect topic: Statistical Shape Model](https://www.sciencedirect.com/topics/computer-science/statistical-shape-model) (exact attribution of k=2 / k=4 to a specific paper not verified)
- Biology: coefficient of variation of linear traits is negatively related to trait mean (small measurements show higher CV) — relevant because small parts (ears, teeth) naturally vary more in relative terms. — [Pélabon et al., PMC7293077, 2020](https://pmc.ncbi.nlm.nih.gov/articles/PMC7293077/)
- AC Unity crowd talks (GDC 2015) are about AI LOD/recycling of 10,000 NPCs, not appearance variation; no public detail on morph/scale randomisation found. — [GDC Vault, 2015](https://gdcvault.com/play/1022411/Massive-Crowd-on-Assassin-s)

### Inferences
- Cheap recipe consistent with the above: seed → a **small latent vector** (2–4 numbers, e.g. `size`, `bulk`, `limb length`, `head ratio`) sampled from a clamped normal (±2σ), each latent mapped to many bones through **fixed, per-species loading vectors** (hand-authored "PCA-like" directions). Height→weight→bone thickness correlation is then built into the loadings (bulk raises all radii together; size scales everything). Optional tiny independent per-bone jitter (≪ latent effect) for texture only.
- "Variation" reads when changes are **coherent and silhouette-level** (whole animal bigger/leaner/longer-legged); "noise" reads when neighbouring bones vary independently (lumpy limbs, asymmetric left/right). Keep left/right mirrored: symmetric pairs should share one random value.
- Magnitude: Planet Zoo-style size spread and biology suggest a few percent to ~±10–15% linear for "same species" feel; beyond that it reads as a different morph. This range is my judgement — no source gave a hard number (see Gaps).
- Determinism: derive per-feature randoms by hashing (seed, feature id) rather than drawing sequentially from one RNG, so adding a new parameter does not reshuffle all existing individuals.

### Gaps
- No developer-primary source found giving concrete per-parameter ranges (e.g. "scale ±8%") for shipped creature games; NMS descriptors unpublished; runevision 2021–2024 procedural creature blog (likely relevant: parametric creatures with high-level parameters) could not be fetched (bot block) — [runevision blog, 2025](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html).
- Typical intraspecific CV for mammal limb lengths (commonly quoted ~4–10%) not confirmed by a fetched source.

## 2. Caricature / exaggeration algorithms for 3D shapes; pitfalls; use in tools

### Takeaway
The standard algorithm is **EDFM — Exaggerate the Difference From the Mean**: `x' = mean + k·(x − mean)`, with k > 1. Likeness is best preserved when each feature's deviation is scaled **relative to its population standard deviation** (Mo et al. 2004), i.e. exaggerate what is *distinctive*, not what is merely large. Known pitfalls: interpenetration of features, excessive deformation at contours, and self-intersection at large factors.

### Cited Findings
- Caricature = point beyond the face on the line from the mean face through the face; effectiveness comes from emphasising deviations from the average. — [Frontiers in VR survey, 2021/2022](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2021.785104/full)
- History: Brennan (1985) interactive 2D EDFM; Blanz & Vetter (1999) EDFM in 3D via PCA morphable model (increase distance to the statistical mean in geometry and texture); Mo et al. (2004): exaggerate features proportionally to their standard deviation to preserve likeness; Sela et al. (2015) gradient-field / curvature-based caricature. — [Frontiers in VR survey, 2021](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2021.785104/full)
- Sela, Aflalo, Kimmel (2015): locally amplify surface area according to Gaussian curvature → natural exaggeration without a mean model. — [CVIU 2015](https://dl.acm.org/doi/10.1016/j.cviu.2015.05.013)
- Harmonic exaggeration (2011): interactively emphasise differences between a shape and a reference template mesh. — [Computers & Graphics, 2011](https://www.sciencedirect.com/science/article/abs/pii/S0097849311000446) (abstract only; full text 403)
- Pitfalls reported: "facial features ... interpenetrate each other", excessive deformation at facial contours, inconsistent quality across subjects; tested parameters "proportions" 0–1.75 and "curvatures" 0–8. — [Frontiers in VR survey, 2021](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2021.785104/full)
- Mesh deformation-based caricature may introduce self-intersections, particularly for large exaggeration factors; factors γ≈0.5–0.75 appear in related work (exact paper unverified). — [CaricatureGS, arXiv 2601.03319, 2026](https://arxiv.org/html/2601.03319)
- Art practice: stylisation pushes proportions beyond realism while keeping internal consistency; exaggerated elements should support the creature's function; proportion conveys character (elongated limbs → agile/predatory). Silhouette test: "If the silhouette reads, it works" (Yona Saura). — [Creative Bloq, 15 tips for creature design](https://www.creativebloq.com/features/15-tips-for-better-creature-design); [PixelSanctuary](https://www.pixelsanctuary.com/tutorials/character-design-silhouette) (popular-art sources, undated/low authority)

### Inferences
- For a bone graph the "mean shape" is naturally the **cross-species mean** of normalised parameters (all species expressed as ratios to body size, e.g. neck length / body length). A species' distinctive traits are the components where `(species − mean)/σ_across_species` is large. Mutant = `species + k·(species − mean)` applied **only** to the top-N distinctive components (or weighted by |z|), k≈1.3–2. This matches Mo et al.'s σ-scaling; picking top-N keeps the mutant "more wolf-like" rather than just bigger.
- Work in **log space for multiplicative parameters** (lengths, radii, size ratios): exaggeration `log x' = log m + k(log x − log m)` never produces negative/zero lengths, whereas linear EDFM can (e.g. a trait below the mean pushed through zero). Angles/orientations need separate handling (slerp-style extrapolation, clamped), not linear extrapolation — consistent with the project's own observation that parts live in different spaces.
- Joint-breaking/self-intersection analogue in a bone graph: exaggerating lengths moves attachment points; if attachments are expressed relative to parent length (as fractions) the graph stays connected by construction. Radius exaggeration can make neighbours interpenetrate — clamp radius ratios between connected bones, or exaggerate lengths more than radii.
- Exaggeration should be applied to the species *template* before per-individual variation, so the mutant is a stable "sub-species" and individuals still vary around it.

### Gaps
- No shipped-game post-mortem found that explicitly uses EDFM for monster/mutant variants; the linkage is by analogy.
- No quantitative threshold where k starts breaking likeness for bodies (only faces studied).

## 3. Soft combination of sizes (means, soft max) and allometry

### Takeaway
The **weighted power (Hölder) mean** `M_p = (Σ wᵢ xᵢ^p / Σ wᵢ)^(1/p)` fits "size moves toward the largest contributing species but not all the way": it is bounded by min/max, idempotent, homogeneous, monotone in each input and in p, equals the geometric mean at p→0 and the max at p→∞. **LogSumExp is NOT suitable** (overshoots the max by up to T·ln n and is not idempotent); **LogMeanExp** is the idempotent variant but is shift- rather than scale-invariant (wrong for sizes unless applied to log-sizes). The designer's "average of native and maximum" is a legitimate but discontinuous-ish special case. Allometry: limb length and diameter scale close to geometric similarity, ~mass^0.35–0.36, so radius should follow size, slightly more than proportionally.

### Cited Findings
- Power mean definition; properties: min ≤ M_p ≤ max; if p < q then M_p ≤ M_q (equality only if all equal); homogeneous `M_p(bx) = b·M_p(x)`; symmetric; p→∞ → max, p→0 → geometric mean, p→−∞ → min. — [Wikipedia: Generalized mean](https://en.wikipedia.org/wiki/Generalized_mean)
- Each mean is reflexive/idempotent: M(a,a)=a; power-mean families tend to the maximum as exponent → ∞. — [arXiv 1504.02371, 2015](https://arxiv.org/pdf/1504.02371)
- LogSumExp: smooth, strictly convex, strictly increasing upper bound of max; `max ≤ LSE ≤ max + (ln n)/t`. — [Berkeley EE127 livebook, 2021](https://inst.eecs.berkeley.edu/~ee127/sp21/livebook/def_lse_fcn.html); [EmergentMind: LogMeanExp](https://www.emergentmind.com/topics/logmeanexp-operator)
- LogMeanExp `L_t(x) = (1/t) log((1/n) Σ e^{t xᵢ})` = LSE − (ln n)/t; interpolates between arithmetic mean (t→0) and max (t→∞) [note: the fetched page stated the limits with t reversed — for this parameterisation, large t gives max]. — [EmergentMind: LogMeanExp](https://www.emergentmind.com/topics/logmeanexp-operator)
- Alexander et al. (1979), shrew→elephant: limb bone lengths and diameters scale close to geometric similarity; length ∝ M^0.35, diameter ∝ M^0.36. — [ResearchGate record, J. Zool. 1979](https://www.researchgate.net/publication/230106352_Allometry_of_the_limb_bones_of_mammals_from_shrews_Sorex_to_elephant_Loxodonta)
- Later work: differential scaling across groups (e.g. Carnivora vs others), and a "universal" mass–proximal limb bone circumference relation across quadrupedal tetrapods (Campione & Evans 2012). — [Bertram & Biewener, differential scaling](https://www.researchgate.net/publication/20806549_Differential_scaling_of_the_long_bones_in_the_terrestrial_carnivora_and_other_mammals); [BMC Biology 2012](https://bmcbiol.biomedcentral.com/articles/10.1186/1741-7007-10-60) (full text not fetched; exponent not verified — from memory ≈ circumference ∝ M^~0.36, treat as unverified)
- Review of theoretical models (geometric, elastic, static stress similarity) for bone allometry. — [Interspecific allometry of bone dimensions: review](https://www.researchgate.net/publication/222679607_Interspecific_allometry_of_bone_dimensions_A_review_of_the_theoretical_models)

### Inferences
- Candidates for hybrid size S from contributors with weights wᵢ (share of body from species i) and native sizes xᵢ (linear size, or mass):
  - Weighted arithmetic mean: linear, never overshoots, but a small share of a big donor moves size only proportionally.
  - Weighted geometric mean (p=0): natural for multiplicative quantities (sizes); symmetric in "×2 / ÷2"; **pulls toward the smaller**, opposite to the brief.
  - Weighted power mean with p≈2–4 on **mass** (or p≈6–12 on linear size, since mass ∝ L³ and M_p on mass equals M_{3p} on length): monotone, bounded by the largest donor, idempotent (pure species keeps its size), homogeneous (unit-free), leans toward the heaviest donor increasingly with p. **Best fit to "moves toward the largest but not all the way".** One knob (p) tunes how "greedy" the heavy donor is.
  - Designer's rule `½(native + max)`: jumps as soon as any gram of moose appears (not continuous in weights: w_moose from 0 to ε doubles the step). Fix: `native + w_eff·(max − native)` with a soft weight — or just use the power mean, which contains this behaviour smoothly.
  - LogSumExp: overshoots max and grows with number of contributors — reject. LogMeanExp on **log-sizes** with weights is equivalent in spirit to power mean (softmax-weighted geometric mean) — acceptable alternative but harder to reason about.
- Worked check (my calculation, not sourced): wolf mass 40 kg, moose 400 kg, moose share 0.3. Arithmetic on mass: 148 kg (linear ×1.54). Power mean p=2 on mass: √(0.7·1600+0.3·160000)=√49120≈222 kg (linear ×1.77). Geometric: 40^0.7·400^0.3≈80 kg (×1.26). Designer's rule on mass: 220 kg (×1.77) regardless of share. So p≈2 on mass at 30% share ≈ designer's rule — a good calibration point.
- Allometry for bones: since observed exponents (~0.35–0.36) are close to 1/3, uniform scaling of all bone lengths and radii by `(M/M₀)^(1/3)` is a good first approximation; for a visibly "heavier" read, scale radius slightly faster than length (e.g. radius ∝ L^1.1 relative to species native) — this is the stylised version of elastic/stress similarity (unverified magnitude, design choice).

### Gaps
- No game source found discussing soft-max of sizes for hybrids; the recommendation is mathematical.
- Exact Campione & Evans exponent not verified (Springer auth redirect).

## 4. Composing variation, exaggeration and size blending in one pipeline

### Takeaway
They compose cleanly if each acts on a **different factor of a multiplicative, log-space decomposition**: `body = size_scale × proportions(shape)`, where size blending sets the scalar size, exaggeration and hybrid blending act on **size-normalised proportions**, and per-individual variation adds a small clamped latent offset last. Applying them in fixed order on separated quantities prevents them fighting.

### Cited Findings
- Homogeneity of power means (`M_p(bx) = b·M_p(x)`) means size blending is unit-free and commutes with uniform scaling. — [Wikipedia: Generalized mean](https://en.wikipedia.org/wiki/Generalized_mean)
- Statistical shape models separate a few dominant global factors (height, mass) from finer shape components, and clamp components to k·σ. — [arXiv 2206.07036, 2022](https://arxiv.org/pdf/2206.07036); [SSM overview](https://www.sciencedirect.com/topics/computer-science/statistical-shape-model)
- EDFM relative to σ preserves identity (Mo 2004). — [Frontiers in VR survey, 2021](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2021.785104/full)
- Geometric similarity of limb bones (~M^1/3) justifies treating size as a global scalar over proportions. — [Alexander et al. 1979](https://www.researchgate.net/publication/230106352_Allometry_of_the_limb_bones_of_mammals_from_shrews_Sorex_to_elephant_Loxodonta)

### Inferences
Proposed order (all in log space for lengths/radii):
1. **Proportions** per species stored size-normalised (ratios to body length).
2. **Mutant exaggeration** on the species proportions: `log r' = log r̄ + k·(log r − log r̄)` on top-N distinctive components (σ-weighted). Produces a mutant template; does not touch size.
3. **Hybrid proportion blend** (whatever the graph-blending scheme is) over templates.
4. **Size**: `S = M_p(native sizes; weights)` on mass, p tunable (≈2); mutants may also get a size multiplier as a separate explicit trait rather than via exaggeration of size (otherwise EDFM on size compounds with the power-mean pull).
5. **Allometric thickness**: radii × extra `(S/S_native)^α`, small α (≈0–0.1), so bigger hybrids read heavier.
6. **Individual variation** last: seed-hashed clamped latent (size ±~5–10%, bulk, limb-length), with symmetric pairs sharing values; applied as multiplicative offsets, so it is size-independent and does not get exaggerated.
- Conflicts to avoid: (i) exaggerating after individual variation amplifies noise into deformity; (ii) exaggerating raw sizes instead of ratios turns every mutant into a giant; (iii) applying linear (not log) blends to ratios breaks idempotence under scaling; (iv) any caching keyed only on species name will ignore all of these per-individual changes — keys must include the parameter hash (project-specific; noted in repo rules).
- Determinism: all randomness from `hash(seed, species, featureId)`; exaggeration factor k and power p are designer knobs, not random.

### Gaps
- No single published pipeline found that combines all three for creatures; composition order is reasoned, not sourced.
- Perceptual thresholds (when does ±x% read as "different individual" vs "different species") not found in literature for animals; would need in-engine shot comparison.
