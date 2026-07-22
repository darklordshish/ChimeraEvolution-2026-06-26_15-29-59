using UnityEngine;

/// <summary>
/// Психика волка: решения (стая/мораль/вой/выбор приёма) поверх общих частей тела.
/// УКУС и ПРЫЖОК — компоненты-доставки (BiteAbility/LeapAbility): психика решает «когда» (TryUse),
/// доставка сама ведёт замах/полёт (Tick). ЗАХВАТ-удержание — спец-механика стаи (жетон, IGrabber),
/// живёт здесь. Тактика стаи через PackCoordinator: слоты окружения + жетоны атаки, единственный
/// жетон захвата. Пинок (Knockback) рвёт всё, включая полёт прыжка.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(BiteAbility))]
[RequireComponent(typeof(LeapAbility))]
[RequireComponent(typeof(Rage))]
[RequireComponent(typeof(SpawnVariance))]
public class WolfPsyche : MonoBehaviour, IGrabber, IBodyStatConsumer, ICarried
{
    [Header("Погоня")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float rotationSpeed = 250f;
    [SerializeField] float sightRange = 25f;
    [SerializeField] float sightHalfAngle = 60f;   // СЕКТОР ОБЗОРА зрения (полу-угол; 60 = конус 120°): вне конуса не видит (стелс со спины)
    [SerializeField] float proximityRadius = 2.5f; // «в упор»: вплотную чует и вне конуса (шорох/дыхание — не подкрасться впритык)
    [SerializeField] float hpRegen = 1f;       // постоянная регенерация HP волка (живучесть стаи)
    [SerializeField] float wanderRadius = 15f;  // радиус случайного блуждания в покое
    [SerializeField, Range(0f, 1f)] float wanderSpeed = 0.5f; // доля скорости при спокойном блуждании
    [SerializeField] float scentRange = 16f;    // в каком радиусе берёт свежий след игрока
    [SerializeField] float hearRange = 28f;     // СЛУХ: приманку змеи (громкость 1.0) слышно с этой дальности, тихий гремок (~0.55) — с ~15м

    // параметры укуса и прыжка теперь на компонентах-доставках BiteAbility/LeapAbility (тюнить там)

    [Header("Захват (удержание)")]
    [SerializeField] float grabWindupTime = 0.35f;
    [SerializeField, Range(0f, 1f)] float grabChance = 0.5f;
    [SerializeField] int grabMinPack = 3;        // ЗАХВАТ — СТАЙНЫЙ приём: держать имеет смысл, только когда есть кому грызть
    [SerializeField] float grabPackRadius = 14f; // в каком радиусе вокруг себя считаем «навалились вместе»
    [SerializeField] float grabGrit = 3f;        // насколько АГРЕССИЯ особи двигает порог «разжать хватку» (0 = все одинаковы)
    [SerializeField] int grabBleedStacks = 1;    // клыки сомкнулись — разовая кровь за вцепление (удержание само урона не даёт)
    [SerializeField] int ripSelfKnock = 6;       // отлёт волка, когда с него срываются рывком
    // замедление жертвы-игрока — теперь у ОБЩЕЙ машины (Constrict.grabSlow1 0.35); волк — держатель ст.1

    [Header("Окружение (стая)")]
    [SerializeField] float circleSpeed = 0.85f;  // доля скорости при кружении в слоте
    [SerializeField] float arriveRadius = 2.5f;  // battle-circle: в этом радиусе от слота ПЛАВНО тормозим (arrive) — садимся без осцилляции
    [SerializeField] float disengageRange = 9f;  // дальше — отпускаем жетон атаки

    [Header("Вой (зов ближней стаи)")]
    [SerializeField] float howlRadius = 16f;    // на сколько разносится вой — сбегаются только ближние волки
    [SerializeField] float howlCooldown = 10f;  // личный КД воя (= жизни стака: один волк держит ~1 живой вклад)
    [SerializeField] float howlCueTime = 0.4f;  // сколько держится вспышка-телеграф воя
    [SerializeField] float alertMemory = 8f;    // сколько волк держит тревогу, услышав вой
    [SerializeField] float curiosityMemory = 5f; // любопытство к странному звуку (гремок): сколько идём проверять
    [SerializeField] float rescueRadius = 12f;   // замечаем возню схваченного сородича (стан рядом) — воем и идём отбивать
    [SerializeField] float preyRange = 14f;      // РАСКРЫТАЯ змея (движется/бежит) в этом радиусе — добыча стаи (хищник стал жертвой)

    [Header("Охота на лося (тень-загон, M3)")]
    [SerializeField] float mooseSpotRange = 22f;  // видит тушу — интерес стаи (дальше мутного лосиного зрения!)
    [SerializeField] float shadowRange = 12f;     // дистанция ТЕНИ: сразу ЗА границей лесенки лося (~10) — умно не провоцируем
    [SerializeField] int shadowMinPack = 2;       // одиночка с тушей не связывается (обходит молча)
    [SerializeField] float packCountRadius = 15f; // «стая рядом» для решения о тени

    [Header("Кулдаун")]
    [SerializeField] float attackCooldown = 1.4f;

    [Header("Расталкивание")]
    [SerializeField] float separationRadius = 1.6f;
    [SerializeField] float separationStrength = 4f;
    [SerializeField, Range(0f, 1f)] float attackSeparation = 0.25f; // жетон атаки: толпа почти не тормозит — иначе равновесие запирает в мёртвой зоне между укусом и прыжком
    [SerializeField] float jitterDamp = 0.08f;   // анти-тремор: постоянная времени сглаживания скорости роя (гасит дрожь расталкивание↔притяжение)

    static readonly Collider[] neighbors = new Collider[16];

    Stagger stagger;
    Knockback knockback;
    Health ownHealth;
    PlayerController playerCtl;
    PackCoordinator pack;
    Telegraph telegraph;
    NavLocomotion nav;
    Rage rage;
    SpawnVariance variance;
    BiteAbility bite;
    LeapAbility leap;
    WindupAbility activeAbility;    // укус/прыжок в процессе (замах/полёт) — психика его тикает
    AlertState alert;               // S1: общая машина восприятия (пока ЗЕРКАЛО — поведение ещё не завязано на State)
    Senses senses;                  // S1: сенсорный профиль — дальности идут через него (пер-состоянчато; множители=1 пока)
    Transform target;
    Health targetHealth;

    float nextAttackTime, windupEnd; // вертикаль/гравитация — в NavLocomotion (общий ход)
    Vector3 smoothHoriz;                 // анти-тремор: сглаженная горизонтальная скорость роя (низкочастотный фильтр)
    bool windingUp, hasToken, grabbing;  // windingUp — только замах ЗАХВАТА
    Color activeTelegraph;
    Vector3 alertPos;               // куда сбегаться по услышанному вою (личная тревога, не глобальная)
    Vector3 curiosityPos;           // странный звук (гремок) — точка любопытства
    Vector3 rescuePos;              // схваченный сородич — точка спасения
    Vector3 personalOffset;         // личное место у точки интереса: стая встаёт кольцом, а не стопкой (анти-дёргание)
    float alertUntil, nextHowlTime, telegraphUntil, curiosityUntil, rescueUntil, nextMateScan, nextPreyScan;
    float curiosityStrength;        // сила текущего зова-манка (воронка): дальний слабый не перебивает ближний сильный
    Health playerHealth;            // кэш для возврата цели с добычи-змеи на игрока
    bool huntingPrey;              // цель сейчас — змея (захват не применяем, он завязан на игрока)
    bool carried;                  // 3e-ii: змея тащит нас на стену — тело-кукла, рулит носитель
    SnakeBodyChain preyBody;       // тело добычи-змеи — рвём по ДЛИНЕ, каждый за свой участок
    float preyT;                   // личный участок вдоль тела змеи (0 голова … 1 хвост)
    Morale morale;                  // ШКАЛА МОРАЛИ (страх↔ярость, стаки ±1×10с); порог храбрости — личность
    Personality personality;        // S1 срез 6: личность особи (храбрость/агрессия/любопытство) — разброс поведения
    Grabbed grabbedStatus;          // единый захват: НАС держат (кольца змеи / хвост игрока) — на слабом хвате кусаемся
    Noise noiseSrc;                 // источник звука (вешает тело): всплеск воя — мир слышит (ось Noise)
    Health mooseTarget;             // ОХОТА НА ЛОСЯ (M3): туша в тени/навале
    float nextMooseScan;
    bool mooseHunting;              // грызли тушу — на потере вернуть доставки на игрока
    CreatureBody body;              // своё тело — для кин-проверки (мой вид = шасси)
    float nextKinCheck; // K3a: игрок-кин не цель (обида-эрозия признания живёт на теле игрока — Betrayal)
    bool playerIsKin;
    [SerializeField] float followDuration = 30f; // ПРИЗЫВ: сколько идём ЗА кин-игроком после его воя (повторный вой продлевает)
    float followUntil;

    public bool Engaged { get; private set; } // игрок в поле зрения = волк агрессивен/нацелен (для «вне боя» игрока)
    bool Alerted => Time.time < alertUntil;   // услышал вой — знает, куда сбегаться (личная память)

    // услышал чужой вой: поднимаю личную тревогу и запоминаю точку сбора
    public void Hear(Vector3 playerPos) { alertUntil = Time.time + alertMemory; alertPos = playerPos; }
    public void ForgetAlert() => alertUntil = 0f; // сброс личной тревоги (при бегстве стаи — теряем игрока)

    // СЛУХ (ось Noise, B2 — прото-канал HearRattle переехал на общую физику): ловим ухом самый громкий
    // источник; СТРАННЫЙ звук (гремок змеи) будит ЛЮБОПЫТСТВО — осторожно иду проверить, если не занят
    // (бой/тревога/паника важнее). Шаг в самозарядную ловушку: у змеи любопытный станет одиночкой-добычей.
    // ВОРОНКА — теперь свойство самой физики (Hear: сила = громкость × близость): слабый дальний манок
    // не перебивает сильный ближний, подходя — слышим сильнее → скатываемся к одной змее без пинг-понга.
    // Шум ДВИЖЕНИЯ чужаков (топот игрока/лося) пока НЕ слушаем — отдельное решение, сдвинет баланс встреч
    void ListenForRattle()
    {
        if (Engaged || Alerted || Routing) return;
        if (!Noise.Hear(transform.position, senses.Range(SenseKind.Hearing), transform, out var pos, out var strength, out var src)) return;
        // «странные» звуки: гремок змеи (ловушка!) и ТОПОТ ТУШИ (интерес к добыче — через сенсорику,
        // не магией: лось шумный bulk×2.5, его шаг слышен издалека). Свои и игрок — не новость (пока)
        if (src == null) return;
        bool strange = src.GetComponentInParent<SnakePsyche>() != null || src.GetComponentInParent<MoosePsyche>() != null;
        if (!strange) return;
        if (Time.time < curiosityUntil && strength < curiosityStrength) return;     // уже идём на более сильный зов
        curiosityPos = pos;
        curiosityStrength = strength;
        curiosityUntil = Time.time + curiosityMemory * (personality != null ? personality.Curiosity : 1f); // любопытные проверяют дольше (личность)
    }

    // возня схваченного сородича рядом (стан = скорее всего в кольцах змеи): запомнить точку спасения.
    // Скан раз в полсекунды; память освежается, пока собрата держат
    void SenseGrabbedMate()
    {
        if (Time.time < nextMateScan) return;
        nextMateScan = Time.time + 0.5f;
        foreach (var col in Physics.OverlapSphere(transform.position, rescueRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var mate = col.GetComponentInParent<WolfPsyche>();
            if (mate == null || mate == this) continue;
            if (mate.stagger == null || !mate.stagger.IsStunned) continue;
            rescuePos = mate.transform.position;
            rescueUntil = Time.time + 3f;
            return;
        }
    }

    public bool Routing => morale != null && morale.IsRouting; // паника: мораль ниже −порога (приказ вожака +5 перевешивает сам)
    public void CalmRout() { if (morale != null) morale.Calm(); }   // вой вожака стирает минус-вклады (бегство гаснет)
    public void Cheer(float value) { if (morale != null) morale.Add(value); } // +вклад духа (вой сородича +1, приказ вожака +5)

    /// <summary>K3: вой кин-игрока — ПРИЗЫВ В ЭСКОРТ: идём ЗА НИМ (не к точке), пока зов свеж; охотничьи
    /// ветки (лось/змея) срабатывают по дороге сами — подмогу можно ПРИВЕСТИ к добыче и натравить.</summary>
    public void FollowKin() => followUntil = Time.time + followDuration;

    // ИСПУГ (страшный вой игрока, дальнее кольцо): −вклад шкалы. Вес — ТЕЛЕСНЫЙ у зовущего:
    // база −2, × бонус органов (родство) → до −4 на сотке — почти приказ вожака, только со знаком минус
    public void Frighten(float moraleHit)
    {
        if (morale != null) morale.Add(-Mathf.Abs(moraleHit));
    }

    // S1-зацепка для машины восприятия: чую что-то, что ведёт к Настороженности (но не подтверждённую цель —
    // ту даёт Engaged). Услышал вой / любопытство к гремку / собрата схватили / взял свежий след.
    bool HasCue()
        => Alerted
        || Time.time < curiosityUntil
        || Time.time < rescueUntil
        || (!playerIsKin && ScentField.Instance != null && ScentField.Instance.TryFollow(transform.position, senses.Range(SenseKind.Scent), out _)); // след кина — не зацепка

    float Speed => moveSpeed * (rage != null ? rage.SpeedMult : 1f)
                             * (variance != null ? variance.SpeedMult : 1f); // ярость ускоряет; разброс делает особей разными

    // тело-на-шасси (CreatureBody: органы Волка × экспрессия ~0.45) кормит деривированное.
    // Урон прыжка и ритм атак остаются фирменными (сериализованы здесь/на LeapAbility).
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed, float howlRange)
    {
        moveSpeed = bodyMoveSpeed;
        bite.SetDamage(damage);
        bite.SetVenom(venom); // эффекты укуса из органа Пасти (data-driven): волчьи клыки → кровотечение
        bite.SetBleed(bleed);
        if (howlRange > 0.01f) howlRadius = howlRange; // ГОЛОС — от данных Пасти (природная норма ×1)
    }

    // сородич погиб рядом (и я в бою) → −1 к морали (единая арифметика вернулась после качелей баланса:
    // −2 ронял стаю постоянно). Пороги 2..4 при −1: трус бежит от 2-3 смертей в окне, железный от 4-5
    public void AddFear()
    {
        if (!Engaged && mooseTarget == null && !huntingPrey) return; // смерти давят только участников боя/охоты
        if (morale != null) morale.Add(-1f);
    }

    void Awake()
    {
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        if (!TryGetComponent(out leap)) leap = gameObject.AddComponent<LeapAbility>();
        if (!TryGetComponent(out rage)) rage = gameObject.AddComponent<Rage>();
        if (!TryGetComponent(out variance)) variance = gameObject.AddComponent<SpawnVariance>();
        if (!TryGetComponent(out alert)) alert = gameObject.AddComponent<AlertState>(); // общая машина восприятия (S1)
        if (!TryGetComponent(out senses)) senses = gameObject.AddComponent<Senses>(); // сенсорный профиль (S1)
        senses.Seed(SenseKind.Sight, sightRange);   // сид базовых дальностей из полей психики (если профиль не задан на префабе)
        senses.Seed(SenseKind.Scent, scentRange);
        senses.Seed(SenseKind.Hearing, hearRange);  // слух — круговой (приёмник гремка/воя; ось Noise)
        senses.SeedCalmMult(SenseKind.Hearing, 1f); // уши дежурят и у спокойного — иначе приманка не тянула бы праздных издалека
        senses.SeedViewAngle(SenseKind.Sight, sightHalfAngle); // зрение — КОНУС (термо/запах остаются круговыми)

        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>(); // запаховый след (виден при волчьем Чутье)
        if (!TryGetComponent<HeatSignature>(out _)) gameObject.AddComponent<HeatSignature>(); // тёплый — виден термозрению игрока
        if (!TryGetComponent<StunTint>(out _)) gameObject.AddComponent<StunTint>(); // статус-сигнал «выключен» (стан/схвачен)
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null) { target = playerCtl.transform; targetHealth = playerCtl.GetComponent<Health>(); }
        playerHealth = targetHealth;
        if (ownHealth != null)
        {
            if (GetComponent<CreatureBody>() == null) ownHealth.RegenPerSecond = hpRegen; // без тела-на-шасси реген свой; с телом — из органов
            ownHealth.onDeath.AddListener(OnKilled); // смерть бьёт по морали стаи
            ownHealth.onDamaged.AddListener(OnHurt); // боль от невидимого источника (змея!) — паника
        }
        TryGetComponent(out body);        // своё тело: кин-проверка идентичности игрока к МОЕМУ виду (шасси)
        TryGetComponent(out personality); // личность вешает ТЕЛО (CreatureBody) в своём Awake — читаем здесь (после всех Awake)
        if (morale == null) TryGetComponent(out morale); // шкала морали — от ТЕЛА (универсальная, вешает CreatureBody)
        if (morale != null && personality != null) morale.SetThreshold(personality.Bravery); // личный порог храбрости = ЛИЧНОСТЬ (срез 6)

        // личный угол особи (случайный на спавне) → у общих точек интереса (тревога/спасение/гремок)
        // каждый стоит на СВОЁМ месте кольцом, а не стопкой; заодно зачаток «личности» волка
        personalOffset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 2.2f;
        preyT = Random.value; // личный участок вдоль тела змеи-добычи
    }

    void OnKilled() => PackCoordinator.Instance.ReportKill(transform.position); // страх идёт от места гибели

    // ICarried (3e-ii): змея тащит на стену. Пока несут — тело-кукла: локомоцию/гравитацию глушим
    // (иначе стан всё равно доходит до Settle и дёргает вниз), позицией владеет змея. Отпуск — падаем с нуля.
    public void SetCarried(bool on)
    {
        carried = on;
        if (!on) nav.ResetMotion(); // сброс накопленной скорости/вертикали — падаем с места, а не рикошетом вниз
    }

    // «ХИЩНИК СТАЛ ЖЕРТВОЙ»: раскрытая змея рядом (движется/бежит — камуфляж её не прячет) становится
    // добычей стаи. Те же укус/прыжок, что по игроку (SetTarget доставкам); захват НЕ применяем — он
    // завязан на PlayerController. Затаившуюся (Camouflage.Hidden) глазами не видим — там работал бы нюх (RPS).
    void RetargetPrey()
    {
        if (Time.time < nextPreyScan) return;
        nextPreyScan = Time.time + 0.4f;

        Health prey = null; float best = preyRange * preyRange;
        foreach (var col in Physics.OverlapSphere(transform.position, preyRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var snake = col.GetComponentInParent<SnakePsyche>();
            if (snake == null) continue;
            if (snake.TryGetComponent<Camouflage>(out var camo) && camo.Hidden) continue; // затаилась — не видим
            if (snake.transform.position.y > transform.position.y + 2f) continue;          // на стене-насесте — не достать, теряем интерес
            float d = (snake.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; prey = snake.GetComponent<Health>(); }
        }

        Transform newTarget = prey != null ? prey.transform : (playerCtl != null ? playerCtl.transform : null);
        Health newHealth = prey != null ? prey : playerHealth;
        if (ReferenceEquals(newTarget, target)) return;

        if (grabbing && grabMachine != null) grabMachine.End();
        grabbing = false; windingUp = false; HideTelegraph(); ReleaseToken();
        target = newTarget; targetHealth = newHealth; huntingPrey = prey != null;
        preyBody = prey != null ? prey.GetComponent<SnakeBodyChain>() : null; // тело добычи — рвём по длине
        if (bite != null) bite.SetTarget(newHealth);
        if (leap != null) leap.SetTarget(newHealth);
    }

    // укушен, но противника НЕ ВИЖУ (змея из засады, удар со спины) → короткая паника: отскочить и бежать.
    // В бою с видимой целью и в ярости не паникуем; из стана не убежать (гейт в Update)
    void OnHurt()
    {
        // ПРЕДАТЕЛЬСТВО (K3a): удар от «своего» (кин-игрока) — это НЕ засада, а предательство. Пере-проверить
        // признание (эрозия могла флипнуть в чужого) и НЕ паниковать: реакцию ведёт эрозия (кин→враг → драка),
        // а не страх. Иначе кин-волк от открытого удара «друга» просто пугался и убегал, вместо того чтобы озлобиться.
        if (playerIsKin && ownHealth != null && ReferenceEquals(ownHealth.LastAttacker, playerHealth))
        { nextKinCheck = 0f; return; }

        if (Engaged || mooseTarget != null) return; // враг ВИДЕН (игрок/туша в бою) — не паника, честная драка
        if (morale != null) morale.Add(-1f); // удар из невидимости (змея из засады, в спину) — −вклад (единая арифметика)
    }

    void OnEnable()
    {
        pack = PackCoordinator.Instance;
        pack.Register(this);
    }

    void OnDisable()
    {
        if (grabbing && grabMachine != null) grabMachine.End();
        if (pack != null) pack.Unregister(this);
    }

    void Update()
    {
        if (carried) return; // тащат на стену: телом рулит змея, сами замираем (StunTint красит «выключенным»)

        // цель пропала. Гнали змею и она сдохла/скрылась → вернуться на игрока; иначе (нет и игрока) — простой
        if (target == null)
        {
            if (huntingPrey && playerCtl != null)
            {
                target = playerCtl.transform; targetHealth = playerHealth; huntingPrey = false;
                if (bite != null) bite.SetTarget(playerHealth);
                if (leap != null) leap.SetTarget(playerHealth);
            }
            else { Engaged = false; Disengage(0f); Settle(Vector3.zero); return; }
        }

        if (telegraphUntil > 0f && Time.time >= telegraphUntil) HideTelegraph(); // погасить вспышку воя

        if (activeAbility == null && !grabbing && !windingUp) RetargetPrey(); // раскрытая змея рядом → добыча стаи

        // K3a — ПОРОГ БЕЗОПАСНОСТИ идентичности: игрок-кин (состав ≥ слабого признания моего вида)
        // НЕ ЦЕЛЬ — свои не добыча. Удар от «своего» = предательство: обида снимает нейтралитет
        if (Time.time >= nextKinCheck)
        {
            nextKinCheck = Time.time + 0.5f;
            playerIsKin = CreatureBody.Regard(CreatureBody.PlayerBody, body) != KinTier.None; // единый глагол (эрозию учёл)
        }

        bool routing = Routing; // личная паника сломлена → бегство (в ярости от воя не бежим)
        // ЗРЕНИЕ — ОБЩЕЕ правило на всех зрячих (`Perception.Sees`): дальность профиля (пер-состоянчато) +
        // конус обзора или «в упор» + затаившуюся не видим + стена рвёт. «В упор» ловит цель вплотную вне
        // конуса (шорох); once Engaged волк доворачивает мордой к цели → держит её в конусе сам
        // K3a-гейт бьёт ТОЧЕЧНО: «свой не добыча» — только когда цель и есть кин-игрок. Иначе кин-статус
        // гасил бой ВООБЩЕ (волк не трогал даже змею, которая жрёт кина) — стая обязана отбивать своего
        bool targetIsKinPlayer = playerIsKin && playerHealth != null && ReferenceEquals(targetHealth, playerHealth);
        Engaged = !routing && !targetIsKinPlayer && Perception.Sees(transform, target, senses, proximityRadius);
        if (Engaged) TryHowl(target.position); // увидел игрока → взвыл, зову ближних в стаю
        alert.Observe(Engaged, HasCue());      // S1: кормим машину восприятия (зеркало — поведение ниже пока не трогаем)

        // M2: ярость — СЛЕДСТВИЕ раскачанного духа (коммит шкалы), а не прямой подарок воя:
        // «когда ярость победит страх — нападают сами». Пока дух выше порога, бафф жив
        if (morale != null && morale.IsCommitted && rage != null) rage.Enrage(0.6f);

        // пинок рвёт всё (включая захват и полёт прыжка): волк полностью теряет управление, пока летит
        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            Disengage(attackCooldown);
            return;
        }

        // активный приём (укус/прыжок) тикает сам: телом на это время рулит доставка
        if (activeAbility != null)
        {
            // паника/стаггер срывают замах; полёт прыжка закоммичен — Abort(false) он игнорит
            if (routing || (stagger != null && stagger.IsStaggered)) activeAbility.Abort(false);
            bool wasLeap = ReferenceEquals(activeAbility, leap);
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            if (st == AbilityRun.Done && wasLeap && TryFollowUpGrab()) return; // наскочил → сразу пробует вцепиться
            Disengage(st == AbilityRun.Done ? attackCooldown : 0.3f);
            return;
        }

        // НАС СХВАТИЛИ (кольца змеи / хвост игрока): единый захват — с места не уйти, но на слабом хвате
        // ДЕРЁМСЯ: кусаем схватившего (цель уже он: змея-обидчик = добыча через RetargetPrey, хвост = игрок).
        // Защёлк (Locked) станит через Grabbed — стаггер-проверка глушит нас; паника из хвата не уносит.
        if (grabbedStatus == null) TryGetComponent(out grabbedStatus);
        if (grabbedStatus != null && grabbedStatus.IsHeld)
        {
            if (windingUp || grabbing || hasToken) Disengage(0f); // свои роли бросаем — заняты выживанием
            var gb = grabbedStatus.Grabber;
            if (gb != null && (stagger == null || !stagger.IsStaggered))
            {
                Vector3 gto = gb.transform.position - transform.position; gto.y = 0f;
                if (gto.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(gto.normalized), rotationSpeed * Time.deltaTime);
                if (Time.time >= nextAttackTime && ReferenceEquals(targetHealth, gb) && gto.magnitude <= bite.Range && bite.TryUse())
                    activeAbility = bite;
            }
            Settle(Vector3.zero);
            return;
        }

        // стая в панике: бросаем приём/захват/жетоны и убегаем прочь. Оглушённый паникёр НЕ бежит
        // (стан держит — иначе жертва убегала бы из обхвата змеи); стан дойдёт до стаггер-чека ниже
        if (routing && !(stagger != null && stagger.IsStaggered)) { Rout(); return; }

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
        bool inCone = Vector3.Angle(transform.forward, dir) <= bite.HalfAngle;

        // оглушение отменяет замах
        if (stagger != null && stagger.IsStaggered) { Disengage(0.3f); Settle(Vector3.zero); return; }

        if (windingUp) { UpdateGrabWindup(dist, inCone); return; } // только замах захвата — укус/прыжок тикают выше

        // не вижу: услышал вой → к точке сбора; иначе по ЗАПАХУ (тропа) + сам вою, зову ближних; иначе брожу.
        if (!Engaged)
        {
            if (hasToken) ReleaseToken();
            pack.LeaveRing(this); // потерял игрока — освобождаем слот кольца (занят бродит/тропит, не держит место)
            SenseGrabbedMate();   // заметить возню схваченного сородича (точка спасения)
            ListenForRattle();    // слух: странный звук (гремок) → любопытство (ось Noise)
            if (TryMooseHunt()) return; // ОХОТА НА ЛОСЯ: тень-загон/навал (игрок не виден — туша интереснее брожения)
            Vector3 dest; bool active = true;
            if (Alerted && (alertPos + personalOffset - transform.position).sqrMagnitude > 9f)
                dest = alertPos + personalOffset;                                  // услышал вой — на СВОЁ место у точки сбора
            else if (Time.time < rescueUntil && (rescuePos + personalOffset - transform.position).sqrMagnitude > 4f)
                { dest = rescuePos + personalOffset; TryHowl(rescuePos); }         // собрата схватили! вой — стая, отбивать
            else if (!playerIsKin && ScentField.Instance.TryFollow(transform.position, senses.Range(SenseKind.Scent), out var scent))
                { dest = scent; TryHowl(scent); }                                 // взял след ДОБЫЧИ — тропим и зовём (след КИН-игрока не тропим: свои пахнут привычно)
            else if (playerIsKin && Time.time < followUntil && playerCtl != null
                     && (playerCtl.transform.position + personalOffset - transform.position).sqrMagnitude > 9f)
                dest = playerCtl.transform.position + personalOffset;              // ЭСКОРТ: идём за кин-вожаком (своё место в кольце)
            else if (Time.time < curiosityUntil && (curiosityPos + personalOffset - transform.position).sqrMagnitude > 4f)
                { dest = curiosityPos + personalOffset; active = false; }          // любопытство: ОСТОРОЖНО проверить гремок
            else { dest = nav.Wander(wanderRadius); active = false; }              // ничего — бродим
            Vector3 mv = nav.DirTo(dest);
            if (mv.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(mv), rotationSpeed * Time.deltaTime);
            Vector3 sep = Separation();
            if (mv.sqrMagnitude < 0.01f) sep *= 0.3f; // прибыл и стоит — не танцуем от расталкивания
            Settle(mv * Speed * (active ? 1f : wanderSpeed) + sep);
            return;
        }

        // доворот мордой к цели (даже когда кружим — выглядит как преследование)
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // цель ушла из зоны вовлечения — отпускаем жетон
        if (hasToken && dist > disengageRange) ReleaseToken();

        // жетон атаки — только против ИГРОКА (кольцо/лимит атакующих вокруг него); добычу-змею грызут все без жетона
        if (!huntingPrey && !hasToken && Time.time >= nextAttackTime && inCone && dist <= leap.MaxRange)
            if (pack.TryAcquireAttack(this)) hasToken = true;

        // атакуем по дистанции (по змее — без жетона)
        if ((huntingPrey || hasToken) && Time.time >= nextAttackTime && inCone)
        {
            if (dist <= bite.Range)
            {
                if (!huntingPrey && PackReadyForGrab() && pack.TryAcquireGrab(this) && Random.value < grabChance)
                {
                    BeginGrabWindup();
                    pack.ReleaseAttack(this); hasToken = false; // захват — отдельная роль, освобождает слот атакующего
                }
                else { pack.ReleaseGrab(this); if (bite.TryUse()) activeAbility = bite; }
                Settle(Vector3.zero);
                return;
            }
            // ПРЫЖОК — рывок усилия: без дыхалки волк остаётся грызть вблизи (отсюда «волны» стаи)
            if (dist >= leap.MinRange && dist <= leap.MaxRange && (Breath == null || Breath.Has(LeapCost)))
            { if (leap.TryUse()) { Breath?.TrySpend(LeapCost); activeAbility = leap; } Settle(Vector3.zero); return; }
        }

        // движение: по ЗМЕЕ окружаем КОЛЬЦОМ (личный угол вокруг добычи); против игрока — с жетоном рвёмся в упор,
        // на откате идём на слот кольца / в рыхлую стаю (battle-circle в PackCoordinator.StandoffPoint)
        Vector3 horizontal;
        if (huntingPrey)
        {
            Vector3 spot = (preyBody != null ? preyBody.BodyPoint(preyT) : target.position) + personalOffset * 0.35f;
            horizontal = nav.Arrive(spot, Speed) + Separation() * attackSeparation; // плавный подход вместо газ/стоп
        }
        else if (hasToken && Time.time >= nextAttackTime)
            // ПЛАВНО подъезжаем на дистанцию укуса: раньше на самой границе был полный газ/полный стоп
            // каждый кадр — главный источник дрожи стаи вокруг жертвы
            horizontal = nav.Arrive(target.position, Speed, stopAt: bite.Range * 0.85f) + Separation() * attackSeparation;
        else
        {
            // battle-circle: идём к своей точке (слот кольца / рыхлая стая) с ARRIVE — плавно тормозим у цели,
            // не «мчим и бьёмся о дедзону». Слоты разнесены → на месте сепарация ≈ 0 → садимся без дрожи
            Vector3 dest = pack.StandoffPoint(this);
            Vector3 flat = dest - transform.position; flat.y = 0f;
            float arv = Speed * circleSpeed * Mathf.Clamp01(flat.magnitude / Mathf.Max(arriveRadius, 0.01f));
            horizontal = nav.DirTo(dest) * arv + Separation();
        }
        // анти-тремор: низкочастотный фильтр гасит дрожь роя (расталкивание↔притяжение в дедзоне у цели/на слоте),
        // не мешая ровному преследованию — устойчивое направление сходится за ~jitterDamp секунд (кадронезависимо)
        smoothHoriz = Vector3.Lerp(smoothHoriz, horizontal, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(jitterDamp, 0.001f)));
        Settle(smoothHoriz);
    }

    // сколько СВОИХ рядом (включая себя) — решение «связываться ли с тушей»
    int PackNearCount(float radius)
    {
        int count = 1;
        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, neighbors, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var w = neighbors[i] != null ? neighbors[i].GetComponentInParent<WolfPsyche>() : null;
            if (w != null && w != this) count++;
        }
        return count;
    }

    // ОХОТА НА ЛОСЯ (M3, тень-загон — спека морали §4): стая ≥2 при виде туши берёт её В ТЕНЬ —
    // кружим на границе его лесенки (умно НЕ провоцируем), ВОЕМ-сзываем (вой = +1 духа и точка сбора
    // на тушу); дух пересёк КОММИТ («ярость победила страх») → НАВАЛ: грызём без жетонов, кровь
    // стекается — даже массивный истечёт. Дух упал — назад в тень; сломлен — паника уже увела.
    // Лось отвечает теллами/рёвом/копытами по нашей морали — перетягивание каната духа
    bool TryMooseHunt()
    {
        if (Time.time >= nextMooseScan)
        {
            nextMooseScan = Time.time + 0.6f;
            mooseTarget = null;
            float best = mooseSpotRange * mooseSpotRange;
            foreach (var col in Physics.OverlapSphere(transform.position, mooseSpotRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var m = col.GetComponentInParent<MoosePsyche>();
                if (m == null || !m.TryGetComponent<Health>(out var hp)) continue;
                float d = (m.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; mooseTarget = hp; }
            }
        }
        if (mooseTarget == null)
        {
            if (mooseHunting) { mooseHunting = false; bite.SetTarget(playerHealth); leap.SetTarget(playerHealth); } // туша ушла/пала — доставки назад на игрока
            return false;
        }
        if (PackNearCount(packCountRadius) < shadowMinPack) return false; // одиночка обходит тушу молча

        Vector3 to = mooseTarget.transform.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        HuntHowl(mooseTarget.transform.position); // ЗАГОННЫЙ КЛИЧ: голоса наслаиваются — раскачка духа до навала

        if (morale != null && morale.IsCommitted) // НАВАЛ
        {
            mooseHunting = true;
            bite.SetTarget(mooseTarget); leap.SetTarget(mooseTarget);
            if (Time.time >= nextAttackTime && Vector3.Angle(transform.forward, dir) <= bite.HalfAngle)
            {
                if (dist <= bite.Range) { if (bite.TryUse()) activeAbility = bite; Settle(Vector3.zero); return true; }
                if (dist >= leap.MinRange && dist <= leap.MaxRange && (Breath == null || Breath.Has(LeapCost)))
                { if (leap.TryUse()) { Breath?.TrySpend(LeapCost); activeAbility = leap; } Settle(Vector3.zero); return true; }
            }
            Settle(nav.Arrive(mooseTarget.transform.position, Speed, stopAt: bite.Range * 0.85f) + Separation() * attackSeparation);
            return true;
        }

        // ТЕНЬ: кружим на своём личном месте у границы лесенки, подгоняя тушу (arrive — без дрожи)
        if (mooseHunting) { mooseHunting = false; bite.SetTarget(playerHealth); leap.SetTarget(playerHealth); } // дух упал — из навала в тень
        Vector3 spot = mooseTarget.transform.position + personalOffset.normalized * shadowRange;
        Settle(nav.Arrive(spot, Speed * circleSpeed, arriveRadius) + Separation()); // тот же arrive, теперь общий
        return true;
    }

    // ЕДИНАЯ МАШИНА ЗАХВАТА (общая со змеёй и хвостом игрока): волк — держатель СТ.1 (плоский пин для стаи).
    // Механику удержания (ApplyGrab/слоу/статус) ведёт она; РЕШЕНИЯ (когда/кого/релиз/кровь/подтягивание) —
    // драйвер. Порог срыва-по-удару высок: волк отпускает по НОКБЕКУ/рывку/прорежению стаи (логика драйвера),
    // а не по одиночному удару — поведение как было, спасение по-прежнему = таран лося (нокбек)
    // ДЫХАЛКА ВОЛКА: погоня замораживает бак (слив = его реген), прыжок из него вычитает. Отсюда «волны»:
    // прыгнул дважды — дальше грызи вблизи или отвались отдышаться. Стая перестаёт быть монолитом
    [SerializeField] float chaseDrain = 10f; // расход в секунду, пока гонится (= реген волка)
    [SerializeField] float leapCost = 30f;   // прыжок — рывок усилия (из бака 86 это два прыжка)
    float ChaseDrain => chaseDrain > 0f ? chaseDrain : 10f;
    float LeapCost => leapCost > 0f ? leapCost : 30f;
    Stamina breath;
    // ленивая привязка: бак до-создаёт тело в Recompute, он бывает позже нашего Awake
    Stamina Breath { get { if (breath == null) TryGetComponent(out breath); return breath; } }

    Constrict grabMachine;
    Constrict GrabMachine
    {
        get
        {
            if (grabMachine == null)
            {
                if (!TryGetComponent(out grabMachine)) grabMachine = gameObject.AddComponent<Constrict>();
                grabMachine.SetMaxStage(1);                 // слабейший держатель: хват не давит, стая грызёт
                grabMachine.ConfigureHolder(99f, 99f, 9999); // жертва-игрок (escape N/A); срыв-по-удару отключён — релиз у драйвера
            }
            return grabMachine;
        }
    }

    /// <summary>Хватать ли вообще: захват удерживает жертву, но САМ урона не наносит — держащий волк стоит
    /// на месте и превращается в неподвижную мишень. В одиночку это самоубийство, в стае — роль: один держит,
    /// остальные грызут. Поэтому вцепляемся, только когда рядом навалилось `grabMinPack` (по умолчанию 3+).</summary>
    bool PackReadyForGrab() => pack.EngagedNear(transform.position, grabPackRadius) >= grabMinPack;

    /// <summary>Сколько нас должно остаться рядом, чтобы ПРОДОЛЖАТЬ держать. Порог ниже, чем для входа в хват
    /// (гистерезис: иначе на границе выйдет флип-флоп «разжал — вцепился»), и его двигает АГРЕССИЯ особи —
    /// злая висит мёртвой хваткой до конца, осторожная разжимает, оставшись одна. Счёт включает самого
    /// держащего, поэтому порог 1 означает «не отпускаю никогда».</summary>
    int HoldThreshold()
    {
        float aggr = personality != null ? personality.Aggression : 1f; // 0.85…1.2, центр 1
        return Mathf.Max(1, Mathf.RoundToInt(grabMinPack - 1 - (aggr - 1f) * grabGrit));
    }

    // приземлился прыжком у цели — шанс сразу вцепиться (с новым ритмом «кольцо → прыжок» мили-ветка
    // захвата почти недостижима, поэтому захват заходит С ПРЫЖКА; урона не добавляет — только контроль)
    bool TryFollowUpGrab()
    {
        if (huntingPrey) return false; // захват завязан на игрока — змею только грызём
        if (!PackReadyForGrab()) return false;
        Vector3 to = target.position - transform.position; to.y = 0f;
        if (to.magnitude > bite.Range) return false;
        if (!pack.TryAcquireGrab(this) || Random.value >= grabChance) { pack.ReleaseGrab(this); return false; }
        BeginGrabWindup();
        pack.ReleaseAttack(this); hasToken = false; // захват — отдельная роль, освобождает слот атакующего
        return true;
    }

    // замах захвата: отменяется уворотом из зоны/конуса (стаггер ловится выше, в Update)
    void UpdateGrabWindup(float dist, bool inCone)
    {
        if (!(dist <= bite.Range && inCone)) Disengage(0.3f);
        else if (Time.time >= windupEnd) StartGrab();
        Settle(Vector3.zero);
    }

    void BeginGrabWindup()
    {
        windingUp = true;
        windupEnd = Time.time + grabWindupTime;
        activeTelegraph = TelegraphColors.Grab;
        ShowTelegraph(activeTelegraph);
    }

    void StartGrab()
    {
        windingUp = false;
        activeTelegraph = TelegraphColors.Grab;
        ShowTelegraph(activeTelegraph); // приём (как и все) — распознаётся Чутьём, иначе тело просто светлеет
        if (playerCtl == null) return;

        // МАШИНА берёт игрока (ApplyGrab + слоу ст.1); this — IGrabber для срыва рывком
        var ph = playerHealth != null ? playerHealth : playerCtl.GetComponent<Health>();
        if (ph == null || !GrabMachine.Begin(ph, this)) { Disengage(attackCooldown); return; }
        grabbing = true;

        // ЗУБЫ СОМКНУЛИСЬ: вцепление — это клыки в теле, а не объятие. Разовая кровь за вход в хват;
        // дальше удержание снова «чистый контроль» (тикающий урон превратил бы захват в казнь).
        if (grabBleedStacks > 0)
        {
            var hit = new Hit(ownHealth, transform.position);
            for (int i = 0; i < grabBleedStacks; i++) hit.Apply(ph, HitEffect.Bleed());
        }
    }

    void UpdateGrab()
    {
        if (playerCtl != null && playerCtl.GrabImmune) { Disengage(attackCooldown); return; } // иммунитет к захвату — отпускаем

        // ПОДМОГУ ПЕРЕБИЛИ: держать в одиночку = стоять неподвижной грушей, ради чего захват и гейтится стаей.
        if (pack.EngagedNear(transform.position, grabPackRadius) < HoldThreshold()) { Disengage(attackCooldown); return; }

        // ТИК МАШИНЫ: слоу/статус — её дело. Для жертвы-игрока вернёт Holding (или Gone, если игрок умер)
        if (grabMachine.Tick() != GrabTick.Holding) { Disengage(attackCooldown); return; }

        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

        // висим вплотную, мягко подтягиваясь к точке удержания (игрока за собой не тащим)
        float hold = bite.Range * 0.6f;
        Vector3 pull = Vector3.ClampMagnitude(dir * (d - hold) * 8f, moveSpeed);
        Settle(pull);
        // отпускает только пинок (Knockback) или рывок (BreakFree) — ни таймаута, ни срыва ударом
    }

    // IGrabber: игрок сорвался рывком — урон цепляющемуся + лёгкий отлёт + отпускаем. Волк рвётся ВСЕГДА.
    public bool BreakFree(int damage)
    {
        if (grabbing)
        {
            if (ownHealth != null && damage > 0)
            {
                ownHealth.LastAttacker = playerCtl != null ? playerCtl.GetComponent<Health>() : null; // срыв = удар игрока
                ownHealth.TakeDamage(damage);
            }
            if (knockback != null)
            {
                Vector3 away = transform.position - target.position; away.y = 0f;
                if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * ripSelfKnock);
            }
            Disengage(attackCooldown);
        }
        return true;
    }

    void ReleaseToken()
    {
        if (pack != null) { pack.ReleaseAttack(this); pack.ReleaseGrab(this); }
        hasToken = false;
    }

    // снять текущий приём, вернуть жетоны и (опц.) уйти в кулдаун
    void Disengage(float cooldown)
    {
        if (grabbing && grabMachine != null) grabMachine.End(); // машина снимет ReleaseGrab у игрока
        grabbing = false;
        windingUp = false;
        HideTelegraph();
        ReleaseToken();
        // ритм атаки: АГРЕССИЯ особи (личность — >1 частит) + лёгкая рандомизация (±10%, рассинхрон стаи)
        if (cooldown > 0f) nextAttackTime = Time.time + cooldown / (personality != null ? Mathf.Max(0.5f, personality.Aggression) : 1f) * Random.Range(0.9f, 1.1f);
    }

    // мораль сломлена: сбрасываем приём/захват/жетоны и бежим прочь от игрока
    void Rout()
    {
        if (windingUp || grabbing || hasToken) Disengage(0f);
        pack.LeaveRing(this); // паника — покидаем строй, слот кольца свободен
        ForgetAlert();        // со страху теряем и точку сбора (игрока) — назад в поиск
        Vector3 from = target != null ? target.position : alertPos;
        Vector3 away = transform.position - from; away.y = 0f;
        Vector3 dir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        Settle(dir * Speed * (morale != null ? morale.FleeSpeedMult : 1f) + Separation()); // «страх окрыляет»: до +30% к бегству
    }

    // вой: зову ближних волков (в радиусе) на точку + бешу их — глобального алерта на всю карту больше нет.
    // Координация стаи (куда бежать, ярость) — ЯЗЫК, идёт через pack; ЗВУК воя — всплеск Noise: мир слышит
    // (лось пойдёт проверить, будущие уши тоже) — физика и семантика разнесены
    void TryHowl(Vector3 pos)
    {
        if (Time.time < nextHowlTime) return;
        // вой — событие СТАИ, не хор: голос подаёт ОДИН (иначе фон морали = размер стаи и страх не пробивает)
        if (!pack.TryClaimHowl()) { nextHowlTime = Time.time + 1f; return; }
        nextHowlTime = Time.time + howlCooldown;
        pack.Howl(transform.position, howlRadius, pos);
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 0.8f, TelegraphColors.Howl); // вой ЗВУЧИТ в мире (тон = цвет голоса)
        FlashTelegraph(TelegraphColors.Howl, howlCueTime); // видимый сигнал: волк зовёт стаю
    }

    // ЗАГОННЫЙ КЛИЧ (тень у туши): БЕЗ гейта-хора — голоса стаи в загоне НАСЛАИВАЮТСЯ (сумма живых
    // воёв ≈ размер стаи — «мелкая не докачивается, большая валит» выходит из арифметики); личный КД
    // остаётся. Обычные вои (по игроку) гейтятся хором как прежде — фон морали там не растёт
    void HuntHowl(Vector3 pos)
    {
        if (Time.time < nextHowlTime) return;
        nextHowlTime = Time.time + howlCooldown;
        pack.Howl(transform.position, howlRadius, pos); // Hear (сородичи сходятся к туше) + Cheer +1
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 0.8f); // клич звучит в мире (лоси слышат — настораживаются)
        FlashTelegraph(TelegraphColors.Howl, howlCueTime);
    }

    // телеграф через таймер telegraphUntil: персистентный (до Disengage) / краткая вспышка / гашение
    // ПРИЁМ всегда намерение: цвет читает лишь Чутьё, иначе тело светлеет своим (единое правило для всех видов)
    void ShowTelegraph(Color c) { telegraph.Set(true, c, intent: true); telegraphUntil = 0f; }
    void FlashTelegraph(Color c, float dur)
    {
        if (windingUp || grabbing) return; // не перебиваем телеграф приёма
        telegraph.Set(true, c, intent: true);
        telegraphUntil = Time.time + dur;
    }
    void HideTelegraph() { telegraph.Clear(); telegraphUntil = 0f; }

    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        int n = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, neighbors, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = neighbors[i];
            if (col.transform == transform) continue;
            if (col.GetComponentInParent<WolfPsyche>() == null) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f) push += away.normalized / Mathf.Max(d, 0.7f); // пол по дистанции — не взрывается вплотную
        }
        return Vector3.ClampMagnitude(push * separationStrength, moveSpeed); // кап силы — без радиальных рывков
    }

    // ход отдан общей локомоции (NavLocomotion.Move): гравитация + сглаживание скорости — анти-тряска
    // живёт в одном месте на все виды
    // ЕДИНАЯ ТОЧКА ДВИЖЕНИЯ — сюда и вешаем дыхалку погони. Нагрузка (`Drain`), а не усилие: на бегу бак
    // не восстанавливается, но сам по себе и не пустеет — пустеют ПРЫЖКИ. Гейт по `Engaged` обязателен,
    // иначе бродящий без дела волк тоже «уставал» бы и никогда не отдыхал
    void Settle(Vector3 horizontal)
    {
        if (Engaged && horizontal.sqrMagnitude > 0.5f) Breath?.Drain(ChaseDrain * Time.deltaTime);
        nav.Move(horizontal);
    }
}
