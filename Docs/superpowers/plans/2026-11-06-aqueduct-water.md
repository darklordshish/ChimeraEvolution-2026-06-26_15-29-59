# План: акведук как оформление водоёмов (2026-11-06)

**Статус:** ЧЕРНОВИК · код не начат.
**Спека:** [`2026-11-06-canyon-mesto-i-voda.md`](../specs/2026-11-06-canyon-mesto-i-voda.md) §1.
**Предмет:** акведук — инженерное оформление **водоёмов вообще**: река, озеро, искусственный водоём/водохранилище, водопад/сброс. Не только лаборатория.
**Границы:** каньон — отдельный объект/S2, в этот план **не входит**. Код и ассеты по этому плану сейчас не менять.

---

## 0. Что должно получиться

- Тип акведука выбирается по воде на сиде: **AW-R River / AW-L Lake / AW-RS Reservoir / AW-F Waterfall-spillway**.
- Акведук стоит **у воды/русла/озера**, не обязательно у станции.
- Нет воды — **`absent`**, а не пустой акведук и не «каньон».
- Есть опоры/быки, швы/зазоры, лоток, сброс, водопад; искусственный водоём — часть объекта.
- Озеро/река/водохранилище **не ломаются**: уровень, контур, меш, берег.
- 4 кадра: A1 вдоль, A2 3/4, A3 от воды, A4 опоры.
- 20 сидов: где есть вода — акведук; где нет — `absent`; озеро/река целы.
- G7 не используется: акведук — отдельная приёмка `AqueductGates`.

---

## 1. Шаги

### Task 0 — базовая линия и каталог воды

- [ ] Зафиксировать HEAD/рабочее дерево; не трогать каньон и S2.
- [x] Для 20 сидов собрать таблицу: есть ли `Hydro.lake`, `riverPts`, искусственный водоём/водопад; где берег/русло. Каталог: `Docs/les/AQUEDUCT-WATER-20.md` + `.csv`.
- [x] Ввести `WaterBodyKind` и `AqueductType` (данные): `River`, `Lake`, `Reservoir`, `Waterfall`, `None`. Код-данные: `Assets/_Chimera/Scripts/Forest/Water/AqueductWaterCatalog.cs`.
- [ ] Определить, что считается водой на сиде: озеро `lakeRadius>0`, река `riverPts.Count>0`, водохранилище — явный флаг/место, водопад — перепад/вода.
- [x] Выход: `AqueductWaterCatalog` + CSV/MD; мир не изменялся. Сторож: `AqueductWaterCatalogTests`.

### Task 1 — типы акведука (данные, не форма)

- [x] `AqueductType` как данные: `AW-R`, `AW-L`, `AW-RS`, `AW-F`; `WaterBodyKind` и `AqueductPlacementPlan`.
- [x] Для каждого типа: где ставится, какие опоры нужны, какой забор/сброс, какой water body не трогать — правила в `AqueductWaterCatalog`. Реализация опор — Task 2.
- [x] `None` → `absent`; `absent` не создаёт ни prefab, ни коллайдер, ни воду. Проверка: `AqueductPlacementTests`.
- [x] Сторож `AqueductTypeTests`; `AqueductPlacementTests` — выбор без геометрии.

### Task 2 — prefab: опоры, швы, лоток, сброс

- [ ] Модельная линия: текущий `CanyonAuthorGraded` (семантически акведук) получает **опоры/быки/пятовые блоки** до terrain.
- [ ] Швы/деформационные зазоры между секциями; лоток читается как жёлоб.
- [ ] Сброс/водопад: полотно/струя, приёмный плёс/пена.
- [ ] Для AW-RS — искусственный водоём/подпорная стенка как часть объекта, не озеро.
- [ ] Юбка/опора не висит; крепление к terrain не режет terrain.
- [ ] Выход: prefab + паспорт опор/секций/сброса; asset rename — отдельным решением.

### Task 3 — интеграция: посадка у воды

- [x] `ForestBuilder`/`DomeWorld`: флаг `buildAqueduct` отдельно от каньона; v1 A/C/B0/Pavilion не тронуты, `PavilionV2` включает.
- [x] Выбор `WaterBodyKind`/`AqueductType` по сиду; для `None` — `absent`; `AW-RS` — долг, не озеро.
- [x] Посадка: origin у воды/русла/озера, yaw по потоку; опоры на `SampleGround`; `DomeCanyonHandoff`/cut каньона не используется.
- [x] Озеро/река/водохранилище не режутся и не удаляются; акведук ставится у/над.
- [x] NavMesh: `AW_Collider` читаемый, печётся с соседними чанками; G0 PASS.
- [x] `wet/dry/absent`: `dry` гасит `Water`; `absent` не ставит prefab.

### Task 4 — камеры A1–A4

- [x] A1 вдоль: снят `A1-along-AW-F-1337.png`.
- [x] A2 3/4: снят `A2-3-4-AW-F-1337.png`.
- [x] A3 от воды: снят `A3-from-water-AW-F-1337.png`.
- [x] A4 опоры: снят `A4-supports-AW-F-1337.png`.
- [x] Подпись: в имени кадра есть type/seed/camera; позиции A1–A4 выводятся `AqueductCameraRig`.
- [x] Папка кадров: `Docs/Диаграммы/Лес/Кадры/17-Aqueduct/`.

### Task 5 — 20 сидов и приёмка

- [x] `AqueductSeedTests`: 20 сидов + 1337; `AqueductWaterCatalogTests` проверяет 20/20 фактическую постановку.
- [x] `AqueductTypeTests`: 4 типа; mapping; `absent`.
- [x] `AqueductPlacementTests`: выбор типа/`absent`; фактическая опора проверена `AqueductWaterTests` и 20-сидовым прогоном.
- [x] `AqueductWaterTests`: вода/`dry`, озеро/река целы; G0 зеркалит.
- [x] `AqueductCameraTests`: A1–A4.
- [x] `AqueductFrameTests`: 4 кадра; это **сооружение**, не трещина.
- [ ] `AqueductGates`: агрегат; G7 не участвует; `CanyonGateTests` не переиспользовать.


### Task 5 — факт после сборки воды

- [x] Открытый U-профиль лотка `AqueductTroughOpen`; вода в лотке и переливе стоит на `−0.30` от кромки (верх `4.45`, кромка `4.75`).
- [x] Водопад `AW-F`: полотно, перелив, три ступени, три струи, `AW_Foam`/`AW_FoamSplash`; A3 снят из воды у подножия.
- [x] `AqueductAgent.VerifyAll`: River PASS, Lake PASS, Reservoir PASS, Waterfall PASS; вода −0.30 от кромки у всех четырёх типов.
- [x] `AqueductFrameTests`, `AqueductWaterTests`, `AqueductCameraTests`, `AqueductWaterCatalogTests` 4/4, `AqueductSeedTests` 2/2 — зелёные.
- [ ] `AqueductGates` — по-прежнему не начат; G7 не используется.

### Task 5 — после приёмки кадров

- [x] Чёрная полоса A1: причина — `DomeWorld` оставлял `Terrain.useHandoffCanyon = true` и `DomeChunkMesh` резал клетки handoff-контура под акведуком. При `buildAqueduct` теперь `useHandoffCanyon = false`, `Terrain.canyon = default`, старый `DomeCanyonAuthorMesh` не строится; terrain под телом целый.
- [x] A3: сплошное полотно заменено 5 струями `AqueductFallStreams`, добавлены каменные полки/пена/плёс; камера от воды садится на terrain по лучу.
- [x] AW-F получает наклон по уклону: лип на земле, плёс у конца — на своей земле; `AW_WaterFall` масштаб 1.0, перекрытие с плёсом 0.07.
- [x] Вода тёмная (`AqueductWater` 0.10/0.24/0.30), без плоской яркой заливки; пена `AqueductFoam`.
- [x] `AqueductAgent.VerifyAll`: River/Lake/Reservoir/Waterfall — PASS; вода −0.30.
- [x] `AqueductWaterTests`, `AqueductCameraTests`, `AqueductFrameTests` — зелёные; `AqueductWaterCatalogTests` 4/4; `AqueductSeedTests` 2/2.
- [ ] `AqueductGates` — по-прежнему не начат; G7 не используется.

### Task 6 — документы и коммиты

- [ ] `СТАТУС`/`УКАЗАТЕЛЬ` — при смене этапа.
- [ ] Паспорт воды/типов — в docs, не в коде.
- [x] `spec_status.py` — чисто.

---

## 2. Файлы (кандидаты; код не начат)

### Модель/ассеты

- `Assets/_Chimera/Models/Canyon/CanyonAuthorGraded.prefab` — текущий prefab; **семантически акведук**, имя пока техническое.
- `Assets/_Chimera/Models/Canyon/Materials/CanyonWater.mat`, `CanyonFoam.mat`, `CanyonRock.mat`.
- `Assets/_Chimera/Resources/Canyon/CanyonAuthorGraded.prefab` — рантайм-копия; при переименовании — отдельное решение.

### Код (будущий)

- `Assets/_Chimera/Scripts/Forest/Water/AqueductType.cs` — тип/данные (кандидат).
- `Assets/_Chimera/Scripts/Forest/Water/AqueductPlacement.cs` — посадка/опоры (кандидат).
- `Assets/_Chimera/Scripts/Forest/Generation/ForestBuilder.cs` — флаг/профиль акведука.
- `Assets/_Chimera/Scripts/Forest/Generation/DomeWorld.cs` — хук, не cut каньона.
- `Assets/_Chimera/Scripts/Forest/Generation/DomeWaterMesh.cs` — только чтение/проверка целостности воды.

### Тесты

- `Assets/_Chimera/Tests/PlayMode/Forest/Worldgen/AqueductTypeTests.cs`
- `.../AqueductPlacementTests.cs`
- `.../AqueductWaterTests.cs`
- `.../AqueductCameraTests.cs`
- `.../AqueductSeedTests.cs`
- `.../AqueductGates.cs` (detector/gates)
- `.../AqueductFrameTests.cs` (если кадровый тест, PlayMode/Editor)
- `.../CanyonGateTests.cs` — **не трогать как каньон**; пометить legacy акведука или разделить отдельной задачей.

### Кадры/доки

- `Docs/Диаграммы/Лес/Кадры/17-Aqueduct/` — A1–A4.
- `Docs/les/` — описание типов/каталога воды; не сцена.

---

## 3. Тесты: что меряется, что кадром

| тест | что меряет | что кадром |
|---|---|---|
| **AqueductTypeTests** | `WaterBodyKind` → `AqueductType`; `None` → `absent`; 4 типа | A1/A3 |
| **AqueductPlacementTests** | origin/yaw, опоры до terrain, швы, лоток, нет виса; берег/русло не перекрыт | A1/A2/A4 |
| **AqueductWaterTests** | вода в лотке/сбросе/водопаде; озеро/река/водохранилище целы; нет воды на траве | A3 |
| **AqueductSeedTests** | 20 сидов: вода → акведук, нет воды → `absent`; ни один сид не ломает воду | нет |
| **AqueductCameraTests** | A1–A4: pos/target/yaw/pitch/FOV, подпись | A1–A4 |
| **AqueductFrameTests** | 4 кадра, «намеренное сооружение»: опоры/швы/лоток/сброс | только кадром |
| **AqueductGates** | агрегат: типы + посадка + вода + 20 сидов | не G7 |

**Не входит в AqueductTests:** K1–K4, S2, G7, `CanyonCut/Datum`; это CanyonGates.

---

## 4. Кадры

Папка: `Docs/Диаграммы/Лес/Кадры/17-Aqueduct/`

| файл | камера | что доказывает |
|---|---|---|
| `A1-along-<seed>.png` | вдоль лотка, 8–20 м | лоток идёт, 2–3 опоры/секции |
| `A2-3-4-<seed>.png` | 3/4, 25–45 м, 6–12 м | корпус/опоры/швы/тень |
| `A3-from-water-<seed>.png` | с воды/из-под сброса, 10–20 м | вода/струя/плёс/пена |
| `A4-supports-<seed>.png` | низкая 3/4/сбоку, 8–15 м | быки/пяты до terrain, нет «воздуха» |

Для каждого типа (AW-R/AW-L/AW-RS/AW-F) — минимум один набор; если тип отсутствует на 20 сидах — это открытый вопрос, не «пустой кадр».

---

## 5. Коммиты

1. **План/спека** — эта спека/план (docs; решения двух объектов и типов).
2. **Каталог воды/типы** — `AqueductTypeTests`/данные (если нужен код).
3. **Prefab** — опоры/швы/лоток/сброс/паспорт (модельная линия).
4. **Интеграция** — посадка у воды, `absent`, сохранность озера/реки; без cut каньона.
5. **Камеры/кадры** — A1–A4, подписи.
6. **20 сидов/ворота** — `AqueductSeedTests`, `AqueductGates`, `СТАТУС`/`УКАЗАТЕЛЬ`.

Не смешивать акведук с каньоном/S2/G7 в одном коммите.

---

## 6. Роли

- **Модельная линия:** опоры/быки, швы, лоток, сброс, водопад, искусственный водоём; паспорт.
- **Unity-интегратор:** посадка у воды, тип, `absent`, NavMesh/коллайдер, сцена; форму не меняет.
- **Forest-warden:** `WaterBodyKind`, каталог 20 сидов, `Aqueduct*Tests`, `AqueductGates`, кадры, docs.
- **Геймдизайнер:** типы, где ставится, вода/сброс, 4 кадра, `absent`; каньон — отдельно.

---

## 7. YAGNI

- Не делать 20 разных prefab акведука: один asset, тип — параметр.
- Не резать terrain под акведуком; не использовать cut каньона.
- Не делать каньон в этом плане; S2 — отдельно.
- Не смешивать G7 и акведук.
- Не менять озеро/реку ради акведука.

---

## 8. Открытые вопросы

1. Для AW-R/AW-L/AW-RS/AW-F — где забор и куда сброс; вода из лаборатории/озера/реки?
2. «Есть вода» на 20 сидах: какие пороги `lakeRadius`/`riverPts`/водохранилища?
3. Опоры/быки или низкие abutments для каждого типа?
4. Нужен ли `dry` у акведука, или он всегда wet, если есть вода?
5. Имя asset: оставить `CanyonAuthorGraded` или переименовать в `AqueductAuthorGraded` отдельным решением?
6. Какие 20 сидов дают каждый тип; если типа нет — `absent` или новый сид?
7. Кто и когда снимает A1–A4 и проверяет «намеренное сооружение»?
