# Staged transformation in games: how the body changes toward another creature, and posture as a global layer

Scope note: most shipped games do NOT publish tech breakdowns of their transformation systems. Where only fan wikis exist, this is marked "(wiki, low confidence)". Engine-level facts (Unity/Spore paper) are primary sources. Items marked "Inference" are my synthesis, not sourced.

## Q1. Techniques: per-stage meshes, blend shapes, bone scaling, additive posture, material/VFX overlays — which are cheap and readable

### Takeaway
Shipped games overwhelmingly use DISCRETE per-stage assets (whole-model swap per stage, or a small number of texture/overlay tiers), not continuous morphs; continuous morphing exists (Fable) but its readable signals are still a few strong silhouette features (horns, halo, height, muscle). Material/texture tiers are the cheapest and most common "stage" signal; silhouette changes are reserved for big thresholds.

### Cited Findings
- **Discrete tiers, texture/face-only:** Oblivion (2006) vampirism has 4 tiers from "closest to mortality" to "fully vampiric"; in the original game later stages age the face, remove brows/facial hair, pale the skin, redden eyes. The 2025 Remaster dropped per-stage appearance: look is constant across all four stages (dark eye circles, darkened lips, paler skin by race). — [UESP Oblivion:Vampirism](https://en.uesp.net/wiki/Oblivion:Vampirism); [Fextralife Oblivion Remastered wiki](https://oblivionremastered.wiki.fextralife.com/Vampirism) (wiki, 2025)
- **Discrete overlay stages on the face:** Mass Effect 2 (2010) renegade scarring deepens with renegade points and finally glows red; paragon heals it. Mod files ship "stages 2–5", implying ~5 discrete texture stages. — [Screen Rant](https://screenrant.com/mass-effect-renegade-shephard-skin-me2-me3-why/); [Nexus mod "Renegade Scars for All"](https://www.nexusmods.com/masseffectlegendaryedition/mods/1126) (stage count inferred from mod file names — medium confidence)
- **Continuous morph + a few strong features (Fable 1–3, 2004–2010):** appearance morphs on two alignment axes (good/evil, pure/corrupt); evil/corrupt = scars, horns, pale/darker skin, glowing eyes; good/pure = halo, light hair. Stat use also morphs the body: melee → muscle, accuracy → taller, magic → glowing blue tattoo lines. Fable III gave an "extreme morph" (wings) only at the end of the main quest. — [Second Person Shooter, "Legible Bodies in Fable II" (2010)](https://secondpersonshooter.com/2010/02/28/legible-bodies-in-fable-ii/); [GamesRadar (2026) on the reboot](https://www.gamesradar.com/games/rpg/fables-reboot-doesnt-have-morality-based-character-morphing-and-fans-are-divided-its-what-helped-set-fable-apart-from-other-middle-ages-western-rpgs/)
- The new Fable reboot (announced details ~2026) removes morality morphing; director's reason: "no objective good and evil" — i.e. morph requires a single clear axis. — [GamingBolt](https://gamingbolt.com/fable-doesnt-have-character-morphing-because-theres-no-objective-good-and-evil-says-director)
- **Per-stage whole models (boss chains):** Resident Evil 2 (1998; remake 2019) William Birkin has five mutation forms G1–G5, each a separate boss model; G1 is asymmetric — only the right side (arm, torso, face) is mutated, with a giant eye on the shoulder; G1 has two sub-stages distinguished just by claws appearing. — [Resident Evil Wiki, Birkin biology](https://residentevil.fandom.com/wiki/William_Birkin/biology) (wiki, medium confidence)
- **Stage = new enemy archetype with a new silhouette:** The Last of Us (2013) infection stages runner → stalker → clicker → bloater; each is visually keyed on head growth (stalker: fungus starting on face; clicker: split skull with fungus, "easy to spot in a horde" by face shape; bloater: fungal armor over the whole body) plus distinct sound (stalker croak, clicker clicks). — [The Mary Sue](https://www.themarysue.com/the-last-of-us-infection-stages-and-types-explained/); [TLOU wiki, Stalker](https://thelastofus.fandom.com/wiki/Stalker) (secondary sources)
- **Mass tiers with swapped sprite sets:** Carrion (2020) uses a "mass-based class system": current size of the monster decides the ability set; mass is gained by eating, lost by depositing or taking damage. — [Game Developer interview with K. Chomicki (2020)](https://www.gamedeveloper.com/design/carrion-game-level-designer-krzysztof-chomicki-on-managing-amorphousness-gravity-and-screams); [Game World Observer (2020)](https://gameworldobserver.com/2020/08/27/carrion)
- **Partial transformation localized to limbs:** Bloodborne (2015) player beast transformation reportedly changes only the arms; the Beast's Embrace rune makes the naked hunter resemble an early Cleric Beast and adds beast howls/screeches on attacks. NPC "stage 1" beasts have longer limbs and excess hair. — [Bloodborne Fandom wiki, Beasthood](https://bloodborne.fandom.com/wiki/Beasthood); [Beast's Embrace](https://bloodborne.fandom.com/wiki/Beast's_Embrace) (wiki, low confidence; Kotaku 2015 piece on the topic was not fetchable)
- **Full body-state = race/model swap:** Skyrim (2011) werewolf is an all-or-nothing beast form with its own animation set; modders report custom races breaking it and the transform script re-reading player race to revert correctly — consistent with a race/model swap rather than morphing. — [Nexus forums](https://forums.nexusmods.com/topic/678197-custom-race-werewolf-transformation/); [UESP Skyrim:Werewolf](https://en.uesp.net/wiki/Skyrim:Werewolf) (mechanism inferred from modder reports — medium confidence)

### Inferences
- Cheapness ranking for a stylized low-poly game: (1) material/tint/emissive overlay per stage — nearly free, always readable up close, weak at distance; (2) bone/part scale per stage — cheap, affects silhouette, but interacts with animation (see Q2); (3) additive posture — cheap at runtime, strongest silhouette read; (4) per-stage meshes / whole-model swaps — expensive in content, strongest read, forces a "transition moment".
- Pattern across examples: games that have many stages (Oblivion 4, ME2 ~5) put stages in texture/face; games that change SILHOUETTE use 2–5 widely spaced stages (TLOU, Birkin) and each silhouette stage gets a unique feature (a growth, a claw, an eye) rather than just "more of the same". This supports the none → weak → medium → pure scheme with a distinct silhouette feature per step.
- Fable is the only mainstream example of continuous whole-body morph; its critic describes it as "legible", but the legible parts are discrete features (horns, halo, tattoos), not the continuous blend.

### Gaps
- No primary tech breakdown found for how Fable implemented morph (blend shapes vs bone scale) — only descriptive sources.
- No primary source for Bloodborne's beast visuals; only fan wikis.

## Q2. Posture (hunch, head carriage) as a global layer independent of animations; interaction with locomotion

### Takeaway
The standard engine tools are (a) an additive animation layer holding a static "hunch delta" pose, weighted by stage, and (b) a post-animation rig constraint (e.g. Unity Animation Rigging) that rotates spine/neck bones by an offset after the animator runs. Spore's shipped system shows the general principle for arbitrary bodies: author motion in body-relative/limb-relative/ground-relative spaces and solve with IK, so posture and proportions can change without re-authoring.

### Cited Findings
- Unity animator layers: Override replaces lower layers; Additive adds on top, and "for additive blending to be successful, the animation on the additive layer must contain the same properties as the previous layers." Avatar masks restrict a layer to body parts (e.g. upper body only). — [Unity Manual: Animation Layers](https://docs.unity3d.com/Manual/AnimationLayers.html) (current Unity 6 docs)
- Additive layer = delta between a reference pose (T-pose or idle) and the additive frame, added on top of the base. — [MoCap Online, Animation Layers guide](https://mocaponline.com/blogs/mocap-news/animation-layers-guide) (secondary, ~2024)
- Known gotcha: users report an additive layer "destroys character stance" in Unity — typical cause is a wrong reference pose for the additive clip. — [Unity Discussions (2021)](https://discussions.unity.com/t/additive-animation-layer-destroys-character-stance/861167) (thread title only read; cause is my inference)
- Unity Animation Rigging Multi-Rotation Constraint: weighted rotation of a constrained bone toward sources, global weight 0..1 interpolated linearly, per-axis masking, optional maintain-offset; commonly used on the spine to follow camera on top of animation. — [Unity Animation Rigging docs 1.4](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/MultiRotationConstraint.html)
- Lone Echo (Ready At Dawn, GDC 2017): spine and legs driven by separate procedural animation based on velocity, plus "an additives layer for some animator control" — i.e. procedural + additive layers coexisting over locomotion. — [Road to VR](https://roadtovr.com/lone-echo-developer-shows-impressive-procedural-hand-posing-system-vr/); [GDC Vault](https://www.gdcvault.com/play/1024446/It-s-All-in-the)
- Spore (Hecker et al., SIGGRAPH 2008): animators author once; data is stored "morphology-independent" via per-channel "movement modes" that define the coordinate frame — e.g. limb-length relative (long limbs → large motion), ground relative (z scaled 0 = rest height, 1 = ground), CreatureSize scale mode (nonuniform scale by the character's bounding box). Runtime specializes to the creature → pose goals → IK solver → passive secondary animation layered on top. Characters are 20–80 "bodies" (spine vertebrae, limbs, parts). A separate gait synthesizer groups legs by length. — [Hecker et al. 2008, PDF](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf); [ACM DL](https://dl.acm.org/doi/10.1145/1360612.1360626); [GDC 2007 talk page](https://www.chrishecker.com/How_To_Animate_a_Character_You've_Never_Seen_Before)

### Inferences
- For a Unity game with a Humanoid/Generic rig: put the stage posture as ONE additive layer (a single-frame "hunch" pose authored against the idle as reference pose), with layer weight = stage weight (0 / 0.33 / 0.66 / 1 or a smoothed value). Locomotion stays on base layer; the hunch is added on every frame, so walk/run keep working. Masking the layer to spine/neck/head keeps legs clean.
- Alternative without clips: post-animator rotation offsets on spine/neck bones (Animation Rigging constraint or LateUpdate code). Risk: foot/hand contact and head-look are computed before the offset — hands will drift unless IK targets are re-solved after the offset. Order of evaluation matters (posture → then IK for feet/hands).
- Bone SCALE for proportions (longer arms, shorter neck) interacts badly with non-uniform parent scale (the project already has a gotcha on this) and with root motion/stride length; Spore's "limb-length relative" mode is the principled answer: stride and reach scale with limb length rather than staying absolute.
- Hunch changes eye height and head forward offset → first-person camera and aim origins should read from the head bone after the posture layer, not from a fixed offset.

### Gaps
- No public source found on how a shipped game implements a gradual "beast posture" layer over the player's normal locomotion set (e.g. Bloodborne, Prototype). Prototype's arm morphs are described only at the gameplay level ([Wikipedia](https://en.wikipedia.org/wiki/Prototype_(video_game))).

## Q3. Signalling stage thresholds and avoiding "popping"

### Takeaway
Games signal a threshold with a dedicated transition moment (animation/cutscene, sound, camera) and make the new stage carry a NEW feature plus a new sound; pure continuous change without a moment is either unnoticed (Oblivion Remastered removed stage visuals entirely) or is made legible only through discrete features (Fable).

### Cited Findings
- Birkin's forms are separated by transformation cutscenes between boss fights; even a sub-stage within G1 is signalled by a single new feature (claws). — [RE Wiki](https://residentevil.fandom.com/wiki/William_Birkin/biology) (wiki)
- TLOU stages pair silhouette with signature audio (croak, clicks), so a stage is identifiable by ear before sight. — [The Mary Sue](https://www.themarysue.com/the-last-of-us-infection-stages-and-types-explained/)
- Bloodborne's Beast's Embrace adds howls/screeches to attacks — sound as the stage signal on an otherwise subtle model. — [Bloodborne wiki](https://bloodborne.fandom.com/wiki/Beast's_Embrace) (wiki, low confidence)
- Skyrim beast form uses a dedicated transformation animation; mods add revert animations "showing fur retracting" — community treats the transition moment as a missing piece worth adding. — [Nexus mod: Werewolf Revert Effect And Animation](https://www.nexusmods.com/skyrimspecialedition/mods/76472)
- Fable III withheld the biggest morph (wings) until the end of the main quest — the most dramatic silhouette change is a milestone reward. — [GamesRadar](https://www.gamesradar.com/games/rpg/fables-reboot-doesnt-have-morality-based-character-morphing-and-fans-are-divided-its-what-helped-set-fable-apart-from-other-middle-ages-western-rpgs/)

### Inferences
- Anti-pop recipe: discrete stage decided by thresholds with hysteresis (e.g. enter medium at 0.65, drop back at 0.60) so the body does not flicker when share oscillates near a boundary; then tween the visual weight (posture layer weight, blendshape, scale) over 0.5–1.5 s rather than snapping.
- Hide the discontinuous part (new mesh piece appearing, e.g. tail/hump) under a short VFX burst (particles/flash/shader dissolve) timed with a sound and a brief "convulse" additive animation; the continuous part (posture, proportions) tweens underneath.
- Tie the transition to a moment the player already attends to (the graft/kill that caused it) — the cause and the change read as one event.

### Gaps
- No developer postmortem found that quantifies transition timing or hysteresis for stage changes.

## Q4. Examples where local part swaps and a global body-state layer coexist

### Takeaway
Fable is the clearest precedent: local-ish stat features (muscle, height, tattoos) and global alignment features (skin, eyes, horns/halo) are computed independently and stack on one body. Spore shows the technical foundation for combining arbitrary local parts with global animation. Birkin G1 and Bloodborne's arms show "local first, global later" as a staging pattern.

### Cited Findings
- Fable II: combat style changes body (muscle/height/tattoos) while morality independently changes skin, eyes, horns/halo — two layers on one character. — [Second Person Shooter (2010)](https://secondpersonshooter.com/2010/02/28/legible-bodies-in-fable-ii/)
- Spore: player attaches arbitrary parts (mouths, graspers, feet, spikes, armor) to a malleable spine torso; one animation set drives all via generalized spaces + IK + secondary animation. — [Hecker et al. 2008](https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf)
- Birkin G1: mutation localized to one side (arm/torso/face), later stages consume the whole body — local → global progression. — [RE Wiki](https://residentevil.fandom.com/wiki/William_Birkin/biology)
- Bloodborne: player beast transformation localized to arms; NPC beasts progress to full-body forms. — [Bloodborne wiki](https://bloodborne.fandom.com/wiki/Beasthood) (wiki, low confidence)

### Inferences
- Layer order that avoids conflicts: base locomotion → global posture additive (weight from dominant-species stage) → local part geometry attached to bones (so parts follow the hunched spine automatically) → IK/contact fix-ups → secondary motion. Local parts must attach to bones, not to fixed offsets, or the global hunch will separate them.
- The global layer should be driven only by the dominant species (one axis at a time, as the Fable reboot's reasoning implies: morph needs one clear axis); mixing two species' postures additively at once risks unreadable in-between poses.

### Gaps
- No example found of a shipped game blending two different creatures' postures simultaneously on the player (multi-species blend); this appears to be uncharted territory.
