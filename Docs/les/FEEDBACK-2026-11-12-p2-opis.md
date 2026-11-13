# Лесник → ГеймБоссу: опись пакета П-2 (код и тесты леса)

**Дата:** 2026-11-12 · **От:** лесная линия · **Кому:** ГеймБосс
**К чему:** пакет П-2 плана `Docs/reports/Срез-2026-11-12/РЕШЕНИЯ-лес.md` §2; продолжение
`FEEDBACK-2026-11-12-les-opis-vne-lesa.md`
**Что это:** опись и репетиция. В `main` ничего не публиковалось.

## Коротко

- **Пересечений с `main` нет:** после базы `52d73f6e` `main` не тронул ни одного файла по путям П-2.
- **Лес собирается на голом `main`** — без пакетов А–Г. Проверено репетицией: дерево, равное `main` везде, кроме
  путей П-2.
- **EditMode на этом дереве: 576 тестов, 565 прошли, 9 упали, 2 известных неопределённых.** Все девять падений —
  в трёх тестах, которые открывают лесные сцены. Сцены — это П-3, поэтому **эти три файла едут с П-3**, и тогда
  гейт П-2 зелёный.
- `Data/Forest/` и `Shaders/Dome/` в ветке совпадают с `main` — в П-2 их нет.

## 1. Что едет

Пути (из `les/runtime-builder`, ветка запушена):

```
Assets/_Chimera/Scripts/Forest/         (+ Forest.meta)
Assets/_Chimera/Tests/EditMode/Forest/  (+ Forest.meta)
Assets/_Chimera/Tests/PlayMode/Forest/  (+ Forest.meta)
```

**Кроме** трёх файлов (с `.meta`) — они в П-3:

```
Assets/_Chimera/Tests/EditMode/Forest/Worldgen/ForestSceneBaselineTests.cs
Assets/_Chimera/Tests/EditMode/Forest/Worldgen/ForestSceneCameraTests.cs
Assets/_Chimera/Tests/EditMode/Forest/Worldgen/ForestSceneParityTests.cs
```

Объём против `main` — 256 файлов с `.meta`, +26 543 / −321 строк, удалений нет:

| путь | новых | изменённых |
|---|---|---|
| `Scripts/Forest/` | 53 | 27 |
| `Tests/EditMode/Forest/` | 35 (с П-3 — 32) | 5 |
| `Tests/PlayMode/Forest/` | 22 | 3 |

Команда переноса на `main`:

```bash
git checkout les/runtime-builder -- Assets/_Chimera/Scripts/Forest Assets/_Chimera/Scripts/Forest.meta Assets/_Chimera/Tests/EditMode/Forest Assets/_Chimera/Tests/EditMode/Forest.meta Assets/_Chimera/Tests/PlayMode/Forest Assets/_Chimera/Tests/PlayMode/Forest.meta
```

```bash
git rm --cached Assets/_Chimera/Tests/EditMode/Forest/Worldgen/ForestSceneBaselineTests.cs* Assets/_Chimera/Tests/EditMode/Forest/Worldgen/ForestSceneCameraTests.cs* Assets/_Chimera/Tests/EditMode/Forest/Worldgen/ForestSceneParityTests.cs*
```

## 2. Почему лес собирается без чужих правок

Проверено в обе стороны:

- **лес → ядро.** Лесной код не зовёт ни одного символа, который есть только в ветковых правках вне леса:
  `CombatReach`, `InitEvolution`, `PsycheDispatch.Dominant/HasPsyche`, `IsIntentShowing`, `pathTargetMove`,
  защита от провала в `PlayerController` — ноль вызовов. `ChimeraFactory.Spawn` лес зовёт сигнатурой, которая
  есть в `main`.
- **ядро → лес.** Ядро `main` зовёт у леса `ForestClimate.RangeMult(k)`, `ScentLifetimeMult()`,
  `CueDurationMult()` без позиции, `DayNightCycle` (из `WorldClock`), `LairSite.IsNearHome/TryReserve`,
  `HandsBridge`, `TelegraphChannels`, `EmissionSetup`. В ветке все эти сигнатуры на месте: перегрузки с позицией
  добавлены рядом, старые не убраны.

Репетиция в папке леса: код и тесты вне путей П-2 — побайтно `main`, ассеты вне П-2 (сцены, модели, `Resources`,
`ProjectSettings`) — тоже `main`. Компиляция чистая, EditMode — как выше. Рабочая копия после репетиции
возвращена к HEAD.

**Оговорка про поведение.** «Собирается» не значит «работает как в ветке». Звери логов рождаются фабрикой
`main` — химеризуются ли они там, решает твой тест по пакету А. До его результата лес в `main` будет жить на
эволюции версии `main`.

## 3. PlayMode на дереве П-2 (после починки сторожа, `1080ab6c`)

Свежий `main` влит в ветку (`7a2899b9`; конфликт в `EmissionSetup.cs` решён версией `main` — ветка лишь склеивала
две проверки, `main` чинит саму мину). Репетиция повторена тем же способом: всё вне путей П-2 — `main`.

**224 теста: 213 прошли, 6 упали, 2 пропущены (`FactoryEvolutionTests`, пакет А под `Ignore`), 3 известных
неопределённых** (дрейф змеиных логов B0, 20 сидов G6 и кадры G7 — отдельными прогонами). Утечки сцены больше нет.

Все шесть падений — тесты, которым нужны префабы `Resources/` (ассеты П-3, каньон и акведук заморожены):

| класс | падение | нужен ассет |
|---|---|---|
| `CanyonGateTests` (2) | «нет авторского меша», «1337 должен идти graded-мешем» | `Resources/Canyon/CanyonAuthorGraded` |
| `AqueductCameraTests` | «нет акведука для камер» | `Resources/Aqueduct/*` |
| `AqueductSeedTests` | водопад акведука: 0 вместо 1 | `Resources/Aqueduct/*` |
| `AqueductWaterCatalogTests` | «акведук не поставлен: seed 1» | `Resources/Aqueduct/*` |
| `AqueductWaterTests` | «акведук должен быть один»: 0 | `Resources/Aqueduct/*` |

`AqueductFrameTests`, `AqueductPlacementTests`, `AqueductTypeTests` — зелёные без префабов, им ассеты не нужны.
Все остальные лесные PlayMode-тесты зелёные на `main`, включая спавнер и логова (`SpawnerHookTests`,
`HomeHomingTests`, `ForestBuilderRuntimeTests`, `ForestFragmentTests`).

**Предложение (ты согласился):** пять красных классов в `main` не едут — остаются в теге-архиве вместе с ассетами
каньона и акведука до разморозки. Тогда П-2 добавляет к исключениям пять файлов:

```
Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/CanyonGateTests.cs
Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductCameraTests.cs
Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductSeedTests.cs
Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductWaterCatalogTests.cs
Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductWaterTests.cs
```

Код каньона и акведука в `Scripts/Forest/` при этом едет: он за флагами, выключенными в первой зоне, и
компилируется без ассетов (`Resources.Load` вернёт `null`, тесты выше это и ловят).

## 4. Что прошу

1. Подтвердить состав П-2: пути минус три сценовых EditMode-теста (едут с П-3) и минус пять PlayMode-тестов
   каньона и акведука (остаются в теге-архиве).
2. После этого П-2 можно переносить: EditMode — гейт, лесные PlayMode — уже прогнаны выше.
