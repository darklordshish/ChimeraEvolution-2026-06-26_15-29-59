# План: Сенсорный слайс S1 — AlertState + SensoryProfile + Страх/Ярость-эффекты

> **Исполнение:** проект Unity БЕЗ автотестов — верификация плейтестом пользователя (CLAUDE.md: «не
> коммитить до подтверждения плейтестом»). Поэтому шаги — по проектному паттерну (см. `2026-07-04-termo-os...md`):
> файл-действие + «Проверка:» (что запустить/наблюдать) + коммит ПОСЛЕ плейтеста. Исполнять инлайн
> (`executing-plans`), не субагентами — только пользователь может плейтестить. Чекбоксы для трекинга.

**Спека:** `Docs/superpowers/specs/2026-07-13-sensorika-s1-alertstate.md` (утверждена, коммит a82027c).

**Цель:** свести волка и змею под общую 3-состоянчатую машину восприятия (`AlertState`) + пер-видовой
`SensoryProfile` (матрица «чувство × состояние»), Страх/Ярость сделать боевыми эффектами (холоднокровие =
иммунитет+рациональность), добавить личность особи — БЕЗ потери наигранного фила, заложив дешёвое расширение.

**Архитектура:** `AlertState`-компонент (Спок/Настор/Атака, движется перцепцией психики) + `Senses`-компонент
(держит `SensoryProfile`, отдаёт дальности с пер-состоянчатым множителем) на КАЖДОМ NPC. Психика скармливает
восприятие в `AlertState` и маппит 3 состояния на своё поведение (тонкая, закон 2). Страх — накопительный
компонент как `Venom`. Игрок — только эмиттер (heat/scent уже есть), без AlertState.

**Порядок срезов:** каркас-волк → каркас-змея → профиль-данные → рычаг-множители → эффекты Страх/Ярость →
личность. Каждый срез играбелен и плейтестится отдельно; фил сохраняем, рычаг крутим ПОСЛЕ подтверждения миграции.

---

## Структура файлов

**Создать:**
- `Assets/_Chimera/Scripts/Senses/AlertState.cs` — компонент: enum `Alert {Calm,Wary,Attack}`, `Observe(target,cue)` с затуханием, `State`.
- `Assets/_Chimera/Scripts/Senses/SenseKind.cs` — enum `SenseKind {Sight,Thermal,Scent}` + `SenseChannel` (данные: range/acuity/множители по состоянию).
- `Assets/_Chimera/Scripts/Senses/Senses.cs` — компонент: держит `SensoryProfile` (набор `SenseChannel`), читает свой `AlertState`, отдаёт `Range(kind)`/`Acuity(kind)` с множителем состояния.
- `Assets/_Chimera/Scripts/Combat/Fear.cs` — накопительный статус-эффект (аналог `Venom`): величина, порог храбрости, rout, затухание, иммунитет холоднокровных.
- `Assets/_Chimera/Scripts/Enemies/Personality.cs` — личность особи (храбрость/агрессия/любопытство, ролл на спавне).

**Модифицировать:**
- `Enemies/WolfPsyche.cs` — маппинг Engaged/Alerted/wander → AlertState; ranges → Senses; rout/Frighten/AddFear → Fear-эффект.
- `Enemies/SnakePsyche.cs` — засада/лур/атака → AlertState; ranges → Senses; CheckFlee = рациональное отступление.
- `Enemies/WerewolfPsyche.cs` — на AlertState/Senses (соло-босс, тот же каркас; врождённая ярость остаётся).
- `Combat/Hit.cs`, `Combat/HitEffect.cs` — ветки `Fear`/`Rage` в словаре.
- `Combat/Rage.cs` — иммунитет холоднокровных к ВНЕШНЕЙ ярости.
- `Senses/Perception.cs` — хелперы принимают дальность извне (уже так у `SeesThermal`); добавить перегрузки, где нужно.
- Генераторы префабов `Enemies/…Prefab.cs` (Wolf/Snake/Werewolf) — вешать `AlertState`/`Senses`/`Personality`, задать дефолт-профиль вида.

---

## Срез 1 — `AlertState` + миграция волка (фил-сохранная)

- [ ] **1. `Senses/AlertState.cs`** — компонент состояния восприятия:
  ```csharp
  public enum Alert { Calm, Wary, Attack }
  public class AlertState : MonoBehaviour {
      [SerializeField] float waryMemory = 4f;   // сколько держим Настороженность после потери зацепки
      [SerializeField] float attackMemory = 2f;  // сколько держим Атаку после потери цели
      float waryUntil, attackUntil;
      public Alert State { get; private set; }
      // психика зовёт КАЖДЫЙ кадр: подтверждённая цель? замеченная зацепка?
      public void Observe(bool target, bool cue) {
          if (target) attackUntil = Time.time + attackMemory;
          if (target || cue) waryUntil = Time.time + waryMemory;
          State = Time.time < attackUntil ? Alert.Attack
                : Time.time < waryUntil   ? Alert.Wary : Alert.Calm;
      }
  }
  ```
- [ ] **2. `WolfPsyche`** — до-создать `AlertState` в Awake (`if (!TryGetComponent(out alert)) alert = gameObject.AddComponent<AlertState>();`), поле `AlertState alert`.
- [ ] **3. `WolfPsyche.Update`** — на входе после ретаргета вычислить восприятие и скормить: `alert.Observe(Engaged, HasCue())`, где `Engaged` (уже есть) = подтверждённая цель, а `HasCue()` = `Alerted || Time.time < curiosityUntil || Time.time < rescueUntil || ScentField.Instance.TryFollow(...)` (зацепки, что СЕЙЧАС ведут к Настороженности). Вынести проверку следа в `HasCue`, чтоб не звать дважды.
- [ ] **4. `WolfPsyche`** — заменить разрозненные ветки поведения на `switch (alert.State)`: `Attack` = текущая ветка `Engaged` (жетоны/укус/прыжок/захват/окружение), `Wary` = текущая ветка `!Engaged` (идёт на alert/rescue/scent/curiosity, крадётся), `Calm` = `nav.Wander`. Существующую логику НЕ переписывать — только перегруппировать под состояния (тот же код внутри веток).
- [ ] **Проверка:** волк играется как до S1 — гонит игрока (Attack), идёт на вой/след/гремок (Wary), в покое бродит (Calm). Ничего не сломалось.
- [ ] **5. Коммит** (после плейтеста): `git commit -m "S1 срез 1: AlertState-каркас, миграция волка (фил сохранён)"`

## Срез 2 — миграция змеи + рациональное отступление

- [ ] **1. `SnakePsyche`** — до-создать `AlertState` (как у волка), поле `alert`.
- [ ] **2. `SnakePsyche.Update`** — скормить: `alert.Observe(hasThermalTarget, hasProwlCue)`, где `hasThermalTarget` = есть `target` и `SeesThermal(...)` (текущее условие атаки), `hasProwlCue` = `Prowl` нашёл тёплую жизнь в `roamSenseRadius` (сейчас неявно). Маппинг: `Attack` = цель в термо + логика удара/лура-в-упор; `Wary` = прокрадывание-к-тёплому/оценка/лур издали; `Calm` = засада-неподвижность/блуждание-пустоты. Обхват/климб/карри — приоритетнее состояний (как сейчас, ранние `return`).
- [ ] **3. `SnakePsyche.CheckFlee`** — оставить механику, но в комментарии и структуре ОТДЕЛИТЬ смысл: это РАЦИОНАЛЬНОЕ решение психики (расклад безнадёжен → выхожу к стене), НЕ эффект Страха. Кода менять не нужно — змея холоднокровна, Страх (срез 5) к ней не липнет по определению; отступление остаётся её собственным.
- [ ] **Проверка:** змея играется как до — засада/лур/бросок/обхват/климб/утаскивание/бегство от стаи; прокрадывание в стелсе.
- [ ] **4. Коммит:** `git commit -m "S1 срез 2: змея на AlertState; CheckFlee = рациональное отступление (не Страх)"`

## Срез 3 — `SensoryProfile` (данные) + дальности через `Senses`

- [ ] **1. `Senses/SenseKind.cs`**:
  ```csharp
  public enum SenseKind { Sight, Thermal, Scent }   // будущее: Hearing — новая строка профиля с дефолтом 0
  [System.Serializable] public class SenseChannel {
      public float range = 0f;      // 0 = чувства нет
      public float acuity = 1f;     // порог чувствительности (S2 задействует; пока задел)
      public float calmMult = 1f, waryMult = 1f, attackMult = 1f; // множители дальности по состоянию
      public float For(Alert s) => range * (s == Alert.Attack ? attackMult : s == Alert.Wary ? waryMult : calmMult);
  }
  ```
- [ ] **2. `Senses/Senses.cs`** — компонент: сериализованные `SenseChannel sight, thermal, scent;` (матрица-способно; будущие сенсы = новые поля), читает `AlertState`:
  ```csharp
  AlertState alert;
  void Awake(){ TryGetComponent(out alert); }
  SenseChannel Ch(SenseKind k)=> k==SenseKind.Sight?sight : k==SenseKind.Thermal?thermal : scent;
  public float Range(SenseKind k)=> Ch(k).For(alert!=null?alert.State:Alert.Wary);
  public float Acuity(SenseKind k)=> Ch(k).acuity;
  ```
- [ ] **3. `WolfPsyche`** — до-создать `Senses`; заменить использования `sightRange`→`senses.Range(Sight)`, `scentRange`→`senses.Range(Scent)`. Старые поля можно оставить как источник дефолта или снести (см. шаг 5).
- [ ] **4. `SnakePsyche`** — `thermalRange`→`senses.Range(Thermal)`, `roamSenseRadius` пока оставить полем психики (это радиус ПОИСКА, не строгий сенс — не гнать в матрицу без нужды, YAGNI). Термо-строку профиля завести.
- [ ] **5. Генераторы префабов Wolf/Snake/Werewolf** — вешать `Senses`, задать профиль вида дефолтом (волк: sight range = прежний sightRange 25, scent = 16, thermal 0; змея: thermal 14, sight малый, scent 0-слабый; все множители пока **1** — фил ещё не трогаем). Множители-в-1 = поведение идентично срезам 1–2. Пользователю: пересобрать префабы (`Chimera → Создать префаб X`) ИЛИ выставить `Senses` в инспекторе.
- [ ] **Проверка:** волк и змея играются ТОЧНО как в срезах 1–2 (множители =1 — дальности не изменились), но теперь через профиль.
- [ ] **6. Коммит:** `git commit -m "S1 срез 3: SensoryProfile + Senses; дальности через профиль (множители=1, фил цел)"`

## Срез 4 — включить рычаг: пер-состоянчатые множители

- [ ] **1. Профили в префабах** — выставить множители: Спокойствие ~0.6 (расслаблен), Настороженность ~1.2 (обострён), Атака = фокус (~1.0, дальность держится на цель). Крутить в инспекторе `Senses` (сериализовано — тюнинг без кода).
- [ ] **2. `AlertState`** (если нужно для читаемости) — экспонировать `State` в `DebugHud`-строку у выбранного NPC (по желанию; отладка живёт до UI).
- [ ] **Проверка (новый геймплей):** мимо СПОКОЙНОГО волка можно прошмыгнуть (радиус чувств сжат); стоит поднять зацепку (шум/след/показаться) → Настороженность → радиус растёт → находит. Змея-засадник в Спокойствии «слепее» — обходима.
- [ ] **3. Коммит:** `git commit -m "S1 срез 4: пер-состоянчатая сенсорика включена (прошмыгнуть мимо спокойного)"`

## Срез 5 — Страх и Ярость как эффекты

- [ ] **1. `Combat/Fear.cs`** — накопительный компонент (эталон — `Venom`):
  ```csharp
  public class Fear : MonoBehaviour {
      [SerializeField] float decayPerSec = 0.5f;   // затухание величины
      [SerializeField] float routDuration = 2.5f;  // бегство при срыве
      float magnitude, routUntil, threshold; Rage rage; Health health;
      void Awake(){ TryGetComponent(out rage); TryGetComponent(out health);
          threshold = 3f; /* переопределит Personality в срезе 6 */ }
      public bool IsRouting => Time.time < routUntil;
      public void SetThreshold(float t)=> threshold = t;
      public void Add(float amount){
          if (GetComponent<ColdBlooded>()!=null) return;      // холоднокровный не боится
          if (rage!=null && rage.IsEnraged) return;            // ярость не боится
          magnitude += amount;
          if (magnitude >= threshold){ routUntil = Time.time + routDuration; magnitude = 0f; }
      }
      void Update(){ if (magnitude>0) magnitude = Mathf.Max(0, magnitude - decayPerSec*Time.deltaTime); }
  }
  ```
- [ ] **2. `Combat/HitEffect.cs` + `Combat/Hit.cs`** — ветка `Fear(amount)`: `case Fear → target.GetOrAdd<Fear>().Add(amount)` (холоднокровие/ярость гейтят внутри `Add`). По образцу `Venom`.
- [ ] **3. `WolfPsyche`** — заменить самопальный Страх на эффект: `AddFear()` (гибель собрата) → `fear.Add(1)`; `Frighten(dur)` (вой игрока, дальнее кольцо) → `fear.Add(большая величина)`; `Routing` → `fear.IsRouting` (плюс существующая ярость-перебивка уже в `Fear.Add`). Снести поля `fear`/`fearThreshold`/`routUntil` — их роль теперь в компоненте. `CalmRout()` (вой вожака гасит) → сброс `Fear` (метод `Fear.Calm()`).
- [ ] **4. `PlayerHowl`** (дальнее кольцо) — вместо `WolfPsyche.Frighten` слать `Hit`-эффект `Fear` (или оставить прямой вызов `fear.Add` — что чище по коду). Ближнее кольцо (стан) не трогаем.
- [ ] **5. `Combat/Rage.cs`** — в точке ВНЕШНЕГО навешивания (`Enrage`/`Rally`/`EnrageFor`) добавить гейт `if (GetComponent<ColdBlooded>()!=null) return;` — холоднокровных чужой яростью не раскачать. Врождённую ярость вервольфа (ставится своим генератором/психикой как `permanent`) не трогаем.
- [ ] **6. Префабы** — `Fear` до-создаётся `RequireComponent`/психикой; проверить, что у змеи `ColdBlooded` глушит (Страх не липнет). Вешать в Awake психик.
- [ ] **Проверка:** стая ломается ВРАЗНОБОЙ (у каждого свой порог); гибель собратьев накапливает страх → бегут; вой игрока (дальнее кольцо) пугает; вой вожака гасит; **змею НЕ запугать** (холоднокровна); яростный волк не боится.
- [ ] **7. Коммит:** `git commit -m "S1 срез 5: Страх (накопительный) и Ярость как эффекты; холоднокровие = иммунитет"`

## Срез 6 — личность особи

- [ ] **1. `Enemies/Personality.cs`** — компонент, ролл на спавне (устойчиво, как `SpawnVariance`):
  ```csharp
  public class Personality : MonoBehaviour {
      [SerializeField] Vector2 braveryRange = new(2,5);     // порог Страха
      [SerializeField] Vector2 aggressionRange = new(0.85f,1.15f); // множитель охоты лезть в Атаку
      [SerializeField] Vector2 curiosityRange = new(0.8f,1.2f);    // охота идти на зацепку
      public float Bravery{get;private set;} public float Aggression{get;private set;} public float Curiosity{get;private set;}
      void Awake(){ Bravery=Random.Range(braveryRange.x,braveryRange.y);
          Aggression=Random.Range(aggressionRange.x,aggressionRange.y);
          Curiosity=Random.Range(curiosityRange.x,curiosityRange.y); }
  }
  ```
- [ ] **2. Проводка:** `Fear.SetThreshold(personality.Bravery)` в Awake психики (заменяет `RollPanicThreshold`). `AlertState` — по желанию принять `aggression`/`curiosity` как множители на `attackMemory`/`waryMemory` ИЛИ психика применяет их к своим порогам эскалации (агрессия ↑ → раньше Атака; любопытство ↑ → охотнее Wary на зацепку). Держать МИНИМАЛЬНО (YAGNI): достаточно Bravery→Fear + один из aggression/curiosity в один порог.
- [ ] **3. Игрок** — детерминирован (Personality не вешаем; порог/агрессия неактуальны, у игрока нет AlertState).
- [ ] **4. Префабы** — `Personality` в Awake психик волка (и опц. змеи — у неё «личность» беднее: холодная).
- [ ] **Проверка:** особи в стае ведут себя чуть по-разному — кто-то ломается первым, кто-то лезет агрессивнее, кто-то охотнее идёт проверять гремок. «Колесо жизни».
- [ ] **5. Коммит:** `git commit -m "S1 срез 6: личность особи (храбрость/агрессия/любопытство)"`

## Финальный проход

- [ ] **1. `CONSTRUCTOR_GUIDE.md`** — секция «Психика и восприятие»: AlertState (3 состояния), SensoryProfile (матрица, ленивое наполнение), Страх/Ярость-эффекты + холоднокровие, личность; **как добавить вид/чувство** дёшево (профиль-данные + дефолт-строка). Обновить таблицу существ.
- [ ] **2. Память** — `chimera-sensory-slice.md`: S1 = СДЕЛАНО, свернуть в указатель на спеку/план; следующий = S2.
- [ ] **3. Коммит доков** отдельно от кода.

## Self-review (проверил план против спеки)

- **Покрытие спеки:** AlertState 3-состоянчатый (срез 1) ✓; SensoryProfile 2 стороны/матрица (срез 3, излучение — задел в профиле, богатеет в S2) ✓; пер-состоянчатый рычаг (срез 4) ✓; Страх накопительный + Ярость эффекты + холоднокровие-иммунитет (срез 5) ✓; рациональность змеи (срез 2) ✓; личность (срез 6) ✓; миграция без потери фила (множители=1 до среза 4, плейтест каждого) ✓; путь расширения (финал-док + профиль-данные) ✓.
- **Плейсхолдеры:** нет; код-сигнатуры конкретны, «Проверка» — что наблюдать.
- **Согласованность типов:** `Alert`/`SenseKind`/`SenseChannel.For(Alert)`/`Senses.Range(SenseKind)`/`Fear.Add/IsRouting/SetThreshold`/`Personality.Bravery` — имена сквозные.

## Осознанно НЕ делаем (границы S1)

- Коллективный нюх/триангуляция + эмиссия следа NPC + per-perceiver выдыхание = **S2** (профиль лишь готов под это: `acuity`-поле, эмиттер-сторона).
- Кросс-видовой грэб = **S3**; чужеродность-химеры (источник Страха/Ярости от состава тела) = **S4**.
- Богатое наполнение матрицы (острота, шум, зрение-варианты) — с приходом ежа/совы (правило трёх).
- Игрок на AlertState/Senses-восприятие — у игрока сенсорика через органы-флаги; он ЭМИТТЕР (heat/scent уже есть).
- Перенос профиля в `SpeciesSO`/bootstrap — пока на префабном `Senses` (тюнинг в инспекторе); миграция в SpeciesSO = YAGNI до нужды.
