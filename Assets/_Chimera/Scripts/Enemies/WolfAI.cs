using UnityEngine;

/// <summary>
/// Волк без NavMesh. Приёмы: УКУС вблизи (красный телеграф), ПРЫЖОК со средней дистанции (оранжевый),
/// ЗАХВАТ-удержание вблизи (фиолетовый) — виснет и режет скорость игрока, пока его не отпнут/не сорвут рывком.
/// Тактика стаи через PackCoordinator: слоты окружения + жетоны атаки (одновременно лезут немногие),
/// единственный жетон захвата. Замах укуса/захвата отменяется уворотом и стаггером; прыжок коммитится;
/// пинок (Knockback) рвёт всё. Реализует IGrabber — рывок игрока срывает захват с уроном.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
public class WolfAI : MonoBehaviour, IGrabber
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

    [Header("Укус (ближний)")]
    [SerializeField] float attackRange = 2.0f;
    [SerializeField] float biteHalfAngle = 55f;
    [SerializeField] int biteDamage = 8;
    [SerializeField] float biteWindupTime = 0.45f;

    [Header("Прыжок (средняя дистанция)")]
    [SerializeField] float leapMinRange = 5.0f;
    [SerializeField] float leapRange = 6.5f;
    [SerializeField] float leapWindupTime = 0.5f;
    [SerializeField] float leapSpeed = 13f;
    [SerializeField] float leapUp = 5f;
    [SerializeField] float leapDuration = 0.5f;
    [SerializeField] int leapDamage = 12;
    [SerializeField] float leapHitRadius = 1.3f;

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
    [SerializeField] float howlCueTime = 0.4f;  // сколько держится вспышка-телеграф воя
    [SerializeField] float alertMemory = 8f;    // сколько волк держит тревогу, услышав вой

    [Header("Кулдаун")]
    [SerializeField] float attackCooldown = 1.4f;

    [Header("Расталкивание")]
    [SerializeField] float separationRadius = 1.6f;
    [SerializeField] float separationStrength = 4f;

    enum Kind { Bite, Leap, Grab }

    static readonly Collider[] neighbors = new Collider[16];

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth;
    PlayerController playerCtl;
    PackCoordinator pack;
    Telegraph telegraph;
    NavLocomotion nav;
    Transform target;
    Health targetHealth;

    float nextAttackTime, verticalVel, windupEnd, leapEnd;
    bool windingUp, leaping, hasToken, grabbing;
    Kind pendingKind;
    Color activeTelegraph;
    Vector3 leapVel;
    Vector3 alertPos;               // куда сбегаться по услышанному вою (личная тревога, не глобальная)
    float alertUntil, nextHowlTime, routUntil, telegraphUntil;
    int fear, fearThreshold;        // личный страх: смерти сородичей рядом; набрал свой порог — паникую

    public bool Engaged { get; private set; } // игрок в поле зрения = волк агрессивен/нацелен (для «вне боя» игрока)
    bool Alerted => Time.time < alertUntil;   // услышал вой — знает, куда сбегаться (личная память)

    // услышал чужой вой: поднимаю личную тревогу и запоминаю точку сбора
    public void Hear(Vector3 playerPos) { alertUntil = Time.time + alertMemory; alertPos = playerPos; }
    public void ForgetAlert() => alertUntil = 0f; // сброс личной тревоги (при бегстве стаи — теряем игрока)

    public bool Routing => Time.time < routUntil && !pack.Fearless; // личная паника; ярость вожака её перебивает
    public void CalmRout() { routUntil = 0f; fear = 0; }                   // вой вожака гасит бегство и страх

    // сородич погиб рядом (и я в бою) → +1 к личному страху; набрал свой порог — паникую и бегу
    public void AddFear()
    {
        if (!Engaged) return;
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

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // запаховый след (виден при волчьем Чутье)
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
        if (ownHealth != null)
        {
            ownHealth.RegenPerSecond = hpRegen; // постоянный реген волка
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

        // пинок рвёт всё (включая захват): волк полностью теряет управление, пока летит
        if (knockback != null && knockback.IsActive) { leaping = false; Disengage(attackCooldown); return; }

        // прыжок коммитится — стаггер его не трогает
        if (leaping) { UpdateLeap(); return; }

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
        bool inCone = Vector3.Angle(transform.forward, dir) <= biteHalfAngle;

        // оглушение отменяет замах
        if (stagger != null && stagger.IsStaggered) { Disengage(0.3f); Settle(Vector3.zero); return; }

        if (windingUp) { UpdateWindup(dist, dir, inCone); return; }

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
            Settle(mv * moveSpeed * (active ? 1f : wanderSpeed) + Separation());
            return;
        }

        // доворот мордой к цели (даже когда кружим — выглядит как преследование)
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // цель ушла из зоны вовлечения — отпускаем жетон
        if (hasToken && dist > disengageRange) ReleaseToken();

        // берём жетон атаки, когда готовы и цель в досягаемости
        if (!hasToken && Time.time >= nextAttackTime && inCone && dist <= leapRange)
            if (pack.TryAcquireAttack(this)) hasToken = true;

        // с жетоном — атакуем по дистанции
        if (hasToken && Time.time >= nextAttackTime && inCone)
        {
            if (dist <= attackRange)
            {
                if (pack.TryAcquireGrab(this) && Random.value < grabChance)
                {
                    BeginAttack(Kind.Grab);
                    pack.ReleaseAttack(this); hasToken = false; // захват — отдельная роль, освобождает слот атакующего
                }
                else { pack.ReleaseGrab(this); BeginAttack(Kind.Bite); }
                Settle(Vector3.zero);
                return;
            }
            if (dist >= leapMinRange && dist <= leapRange) { BeginAttack(Kind.Leap); Settle(Vector3.zero); return; }
        }

        // движение: с жетоном — рвёмся в упор; без — кружим к слоту окружения
        Vector3 horizontal;
        if (hasToken)
            horizontal = (dist > attackRange ? nav.DirTo(target.position) * moveSpeed : Vector3.zero) + Separation();
        else
            horizontal = nav.DirTo(pack.SlotPoint(this)) * moveSpeed * circleSpeed + Separation();
        Settle(horizontal);
    }

    void UpdateWindup(float dist, Vector3 dir, bool inCone)
    {
        if (pendingKind == Kind.Leap)
        {
            if (Time.time >= windupEnd) LaunchLeap(dir);
        }
        else // укус или захват — оба отменяются уворотом из зоны/конуса
        {
            if (!(dist <= attackRange && inCone)) Disengage(0.3f);
            else if (Time.time >= windupEnd)
            {
                if (pendingKind == Kind.Grab) StartGrab();
                else { new Hit(ownHealth, transform.position).Apply(targetHealth, HitEffect.Damage(biteDamage)); Disengage(attackCooldown); }
            }
        }
        Settle(Vector3.zero);
    }

    void BeginAttack(Kind kind)
    {
        windingUp = true;
        pendingKind = kind;
        windupEnd = Time.time + (kind == Kind.Leap ? leapWindupTime : kind == Kind.Grab ? grabWindupTime : biteWindupTime);
        activeTelegraph = kind == Kind.Leap ? TelegraphColors.Leap : kind == Kind.Grab ? TelegraphColors.Grab : TelegraphColors.Bite;
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
        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // висим вплотную, мягко подтягиваясь к точке удержания (игрока за собой не тащим)
        float hold = attackRange * 0.6f;
        Vector3 pull = Vector3.ClampMagnitude(dir * (d - hold) * 8f, moveSpeed);
        Settle(pull);
        // отпускает только пинок (Knockback) или рывок (BreakFree) — ни таймаута, ни срыва ударом
    }

    // IGrabber: игрок сорвался рывком — урон цепляющемуся + лёгкий отлёт + отпускаем
    public void BreakFree(int damage)
    {
        if (!grabbing) return;
        if (ownHealth != null && damage > 0) ownHealth.TakeDamage(damage);
        if (knockback != null)
        {
            Vector3 away = transform.position - target.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * ripSelfKnock);
        }
        Disengage(attackCooldown);
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
        pendingKind = Kind.Bite;
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
        Settle(dir * moveSpeed + Separation());
    }

    // вой: зову ближних волков (в радиусе) на точку — глобального алерта на всю карту больше нет
    void TryHowl(Vector3 pos)
    {
        if (Time.time < nextHowlTime) return;
        nextHowlTime = Time.time + howlCooldown;
        pack.Howl(transform.position, howlRadius, pos);
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

    void LaunchLeap(Vector3 dir)
    {
        windingUp = false;
        HideTelegraph();
        leaping = true;
        leapEnd = Time.time + leapDuration;
        Vector3 flat = dir; flat.y = 0f; flat.Normalize();
        leapVel = flat * leapSpeed + Vector3.up * leapUp;
    }

    void UpdateLeap()
    {
        leapVel.y += gravity * Time.deltaTime;
        controller.Move(leapVel * Time.deltaTime);

        if (Time.time >= leapEnd)
        {
            leaping = false;
            // приземлили наскок — кусаем, если цель ещё рядом (с приземления можно увернуться)
            if (targetHealth != null)
            {
                Vector3 d = target.position - transform.position; d.y = 0f;
                if (d.magnitude <= leapHitRadius) new Hit(ownHealth, transform.position).Apply(targetHealth, HitEffect.Damage(leapDamage));
            }
            Disengage(attackCooldown);
        }
    }

    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        int n = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, neighbors, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = neighbors[i];
            if (col.transform == transform) continue;
            if (col.GetComponentInParent<WolfAI>() == null) continue;
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
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Quaternion lf = Quaternion.AngleAxis(-biteHalfAngle, Vector3.up);
        Quaternion rt = Quaternion.AngleAxis(biteHalfAngle, Vector3.up);
        Gizmos.color = (windingUp || grabbing) ? activeTelegraph : (leaping ? TelegraphColors.Leap : (hasToken ? Color.red : Color.yellow));
        Gizmos.DrawLine(o, o + transform.forward * attackRange);
        Gizmos.DrawLine(o, o + lf * transform.forward * attackRange);
        Gizmos.DrawLine(o, o + rt * transform.forward * attackRange);
    }
}
