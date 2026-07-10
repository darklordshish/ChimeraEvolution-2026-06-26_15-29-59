using UnityEngine;

/// <summary>
/// Психика змеи — СОЛО-засадный хищник. Термозрение видит тёплого игрока СКВОЗЬ укрытия (в радиусе);
/// не чует — ждёт НЕПОДВИЖНО (в 1d станет невидимой). Подпустил → гремок-замах → рывок из засады
/// (`LeapAbility`) → в упор либо ядовитый укус (`BiteAbility.venomStacks`), либо ОБХВАТ.
///
/// ОБХВАТ (эволюция волчьего `IGrabber`) — «сжатие» с РАТЧЕТОМ: время тянет к удушению, урон по змее
/// откатывает, но НЕ ниже достигнутой стадии. Змея на обхвате СОЛО-закоммичена (стоит, душит — дуэль 1-на-1):
///  • стадия 1 — как волк: замедляет, рвётся РЫВКОМ/ПИНКОМ (единственное окно на побег);
///  • стадия 2 — сжатие защёлкнулось (назад в ст.1 хода нет): рывок/пинок бесполезны, УРОН по змее лишь
///    ДЕРЖИТ сжатие, не давая дойти до ст.3; выход один — забить змею;
///  • стадия 3 — жёсткое удушение (DoT): чистая гонка «забьёшь vs додушит».
/// На каждой новой стадии сжатие впрыскивает ЯД (яд + обхват давят разом).
/// Нет стаи/воя/ярости — проще волка. Числа тела (урон/скорость) — из органов через IBodyStatConsumer.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(BiteAbility))]
[RequireComponent(typeof(LeapAbility))]
[RequireComponent(typeof(SpawnVariance))]
public class SnakePsyche : MonoBehaviour, IBodyStatConsumer, IGrabber
{
    [Header("Засада / термочутьё")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotationSpeed = 320f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float thermalRange = 14f;              // термозрение: видит тёплого сквозь укрытия
    [SerializeField] float creepRange = 11f;               // ближе — подкрадывается; дальше в термо-радиусе — ждёт
    [SerializeField, Range(0f, 1f)] float creepSpeed = 0.5f;
    [SerializeField] float revealMemory = 2f;              // камуфляж: держится «раскрыта» столько после приёма (> кулдауна — не мигает в мили)
    [SerializeField] float rattleInterval = 3f;            // гремок: как часто затаившаяся змея выдаёт себя (единственная зацепка на невидимку)
    [SerializeField] float rattleCue = 0.4f;               // длительность проблеска-гремка (звук ляжет сверху позже)

    [Header("Кулдаун")]
    [SerializeField] float attackCooldown = 1.6f;

    [Header("Обхват (удушающий захват)")]
    [SerializeField, Range(0f, 1f)] float grabChance = 0.5f; // шанс обвить вместо простого укуса (в упор)
    [SerializeField] float grabWindup = 0.35f;               // замах перед обхватом (телеграф — увернись)
    [SerializeField] float tightenRate = 1f;                 // сжатие/сек: тянет к удушению
    [SerializeField] float stage2At = 1.5f;                  // сжатие ≥ этого → стадия 2 (рывок/пинок не пускают)
    [SerializeField] float stage3At = 3.5f;                  // сжатие ≥ этого → стадия 3 (удушение-DoT)
    [SerializeField] float loosenPerDamage = 0.12f;          // каждый 1 HP урона по змее откатывает сжатие
    [SerializeField] float chokeDamage = 4f;                 // урон удушения (стадия 3) за тик
    [SerializeField] float chokeInterval = 0.5f;
    [SerializeField, Range(0f, 1f)] float grabSlow1 = 0.35f; // режет скорость И рывок игрока по стадиям (туже → короче рывок)
    [SerializeField, Range(0f, 1f)] float grabSlow2 = 0.15f;
    [SerializeField, Range(0f, 1f)] float grabSlow3 = 0f;    // 3-я стадия — полный корень (ни рывка, ни хода; выход — иммун/убить)
    [SerializeField] int ripSelfKnock = 5;                   // отлёт змеи, когда сорвались рывком (стадия 1)

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth, targetHealth;
    Transform target;
    PlayerController playerCtl;
    Telegraph telegraph;
    NavLocomotion nav;
    SpawnVariance variance;
    BiteAbility bite;
    LeapAbility leap;
    WindupAbility activeAbility;   // укус/рывок в процессе — психика его тикает
    Camouflage camo;               // камуфляж (Чешуя): раскрываем на время боя (лениво — CreatureBody вешает его после нашего Awake)

    float nextAttackTime, verticalVel, windupEnd, nextRattle;
    bool windingUp, constricting;                // windingUp — только замах ОБХВАТА
    float grip, gripFloor, chokeNext;             // grip — «сжатие»; gripFloor — ратчет: ниже достигнутой стадии не откатывается
    int stage, maxStage, lastHp;                  // stage 1..3; maxStage — чтобы яд впрыснуть раз на новую стадию

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
        if (!TryGetComponent(out variance)) variance = gameObject.AddComponent<SpawnVariance>();

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // змея тоже пахнет — нюх волка её ловит (RPS)
        if (!TryGetComponent<HeatSignature>(out _)) gameObject.AddComponent<HeatSignature>(); // подпись гаснет сама: змея холоднокровна (а тёплая химера-змея засветится)
        if (!TryGetComponent<StunTint>(out _)) gameObject.AddComponent<StunTint>(); // статус-сигнал «выключен» (стан/схвачен)
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
    }

    void OnDisable()
    {
        if (constricting && playerCtl != null) playerCtl.ReleaseGrab(this); // убили на обхвате — отпускаем игрока
    }

    float Speed => moveSpeed * (variance != null ? variance.SpeedMult : 1f);

    // тело-на-шасси Змея кормит деривированное (урон укуса, скорость); яд/обхват — фирменные, на компонентах/психике
    public void OnBodyStats(int damage, float bodyMoveSpeed)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite != null) bite.SetDamage(damage);
    }

    // камуфляж: раскрыть себя на время боя (лениво берём компонент — CreatureBody вешает его в свой Start, после нашего Awake)
    void RevealSelf()
    {
        if (camo == null) TryGetComponent(out camo);
        if (camo != null) camo.Reveal(revealMemory);
    }

    // ГРЕМОК: затаившаяся змея периодически выдаёт себя кратким проблеском (звук позже) — единственная зацепка на невидимку/приманка
    void TryRattle()
    {
        if (Time.time < nextRattle) return;
        nextRattle = Time.time + rattleInterval;
        if (camo == null) TryGetComponent(out camo);
        if (camo != null) camo.Reveal(rattleCue);
    }

    void Update()
    {
        if (target == null) { Settle(Vector3.zero); return; }

        // ОБХВАТ имеет приоритет: змея закоммичена в дуэль (стоит и душит), знает свои срывы сама
        if (constricting) { RevealSelf(); UpdateConstrict(); return; }

        // пинок рвёт активный приём / замах обхвата (вне обхвата)
        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            if (windingUp) { windingUp = false; telegraph.Clear(); }
            Settle(Vector3.zero);
            return;
        }

        // активный приём (укус/рывок) тикает сам
        if (activeAbility != null)
        {
            RevealSelf(); // приём в процессе — змея видна (и ещё revealMemory после, чтобы не мигала на кулдауне)
            if (stagger != null && stagger.IsStaggered) activeAbility.Abort(false); // полёт рывка сам решит
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            nextAttackTime = Time.time + (st == AbilityRun.Done ? attackCooldown : 0.3f);
            return;
        }

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        if (windingUp) { RevealSelf(); UpdateGrabWindup(); return; }

        // ЗАСАДА: термозрение — тёплый игрок сквозь укрытия в радиусе. Не чует — ждёт неподвижно.
        if (!Perception.SeesThermal(transform.position, target, thermalRange)) { TryRattle(); Settle(Vector3.zero); return; }
        if (targetHealth != null) targetHealth.MarkInCombat(); // змея на охоте → игрок в бою

        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Face(dist > 0.001f ? to / dist : transform.forward);

        if (Time.time >= nextAttackTime)
        {
            if (dist <= bite.Range)
            {
                // в упор: обвить (обхват) либо простой ядовитый укус
                if (Random.value < grabChance) { BeginGrabWindup(); Settle(Vector3.zero); return; }
                if (bite.TryUse()) activeAbility = bite;
                Settle(Vector3.zero); return;
            }
            if (dist >= leap.MinRange && dist <= leap.MaxRange) { if (leap.TryUse()) activeAbility = leap; Settle(Vector3.zero); return; }
        }

        // подпустил близко → подкрадывается; далеко в термо-радиусе → терпеливо ждёт (засада) + гремит-выдаёт себя
        if (dist <= creepRange) Settle(nav.DirTo(target.position) * Speed * creepSpeed);
        else { TryRattle(); Settle(Vector3.zero); }
    }

    // замах обхвата: увернулся из радиуса — сорван; выдержал — обвивает
    void UpdateGrabWindup()
    {
        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Face(d > 0.001f ? to / d : transform.forward);

        if (d > bite.Range * 1.2f) { windingUp = false; telegraph.Clear(); nextAttackTime = Time.time + 0.3f; }
        else if (Time.time >= windupEnd) { windingUp = false; StartConstrict(); }
        Settle(Vector3.zero);
    }

    void BeginGrabWindup()
    {
        windingUp = true;
        windupEnd = Time.time + grabWindup;
        telegraph.Set(true, TelegraphColors.Grab);
    }

    void StartConstrict()
    {
        constricting = true;
        grip = 0f; gripFloor = 0f; stage = 1; maxStage = 1;
        lastHp = ownHealth != null ? ownHealth.Current : 0;
        chokeNext = 0f;
        telegraph.Set(true, TelegraphColors.Grab);
        if (playerCtl != null) playerCtl.ApplyGrab(this, grabSlow1); // режем скорость игрока (усилится к стадии 3)
    }

    void UpdateConstrict()
    {
        // чёрный ход: игрок получил иммунитет к захвату (будущая способность) — кольца слетают
        if (playerCtl != null && playerCtl.GrabImmune) { EndConstrict(attackCooldown); return; }

        // СТАН (вой волчьей Пасти) рвёт обхват — RPS: волчий вой = козырь против змеи.
        // Но НЕ на 3-й стадии: там мёртвая хватка, выть поздно (только дожать змею).
        if (stagger != null && stagger.IsStunned && stage < 3) { EndConstrict(attackCooldown); return; }

        // пинок: в 1-й стадии срывает (окно), в 2+ сжатие держит — гасим отлёт
        if (knockback != null && knockback.IsActive)
        {
            if (stage <= 1) { EndConstrict(attackCooldown); return; }
            knockback.Cancel();
        }
        if (ownHealth == null) { EndConstrict(0.3f); return; }

        if (targetHealth != null) targetHealth.MarkInCombat();

        // сжатие тикает вверх (время); урон по змее откатывает вниз, но НЕ ниже пола достигнутой стадии (ратчет)
        grip += tightenRate * Time.deltaTime;
        int dmg = lastHp - ownHealth.Current;
        if (dmg > 0) grip -= dmg * loosenPerDamage;
        grip = Mathf.Max(gripFloor, grip);
        lastHp = ownHealth.Current;

        int newStage = grip >= stage3At ? 3 : grip >= stage2At ? 2 : 1;
        if (newStage != stage) SetStage(newStage);

        // стадия 3 — удушение: DoT-гонка (минует i-frames — рывком из удушения не спрятаться)
        if (stage >= 3 && targetHealth != null && Time.time >= chokeNext)
        {
            targetHealth.TakeDamage(Mathf.RoundToInt(chokeDamage), true);
            chokeNext = Time.time + chokeInterval;
        }

        // стоим на месте, морда к игроку, мягко держим дистанцию удержания (не таскаем игрока за собой)
        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        Face(dir);
        float hold = bite.Range * 0.6f;
        Settle(Vector3.ClampMagnitude(dir * (d - hold) * 8f, moveSpeed));
    }

    void SetStage(int s)
    {
        if (s > maxStage)
        {
            InjectVenom();                                    // туже сжатие → впрыск яда (раз на каждую новую стадию)
            maxStage = s;
            gripFloor = s >= 3 ? stage3At : stage2At;         // ратчет: защёлкнулись — назад за порог стадии не пускаем
        }
        stage = s;
        telegraph.Set(true, StageColor(s));
        if (playerCtl != null) playerCtl.ApplyGrab(this, SlowFor(s));
    }

    void InjectVenom()
    {
        if (targetHealth != null) new Hit(null, transform.position).Apply(targetHealth, HitEffect.Venom());
    }

    void EndConstrict(float cooldown)
    {
        constricting = false; windingUp = false;
        telegraph.Clear();
        if (playerCtl != null) playerCtl.ReleaseGrab(this);
        nextAttackTime = Time.time + cooldown;
    }

    // IGrabber: игрок рвётся рывком. Отпускаем ТОЛЬКО в 1-й стадии (урон + отлёт змеи); в 2+ сжатие держит.
    public bool BreakFree(int damage)
    {
        if (!constricting) return true;   // нечего держать — считаем свободным
        if (stage >= 2) return false;     // сжатие: рывок бесполезен, не отпускаем
        if (ownHealth != null && damage > 0) ownHealth.TakeDamage(damage);
        if (knockback != null)
        {
            Vector3 away = transform.position - target.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * ripSelfKnock);
        }
        EndConstrict(attackCooldown);
        return true;
    }

    Color StageColor(int s) => s >= 3 ? TelegraphColors.Bite : s == 2 ? TelegraphColors.Charge : TelegraphColors.Grab;
    float SlowFor(int s) => s >= 3 ? grabSlow3 : s == 2 ? grabSlow2 : grabSlow1;

    void Face(Vector3 d) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), rotationSpeed * Time.deltaTime);

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal; motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        float r = bite != null ? bite.Range : 2f;
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Gizmos.color = constricting ? StageColor(stage) : (windingUp ? TelegraphColors.Grab : new Color(0.4f, 0.8f, 0.4f));
        Gizmos.DrawLine(o, o + transform.forward * r);
    }
}
