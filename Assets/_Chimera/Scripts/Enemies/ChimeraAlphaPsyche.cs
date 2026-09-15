using System.Collections.Generic;
using UnityEngine;

/// <summary>ХИМЕРА-АЛЬФА (#4b-2): универсальная психика-фолбэк для существ без доминантного вида
/// (истинная химера — кин ни к кому). Агрессивный апекс: охотится на ВСЁ живое, бьёт ЛЮБОЙ доставкой,
/// что дало тело (арсенал от состава). Дженерик — не знает видов: чем бить и откуда подходить, решает
/// <see cref="Arsenal"/> по окнам из записей органов — босс с лосиными ногами таранит, химера без Пасти бьёт
/// конечностью. Подход A брейншторма #4b-2: компактный автомат, БЕЗ базы-класса. Восприятие — радиус-скан
/// (конусы зрения/слуха — #4b-3+).
/// Кин-фильтр — «признаю ли Я его» (Regard(me,other) = его вид ВО мне): доминанта-волк волков не бьёт;
/// истинная химера размыта → не признаёт никого → бьёт всех. «Монстр для всех» ВЫПАДАЕТ из состава, не хардкод.
/// Захват (`Constrict`, отдельный канал 4b-2 §2.2) альфа пока не водит: машине нужен драйвер «когда хватать и отпускать».</summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavLocomotion))]
public class ChimeraAlphaPsyche : MonoBehaviour, IBodyStatConsumer
{
    [SerializeField] float scanRadius = 16f;        // радиус обнаружения живых
    [SerializeField] float attackCooldown = 1.1f;   // ритм атак — решение психики, не свойство доставки
    [SerializeField] float emptyArsenalStop = 1.8f; // бить нечем — на сколько подходить к цели (тактика; дальности приёмов — в записях органов)
    [SerializeField] float wanderRadius = 10f;
    [SerializeField] float retargetInterval = 0.5f;
    [SerializeField] float rotationSpeed = 240f;    // доворот морды к цели: укус/залп бьют в конус ВПЕРЁД — без доворота мажут

    // 0-гоча (сериализация): читаем 0 как «не настроено»
    float ScanRadius => scanRadius > 0f ? scanRadius : 16f;
    float AttackCooldown => attackCooldown > 0f ? attackCooldown : 1.1f;
    float EmptyArsenalStop => emptyArsenalStop > 0f ? emptyArsenalStop : 1.8f;
    float WanderRadius => wanderRadius > 0f ? wanderRadius : 10f;
    float RetargetInterval => retargetInterval > 0f ? retargetInterval : 0.5f;
    float RotationSpeed => rotationSpeed > 0f ? rotationSpeed : 240f;

    float moveSpeed = 4f;
    SpawnVariance variance; // разброс особи (вешает тело): у альфы тоже своя скорость у каждой особи
    float Speed => moveSpeed * (variance != null ? variance.SpeedMult : 1f);
    CreatureBody body;
    Health ownHealth;
    NavLocomotion nav;
    Stagger stagger;
    Knockback knockback;
    // АРСЕНАЛ — не поля «укус/залп», а всё, что тело завело по записям органов. Собирается каждый кадр решения:
    // доставки появляются и гаснут с прививкой, кэш из Awake ослеп бы (тело заводит их позже нашего Awake)
    readonly List<WindupAbility> arsenal = new();
    WindupAbility active;
    Transform target;
    Health targetHealth;
    float nextAttackTime, nextRetarget;

    static readonly Collider[] scanHits = new Collider[32];

    // тело кормит скоростью хода; числа приёмов доставки берут из записей органов сами
    public void OnBodyStats(float bodyMoveSpeed)
    {
        moveSpeed = bodyMoveSpeed;
    }

    void Awake()
    {
        ownHealth = GetComponent<Health>();
        nav = GetComponent<NavLocomotion>();
        TryGetComponent(out body);
        TryGetComponent(out variance);
        TryGetComponent(out stagger);
        TryGetComponent(out knockback);
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
            nav.Move(nav.Arrive(nav.Wander(WanderRadius), Speed)); // никого — бродим по навмешу
            return;
        }

        // ДОВОРОТ морды к цели: укус/залп бьют в конус вперёд (transform.forward) — без доворота конус мажет (замах есть, урона нет)
        Vector3 toT = target.position - transform.position; toT.y = 0f;
        if (toT.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toT), RotationSpeed * Time.deltaTime);

        Arsenal.Collect(gameObject, arsenal);
        float dist = Vector3.Distance(transform.position, target.position);
        if (Time.time >= nextAttackTime)
        {
            var pick = Arsenal.Pick(arsenal, dist); // дальняя, если цель в её окне; вплотную — ближняя
            if (pick != null)
            {
                pick.SetTarget(targetHealth);
                if (pick.TryUse()) { active = pick; return; }
            }
        }
        nav.Move(Approach(dist, toT));
    }

    // КАК ПОДХОДИТЬ — тоже из окон арсенала: есть ближний приём — к его досягаемости; только дальние — в их окно,
    // а слишком близко — отступить (так ёж держит окно залпа); бить нечем — просто сближение
    Vector3 Approach(float dist, Vector3 toTarget)
    {
        float melee = 0f, farMin = float.MaxValue, farMax = 0f;
        foreach (var a in arsenal)
        {
            if (a.WindowMin <= 0f) melee = Mathf.Max(melee, a.WindowMax);
            else { farMin = Mathf.Min(farMin, a.WindowMin); farMax = Mathf.Max(farMax, a.WindowMax); }
        }
        if (melee > 0f) return nav.Arrive(target.position, Speed, stopAt: melee * 0.9f);
        if (farMax > 0f)
        {
            if (dist < farMin) return toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized * Speed : Vector3.zero;
            return nav.Arrive(target.position, Speed, stopAt: (farMin + farMax) * 0.5f);
        }
        return nav.Arrive(target.position, Speed, stopAt: EmptyArsenalStop);
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
