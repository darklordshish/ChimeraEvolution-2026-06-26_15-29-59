# #2A Провизия приёмов из данных — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация каждой задачи = пользователь запускает Play и репортит; **коммит ТОЛЬКО после подтверждения плейтестом**. Claude пишет C#, пользователь — действия в редакторе (бутстрап видов, Play). Работаем на `main`, без ворктри. Шаги — чекбоксы `- [ ]`.

**Спека:** `Docs/superpowers/specs/2026-07-29-2a-provizia-priyomov-v-dannye.md` (design-детали там, здесь — порядок и код).

**Goal:** перенести провизию захвата/клубка/массы из психик/префабов в данные тела; таран-по-массе.

**Architecture:** `CreatureBody.Recompute` провизионит компоненты-приёмы из флагов органа/шасси (как уже делает для игрока); психики только драйвят. Поведение-сохраняюще, кроме намеренного таран-по-массе.

**Стек:** Unity 6 (6000.4.7f1), C#, партиал `CreatureBody`. Гоча: новое `[SerializeField]`/поле в ассете приходит 0 — читать 0 как «не настроено».

Порядок = спека §9. Каждая задача — отдельный тематический коммит после плейтеста.

---

### Задача 1: Захват в данные

**Files:**
- Modify: `Assets/_Chimera/Scripts/Player/SpeciesSO.cs` — `Organ.constrictStage`
- Modify: `Assets/_Chimera/Scripts/Player/CreatureBody.Expression.cs` — `Contribution` несёт кап; `Express` считает
- Modify: `Assets/_Chimera/Scripts/Player/CreatureBody.cs` — `Recompute`: кап + провизия `Constrict` для NPC
- Modify: `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs` — значения органов
- Modify: `Assets/_Chimera/Scripts/Enemies/{Wolf,Hedgehog,Snake}Psyche.cs` — убрать `SetMaxStage`, тело владеет капом

- [ ] **1.1 Поле органа.** В `SpeciesSO.cs`, класс `Organ`, рядом с `nativeChassis`:
```csharp
public int constrictStage; // РОДНАЯ сила захвата органа (1–3): снейк Хвост 3, волк/ёж челюсть 1. 0 = не грэпл-орган.
                           // Кап = нативен для шасси ? constrictStage : min(2, constrictStage) — обобщает старое native?3:2
```

- [ ] **1.2 Кап в Express (per-орган эффективный кап, Sup берёт max).** В `CreatureBody.Expression.cs`:
  - В `struct Contribution` заменить `bool constrictNative` на `int constrictCap;` (эффективный кап уже посчитан).
  - В `Sup`: `constrictCap = Mathf.Max(a.constrictCap, b.constrictCap)` (вместо OR по `constrictNative`).
  - В `Express`, где считался `constrictNative`, посчитать кап:
```csharp
// эффективный кап захвата этого органа: нативен для шасси → полная сила, чужой → min(2, сила).
// 0 у enablesConstrict-органа = «не настроено» → дефолт 3 (старое нативное поведение); после бутстрапа не встречается
int cStage = w.constrictStage > 0 ? w.constrictStage : 3;
bool cNative = chassis != null && w.nativeChassis == chassis.speciesName;
constrictCap = w.enablesConstrict ? (cNative ? cStage : Mathf.Min(2, cStage)) : 0,
```

- [ ] **1.3 Recompute: агрегат капа + провизия NPC.** В `CreatureBody.cs` `Recompute`:
  - В блоке суммирования групп добавить агрегат: `int constrictCap = 0;` и в цикле `constrictCap = Mathf.Max(constrictCap, c.constrictCap);` (убрать старый `constrictNativeOn`).
  - Игроцкий блок: `constrictAb.SetMaxStage(constrictNativeOn ? 3 : 2)` → `constrictAb.SetMaxStage(Mathf.Max(1, constrictCap))`.
  - Добавить провизию машины для NPC (у игрока — через `constrictAb` выше):
```csharp
// NPC-ПРОВИЗИЯ ЗАХВАТА: тело гарантирует машину и кап из данных (у игрока — PlayerConstrict).
// Психика перестала капить — только драйвит; get-or-add робастен к порядку Start/Awake
if (move == null && constrictOn)
{
    if (!TryGetComponent<Constrict>(out var grabM)) grabM = gameObject.AddComponent<Constrict>();
    grabM.SetMaxStage(Mathf.Max(1, constrictCap));
}
```
  - Удалить объявление/использование старого `constrictNativeOn` (заменено `constrictCap`).

- [ ] **1.4 Бутстрап значений.** В `SpeciesBootstrap.cs`:
  - Змея, «Хвост»: добавить `constrictStage = 3` (к существующим `enablesConstrict=true, nativeChassis="Змея"`).
  - Волк, «Пасть»: добавить `enablesConstrict = true, constrictStage = 1, nativeChassis = "Волк"`.
  - Ёж, «Цепкая пасть»: добавить `enablesConstrict = true, constrictStage = 1, nativeChassis = "Ёж"`.

- [ ] **1.5 Психики: снять капирование (тело владеет капом).**
  - `WolfPsyche.cs`, геттер `GrabMachine`: удалить строку `grabMachine.SetMaxStage(1);` (оставить get-or-add + `ConfigureHolder`).
  - `HedgehogPsyche.cs`, геттер `GrabMachine`: удалить `grabMachine.SetMaxStage(1);`.
  - `SnakePsyche.cs`, `Awake`: строку `if (!TryGetComponent(out constrictM)) constrictM = gameObject.AddComponent<Constrict>();` оставить (машина нужна ей рано), но убрать любое явное капирование если есть; кап теперь даёт тело. Комментарий «дефолт капа = 3» заменить на «кап даёт тело (constrictStage Хвоста)».

- [ ] **1.6 ПОЛЬЗОВАТЕЛЬ:** меню **Chimera → Создать дефолтные виды**, затем **Ctrl+S**. Дать редактору перекомпилировать, консоль без ошибок.

- [ ] **1.7 ВЕРИФИКАЦИЯ (плейтест):**
  - Змея душит как раньше — доходит до **ст.3** (партер/чок) дома.
  - Волк и ёж пинят добычу — **ст.1** (не защёлкивают).
  - **Игрок с надетой Пастью волка — хватает** (ст.1): новая «кража грэпла».
  - Срыв рывком, спасатель-удар (сбив на стадию), выдох-по-стамине — работают.
  - Массивную тушу (лось) любой хват держит на стадию слабее — как раньше.

- [ ] **1.8 КОММИТ (после подтверждения):**
```bash
git add Assets/_Chimera/Scripts/Player/SpeciesSO.cs Assets/_Chimera/Scripts/Player/CreatureBody.cs Assets/_Chimera/Scripts/Player/CreatureBody.Expression.cs Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs "Assets/_Chimera/Data/Змея.asset" "Assets/_Chimera/Data/Волк.asset" "Assets/_Chimera/Data/Ёж.asset" Assets/_Chimera/Scripts/Enemies/WolfPsyche.cs Assets/_Chimera/Scripts/Enemies/HedgehogPsyche.cs Assets/_Chimera/Scripts/Enemies/SnakePsyche.cs
git commit -m "фича: захват из данных — кап по constrictStage+nativeChassis, тело провизионит NPC"
```

---

### Задача 2: Клубок в данные

**Files:**
- Modify: `SpeciesSO.cs` — `Organ.enablesCurl`
- Modify: `CreatureBody.Expression.cs` — `Contribution.curl`; `Express`
- Modify: `CreatureBody.cs` — `Recompute` провизия `CurlDefense` + `SetCurl`
- Modify: `SpeciesBootstrap.cs` — новый `chassisOnly`-орган ежа
- Modify: `Enemies/HedgehogPsyche.cs` — убрать `AddComponent<CurlDefense>`

- [ ] **2.1 Поле органа.** `SpeciesSO.cs`, `Organ`:
```csharp
public bool enablesCurl; // КЛУБОК (CurlDefense): свернуться в шар — броня↑, катание-таран. chassisOnly (форма целого тела)
```

- [ ] **2.2 Contribution + Express.** В `CreatureBody.Expression.cs`: добавить `public bool curl;` в `Contribution`; в `Sup` — `curl = a.curl || b.curl`; в `Express` (среди дискретных флагов) — `curl = w.enablesCurl,`.

- [ ] **2.3 Recompute провизия.** В `CreatureBody.cs`: агрегат `bool curlOn = false;` + в цикле `curlOn |= c.curl;`. Рядом с `SetThorns(...)` вызвать `SetCurl(curlOn);`. Добавить хелпер в семью `Set*`:
```csharp
// клубок как компонент: тело вешает/снимает по флагу enablesCurl (chassisOnly-орган ежа); психика драйвит
void SetCurl(bool on)
{
    if (on && !TryGetComponent<CurlDefense>(out _)) gameObject.AddComponent<CurlDefense>();
    else if (!on && TryGetComponent<CurlDefense>(out var c)) Destroy(c);
}
```

- [ ] **2.4 Бутстрап: орган ежа.** В `SpeciesBootstrap.cs`, массив органов Ежа — добавить (параллель змеиному «Тело-хвост»):
```csharp
new Organ { organName = "Игольчатое тело", slot = "Тело", hotkey = "7", cost = 4, chassisOnly = true, enablesCurl = true }, // ходовая ФОРМА шасси ежа: сворачивание в шар (клубок/катание). chassisOnly — аугументом не крадётся
```

- [ ] **2.5 Психика ежа.** `HedgehogPsyche.cs`, `Awake`: удалить `if (!TryGetComponent(out curl)) curl = gameObject.AddComponent<CurlDefense>();`. Поле `curl` брать лениво:
```csharp
CurlDefense curl { get { if (_curl == null) TryGetComponent(out _curl); return _curl; } }
CurlDefense _curl;
```
(если `curl` уже поле — переименовать в `_curl` и добавить ленивый геттер; драйв-код `curl.Curl()/Uncurl()/Hold()/RollTick()` не трогать.)

- [ ] **2.6 ПОЛЬЗОВАТЕЛЬ:** **Chimera → Создать дефолтные виды** + **Ctrl+S**. Перекомпиляция, консоль чистая.

- [ ] **2.7 ВЕРИФИКАЦИЯ (плейтест):** ёж сворачивается в клубок под давлением, катится на исходе дыхалки, разворачивается на спине — **как раньше**. (Dev-проверка по желанию: снять ежиное «тело» — клубок пропал.)

- [ ] **2.8 КОММИТ:**
```bash
git add Assets/_Chimera/Scripts/Player/SpeciesSO.cs Assets/_Chimera/Scripts/Player/CreatureBody.cs Assets/_Chimera/Scripts/Player/CreatureBody.Expression.cs Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs "Assets/_Chimera/Data/Ёж.asset" Assets/_Chimera/Scripts/Enemies/HedgehogPsyche.cs
git commit -m "фича: клубок из данных — флаг enablesCurl (chassisOnly), тело вешает CurlDefense"
```

---

### Задача 3: Масса в данные

**Files:**
- Modify: `SpeciesSO.cs` — `SpeciesSO.massive`
- Modify: `CreatureBody.cs` — `Recompute` провизия `Massive` + `SetMassive`
- Modify: `SpeciesBootstrap.cs` — `Лось.massive = true`
- (позже, отдельно) `Editor/MoosePrefab.cs` — убрать дубль `AddComponent<Massive>()`

- [ ] **3.1 Поле шасси.** `SpeciesSO.cs`, класс `SpeciesSO` (не `Organ`):
```csharp
public bool massive; // МАССИВНАЯ ТУША (свойство шасси): обхват на стадию слабее, нокбэк не берёт, стае нужно больше.
                     // Физ.характеристика шасси в ряду nativeChassis/мощи — не принадлежность к виду, а масса
```

- [ ] **3.2 Recompute провизия.** В `CreatureBody.cs`: рядом с прочими `Set*` вызвать `SetMassive(chassis != null && chassis.massive);`. Хелпер:
```csharp
// масса как маркер: тело вешает/снимает по флагу шасси (было на префабе). Потребители уже чекают GetComponent<Massive>
void SetMassive(bool on)
{
    if (on && !TryGetComponent<Massive>(out _)) gameObject.AddComponent<Massive>();
    else if (!on && TryGetComponent<Massive>(out var m)) Destroy(m);
}
```
*Примечание:* пока префаб лося тоже вешает `Massive` — будет дубль-маркер, он БЕЗВРЕДЕН (`GetComponent<Massive>()` вернёт один, потребители чекают на null). Чистку префаба делаем отдельным шагом 3.5.

- [ ] **3.3 Бутстрап.** `SpeciesBootstrap.cs`, лось: `moose.massive = true;` (рядом с `moose.eatsMeat = false;`).

- [ ] **3.4 ПОЛЬЗОВАТЕЛЬ:** **Chimera → Создать дефолтные виды** + **Ctrl+S**. Плейтест: лось остаётся массивным — не откидывается тараном/рогами, змея его не выбирает, стае нужно больше (как раньше). В рантайме на лосе виден `Massive`.

- [ ] **3.5 Чистка префаба (после подтверждения 3.4).** В `Editor/MoosePrefab.cs` удалить `go.AddComponent<Massive>();`. Пользователь: **Chimera → Создать префаб Лося** (перегенерит без дубля). *Зона моделей — согласовать коммит `.prefab` с пользователем (лось — генерируемый скриптом; префаб-меш обычно не коммитим).* Проверить: лось всё ещё массивен (теперь из флага).

- [ ] **3.6 КОММИТ** (скрипты + `Лось.asset`; `MoosePrefab.cs` — да; `.prefab` — по согласованию):
```bash
git add Assets/_Chimera/Scripts/Player/SpeciesSO.cs Assets/_Chimera/Scripts/Player/CreatureBody.cs Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs "Assets/_Chimera/Data/Лось.asset" Assets/_Chimera/Scripts/Editor/MoosePrefab.cs
git commit -m "фича: масса из данных — флаг massive на шасси, тело вешает Massive-маркер"
```

---

### Задача 4: Таран-по-массе

**Files:**
- Modify: `Assets/_Chimera/Scripts/Combat/Deliveries/ChargeAbility.cs` — отброс под свой `Massive`

- [ ] **4.1 Гейт отброса.** В `ChargeAbility.cs`, где считается `knockForce` для удара по цели (строка с `kb.Push(dir * knockForce)`): завязать силу на СВОЮ массивность. Дефолт — вариант (б): масса = полный отброс, лёгкое тело = слабый базовый толчок.
```csharp
// ТАРАН-ПО-МАССЕ: снести с ног = нужна масса. Массивная туша (лось) откидывает в полную силу,
// лёгкое тело (игрок на человечьем шасси) — лишь слабый толчок. Приём читает СВОЁ тело (§5 спеки)
bool selfMassive = GetComponent<Massive>() != null;
float appliedKnock = selfMassive ? knockForce : knockForce * lightChargeMult;
```
Добавить сериализованное поле рядом с `knockForce`:
```csharp
[SerializeField, Range(0f, 1f)] float lightChargeMult = 0.25f; // доля отброса у НЕмассивного таранящего (0 = только урон)
```
Заменить `kb.Push(dir * knockForce)` на `kb.Push(dir * appliedKnock)`. (Топотный `stompForce` при желании — тем же приёмом; для минимума не трогаем.)

- [ ] **4.2 ВЕРИФИКАЦИЯ (плейтест):**
  - Лось-таран (массивный) — **отбрасывает** цель, как раньше.
  - Таран игрока (человечье шасси + Лосиные ноги) — **толкает слабее** (≈25%). Число `lightChargeMult` покрутить на вкус.

- [ ] **4.3 КОММИТ:**
```bash
git add Assets/_Chimera/Scripts/Combat/Deliveries/ChargeAbility.cs
git commit -m "фича: таран-по-массе — полный отброс у массивных, слабый толчок у лёгких"
```

---

## Self-review (покрытие спеки)

- Спека §2 захват → Задача 1 ✅ · §3 клубок → Задача 2 ✅ · §4 масса → Задача 3 ✅ · §5 таран → Задача 4 ✅.
- Роли (§1-бис) — намеренно НЕ в плане (уходят в #4) ✅.
- `ConfigureHolder` остаётся в психиках (не трогаем) ✅ · вервольф-`Massive` на префабе (не трогаем) ✅.
- Гоча нулевого поля закрыта дефолтом в 1.2 (`constrictStage>0?...:3`) ✅.
- Дубль-`Massive` в переходный момент помечен безвредным (3.2), чистка префаба вынесена в 3.5 ✅.
