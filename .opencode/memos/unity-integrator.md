# Памятка завхоза (Unity) — 05.09.2026

**Гочи:** `FindAnyObjectByType`, `InputAction` без `expectedControlLayout`, `Destroy` → `~dead`, `CC` в `Instantiate(pos,rot)`, высоты от земли `footY`, ось `max(baseSize)` запас 5%, масштаб → пустой узел, торец капсулы → шар, `IsOwnFace`/`IsHeadName` контракт → `RebuildRenderers`, `Tint Rebase`, `git mv .cs+.meta`, Editor только в `Scripts/Editor/`.

**Префабы:** не руками, `Editor/*Prefab.cs` + `SpeciesBootstrap` → `Chimera → Создать дефолтные виды` → `Создать префаб X`. Состав NPC: `CharacterController+Health+Telegraph+CreatureBody`.

**Сцена:** `SampleScene` — Player, Arena 200м NavMesh, 4 спавнера + Werewolf, PackCoordinator, EvolutionConfig, Forage. Сериализация `SpeciesSO` `Data/*.asset` (`BodySlots` единственная правда имён), `.meta` GUID.

**После правки:** бутстрап + Ctrl+S + `BodyRules.CheckData` + плейтест до коммита.
