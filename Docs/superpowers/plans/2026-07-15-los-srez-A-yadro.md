# Лось — Срез A (ядро NPC): план реализации

> **Для агентных исполнителей:** РЕКОМЕНДУЕМЫЙ САБ-СКИЛЛ: `superpowers:subagent-driven-development` или `superpowers:executing-plans`. Шаги — чекбоксы (`- [ ]`).

**Goal:** Играбельный лось-NPC: массивный нейтрал, который пасётся/бродит, а по провокации (подошёл близко или ударил) идёт в закоммиченный **таран копытами**.

**Architecture:** По спеке `Docs/superpowers/specs/2026-07-15-los-design.md`, срез A. Тело = данные (`CreatureBody` + вид «Лось»), психика = тонкий код (`MoosePsyche`), таран = доставка `ChargeAbility` (наследник `WindupAbility`, по образцу `LeapAbility`). Массивность (`Massive`) через существующий маркер + резист нокбэка в `Knockback`. Полная лесенка предупреждений/берсерк — срез C, рёв и экосистема — срез D (свои планы).

**Tech Stack:** Unity 6 (6000.4.7f1), URP, CharacterController + `NavLocomotion` (NE NavMeshAgent), новый Input System.

**⚠️ Верификация — ПЛЕЙТЕСТОМ, не авто-тестами** (в проекте нет юнит-тестов; по `CLAUDE.md` проверка = пользователь запускает Play и репортит). Каждая задача: код (Claude) → шаги в редакторе (пользователь) → **чекпоинт-плейтест** → **коммит только после подтверждения «работает»**. Перекомпиляция во время Play обнуляет несериализованные поля — это НЕ баг, стоп Play → пересборка → новый Play.

---

## File Structure

- **Create** `Assets/_Chimera/Scripts/Combat/ChargeAbility.cs` — доставка «таран» (наследник `WindupAbility`).
- **Create** `Assets/_Chimera/Scripts/Enemies/MoosePsyche.cs` — психика лося (скелет: пастьба → провокация → таран).
- **Create** `Assets/_Chimera/Scripts/Editor/MoosePrefab.cs` — генератор префаба лося (по образцу `SnakePrefab`).
- **Modify** `Assets/_Chimera/Scripts/Combat/Knockback.cs` — резист нокбэка у `Massive`.
- **Modify** `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs` — вид «Лось» (данные органов).
- **Modify** `Assets/_Chimera/Scripts/Editor/ChimeraDevWindow.cs` — дев-кнопка «Спавн лося».

Числа во всех задачах — **placeholder, тюнинг игрой** (числовой баланс дефернут). Файлы новых скриптов создаём В UNITY (правый клик в Project → Create → C# Script) или пишем и даём Unity импортировать — `.meta` создаст сам.

---

## Task A1: `Massive` резистит нокбэк

**Files:** Modify `Assets/_Chimera/Scripts/Combat/Knockback.cs`

Единый чокпоинт откидывания — `Knockback.Push`. Гейт по `Massive` делает массивных неоткидываемыми ВЕЗДЕ (таран лося, человеческий пинок, рога) одной правкой. Следствие: **пинок больше не откидывает вервольфа** (он `Massive`).

- [ ] **Шаг 1: правка `Knockback.Push`**

```csharp
    public void Push(Vector3 velocity)
    {
        if (GetComponent<Massive>() != null) return; // массивную тушу не откинуть (вервольф, лось) — один механизм для всех источников
        velocity.y = 0f;
        vel = velocity;
    }
```

- [ ] **Шаг 2: плейтест-чекпоинт (пользователь).** Play → пинком (E) по вервольфу: **босс НЕ отлетает** (раньше отлетал). Обычных волков пинок откидывает как прежде. Если пинок вервольфа применяет нокбэк не через `Knockback.Push` — сообщи, добавлю гейт и туда.

- [ ] **Шаг 3: коммит (после подтверждения).**

```bash
git add Assets/_Chimera/Scripts/Combat/Knockback.cs
git commit -m "Massive резистит нокбэк (лось/вервольф не откидываются; пинок не откидывает босса)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task A2: вид «Лось» (данные органов)

**Files:** Modify `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs`

Лось — шасси NPC (как змея). Органы по каноничным слотам (см. спека §3): Копыто=Руки, Ноги, Глотка=Пасть, Слух=Чутьё, Сердце, Толстая шкура=Шкура. Рога (придаток) и рёв (`enablesHowl`) — в срезах A2/D, здесь НЕ добавляем. Донором игроку лось пока НЕ становится (донор-сторона отложена) — только создаём ассет.

- [ ] **Шаг 1: имя меню.** В `[MenuItem("Chimera/Создать дефолтные виды (Человек, Волк, Змея)")]` заменить текст на `"Chimera/Создать дефолтные виды (Человек, Волк, Змея, Лось)"`.

- [ ] **Шаг 2: блок вида «Лось».** Вставить ПЕРЕД `AssetDatabase.SaveAssets();` (после блока змеи, по образцу змеи):

```csharp
        // ── Лось: массивный травоядный-таран (NPC-шасси; экспрессия 0.5). Рёв/рога — срезы A2/D ──
        var moose = GetOrCreate("Лось");
        moose.speciesName = "Лось";
        moose.tint = new Color(0.42f, 0.32f, 0.22f); // тёмно-бурый
        moose.mutagenPool = 24;
        moose.organs = new[]
        {
            new Organ { organName = "Копыто",        slot = "Руки",   hotkey = "1", cost = 5, damage = 22, range = 1.8f }, // удар копытом — оружие
            new Organ { organName = "Лосиные ноги",  slot = "Ноги",   hotkey = "2", cost = 5, moveSpeed = 4.5f, dashSpeed = 16f }, // медленный, но таранит
            new Organ { organName = "Глотка",        slot = "Пасть",  hotkey = "5", cost = 4 }, // рёв — срез D (enablesHowl не ставим сейчас)
            new Organ { organName = "Слух",          slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f }, // слух/зрение — модуль слуха в срезе B
            new Organ { organName = "Лосиное сердце",slot = "Сердце", hotkey = "3", cost = 6, maxHp = 260, regen = 1f, regenOOC = 0f, atkCooldown = 0.5f }, // много HP
            new Organ { organName = "Толстая шкура", slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.35f }, // броня против ПРЯМОГО урона (не крови)
        };
        EditorUtility.SetDirty(moose);
```

- [ ] **Шаг 3: пользователь — прогнать генератор.** В Unity: меню **Chimera → Создать дефолтные виды (…, Лось)**, затем **Ctrl+S**. Проверить: появился `Assets/_Chimera/Data/Лось.asset` с 6 органами.

- [ ] **Шаг 4: коммит (после подтверждения, что ассет создался).**

```bash
git add Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs Assets/_Chimera/Data/Лось.asset Assets/_Chimera/Data/Лось.asset.meta
git commit -m "Вид Лось: данные органов (копыто/ноги/глотка/слух/сердце/шкура), шасси NPC" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task A3: `ChargeAbility` — доставка «таран»

**Files:** Create `Assets/_Chimera/Scripts/Combat/ChargeAbility.cs`

Наследник `WindupAbility` по образцу `LeapAbility`, но рывок ПО ЗЕМЛЕ (без дуги): замах (телеграф Charge) → закоммиченный рывок в направлении цели (зафиксировано в конце замаха) → при попадании в `hitRadius`: урон + `Knockback` (сам резистит `Massive`) + `Stagger`. Закоммичен как прыжок: мягкий срыв (стаггер) игнорит, жёсткий (нокбэк) рвёт.

- [ ] **Шаг 1: создать `ChargeAbility.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Доставка «таран»: замах (телеграф Charge) → закоммиченный рывок ВПЕРЁД по земле (без дуги прыжка) →
/// удар копытами по цели в hitRadius: урон + Knockback (резист у Massive — внутри Knockback) + Stagger.
/// Закоммичен как прыжок: мягкий срыв (стаггер) игнорит, жёсткий (нокбэк) рвёт. Дефолты — лось.
/// </summary>
public class ChargeAbility : WindupAbility
{
    [Header("Таран")]
    [SerializeField] float minRange = 4f;
    [SerializeField] float maxRange = 12f;
    [SerializeField] float chargeSpeed = 16f;
    [SerializeField] float duration = 0.7f;
    [SerializeField] int damage = 20;
    [SerializeField] float hitRadius = 1.8f;
    [SerializeField] float knockForce = 12f;   // отлёт цели (Knockback сам резистит Massive)
    [SerializeField] float staggerTime = 0.5f; // сбив цели при попадании

    public float MinRange => minRange; // психика читает окно дистанций тарана
    public float MaxRange => maxRange;

    bool charging, hit;
    float chargeEnd;
    Vector3 dir;

    protected override Color TelegraphColor => TelegraphColors.Charge;

    protected override AbilityRun OnTick()
    {
        if (!charging)
        {
            if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; }
            charging = true; hit = false;
            telegraph.Clear();
            chargeEnd = Time.time + duration;
            dir = DirToTarget();               // направление берём в последний кадр замаха
        }

        controller.Move(dir * chargeSpeed * Time.deltaTime);          // рывок вперёд
        if (!controller.isGrounded) controller.Move(Vector3.up * gravity * Time.deltaTime); // прижать к земле

        if (!hit && targetHealth != null && DistToTarget() <= hitRadius)
        {
            hit = true;
            var h = new Hit(ownHealth, transform.position);
            h.Apply(targetHealth, HitEffect.Damage(Mathf.RoundToInt(damage * DamageMult)));
            if (targetHealth.TryGetComponent<Knockback>(out var kb)) kb.Push(dir * knockForce); // Massive-цель Push проигнорит
            if (targetHealth.TryGetComponent<Stagger>(out var st)) st.Hitstun(staggerTime);
        }
        if (Time.time < chargeEnd) return AbilityRun.Running;

        charging = false;
        return AbilityRun.Done;
    }

    // таран закоммичен: стаггер (мягкий срыв) не рвёт; нокбэк (hard) рвёт
    public override void Abort(bool hard)
    {
        if (charging && !hard) return;
        charging = false;
        base.Abort(hard);
    }
}
```

- [ ] **Шаг 2: пользователь — дождаться компиляции Unity** (нет ошибок в Console). `TelegraphColors.Charge` уже есть в легенде.

- [ ] **Шаг 3: коммит (после подтверждения, что компилится).**

```bash
git add Assets/_Chimera/Scripts/Combat/ChargeAbility.cs Assets/_Chimera/Scripts/Combat/ChargeAbility.cs.meta
git commit -m "ChargeAbility: доставка «таран» (наземный закоммиченный рывок, урон+нокбэк+стаггер)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task A4: `MoosePsyche` — психика (скелет)

**Files:** Create `Assets/_Chimera/Scripts/Enemies/MoosePsyche.cs`

Скелет: пасётся/бродит (`NavLocomotion.Wander`), видит цель через конус (`Senses`, широкий полу-угол 100° — травоядный), провокация в срезе A ПРОСТАЯ (подошёл ближе `provokeRadius` ИЛИ ударил) → таран в окне дистанций → кулдаун. Читает статы тела (`IBodyStatConsumer`). Лесенка предупреждений/берсерк — срез C.

- [ ] **Шаг 1: создать `MoosePsyche.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Психика лося (СРЕЗ A — скелет): массивный нейтрал. Пасётся/бродит, игнорит игрока на дистанции;
/// провокация (подошёл слишком близко ИЛИ получил урон) → закоммиченный ТАРАН копытами → кулдаун.
/// Полная лесенка предупреждений + локальный берсерк — срез C. Рёв/экосистема — срез D.
/// Читает статы тела (IBodyStatConsumer); тело = CreatureBody на шасси «Лось».
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(ChargeAbility))]
[RequireComponent(typeof(Rage))]
[RequireComponent(typeof(SpawnVariance))]
public class MoosePsyche : MonoBehaviour, IBodyStatConsumer
{
    [Header("Восприятие")]
    [SerializeField] float sightRange = 20f;
    [SerializeField] float sightHalfAngle = 100f; // травоядный — ШИРЕ конус (панорама), короткий/мутный (острота — срез B)
    [SerializeField] float proximityRadius = 3f;

    [Header("Поведение")]
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float rotationSpeed = 200f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float wanderRadius = 14f;
    [SerializeField, Range(0f, 1f)] float grazeSpeed = 0.5f;
    [SerializeField] float provokeRadius = 5f;   // ближе — провокация (СРЕЗ A: упрощённо; лесенка — срез C)
    [SerializeField] float attackCooldown = 2.5f;

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth;
    NavLocomotion nav;
    ChargeAbility charge;
    Rage rage;
    SpawnVariance variance;
    AlertState alert;
    Senses senses;
    Personality personality;
    PlayerController playerCtl;
    Transform target;
    Health targetHealth;

    WindupAbility activeAbility;
    float nextAttackTime, verticalVel;
    bool provoked;

    float Speed => moveSpeed * (rage != null ? rage.SpeedMult : 1f) * (variance != null ? variance.SpeedMult : 1f);

    // тело-на-шасси кормит скорость; урон тарана остаётся на ChargeAbility (как урон прыжка у волка)
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed) => moveSpeed = bodyMoveSpeed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out charge)) charge = gameObject.AddComponent<ChargeAbility>();
        if (!TryGetComponent(out rage)) rage = gameObject.AddComponent<Rage>();
        if (!TryGetComponent(out variance)) variance = gameObject.AddComponent<SpawnVariance>();
        if (!TryGetComponent(out alert)) alert = gameObject.AddComponent<AlertState>();
        if (!TryGetComponent(out senses)) senses = gameObject.AddComponent<Senses>();
        senses.Seed(SenseKind.Sight, sightRange);
        senses.SeedViewAngle(SenseKind.Sight, sightHalfAngle);
        if (!TryGetComponent<HeatSignature>(out _)) gameObject.AddComponent<HeatSignature>(); // тёплый — виден термо
        if (!TryGetComponent<StunTint>(out _)) gameObject.AddComponent<StunTint>();            // статус «выключен»
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
        if (ownHealth != null) ownHealth.onDamaged.AddListener(() => provoked = true); // ударили — злимся (провокация)
        TryGetComponent(out personality); // личность вешает CreatureBody в своём Awake — читаем после
    }

    void Update()
    {
        if (target == null) { Settle(Vector3.zero); return; }

        if (knockback != null && knockback.IsActive) // нокбэк рвёт всё
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            return;
        }

        if (activeAbility != null) // активный таран тикает сам
        {
            if (stagger != null && stagger.IsStaggered) activeAbility.Abort(false); // таран закоммичен — игнорит мягкий срыв
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            nextAttackTime = Time.time + attackCooldown;
            return;
        }

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        Vector3 toT = target.position - transform.position; toT.y = 0f;
        float dist = toT.magnitude;
        bool inView = dist <= proximityRadius || Vector3.Angle(transform.forward, toT) <= senses.ViewHalfAngle(SenseKind.Sight);
        bool sees = dist <= senses.Range(SenseKind.Sight) && inView && Perception.HasLineOfSight(transform.position, target);

        if (sees && dist <= provokeRadius) provoked = true; // СРЕЗ A: подошёл слишком близко → провокация
        alert.Observe(provoked, sees);                      // кормим машину восприятия

        if (provoked && sees)
        {
            Vector3 dir = dist > 0.001f ? toT / dist : transform.forward;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            if (Time.time >= nextAttackTime && dist >= charge.MinRange && dist <= charge.MaxRange)
            { if (charge.TryUse()) activeAbility = charge; Settle(Vector3.zero); return; } // в окне → таранит
            Settle(nav.DirTo(target.position) * Speed); // не в окне — доводим дистанцию
            return;
        }

        // спокоен: пасётся/бродит
        Vector3 w = nav.DirTo(nav.Wander(wanderRadius));
        if (w.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(w), rotationSpeed * Time.deltaTime);
        Settle(w * Speed * grazeSpeed);
    }

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 m = horizontal; m.y = verticalVel;
        controller.Move(m * Time.deltaTime);
    }
}
```

- [ ] **Шаг 2: пользователь — дождаться компиляции** (нет ошибок). Проверить, что `IBodyStatConsumer`, `Perception.HasLineOfSight`, `AlertState`, `Senses`, `SenseKind`, `Personality`, `HeatSignature` резолвятся (все уже в проекте — те же, что у волка).

- [ ] **Шаг 3: коммит (после компиляции).**

```bash
git add Assets/_Chimera/Scripts/Enemies/MoosePsyche.cs Assets/_Chimera/Scripts/Enemies/MoosePsyche.cs.meta
git commit -m "MoosePsyche (скелет среза A): пастьба → провокация → таран копытами" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task A5: префаб лося + дев-спавн — ПЕРВЫЙ ИГРАБЕЛЬНЫЙ ЧЕКПОИНТ

**Files:** Create `Assets/_Chimera/Scripts/Editor/MoosePrefab.cs`; Modify `Assets/_Chimera/Scripts/Editor/ChimeraDevWindow.cs`

- [ ] **Шаг 1: создать `MoosePrefab.cs`** (по образцу `SnakePrefab`, крупный плейсхолдер-бокс + компоненты + `CreatureBody` на шасси Лось)

```csharp
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб лося (крупная капсула-плейсхолдер + компоненты). Меню: Chimera → Создать префаб Лося.
/// Тело на шасси «Лось» (CreatureBody × экспрессия 0.5). Массивный (Massive): обхват/нокбэк по нему слабее/нет.
/// Таран — ChargeAbility. Dev-спавн берёт этот префаб. Editor-only.
/// </summary>
public static class MoosePrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Moose.prefab";
    const string MatPath = "Assets/_Chimera/Materials/MooseBody.mat";

    [MenuItem("Chimera/Создать префаб Лося")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildMoose();

        var anyRenderer = go.GetComponentInChildren<Renderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(anyRenderer.sharedMaterial);
            mat.SetColor("_BaseColor", new Color(0.42f, 0.32f, 0.22f)); // тёмно-бурый
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб лося создан: " + Path + ". Тюнь MoosePsyche/ChargeAbility. Dev-спавн берёт этот префаб.");
    }

    public static GameObject BuildMoose()
    {
        var go = new GameObject("Moose");
        var cc = go.AddComponent<CharacterController>();
        cc.height = 2.2f; cc.radius = 0.9f; cc.center = new Vector3(0f, 1.1f, 0f); // крупная туша

        // корпус — вытянутый бокс-плейсхолдер (голова-морда — маленький бокс спереди для читаемости направления)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        body.transform.localScale = new Vector3(1.1f, 1.6f, 2.6f);
        Object.DestroyImmediate(body.GetComponent<Collider>()); // коллизия — на CharacterController

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.5f, 1.4f);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.7f);
        Object.DestroyImmediate(head.GetComponent<Collider>());

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();
        go.AddComponent<Massive>(); // массивная туша: обхват слабее, нокбэк не берёт

        var charge = go.AddComponent<ChargeAbility>();
        WerewolfPrefab.Configure(charge, ("windupTime", 0.5f), ("minRange", 4f), ("maxRange", 12f),
                                          ("chargeSpeed", 16f), ("duration", 0.7f), ("damage", 22),
                                          ("hitRadius", 1.8f), ("knockForce", 12f), ("staggerTime", 0.5f));

        // тело на шасси Лось (природная особь: экспрессия 0.5; витальность/скорость из органов)
        var cbody = go.AddComponent<CreatureBody>();
        var moose = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Лось.asset");
        if (moose == null) Debug.LogWarning("MoosePrefab: ассет Лось не найден — прогони «Chimera → Создать дефолтные виды» и пересоздай префаб.");
        var so = new SerializedObject(cbody);
        so.FindProperty("chassis").objectReferenceValue = moose;
        so.FindProperty("expression").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<MoosePsyche>();
        return go;
    }
}
```

- [ ] **Шаг 2: дев-кнопка спавна** — в `ChimeraDevWindow.cs` добавить блок (по образцу блока «Змея»). Вставить перед закрывающей `}` метода `OnGUI` (после блока змеи):

```csharp
        // ── Лось ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Лось", EditorStyles.boldLabel);
        var moose = Object.FindObjectsByType<MoosePsyche>();
        EditorGUILayout.LabelField($"Живых: {moose.Length}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Спавн лося")) SpawnMoose();
            if (GUILayout.Button("Убить всех лосей"))
                foreach (var m in moose)
                    if (m.TryGetComponent<Health>(out var mh)) mh.TakeDamage(99999, true);
        }
```

И метод-хелпер рядом с `SpawnSnake` (в конце класса):

```csharp
    static void SpawnMoose()
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        Vector3 pos = (pc != null ? pc.transform.position : Vector3.zero) + new Vector3(10f, 0f, 8f);
        if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoosePrefab.Path);
        var go = prefab != null ? Object.Instantiate(prefab) : MoosePrefab.BuildMoose();
        go.transform.position = pos;
    }
```

*(`NavMesh` уже используется в `ChimeraDevWindow` — `using UnityEngine.AI;` там есть.)*

- [ ] **Шаг 3: пользователь — собрать префаб и заспавнить.** В Unity: **Chimera → Создать префаб Лося** (проверить `Assets/_Chimera/Prefabs/Moose.prefab`). Затем Play → дев-панель (**Chimera → Dev-панель**) → **Спавн лося**.

- [ ] **Шаг 4: 🎯 ПЛЕЙТЕСТ-ЧЕКПОИНТ (пользователь репортит):**
  - Лось появился крупной бурой тушей, **пасётся/бродит** и **игнорит** тебя на дистанции.
  - Подошёл ближе `provokeRadius` (~5 м) ИЛИ ударил его → лось **разворачивается и таранит** (розовый телеграф Charge → рывок → сбивает/толкает), потом кулдаун.
  - **Массивность:** обхват змеи держит лося слабее; пинок (E) лося НЕ откидывает.
  - Числа (скорость/урон/дистанции/HP) — на глаз, тюним потом на `MoosePsyche`/`ChargeAbility`.

- [ ] **Шаг 5: коммит (ТОЛЬКО после «работает» от пользователя).**

```bash
git add Assets/_Chimera/Scripts/Editor/MoosePrefab.cs Assets/_Chimera/Scripts/Editor/MoosePrefab.cs.meta \
        Assets/_Chimera/Scripts/Editor/ChimeraDevWindow.cs \
        Assets/_Chimera/Prefabs/Moose.prefab Assets/_Chimera/Prefabs/Moose.prefab.meta \
        Assets/_Chimera/Materials/MooseBody.mat Assets/_Chimera/Materials/MooseBody.mat.meta
git commit -m "Префаб лося + дев-спавн: первый играбельный лось (нейтрал-таран, массивный)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task A6 (добор среза A, после первого чекпоинта): топот + рога

Отдельные мини-задачи, каждая со своим плейтестом. Делать ПОСЛЕ того, как таран играет.

- **Топот (`StompAbility` или метод в психике):** AOE-`Stagger.Hitstun` по всем в радиусе ~3 м на ПРИЗЕМЛЕНИИ тарана (в конце `ChargeAbility.OnTick`, перед `Done`: `Physics.OverlapSphere` → каждому со `Stagger` дать `Hitstun`; себя пропустить). Даёт «землетрясение», разгоняет скопления.
- **Рога-свайп:** короткий боковой удар по немассивному в ближней зоне (когда цель вплотную, вне окна тарана): `Hit` с `HitEffect.Damage` + `Knockback.Push` (резист Massive) + `HitEffect.Bleed()`. Bleed уже есть в словаре. Наказывает липнущих вплотную.

Детальный код этих двух распишем как дойдём (после плейтеста тарана — станет ясно, где именно они нужны по ощущению).

---

## Дальнейшие срезы (свои планы, по мере прохождения)

- **B. Модуль слуха** (shared-инфра: `Noise`-эмиттер событий + `Hearing`-сенсор; лось идёт на шум) — свой план.
- **C. Внутренняя психика:** полная **лесенка предупреждений** (холка→уши→фырк→топот→опустил рога) по «метру провокации» + **локальный берсерк** + **эмоц-тинт** (рест-цвет `Telegraph`, градиент натуральный→ярость) — свой план.
- **D. Внешняя психика:** рёв (`BellowAbility`, `Fear` волкам на видо-теге) + экосистема волк↔лось (мораль/пак-сайз/`Bleed`-out) — свой план. Обобщение рёва до kinship — по спеке `2026-07-15-vidovaya-identichnost-design.md`.

---

## Self-Review (проверка плана против спеки)

- **Покрытие среза A спеки §13:** ассет+MoosePsyche-скелет+Massive-резист-knockback+конус+таран — ✅ (A1–A5); топот+рога — ✅ вынесены в A6 (добор после первого чекпоинта). Personality — читается в `MoosePsyche.Start` (вешает `CreatureBody`).
- **Плейсхолдеры:** числа сознательно placeholder (баланс дефернут — помечено). Код всех новых файлов приведён целиком.
- **Согласованность типов:** `ChargeAbility.MinRange/MaxRange` (публичные) читает `MoosePsyche`; `WerewolfPrefab.Configure(field,value)` поля совпадают с `[SerializeField]` в `ChargeAbility` (windupTime — на базе `WindupAbility`, остальные — в `ChargeAbility`). `IBodyStatConsumer.OnBodyStats(int,float,int,int)` — сигнатура как у волка. `Massive`, `Perception.HasLineOfSight`, `AlertState`, `Senses/SenseKind`, `HeatSignature`, `StunTint`, `HitFlash`, `TelegraphColors.Charge` — существующие типы.
- **Unity-гочи учтены:** `.meta` коммитим вместе с файлами; после правки `SpeciesBootstrap` — прогон генератора + Ctrl+S; префаб пересобирается генератором; не коммитим до плейтеста.
