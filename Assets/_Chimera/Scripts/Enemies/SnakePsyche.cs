using UnityEngine;

/// <summary>
/// Психика змеи — СОЛО-засадный хищник. Термозрение видит тёплого игрока СКВОЗЬ укрытия (в радиусе);
/// не чует — ждёт НЕПОДВИЖНО (в 1d станет невидимой). Подпустил → гремок-замах → рывок из засады
/// (`LeapAbility`) → укус с ЯДОМ (`BiteAbility.venomStacks`). Обхват (3 стадии) — слайс 1c-ii.
/// Нет стаи/воя/ярости — проще волка. Числа тела (урон/скорость) приходят из органов через IBodyStatConsumer.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(BiteAbility))]
[RequireComponent(typeof(LeapAbility))]
[RequireComponent(typeof(SpawnVariance))]
public class SnakePsyche : MonoBehaviour, IBodyStatConsumer
{
    [Header("Засада / термочутьё")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotationSpeed = 320f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float thermalRange = 14f;              // термозрение: видит тёплого сквозь укрытия
    [SerializeField] float creepRange = 11f;               // ближе — подкрадывается; дальше в термо-радиусе — ждёт
    [SerializeField, Range(0f, 1f)] float creepSpeed = 0.5f;

    [Header("Кулдаун")]
    [SerializeField] float attackCooldown = 1.6f;

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth, targetHealth;
    Transform target;
    NavLocomotion nav;
    SpawnVariance variance;
    BiteAbility bite;
    LeapAbility leap;
    WindupAbility activeAbility;   // укус/рывок в процессе — психика его тикает

    float nextAttackTime, verticalVel;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        if (!TryGetComponent(out leap)) leap = gameObject.AddComponent<LeapAbility>();
        if (!TryGetComponent(out variance)) variance = gameObject.AddComponent<SpawnVariance>();

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // змея тоже пахнет — нюх волка её ловит (RPS)
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { target = pc.transform; targetHealth = pc.GetComponent<Health>(); }
    }

    float Speed => moveSpeed * (variance != null ? variance.SpeedMult : 1f);

    // тело-на-шасси Змея кормит деривированное (урон укуса, скорость); яд/обхват — фирменные, на компонентах/психике
    public void OnBodyStats(int damage, float bodyMoveSpeed)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite != null) bite.SetDamage(damage);
    }

    void Update()
    {
        if (target == null) { Settle(Vector3.zero); return; }

        // пинок рвёт активный приём
        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            Settle(Vector3.zero);
            return;
        }

        // активный приём (укус/рывок) тикает сам
        if (activeAbility != null)
        {
            if (stagger != null && stagger.IsStaggered) activeAbility.Abort(false); // полёт рывка сам решит
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            nextAttackTime = Time.time + (st == AbilityRun.Done ? attackCooldown : 0.3f);
            return;
        }

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        // ЗАСАДА: термозрение — тёплый игрок сквозь укрытия в радиусе. Не чует — ждёт неподвижно.
        if (!Perception.SeesThermal(transform.position, target, thermalRange)) { Settle(Vector3.zero); return; }
        if (targetHealth != null) targetHealth.MarkInCombat(); // змея на охоте → игрок в бою

        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Face(dist > 0.001f ? to / dist : transform.forward);

        if (Time.time >= nextAttackTime)
        {
            if (dist <= bite.Range) { if (bite.TryUse()) activeAbility = bite; Settle(Vector3.zero); return; }
            if (dist >= leap.MinRange && dist <= leap.MaxRange) { if (leap.TryUse()) activeAbility = leap; Settle(Vector3.zero); return; }
        }

        // подпустил близко → подкрадывается; далеко в термо-радиусе → терпеливо ждёт (засада, не гонит по карте)
        Settle(dist <= creepRange ? nav.DirTo(target.position) * Speed * creepSpeed : Vector3.zero);
    }

    void Face(Vector3 d) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), rotationSpeed * Time.deltaTime);

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal; motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }
}
