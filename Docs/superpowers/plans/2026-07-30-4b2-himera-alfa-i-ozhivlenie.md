# #4b-2 Химера-альфа + оживление тест-химеры — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация = пользователь запускает Play и репортит; **коммит только после плейтеста**. Claude пишет C#. `main`. Шаги — чекбоксы `- [ ]`.

**Спека:** `Docs/superpowers/specs/2026-07-30-4b2-himera-alfa-i-ozhivlenie.md`.

**Goal:** тест-химера оживает в дженерик-NPC (ходит/дерётся) и получает психику ХИМЕРУ-АЛЬФУ (охота на всё, арсенал от состава); диспатч по идентичности; замах читается телеграфом, тинт-по-составу держится.

**Architecture:** спавнер до-навешивает min-стек живого NPC (CharacterController + Health + базовый укус) ДО `CreatureBody` (его `Recompute` задаёт HP/урон); после сборки состава роутер `PsycheDispatch.Attach` вешает `ChimeraAlphaPsyche` и пере-раздаёт статы. Движение — CC + `NavMesh.CalculatePath` (агент не нужен). Телеграф-развязка: тело пере-базирует `Telegraph` в цвет-по-составу.

**Tech Stack:** Unity 6, C#, `CreatureBody` (партиал), `NavLocomotion`, `WindupAbility`-доставки, `Physics.OverlapSphereNonAlloc`.

---

## Уточнения по плейтесту

**Кин-фильтр — СОХРАНЁН, исправлено направление.** Спека §2.2 предлагала `Regard(other,me) < Weak` — неверное направление: `Regard(other, me)` смотрит `me.Chassis`, и чистый человек-NPC ложно читается кином истинной химеры на человечьем шасси. Первая попытка (убрать фильтр совсем) отвергнута пользователем как принципиально неверная: кин-фильтр — ядро универсальности CreatureBody (химера от вида отличается ТОЛЬКО составом; при эволюции химера может «провалиться в вид»). **Правильное направление — «признаю ли Я его»: `Regard(me, other) = me.Tier(other.Chassis)`** (сколько ЕГО вида ВО мне), как у ежа. Тогда истинная химера размыта → не признаёт никого → бьёт всех («монстр для всех» ВЫПАДАЕТ из состава, не хардкод); доминанта-волк → волков не бьёт; при эволюции фильтр поедет с составом. Альфа: `Regard(body, other) >= KinTier.Weak → свой, пропустить`.

**Доворот морды к цели.** Код §3.1 двигал альфу `nav.Move`, но не поворачивал `transform` — а `BiteAbility.OnTick` бьёт в конус относительно `transform.forward` → телеграф-замах есть, а конус мажет (урона нет). Добавлен доворот к цели (`RotateTowards`, rotationSpeed 240) перед выбором атаки — как у волка/ежа.

**Уважение dev-призрака.** Радиус-скан `OverlapSphere` не спрашивал восприятие → химеры находили и атаковали игрока в режиме призрака. Точечно: в `Retarget` пропуск цели, если `Perception.PlayerGhost && GetComponent<PlayerController>()` (паттерн как в `Noise.cs`). Полноценная сенсорика альфы (профиль `Senses` + `Perception.Sees`: конусы/LoS/камуфляж) — #4b-3.

---

## File Structure

- **Modify** `Assets/_Chimera/Scripts/Combat/Feedback/Telegraph.cs` — метод `Rebase()` (переснять базовые цвета из текущего MPB).
- **Modify** `Assets/_Chimera/Scripts/Player/CreatureBody.Tint.cs` — `UpdateTint()` в конце зовёт `Telegraph.Rebase()`.
- **Modify** `Assets/_Chimera/Scripts/Player/CreatureBody.cs` — публичный `Refeed()` (пере-раздать статы рантайм-психике).
- **Modify** `Assets/_Chimera/Scripts/Debug/TestChimeraSpawner.cs` — оживление (min-стек до `CreatureBody`) + вызов `PsycheDispatch.Attach`.
- **Create** `Assets/_Chimera/Scripts/Enemies/ChimeraAlphaPsyche.cs` — психика-автомат.
- **Create** `Assets/_Chimera/Scripts/Enemies/PsycheDispatch.cs` — статический роутер.

Два коммита: (1) телеграф-развязка; (2) оживление + альфа + диспатч.

---

## Задача 1: Телеграф-развязка (замах не стирает тинт-по-составу)

**Files:** Modify `Combat/Feedback/Telegraph.cs`, `Player/CreatureBody.Tint.cs`

- [ ] **1.1 `Telegraph.Rebase()`.** В `Telegraph.cs` добавить метод (рядом с `Reapply`, стр. ~81):
```csharp
/// <summary>Переснять «родные» цвета из ТЕКУЩЕГО состояния рендереров (per-renderer _BaseColor из MPB).
/// Тело зовёт после покраски составом (tintComposition): иначе Telegraph держит СТАРТОВЫЙ материаловый
/// цвет (снят в Awake) и гасит замах В НЕГО, стирая тинт-по-составу. no-op до Awake (renderers ещё нет).</summary>
public void Rebase()
{
    if (renderers == null) return;
    for (int i = 0; i < renderers.Length; i++)
    {
        if (renderers[i] == null) continue;
        renderers[i].GetPropertyBlock(mpb);
        if (mpb.isEmpty) continue;                 // тело ещё не красило этот рендерер — держим материаловую базу
        baseColors[i] = mpb.GetColor(BaseColor);   // текущий цвет-по-составу становится «родным» для телеграфа
    }
}
```

- [ ] **1.2 Вызов из тела.** В `CreatureBody.Tint.cs`, метод `UpdateTint()` — В КОНЦЕ (после цикла покраски рендереров) добавить:
```csharp
        GetComponent<Telegraph>()?.Rebase(); // замах гаснет В ЦВЕТ-ПО-СОСТАВУ, а не в стартовый материал (развязка тинт×Telegraph)
```

- [ ] **1.3 Компиляция** — консоль без ошибок.

- [ ] **1.4 ПОЛЬЗОВАТЕЛЬ (регресс-чек):** перекомпиляция. **Play.** Помахай приёмами за игрока (и/или спровоцируй замах у волка). Убедись: телеграфы работают как раньше, ничего не сломалось. *(Полная проверка «тинт держится после замаха» — в финальном плейтесте на химере, Задача 4.)*

- [ ] **1.5 КОММИТ:**
```bash
git add Assets/_Chimera/Scripts/Combat/Feedback/Telegraph.cs Assets/_Chimera/Scripts/Player/CreatureBody.Tint.cs
git commit -m "фича: развязка Telegraph × тинт-по-составу (Rebase) (#4b-2)"
```

---

## Задача 2: Пере-раздача статов рантайм-психике (`CreatureBody.Refeed`)

**Files:** Modify `Player/CreatureBody.cs`

- [ ] **2.1 Публичный `Refeed`.** В `CreatureBody.cs` (ядро, рядом с `Configure`/`ExpandPool`) добавить:
```csharp
/// <summary>Пере-раздать статы компонентам (урон/скорость через OnBodyStats, HP и т.д.). Для психики,
/// НАВЕШЕННОЙ В РАНТАЙМЕ ПОСЛЕ сборки (диспатч добавляет её после Recompute — Feed→OnBodyStats до неё
/// не дошёл, урон/скорость не получены). Идемпотентно (просто пересчёт).</summary>
public void Refeed() => Recompute();
```
*(`Recompute` — в ядре; `Refeed` в том же классе видит его независимо от модификатора.)*

- [ ] **2.2 Компиляция** — без ошибок. (Плейтест — вместе с Задачей 4: `Refeed` зовёт диспатч.)

---

## Задача 3: `ChimeraAlphaPsyche` (дженерик-автомат)

**Files:** Create `Assets/_Chimera/Scripts/Enemies/ChimeraAlphaPsyche.cs`

- [ ] **3.1 Компонент психики.** Полный файл:
```csharp
using UnityEngine;

/// <summary>ХИМЕРА-АЛЬФА (#4b-2): универсальная психика-фолбэк для существ без доминантного вида
/// (истинная химера — кин ни к кому). Агрессивный апекс: охотится на ВСЁ живое, бьёт ЛЮБОЙ доставкой,
/// что дало тело (арсенал от состава). Дженерик — не знает видов, опрашивает тело (как ёж берёт bite/volley
/// «если орган дал»). Подход A брейншторма #4b-2: компактный автомат, БЕЗ базы-класса (её извлечём в #4b-3,
/// когда появятся видовые модули). Восприятие — радиус-скан (конусы зрения/слуха — #4b-3+).
/// Кин-фильтр НЕ применяем: «монстр для всех» (см. уточнение к спеке в плане).</summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavLocomotion))]
public class ChimeraAlphaPsyche : MonoBehaviour, IBodyStatConsumer
{
    [SerializeField] float scanRadius = 16f;       // радиус обнаружения живых
    [SerializeField] float meleeRange = 2f;        // ближе — ближняя атака
    [SerializeField] float rangedRange = 12f;      // дальше meleeRange, но в этом радиусе → дальняя (если есть)
    [SerializeField] float attackCooldown = 1.1f;
    [SerializeField] float wanderRadius = 10f;
    [SerializeField] float retargetInterval = 0.5f;

    // 0-гоча (сериализация): читаем 0 как «не настроено»
    float ScanRadius => scanRadius > 0f ? scanRadius : 16f;
    float MeleeRange => meleeRange > 0f ? meleeRange : 2f;
    float RangedRange => rangedRange > 0f ? rangedRange : 12f;
    float AttackCooldown => attackCooldown > 0f ? attackCooldown : 1.1f;
    float WanderRadius => wanderRadius > 0f ? wanderRadius : 10f;
    float RetargetInterval => retargetInterval > 0f ? retargetInterval : 0.5f;

    float moveSpeed = 4f;
    Health ownHealth;
    NavLocomotion nav;
    Stagger stagger;
    Knockback knockback;
    BiteAbility bite;      // базовая ближняя (гарантирует спавнер при оживлении)
    QuillVolley volley;    // дальняя — только если орган Иглы дал (может не быть)
    WindupAbility active;
    Transform target;
    Health targetHealth;
    float nextAttackTime, nextRetarget;

    static readonly Collider[] scanHits = new Collider[32];

    // тело кормит числами (урон/скорость) — как ёж; base-укус получает урон/яд/кровь
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed, float howlRange)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite != null) { bite.SetDamage(damage); bite.SetVenom(venom); bite.SetBleed(bleed); }
    }

    void Awake()
    {
        ownHealth = GetComponent<Health>();
        nav = GetComponent<NavLocomotion>();
        TryGetComponent(out stagger);
        TryGetComponent(out knockback);
        TryGetComponent(out bite);
        TryGetComponent(out volley); // нет органа-залпа → только ближний бой
    }

    void Update()
    {
        if (ownHealth == null || nav == null) return;

        if (knockback != null && knockback.IsActive)
        {
            if (active != null) { active.Abort(true); active = null; }
            return;
        }

        if (active != null)
        {
            if (stagger != null && stagger.IsStaggered) active.Abort(false);
            if (active.Tick() == AbilityRun.Running) return;
            active = null; nextAttackTime = Time.time + AttackCooldown;
            return;
        }
        if (stagger != null && stagger.IsStaggered) { nav.Move(Vector3.zero); return; }

        if (Time.time >= nextRetarget) { nextRetarget = Time.time + RetargetInterval; Retarget(); }

        if (target == null || targetHealth == null || targetHealth.Current <= 0)
        {
            nav.Move(nav.Arrive(nav.Wander(WanderRadius), moveSpeed)); // никого — бродим по навмешу
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (Time.time >= nextAttackTime)
        {
            // дальняя, если цель далеко и залп есть
            if (volley != null && dist > MeleeRange && dist <= RangedRange)
            {
                volley.SetTarget(targetHealth);
                if (volley.TryUse()) { active = volley; return; }
            }
            // ближняя вплотную
            if (dist <= MeleeRange && bite != null)
            {
                bite.SetTarget(targetHealth);
                if (bite.TryUse()) { active = bite; return; }
            }
        }
        nav.Move(nav.Arrive(target.position, moveSpeed, stopAt: MeleeRange * 0.9f)); // преследуем
    }

    // ближайший живой (кроме себя). Кин-фильтр не применяем — истинная химера враждебна всем
    void Retarget()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, ScanRadius, scanHits);
        float best = float.MaxValue; Transform bestT = null; Health bestH = null;
        for (int i = 0; i < hits; i++)
        {
            var other = scanHits[i].GetComponentInParent<Health>();
            if (other == null || other == ownHealth || other.Current <= 0) continue;
            float d = (other.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; bestT = other.transform; bestH = other; }
        }
        target = bestT; targetHealth = bestH;
    }
}
```

- [ ] **3.2 Сверить геттеры под фактический API (перед компиляцией):**
  - `Health.Current` — ёж читает `ownHealth.Current` (есть). Если живость иначе (`IsDead`/`IsAlive`) — заменить `targetHealth.Current <= 0` / `other.Current <= 0` соответственно.
  - `Knockback.IsActive`, `Stagger.IsStaggered` — так их зовёт `HedgehogPsyche` (есть).
  - `BiteAbility.SetDamage/SetVenom/SetBleed`, `QuillVolley` наследует `WindupAbility` (`SetTarget`/`TryUse`/`Tick`/`Abort`) — из `HedgehogPsyche.OnBodyStats` и `WindupAbility`.
  - `NavLocomotion.Arrive(dest, speed, arriveRadius=1.5, stopAt=0)` / `Wander(radius)` / `Move(v)` — есть.

- [ ] **3.3 Компиляция** — без ошибок. (Плейтест — Задача 4.)

---

## Задача 4: Диспатч + оживление в спавнере (живой монстр)

**Files:** Create `Assets/_Chimera/Scripts/Enemies/PsycheDispatch.cs`; Modify `Debug/TestChimeraSpawner.cs`

- [ ] **4.1 Роутер.** Полный файл `Enemies/PsycheDispatch.cs`:
```csharp
using UnityEngine;

/// <summary>ДИСПАТЧ ПСИХИКИ (#4b-2): по ДОМИНАНТНОЙ идентичности тела вешает психику-модуль. Пока
/// тривиальный — истинная химера и доминантные равно получают ХИМЕРУ-АЛЬФУ; доминантным логируем задел
/// (#4b-3 повесит их видовой модуль). Роутер переиспользуем: позже его зовут боссовость (#4b-4) и обычная
/// химеризация NPC. Звать ПОСЛЕ сборки состава (MostKin валиден); сам пере-раздаёт статы новой психике.</summary>
public static class PsycheDispatch
{
    public static void Attach(CreatureBody body)
    {
        if (body == null) return;
        var dom = body.MostKin(out var tier);
        if (dom != null)
            Debug.Log($"диспатч: доминанта {dom.speciesName} ({tier}) → видовой модуль (TODO #4b-3), пока альфа");
        else
            Debug.Log("диспатч: истинная химера (кин ни к кому) → химера-альфа");
        body.gameObject.AddComponent<ChimeraAlphaPsyche>();
        body.Refeed(); // психика навешена ПОСЛЕ Recompute — пере-раздать урон/скорость (иначе OnBodyStats её не застал)
    }
}
```

- [ ] **4.2 Оживление в спавнере.** В `TestChimeraSpawner.SpawnRandom` заменить блок создания тела. БЫЛО:
```csharp
        if (go.TryGetComponent<Collider>(out var col)) Destroy(col); // без физ-коллайдера-заглушки

        var body = go.AddComponent<CreatureBody>();
        var chassis = species[Random.Range(0, species.Length)];
        body.Configure(chassis, species, tintFromComposition: true); // ...
        body.ExpandPool(9999); // ...
```
СТАЛО:
```csharp
        // ОЖИВЛЕНИЕ: сфера → ходячий/дерущийся NPC. Порядок важен — CC/Health/укус ДО CreatureBody,
        // чтобы его Awake их нашёл, а Recompute задал HP (health.SetMaxHealth гейтится наличием Health)
        if (go.TryGetComponent<Collider>(out var col)) Destroy(col); // сферный коллайдер долой
        var cc = go.AddComponent<CharacterController>();             // и коллайдер, и мотор для NavLocomotion.Move
        cc.height = 1f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.5f, 0f);
        go.AddComponent<Health>();      // тело задаст Max в Recompute (applyVitals)
        go.AddComponent<BiteAbility>(); // базовая атака (гарантия хотя бы одной); Awake создаст и Telegraph

        var body = go.AddComponent<CreatureBody>();
        var chassis = species[Random.Range(0, species.Length)];
        body.Configure(chassis, species, tintFromComposition: true); // все виды — потенциальные доноры; красим по составу
        body.ExpandPool(9999); // ТЕСТБЕД: снять экономику пула (см. коммит 3696ecc)
```

- [ ] **4.3 Вызов диспатча.** В конце `SpawnRandom`, перед `return body;` (после цикла случайных `Install`):
```csharp
        PsycheDispatch.Attach(body); // по идентичности вешает психику (пока всегда альфа) + пере-раздаёт статы
        return body;
```

- [ ] **4.4 Компиляция** — консоль без ошибок.

- [ ] **4.5 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция. **Play** → дев-панель **«спавн (случайный состав)»** × несколько. *(Сцену/спавнер настраивать заново не надо — тот же объект из #4b-1.)*

- [ ] **4.6 ВЕРИФИКАЦИЯ (плейтест):**
  - Химера **оживает**: ходит по арене (не стоит столбом, не проваливается сквозь пол).
  - Видит игрока/NPC → **преследует и атакует** (укус вблизи; у кого залп игл — стреляет издали).
  - **Арсенал от состава:** химера с иглами — стреляет; без спец-органов — кусает.
  - **Телеграф:** замах мигает; после замаха тело возвращается в **цвет-по-составу** (не в серый) — проверка развязки Задачи 1.
  - **Монстр для всех:** истинная химера атакует и игрока, и волков, и лосей.
  - В консоли — лог диспатча («истинная химера → альфа» / «доминанта X → TODO модуль»).

- [ ] **4.7 КОММИТ:**
```bash
git add Assets/_Chimera/Scripts/Player/CreatureBody.cs \
        Assets/_Chimera/Scripts/Enemies/ChimeraAlphaPsyche.cs Assets/_Chimera/Scripts/Enemies/ChimeraAlphaPsyche.cs.meta \
        Assets/_Chimera/Scripts/Enemies/PsycheDispatch.cs Assets/_Chimera/Scripts/Enemies/PsycheDispatch.cs.meta \
        Assets/_Chimera/Scripts/Debug/TestChimeraSpawner.cs
git commit -m "фича: химера-альфа + оживление тест-химеры + диспатч (#4b-2)"
```

---

## Риски (из спеки §5, держать в уме при плейтесте)
- **Навмеш под сферой:** если проваливается/висит — подобрать `cc.center`/высоту спавна (`y=0.5`).
- **HP=полный после `SetMaxHealth`:** проверить, что `Health.Current` не 0 (иначе умирает мгновенно) — если так, `Health` инициализирует Current из Max сам; если нет — добавить явную инициализацию.
- **Telegraph.Rebase порядок:** `BiteAbility.Awake` создаёт Telegraph ДО `CreatureBody` (укус добавлен раньше) → к первому `UpdateTint` Telegraph уже есть, `Rebase` сработает.
- **Пустой арсенал** невозможен — базовый `BiteAbility` всегда есть.
- **Захват `Constrict`** в MVP-альфе НЕ используется (химера с захватом просто кусает) — дженерик-захват в #4b-3.

## Self-review (покрытие спеки)
- §2.1 оживление → Задача 4.2 ✅ · §2.2 альфа-автомат/арсенал → Задача 3 ✅ (кин-фильтр — см. «Уточнение к спеке») · §2.3 диспатч-роутер → Задача 4.1 ✅ · §2.4 телеграф-развязка → Задача 1 ✅.
- Порядок оживления (Health/укус до CreatureBody) + `Refeed` для рантайм-психики — Задачи 2, 4.2 ✅.
- Деферы (видовые модули/боссовость/конусы/захват) — не в задачах ✅.
- Типы/сигнатуры сверяются с фактическим API в 3.2 перед компиляцией ✅.
