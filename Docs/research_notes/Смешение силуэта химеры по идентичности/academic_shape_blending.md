# Академические методы смешения/интерполяции 3D-форм животных с разной топологией скелета

Legend: [PROC] = procedural, runs at runtime without per-creature training; [LEARNED] = needs a trained model / dataset.

## 1. Correspondence between skeletons/parts of different topology (part-level vs node-level)

### Takeaway
Graphics literature solves cross-topology correspondence by one of two routes: (a) node-level matching on curve skeletons (combinatorial voting, needs many-to-one handling), or (b) a coarser part/structure graph with explicit one-to-many, split/merge and null correspondences. For creative blending across different topologies, the strongest precedent (Alhashim et al. 2014) works at the PART/structure level, not per node — which matches a design where all species share one list of named body regions.

### Cited Findings
- Au, Tai, Cohen-Or, Zheng, Fu, "Electors voting for fast automatic shape correspondence", Computer Graphics Forum (Eurographics 2010) [PROC, no training]: uses curve-skeleton nodes as shape features in a voting scheme; the matching with most votes wins. Skeleton compactness greatly reduces the combinatorial problem size; works well for shapes with many protrusions (animals). — [PDF](http://visgraph.cse.ust.hk/projects/shape_matching/shape-matching.pdf)
- Alhashim, Xu, Zhuang, Cao, Simari, Zhang, "Topology-varying 3D shape creation via structural blending", ACM TOG 33(4):158, SIGGRAPH 2014 [PROC]: blending is defined on a "spatio-structural graph" of medial curves and sheets that is "structure-oriented, part-aware" and facilitates topology manipulation; split and merge are realized by allowing one-to-many correspondences between source and target; multiple blending paths are sampled in an interactive exploratory tool. — [PDF](https://honghuali.github.io/projects/topo_sig14.pdf), [ACM](https://dl.acm.org/doi/10.1145/2601097.2601102)
- Kalogerakis, Chaudhuri, Koller, Koltun, "A probabilistic model for component-based shape synthesis", ACM TOG 31(4), SIGGRAPH 2012 [LEARNED, but tiny Bayesian net, not deep]: synthesizes new shapes as plausible combinations of components of existing shapes; the model relates component properties to latent "causes of structural variability" and is learned without supervision from a set of COMPATIBLY SEGMENTED shapes (i.e. part labels shared across the set are the prerequisite). — [ACM](https://dl.acm.org/doi/10.1145/2185520.2185551)
- Duncan et al., "Zoomorphic design", ACM TOG / SIGGRAPH 2015 [PROC, optimization]: merges a man-made shape with an animal shape; a graph-kernel technique picks a suitable pair of shapes to merge; merging is a continuous optimization where both shapes deform jointly under an energy combining design factors. — [Project](https://craigyuyu.github.io/home/project_pages/zoomorphic/index.html), [ACM](https://dl.acm.org/doi/10.1145/2766902)
- A survey of the correspondence field exists: "Recent advances in shape correspondence", The Visual Computer (2019/2020). — [Springer](https://link.springer.com/article/10.1007/s00371-019-01760-0)
- "Compact Part-Based Shape Spaces for Dense Correspondences" (arXiv 1311.7535) proposes part-based shape spaces as an intermediary for dense correspondence (title/abstract level only, not read in full). — [arXiv](https://arxiv.org/pdf/1311.7535)

### Inferences
- If every species already carries the same named region list, the hard part of the literature (discovering correspondence) is solved by construction; what remains is mapping each region's internal chain of bones (different node counts per species) — this is exactly the "one-to-many" case of Alhashim 2014, most simply handled by resampling each region's chain by normalized arc length rather than matching nodes.
- Node-level matching (Au 2010) is useful for automatic discovery but brittle for creative blends; a region-level descriptor avoids it entirely.

### Gaps
- Did not retrieve primary sources for functional maps (Ovsjanikov et al.) or co-segmentation papers; they target dense surface correspondence and require meshes of similar genus, so they are likely less relevant for a skeleton-graph game, but this was not verified.

## 2. Interpolating shapes/transformations: ARAP, Alexa's LCT, log/Lie blending, multiplicative blending

### Takeaway
The standard answer in graphics is: decompose each local transform into rotation × stretch, interpolate rotations on SO(3) (quaternion slerp / log map) and stretches/scales separately; linear blending of matrices causes artifacts. Log-Euclidean averaging of positive quantities is the principled way to avoid the "swelling" effect — this directly supports blending lengths/radii multiplicatively (geometric mean = linear blend in log space).

### Cited Findings
- Alexa, Cohen-Or, Levin, "As-Rigid-As-Possible Shape Interpolation", SIGGRAPH 2000 [PROC]: blends interiors rather than boundaries; each local linear map is decomposed into a rotation and a stretch, rotation interpolated as rotation and stretch interpolated linearly, so local volumes are least-distorted along the path. — [PDF](https://www.tau.ac.il/~levin/arap.pdf), [ACM](https://dl.acm.org/doi/10.1145/344779.344859)
- Alexa, "Linear combination of transformations", ACM TOG / SIGGRAPH 2002 [PROC]: defines operations enabling weighted (linear) combinations and interpolation of transformations (via matrix logarithm/exponential), supporting animation through keyframe transforms and PCA over sequences of transformations; compared against matrix-decomposition and quaternion interpolation. — [PDF](http://mesh.brown.edu/DGP/pdfs/Alexa-sg2002.pdf), [ACM](https://dl.acm.org/doi/abs/10.1145/566654.566592)
- Arsigny, Fillard, Pennec, Ayache, "Log-Euclidean metrics for fast and simple calculus on diffusion tensors", Magn. Reson. Med. 2006 [PROC]: after taking matrix logarithms, Riemannian computations become Euclidean; Euclidean averaging of SPD matrices causes "swelling" (determinant of the average exceeds determinants of the inputs), absent in log-Euclidean/Riemannian frameworks where determinants are interpolated monotonically; log-Euclidean gives similar results to affine-invariant Riemannian but is much cheaper. — [PDF](https://www-sop.inria.fr/asclepios/Publications/Arsigny/arsigny_mrm_2006.pdf)
- A log-Euclidean polyaffine framework (Arsigny/Pennec, JMIV) fuses several local rigid/affine transforms smoothly, used in locally affine 3D registration. — [PDF](https://www-sop.inria.fr/asclepios/Publications/Xavier.Pennec/Arsigny_JMIV_polyaffine.pdf)
- Alhashim et al. 2014 interpolate geometry of corresponding parts by LINEAR interpolation of curve/sheet control points, and linearly interpolate contact (attachment) point positions along connecting curves — i.e. purely linear, not log-space. — [PDF](https://honghuali.github.io/projects/topo_sig14.pdf)
- Spore (Hecker et al., SIGGRAPH 2008, "Real-time motion retargeting to highly varied user-created morphologies") records animation in a morphology-independent form so animations adapt to arbitrary skeleton topologies (e.g. two arms one leg vs seven legs). — [Hecker](https://www.chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies)

### Inferences
- For positive scalars (lengths, radii) the 1-D analogue of log-Euclidean is exactly the geometric blend `L = L_base^(1-w) * L_donor^w`; it avoids the scalar analogue of swelling (e.g. aspect ratio a/b blends consistently whether you blend a,b or a/b), and it is scale-covariant — blending ratios gives the same result as blending absolute sizes then renormalizing. This supports the project's belief (multiplicative lengths/radii, rotations on SO(3)) with a well-established precedent.
- Linear control-point blending (Alhashim) is fine for small differences but for w-per-region with very different proportions (snake vs moose neck), log-space is the more defensible choice.

### Gaps
- Did not fetch dual-quaternion blending (Kavan et al.) primary source; it concerns skinning of rigid transforms, less relevant to proportion blending.

## 3. Statistical shape models over animals (SMAL family, dogs, LASSIE, 3D-Fauna): cross-species interpolation, per-part descriptors?

### Takeaway
All major animal shape models (SMAL, SMAL+, BARC/BITE, 3D-Fauna) use ONE fixed template topology/skeleton and a global low-dimensional latent (PCA or learned bank); none of them use compact per-part descriptors, and they cannot express a per-region weight natively. Cross-species interpolation is smooth within quadrupeds but the space cannot represent features outside its training span (pig snout). Part-based models (LASSIE/Hi-LASSIE) do have per-part neural surfaces on a discovered skeleton but are per-class and learned.

### Cited Findings
- Zuffi, Kanazawa, Jacobs, Black, "3D Menagerie: Modeling the 3D shape and pose of animals" (SMAL), CVPR 2017 [LEARNED model; runtime evaluation is cheap linear]: PCA on pose-normalized template-registered meshes from scans of 41 toy figurines (lions, cats, dogs, horses, cows, hippos; families Felidae, Canidae, Equidae, Bovidae, Hippopotamidae); family-specific models are Gaussians over PCA coefficients; fixed 33-joint skeleton with linear blend skinning; shape does not change skeleton structure; fits unseen boar, donkey, sheep, pig, but "characteristic shape properties such as the pig snout cannot be exactly captured". — [arXiv](https://ar5iv.labs.arxiv.org/html/1611.07700), [site](https://smal.is.tue.mpg.de/)
- Rüegg et al., BARC, CVPR 2022 [LEARNED]: modifies the SMAL shape space for dogs and learns a breed-aware latent where breed clusters align with genetic similarity, via a breed-similarity loss. BITE (2023) continues the dog-specific line. — [CVPR PDF](https://openaccess.thecvf.com/content/CVPR2022/papers/Ruegg_BARC_Learning_To_Regress_3D_Dog_Shape_From_Images_by_CVPR_2022_paper.pdf), [survey mention](https://arxiv.org/pdf/2508.16062)
- Zuffi et al., AWOL, ECCV 2024 [LEARNED]: maps CLIP latent space to parametric model parameters (SMAL+ with 145 shape dimensions incl. giraffes, bears, rodents); hypothesis that smooth language→parameter mapping lets interpolation in language produce novel 3D shapes and species not seen in training. — [arXiv](https://arxiv.org/abs/2404.03042), [MPI](https://is.mpg.de/publications/zuffi_eccv2024_awol)
- Li et al., "Learning the 3D Fauna of the Web", CVPR 2024 [LEARNED]: pan-category deformable model for >100 quadruped species; a "Semantic Bank of Skinned Models" auto-discovers a small set of base shapes using DINO features; interpolation between reconstructed instances from different images is shown to be continuous and smooth. — [project](https://kyleleey.github.io/3DFauna/), [arXiv](https://arxiv.org/abs/2401.02400)
- Yao et al., Hi-LASSIE, CVPR 2023 (LASSIE, NeurIPS 2022) [LEARNED per class, from 20–30 images]: discovers a class-specific 3D skeleton (joints + part connectivity), then optimizes shared and per-instance neural part surfaces; part-based representation enables animation and motion retargeting. — [project](https://chhankyao.github.io/hi-lassie/), [CVPR PDF](https://openaccess.thecvf.com/content/CVPR2023/papers/Yao_Hi-LASSIE_High-Fidelity_Articulated_Shape_and_Skeleton_Discovery_From_Sparse_Image_CVPR_2023_paper.pdf)

### Inferences
- These models confirm that a shared-skeleton, shared-template representation makes cross-species blending trivial (linear in latent) — but at the price of fixed topology and global (not per-region) control. A game with per-region weights and species-specific bone counts needs a region-factored representation that these models do not provide.
- The "pig snout" failure is an argument for per-region descriptors: a global PCA cannot localize a feature, while a region-level descriptor can carry it.

### Gaps
- No evidence found of per-region (localized) weights in SMAL-family models; not confirmed whether any follow-up (e.g. SMAL+ in AWOL, VAREN horses) adds localized bases.

## 4. Compact per-part descriptors (length ratios, thickness profiles, cross-section) and reconstruction

### Takeaway
Generalized cylinders (axis curve + radius profile) are the classical compact per-part descriptor for animals, and are still used procedurally (Infinigen's lofted NURBS parts: center curve + deviations). Infinigen is the closest published precedent to the game's needs: tree-structured creature genome, per-part parameters, and "interpolate similar genomes" for variation — procedural, no per-creature training.

### Cited Findings
- Generalized cylinders (a circle of varying radius swept along a 3D curve) naturally approximate animal shapes; skeleton and radii are recoverable from a projected outline; used for sketch-based animal modeling (e.g. "Modeling 3D animals from a side-view sketch", Computers & Graphics 2014). — [ScienceDirect](https://www.sciencedirect.com/science/article/abs/pii/S0097849314001253)
- Raistrick et al., "Infinite Photorealistic Worlds using Procedural Generation" (Infinigen), CVPR 2023 [PROC]: creature parts are NURBS or transpiled node-graphs; NURBS parameters are randomized "under a factorization inspired by lofting, composed of deviations from a center curve"; distribution grounded on 30 hand-modelled heads and bodies; genome is a tree ("limbs don't form closed loops"), nodes hold part parameters, edges specify attachment; 5 classes of creature genomes (carnivores, herbivores, birds, beetles, fish); variation by combining parts at random or interpolating similar genomes. — [arXiv HTML](https://arxiv.org/html/2306.09310)
- Neural Generalized Cylinder, SIGGRAPH Asia 2024 [LEARNED] — controllable shape modeling with generalized-cylinder handles (title-level only). — [ACM](https://dl.acm.org/doi/10.1145/3680528.3687617)
- Spore's creature creator: spine of user-chosen number of segments, each segment's thickness adjustable, parts dragged onto body. — [Bournemouth MSc thesis summary](https://nccastaff.bournemouth.ac.uk/jmacey/MastersProject/MSc22/01/ProceduralCreatureGenerationandAnimationforGames.pdf)

### Inferences
- A per-region descriptor of {length relative to torso, radius profile sampled at fixed normalized positions, cross-section aspect ratio, attachment angles} is a generalized-cylinder descriptor; resampling each region at fixed normalized arc-length stations makes descriptors of species with different bone counts directly comparable, and reconstruction onto the base skeleton's own bone count is re-sampling back. This is an inference, not a published method verified here.
- Infinigen's "interpolate similar genomes" suggests interpolation is only trusted between similar species; blending across very different classes was not claimed.

### Gaps
- Found no academic work specifically on allometric (log-space, power-law) shape blending of creature parts; "allometric shape modeling" searches were not productive within budget.
- Did not verify how Infinigen interpolates (linear on NURBS parameters vs other).

## 5. Hybrid-creature generation and plausibility evaluation

### Takeaway
Recent hybrid-creature work (2024–2026) is almost entirely learned (SDS/diffusion), assembles parts from different animals, and evaluates mostly via CLIP-style text alignment plus user studies; the newest (Muses, 2026) explicitly composes a 3D SKELETON first with graph constraints to harmonize layout and scale — conceptually close to "base skeleton + donor region proportions". Pre-deep-learning procedural work (Alhashim 2014) evaluated with a preliminary user study and comparisons.

### Cited Findings
- Alhashim et al. 2014 [PROC]: tested on 110 man-made models (not animals); compared against part recomposition (Xu et al. 2012) and level-set volumetric blending (Breen & Whitaker 2001); conducted a "preliminary user study"; also shows "unnatural blending results" and discusses remedies. — [PDF](https://honghuali.github.io/projects/topo_sig14.pdf)
- DreamBeast, arXiv Sept 2024 [LEARNED, SDS]: fantastical animals composed of distinct parts (heads, limbs, wings, tails, bodies) via part-aware knowledge transfer from Stable Diffusion 3 into a 3D Part-Affinity implicit representation guiding multi-view diffusion; claims "extensive quantitative and qualitative evaluations" (specific metrics not retrieved). — [arXiv](https://arxiv.org/abs/2409.08271), [project](https://dreambeast3d.github.io/)
- Chirpy3D, arXiv 2025 [LEARNED]: part-aware multi-view diffusion for fine-grained objects (birds); users compose new objects by selecting existing parts from different species, or sample novel parts from a learned part latent space. — [arXiv](https://arxiv.org/pdf/2501.04144)
- Muses, arXiv Jan 2026 (rev. June 2026) [training-free but uses pretrained generative models]: first builds a creatively composed 3D skeleton "with coherent layout and scale through graph-constrained reasoning", then skeleton-guided voxel assembly in a structured latent space integrating regions from different objects; argues prior part-aware optimization / manual assembly / 2D-generation methods give incoherent assets; evaluation specifics not retrieved. — [arXiv](https://arxiv.org/abs/2601.03256), [GitHub](https://github.com/luhexiao/Muses)
- Kalogerakis et al. 2012: plausibility is modeled probabilistically (learned latent causes of structural variability across compatibly segmented shapes). — [ACM](https://dl.acm.org/doi/10.1145/2185520.2185551)

### Inferences
- The consistent lesson across Alhashim 2014, Muses 2026 and Kalogerakis 2012: plausibility of hybrids depends on harmonizing SCALE and ATTACHMENT between parts, not on the per-part shapes themselves. For a runtime procedural system, the analogue is keeping base calibre/size and attachment topology, and moving only dimensionless proportions — consistent with the project's design.
- No paper found evaluates per-region continuous weights w∈[0,1] on animals; this is a gap/novelty.

### Gaps
- Could not retrieve specific evaluation metrics/sample sizes for DreamBeast, Chirpy3D, Muses (abstract pages only).
- Consumer "AI animal hybrid generator" sites exist but are 2D image tools without published methods; excluded as non-academic.
