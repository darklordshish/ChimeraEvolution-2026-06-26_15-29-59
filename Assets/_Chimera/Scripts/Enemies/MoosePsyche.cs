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
    [SerializeField] float hearRange = 24f;              // СЛУХ — топ-чувство лося (дальше мутного зрения, круговой)
    [SerializeField, Range(0f, 1f)] float hearThreshold = 0.12f; // порог реакции: тихий шаг под ним — «крадёшься — прошёл»
    [SerializeField] float noiseMemory = 4f;             // сколько следим за источником шума после последнего звука
    [SerializeField] float comfortDistance = 12f;        // зона комфорта: шум ближе — ТАКТИЧНО отходим (травоядное не проверяет странное)

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
    Grabbed grabbedStatus; // единый захват: НАС держат (хвост игрока) — массивного не защёлкнуть, бодаемся в ответ
    float nextAttackTime, verticalVel;
    bool provoked;
    Vector3 noisePos;      // слух: последний услышанный шум — идём проверить
    float noiseUntil;

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
        senses.Seed(SenseKind.Hearing, hearRange); // слух — круговой (viewHalfAngle 180 по умолчанию)
        senses.SeedCalmMult(SenseKind.Hearing, 1f); // уши дежурят и у спокойного: стелс от слуха — ТИХИЙ шаг, не «зверь расслабился»
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

        // НАС СХВАТИЛИ (хвост игрока): массивного не защёлкнуть (кап ст.1) — лось в слабом хвате БОДАЕТСЯ
        // (рога: урон+кровь+отлёт; отлёт сам рвёт хват дистанцией). Хватать лося = провокация.
        if (grabbedStatus == null) TryGetComponent(out grabbedStatus);
        if (grabbedStatus != null && grabbedStatus.IsHeld)
        {
            provoked = true;
            if (stagger == null || !stagger.IsStaggered)
            {
                Vector3 gto = target.position - transform.position; gto.y = 0f;
                float gd = gto.magnitude;
                if (gd > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(gto / gd), rotationSpeed * Time.deltaTime);
                if (Time.time >= nextAttackTime && gd <= antler.Range && antler.TryUse()) activeAbility = antler;
            }
            Settle(Vector3.zero);
            return;
        }

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        Vector3 toT = target.position - transform.position; toT.y = 0f;
        float dist = toT.magnitude;
        bool inView = dist <= proximityRadius || Vector3.Angle(transform.forward, toT) <= senses.ViewHalfAngle(SenseKind.Sight);
        bool sees = dist <= senses.Range(SenseKind.Sight) && inView && Perception.HasLineOfSight(transform.position, target);

        if (sees && dist <= provokeRadius) provoked = true; // СРЕЗ A: подошёл слишком близко → провокация

        // СЛУХ (срез B): шум над порогом — запоминаем точку и СЛЕДИМ (память освежается, пока шумит).
        // «Бежишь мимо — заметил; крадёшься/замер — прошёл»: тихий шаг игрока живёт ПОД порогом
        if (!provoked
            && Noise.Hear(transform.position, senses.Range(SenseKind.Hearing), transform, out var nPos, out var nStr, out _)
            && nStr >= hearThreshold)
        { noisePos = nPos; noiseUntil = Time.time + noiseMemory; }

        alert.Observe(provoked, sees || Time.time < noiseUntil); // кормим машину восприятия (шум = настороженность)

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

        // НАСТОРОЖЕН ШУМОМ: травоядное НЕ идёт проверять странный звук (любопытство — волчье) — оно СЛЕДИТ
        // и ДЕРЖИТ ДИСТАНЦИЮ: поднял голову, морда к источнику; шум ближе зоны комфорта — ТАКТИЧНО отходит
        // (спокойным шагом, не паника). Звук угас — выпас. Провокация по-прежнему близость/урон (лесенка — срез C)
        if (Time.time < noiseUntil)
        {
            Vector3 toN = noisePos - transform.position; toN.y = 0f;
            float dN = toN.magnitude;
            Vector3 watch = dN > 0.001f ? toN / dN : transform.forward;
            if (dN < comfortDistance)
            {
                // отходим от шума, восстанавливая комфортную дистанцию (навигация обходит стены)
                Vector3 away = transform.position - noisePos; away.y = 0f;
                Vector3 dir = nav.DirTo(transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * 6f);
                // ЗАГНАН (отступать некуда — угол/стены): не дрожим об стену, а встаём МОРДОЙ к угрозе.
                // Это ступень-ноль ЛЕСЕНКИ ПРЕДУПРЕЖДЕНИЙ — холка/фырк/топот/рога лягут сюда в срезе C
                if (dir.sqrMagnitude < 0.04f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(watch), rotationSpeed * Time.deltaTime);
                    Settle(Vector3.zero);
                    return;
                }
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
                Settle(dir * Speed * grazeSpeed);
                return;
            }
            // дистанция комфортна: замер и СМОТРИТ в сторону звука (живой телеграф «я тебя слышу»)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(watch), rotationSpeed * Time.deltaTime);
            Settle(Vector3.zero);
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
