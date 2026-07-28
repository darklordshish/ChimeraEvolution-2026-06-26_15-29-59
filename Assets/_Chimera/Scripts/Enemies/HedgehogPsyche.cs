using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ЁЖ «ХЕДЖХАЛК» — психика (слайс A + экосистемные связи).
///
/// МЕСТО В КРУГЕ (решение пользователя): ёж ОХОТИТСЯ на змей, ОПАСАЕТСЯ волков, НЕЙТРАЛЕН к лосям.
/// Круг замыкается: ёж бьёт змею, змея бьёт волка, волк берёт ежа числом. У каждого есть и добыча,
/// и охотник — поэтому биом живёт сам, без сценариев.
///
/// Лестница отчаяния собирается по слайсам: залп-замедление (не подпускает) и уход перекатами есть;
/// КЛУБОК (C1) — угроза давит → сворачивается (`CurlDefense`: броня↑ + жжёт стамину). ПРОДАВИЛИ — дыхалка
/// на исходе → КАТАНИЕ (C2): прорыв тараном сквозь угрозу, подруливая к ней. Окно «на спине» (C3) и полная
/// психика-лестница (5 ступеней + Cornered + закольцовка страха→ярость) — слайс D.
///
/// Опасность ежа держится НЕ психикой, а телом: иглы (`Thorns`) наказывают удар в упор, ядоупорное
/// сердце (`VenomResist`) обесценивает змеиный укус. Оба компонента вешает `CreatureBody` по флагам
/// органов — психика о них не знает и знать не должна.
/// </summary>
[RequireComponent(typeof(Health))]
public class HedgehogPsyche : MonoBehaviour, IBodyStatConsumer
{
    [Header("Чувства (ночной зверь: слух и нюх остры, зрение слабое)")]
    [SerializeField] float sightRange = 14f;
    [SerializeField] float sightHalfAngle = 65f;
    [SerializeField] float hearRange = 20f;
    [SerializeField] float scentRange = 16f;
    [SerializeField] float proximityRadius = 2f;

    [Header("Экосистема")]
    [SerializeField] float preyRange = 18f;      // в каком радиусе ищем ЗМЕЙ (добыча)
    // ОПАСАЕТСЯ ЧИСЛА, А НЕ ОДИНОЧКИ. Угроза = ЧУЖОЙ ХИЩНИК (не-кин + eatsMeat) — обобщено через
    // CreatureBody, не хардкод на волка: так же сработает игрок-хищник и будущие виды, а травоядный
    // лось и кин-игрок — нет. Колючему зверю одиночка не страшен (иглы), опасно ЧИСЛО (стая берёт числом)
    [SerializeField] float wolfFearRadius = 8f;  // радиус скана угроз (имя-легаси, сериализация на префабе)
    [SerializeField] int wolfFearCount = 2;      // сколько угроз рядом, чтобы отходить / считаться «окружили»
    [SerializeField] float curlTriggerRange = 2.8f; // угроза ближе этого = «в упор» (слайс C)
    [SerializeField] float hurtCurlWindow = 2.5f;   // сколько «помним» удар не-кина как повод держать клубок (перестал бить → через столько развернётся)
    [SerializeField] float rollAtBreath = 0.3f;     // доля стамины, ниже которой свёрнутый ёж идёт на ПРОРЫВ катанием (C2)
    [SerializeField] float retargetInterval = 0.7f;

    [Header("Бой")]
    [SerializeField] float rotationSpeed = 240f;
    [SerializeField] float attackCooldown = 1.1f;
    [SerializeField] float wanderRadius = 7f;
    [SerializeField] float grabCooldown = 3f;    // пауза между попытками вцепиться

    float moveSpeed = 4f; // приходит из органов (Ежиные ноги) через тело

    Transform target;
    Health targetHealth, ownHealth, playerHealth;
    PlayerController playerCtl;
    NavLocomotion nav;
    BiteAbility bite;
    QuillVolley volley;   // залп — ДАЛЬНЯЯ грань (первый ranged в игре); есть только если орган Иглы даёт его
    float nextVolleyAt;
    [SerializeField] float volleyCooldown = 2.5f;
    Stagger stagger;
    Knockback knockback;
    Senses senses;
    AlertState alert;
    Rage rage;
    SpawnVariance variance;
    WindupAbility activeAbility;
    float nextAttackTime, nextRetarget, nextGrabAt;
    bool huntingPrey, holding;
    CurlDefense curl; // КЛУБОК (слайс C): оборонительная стойка последнего рубежа — свернулся, когда стая в упор

    Stamina breath;
    // ленивая привязка: бак до-создаёт тело в Recompute, он бывает позже нашего Awake
    Stamina Breath { get { if (breath == null) TryGetComponent(out breath); return breath; } }

    Satiety satiety; // шкала сытости-голода (M3): сытый ёж не гоняется за змеями
    Satiety Belly { get { if (satiety == null) TryGetComponent(out satiety); return satiety; } }

    // UNITY-ГОЧА: новое [SerializeField]-поле у психики, УЖЕ лежащей на префабе, приходит НУЛЁМ (инициализатор
    // применяется только к свежесозданным). Порог 0 = «в упор ≤0 никогда» → клубок молча не сработает. Читаем 0 как «не настроено»
    float CurlTrigger => curlTriggerRange > 0f ? curlTriggerRange : 2.8f;
    float HurtWindow => hurtCurlWindow > 0f ? hurtCurlWindow : 2.5f;
    float RollAtBreath => rollAtBreath > 0f ? rollAtBreath : 0.3f;

    CreatureBody ownBody; // кин-признание угроз (Regard): свой вид и травоядные — не повод для клубка
    float hurtUntil;      // не-кин ударил недавно → сворачиваемся (Health.onDamaged — «кто атакует»)

    /// <summary>ЦЕПКАЯ ПАСТЬ — та же машина захвата, что у волка и змеи, но на ОДНУ стадию: ёж не душит
    /// и не заваливает, он ВЦЕПЛЯЕТСЯ и мотает головой, добивая. Это последнее звено анти-змеиной связки:
    /// змея бросается → срывается об иглы → ёж вцепился → добил.
    ///
    /// Захват ЖЖЁТ СТАМИНУ (общее правило машины), а стамина ежу нужна на оборону — отсюда честный выбор
    /// внутри вида: держать добычу или приберечь дыхалку. Вцепившийся ёж не может свернуться.</summary>
    Constrict grabMachine;
    Constrict GrabMachine
    {
        get
        {
            if (grabMachine == null)
            {
                if (!TryGetComponent(out grabMachine)) grabMachine = gameObject.AddComponent<Constrict>();
                grabMachine.SetMaxStage(1);              // плоский пин, как у волка: держит, но не защёлкивает
                grabMachine.ConfigureHolder(3.5f, 5.5f, 7); // жертва вырывается сама; сильный удар извне рвёт хват
            }
            return grabMachine;
        }
    }

    float Speed => moveSpeed * (rage != null ? rage.SpeedMult : 1f)
                             * (variance != null ? variance.SpeedMult : 1f)
                             * (Breath != null ? Breath.MoveMult : 1f);

    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed, float howlRange)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite == null) return;
        bite.SetDamage(damage);
        bite.SetVenom(venom);
        bite.SetBleed(bleed);
    }

    void Awake()
    {
        ownHealth = GetComponent<Health>();
        TryGetComponent(out ownBody);                 // тело ежа — для кин-признания угроз (Regard)
        ownHealth.onDamaged.AddListener(OnHurt);       // «кто атакует»: удар не-кина → повод свернуться
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        TryGetComponent(out rage);
        TryGetComponent(out variance);
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        TryGetComponent(out volley); // залп — ТОЛЬКО с префаба (орган Иглы): нет компонента = ближний ёж
        if (!TryGetComponent(out senses)) senses = gameObject.AddComponent<Senses>();
        if (!TryGetComponent(out alert)) alert = gameObject.AddComponent<AlertState>();
        if (!TryGetComponent(out curl)) curl = gameObject.AddComponent<CurlDefense>(); // клубок — фича ежа (тюнить на префабе)

        // профиль чувств: зрение скупое и КОНУСОМ, слух и нюх щедрые и круговые
        senses.Seed(SenseKind.Sight, sightRange);
        senses.SeedViewAngle(SenseKind.Sight, sightHalfAngle);
        senses.Seed(SenseKind.Hearing, hearRange);
        senses.SeedCalmMult(SenseKind.Hearing, 1f); // уши дежурят и у спокойного
        senses.Seed(SenseKind.Scent, scentRange);
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();
        if (playerCtl != null)
        {
            playerHealth = playerCtl.GetComponent<Health>();
            target = playerCtl.transform;
            targetHealth = playerHealth;
            bite.SetTarget(targetHealth);
        }
    }

    void OnDestroy() { if (ownHealth != null) ownHealth.onDamaged.RemoveListener(OnHurt); }

    // «КТО АТАКУЕТ» — ударил не-кин → окно, в котором ёж сворачивается. Клубок реагирует на ДАВЛЕНИЕ,
    // а не на вид: Regard решает свой/чужой, onDamaged — что бьют. Случайный удар кина угрозой не считаем
    void OnHurt()
    {
        var atk = ownHealth.LastAttacker;
        if (atk == null) return;
        var body = atk.GetComponentInParent<CreatureBody>();
        if (body != null && body != ownBody && CreatureBody.Regard(ownBody, body) == KinTier.None)
            hurtUntil = Time.time + HurtWindow;
    }

    void Update()
    {
        if (ownHealth == null) return;

        // пинок рвёт всё, включая хват
        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            ReleaseGrab();
            return;
        }

        // ДЕРЖИМ ДОБЫЧУ: пока хват жив, ёж стоит и грызёт. Машина сама снимет хват, когда жертва
        // вырвется, её отобьют ударом извне или у ежа кончится дыхалка
        if (holding)
        {
            if (GrabMachine.Tick() != GrabTick.Holding) { ReleaseGrab(); }
            else
            {
                if (activeAbility != null)
                {
                    if (activeAbility.Tick() == AbilityRun.Running) return;
                    activeAbility = null;
                    nextAttackTime = Time.time + attackCooldown;
                }
                else if (Time.time >= nextAttackTime && bite.TryUse()) activeAbility = bite;
                Settle(Vector3.zero);
                return;
            }
        }

        if (activeAbility != null)
        {
            if (stagger != null && stagger.IsStaggered) activeAbility.Abort(false);
            if (activeAbility.Tick() == AbilityRun.Running) return;
            activeAbility = null;
            nextAttackTime = Time.time + attackCooldown;
            return;
        }

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        // КЛУБОК (слайс C): свёрнутый ёж только держит шар — не ретаргетит, не стреляет, не ходит.
        // Держим, пока рядом угроза, что давит (в упор ИЛИ недавно ударила), и есть дыхалка
        if (curl != null && curl.Curled)
        {
            // УЖЕ КАТИМСЯ — докатываем (коммит, как таран), подруливая к угрозе, пока не выдохлись
            if (curl.Rolling)
            {
                Vector3 rollAim = ThreatsNear(out Vector3 rc, out _, out _) ? rc - transform.position : transform.forward;
                if (curl.RollTick(rollAim)) alert.Observe(true, true);
                return; // выдохся — RollTick сам развернул (окно «на спине» — C3)
            }

            bool near = ThreatsNear(out Vector3 tc, out float nsq, out _);
            bool press = Time.time < hurtUntil || (near && nsq <= CurlTrigger * CurlTrigger);
            if (press)
            {
                // ПРОДАВИЛИ: дыхалка на исходе → ПРОРЫВ катанием сквозь угрозу (последний рывок перед выдохом)
                if (Breath != null && !Breath.Exhausted && Breath.Normalized < RollAtBreath)
                {
                    curl.RollTick(near ? tc - transform.position : transform.forward);
                    alert.Observe(true, true);
                    return;
                }
                if (curl.Hold()) { Settle(Vector3.zero); alert.Observe(true, true); return; }
            }
            curl.Uncurl();
            return;
        }

        Retarget();

        // АТАКОВАН не-кином (кто бьёт — угроза, вид не важен: волк, игрок, задевший тараном лось) →
        // СВОРАЧИВАЕМСЯ, если есть дыхалка. «Кто атакует» — прямая просьба, реагируем на удар, не на вид
        if (curl != null && Time.time < hurtUntil && (Breath == null || !Breath.Exhausted))
        {
            curl.Curl();
            Settle(Vector3.zero);
            alert.Observe(true, true);
            return;
        }

        // УГРОЗЫ-ХИЩНИКИ рядом — не паникуем, не даём окружить. Проверяем ДО боя: хищник важнее добычи
        if (ThreatsNear(out Vector3 threatCenter, out float nearestSq, out int threatN))
        {
            // ОКРУЖИЛИ В УПОР (≥ порога хищников продавили дистанцию) → клубок превентивно, пока не ударили
            if (curl != null && threatN >= wolfFearCount && nearestSq <= CurlTrigger * CurlTrigger
                && (Breath == null || !Breath.Exhausted))
            {
                curl.Curl();
                Settle(Vector3.zero);
                alert.Observe(true, true);
                return;
            }

            // стая ЧИСЛОМ, но не в упор → отходим ПО ДУГЕ, не даём окружить (одиночку на дистанции игнорим — иглы)
            if (threatN >= wolfFearCount)
            {
                Vector3 away = transform.position - threatCenter; away.y = 0f;
                if (away.sqrMagnitude > 0.001f)
                {
                    // боковая составляющая даёт скольжение вдоль стен: чистое «прочь» упирало в угол и держало там
                    Vector3 dir = (away.normalized + Vector3.Cross(Vector3.up, away.normalized) * 0.45f).normalized;
                    Face(dir);
                    Settle(dir * Speed);
                    alert.Observe(true, true);
                    return;
                }
            }
        }

        if (target == null) { Wander(); return; }

        // ДОБЫЧУ (змею) ёж ЧУЕТ по нюху и идёт к ней даже вслепую (в манке, за углом) — он ночной охотник.
        // ИГРОКА же трогает только когда ВИДИТ. Отсюда: не вижу и не чую добычу — брожу
        bool sees = Perception.Sees(transform, target, senses, proximityRadius);
        alert.Observe(sees || huntingPrey, false);
        if (!sees && !huntingPrey) { Wander(); return; }

        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        if (dist > 0.001f) Face(to.normalized);

        // ДАЛЬНЯЯ ГРАНЬ — ЗАЛП ИГЛАМИ: держит дистанцию и осыпает. Число попавших замедляет цель — по
        // добыче это КИТ (увязла → подошёл → схватил), по игроку — «не подпускаю». В окне дистанции залпа
        // не сближаемся: стоим и стреляем. НУЖЕН ВИЗУАЛЬНЫЙ КОНТАКТ — в невидимку (манок) не прицелиться
        if (volley != null && sees && dist >= volley.MinRange && dist <= volley.MaxRange && Time.time >= nextVolleyAt)
        {
            volley.SetTarget(targetHealth);
            if (volley.TryUse())
            {
                activeAbility = volley;
                nextVolleyAt = Time.time + volleyCooldown;
                Settle(Vector3.zero);
                return;
            }
        }

        // ВЦЕПИТЬСЯ — только в ДОБЫЧУ (змею): игрока и прочих ёж не хватает, он для них крепость,
        // а не контролёр. Хват дорог по дыхалке, поэтому не пробуем, когда её нет
        if (huntingPrey && dist <= bite.Range && Time.time >= nextGrabAt
            && (Breath == null || !Breath.Exhausted) && GrabMachine.Begin(targetHealth))
        {
            holding = true;
            nextGrabAt = Time.time + grabCooldown;
            Settle(Vector3.zero);
            return;
        }

        if (Time.time >= nextAttackTime && dist <= bite.Range && bite.TryUse())
        {
            activeAbility = bite;
            Settle(Vector3.zero);
            return;
        }

        Settle(nav.Arrive(target.position, Speed, stopAt: bite.Range * 0.85f));
    }

    // цель: ближайшая ЗМЕЯ в радиусе (добыча) — иначе игрок. Лось не выбирается никогда: нейтралитет
    void Retarget()
    {
        if (Time.time < nextRetarget) return;
        nextRetarget = Time.time + retargetInterval;

        // M3: СЫТЫЙ ёж не охотится — не гоняется за змеями, чилит (цель = игрок/брожение). Голод включает охоту
        Health prey = null;
        bool sated = Belly != null && Belly.IsSated;
        float best = preyRange * preyRange;
        if (!sated)
        foreach (var col in Physics.OverlapSphere(transform.position, preyRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var snake = col.GetComponentInParent<SnakePsyche>();
            if (snake == null) continue;
            // КАМУФЛЯЖ ЗМЕИ ЕЖУ НЕ ПОМЕХА (в отличие от волка, что охотится глазами): ёж — ночной охотник
            // по НЮХУ, он чует затаившуюся змею и в манке. Именно это делает его её хищником: спрятаться
            // от глаз можно, от носа — нет
            float d = (snake.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; prey = snake.GetComponent<Health>(); }
        }

        Transform newTarget = prey != null ? prey.transform : (playerCtl != null ? playerCtl.transform : null);
        Health newHealth = prey != null ? prey : playerHealth;
        if (ReferenceEquals(newTarget, target)) return;

        ReleaseGrab(); // цель сменилась — старую отпускаем
        target = newTarget;
        targetHealth = newHealth;
        huntingPrey = prey != null;
        if (bite != null) bite.SetTarget(newHealth);
    }

    readonly HashSet<CreatureBody> threatSet = new(); // дедуп: одно многосегментное существо (змея) = один голос

    /// <summary>Угрозы рядом? Считаем ЧУЖИХ ХИЩНИКОВ (не-кин + eatsMeat) в радиусе: count (число — для
    /// «окружили»/отхода), центр (отходить от кучи, а не от ближайшего — иначе шарахает между двумя по бокам)
    /// и квадрат дистанции до ближайшего (для «в упор»). Обобщено через CreatureBody — ни одного вида в коде.</summary>
    bool ThreatsNear(out Vector3 center, out float nearestSq, out int count)
    {
        threatSet.Clear();
        Vector3 sum = Vector3.zero; count = 0; nearestSq = float.MaxValue; center = Vector3.zero;
        foreach (var col in Physics.OverlapSphere(transform.position, wolfFearRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var other = col.GetComponentInParent<CreatureBody>();
            if (other == null || other == ownBody || !IsThreat(other) || !threatSet.Add(other)) continue;
            Vector3 p = other.transform.position;
            sum += p; count++;
            float d = (p - transform.position).sqrMagnitude;
            if (d < nearestSq) nearestSq = d;
        }
        if (count == 0) return false;
        center = sum / count;
        return true;
    }

    // УГРОЗА = ЧУЖОЙ ХИЩНИК: травоядные (лось, eatsMeat=false) не давят, кин (свой вид, кин-игрок) не враг.
    // Переиспользуем Regard (кин) + eatsMeat (хищник) — без исключений на конкретный вид. Змея формально
    // попадает (хищник, не-кин), но она добыча ежа: не бьёт его и одна не «окружает» — клубок не триггерит
    bool IsThreat(CreatureBody other) =>
        other.Chassis != null && other.Chassis.eatsMeat
        && CreatureBody.Regard(ownBody, other) == KinTier.None;

    void ReleaseGrab()
    {
        if (!holding) return;
        holding = false;
        if (grabMachine != null) grabMachine.End();
    }

    void Face(Vector3 dir) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir),
                                                      rotationSpeed * Time.deltaTime);

    /// <summary>Праздное блуждание. ВНИМАНИЕ: `nav.Wander` отдаёт ТОЧКУ, а не направление — её нужно
    /// превращать в ход через `Arrive`/`DirTo`. Отданная в `Move` напрямую, она уезжает в движение как
    /// мировая координата, и зверь ломится «прочь от начала координат», пока не упрётся в угол карты.</summary>
    void Wander()
    {
        Vector3 step = nav.Arrive(nav.Wander(wanderRadius), Speed * 0.55f, stopAt: 1f);
        // МОРДОЙ ПО ХОДУ: без этого зверь едет боком и задом, сохраняя ориентацию с последнего боя.
        // Поворот нужен не только в драке — «куда смотрит» читается игроком всё время
        if (step.sqrMagnitude > 0.01f) Face(step.normalized);
        Settle(step);
    }

    void Settle(Vector3 horizontal) => nav.Move(horizontal);
}
