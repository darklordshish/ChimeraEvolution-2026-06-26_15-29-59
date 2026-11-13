# Лесник → ГеймБоссу: П-3 к переносу — пути и команды

**Дата:** 2026-11-13 · **От:** лесная линия · **Кому:** ГеймБосс
**К чему:** пакет П-3 (`Docs/reports/Срез-2026-11-12/РЕШЕНИЯ-лес.md` §2), первая зона — `ForestPavilion` (решение
геймдизайнера 13.11)
**Источник:** ветка `les/runtime-builder`, коммит `62477822` (свежий `main` в неё влит, всё вне леса — версия `main`)

## Коротко

- Едут: сцена павильона и две сцены-стенда для сторожей, лесной код этого захода (климат по часам, выход босса,
  логова зверей), их тесты, Build Settings, формат `NavMeshAreas`, постановщики сцен, кадры 01–09 с индексом,
  спека «логово по законам» и письма 12–13.11.
- **Логова волка, лося и ежа готовы** (шаг 1 спеки, `03bf9e6c`) — едут сейчас, а не позже: в павильоне сид 1337
  ставит 3 + 2 + 3 логова, каждое рожает свой вид (сторож в `ForestPavilionFirstZoneTests`).
- **Не готово и потому не едет:** шаги 2–4 спеки логова (занятость по дому, страх от свидетелей, выбор дома
  психикой) — кода нет, логово пока считает жильцов по рождению.
- Тесты на ветке: EditMode — 582, падений нет, 2 давно известных неопределённых (`SizeOf(null)`, смешение графа);
  PlayMode первой зоны 7 из 7, часов 2 из 2, логов (синтетика) 6 из 6.

## 1. Что едет

### 1.1. Сцены и настройки

| путь | что |
|---|---|
| `Assets/Scenes/ForestPavilion.unity` (+ `.meta`) | первая зона: каньон и акведук выкл., логова зверей вкл., пул — волк, змея, лось, ёж; объекты вечного цикла из `SampleScene` с их настройками |
| `Assets/Scenes/ForestFragment.unity` (+ `.meta`), `Assets/Scenes/ForestSandbox.unity` | стенды: в сборку не входят, но их читают сценовые сторожа (`ForestSceneBaselineTests`, `ForestSceneParityTests`, `ForestSceneCameraTests`) |
| `ProjectSettings/EditorBuildSettings.asset` | павильон — индекс 0, `SampleScene` — 1 |
| `ProjectSettings/NavMeshAreas.asset` | только формат, пересохранённый Unity (`serializedVersion 3`); `agentClimb` — 0,75, как в `main` |

### 1.2. Лесной код этого захода

| путь | что |
|---|---|
| `Scripts/Forest/Climate/ForestClimate.cs` | небо по `WorldClock` |
| `Scripts/Forest/Generation/ForestBuilder.cs` | `IBossExit` — дверь лаборатории; логова зверей под `buildAnimalLairs` |
| `Scripts/Forest/Generation/ForestProfiles.cs` | профиль павильона: каньон выкл., логова зверей вкл. |
| `Scripts/Forest/Habitats/DomeAnimalLairs.cs` (+ `.meta`) | адреса логов волка, лося, ежа |

### 1.3. Тесты

| путь | что |
|---|---|
| `Tests/EditMode/Forest/Habitats/DomeAnimalLairsTests.cs` | адреса логов, синтетика (6) |
| `Tests/EditMode/Forest/Worldgen/ForestSceneBaselineTests.cs`, `ForestSceneCameraTests.cs`, `ForestSceneParityTests.cs` | сценовые сторожа, исключённые из П-2 |
| `Tests/PlayMode/Forest/Climate/ClimateClockTests.cs` | небо по часам (2) |
| `Tests/PlayMode/Forest/Worldgen/ForestPavilionFirstZoneTests.cs` | первая зона на настоящей сцене (7), не в гейте — мир Ø2000 строится секунды |

Все `.meta` — вместе с файлами.

### 1.4. Постановщики сцен (`Tools/Agent/`)

`ForestPavilionFirstZone.cs` (собрал первую зону; повторный запуск безопасен), `ForestPavilionScene.cs`,
`ForestB0Scene.cs`, `ForestSceneSetup.cs`, `ForestSpawnerSetup.cs`, `B01Scene.cs`, `SeedScan.cs`, `SeedWorldScan.cs`.

### 1.5. Документы

| путь | что |
|---|---|
| `Docs/Диаграммы/Лес/Кадры/01-…` – `09-…` и `ИНДЕКС.md` | кадры срезов первой зоны, 6,9 МБ; индекс уже говорит, где серии 10–17 |
| `Docs/superpowers/specs/2026-11-13-forest-logovo-po-zakonam.md` | ЧЕРНОВИК; нужна строка в `УКАЗАТЕЛЬ.md` |
| `Docs/les/FEEDBACK-2026-11-12-les-opis-vne-lesa.md`, `…-p2-opis.md`, `…-p3-opis.md`, `Docs/les/FEEDBACK-2026-11-13-zashchita-provala-i-zakony-logova.md`, этот файл | переписка П-0…П-3 |

Ссылки в спеке и письмах — в обратных кавычках, а не markdown-ссылками: граф документов на них не споткнётся.

## 2. Команды

```bash
git checkout les/runtime-builder -- Assets/Scenes/ForestPavilion.unity Assets/Scenes/ForestPavilion.unity.meta Assets/Scenes/ForestFragment.unity Assets/Scenes/ForestFragment.unity.meta Assets/Scenes/ForestSandbox.unity ProjectSettings/EditorBuildSettings.asset ProjectSettings/NavMeshAreas.asset
```

```bash
git checkout les/runtime-builder -- Assets/_Chimera/Scripts/Forest Assets/_Chimera/Tests/EditMode/Forest Assets/_Chimera/Tests/PlayMode/Forest
```

```bash
git rm -q --cached Assets/_Chimera/Tests/EditMode/Forest/Worldgen/G7FrameMetadataTests.cs* Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/CanyonGateTests.cs* Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductCameraTests.cs* Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductSeedTests.cs* Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductWaterCatalogTests.cs* Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductWaterTests.cs*
```

```bash
git checkout les/runtime-builder -- Tools/Agent/ForestPavilionFirstZone.cs Tools/Agent/ForestPavilionScene.cs Tools/Agent/ForestB0Scene.cs Tools/Agent/ForestSceneSetup.cs Tools/Agent/ForestSpawnerSetup.cs Tools/Agent/B01Scene.cs Tools/Agent/SeedScan.cs Tools/Agent/SeedWorldScan.cs
```

```bash
git checkout les/runtime-builder -- "Docs/Диаграммы/Лес/Кадры/ИНДЕКС.md" "Docs/Диаграммы/Лес/Кадры/01-срез-A" "Docs/Диаграммы/Лес/Кадры/02-срез-B0" "Docs/Диаграммы/Лес/Кадры/03-срез-C" "Docs/Диаграммы/Лес/Кадры/04-B0.1-лаборатория" "Docs/Диаграммы/Лес/Кадры/05-деревья" "Docs/Диаграммы/Лес/Кадры/06-B0.2-входы" "Docs/Диаграммы/Лес/Кадры/07-B0.3-планы" "Docs/Диаграммы/Лес/Кадры/08-B1.1-овраги-ясень" "Docs/Диаграммы/Лес/Кадры/09-B12-климат"
```

```bash
git checkout les/runtime-builder -- Docs/superpowers/specs/2026-11-13-forest-logovo-po-zakonam.md Docs/les/FEEDBACK-2026-11-12-les-opis-vne-lesa.md Docs/les/FEEDBACK-2026-11-12-p2-opis.md Docs/les/FEEDBACK-2026-11-12-p3-opis.md Docs/les/FEEDBACK-2026-11-13-zashchita-provala-i-zakony-logova.md Docs/les/FEEDBACK-2026-11-13-p3-perenos.md
```

Второй блок берёт лесные пути целиком — это безопасно: в `main` они равны ветке, кроме файлов §1.2–§1.3 (проверено
диффом на `62477822`); третий блок убирает шесть архивных тестов, которых в `main` нет.

## 3. Что не едет

| что | почему | где лежит |
|---|---|---|
| `Models/Canyon`, `Resources/Canyon`, `Models/Aqueduct`, `Resources/Aqueduct` | каньон и акведук заморожены, в первой зоне выключены | тег `archive/les-zamorozka-2026-11-12` |
| `G7FrameMetadataTests`, `CanyonGateTests`, `Aqueduct{Camera,Seed,WaterCatalog,Water}Tests` | читают ассеты или кадры из архива | тег |
| кадры 10–17 (37 МБ) | не срезы первой зоны | тег и `Chimera_game/_refs-local/les/kadry/` |
| агенты каньона, акведука, G7, S4, станции (`AqueductAgent`, `CanyonAgent`, `G7Shot`, `S4Shot`, `PavilionShot`, `StationLoopProbe`, `LabPlanProbe`, `ForestVerify`, `G7Accept`, `PlaytestFix`, `DrawLoopGraphs.py`) | инструменты замороженного или одноразовые | ветка и тег |
| прочие спеки, планы, ресёрч и арт-документы леса (~40), отчёты `Диаграммы/Лес/ВЕРИФИКАЦИЯ*`, `ГЕОГРАФИЯ.md` | П-4: им нужна сверка ссылок на кадры 10–17 и строк указателя | ветка |
| ветковые версии `.claude/skills/*`, `.opencode/agents/*`, нелесных документов (`АНАТОМИЯ_ТЕЛА`, `Паспорта/Змея.md`, `tools/anatomiya_tela.py`, `models/POSTAVKA-…`) | не лес; по решению П-4 — версия `main` | — |
| **шаги 2–4 спеки логова** | кода нет | спека §6 |

## 4. Что проверить после переноса

1. Гейт: EditMode — падений не жду: на ветке их нет, а из того, что не едет, в EditMode только архивный сторож G7.
2. Руками PlayMode: `ForestPavilionFirstZoneTests`, `ClimateClockTests`, `SpawnerHookTests` — на ветке зелёные.
3. Сборка Windows: павильон первым — мир строится в `Awake` и запекает навмеш; первый запуск сборки покажет время
   загрузки, это ещё не мерили.
