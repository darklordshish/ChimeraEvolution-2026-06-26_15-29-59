# Skeletal implicit surfaces / SDFs for organic creature bodies, and morphing two bodies into a hybrid

Note on method: several primary PDFs (SCALIS on HAL/EG diglib, Zanni/Fuentes convolution formulae on HAL, the Gradient-Based Blend on ResearchGate/ACM, the 2026 LOD paper on ScienceDirect) were blocked (403 / anti-bot) or too large to fetch. Where only a search snippet was available this is stated. Two PDFs (Galin et al. BlobTree morphing, Cohen-Or et al. distance-field metamorphosis) were downloaded and their text checked directly; quotes below from them are verbatim. A WebFetch summary of the BlobTree paper invented the words "ghosting" and "unexpected disconnection" — they are NOT in the paper; do not cite them to it.

## Q1. Skeletal implicit / convolution surfaces and blending operators that avoid joint bulges and handle varying radii

### Takeaway
Three families exist: (a) summed potential fields around skeleton elements (blobs / convolution / SCALIS) — smooth, naturally blending, but prone to bulges at junctions and to "blobby" loss of thin detail, which SCALIS fixes for varying radii; (b) exact SDF primitives (round cone = capsule with two radii) combined with smooth-minimum — cheap, local ("rigid") blends whose width is set in metres by one parameter k; (c) gradient-based blend operators on compactly supported fields — the research answer to bulges, unwanted blending at distance and topology changes, at a higher per-sample cost. For a CPU-polygonized low-poly body, (b) is the pragmatic default; (a)/(c) matter only if junction bulges become visible.

### Cited Findings
- Classic skeletal ("blobs / soft objects") model: field F(p) = Σ_i f_i(p), each f_i = g_i(d_i(p)) a decreasing potential function g of the distance d_i to skeleton element i; the BlobTree generalises this into a tree whose nodes hold blend (sum), boolean (min/max or Pasko R-functions) and warp operators — [Galin, Leclercq, Akkouche, "Blob-Tree Metamorphosis"](https://perso.liris.cnrs.fr/eric.galin/Articles/1999-blobtree-morphing.pdf)
- Convolution surfaces integrate a smoothing kernel along the skeleton, but "failed to reconstruct prescribed radii and were unable to model large shapes with fine details" — [SCALIS abstract, HAL](https://hal.science/hal-00863523) / [EG diglib](https://diglib.eg.org/items/b8137ea5-3472-43e5-bd3d-a547fb602e72)
- SCALIS (SCALe-invariant Integral Surfaces, Zanni, Bernhardt, Quiblier, Cani, Eurographics 2013): like convolution surfaces they blend smoothly when skeleton parts are added, but "blending properties are scale-invariant"; the model provides "accurate thickness control and achieves consistent blends between large and small features without any smoothing of sharp details"; defined from skeleton graphs with radii prescribed at vertices — [HAL](https://hal.science/hal-00863523); [ResearchGate](https://www.researchgate.net/publication/259547783_SCALe-invariant_Integral_Surfaces)
- SCALIS incorporates a scale function λ into the convolution integral; closed-form convolution field formulae exist for varying-radius skeletons made of line segments and arcs of circles (Fuentes Suárez & Hubert) — [Springer chapter](https://link.springer.com/chapter/10.1007/978-3-319-77066-6_3); [HAL doc](https://hal.science/hal-01534159v2/document) (snippet only; PDF blocked)
- Bulge condition for convolution: for 2D/3D skeletons "the surface will be bulge-free if skeletal elements are sufficiently large with respect to the convolution kernel" — search snippet referencing Sherstyuk's kernel comparison, [Semantic Scholar](https://www.semanticscholar.org/paper/Kernel-functions-in-convolution-surfaces:-a-Sherstyuk/77fb45022ee03b960818529512b7aefb6a4e8eda) (snippet only)
- Fast ray tracing of SCALIS exists (Inria), i.e. the model is used at interactive rates on GPU — [HAL hal-03169283](https://inria.hal.science/hal-03169283) (title only)
- Gradient-based implicit blend (Gourmel, Barthe, Cani, Wyvill, Bernhardt, Paulin, Grasberger, ACM TOG 2013): a family of binary composition operators that solve four problems — "suppressing bulges when two shapes merge, avoiding unwanted blending at a distance, ensuring that the resulting shape keeps the topology of the union, and enabling sharp details to be added without being blown up"; key idea: combine fields "not only based on their values, but also on their gradients" (blend amount depends on the angle between the two gradients); C∞ operators evaluated on the GPU — [ACM](https://dl.acm.org/doi/10.1145/2451236.2451238); [ResearchGate](https://www.researchgate.net/publication/236595623_A_Gradient-Based_Implicit_Blend) (abstract/snippet only; full PDF not retrieved). Follow-up: sketch-controlled blending — [ACM TOG 2017](https://dl.acm.org/doi/10.1145/3130800.3130825)
- Exact varying-radius primitive for a "bone" (two radii): Quilez's round cone, exact SDF. Arbitrary-endpoint version:
  ```
  float sdRoundCone(vec3 p, vec3 a, vec3 b, float r1, float r2){
    vec3 ba=b-a; float l2=dot(ba,ba); float rr=r1-r2; float a2=l2-rr*rr; float il2=1.0/l2;
    vec3 pa=p-a; float y=dot(pa,ba); float z=y-l2;
    float x2=dot2(pa*l2-ba*y); float y2=y*y*l2; float z2=z*z*l2;
    float k=sign(rr)*rr*rr*x2;
    if(sign(z)*a2*z2>k) return sqrt(x2+z2)*il2-r2;
    if(sign(y)*a2*y2<k) return sqrt(x2+y2)*il2-r1;
    return (sqrt(x2*a2*il2)+y*rr)*il2-r1; }
  ```
  Capsule: `h=clamp(dot(pa,ba)/dot(ba,ba),0,1); return length(pa-ba*h)-r;`. Elongation operator `q = p - clamp(p,-h,h)` is exact for 1D elongation, has a zero-distance core in 2D/3D — [Quilez, distance functions](https://iquilezles.org/articles/distfunctions/)
- Smooth minimum formulas, normalised so k = blend thickness in world units — [Quilez, smin](https://iquilezles.org/articles/smin/):
  - quadratic: `k*=4; h=max(k-|a-b|,0)/k; return min(a,b) - h*h*k/4;`
  - cubic: `k*=6; h=max(k-|a-b|,0)/k; return min(a,b) - h*h*h*k/6;`
  - exponential: `r=exp2(-a/k)+exp2(-b/k); return -k*log2(r);`
  - root: `k*=2; x=b-a; return 0.5*(a+b-sqrt(x*x+k*k));`
  - circular: `k*=1/(1-sqrt(0.5)); h=max(k-|a-b|,0)/k; return min(a,b) - k*0.5*(1+h-sqrt(1-h*(h-2)));`
  - Quadratic/cubic/quartic/circular ("CD family") are **rigid**: they leave shapes unchanged outside the blend zone; exponential, sigmoid, root distort shapes everywhere. Exponential and circular-geometric are **associative** (order-independent); quadratic/cubic/quartic are not. Quadratic and cubic also return a blend factor usable to mix materials/attributes.
- Smooth union is a bound, not an exact SDF — [Quilez, distance functions](https://iquilezles.org/articles/distfunctions/). Smooth union adds material where surfaces are nearly equidistant (the "bulge"/fillet), by subtracting a (quadratic) offset — search snippet on [Quilez smin](https://iquilezles.org/articles/smin/)
- Anisotropic convolution surfaces exist as a research direction (non-circular cross-sections) — [Computers & Graphics 2019](https://www.sciencedirect.com/science/article/abs/pii/S0097849319300792) (title only)

### Inferences
- Summed-field (blob/convolution) bodies bulge where many bones meet (hip, shoulder, neck base) because contributions add; smin-of-SDF bodies instead add a fillet of controlled width k — the fillet is itself a "bulge", but bounded and local, and k in metres can be tied to the bone radius (e.g. k ≈ 0.3–0.5 × smaller radius) so thin parts don't get swallowed.
- Non-associative smin (quadratic/cubic) makes the result depend on the order bones are folded; for a body rebuilt from a graph this is acceptable if the fold order is fixed by the graph (deterministic), but reordering bones between the base and the hybrid would change joints. Associative exponential smin avoids that but is non-rigid (shrinks/fattens everything slightly).
- Cross-section aspect (elliptical bones) is not exact with round-cone; the usual cheap approach is to scale the local coordinates perpendicular to the bone before evaluating (gives a bound, not exact distance) — fine for polygonization, which only needs the sign and a reasonable gradient near the zero set.
- Faceted look: all these operators are smooth; the crispness of the silhouette comes from the grid and flat shading, not from the field. Gradient-based blend's "sharp details not blown up" is the only operator here that explicitly helps keep small sharp features.

### Gaps
- Exact SCALIS integral formula and kernel exponents could not be retrieved (HAL/EG pages returned 403 / anti-bot).
- Exact form of the gradient-based operator and its per-sample cost vs smin were not retrieved (PDF >10 MB, others 403).
- No source found comparing smin vs convolution specifically for quadruped joints.

## Q2. Blending two SDFs directly (lerp of fields, variational interpolation) vs morphing skeleton parameters and rebuilding

### Takeaway
Direct field interpolation F_t = (1−t)F_A + tF_B is topology-agnostic but, when the corresponding parts are not spatially aligned, parts "unexpectedly disappear and reappear" (ghosting / tearing); fixing it requires a correspondence-driven warp. Skeleton-parameter morphing (matched bones, interpolate positions/radii, grow unmatched bones from zero) is the established approach for skeletal implicit models and keeps the shape coherent; its weak point is the correspondence itself.

### Cited Findings
- Cohen-Or, Levin, Solomovici (ACM TOG 1998): distance-field interpolation (DFI) "works well provided that 'corresponding parts' of the two objects are properly aligned. Otherwise, some parts may unexpectedly disappear and reappear." Their fix: warp-guided DFI, warp controlled by user anchor points, decomposed into rigid rotation + elastic part; linear rotation warp W_t = R_t u preferred over (1−t)I + tR u because it is an isometry for every t. Payne & Toga's cross-dissolve of distance values without warp/correspondence "often yields poor results". Conclusion notes that the user-guided warp is "the weak part of the technique" and automatic alignment of arbitrary objects is "a major challenge". Counter-example where no-warp DFI works: a rod and the same rod with its middle third removed — [Cohen-Or et al., PDF](https://www.cs.tau.ac.il/~dcor/online_papers/papers/tog98.pdf); [ACM](https://dl.acm.org/doi/10.1145/274363.274366)
- Pasko proposed linear interpolation of implicit functions for morphing; Wyvill morphed point-skeleton blobs by pairing elements (cellular/hierarchical matching), "creating null components whenever necessary" and interpolating the parameters of paired primitives, "However the visual aspect of the morphing sequences are often poor" — [Galin et al.](https://perso.liris.cnrs.fr/eric.galin/Articles/1999-blobtree-morphing.pdf)
- Galin et al. (BlobTree metamorphosis): a user-provided graph of correspondence between primitives; unmatched elements create "phantom elements"; non-bijective matches are split into sub-components to get a bijection; a generic intermediate BlobTree is instantiated over time; the time-varying skeleton is a linear interpolation via Minkowski sums, which "implicitly generates a trajectory path for each time varying skeletal element, which often results in a loss of shape coherence"; hence animator control of trajectories and speed. Implicit-surface techniques avoid a fixed sampling grid and "directly compute the metamorphosis by interpolating the parameters of the functions" — [Galin et al.](https://perso.liris.cnrs.fr/eric.galin/Articles/1999-blobtree-morphing.pdf)
- Boundary-mesh morphing requires source and target to share topology (genus); volumetric/implicit methods do not — [Galin et al.](https://perso.liris.cnrs.fr/eric.galin/Articles/1999-blobtree-morphing.pdf)
- Turk & O'Brien (SIGGRAPH 99), variational implicit shape transformation: treat an N-D morph as scattered-data interpolation in N+1 dimensions — boundary (zero) and interior (positive, along normals) constraints of each shape on two parallel slices, solved with a thin-plate-like variational (RBF) interpolant; transitions "appear smooth and natural, even between objects of differing topologies" — [arXiv 2303.02937](https://arxiv.org/abs/2303.02937); [ACM](https://dl.acm.org/doi/10.1145/1198555.1198639)
- Skeleton-guided distance-field metamorphosis is a later line of work (skeleton used to drive DFI) — [Graphical Models 2016](https://www.sciencedirect.com/science/article/abs/pii/S1524070316300017); [skeleton-driven 2D DFI with intrinsic parameters](https://www.sciencedirect.com/science/article/abs/pii/S1524070303001012) (titles/abstract only)

### Inferences
- For a hybrid body that keeps the base skeleton, the correspondence problem is already solved by construction (same bone graph, same bone names) — so skeleton-parameter morphing has no downside here and field lerp has no upside. Field lerp would still ghost wherever the donor's part lies elsewhere (e.g. a longer neck puts the head in a different place: lerp gives two faint heads / a gap, per Cohen-Or's "disappear and reappear").
- Variational (RBF) interpolation needs an n×n solve over surface constraints — far too heavy and uncontrollable for per-region runtime hybrids; relevant only as an offline tool.
- "Null components / phantom elements" (grow from zero radius) is the standard answer for a donor bone that the base lacks (e.g. horns): interpolate radius from 0.

### Gaps
- No source quantifying cost of Turk & O'Brien at game scale; original PDF (Berkeley host) failed TLS.

## Q3. Per-region skeleton-parameter morphing with smooth transitions (weight falloff, no seams, consistent joints)

### Takeaway
The literature supports interpolating matched primitive parameters with user-controlled correspondence and trajectories (BlobTree morphing); per-region weights are a direct extension. Seam-free transitions come for free if the weight is a smooth function carried by the bones (not by space) and joints are shared endpoints: the field is re-evaluated, so any continuous parameter change yields a continuous surface.

### Cited Findings
- Correspondence graph with hierarchical control "matching elements or whole sub-trees" and per-node speed/trajectory control gives the animator local control over the transformation — [Galin et al.](https://perso.liris.cnrs.fr/eric.galin/Articles/1999-blobtree-morphing.pdf)
- Linear interpolation of positions via Minkowski sums loses shape coherence; Cohen-Or recommend decomposing into rigid rotation (interpolated as rotation) + elastic part — [Galin et al.](https://perso.liris.cnrs.fr/eric.galin/Articles/1999-blobtree-morphing.pdf); [Cohen-Or et al.](https://www.cs.tau.ac.il/~dcor/online_papers/papers/tog98.pdf)
- smin's blend factor can carry attributes (colour/material) across the fillet — [Quilez smin](https://iquilezles.org/articles/smin/)

### Inferences (engineering synthesis, not from a single source)
- Parametrise each bone by (length, r_start, r_end, aspect, blend k, local orientation). Interpolate lengths and radii multiplicatively (log-space: r = r_A^(1−w) r_B^w) so ratios blend evenly; interpolate orientations as rotations (slerp), not component-wise — consistent with Cohen-Or's rigid/elastic split.
- Region weight w_bone: assign donor weight per region (head, neck, forelimbs...) and diffuse it along the bone graph (e.g. average with neighbours over 1–2 hops, or a falloff by graph distance) so the transition spans a bone rather than happening at one joint.
- Joint consistency: define the child's start radius = parent's end radius after blending (take one value at each graph node, blend node radii, not per-bone ends) — then a region boundary cannot open a step in thickness; bone endpoints follow from blended lengths along the (fixed) base topology, so joints stay connected.
- Blend radius k should scale with the blended local radii, otherwise a thinner donor limb gets swallowed by a fillet sized for the base.
- Because the field is a continuous function of parameters, surface change is continuous; any visible "jump" comes from polygonization (Q4), not from the morph.

### Gaps
- No published source found specifically on region-weighted partial hybrids of two skeletal implicit creatures (the closest is correspondence-graph-controlled BlobTree morphing).

## Q4. Runtime polygonization for a faceted low-poly look: algorithm, stability, cost, Unity implementations

### Takeaway
On a fixed global grid, Surface Nets (one vertex per cell, placed from edge crossings) is simplest, SIMD/Burst-friendly and gives the most regular faceted mesh; dual contouring keeps sharp features via Hermite data (gradients) but risks self-intersections/non-manifold output; marching cubes gives many thin slivers and no sharp features. Burst Surface Nets meshes a 32³ block in ~0.1–0.4 ms single-threaded. Popping under small parameter changes is inherent to grid extraction (topology of a cell flips when a corner sign flips); a fixed world-aligned grid limits it to the cells actually crossing the change.

### Cited Findings
- Surface Nets places a vertex inside each surface-crossing voxel (naive: centre / average of edge crossings) and is smoother than marching cubes; dual contouring keeps a dual vertex inside the voxel positioned using Hermite data (surface gradients) to recover sharp features; dual methods "often generate non-manifold surfaces with many self-intersections" because vertex placement is less restricted; marching cubes cannot recover sharp features without extensions; Surface Nets "more easily SIMD-optimized" — search synthesis over [GameDev.net thread](https://gamedev.net/forums/topic/719387-surface-nets-lod-chunk-structure/), [Wikipedia Isosurface](https://en.wikipedia.org/wiki/Isosurface), [Unreal forum](https://forums.unrealengine.com/t/best-approach-to-visualize-marching-cubes-surface-nets-dual-contouring/1904425) (snippets; secondary sources)
- fastNaiveSurfaceNets (Unity, Burst + SIMD, MIT): ~0.1–0.4 ms per 32³ chunk single-threaded on 1st-gen Ryzen; processes 32 cubes at once via sign-bit corner masks; no duplicate vertices; normals from SDF or from triangles; uses the advanced Mesh API; chunk must be 32 voxels in one dimension; Unity 2020.3; needs SSE4.1 — [GitHub bigos91/fastNaiveSurfaceNets](https://github.com/bigos91/fastNaiveSurfaceNets)
- IsoMesh (Unity, MIT, compute shaders): SDF primitives (sphere, rounded cuboid, torus, box frame), mesh→SDF baking, elongation op, raymarched preview, extraction via surface nets **or** dual contouring with vertex refinement by binary search or gradient descent, normals with angle-tolerance splitting; tested Unity 2021.2 — [GitHub EmmetOT/IsoMesh](https://github.com/EmmetOT/IsoMesh)
- Surface Nets on Unity compute shaders — [GitHub RedaMazoz/Surface-Nets](https://github.com/RedaMazoz/Surface-Nets); topic list — [GitHub surface-nets](https://github.com/topics/surface-nets)
- Temporal coherence of extracted meshes is an active research problem (e.g. spacetime-octree extraction for temporally smooth meshes) — [arXiv 2509.13306](https://arxiv.org/html/2509.13306v1)
- Media Molecule's Dreams tried many SDF renderers before a splat-based ("BubbleBath") engine — Alex Evans, "Learning from Failure" (SIGGRAPH/Umbra Ignite 2015) — [Media Molecule blog](https://www.mediamolecule.com/blog/article/siggraph_2015); [Game Developer](https://www.gamedeveloper.com/design/how-media-molecule-designed-a-fun-and-robust-toolset-for-i-dreams-i-)
- Claybook (Second Order): SDF modelling, GPU clay/fluid sim, ray-traced SDF rendering on UE4, 60 Hz on Xbox One; Aaltonen argues an optimised SDF ray tracer beat triangles for that game — [GDC 2018 slides](https://media.gdcvault.com/gdc2018/presentations/Aaltonen_Sebastian_GPU_Based_Clay.pdf); [GDC Vault](https://www.gdcvault.com/play/1025316/Advanced-Graphics-Techniques-Tutorial-GPU)

### Inferences
- Cost estimate for a creature: a 1–2 m body at 4 cm cells is ~25–50 cells per axis, i.e. about one to four 32³ blocks → Surface Nets extraction well under ~2 ms on one core (from the fastNaiveSurfaceNets figure). The dominant cost is field evaluation: cells × bones. With ~15–40 bones, cull bones per cell by bounding boxes (only bones within max radius + k affect a cell) to keep it linear-ish.
- Crisp faceted silhouette: Surface Nets + flat shading (per-triangle normals, unshared vertices) gives a regular, even facet size — the "crisp low-poly" look. Dual contouring gives sharper creases but irregular facets and possible self-intersections. Marching cubes gives slivers that look noisy when flat-shaded. Raymarched SDFs (Claybook/IsoMesh preview) give smooth, non-faceted rendering — wrong look.
- Popping: with a fixed world grid (not a grid scaled to the body's bounding box), a small parameter change moves only vertices near the change, and topology flips only where a corner changes sign. Keeping the grid phase constant is what prevents global re-faceting; changing bounds-relative grids re-facet everything. Placing Surface Net vertices from interpolated edge crossings (rather than cell centres) makes vertex motion continuous between sign flips.
- Runtime rebuild only on graft/change (not per frame) makes a CPU Burst job adequate; a compute-shader path (IsoMesh-style) adds GPU→CPU readback if colliders/CPU data are needed.

### Gaps
- No benchmark found for field evaluation cost with capsule-graph SDFs at this size; estimate above is inferred.
- No source quantifying popping for Surface Nets vs dual contouring under parameter animation.

## Q5. Commercial / open-source tools usable in Unity for SDF-based bodies

### Takeaway
Open-source Unity options found: IsoMesh (compute, SN/DC, MIT), fastNaiveSurfaceNets (Burst CPU, MIT), Surface-Nets compute sample; none provides skeletal/round-cone creature bodies or hybrid morphing out of the box — those operators (round cone, smin) are a few lines from Quilez. Dreams and Claybook are proprietary references, both render SDFs non-polygonally.

### Cited Findings
- IsoMesh — [GitHub](https://github.com/EmmetOT/IsoMesh); fastNaiveSurfaceNets — [GitHub](https://github.com/bigos91/fastNaiveSurfaceNets); Surface-Nets compute — [GitHub](https://github.com/RedaMazoz/Surface-Nets); Unity Mesh API examples for fast mesh upload — [GitHub Unity-Technologies/MeshApiExamples](https://github.com/Unity-Technologies/MeshApiExamples)
- glsl-smooth-min / sdf-csg small libraries for smin/CSG — [glsl-smooth-min](https://github.com/glslify/glsl-smooth-min); [sdf-csg](https://github.com/irev-dev/sdf-csg)

### Inferences
- For an existing in-house bone-graph field + fixed-grid mesher, these libraries are mainly reference implementations (Burst SIMD corner-mask trick, Mesh API upload) rather than drop-ins.

### Gaps
- Commercial Unity Asset Store SDF-modelling packages (e.g. Clayxels, MudBun) were not researched within budget; their current status/licensing is unverified.
