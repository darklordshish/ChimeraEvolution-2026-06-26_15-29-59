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

## 3. Что проверить на П-2 руками (PlayMode, не в гейте)

PlayMode в репетиции не гонял: в общем прогоне его валит утечка сторожа, которую ты чинишь. После починки — прогон
лесных PlayMode на дереве П-2. Ожидаю красными тесты, которым нужны ассеты П-3:

- `CanyonGateTests` — префаб `Resources/Canyon/CanyonAuthorGraded` (в ветке он красный и с ассетами — §2.4
  прошлого письма);
- `Aqueduct*Tests` (7 классов) — префабы `Resources/Aqueduct/*` **[проверить]**: часть может строить акведук сама;
- тесты, ищущие сцену или игрока сцены, — **[проверить]** тем же прогоном.

Что из них красное — либо едет тестом с П-3, либо переходит в П-3 вместе с ассетами. Каньон и акведук по плану
заморожены и в первую зону не входят: их тесты логично держать в теге-архиве вместе с ассетами, а не в `main`
красными.

## 4. Что прошу

1. Подтвердить состав П-2 (пути минус три сценовых теста).
2. Сказать, когда сторож починен, — прогоню лесные PlayMode на дереве П-2 и пришлю список «едет / ждёт П-3».
