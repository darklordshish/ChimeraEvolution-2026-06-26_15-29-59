using UnityEngine;

/// <summary>
/// Волк без NavMesh. Два приёма: УКУС вблизи (красный телеграф) и ПРЫЖОК со средней дистанции
/// (оранжевый телеграф → бросок по дуге, урон при контакте). Замах укуса отменяется стаггером/уворотом;
/// прыжок, раз начавшись, коммитится (увернуться можно рывком/шагом). Расталкивается со стаей; отлетает от пинка.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WolfAI : MonoBehaviour
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
    [SerializeField] float leapRange = 6.5f;
    [SerializeField] float leapWindupTime = 0.5f;
    [SerializeField] float leapSpeed = 13f;
    [SerializeField] float leapUp = 5f;
    [SerializeField] float leapDuration = 0.5f;
    [SerializeField] int leapDamage = 12;
    [SerializeField] float leapHitRadius = 1.3f;

    [Header("Кулдаун / телеграф")]
    [SerializeField] float attackCooldown = 1.4f;
    [SerializeField] Color biteColor = new Color(1f, 0.3f, 0.2f);
    [SerializeField] Color leapColor = new Color(1f, 0.65f, 0.1f);

    [Header("Стая")]
    [SerializeField] float separationRadius = 1.6f;
    [SerializeField] float separationStrength = 4f;

    static readonly Collider[] neighbors = new Collider[16];
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    Transform target;
    Health targetHealth;

    float nextAttackTime, verticalVel, windupEnd, leapEnd;
    bool windingUp, isLeapWindup, leaping, leapHit, telegraphOn;
    Color activeTelegraph;
    Vector3 leapVel;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();

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
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) { target = player.transform; targetHealth = player.GetComponent<Health>(); }
    }

    void Update()
    {
        if (target == null) { Cancel(); Settle(Vector3.zero); return; }

        // отлёт от пинка — полный отказ управления
        if (knockback != null && knockback.IsActive) { Cancel(); leaping = false; return; }

        // в прыжке — летим по дуге, бьём при контакте
        if (leaping) { UpdateLeap(); return; }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;
        bool inCone = Vector3.Angle(transform.forward, dir) <= biteHalfAngle;

        // оглушение отменяет замах
        if (stagger != null && stagger.IsStaggered) { Cancel(); Settle(Vector3.zero); return; }

        if (windingUp)
        {
            if (isLeapWindup)
            {
                if (Time.time >= windupEnd) LaunchLeap(dir);
            }
            else // замах укуса
            {
                if (!(dist <= attackRange && inCone)) { Cancel(); nextAttackTime = Time.time + 0.3f; } // увернулся
                else if (Time.time >= windupEnd)
                {
                    targetHealth.TakeDamage(biteDamage);
                    Cancel();
                    nextAttackTime = Time.time + attackCooldown;
                }
            }
            Settle(Vector3.zero); // во время замаха стоит
            return;
        }

        if (dist > sightRange) { Settle(Vector3.zero); return; }

        // доворот мордой к цели
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // выбор атаки: вблизи — укус, на средней — прыжок
        if (Time.time >= nextAttackTime && inCone)
        {
            if (dist <= attackRange) BeginWindup(false);
            else if (dist <= leapRange) BeginWindup(true);
        }

        // движение + расталкивание
        Vector3 horizontal = (dist > attackRange ? dir * moveSpeed : Vector3.zero) + Separation();
        Settle(horizontal);
    }

    void BeginWindup(bool leap)
    {
        windingUp = true;
        isLeapWindup = leap;
        windupEnd = Time.time + (leap ? leapWindupTime : biteWindupTime);
        activeTelegraph = leap ? leapColor : biteColor;
        SetTelegraph(true);
    }

    void Cancel()
    {
        windingUp = false;
        isLeapWindup = false;
        SetTelegraph(false);
    }

    void LaunchLeap(Vector3 dir)
    {
        windingUp = false;
        isLeapWindup = false;
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

        if (Time.time >= leapEnd)
        {
            leaping = false;
            nextAttackTime = Time.time + attackCooldown;
        }
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
        Gizmos.color = windingUp ? activeTelegraph : (leaping ? leapColor : Color.yellow);
        Gizmos.DrawLine(o, o + transform.forward * attackRange);
        Gizmos.DrawLine(o, o + lf * transform.forward * attackRange);
        Gizmos.DrawLine(o, o + rt * transform.forward * attackRange);
    }
}
