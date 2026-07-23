using UnityEngine;

/// <summary>
/// ЁЖ «ХЕДЖХАЛК» — психика (слайс A + экосистемные связи).
///
/// МЕСТО В КРУГЕ (решение пользователя): ёж ОХОТИТСЯ на змей, ОПАСАЕТСЯ волков, НЕЙТРАЛЕН к лосям.
/// Круг замыкается: ёж бьёт змею, змея бьёт волка, волк берёт ежа числом. У каждого есть и добыча,
/// и охотник — поэтому биом живёт сам, без сценариев.
///
/// Лестница отчаяния (не подпускает → уходит → клубок → катание → выдохся) приедет слайсом D, когда
/// будут снаряды и альт-шасси: сейчас её нечем играть.
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
    // ОПАСАЕТСЯ СТАИ, А НЕ ВОЛКА. Порог обязателен: волков на арене десятки, и «отходить от любого
    // ближнего» означало отходить всегда — ежи уползали по прямой и забивались в углы. Колючему зверю
    // одиночный волк не страшен (иглы), опасно именно ЧИСЛО — так и в спеке: стая берёт его числом
    [SerializeField] float wolfFearRadius = 8f;
    [SerializeField] int wolfFearCount = 2;      // сколько волков рядом, чтобы начать отходить
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

    Stamina breath;
    // ленивая привязка: бак до-создаёт тело в Recompute, он бывает позже нашего Awake
    Stamina Breath { get { if (breath == null) TryGetComponent(out breath); return breath; } }

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
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        TryGetComponent(out rage);
        TryGetComponent(out variance);
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        TryGetComponent(out volley); // залп — ТОЛЬКО с префаба (орган Иглы): нет компонента = ближний ёж
        if (!TryGetComponent(out senses)) senses = gameObject.AddComponent<Senses>();
        if (!TryGetComponent(out alert)) alert = gameObject.AddComponent<AlertState>();

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

        Retarget();

        // ОПАСКА ВОЛКОВ — отходим, а не паникуем: ёж не убегает в ужасе, он не даёт себя окружить.
        // Проверяем ДО боя: волк рядом важнее любой добычи
        Vector3 packCenter = Vector3.zero;
        if (WolvesNear(ref packCenter))
        {
            Vector3 away = transform.position - packCenter; away.y = 0f;
            if (away.sqrMagnitude > 0.001f)
            {
                // уходим ПО ДУГЕ, а не по прямой: чистое «прочь от стаи» упирает в стену и держит там,
                // пока стая не подойдёт вплотную. Боковая составляющая даёт скольжение вдоль препятствий
                Vector3 dir = (away.normalized + Vector3.Cross(Vector3.up, away.normalized) * 0.45f).normalized;
                Face(dir);
                Settle(dir * Speed);
                alert.Observe(true, true);
                return;
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

        Health prey = null; float best = preyRange * preyRange;
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

    /// <summary>Стая рядом? Считаем волков в радиусе и отдаём их ЦЕНТР — отходить надо от кучи, а не от
    /// ближайшего: иначе ёж шарахается между двумя волками, стоящими по бокам.</summary>
    bool WolvesNear(ref Vector3 center)
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var col in Physics.OverlapSphere(transform.position, wolfFearRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var wolf = col.GetComponentInParent<WolfPsyche>();
            if (wolf == null) continue;
            sum += wolf.transform.position; n++;
        }
        if (n < wolfFearCount) return false;
        center = sum / n;
        return true;
    }

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
