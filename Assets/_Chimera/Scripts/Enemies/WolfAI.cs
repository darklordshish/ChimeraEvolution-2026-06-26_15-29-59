using UnityEngine;

/// <summary>
/// Волк без NavMesh. Приёмы: УКУС вблизи (красный телеграф), ПРЫЖОК со средней дистанции (оранжевый),
/// ЗАХВАТ-удержание вблизи (фиолетовый) — виснет и режет скорость игрока, пока его не отпнут/не сорвут рывком.
/// Тактика стаи через PackCoordinator: слоты окружения + жетоны атаки (одновременно лезут немногие),
/// единственный жетон захвата. Замах укуса/захвата отменяется уворотом и стаггером; прыжок коммитится;
/// пинок (Knockback) рвёт всё. Реализует IGrabber — рывок игрока срывает захват с уроном.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WolfAI : MonoBehaviour, IGrabber
{
    [Header("Погоня")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float rotationSpeed = 250f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float sightRange = 25f;

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

    [Header("Кулдаун / телеграф")]
    [SerializeField] float attackCooldown = 1.4f;
    [SerializeField] Color biteColor = new Color(1f, 0.3f, 0.2f);
    [SerializeField] Color leapColor = new Color(1f, 0.65f, 0.1f);
    [SerializeField] Color grabColor = new Color(0.7f, 0.2f, 0.9f);

    [Header("Расталкивание")]
    [SerializeField] float separationRadius = 1.6f;
    [SerializeField] float separationStrength = 4f;

    enum Kind { Bite, Leap, Grab }

    static readonly Collider[] neighbors = new Collider[16];
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth;
    PlayerController playerCtl;
    PackCoordinator pack;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    Transform target;
    Health targetHealth;

    float nextAttackTime, verticalVel, windupEnd, leapEnd;
    bool windingUp, leaping, leapHit, telegraphOn, hasToken, grabbing;
    Kind pendingKind;
    Color activeTelegraph;
    Vector3 leapVel;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();

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
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
    }

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
        if (target == null) { Disengage(0f); Settle(Vector3.zero); return; }

        // пинок рвёт всё (включая захват): волк полностью теряет управление, пока летит
        if (knockback != null && knockback.IsActive) { leaping = false; Disengage(attackCooldown); return; }

        // прыжок коммитится — стаггер его не трогает
        if (leaping) { UpdateLeap(); return; }

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

        if (dist > sightRange) { if (hasToken) ReleaseToken(); Settle(Vector3.zero); return; }

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
                if (pack.TryAcquireGrab(this) && Random.value < grabChance) BeginAttack(Kind.Grab);
                else { pack.ReleaseGrab(this); BeginAttack(Kind.Bite); }
                Settle(Vector3.zero);
                return;
            }
            if (dist >= leapMinRange && dist <= leapRange) { BeginAttack(Kind.Leap); Settle(Vector3.zero); return; }
        }

        // движение: с жетоном — рвёмся в упор; без — кружим в своём слоте окружения
        Vector3 horizontal;
        if (hasToken)
            horizontal = (dist > attackRange ? dir * moveSpeed : Vector3.zero) + Separation();
        else
        {
            Vector3 toSlot = pack.SlotPoint(this) - transform.position; toSlot.y = 0f;
            Vector3 slotDir = toSlot.sqrMagnitude > 0.04f ? toSlot.normalized : Vector3.zero;
            horizontal = slotDir * moveSpeed * circleSpeed + Separation();
        }
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
                else { targetHealth.TakeDamage(biteDamage); Disengage(attackCooldown); }
            }
        }
        Settle(Vector3.zero);
    }

    void BeginAttack(Kind kind)
    {
        windingUp = true;
        pendingKind = kind;
        windupEnd = Time.time + (kind == Kind.Leap ? leapWindupTime : kind == Kind.Grab ? grabWindupTime : biteWindupTime);
        activeTelegraph = kind == Kind.Leap ? leapColor : kind == Kind.Grab ? grabColor : biteColor;
        SetTelegraph(true);
    }

    void StartGrab()
    {
        windingUp = false;
        grabbing = true;
        activeTelegraph = grabColor;
        SetTelegraph(true);
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
        SetTelegraph(false);
        ReleaseToken();
        if (cooldown > 0f) nextAttackTime = Time.time + cooldown;
    }

    void LaunchLeap(Vector3 dir)
    {
        windingUp = false;
        SetTelegraph(false);
        leaping = true;
        leapHit = false;
        leapEnd = Time.time + leapDuration;
        Vector3 flat = dir; flat.y = 0f; flat.Normalize();
        leapVel = flat * leapSpeed + Vector3.up * leapUp;
    }

    void UpdateLeap()
    {
        leapVel.y += gravity * Time.deltaTime;
        controller.Move(leapVel * Time.deltaTime);

        if (!leapHit && targetHealth != null)
        {
            Vector3 d = target.position - transform.position; d.y = 0f;
            if (d.magnitude <= leapHitRadius) { targetHealth.TakeDamage(leapDamage); leapHit = true; }
        }

        if (Time.time >= leapEnd) { leaping = false; Disengage(attackCooldown); }
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
            if (d > 0.001f) push += away / (d * d);
        }
        return push * separationStrength;
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
        Gizmos.color = (windingUp || grabbing) ? activeTelegraph : (leaping ? leapColor : (hasToken ? Color.red : Color.yellow));
        Gizmos.DrawLine(o, o + transform.forward * attackRange);
        Gizmos.DrawLine(o, o + lf * transform.forward * attackRange);
        Gizmos.DrawLine(o, o + rt * transform.forward * attackRange);
    }
}
