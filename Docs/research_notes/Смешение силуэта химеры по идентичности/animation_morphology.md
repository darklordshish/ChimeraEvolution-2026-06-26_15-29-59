# Animating bodies whose proportions change at runtime (retargeting, rigging, procedural locomotion, Unity tooling)

Scope note: this covers keeping a skinned creature animatable when bone lengths/radii change toward another animal and parts are grafted on. The main primary source is the Spore SIGGRAPH 2008 paper (Hecker et al.). I read its full text, and it carries most of the detail below. For the other sources I have only search snippets or landing pages, and those claims are marked as such.

## 1. How Spore (and similar systems) animated arbitrary bodies; lessons

### Takeaway
Spore did not retarget clips bone-to-bone. It stored animation as semantic pose goals ("the grasper(s) in front", "move toward the ground", "scale by limb length") in a morphology-independent space. At runtime it specialized them onto the actual body, added synthesized gait, solved everything with a simple tunable particle/length-constraint IK solver, and put passive "jiggle" on the leftovers. The lesson is to put semantics (tags or capabilities on bones) in the data and let IK and gait synthesis absorb the geometry.

### Cited Findings
- Paper: Hecker, Raabe, Enslow, DeWeese, Maynard, van Prooijen, "Real-time Motion Retargeting to Highly Varied User-Created Morphologies", SIGGRAPH 2008. Animations are recorded "in a morphology-independent form" and specialized at runtime into "pose goals that are supplied to a robust and efficient inverse kinematics solver." — [Paper PDF (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf); [project page](https://www.chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies)
- Body representation: a character is 20–80 "bodies" (bones plus parts) forming a DAG with a serial spine chain at the root. The root is chosen heuristically as the spine body with the most incident legs. Bodies carry tags called capabilities ("caps": grasper, mouth, foot, spine…). Early prototypes allowed general graphs with loops, but testing showed the complexity hindered players. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Standardized per-part deforms (mouth open/close, hand open, ear droop) exist on every part of a type. They stay opaque to the animation system, so any mouth responds to "open". — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Selection by semantic query instead of bone name. Queries filter by cap type, spatial constraints (Front/Center/Back, Left/Right, Top/Bottom; relative to the whole body or to the set of same-cap bodies), extent (FrontMost…), and a limb modifier that walks up to the nearest spine segment (an approximation of shoulder/hip). Game AI code uses the same queries. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- "Movement modes" define the coordinate frame a motion is recorded in, which is an invertible generalize/specialize pair G/S:
  - Absolute
  - Rest-relative: a delta from the rest pose
  - Scale: CreatureSize scales by the body bounding box. LimbLength scales by limb length, meaning the path length from the body to the nearest spine segment, so long limbs make big motions.
  - Ground-relative: vertical axis normalized so 0 = rest height and 1 = ground, so "pound the ground" hits the ground for any leg length.
  - Secondary-relative: normalized along the vector to another body or an external target ("hand to mouth", "reach fruit").
  - Lookat with soft limits.
  
  Every mode keeps the rest pose as its origin. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Binding: the first time an animation plays on a character it evaluates "branch predicates" such as UprightSpine, HasGraspers and HasFeet. These pick among alternative authored animations, and branching is described as "a solution of last resort". "Variants" enumerate which body plays the channel (e.g., which of 4 graspers) and the game chooses at runtime. Sagittal mirroring is automatic. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Gait synthesis: a leg is a path from a foot-cap leaf to a spine segment (the hip). Legs are clustered into groups of roughly equal length, and the length ratios are approximated by small rationals to set the relative cycle frequency per group. Each foot gets a duty factor (the fraction of the cycle on the ground) and a step-trigger phase offset. The hips translate and rotate with the feet. The swing-foot arc is authored in a normalized space and scaled by leg length. Animators author speed→gait-parameter tables for 1–6 feet, and more feet are generated procedurally. Different leg groups can run different gaits at once (short legs running while long legs trot). Footless bodies either float or crawl, with spine bodies converted into pseudo-feet for an inch-worm gait. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- IK solver design:
  - It is a "Particle IK": joints are 3-DOF particles with 1-DOF length constraints, solved by Jakobsen-style iterative length correction.
  - It runs in two phases. The spine is solved first, with each limb reduced to a single chord constraint that may compress to 10%. Then the limbs are solved with the spine fixed.
  - Constraints are soft: they may stretch or compress, within 10%/120% bounds, for a more organic feel.
  - The spine uses particles only at branch points, with interior vertebrae reconstructed along quintic Hermite splines (cubics fit poorly).
  - An anti-buckling heuristic blends toward the rest pose.
  - An "aim preconditioner" rotates and scales the limb toward the goal before the iterations. It favors shoulder/hip rotation and gives more natural bends.
  - The authors tried CCD, Jacobian and constrained-dynamics solvers and found them "slower and less amenable to tuning".
  
  — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Goal delegation: a mouth on the spine delegates its goal to the parent spine, so the spine bends instead of the mouth pivoting at its joint. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- "Jiggles": sub-trees with no IK goals get a highly damped pseudo-physics that is purely passive and never feeds back. The earlier "Wiggles" set IK goals and degraded the animators' work. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Results:
  - About 0.2 ms per frame for a 25-body character on a 1.7 GHz Pentium-M, with about 35% of that in IK.
  - About a 90% pass rate on a stochastic validation grid of hundreds of uploaded creatures × about 1000 animations.
  - Limitations: no volume or self-collision awareness ("hug", "rub chin" impossible), IK singularities, and anti-buckling looks "heavy-handed".
  
  — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Hecker's own summary of the lesson is that having humans tell the system what matters (semantic markup) was "immensely powerful, efficient, and robust" compared with discovering semantics algorithmically. He also notes that proportion sliders (height, weight, body shape) in ordinary games are the same problem at smaller scale. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Spore liner notes: procedural systems were required to scale, while pure data-driven effects needed creature-aware logic in limb-relative coordinates. (Summary from a fetch; secondary detail.) — [Hecker, "My Liner Notes for Spore" (2008–09)](https://chrishecker.com/My_Liner_Notes_for_Spore)
- Rain World (GDC 2016 Animation Bootcamp, Jakobsson & Therrien): creatures are point masses connected at fixed distances (a constraint frame) with a "paper doll" of parts drawn over them. There are no animation clips; limbs are defined and the creature AI decides how to use them. (Summary from a news article snippet; the talk itself was not fetched.) — [Game Developer (2016)](https://www.gamedeveloper.com/art/video-animating-i-rain-world-i-and-its-many-squishy-stretchy-creatures); [GDC Vault (2016)](https://www.gdcvault.com/play/1023475/Animation-Bootcamp-Rainworld-Animation)
- David Rosen, "An Indie Approach to Procedural Animation" (GDC 2014): interactive, fluid animation from very few keyframes plus procedural techniques (Overgrowth, Receiver). I found only the abstract; the talk content was not verified. — [GDC Vault (2014)](https://www.gdcvault.com/play/1020583/Animation-Bootcamp-An-Indie-Approach); [Wolfire blog (2014)](https://www.wolfire.com/blog/2014/05/GDC-2014-Procedural-Animation-Video/)

### Inferences
- The Spore pattern maps directly onto a bone graph with tagged roles. Selecting by role ("front feet", "jaw", "tail tip") and recording motion as rest-relative, limb-length-scaled or ground-relative deltas makes one authored attack or idle work on a stretched or grafted body without re-authoring.
- Spore's leg grouping by length with rational frequency ratios is a ready answer for a biped with long digitigrade hind legs plus short grafted forelimbs: two leg groups with a 2:1 or 3:2 cadence.
- Since the target here has only 5 base species with small bone graphs, a lighter approach may be enough: per-species authored clips in normalized/rest-relative form, plus IK for feet and targets. Spore's full G/S machinery is only needed for open-ended grafts. (Judgement, unverified.)
- The Spore team found simple, tunable solvers beat mathematically fancier ones in production. This argues for FABRIK/two-bone/particle-style solvers over Jacobian solvers.

### Gaps
- I did not obtain transcripts of Rosen 2014 or Rain World 2016. The specific techniques in them (e.g., Rosen's spring-driven interpolation and stride-based walk cycles) are known to me only from memory, not from verified sources.
- I found no source describing how Spore rebuilt the skinned mesh when the torso was sculpted. Its body is a "clay-like torso" with deformable parts ([Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf) cites Willmott et al. 2007 for the editor), and those skinning details are in the Willmott paper, which I did not fetch.

## 2. Changing bone lengths at runtime: bone scaling vs regenerating mesh plus bind poses; artifacts; best practices for proportion sliders

### Takeaway
Scaling bone transforms is cheap but has two problems. Non-uniform scale propagates shear to children: rotated children skew and rigid detail meshes squash. It also scales thickness and length together unless it is carefully compensated. Blend shapes change shape without touching the skeleton. For large, data-driven proportion changes the robust option is to rebuild the mesh and bind poses from the new rest skeleton, which suits an implicit/generated skin, and to keep every bone transform at uniform scale.

### Cited Findings
- Unity: each bindpose matrix is the inverse of the bone's transform in its base (bind) state. The documented example builds the mesh, bindposes and `SkinnedMeshRenderer.bones` together. — [Unity Scripting API: Mesh.bindposes (current)](https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html)
- `SkinnedMeshRenderer.bones` must match `sharedMesh` strictly, in the same order. — [Unity Discussions (c. 2021)](https://discussions.unity.com/t/skinnedmeshrendererconversion-bindposes-and-reparenting/862316); [Unity API: SkinnedMeshRenderer.bones](https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer-bones.html)
- Composing a parent with non-uniform scale and a rotated child produces shear/skew. Maya's "Segment Scale Compensate" exists for this reason, and Unity users report that Maya squash-and-stretch rigs relying on it do not behave the same after export. — [Polycount (c. 2020)](https://polycount.com/discussion/220323/non-compensated-scale-on-a-child-joint-makes-its-rotate-wrong-if-parent-joint-has-a-non-uniform-scal); [Unity Discussions (c. 2017)](https://discussions.unity.com/t/segment-scale-compensate-not-working/670493)
- Non-uniform bone scale also breaks normals unless the inverse-transpose is used (an engine issue discussing an "accurate affine-normal path"). This is a secondary source from a small engine. — [neotolis-engine issue #529 (c. 2025–26)](https://github.com/d954mas/neotolis-engine/issues/529)
- Customization practice (secondary sources): sliders on bone `localScale` with values near 1 are used for body proportions, while blend shapes ("morphs") change body type without animation issues. — [Minions Art (c. 2018)](https://www.patreon.com/posts/22650450); [Skyrim "Enhanced Character Edit" mod](https://www.nexusmods.com/skyrim/mods/57440)
- Linear blend skinning collapses at twisting joints ("candy-wrapper"). Dual-quaternion skinning avoids this, but its patent literature adds "scale non-compensating joints" to handle scale. — [Velocity Skinning, arXiv (2021)](https://arxiv.org/pdf/2104.04934); [US patent 9613456 (2017)](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/9613456)

### Inferences
- Bones should carry only translation and rotation, with uniform scale 1. Length changes should be expressed as child-joint offsets (moving the child joint along the bone) and thickness as mesh radius. With a generated skin the length change then means: move the joints, regenerate the skin around the new rest skeleton, recompute bindposes as `bone.worldToLocalMatrix * root.localToWorldMatrix` at rest, and reassign mesh, bones and bindposes together. The project rules already match this: the "non-uniform parent scale flattens everything inside" gotcha and "hang things only on an empty node with scale 1".
- Rigid detail parts (claws, quills, hooves) should hang from uniformly scaled attachment nodes at the bone end or at a parametric position along the bone (t in 0..1 plus an offset in bone radii). When the bone lengthens, the node moves and the part stays rigid. Scaling the part by its body's calibre should be a separate uniform scale on its own node.
- The mesh should be regenerated only when proportions change (graft events), not per frame. Per-frame animation stays ordinary skinning on fixed bindposes. Morphing proportions over time (a gradual transformation) could either be baked into several bake steps or use blend shapes between old and new skin if the vertex topology is kept identical. A lattice with a fixed vertex count, which this project already uses, makes blend shapes between two generated skins feasible. (Inference.)
- Radii change: when radius is baked into the generated skin there is no need for scale on the bone at all.

### Gaps
- I found no authoritative postmortem (GDC or paper) comparing slider approaches in a shipped AAA character creator in numbers. The best-practice claims here come from forums and secondary sources.
- The exact cost of rebuilding a SkinnedMeshRenderer mesh plus bindposes at runtime in Unity 6 was not measured or found. It is expected to be milliseconds for meshes of a few thousand triangles, but that is unverified.

## 3. Procedural locomotion/IK for quadrupeds and bipeds with varying leg lengths; Unity packages and assets

### Takeaway
Stride and cadence should scale with leg length, via the Froude number, so short-legged and long-legged hybrids pick plausible gaits and step rates. Feet are placed by per-leg IK (two-bone or FABRIK) against raycast ground, with body height and pitch following the average foot height. In Unity the free baseline is the Animation Rigging package (Burst-compiled constraint jobs). Final IK (paid) adds ready grounders for quadrupeds and arbitrary legged creatures.

### Cited Findings
- Froude number Fr = v²/(gL) with L = leg length. Mammals of different sizes switch walk→trot at Fr ≈ 0.3–0.5 and trot→gallop at Fr ≈ 2–3. In horses the walk–trot transition happens at about Fr 0.35 even though absolute speed ranges 1.6–2.3 m/s with size. Humans switch walk→run at about Fr 0.5. — [Transition from walking to running, Wikipedia](https://en.wikipedia.org/wiki/Transition_from_walking_to_running); [Griffin et al., J Exp Biol (2004)](https://pubmed.ncbi.nlm.nih.gov/15531642/)
- Spore precedent: per leg group, duty factor plus step-trigger phase, a swing arc normalized and scaled by leg length, hips following the feet, and speed-interpolated gait parameters. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Unity Animation Rigging:
  - It is a package of constraints for procedural motion (IK, aim, deformation rigs), built on the C# Animation Jobs API.
  - Constraints are Burst-compiled, and the package depends on com.unity.burst.
  - RigBuilder "builds" the constraint hierarchy into AnimationJobs on Awake, so constraint order cannot be changed later by simply re-ordering the hierarchy.
  - The 1.3 manual lists Unity 2023.2+ compatibility.
  
  — [Animation Rigging manual 1.3](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html); [Changelog 1.x (2020–23)](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/changelog/CHANGELOG.html); [Uninomicon](https://uninomicon.com/animation_rigging_package)
- Community practice: foot IK with Animation Rigging uses Two Bone IK per foot plus a Multi-Position constraint for the pelvis, and quadruped setups exist. — [Unity Discussions (c. 2020)](https://discussions.unity.com/t/foot-placement-ik/789265); [YouTube tutorial](https://www.youtube.com/watch?v=iMojuj0K63s)
- Final IK (RootMotion) Grounder is "automatic vertical foot placement and alignment" for bipeds, spiders, bots and quadrupeds. GrounderQuadruped works with LimbIK, CCD or FABRIK solvers and has options such as maintaining head rotation. — [Final IK docs, GrounderQuadruped](http://www.root-motion.com/finalikdox/html/class_root_motion_1_1_final_i_k_1_1_grounder_quadruped.html); [Final IK index](http://www.root-motion.com/finalikdox/html/index.html)
- Learned locomotion for arbitrary morphologies: Shared Modular Policies (ICML 2020) use one small network per actuator, coupled by message passing, so one policy controls many morphologies. — [Huang, Mordatch, Pathak (2020)](https://proceedings.mlr.press/v119/huang20d.html); [code](https://github.com/huangwl18/modular-rl)

### Inferences
- A practical recipe for this project:
  1. Compute each leg's length from the current bone graph (hip → foot, rest pose).
  2. Set stride length ≈ k·L and cadence from the target speed. Choose the gait by Fr = v²/(gL), with walk below about 0.4, trot/run between 0.4 and 2.5, and gallop above.
  3. Use a per-leg phase offset table per stance (quadruped trot: diagonal pairs in phase; walk: four-beat).
  4. Animate the swing foot on a normalized arc scaled by L.
  5. Plant the foot with raycast + two-bone IK.
  6. Set body height = rest hip height adjusted by the average of the planted feet, and pitch from the front/rear foot-height difference.
  
  With different front and rear leg lengths, height and pitch fall out of the same IK. Cadence should use one shared clock with rational ratios as in Spore, or the longer leg's frequency, so gaits stay synchronized.
- The project already has a single-chain snake mover. Spore's "pseudo-feet on the spine" crawl is the analogous approach for legless bodies.
- For a hybrid whose skeleton is rebuilt at runtime, rigs must be rebuilt as well. Animation Rigging supports adding constraint components and calling `RigBuilder.Build()` again. I am moderately confident this API is public and re-callable, but not verified from the docs in this session. Hand-written FABRIK or two-bone solvers in plain C# or Burst jobs avoid this dependency and are small.
- Learned (RL) controllers are research-grade and not practical for this game (training cost, tooling). They are noted only for completeness.

### Gaps
- Unity 6-specific compatibility of Animation Rigging (package 1.3.x) was not confirmed beyond "2023.2+" in the manual.
- Kinematica / motion matching: Unity's Kinematica was an experimental package and, to my knowledge, was discontinued (around 2021). I did not verify this in this session. Motion matching generally assumes a mocap database for a fixed skeleton, so it is poorly suited to runtime-varying morphology. (Unverified; my judgement.)
- Physics-based character controllers for varied morphologies were not researched beyond the RL paper.

## 4. Mixed stance (biped with digitigrade hind legs; quadruped with human hands as forefeet)

### Takeaway
I found no source that addresses this directly. The closest technique is Spore's: classify legs by semantic role, group them by length, give each group its own gait parameters and phase, and let IK plant whatever end effector the leg has. The foot's shape (paw, hoof, hand) is only the rigid detail at the end of the chain plus a foot-alignment rule.

### Cited Findings
- Spore handles legs of different lengths in arbitrary arrangements. Each group runs its own gait style simultaneously, e.g., short legs running while long legs trot. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Spore binds animations with predicates (UprightSpine, HasGraspers, HasFeet) and falls back to branched animations only when needed. Tool use branches on graspers vs mouth. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Final IK's Grounder supports mixed rigs (bipeds, quadrupeds, multi-legged) through generic limb solvers. — [Final IK docs](http://www.root-motion.com/finalikdox/html/class_root_motion_1_1_final_i_k_1_1_grounder_quadruped.html)

### Inferences
- Stance should be a property of the chassis (the body plan: biped, quadruped or crawler), and the gait table should come from the chassis. A grafted limb inherits its role and slot from the chassis (the chassis' front leg stays a front leg), while its length and segment count come from the donor.
- A digitigrade hind leg on a biped is a 3-segment leg (thigh, shin, metatarsal). Two-bone IK does not cover it. The options are:
  - Use a 3-bone solver (FABRIK/CCD with hinge limits).
  - Treat the metatarsal as a fixed-ratio extension of the shin, a common trick in which the ankle angle is driven proportionally to the knee angle. I found no source for this during the session; it is known from practice.
- Human hands as forefeet: plant with palm-down alignment and a slight wrist extension, and switch to a "grasper" role when the attack or interaction channel wants the hand (Spore's variants: the game picks which grasper plays).
- Mixed front and rear leg lengths make the spine pitch. A clamp or lean compensation should keep the head level (compare GrounderQuadruped's "maintain head rotation" option).

### Gaps
- I found no published game postmortem on animating mixed-stance hybrids (e.g., user-made chimera games other than Spore). This is an open area; the recommendations above are extrapolated.

## 5. Cost and performance with many creatures on screen

### Takeaway
Procedural animation of small skeletons is cheap. Spore's figure was about 0.2 ms per 25-bone character in 2008 on a laptop CPU. In Unity the bottlenecks are usually Transform-hierarchy updates and per-renderer skinning. GPU skinning, Burst-compiled jobs, Animator/visibility culling, LOD on IK update rate, and avoiding per-frame mesh regeneration are the levers.

### Cited Findings
- Spore: about 0.2 ms per frame for a 25-body character on a 1.7 GHz Pentium-M, 35% of it in IK. — [Paper (2008)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Unity Player Settings expose GPU Skinning modes CPU, GPU, and GPU (Batched). The forum post reports GPU (Batched) as the default in newer versions. — [Unity Discussions (c. 2024)](https://discussions.unity.com/t/how-to-use-gpu-skinning/1500752); [Unity Discussions (c. 2023)](https://discussions.unity.com/t/question-on-gpu-compute-skinning-vs-srp-batching/925127)
- Crowd tools using GPU skinning plus instancing claim thousands of animated meshes, versus "hundreds" with the default Animator pipeline. This is a vendor claim. — [Crowd Skinner (itch.io)](https://aerilon.itch.io/crowd-skinner)
- In one GPU-skinning implementation, reading `localToWorldMatrix` from every bone Transform costs more than the skinning itself. Animator must be AlwaysAnimate for compute skinning in that tool. — [kosrud/dq-skinning-for-unity (GitHub)](https://github.com/kosrud/dq-skinning-for-unity)
- Animation Rigging constraints run as Burst-compiled animation jobs, and enabling Burst "dramatically improves" performance. — [Animation Rigging changelog](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/changelog/CHANGELOG.html)

### Inferences
- With small bone graphs (roughly 10–40 bones) and a few dozen creatures, plain C# IK is likely affordable. Moving the solvers into Burst `IJobParallelFor` over NativeArrays (or `TransformAccessArray`) scales further.
- Several measures should cut the per-frame budget:
  - Update IK at reduced rates for distant creatures.
  - Skip foot IK for off-screen creatures.
  - Cache leg lengths and gait parameters at graft time, not per frame.
- Mesh regeneration (the skin field) must stay an event-time cost (a graft), never per frame. Its cost should be amortized, e.g., built on a worker thread with the job system and swapped in the next frame.

### Gaps
- There are no Unity 6-specific benchmarks for Animation Rigging with N characters, or for runtime SkinnedMeshRenderer rebuild cost. These should be measured in-project.
