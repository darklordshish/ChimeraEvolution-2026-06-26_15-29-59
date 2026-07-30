using UnityEngine;

/// <summary>ХИМЕРА-АЛЬФА (#4b-2): универсальная психика-фолбэк для существ без доминантного вида
/// (истинная химера — кин ни к кому). Агрессивный апекс: охотится на ВСЁ живое, бьёт ЛЮБОЙ доставкой,
/// что дало тело (арсенал от состава). Дженерик — не знает видов, опрашивает тело (как ёж берёт bite/volley
/// «если орган дал»). Подход A брейншторма #4b-2: компактный автомат, БЕЗ базы-класса (её извлечём в #4b-3,
/// когда появятся видовые модули). Восприятие — радиус-скан (конусы зрения/слуха — #4b-3+).
/// Кин-фильтр — «признаю ли Я его» (Regard(me,other) = его вид ВО мне): доминанта-волк волков не бьёт;
/// истинная химера размыта → не признаёт никого → бьёт всех. «Монстр для всех» ВЫПАДАЕТ из состава, не хардкод.</summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavLocomotion))]
public class ChimeraAlphaPsyche : MonoBehaviour, IBodyStatConsumer
{
    [SerializeField] float scanRadius = 16f;       // радиус обнаружения живых
    [SerializeField] float meleeRange = 2f;        // ближе — ближняя атака
    [SerializeField] float rangedRange = 12f;      // дальше meleeRange, но в этом радиусе → дальняя (если есть)
    [SerializeField] float attackCooldown = 1.1f;
    [SerializeField] float wanderRadius = 10f;
    [SerializeField] float retargetInterval = 0.5f;
    [SerializeField] float rotationSpeed = 240f;   // доворот морды к цели: укус/залп бьют в конус ВПЕРЁД — без доворота мажут

    // 0-гоча (сериализация): читаем 0 как «не настроено»
    float ScanRadius => scanRadius > 0f ? scanRadius : 16f;
    float MeleeRange => meleeRange > 0f ? meleeRange : 2f;
    float RangedRange => rangedRange > 0f ? rangedRange : 12f;
    float AttackCooldown => attackCooldown > 0f ? attackCooldown : 1.1f;
    float WanderRadius => wanderRadius > 0f ? wanderRadius : 10f;
    float RetargetInterval => retargetInterval > 0f ? retargetInterval : 0.5f;
    float RotationSpeed => rotationSpeed > 0f ? rotationSpeed : 240f;

    float moveSpeed = 4f;
    CreatureBody body;
    Health ownHealth;
    NavLocomotion nav;
    Stagger stagger;
    Knockback knockback;
    BiteAbility bite;      // базовая ближняя (гарантирует спавнер при оживлении)
    QuillVolley volley;    // дальняя — только если орган Иглы дал (может не быть)
    WindupAbility active;
    Transform target;
    Health targetHealth;
    float nextAttackTime, nextRetarget;

    static readonly Collider[] scanHits = new Collider[32];

    // тело кормит числами (урон/скорость) — как ёж; base-укус получает урон/яд/кровь
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed, float howlRange)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite != null) { bite.SetDamage(damage); bite.SetVenom(venom); bite.SetBleed(bleed); }
    }

    void Awake()
    {
        ownHealth = GetComponent<Health>();
        nav = GetComponent<NavLocomotion>();
        TryGetComponent(out body);
        TryGetComponent(out stagger);
        TryGetComponent(out knockback);
        TryGetComponent(out bite);
        TryGetComponent(out volley); // нет органа-залпа → только ближний бой
    }

    void Update()
    {
        if (ownHealth == null || nav == null) return;

        if (knockback != null && knockback.IsActive)
        {
            if (active != null) { active.Abort(true); active = null; }
            return;
        }

        if (active != null)
        {
            if (stagger != null && stagger.IsStaggered) active.Abort(false);
            if (active.Tick() == AbilityRun.Running) return;
            active = null; nextAttackTime = Time.time + AttackCooldown;
            return;
        }
        if (stagger != null && stagger.IsStaggered) { nav.Move(Vector3.zero); return; }

        if (Time.time >= nextRetarget) { nextRetarget = Time.time + RetargetInterval; Retarget(); }

        if (target == null || targetHealth == null || targetHealth.Current <= 0)
        {
            nav.Move(nav.Arrive(nav.Wander(WanderRadius), moveSpeed)); // никого — бродим по навмешу
            return;
        }

        // ДОВОРОТ морды к цели: укус/залп бьют в конус вперёд (transform.forward) — без доворота конус мажет (замах есть, урона нет)
        Vector3 toT = target.position - transform.position; toT.y = 0f;
        if (toT.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toT), RotationSpeed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, target.position);
        if (Time.time >= nextAttackTime)
        {
            // дальняя, если цель далеко и залп есть
            if (volley != null && dist > MeleeRange && dist <= RangedRange)
            {
                volley.SetTarget(targetHealth);
                if (volley.TryUse()) { active = volley; return; }
            }
            // ближняя вплотную
            if (dist <= MeleeRange && bite != null)
            {
                bite.SetTarget(targetHealth);
                if (bite.TryUse()) { active = bite; return; }
            }
        }
        nav.Move(nav.Arrive(target.position, moveSpeed, stopAt: MeleeRange * 0.9f)); // преследуем
    }

    // ближайший НЕ-кин (кого Я не признаю своим по составу). Истинная химера размыта → не признаёт никого → бьёт всех
    void Retarget()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, ScanRadius, scanHits, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Transform bestT = null; Health bestH = null;
        for (int i = 0; i < hits; i++)
        {
            var other = scanHits[i].GetComponentInParent<Health>();
            if (other == null || other == ownHealth || other.Current <= 0) continue;
            if (Perception.PlayerGhost && other.GetComponent<PlayerController>() != null) continue; // dev-призрак: игрок невидим, пока не атаковал (BreakGhost раскроет)
            var ob = other.GetComponent<CreatureBody>();
            if (ob != null && body != null && CreatureBody.Regard(body, ob) >= KinTier.Weak) continue; // СВОИХ (кого Я признаю по составу) пропускаю
            float d = (other.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; bestT = other.transform; bestH = other; }
        }
        target = bestT; targetHealth = bestH;
    }
}
