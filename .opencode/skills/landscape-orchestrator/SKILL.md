---
name: landscape-orchestrator
description: Use when running the CHIMERA landscape package S0–S5 — phase gates, agent ownership, spec-first review, and no-code-until-accepted.
---

# Landscape orchestrator

## Rule

One phase at a time. **No code starts until the phase spec is reviewed and its gates are named.** A phase is accepted only by its own gates: logic → tests/detectors, form → frame + map, feel → game designer.

## Phases

| phase | owner | exit gate |
|---|---|---|
| S0 baseline | tester + forest-warden | frozen A/C/B0/Pavilion heights/fingerprints; `worldVersion=1` |
| S1 field/forms | haskell-categorizer | S1 detectors + 20 seeds; A/C heights unchanged |
| S3 integration/order | forest-warden | one `SampleGround`; POI/pad last; A/C/B0 unchanged |
| S2 composition | designer-artdirector | forms/POIs/roads; frame G7 A; M1–M10 |
| S4 erosion/roads | general + forest-warden | bake cache + roads from base; M15/M16 |
| S5 verification | tester | G0–G8; 20 seeds; L3 left to designer |

## Unity console

Every agent that touches Unity **must first read `.opencode/skills/chimera-unity/SKILL.md`**. The editor is controlled only through `unity command` / `unity status` / `unity command editor_status`; do not hand-edit scenes or assets, do not restart Unity mid-build, and do not use `--no-verify`. Headless: start the batch editor without `-quit`, confirm `editor_status=ready`, then run tests/captures.

## Agent ownership

- **haskell-categorizer** — S1 math: `H0`, bands, signed forms, H/D, curvature.
- **designer-artdirector** — S2: composition, POIs, roads, materials, frames.
- **forest-warden** — S3: order, masks, `SampleGround`, station, NavMesh, A/C.
- **general** — S4: erosion bake, roads graph, external practice.
- **tester** — S0/S5: baselines, metrics, tests, parity, 20 seeds.
- **unity-integrator** — scene/prefab/camera changes only after spec.
- **docs-keeper** — master plan, status, index, `spec_status.py`.
- **model-critic** — form frames only; score threshold 8.5 before showing designer.

## Per phase loop

1. Read `Docs/les/PLAN-2026-11-01-landshaft.md` and the phase spec.
2. Write/confirm the phase plan: files, steps, tests, commit points.
3. Orchestrator review: boundaries, one live source, no duplicate decisions.
4. Launch 2+ agents: one implements, one cross-reviews. No two writers on one file.
5. Run gates; fix until green or stop on a design fork.
6. Update `СТАТУС`, `УКАЗАТЕЛЬ`, phase header; run `spec_status.py`.
7. Commit only the phase; push after.

## Never

- Do not let P5-lite stand for P5 full (erosion).
- Do not let `Clearings` be called combat arenas; they are zones/POIs.
- Do not measure `H/D` by half-height; use `H/(D−brow)`.
- Do not put landform after the station pad.
- Do not enable `worldVersion=2` in a scene without an explicit parity test.
- Do not accept a frame without hiding roads, arena edges and cover pods.
