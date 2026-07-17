using UnityEngine;

/// <summary>
/// Психика лося (срезы A+B+C): массивный нейтрал с ХАРАКТЕРОМ. Пасётся; слышит (следит/тактично
/// отходит от шума); ЛЕСЕНКА ПРЕДУПРЕЖДЕНИЙ: видимый провокатор вблизи копит раздражение — морда
/// наливается бордовым (градиент SetMood), ступени слышны (фырк → топот-демонстрация + надвигание);
/// продавил (вплотную / дозрел / загнан / урон) → закоммиченный ТАРАН + ЛОКАЛЬНЫЙ БЕРСЕРК «загнанный
/// зверь» (Rage на сцену, поддерживается пока видит; сцена рассосалась → остыл с осадком).
/// Рёв/экосистема — срез D. Читает статы тела (IBodyStatConsumer); тело = CreatureBody на шасси «Лось».
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
    [SerializeField] float provokeRadius = 5f;   // вторжение ВПЛОТНУЮ на глазах — мгновенный максимум лесенки
    [SerializeField] float attackCooldown = 2.5f;

    [Header("Лесенка предупреждений + берсерк (срез C)")]
    [SerializeField] float warnRadius = 10f;         // видимый провокатор ближе — раздражение растёт (ближе = быстрее)
    [SerializeField] float irritationRise = 0.35f;   // рост раздражения/с у дальней границы (у provokeRadius ~вдвое быстрее)
    [SerializeField] float irritationDecay = 0.2f;   // спад/с, когда провокатор ушёл
    [SerializeField] float berserkDuration = 6f;     // локальный берсерк: ярость-хвост после потери цели (в бою поддерживается)
    [SerializeField] float calmDistance = 18f;       // разъярённый остывает: цель дальше этого ИЛИ вне зрения дольше calmDelay
    [SerializeField] float calmDelay = 5f;

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
    EmotionTint emo;       // морда наливается по лесенке (SetMood); полный бордовый берсерка — статус Rage сам
    Noise noiseSrc;        // теллы-ступени СЛЫШНЫ: фырк/топот-демонстрация — всплески громкости
    float nextAttackTime, verticalVel;
    bool provoked;
    float irritation;      // ЛЕСЕНКА 0..1: копится от видимого провокатора, спадает без него
    float calmSince = -1f; // разъярён: с какого момента сцена «рассосалась» (не видит/далеко)
    int tellStep;          // последняя озвученная ступень (фырк/топот не спамим)
    Vector3 noisePos;      // слух: последний услышанный шум — следим/отходим
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
        senses.SeedCalmMult(SenseKind.Sight, 1f);   // панорама-сторож НЕ расслабляется (иначе видит с 8м и лесенка
                                                    // схлопывается в скачок); стелс от лося — конус сзади + тихий шаг
        senses.Seed(SenseKind.Hearing, hearRange); // слух — круговой (viewHalfAngle 180 по умолчанию)
        senses.SeedCalmMult(SenseKind.Hearing, 1f); // уши дежурят и у спокойного: стелс от слуха — ТИХИЙ шаг, не «зверь расслабился»
        if (!TryGetComponent<HeatSignature>(out _)) gameObject.AddComponent<HeatSignature>(); // тёплый — виден термо
        if (!TryGetComponent<StunTint>(out _)) gameObject.AddComponent<StunTint>();            // статус «выключен»
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
        if (ownHealth != null) ownHealth.onDamaged.AddListener(Provoke); // удар = максимальная провокация (минуя лесенку)
        TryGetComponent(out personality); // личность вешает CreatureBody в своём Awake — читаем после
    }

    // ПРОДАВИЛИ (вплотную / лесенка дозрела / загнан / урон): боевой режим + ЛОКАЛЬНЫЙ БЕРСЕРК
    // «загнанный зверь» — Rage (морду красит статус сам, урон/скорость выше). В бою ярость поддерживается
    void Provoke()
    {
        if (!provoked && rage != null) rage.Enrage(berserkDuration);
        provoked = true;
        irritation = 1f;
    }

    // морда наливается кровью по лесенке (градиент к ярости); полный бордовый берсерка — статус, он сильнее mood
    void UpdateMood()
    {
        if (emo == null && !TryGetComponent(out emo)) return;
        emo.SetMood(TelegraphColors.RageTint, irritation * 0.85f);
    }

    // теллы-ступени СЛЫШНЫ: 0.5 — ФЫРК (тихий всплеск), 0.75 — ТОПОТ-демонстрация (громкий). Раз на подъём
    void TellSteps()
    {
        int step = irritation >= 0.75f ? 2 : irritation >= 0.5f ? 1 : 0;
        if (step > tellStep)
        {
            if (noiseSrc == null) TryGetComponent(out noiseSrc);
            if (noiseSrc != null) noiseSrc.Spike(step == 1 ? 0.45f : 0.8f, 0.4f);
            tellStep = step;
        }
        else if (irritation < 0.4f) tellStep = 0; // остыл ниже фырка — ступени можно озвучить заново
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
            Provoke(); // хватать тушу = максимальная провокация (берсерк)
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

        // ЛЕСЕНКА (срез C): видимый провокатор в warnRadius копит раздражение (ближе — быстрее), ушёл — спадает.
        // Вплотную на глазах — мгновенный максимум (вторжение в личное пространство)
        if (!provoked)
        {
            if (sees && dist <= provokeRadius) Provoke();
            else
            {
                if (sees && dist <= warnRadius)
                    irritation = Mathf.Min(1f, irritation + irritationRise * (0.5f + 0.5f * (1f - dist / warnRadius)) * Time.deltaTime);
                else
                    irritation = Mathf.Max(0f, irritation - irritationDecay * Time.deltaTime);
                if (irritation >= 1f) Provoke();
            }
            UpdateMood();
            TellSteps();
        }

        // СЛУХ (срез B): шум над порогом — запоминаем точку и СЛЕДИМ (память освежается, пока шумит).
        // «Бежишь мимо — заметил; крадёшься/замер — прошёл»: тихий шаг игрока живёт ПОД порогом
        if (!provoked
            && Noise.Hear(transform.position, senses.Range(SenseKind.Hearing), transform, out var nPos, out var nStr, out _)
            && nStr >= hearThreshold)
        { noisePos = nPos; noiseUntil = Time.time + noiseMemory; }

        alert.Observe(provoked, sees || Time.time < noiseUntil || irritation > 0.3f); // шум/лесенка = настороженность

        // РАЗЪЯРЁН (берсерк «загнанный зверь»): преследует ОТ и ДО — мордой к цели, догоняет, бьёт когда видит.
        // Ярость поддерживается, пока сцена жива; РАССОСАЛАСЬ (не видит цель / та далеко дольше calmDelay) →
        // остыл С ОСАДКОМ (раздражение 0.4 — снова задирать быстрее). Реакция на стаю — срез D
        if (provoked)
        {
            bool sceneOver = !sees || dist > calmDistance;
            if (!sceneOver) { calmSince = -1f; if (rage != null) rage.Enrage(berserkDuration); } // бой жив — ярость свежа
            else if (calmSince < 0f) calmSince = Time.time;
            else if (Time.time - calmSince >= calmDelay)
            {
                provoked = false; calmSince = -1f;
                irritation = 0.4f; UpdateMood(); // осадок: морда остаётся тронутой, лесенка начнётся не с нуля
            }
        }
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

        // ЛЕСЕНКА ЗРЕЕТ: с фырка (0.5) — развернулся МОРДОЙ и стоит (предупреждение читается: бордовеющая
        // морда + звук); с топота (0.75) — НАДВИГАЕТСЯ на провокатора шагом (блеф-демонстрация «опустил рога»)
        if (irritation >= 0.5f)
        {
            Vector3 dirT = dist > 0.001f ? toT / dist : transform.forward;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dirT), rotationSpeed * Time.deltaTime);
            if (irritation >= 0.75f && dist > 3f) Settle(nav.DirTo(target.position) * Speed * 0.35f);
            else Settle(Vector3.zero);
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
                // ЗАГНАН (отступать некуда — угол/стены): встаём МОРДОЙ к угрозе, и ЛЕСЕНКА ЗРЕЕТ ПРЯМО ЗДЕСЬ —
                // шумный преследователь в углу дожмёт до ответной агрессии (фырк → топот → берсерк)
                if (dir.sqrMagnitude < 0.04f)
                {
                    irritation = Mathf.Min(1f, irritation + irritationRise * Time.deltaTime);
                    UpdateMood(); TellSteps();
                    if (irritation >= 1f) { Provoke(); return; }
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
