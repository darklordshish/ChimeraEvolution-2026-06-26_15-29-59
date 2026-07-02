using UnityEngine;

/// <summary>
/// Босс «Вервольф» — вершина волчьей линии, зеркало игрока на 100 родстве, но мощнее: много HP,
/// хорошая регенерация, чувствительное обнаружение. Приёмы: УКУС с ВАМПИРИЗМОМ (лечит себя, может
/// набрать временный HP свыше максимума — он не регенится, только вампиризмом) и ПРЫЖОК-наскок.
/// Соло (без стаи): хантит по NavMesh, в покое бродит. Уступает пинку/стаггеру, как волк.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
public class WerewolfBoss : MonoBehaviour
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

    [Header("Укус (вампиризм)")]
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] float biteHalfAngle = 60f;
    [SerializeField] int biteDamage = 28;
    [SerializeField] int biteLifeSteal = 25; // лечит себя; может уйти в temp HP свыше макс.
    [SerializeField] float biteWindup = 0.4f;

    [Header("Прыжок")]
    [SerializeField] float leapMinRange = 6f;
    [SerializeField] float leapRange = 11f;
    [SerializeField] float leapWindup = 0.5f;
    [SerializeField] float leapSpeed = 16f;
    [SerializeField] float leapUp = 6f;
    [SerializeField] float leapDuration = 0.55f;
    [SerializeField] int leapDamage = 30;
    [SerializeField] float leapHitRadius = 2f;

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

    enum Kind { Bite, Leap, Charge, Howl }

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth, targetHealth;
    Transform target;
    Telegraph telegraph;
    NavLocomotion nav;

    float nextAttackTime, verticalVel, windupEnd, leapEnd, chargeEnd, nextHowl;
    Vector3 leapVel;
    bool windingUp, leaping, charging;
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

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // босс тоже пахнет (тропишь его)
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

        if (knockback != null && knockback.IsActive) { leaping = false; charging = false; Cancel(); return; }
        if (leaping) { UpdateLeap(); return; }
        if (charging) { UpdateCharge(); return; }

        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : transform.forward;
        bool inCone = Vector3.Angle(transform.forward, dir) <= biteHalfAngle;

        if (stagger != null && stagger.IsStaggered) { Cancel(); Settle(Vector3.zero); return; }

        if (windingUp) { UpdateWindup(dist, dir, inCone); return; }

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
            Settle(mv * moveSpeed * (active ? 1f : wanderSpeed));
            return;
        }

        Face(dir);

        if (Time.time >= nextAttackTime && inCone)
        {
            if (dist <= attackRange) { BeginAttack(Kind.Bite); Settle(Vector3.zero); return; }
            if (dist >= leapMinRange && dist <= leapRange) { BeginAttack(Kind.Leap); Settle(Vector3.zero); return; }
            if (dist > leapRange && dist <= chargeMaxRange) { BeginAttack(Kind.Charge); Settle(Vector3.zero); return; }
        }

        // погоня по NavMesh
        Settle(dist > attackRange ? nav.DirTo(target.position) * moveSpeed : Vector3.zero);
    }

    void Face(Vector3 d) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), rotationSpeed * Time.deltaTime);

    void UpdateWindup(float dist, Vector3 dir, bool inCone)
    {
        if (pendingKind == Kind.Leap)
        {
            if (Time.time >= windupEnd) LaunchLeap(dir);
        }
        else if (pendingKind == Kind.Charge)
        {
            if (Time.time >= windupEnd) StartCharge();
        }
        else if (pendingKind == Kind.Howl)
        {
            if (Time.time >= windupEnd) { DoHowl(); Cancel(); nextAttackTime = Time.time + attackCooldown; }
        }
        else // укус — отменяется уворотом из зоны/конуса
        {
            if (!(dist <= attackRange && inCone)) { Cancel(); nextAttackTime = Time.time + 0.3f; }
            else if (Time.time >= windupEnd) { Bite(); Cancel(); nextAttackTime = Time.time + attackCooldown; }
        }
        Settle(Vector3.zero);
    }

    void Bite()
    {
        if (targetHealth == null) return;
        var h = new Hit(ownHealth, transform.position);
        h.Apply(targetHealth, HitEffect.Damage(biteDamage));
        h.Apply(targetHealth, HitEffect.LifeSteal(biteLifeSteal)); // вампиризм → может уйти в temp HP свыше макс.
    }

    void DoHowl()
    {
        var spawner = FindAnyObjectByType<WolfSpawner>();
        if (spawner != null) spawner.SpawnAt(transform.position, summonCount); // призыв стаи вокруг себя
        PackCoordinator.Instance.Rally(rageDuration); // ярость: перебить бегство, вся стая в атаку без страха
        if (target != null) PackCoordinator.Instance.AlertAll(target.position); // вой альфы на всю карту — вся стая сходится на игрока
        nextHowl = Time.time + howlCooldown;
    }

    int CountWolves() => FindObjectsByType<WolfAI>().Length;

    void BeginAttack(Kind kind)
    {
        windingUp = true;
        pendingKind = kind;
        windupEnd = Time.time + (kind == Kind.Leap ? leapWindup : kind == Kind.Charge ? chargeWindup : kind == Kind.Howl ? howlWindup : biteWindup);
        activeTelegraph = kind == Kind.Leap ? TelegraphColors.Leap : kind == Kind.Charge ? TelegraphColors.Charge : kind == Kind.Howl ? TelegraphColors.Howl : TelegraphColors.Bite;
        telegraph.Set(true, activeTelegraph);
    }

    void Cancel() { windingUp = false; telegraph.Clear(); }

    void LaunchLeap(Vector3 dir)
    {
        windingUp = false;
        telegraph.Clear();
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
            if (targetHealth != null) // приземление — укус с вампиризмом, если цель рядом
            {
                Vector3 d = target.position - transform.position; d.y = 0f;
                if (d.magnitude <= leapHitRadius)
                {
                    var h = new Hit(ownHealth, transform.position);
                    h.Apply(targetHealth, HitEffect.Damage(leapDamage));
                    h.Apply(targetHealth, HitEffect.LifeSteal(biteLifeSteal));
                }
            }
            nextAttackTime = Time.time + attackCooldown;
        }
    }

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

        if (dist <= leapRange) // дорвался — прыжок с атакой
        {
            charging = false;
            LaunchLeap(dist > 0.001f ? to / dist : transform.forward);
            return;
        }
        if (Time.time >= chargeEnd) { charging = false; Cancel(); nextAttackTime = Time.time + attackCooldown; return; } // не успел

        Vector3 moveDir = nav.DirTo(target.position);
        if (moveDir.sqrMagnitude > 0.001f) Face(moveDir);
        Settle(moveDir * chargeSpeed);
    }

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal; motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Gizmos.color = windingUp ? activeTelegraph : (leaping ? TelegraphColors.Leap : (charging ? TelegraphColors.Charge : Color.magenta));
        Gizmos.DrawLine(o, o + transform.forward * attackRange);
    }
}
