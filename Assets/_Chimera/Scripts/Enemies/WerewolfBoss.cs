using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Босс «Вервольф» — вершина волчьей линии, зеркало игрока на 100 родстве, но мощнее: много HP,
/// хорошая регенерация, чувствительное обнаружение. Приёмы: УКУС с ВАМПИРИЗМОМ (лечит себя, может
/// набрать временный HP свыше максимума — он не регенится, только вампиризмом) и ПРЫЖОК-наскок.
/// Соло (без стаи): хантит по NavMesh, в покое бродит. Уступает пинку/стаггеру, как волк.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
public class WerewolfBoss : MonoBehaviour
{
    [Header("Тело (зеркало игрока на 100 родстве, но мощнее)")]
    [SerializeField] int maxHp = 600;
    [SerializeField] float regenPerSec = 8f;
    [SerializeField, Range(0f, 1f)] float damageReduction = 0.4f;
    [SerializeField] int tempHpCap = 50;     // потолок временного HP свыше макс. (копится вампиризмом)

    [Header("Движение / чутьё")]
    [SerializeField] float moveSpeed = 10f;
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
    [SerializeField] Color chargeColor = new Color(0.9f, 0.1f, 0.5f);

    [Header("Вой (призыв стаи)")]
    [SerializeField] int summonCount = 5;        // волков за один вой (на все атакующие позиции)
    [SerializeField] int howlWolfCap = 15;       // потолок волков, до которого добивает вой (выше лимита спавнера)
    [SerializeField] float howlWindup = 1.1f;
    [SerializeField] float howlCooldown = 16f;
    [SerializeField] float howlInitialDelay = 8f; // не воет сразу при появлении
    [SerializeField] Color howlColor = new Color(0.6f, 0.5f, 1f);

    [Header("Кулдаун / телеграф / навигация")]
    [SerializeField] float attackCooldown = 1.2f;
    [SerializeField] Color biteColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] Color leapColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField] float pathInterval = 0.2f;
    [SerializeField] float pathDirectRange = 5f;
    [SerializeField] float wanderRadius = 18f;
    [SerializeField, Range(0f, 1f)] float wanderSpeed = 0.5f;

    enum Kind { Bite, Leap, Charge, Howl }
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth, targetHealth;
    Transform target;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    NavMeshPath navPath;

    float nextAttackTime, verticalVel, windupEnd, leapEnd, chargeEnd, nextHowl, nextPathTime, nextWanderTime;
    Vector3 cachedNavDir, wanderTarget, leapVel;
    bool windingUp, leaping, charging, telegraphOn;
    Kind pendingKind;
    Color activeTelegraph;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        navPath = new NavMeshPath();

        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
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

        // не вижу игрока — брожу; временные HP пропадают (сначала снова набьёт вампиризмом)
        if (dist > sightRange)
        {
            ownHealth.ClearOverheal();
            Vector3 w = NavDir(WanderTarget());
            if (w.sqrMagnitude > 0.001f) Face(w);
            Settle(w * moveSpeed * wanderSpeed);
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
        Settle(dist > attackRange ? NavDir(target.position) * moveSpeed : Vector3.zero);
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
        if (targetHealth != null) targetHealth.TakeDamage(biteDamage);
        if (ownHealth != null) ownHealth.Heal(biteLifeSteal); // вампиризм → может уйти в temp HP свыше макс.
    }

    void DoHowl()
    {
        var spawner = FindAnyObjectByType<WolfSpawner>();
        if (spawner != null) spawner.SpawnAt(transform.position, summonCount); // призыв стаи вокруг себя
        nextHowl = Time.time + howlCooldown;
    }

    int CountWolves() => FindObjectsByType<WolfAI>().Length;

    void BeginAttack(Kind kind)
    {
        windingUp = true;
        pendingKind = kind;
        windupEnd = Time.time + (kind == Kind.Leap ? leapWindup : kind == Kind.Charge ? chargeWindup : kind == Kind.Howl ? howlWindup : biteWindup);
        activeTelegraph = kind == Kind.Leap ? leapColor : kind == Kind.Charge ? chargeColor : kind == Kind.Howl ? howlColor : biteColor;
        SetTelegraph(true);
    }

    void Cancel() { windingUp = false; SetTelegraph(false); }

    void LaunchLeap(Vector3 dir)
    {
        windingUp = false;
        SetTelegraph(false);
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
                if (d.magnitude <= leapHitRadius) { targetHealth.TakeDamage(leapDamage); ownHealth.Heal(biteLifeSteal); }
            }
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void StartCharge()
    {
        windingUp = false;
        charging = true;
        chargeEnd = Time.time + chargeMaxDuration;
        activeTelegraph = chargeColor;
        SetTelegraph(true); // телеграф держим — видно, что несётся на четвереньках
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

        Vector3 moveDir = NavDir(target.position);
        if (moveDir.sqrMagnitude > 0.001f) Face(moveDir);
        Settle(moveDir * chargeSpeed);
    }

    Vector3 NavDir(Vector3 dest)
    {
        Vector3 flat = dest - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude < pathDirectRange * pathDirectRange) // вблизи — прямо
            return flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;

        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + pathInterval;
            cachedNavDir = ComputeNavDir(dest);
        }
        return cachedNavDir;
    }

    Vector3 ComputeNavDir(Vector3 dest)
    {
        if (NavMesh.CalculatePath(transform.position, dest, NavMesh.AllAreas, navPath) && navPath.corners.Length > 1)
        {
            Vector3 d = navPath.corners[1] - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        Vector3 fb = dest - transform.position; fb.y = 0f;
        return fb.sqrMagnitude > 0.01f ? fb.normalized : transform.forward;
    }

    Vector3 WanderTarget()
    {
        if (Time.time >= nextWanderTime || (wanderTarget - transform.position).sqrMagnitude < 4f)
        {
            nextWanderTime = Time.time + Random.Range(2f, 5f);
            Vector2 c = Random.insideUnitCircle * wanderRadius;
            Vector3 cand = transform.position + new Vector3(c.x, 0f, c.y);
            wanderTarget = NavMesh.SamplePosition(cand, out var hit, 4f, NavMesh.AllAreas) ? hit.position : transform.position;
        }
        return wanderTarget;
    }

    void SetTelegraph(bool on)
    {
        if (on == telegraphOn) return;
        telegraphOn = on;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? activeTelegraph : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
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
        Gizmos.color = windingUp ? activeTelegraph : (leaping ? leapColor : (charging ? chargeColor : Color.magenta));
        Gizmos.DrawLine(o, o + transform.forward * attackRange);
    }
}
