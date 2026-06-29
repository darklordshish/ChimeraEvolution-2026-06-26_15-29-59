using UnityEngine;

/// <summary>
/// Волк без NavMesh. Бежит к игроку; в радиусе и лицом к цели начинает ЗАМАХ (телеграф),
/// и только в конце замаха кусает. Замах можно отменить оглушением (Stagger) или уворотом
/// (выйти из фронтального конуса). Волки расталкиваются между собой.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WolfAI : MonoBehaviour
{
    [Header("Погоня")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float rotationSpeed = 250f; // ниже = легче обойти со спины
    [SerializeField] float gravity = -20f;
    [SerializeField] float sightRange = 25f;

    [Header("Атака")]
    [SerializeField] float attackRange = 2.0f;
    [SerializeField] float biteHalfAngle = 55f;   // укус только если цель во фронтальном конусе
    [SerializeField] int attackDamage = 8;
    [SerializeField] float attackCooldown = 1.2f;
    [SerializeField] float windupTime = 0.45f;    // замах: окно, чтобы увернуться/прервать
    [SerializeField] Color telegraphColor = new Color(1f, 0.3f, 0.2f);

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
    float nextAttackTime, verticalVel, windupEnd;
    bool windingUp, telegraphOn;

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
        if (target == null) { EndWindup(); Settle(Vector3.zero); return; }

        // отлетает от пинка — ИИ не рулит, движением занимается Knockback
        if (knockback != null && knockback.IsActive) { EndWindup(); return; }

        // оглушение ОТМЕНЯЕТ замах — вот ради чего стаггер
        if (stagger != null && stagger.IsStaggered) { EndWindup(); Settle(Vector3.zero); return; }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;
        bool inCone = dist <= attackRange && Vector3.Angle(transform.forward, dir) <= biteHalfAngle;

        if (windingUp)
        {
            // замах: стоим, НЕ доворачиваемся (закоммитились) — можно отшагнуть из конуса
            if (!inCone) { EndWindup(); nextAttackTime = Time.time + 0.3f; }   // увернулся — промах
            else if (Time.time >= windupEnd)
            {
                targetHealth.TakeDamage(attackDamage);                          // укус состоялся
                EndWindup();
                nextAttackTime = Time.time + attackCooldown;
            }
            Settle(Vector3.zero);
            return;
        }

        if (dist > sightRange) { Settle(Vector3.zero); return; }

        // доворот к цели
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // движение + расталкивание
        Vector3 horizontal = (dist > attackRange ? dir * moveSpeed : Vector3.zero) + Separation();

        // в радиусе и лицом к цели, кулдаун готов → начать замах
        if (inCone && Time.time >= nextAttackTime) BeginWindup();

        Settle(horizontal);
    }

    void BeginWindup()
    {
        windingUp = true;
        windupEnd = Time.time + windupTime;
        SetTelegraph(true);
    }

    void EndWindup()
    {
        windingUp = false;
        SetTelegraph(false);
    }

    void SetTelegraph(bool on)
    {
        if (on == telegraphOn) return;
        telegraphOn = on;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? telegraphColor : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    // отталкивание от соседних волков, чтобы не слипались
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

    // конус укуса: жёлтый — ждёт, КРАСНЫЙ — замах (вот когда опасно)
    void OnDrawGizmos()
    {
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Quaternion lf = Quaternion.AngleAxis(-biteHalfAngle, Vector3.up);
        Quaternion rt = Quaternion.AngleAxis(biteHalfAngle, Vector3.up);
        Gizmos.color = windingUp ? Color.red : Color.yellow;
        Gizmos.DrawLine(o, o + transform.forward * attackRange);
        Gizmos.DrawLine(o, o + lf * transform.forward * attackRange);
        Gizmos.DrawLine(o, o + rt * transform.forward * attackRange);
    }
}
