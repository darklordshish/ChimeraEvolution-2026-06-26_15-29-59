# Game practice: part-based / hybrid creature bodies that read as one creature

Scope: how shipped games (and their devs) built creature bodies from parts of different animals or from user-built bodies, and specifically how the overall body/silhouette was made coherent rather than parts glued onto an unchanged trunk. Research date 2026-09-27. Several primary sites (Steam guides, GameSpot, Kotaku, Fandom wikis, ModDB) returned 402/403/429 to the fetch tool, so some items rest on search-result snippets only; these are marked.

## Q1. Impossible Creatures (Relic, 2003): how two animals were merged

### Takeaway
Verifiable public detail is thin. What is confirmed: exactly two animals per creature, per-body-region choice of donor, stats that move up or down with the chosen parts, the larger animal's size class wins, and a dev/modding tool (ComboTest) whose main manual fix for bad joins is a per-creature **limb-scale file** — i.e. fitting was partly hand-tuned scale correction, not an automatic proportion interpolation. No source found describing interpolation of the torso/proportions.

### Cited Findings
- Any two animals can be combined; the player chooses which body parts come from which animal; for some limbs the player can choose whether the creature has them at all — [ichive Fandom wiki "Animals" (search snippet, undated)](https://ichive.fandom.com/wiki/Animals)
- Only two animals at once; 76 animals (51 without downloads); army capped at 9 creature types — [Wikipedia, Impossible Creatures](https://en.wikipedia.org/wiki/Impossible_Creatures) (also [GameSpew 2015](https://www.gamespew.com/2015/11/impossible-creatures-evolving-rts/), search snippet)
- Parts carry abilities (electric eel tail → shock; gorilla forearms → smash; cheetah back legs → speed); base stats (speed, melee/ranged damage, defence, health, sight radius) "go up or down depending on which parts you use" — i.e. per-part contribution to stats — [GameSpew, 2015 (search snippet)](https://www.gamespew.com/2015/11/impossible-creatures-evolving-rts/)
- A search-result snippet states the game divides creatures into 5 body parts (exact list not verified in fetched text) — [Steam guide "Impossible Creatures 101" (snippet only)](https://steamcommunity.com/sharedfiles/filedetails/?id=553316371)
- Size: "If two animals of different sizes are combined, the resulting creature will have the larger size"; size ranked 1–10 (mods add 11–12) — [Impossible Creatures Wiki "Size" (search snippet; page blocked)](https://impossiblecreatures.fandom.com/wiki/Size)
- ComboTest (in the SDK) "is essentially the creature combiner used to see how user-created stock animals will mesh with other creatures"; "some mild editing, such as limb-scale adjustment, is done from within ComboTest" — [Steam guide "Impossible Creatures Modding: Getting Started" (snippet; page 429)](https://steamcommunity.com/sharedfiles/filedetails/?id=735413158)
- When limbs "need to be adjusted for leg clipping or ugly looking connections at tail and neck", a `.lsc` file is automatically created/updated with those changes; stats live in the creature's `.lua` file — same Steam guide (snippet)
- ComboTest lets you pick any 2 creatures, combine their limbs and play their animations "to see how the swapped limbs interact with each other" — [Steam guide "Re-texture a Creature" (snippet)](https://steamcommunity.com/sharedfiles/filedetails/?id=3312399093)
- Modding SDK and Mission Editor shipped with the Steam Edition (2015) — [Wikipedia](https://en.wikipedia.org/wiki/Impossible_Creatures)

### Inferences
- The existence of a per-stock-creature limb-scale file and the named failure modes ("leg clipping", "ugly connections at tail and neck") imply the combiner attached donor parts at fixed joints of a shared/standard skeleton layout and relied on per-animal authored scale corrections to hide mismatches — a hand-authored fitting table rather than automatic proportion blending. (Inference; not stated by Relic in anything found.)
- The "larger size wins" rule suggests the overall body scale follows one parent rather than an average; whether parts are rescaled toward that size is not confirmed.
- The named problem spots — neck and tail joins, and legs clipping — are exactly where a donor part meets an unchanged trunk; this matches the "glued-on" pitfall.

### Gaps
- No Relic postmortem, GDC talk or interview describing the combiner's geometry (shared skeleton, how torso is chosen, whether proportions are interpolated, how animation is shared) was found. GameSpot 2002 previews (IGF, Gen Con) and namu.wiki exist but were blocked (403). Primary SDK docs (bone naming, .lsc format) were not readable.
- Whether stats are averaged, per-part summed, or size-scaled is not verified beyond "go up or down depending on parts".

## Q2. Spore (Maxis, 2008): implicit torso skin, rigblocks, retargeting

### Takeaway
Spore's coherence came from two layers: (1) a single **implicit (metaball) skin** regenerated on the fly around the spine and limbs, so any assembly is one watertight surface — Hecker explicitly chose topological robustness over local control; (2) detail parts are **Rigblocks**, rigid artist-made meshes with player-driven parametric deform "handles" composed additively. Animation was made morphology-independent by having animators author against semantic queries (body capabilities + spatial queries) and scale modes (CreatureSize, LimbLength, GroundRelative), then solving with a particle IK. Nothing in Spore "blends species"; coherence is produced by one continuous skin plus shape parameters, not by donors.

### Cited Findings
- Spore uses "blobby implicit surfaces" (metaballs) for the skin and "has to generate the entire mesh on the fly as the player makes the creature" — [Chris Hecker, "My Liner Notes for Spore" (2008)](https://chrishecker.com/My_Liner_Notes_for_Spore)
- Why implicit: they lack local control vs. polygon modeling, but "the topological robustness is actually more valuable to Spore than the local control"; also tiny data footprint (for sharing creatures) — Hecker liner notes (2008)
- Field function: "a 4th order polynomial in the squared distance from the sample point to the center of the given metaball" (chosen for continuous derivatives → no lighting seams); only spherical metaballs (ellipsoidal "significantly slower to evaluate"); metaballs distributed along limbs by math on "how close they need to be to form a smooth shape"; polygonization avoided then-patented Marching Cubes (used ear clipping) — Hecker liner notes (2008)
- Bone weights come from which body part generated each metaball; this "works well for limbs, but sometimes big spine segments don't generate smooth weights, and the torso on fat creatures can shear"; a fix was prototyped but "torso-attached parts is harder, so we had to punt" — Hecker liner notes (2008)
- Unintended "flying squirrel bug": metaballs of independent limbs were not grouped, so webbing formed between limbs; kept as a feature (bat wings) — Hecker liner notes (2008)
- Player builds by "manipulating a malleable clay-like torso containing the spine, and attaching limbs and deformable anatomical body parts"; a creature has 20–80 "bodies" (anatomical parts, spine vertebrae, limbs); bodies carry capability tags (grasper, mouth, spine, root…) and standardized scalar deform curves (open/close mouth, etc.) — [Hecker et al., "Real-time Motion Retargeting to Highly Varied User-Created Morphologies", SIGGRAPH 2008 / ACM TOG 27(3)](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Bodies form a DAG with a serial chain of spine bodies at the root; the root is "a unique spine body chosen by a heuristic based on the maximum number of incident leg limbs and the position of the body"; rest pose = whatever the player built, assumed "reasonable" — Hecker et al. 2008
- Animators select bodies by capability + spatial queries (Front/Center/Back, relative to whole-character bounds or to the "setspace" of same-capability bodies) + extent (FrontMost…) + limb modifier "SpineSegment" (walk up to the shoulder/hip) — Hecker et al. 2008
- Scale modes: none; **CreatureSize** (nonuniform scale proportional to the character's bounding box); **LimbLength** (scale by path length from the body to the nearest spine segment — "an approximation to the workspace"); **GroundRelative** (z from 0 = rest height to 1 = ground, so every character's grasper hits the ground with same timing) — Hecker et al. 2008
- Locomotion synthesized for arbitrary leg counts: animator gait parameters for 1–6 feet, procedural for ≥7; foot path authored in a normalized space scaled by leg length; footless creatures either float or crawl (spine bodies turned into pseudo-feet, inch-worm gait) — Hecker et al. 2008
- Particle IK: two phases (spine first as a spline through branch points, then limbs; a limb subtree is rotated+scaled rigidly toward the goal as preconditioner); buckling detection pulls the spine back toward the root-relative rest pose — Hecker et al. 2008
- QA: stochastic testing against a database of player-uploaded creatures, "Animation Validation Grid" spreadsheet; ~90% pass rate over several hundred creatures × ~1000 animations; ~0.2 ms/frame for 25 bodies on 1.7 GHz Pentium-M; animators needed "weeks to build up an intuition about which kinds of motions generalize" — Hecker et al. 2008
- Rigblock = geometric building block (hand, mouth; wheel, engine) plus a set of animations "parameterized over a unit interval" driven by player handles (sliders); deform animations must **compose additively** like delta morph targets, not be weight-blended ("applying a second deform would undo part of the effect of the first"); engine accumulates translation/scale/rotation deltas separately then forms the matrix (matching Maya); finished model is "baked" and animation data removed — [Choy, Ingram, Quigley, Sharp, Willmott, "Rigblocks: Player-deformable Objects", SIGGRAPH 2007 sketch](https://www.cs.cmu.edu/~ajw/s2007/0248-Rigblocks.pdf); also [ACM DL](https://dl.acm.org/doi/10.1145/1278780.1278880)
- Rigblocks position themselves as the "sweet spot" between artist-authored fixed models and "lower-quality, effort-intensive, wholly player-driven approaches, such as providing a sculpting tool" — Choy et al. 2007
- Secondary: stretching the spine or limbs auto-adds metaballs; individual metaballs (vertebrae) can be scaled for thickness; detail parts "snap onto" the body — [Rempton Games blog, 2022 (secondary, no sources cited)](https://remptongames.com/2022/08/07/how-the-spore-creature-creator-works/)

### Inferences
- Spore's coherence mechanism is the continuous skin: parts do not float because the body surface is recomputed around whatever skeleton exists, and the torso thickness is a per-vertebra scalar the player edits. For a donor-driven game the analogue would be driving those per-vertebra/segment radii (and limb lengths) from donor data, not just attaching meshes.
- The known Spore failure (fat torsos shearing, torso-attached parts hard to weight) sits exactly at "big implicit torso + rigid parts attached to it" — a direct warning for a metaball body with rigid detail meshes on bones.
- Additive delta composition (not weighted average) is Spore's answer for combining several independent shape modifiers on one part — relevant if several donors each push one body proportion.
- Semantic tagging of bones (capabilities) + normalized, body-relative animation spaces is what let one animation set survive radically different proportions; changing proportions per hybrid is therefore compatible with shared animation if motion is authored in limb-length/ground-relative terms.

### Gaps
- Whether Spore's creature "archetypes"/NPC creatures were ever blended between two designs — no source; Spore has no species blending.
- Andrew Willmott's other SIGGRAPH 2007 sketches (texturing, etc.) not read.

## Q3. Other games with part-based or blended bodies (technique-revealing only)

### Takeaway
No Man's Sky is the closest analogue: per-archetype shared rig ("blueprint"/"silhouette") with per-bone scaling and swappable accessory parts on a master model, animation adapting to the scaled rig — coherence comes from staying inside one archetype's skeleton and scaling its bones, not from cross-archetype merging. For the other candidates (Black & White, Species: ALRE, The Sims/Black Desert sliders, Monster Rancher, Starbound) nothing technique-level was verifiable in this pass.

### Cited Findings
- NMS art director Grant Duncan: they looked at Earth creatures and "it's quite surprising how few different types of skeletons there are"; these templates are called blueprints — [Game Developer / NMS retrospectives, via search snippet](https://www.gamedeveloper.com/programming/what-the-code-of-i-no-man-s-sky-i-says-about-procedural-generation) (quote attribution from search results; primary interview not fetched)
- Blueprint process: templates, accessories, layering, scaling; shared rigs e.g. 'horse' and 'deer' start from the same skeleton, 'sharks' and 'dolphins' share one; "as height changes, weight can change, the proportions … can change … the animations can change, the bones can get bigger, thicker, and the voice can get deeper" — [NMS Wiki "Procedural generation" (search snippet; page blocked)](https://nomanssky-archive.fandom.com/wiki/Procedural_generation)
- Data-mined structure: descriptor files reference parts by name/tag (head, legs…) with chance values; a single master model contains all variant parts, toggled visible/hidden; bone scaling/proportions set through descriptors; animations shared across variants — [Game Developer, "What the code of No Man's Sky says about procedural generation" (2016)](https://www.gamedeveloper.com/programming/what-the-code-of-i-no-man-s-sky-i-says-about-procedural-generation)
- Sean Murray (Game Informer video, 2014): artists create thousands of component parts; each creature type is a "silhouette" explicitly created by artists (lizard, rat, fish, cow…), hundreds of types each with many variants; in dev-tool footage each variant within a type has "essentially the exact same rig", each body part can be scaled, attachments added from a pool; animations "modify to fit the rig however it is scaled" — [Christo, "Procedural Creature Generation and Animation for Games", Bournemouth MSc thesis (2022), citing Game Informer 2014 video](https://nccastaff.bournemouth.ac.uk/jmacey/MastersProject/MSc22/01/ProceduralCreatureGenerationandAnimationforGames.pdf) — secondary, author's reading of footage
- Same thesis: "having a limited scope is important for effective procedural generation"; artist-centric pool of premade content preferred when artists are available — Christo 2022 (opinion)
- Black & White: generic morph-target definition only surfaced; no primary technical source on how the creature's good/evil or fat/thin body change was implemented — [Wikipedia, Morph target animation](https://en.wikipedia.org/wiki/Morph_target_animation) (general, not B&W-specific)
- Sims 4 CAS sliders are built as deformer maps (DMap shape + DMap normals) per body region, produced by diffing base vs. edited mesh — [SimsCommunity MorphMaker tutorial (2018)](https://simscommunity.info/2018/07/10/learning-your-tools-the-sims-4-morphmaker/) (community tool, not EA documentation)

### Inferences
- NMS gets coherent silhouettes by (a) choosing the silhouette archetype first (artist-authored) and (b) varying proportions via per-bone scale on that archetype's rig; parts are accessories on the rig. Translating to a hybrid system: the body silhouette would have to be a *parameterized* archetype whose bone lengths/thicknesses are the donor-driven variables; attaching parts alone never changes the silhouette class.
- The NMS "height → weight → thickness → voice" chain is a correlated-parameter rule: one scalar drives several proportions together so variants stay plausible. A hybrid system could similarly map "donor share" to a correlated set of proportion changes rather than independent knobs.

### Gaps
- Black & White creature growth/alignment morphing: no primary technical source found.
- Species: ALRE: dev blog page fetched had only changelogs; how genes drive the procedural body mesh was not found in this pass (earlier blog pages likely contain it).
- Monster Rancher / fusion games, Starbound monster generator, Black Desert sliders: not researched to technique level (time/tool-call budget); Starbound is 2D and likely less relevant.
- No official GDC talk on NMS creatures found (the GDC Vault NMS talk found is on terrain: "Continuous World Generation in No Man's Sky").

## Q4. What worked, what looked bad, known pitfalls

### Takeaway
Documented pitfalls cluster at the junction between a big continuous torso and attached parts: joins at neck/tail and legs clipping (Impossible Creatures needed hand-tuned limb scale files), torso shearing and hard-to-weight torso-attached parts (Spore), and animations that do not generalize across proportions (Spore ~10% fail rate even after investment). What worked: one continuous implicit skin, semantic bone tags, normalized body-relative motion, additive parametric deforms, and constraining variety to archetype rigs.

### Cited Findings
- IC modders adjust limb scale for "leg clipping or ugly looking connections at tail and neck" (saved in `.lsc`) — [Steam modding guide (snippet)](https://steamcommunity.com/sharedfiles/filedetails/?id=735413158)
- Spore: fat-creature torso shear; torso-attached parts' weighting punted at ship — [Hecker liner notes 2008](https://chrishecker.com/My_Liner_Notes_for_Spore)
- Spore: metaball webbing between limbs (bug kept as feature) — Hecker liner notes 2008
- Spore animation: ~90% pass rate; failures split into code bugs, aesthetic generalization issues fixable by animators, and issues needing new features/branches — [Hecker et al. 2008](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Spore authors: "having a human simply tell the system what is important is immensely powerful, efficient, and robust" (vs. algorithmic discovery of semantics) — Hecker et al. 2008
- Rigblocks: weight-blending deforms undoes earlier deforms; additive delta composition required — [Choy et al. 2007](https://www.cs.cmu.edu/~ajw/s2007/0248-Rigblocks.pdf)
- NMS approach relies on strict predefined animal types plus a large artist-made part bank — [Christo 2022](https://nccastaff.bournemouth.ac.uk/jmacey/MastersProject/MSc22/01/ProceduralCreatureGenerationandAnimationforGames.pdf)

### Inferences
- None of the shipped examples found blends a *base body's proportions toward donors*; IC swaps whole regions with scale patches, Spore lets the player sculpt the torso directly, NMS varies bones within a fixed archetype. So "silhouette shifts toward the source animals" appears to be something these games did via (a) whole-region replacement (IC), or (b) explicit per-segment shape parameters (Spore/NMS) — the latter is the transferable piece: make torso segment radii / lengths parameters and let donors write them.
- The "Frankenstein" look is best avoided where the continuous skin itself changes (Spore) rather than where rigid pieces are only rescaled (IC's hand-fixed joins).

### Gaps
- No postmortem explicitly discussing "Frankenstein look" or player reception of hybrid silhouettes was found.
- Impossible Creatures' actual fitting algorithm (automatic joint alignment vs. authored offsets) remains unverified.
