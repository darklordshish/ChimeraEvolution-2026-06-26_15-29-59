using UnityEngine;

/// <summary>
/// Психика волка: решения (стая/мораль/вой/выбор приёма) поверх общих частей тела.
/// УКУС и ПРЫЖОК — компоненты-доставки (BiteAbility/LeapAbility): психика решает «когда» (TryUse),
/// доставка сама ведёт замах/полёт (Tick). ЗАХВАТ-удержание — спец-механика стаи (жетон, IGrabber),
/// живёт здесь. Тактика стаи через PackCoordinator: слоты окружения + жетоны атаки, единственный
/// жетон захвата. Пинок (Knockback) рвёт всё, включая полёт прыжка.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(BiteAbility))]
[RequireComponent(typeof(LeapAbility))]
[RequireComponent(typeof(Rage))]
[RequireComponent(typeof(SpawnVariance))]
public class WolfPsyche : MonoBehaviour, IGrabber, IBodyStatConsumer
{
    [Header("Погоня")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float rotationSpeed = 250f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float sightRange = 25f;
    [SerializeField] float hpRegen = 1f;       // постоянная регенерация HP волка (живучесть стаи)
    [SerializeField] float wanderRadius = 15f;  // радиус случайного блуждания в покое
    [SerializeField, Range(0f, 1f)] float wanderSpeed = 0.5f; // доля скорости при спокойном блуждании
    [SerializeField] float scentRange = 16f;    // в каком радиусе берёт свежий след игрока

    // параметры укуса и прыжка теперь на компонентах-доставках BiteAbility/LeapAbility (тюнить там)

    [Header("Захват (удержание)")]
    [SerializeField] float grabWindupTime = 0.35f;
    [SerializeField, Range(0f, 1f)] float grabChance = 0.5f;
    [SerializeField] float grabSlow = 0.35f;     // во сколько режется скорость игрока, пока висим (урона от удержания нет)
    [SerializeField] int ripSelfKnock = 6;       // отлёт волка, когда с него срываются рывком

    [Header("Окружение (стая)")]
    [SerializeField] float circleSpeed = 0.85f;  // доля скорости при кружении в слоте
    [SerializeField] float disengageRange = 9f;  // дальше — отпускаем жетон атаки

    [Header("Вой (зов ближней стаи)")]
    [SerializeField] float howlRadius = 16f;    // на сколько разносится вой — сбегаются только ближние волки
    [SerializeField] float howlCooldown = 5f;   // как часто волк может выть
    [SerializeField] float howlRageDuration = 3f; // ярость ближним от воя (< кулдауна — пульс, не фон)
    [SerializeField] float howlCueTime = 0.4f;  // сколько держится вспышка-телеграф воя
    [SerializeField] float alertMemory = 8f;    // сколько волк держит тревогу, услышав вой

    [Header("Кулдаун")]
    [SerializeField] float attackCooldown = 1.4f;

    [Header("Расталкивание")]
    [SerializeField] float separationRadius = 1.6f;
    [SerializeField] float separationStrength = 4f;

    static readonly Collider[] neighbors = new Collider[16];

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth;
    PlayerController playerCtl;
    PackCoordinator pack;
    Telegraph telegraph;
    NavLocomotion nav;
    Rage rage;
    SpawnVariance variance;
    BiteAbility bite;
    LeapAbility leap;
    WindupAbility activeAbility;    // укус/прыжок в процессе (замах/полёт) — психика его тикает
    Transform target;
    Health targetHealth;

    float nextAttackTime, verticalVel, windupEnd;
    bool windingUp, hasToken, grabbing;  // windingUp — только замах ЗАХВАТА
    Color activeTelegraph;
    Vector3 alertPos;               // куда сбегаться по услышанному вою (личная тревога, не глобальная)
    float alertUntil, nextHowlTime, routUntil, telegraphUntil;
    int fear, fearThreshold;        // личный страх: смерти сородичей рядом; набрал свой порог — паникую

    public bool Engaged { get; private set; } // игрок в поле зрения = волк агрессивен/нацелен (для «вне боя» игрока)
    bool Alerted => Time.time < alertUntil;   // услышал вой — знает, куда сбегаться (личная память)

    // услышал чужой вой: поднимаю личную тревогу и запоминаю точку сбора
    public void Hear(Vector3 playerPos) { alertUntil = Time.time + alertMemory; alertPos = playerPos; }
    public void ForgetAlert() => alertUntil = 0f; // сброс личной тревоги (при бегстве стаи — теряем игрока)

    public bool Routing => Time.time < routUntil && !pack.Fearless && !(rage != null && rage.IsEnraged); // паника; ярость её перебивает
    public void CalmRout() { routUntil = 0f; fear = 0; }                   // вой вожака гасит бегство и страх
    public void EnrageFor(float duration) => rage.Enrage(duration);        // вой сородича/вожака бесит

    float Speed => moveSpeed * (rage != null ? rage.SpeedMult : 1f)
                             * (variance != null ? variance.SpeedMult : 1f); // ярость ускоряет; разброс делает особей разными

    // тело-на-шасси (CreatureBody: органы Волка × экспрессия ~0.45) кормит деривированное.
    // Урон прыжка и ритм атак остаются фирменными (сериализованы здесь/на LeapAbility).
    public void OnBodyStats(int damage, float bodyMoveSpeed)
    {
        moveSpeed = bodyMoveSpeed;
        bite.SetDamage(damage);
    }

    // сородич погиб рядом (и я в бою) → +1 к личному страху; набрал свой порог — паникую и бегу
    public void AddFear()
    {
        if (!Engaged || (rage != null && rage.IsEnraged)) return; // яростные не боятся
        if (++fear >= fearThreshold)
        {
            routUntil = Time.time + pack.RoutDuration; // брошу бой и убегу (жетоны отпущу сам в Rout)
            ForgetAlert();                             // потеряю игрока — назад в поиск
            fear = 0;
            fearThreshold = pack.RollPanicThreshold(); // следующий раз — новый порог храбрости
        }
    }

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
        if (!TryGetComponent(out variance)) variance = gameObject.AddComponent<SpawnVariance>();

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // запаховый след (виден при волчьем Чутье)
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
        if (ownHealth != null)
        {
            if (GetComponent<CreatureBody>() == null) ownHealth.RegenPerSecond = hpRegen; // без тела-на-шасси реген свой; с телом — из органов
            ownHealth.onDeath.AddListener(OnKilled); // смерть бьёт по морали стаи
        }
        fearThreshold = pack.RollPanicThreshold(); // личный порог храбрости (случайный из диапазона пула)
    }

    void OnKilled() => PackCoordinator.Instance.ReportKill(transform.position); // страх идёт от места гибели

    void OnEnable()
    {
        pack = PackCoordinator.Instance;
        pack.Register(this);
    }

    void OnDisable()
    {
        if (grabbing && playerCtl != null) playerCtl.ReleaseGrab(this);
        if (pack != null) pack.Unregister(this);
    }

    void Update()
    {
        if (target == null) { Engaged = false; Disengage(0f); Settle(Vector3.zero); return; }

        if (telegraphUntil > 0f && Time.time >= telegraphUntil) HideTelegraph(); // погасить вспышку воя

        bool routing = Routing; // личная паника сломлена → бегство (в ярости от воя не бежим)
        Engaged = !routing
                  && (target.position - transform.position).sqrMagnitude <= sightRange * sightRange
                  && Perception.HasLineOfSight(transform.position, target); // зрение требует прямой видимости
        if (Engaged) TryHowl(target.position); // увидел игрока → взвыл, зову ближних в стаю

        // пинок рвёт всё (включая захват и полёт прыжка): волк полностью теряет управление, пока летит
        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            Disengage(attackCooldown);
            return;
        }

        // активный приём (укус/прыжок) тикает сам: телом на это время рулит доставка
        if (activeAbility != null)
        {
            // паника/стаггер срывают замах; полёт прыжка закоммичен — Abort(false) он игнорит
            if (routing || (stagger != null && stagger.IsStaggered)) activeAbility.Abort(false);
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            Disengage(st == AbilityRun.Done ? attackCooldown : 0.3f);
            return;
        }

        // стая в панике: бросаем приём/захват/жетоны и убегаем прочь (потом вернёмся в поиск)
        if (routing) { Rout(); return; }

        // захват: висим и держим. Срывают только пинок (выше) и рывок (BreakFree). Удар/стаггер НЕ прерывают.
        if (grabbing)
        {
            UpdateGrab();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;
        bool inCone = Vector3.Angle(transform.forward, dir) <= bite.HalfAngle;

        // оглушение отменяет замах
        if (stagger != null && stagger.IsStaggered) { Disengage(0.3f); Settle(Vector3.zero); return; }

        if (windingUp) { UpdateGrabWindup(dist, inCone); return; } // только замах захвата — укус/прыжок тикают выше

        // не вижу: услышал вой → к точке сбора; иначе по ЗАПАХУ (тропа) + сам вою, зову ближних; иначе брожу.
        if (!Engaged)
        {
            if (hasToken) ReleaseToken();
            Vector3 dest; bool active = true;
            if (Alerted && (alertPos - transform.position).sqrMagnitude > 9f)
                dest = alertPos;                                                  // услышал вой — на точку сбора
            else if (ScentField.Instance.TryFollow(transform.position, scentRange, out var scent))
                { dest = scent; TryHowl(scent); }                                 // взял след — тропим и зовём ближних
            else { dest = nav.Wander(wanderRadius); active = false; }              // ничего — бродим
            Vector3 mv = nav.DirTo(dest);
            if (mv.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(mv), rotationSpeed * Time.deltaTime);
            Settle(mv * Speed * (active ? 1f : wanderSpeed) + Separation());
            return;
        }

        // доворот мордой к цели (даже когда кружим — выглядит как преследование)
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // цель ушла из зоны вовлечения — отпускаем жетон
        if (hasToken && dist > disengageRange) ReleaseToken();

        // берём жетон атаки, когда готовы и цель в досягаемости
        if (!hasToken && Time.time >= nextAttackTime && inCone && dist <= leap.MaxRange)
            if (pack.TryAcquireAttack(this)) hasToken = true;

        // с жетоном — атакуем по дистанции
        if (hasToken && Time.time >= nextAttackTime && inCone)
        {
            if (dist <= bite.Range)
            {
                if (pack.TryAcquireGrab(this) && Random.value < grabChance)
                {
                    BeginGrabWindup();
                    pack.ReleaseAttack(this); hasToken = false; // захват — отдельная роль, освобождает слот атакующего
                }
                else { pack.ReleaseGrab(this); if (bite.TryUse()) activeAbility = bite; }
                Settle(Vector3.zero);
                return;
            }
            if (dist >= leap.MinRange && dist <= leap.MaxRange) { if (leap.TryUse()) activeAbility = leap; Settle(Vector3.zero); return; }
        }

        // движение: с жетоном — рвёмся в упор; без — кружим к слоту окружения
        Vector3 horizontal;
        if (hasToken)
            horizontal = (dist > bite.Range ? nav.DirTo(target.position) * Speed : Vector3.zero) + Separation();
        else
            horizontal = nav.DirTo(pack.SlotPoint(this)) * Speed * circleSpeed + Separation();
        Settle(horizontal);
    }

    // замах захвата: отменяется уворотом из зоны/конуса (стаггер ловится выше, в Update)
    void UpdateGrabWindup(float dist, bool inCone)
    {
        if (!(dist <= bite.Range && inCone)) Disengage(0.3f);
        else if (Time.time >= windupEnd) StartGrab();
        Settle(Vector3.zero);
    }

    void BeginGrabWindup()
    {
        windingUp = true;
        windupEnd = Time.time + grabWindupTime;
        activeTelegraph = TelegraphColors.Grab;
        ShowTelegraph(activeTelegraph);
    }

    void StartGrab()
    {
        windingUp = false;
        grabbing = true;
        activeTelegraph = TelegraphColors.Grab;
        ShowTelegraph(activeTelegraph);
        if (playerCtl != null) playerCtl.ApplyGrab(this, grabSlow); // режем скорость игрока; урона от удержания нет
    }

    void UpdateGrab()
    {
        if (playerCtl != null && playerCtl.GrabImmune) { Disengage(attackCooldown); return; } // иммунитет к захвату — отпускаем

        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // висим вплотную, мягко подтягиваясь к точке удержания (игрока за собой не тащим)
        float hold = bite.Range * 0.6f;
        Vector3 pull = Vector3.ClampMagnitude(dir * (d - hold) * 8f, moveSpeed);
        Settle(pull);
        // отпускает только пинок (Knockback) или рывок (BreakFree) — ни таймаута, ни срыва ударом
    }

    // IGrabber: игрок сорвался рывком — урон цепляющемуся + лёгкий отлёт + отпускаем. Волк рвётся ВСЕГДА.
    public bool BreakFree(int damage)
    {
        if (grabbing)
        {
            if (ownHealth != null && damage > 0) ownHealth.TakeDamage(damage);
            if (knockback != null)
            {
                Vector3 away = transform.position - target.position; away.y = 0f;
                if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * ripSelfKnock);
            }
            Disengage(attackCooldown);
        }
        return true;
    }

    void ReleaseToken()
    {
        if (pack != null) { pack.ReleaseAttack(this); pack.ReleaseGrab(this); }
        hasToken = false;
    }

    // снять текущий приём, вернуть жетоны и (опц.) уйти в кулдаун
    void Disengage(float cooldown)
    {
        if (grabbing && playerCtl != null) playerCtl.ReleaseGrab(this);
        grabbing = false;
        windingUp = false;
        HideTelegraph();
        ReleaseToken();
        if (cooldown > 0f) nextAttackTime = Time.time + cooldown;
    }

    // мораль сломлена: сбрасываем приём/захват/жетоны и бежим прочь от игрока
    void Rout()
    {
        if (windingUp || grabbing || hasToken) Disengage(0f);
        Vector3 from = target != null ? target.position : alertPos;
        Vector3 away = transform.position - from; away.y = 0f;
        Vector3 dir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        Settle(dir * Speed + Separation());
    }

    // вой: зову ближних волков (в радиусе) на точку + бешу их — глобального алерта на всю карту больше нет
    void TryHowl(Vector3 pos)
    {
        if (Time.time < nextHowlTime) return;
        nextHowlTime = Time.time + howlCooldown;
        pack.Howl(transform.position, howlRadius, pos, howlRageDuration);
        FlashTelegraph(TelegraphColors.Howl, howlCueTime); // видимый сигнал: волк зовёт стаю
    }

    // телеграф через таймер telegraphUntil: персистентный (до Disengage) / краткая вспышка / гашение
    void ShowTelegraph(Color c) { telegraph.Set(true, c); telegraphUntil = 0f; }
    void FlashTelegraph(Color c, float dur)
    {
        if (windingUp || grabbing) return; // не перебиваем телеграф приёма
        telegraph.Set(true, c);
        telegraphUntil = Time.time + dur;
    }
    void HideTelegraph() { telegraph.Clear(); telegraphUntil = 0f; }

    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        int n = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, neighbors, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = neighbors[i];
            if (col.transform == transform) continue;
            if (col.GetComponentInParent<WolfPsyche>() == null) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f) push += away.normalized / Mathf.Max(d, 0.7f); // пол по дистанции — не взрывается вплотную
        }
        return Vector3.ClampMagnitude(push * separationStrength, moveSpeed); // кап силы — без радиальных рывков
    }

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal;
        motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        float r = bite != null ? bite.Range : 2f;          // в эдит-режиме (до Awake) — дефолты
        float half = bite != null ? bite.HalfAngle : 55f;
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Quaternion lf = Quaternion.AngleAxis(-half, Vector3.up);
        Quaternion rt = Quaternion.AngleAxis(half, Vector3.up);
        Gizmos.color = (windingUp || grabbing) ? activeTelegraph : (hasToken ? Color.red : Color.yellow);
        Gizmos.DrawLine(o, o + transform.forward * r);
        Gizmos.DrawLine(o, o + lf * transform.forward * r);
        Gizmos.DrawLine(o, o + rt * transform.forward * r);
    }
}
