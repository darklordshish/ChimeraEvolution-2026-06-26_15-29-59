using UnityEngine;

/// <summary>
/// Босс «Вервольф» — вершина волчьей линии, зеркало игрока на 100 родстве, но мощнее: много HP,
/// хорошая регенерация, чувствительное обнаружение. Приёмы: УКУС с ВАМПИРИЗМОМ (лечит себя, может
/// набрать временный HP свыше максимума — он не регенится, только вампиризмом) и ПРЫЖОК-наскок.
/// Соло (без стаи): хантит по NavMesh, в покое бродит. Уступает пинку/стаггеру, как волк.
/// УКУС и ПРЫЖОК — те же доставки, что у волка (BiteAbility/LeapAbility), но с числами босса
/// (вампиризм); ЧАРДЖ и ВОЙ — фирменные приёмы психики (чардж обналичивается в мгновенный наскок).
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(BiteAbility))]
[RequireComponent(typeof(LeapAbility))]
[RequireComponent(typeof(Rage))]
public class WerewolfPsyche : MonoBehaviour, IBodyStatConsumer
{
    [Header("Тело — быстрый убийца, НЕ танк")]
    [SerializeField] int maxHp = 300;
    [SerializeField] float regenPerSec = 6f;
    [SerializeField, Range(0f, 1f)] float damageReduction = 0.15f; // не вербеар — брони мало
    [SerializeField] int tempHpCap = 50;     // потолок временного HP свыше макс. (копится вампиризмом)

    [Header("Движение / чутьё")]
    [SerializeField] float moveSpeed = 12f;  // быстрый — не укайтишь налегке
    [SerializeField] float rotationSpeed = 300f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float sightRange = 55f; // чувствительное обнаружение

    // параметры укуса (вампиризм) и прыжка — на компонентах BiteAbility/LeapAbility (генерит префаб-меню)

    [Header("Чардж (разбег на четвереньках → прыжок)")]
    [SerializeField] float chargeSpeed = 20f;       // скорость бега на четвереньках
    [SerializeField] float chargeWindup = 0.35f;
    [SerializeField] float chargeMaxDuration = 2f;  // не дорвался за это время — отбой
    [SerializeField] float chargeMaxRange = 30f;    // с какой макс. дистанции бросается в разбег

    [Header("Вой (призыв стаи)")]
    [SerializeField] int summonCount = 5;        // волков за один вой (на все атакующие позиции)
    [SerializeField] int howlWolfCap = 15;       // потолок волков, до которого добивает вой (выше лимита спавнера)
    [SerializeField] float rageDuration = 8f;    // вой = приказ атаковать без страха: стая не бежит + наваливается вся
    [SerializeField] float howlWindup = 1.1f;
    [SerializeField] float howlCooldown = 16f;
    [SerializeField] float howlInitialDelay = 8f; // не воет сразу при появлении

    [Header("Кулдаун / навигация")]
    [SerializeField] float attackCooldown = 1.2f;
    [SerializeField] float wanderRadius = 18f;
    [SerializeField, Range(0f, 1f)] float wanderSpeed = 0.5f;
    [SerializeField] float scentRange = 22f; // нюх острее волчьего

    enum Kind { Charge, Howl }   // укус/прыжок — доставки (activeAbility), здесь только спец-приёмы

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth, targetHealth;
    Transform target;
    Telegraph telegraph;
    NavLocomotion nav;
    Rage rage;
    BiteAbility bite;
    LeapAbility leap;
    WindupAbility activeAbility;   // укус/прыжок в процессе (замах/полёт) — психика его тикает

    float nextAttackTime, verticalVel, windupEnd, chargeEnd, nextHowl;
    bool windingUp, charging;
    Kind pendingKind;
    Color activeTelegraph;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        if (!TryGetComponent(out leap)) leap = gameObject.AddComponent<LeapAbility>();
        if (!TryGetComponent(out rage)) rage = gameObject.AddComponent<Rage>();

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // босс тоже пахнет (тропишь его)
        if (!TryGetComponent<HeatSignature>(out _)) gameObject.AddComponent<HeatSignature>(); // и тёплый — виден термозрению
        if (!TryGetComponent<StunTint>(out _)) gameObject.AddComponent<StunTint>(); // статус-сигнал «выключен» (стан)
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { target = pc.transform; targetHealth = pc.GetComponent<Health>(); }

        ownHealth.SetMaxHealth(maxHp);
        ownHealth.DamageReduction = damageReduction;
        ownHealth.RegenPerSecond = regenPerSec;
        ownHealth.OverhealCap = tempHpCap; // вампиризм может уходить свыше макс.
        nextHowl = Time.time + howlInitialDelay;
    }

    void Update()
    {
        if (target == null) { Cancel(); Settle(Vector3.zero); return; }

        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            charging = false;
            Cancel();
            return;
        }

        // активный приём (укус/прыжок) тикает сам: телом на это время рулит доставка
        if (activeAbility != null)
        {
            if (stagger != null && stagger.IsStaggered) activeAbility.Abort(false); // полёт закоммичен — сам решит
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            nextAttackTime = Time.time + (st == AbilityRun.Done ? attackCooldown : 0.3f);
            return;
        }

        if (charging) { UpdateCharge(); return; }

        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : transform.forward;
        bool inCone = Vector3.Angle(transform.forward, dir) <= bite.HalfAngle;

        if (stagger != null && stagger.IsStaggered) { Cancel(); Settle(Vector3.zero); return; }

        if (windingUp) { UpdateWindup(); return; } // только чардж/вой — укус/прыжок тикают выше

        // вой-призыв: по кулдауну, если стая поредела — в любом состоянии (хоть в бою, хоть на патруле)
        if (Time.time >= nextHowl)
        {
            if (CountWolves() < howlWolfCap) { Face(dir); BeginAttack(Kind.Howl); Settle(Vector3.zero); return; }
            nextHowl = Time.time + 3f; // стая ещё полная — перепроверим позже
        }

        // не вижу игрока (далеко или за стеной): temp HP пропадают; тропим по ЗАПАХУ, иначе бродим
        bool sees = dist <= sightRange && Perception.HasLineOfSight(transform.position, target);
        if (!sees)
        {
            ownHealth.ClearOverheal();
            Vector3 dest; bool active = true;
            if (ScentField.Instance.TryFollow(transform.position, scentRange, out var scent)) dest = scent;
            else { dest = nav.Wander(wanderRadius); active = false; }
            Vector3 mv = nav.DirTo(dest);
            if (mv.sqrMagnitude > 0.001f) Face(mv);
            Settle(mv * Speed * (active ? 1f : wanderSpeed));
            return;
        }

        Face(dir);
        if (targetHealth != null) targetHealth.MarkInCombat(); // босс видит игрока → бой (соло-босс тоже гейтит реген)

        if (Time.time >= nextAttackTime && inCone)
        {
            if (dist <= bite.Range) { if (bite.TryUse()) activeAbility = bite; Settle(Vector3.zero); return; }
            if (dist >= leap.MinRange && dist <= leap.MaxRange) { if (leap.TryUse()) activeAbility = leap; Settle(Vector3.zero); return; }
            if (dist > leap.MaxRange && dist <= chargeMaxRange) { BeginAttack(Kind.Charge); Settle(Vector3.zero); return; }
        }

        // погоня по NavMesh
        Settle(dist > bite.Range ? nav.DirTo(target.position) * Speed : Vector3.zero);
    }

    float Speed => moveSpeed * (rage != null ? rage.SpeedMult : 1f); // вечная ярость: быстрее (и уязвимее)

    // тело-на-шасси (CreatureBody: человек + фулл волчьи органы ×2) кормит деривированное.
    // Конституция (HP/броня/реген/temp HP) остаётся фирменной — задаётся в Start этой психики.
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed)
    {
        moveSpeed = bodyMoveSpeed;
        bite.SetDamage(damage);
        bite.SetVenom(venom);
        bite.SetBleed(bleed); // вервольф несёт волчью Пасть → кровотечение приходит АВТОМАТИЧЕСКИ (боссовый вампиризм остаётся запечённым)
    }

    void Face(Vector3 d) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), rotationSpeed * Time.deltaTime);

    void UpdateWindup()
    {
        if (pendingKind == Kind.Charge)
        {
            if (Time.time >= windupEnd) StartCharge();
        }
        else // вой
        {
            if (Time.time >= windupEnd) { DoHowl(); Cancel(); nextAttackTime = Time.time + attackCooldown; }
        }
        Settle(Vector3.zero);
    }

    void DoHowl()
    {
        var spawner = FindAnyObjectByType<WolfSpawner>();
        if (spawner != null) spawner.SpawnAt(transform.position, summonCount); // призыв стаи вокруг себя
        PackCoordinator.Instance.Rally(rageDuration); // ярость: перебить бегство, вся стая в атаку без страха
        if (target != null && !Perception.PlayerGhost) // dev-призрака вой не выцеливает (иначе стая вечно кластером на наблюдателе)
            PackCoordinator.Instance.AlertAll(target.position); // вой альфы на всю карту — вся стая сходится на игрока
        nextHowl = Time.time + howlCooldown;
    }

    int CountWolves() => FindObjectsByType<WolfPsyche>().Length;

    void BeginAttack(Kind kind)
    {
        windingUp = true;
        pendingKind = kind;
        windupEnd = Time.time + (kind == Kind.Charge ? chargeWindup : howlWindup);
        activeTelegraph = kind == Kind.Charge ? TelegraphColors.Charge : TelegraphColors.Howl;
        telegraph.Set(true, activeTelegraph);
    }

    void Cancel() { windingUp = false; telegraph.Clear(); }

    void StartCharge()
    {
        windingUp = false;
        charging = true;
        chargeEnd = Time.time + chargeMaxDuration;
        activeTelegraph = TelegraphColors.Charge;
        telegraph.Set(true, activeTelegraph); // телеграф держим — видно, что несётся на четвереньках
    }

    void UpdateCharge()
    {
        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        if (dist <= leap.MaxRange) // дорвался — мгновенный наскок (разбег и был телеграфом)
        {
            charging = false;
            telegraph.Clear();
            if (leap.TryPounceNow()) activeAbility = leap;
            return;
        }
        if (Time.time >= chargeEnd) { charging = false; Cancel(); nextAttackTime = Time.time + attackCooldown; return; } // не успел

        Vector3 moveDir = nav.DirTo(target.position);
        if (moveDir.sqrMagnitude > 0.001f) Face(moveDir);
        Settle(moveDir * chargeSpeed * (rage != null ? rage.SpeedMult : 1f));
    }

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal; motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }
}
