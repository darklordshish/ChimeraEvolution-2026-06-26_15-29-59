---
name: chimera-testing
description: Use when writing or running CHIMERA tests — EditMode/PlayMode via the Unity pipeline, forest and form tests, detectors as gates. Tests guard invariants, not coverage.
---

# Chimera Testing

Тесты в проекте есть и являются воротами для логики. Инвариант без теста — пожелание.

## Где что

- `Assets/_Chimera/Tests/EditMode/` — чистая логика, без сцены; `PlayMode/` — сцена, `CharacterController`,
  `Instantiate`, кадры. Отдельные asmdef: `Chimera.Tests.EditMode`, `Chimera.Tests.PlayMode`.
- `com.unity.test-framework` в `Packages/manifest.json` (сейчас 1.6.0), NUnit.
- Лесные тесты: `Tests/EditMode/Forest/` и `Tests/PlayMode/Forest/` — детерминизм сида, швы чанков,
  вода, флора, логова, спавнер, хуки климата.
- Форма: `BodyMeshTests`, `PartRingFitTests`, `PartsImportTests`, `GrowthTests`, `BodySizeTests`,
  `RegionKeysTests`, `ChimeraMatrixTests`, `SkinGrainTests`, `LatticePhaseTests`.
- Python — только Anaconda `C:\ProgramData\Anaconda3\python.exe`; `Anatomy/tools/audit.py` и старые
  cage-проверки — исторический слой, не главный детектор формы.

## Как гонять

Пайплайн живого редактора (подробности — `CLAUDE.md`, «Тесты»; полный набор команд — `.claude/skills/chimera-unity`):

```
unity command set_autotick --enable true
unity command run_tests --mode editor --async_tests true
unity command test_status          # опрашивать до завершения
```

Без `set_autotick` Unity душит тик неактивного окна и тесты «висят». PlayMode — отдельным прогоном.
Запасной путь — Unity Test Runner. `--no-verify` и коммит по непрогнанным тестам не использовать.

## Как писать

- EditMode — без тяжёлого Unity API: чистые функции/структуры, `Assert.That(..., Within(...))`.
- PlayMode — минимальная сцена, не грузить `SampleScene`; создавать пробный объект и звать настоящий билдер.
- Не мокать `SpeciesSO` руками — брать `SpeciesBootstrap`/`Data/*.asset`.
- Тест пишется рядом с фичей, а не «после». Меняешь правило — правь его сторожа тем же заходом.
- Не гнаться за покрытием: тест на инвариант, гоча или найденную мину.
- **Сцена — тоже тест:** `ForestSceneCameraTests` читает `ForestFragment`/`ForestSandbox` и требует одну
  включённую `Main Camera`; `GameObject.Find` не видит неактивное — для `Clear` брать
  `FindObjectsByType<T>(FindObjectsInactive.Include)` и прямые ссылки.
- **Станция/климат:** `Climate_InsideLab_HidesRainAndFog` проверяет точку внутри `DomeLabComplex`; `Clear`
  проверять по объектам, статикам и `RenderSettings`, не только по `null` полям.
- **Камера:** `BodySize.FromGround` — тест от земли; FPS/третье лицо — через `OverlapSphere` по коллайдерам
  лаборатории; `SphereCast` держать с запасом буфера.
- **Сцена после теста:** `EditorSceneManager.CloseScene(..., true)`; PlayMode не оставлять в Play перед
  EditMode-прогоном (SaveModifiedSceneTask падает с «cannot be used during play mode»).

## Ворота

- `Docs/tools/spec_status.py` — перед коммитом спеки/правила.
- `chimera-map`, `chimera-silhouette`, `chimera-matrix` — детекторы формы, не замена тестам.
- Известные неопределённые тесты (`ChimeraPlanTests` — смешение графа) не считать «красными»,
  но и не прятать: они ведут долг.
