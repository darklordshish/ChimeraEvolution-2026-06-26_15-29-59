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
///
/// ЗАХВАТ — ПРОСТЕЙШИЙ ВАРИАНТ (спека 16.09 §4, обдумать позже): хватает, только если у тела есть запись захвата, цель
/// в досягаемости записи и других чужих рядом нет — толпу не хватаем, держащий стоит грушей. Замах, досягаемость,
/// ритм укуса в хвате, отлёт сорванного и перезарядка — всё из записи органа; машина держит, психика только решает.</summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavLocomotion))]
public class ChimeraAlphaPsyche : MonoBehaviour, IBodyStatConsumer, IGrabber
{
    [SerializeField] float scanRadius = 16f;        // радиус обнаружения живых
    [SerializeField, NotOrganData("решение психики: ритм выбора атаки; перезарядку приёма держит доставка по записи органа")] float attackCooldown = 1.1f;
    [SerializeField] float emptyArsenalStop = 1.8f; // бить нечем — на сколько подходить к цели (тактика; дальности приёмов — в записях органов)
    [SerializeField] float wanderRadius = 10f;
    [SerializeField, NotOrganData("восприятие: как часто психика пересматривает цель")] float retargetInterval = 0.5f;
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

    // ЗАХВАТ: машину заводит тело по записи органа — берём лениво; нет записи — Ready ложно, хватать нечем
    Constrict grip;
    Constrict Grip { get { if (grip == null) TryGetComponent(out grip); return grip; } }
    bool holding;
    float grabWindupEnd = -1f; // идёт замах захвата; -1 — нет
    float nextHoldStrike;      // когда можно ударить того, кого держим (ритм — запись захвата)

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
            if (holding) ReleaseGrab();          // отбросило — хват сорван
            else CancelGrabWindup();
            if (active != null) { active.Abort(true); active = null; }
            return;
        }

        if (holding) { UpdateHold(); return; }

        if (active != null)
        {
            if (stagger != null && stagger.IsStaggered) active.Abort(false);
            if (active.Tick() == AbilityRun.Running) return;
            active = null; nextAttackTime = Time.time + AttackCooldown;
            return;
        }
        if (stagger != null && stagger.IsStaggered) { CancelGrabWindup(); nav.Move(Vector3.zero); return; }

        if (Time.time >= nextRetarget) { nextRetarget = Time.time + RetargetInterval; Retarget(); }

        if (target == null || targetHealth == null || targetHealth.Current <= 0)
        {
            CancelGrabWindup();
            nav.Move(nav.Arrive(nav.Wander(WanderRadius), Speed)); // никого — бродим по навмешу
            return;
        }

        // ДОВОРОТ морды к цели: укус/залп бьют в конус вперёд (transform.forward) — без доворота конус мажет (замах есть, урона нет)
        Vector3 toT = target.position - transform.position; toT.y = 0f;
        if (toT.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toT), RotationSpeed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, target.position);

        // ЗАМАХ ЗАХВАТА идёт: цель ушла или подошёл второй чужой — сорван; дотянул — хватаем
        if (grabWindupEnd >= 0f)
        {
            if (Grip == null || dist > Grip.GrabRange || OthersNear()) CancelGrabWindup();
            else if (Time.time >= grabWindupEnd)
            {
                CancelGrabWindup();
                if (Grip.Begin(targetHealth, this)) { holding = true; nextHoldStrike = Time.time + Grip.BiteInterval; }
                else nextAttackTime = Time.time + AttackCooldown;
            }
            nav.Move(Vector3.zero);
            return;
        }

        Arsenal.Collect(gameObject, arsenal);
        if (Time.time >= nextAttackTime)
        {
            // ЗАХВАТ ПО ВОЗМОЖНОСТИ: запись есть, перезарядка прошла, цель в досягаемости и она ОДНА
            if (Grip != null && Grip.Ready && dist <= Grip.GrabRange && !OthersNear())
            {
                grabWindupEnd = Time.time + Grip.WindupTime;
                if (TryGetComponent<Telegraph>(out var cue)) cue.Set(true, TelegraphColors.Grab, intent: true);
                nav.Move(Vector3.zero);
                return;
            }
            var pick = Arsenal.Pick(arsenal, dist); // дальняя, если цель в её окне; вплотную — ближняя
            if (pick != null)
            {
                pick.SetTarget(targetHealth);
                if (pick.TryUse()) { active = pick; return; }
            }
        }
        nav.Move(Approach(dist, toT));
    }

    // ДЕРЖИМ: стоим мордой к жертве, машина держит; жертву бьём ближним приёмом арсенала не чаще ритма записи
    void UpdateHold()
    {
        var v = Grip != null ? Grip.Victim : null;
        if (v == null) { ReleaseGrab(); return; }                        // жертва умерла или пропала
        if (OthersNear()) { ReleaseGrab(); return; }                     // подошёл второй чужой — грушей не стоим
        Vector3 to = v.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > Grip.HoldRange) { ReleaseGrab(); return; }    // жертва дальше хвата — соскользнула
        if (Grip.Tick() != GrabTick.Holding) { ReleaseGrab(); return; }  // вырвалась, сорвали ударом, выдохлись
        if (to.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), RotationSpeed * Time.deltaTime);

        if (active != null)
        {
            if (active.Tick() == AbilityRun.Running) { nav.Move(Vector3.zero); return; }
            active = null;
        }
        else if (Time.time >= nextHoldStrike)
        {
            Arsenal.Collect(gameObject, arsenal);
            var strike = Arsenal.Pick(arsenal, to.magnitude);
            if (strike != null && strike.WindowMin <= 0f)
            {
                strike.SetTarget(Grip.Victim);
                if (strike.TryUse()) { active = strike; nextHoldStrike = Time.time + Grip.BiteInterval; }
            }
        }
        nav.Move(Vector3.zero);
    }

    void ReleaseGrab()
    {
        if (Grip != null && Grip.Holding) Grip.End(); // перезарядку захвата поставит машина по записи
        holding = false;
        if (active != null) { active.Abort(true); active = null; }
        nextAttackTime = Time.time + AttackCooldown;
    }

    void CancelGrabWindup()
    {
        if (grabWindupEnd < 0f) return;
        grabWindupEnd = -1f;
        if (TryGetComponent<Telegraph>(out var cue)) cue.Clear();
    }

    // IGrabber: игрок рвётся рывком — по правилу змеи: на ст.1 отпускаем (урон нам и отлёт из записи), со ст.2 хват держит
    public bool BreakFree(int damage)
    {
        if (!holding || Grip == null || !Grip.VictimIsPlayer) return true;
        if (Grip.Stage >= 2) return false;
        var victim = Grip.Victim;
        if (ownHealth != null && damage > 0)
        {
            ownHealth.LastAttacker = victim; // рывок-срыв ранит держащего — это удар игрока
            ownHealth.TakeDamage(damage);
        }
        if (knockback != null && victim != null)
        {
            Vector3 away = transform.position - victim.transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * Grip.RipSelfKnock);
        }
        ReleaseGrab();
        return true;
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

    // чужой ли это для МЕНЯ: живой, не я, не призрак-игрок и не тот, кого Я признаю своим по составу
    bool IsHostile(Health other)
    {
        if (other == null || other == ownHealth || other.Current <= 0) return false;
        if (Perception.PlayerGhost && other.GetComponent<PlayerController>() != null) return false; // dev-призрак: игрок невидим, пока не атаковал (BreakGhost раскроет)
        var ob = other.GetComponent<CreatureBody>();
        return !(ob != null && body != null && CreatureBody.Regard(body, ob) >= KinTier.Weak); // СВОИХ (кого Я признаю по составу) пропускаю
    }

    // ближайший НЕ-кин (кого Я не признаю своим по составу). Истинная химера размыта → не признаёт никого → бьёт всех
    void Retarget()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, ScanRadius, scanHits, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Transform bestT = null; Health bestH = null;
        for (int i = 0; i < hits; i++)
        {
            var other = scanHits[i].GetComponentInParent<Health>();
            if (!IsHostile(other)) continue;
            float d = (other.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; bestT = other.transform; bestH = other; }
        }
        target = bestT; targetHealth = bestH;
    }

    // ЕСТЬ ЛИ РЯДОМ ДРУГОЙ ЧУЖОЙ, кроме цели (или жертвы в хвате): «врагов несколько» — захват не начинаем и не держим
    bool OthersNear()
    {
        var focus = holding && Grip != null ? Grip.Victim : targetHealth;
        int hits = Physics.OverlapSphereNonAlloc(transform.position, ScanRadius, scanHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            var other = scanHits[i].GetComponentInParent<Health>();
            if (other != null && other != focus && IsHostile(other)) return true;
        }
        return false;
    }
}
