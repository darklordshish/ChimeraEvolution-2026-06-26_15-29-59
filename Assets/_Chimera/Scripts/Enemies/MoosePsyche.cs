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
[RequireComponent(typeof(AntlerAbility))]
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
    AntlerAbility antler;
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
        if (!TryGetComponent(out antler)) antler = gameObject.AddComponent<AntlerAbility>();
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

        // РАЗЪЯРЁН: преследует игрока ОТ и ДО — всегда мордой к нему, догоняет, бьёт когда ВИДИТ и в дистанции.
        // Не отвлекается на выпас (потому раньше «внезапно отворачивался» / «терял интерес» при потере кадра видимости).
        // Остывание + лесенка предупреждений + реакция на стаю — срез C/D.
        if (provoked)
        {
            Vector3 dir = dist > 0.001f ? toT / dist : transform.forward;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            if (sees && Time.time >= nextAttackTime) // приёмы — только когда ВИДИТ (не бьёт сквозь стену/со спины)
            {
                if (dist <= antler.Range)                                // вплотную — не разогнаться, бьём РОГАМИ (урон+кровь+отлёт)
                { if (antler.TryUse()) activeAbility = antler; Settle(Vector3.zero); return; }
                if (dist >= charge.MinRange && dist <= charge.MaxRange)  // в окне — ТАРАН копытами (+топот на приземлении)
                { if (charge.TryUse()) activeAbility = charge; Settle(Vector3.zero); return; }
            }
            Settle(nav.DirTo(target.position) * Speed);                  // догоняет игрока
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
