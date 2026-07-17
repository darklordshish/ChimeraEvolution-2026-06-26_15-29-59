using UnityEngine;

/// <summary>
/// Психика змеи — СОЛО-засадный охотник ХОЛОДНОГО РАСЧЁТА. Цель — ближайшая ТЁПЛАЯ ОДИНОЧКА
/// в термо-радиусе: игрок ИЛИ волк — для змеи всё добыча, поведение унифицировано (первое
/// NPC-против-NPC в игре). Рядом с жертвой другие тёплые (стая, компания) — тихо сидит в засаде;
/// Massive-туши не выбирает (предпочтение ПСИХИКИ — механика хвата с ними честна: на стадию слабее).
/// Рывок из засады (`LeapAbility`) → ядовитый укус (`BiteAbility`) → ОБХВАТ — ЕДИНЫЙ захват (Grabbed,
/// спека 2026-07-17), одна стадийная машина на обе жертвы: ст.1 слабый хват (ИГРОК рвётся рывком/пинком,
/// NPC-жертва ДЕРЁТСЯ и вырывается по таймеру; урон от жертвы ослабляет хват) → ст.2 защёлк (стан) →
/// ст.3 удушение-DoT. Удар СПАСАТЕЛЯ извне рвёт хват (стая отбивает своего); вой-стан рвёт ст.1–2.
/// Впрыск яда на каждую новую стадию — обеим жертвам. Гремок мигает жёлтой погремушкой.
/// СЫТАЯ ОСТОРОЖНОСТЬ: убив добычу любым своим оружием (кольца/клыки/яд), тело СЫТО — переваривание
/// это ФИЗИОЛОГИЯ ШАССИ (компонент Digestion: бонус-реген до полного HP, урон будит; кормит канал
/// «родство — убийце»), психика лишь ведёт себя сыто: прячется переваривать на стену-насест
/// (полная невидимость) — охота на паузе, экосистема дышит.
/// Числа тела (урон/скорость) приходят из органов через IBodyStatConsumer.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
[RequireComponent(typeof(BiteAbility))]
[RequireComponent(typeof(LeapAbility))]
[RequireComponent(typeof(SpawnVariance))]
public class SnakePsyche : MonoBehaviour, IBodyStatConsumer, IGrabber
{
    [Header("Засада / термочутьё / выбор жертвы")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotationSpeed = 320f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float thermalRange = 14f;              // термозрение: видит тёплого сквозь укрытия
    [SerializeField, Range(0f, 1f)] float creepSpeed = 0.5f;
    [SerializeField] float roamSenseRadius = 22f;           // праздный поиск: чуем тёплую жизнь ЗА термо-радиусом — крадёмся к ней (не застываем точкой-приманкой)
    [SerializeField] float wanderRadius = 12f;              // ничего не чуем — тихо бродим этим радиусом (перемешиваем засады)
    [SerializeField] float lureInterval = 1.2f;             // ПРИМАНКА: жертва вне броска — гремим часто и маним
    [SerializeField] float rattleHearRadius = 15f;          // пассивный гремок: тихий (будит любопытство рядом)
    [SerializeField] float lureHearRadius = 28f;            // приманка: ГРОМКАЯ — тянет зверьё издалека (должна перекрывать термо и блуждание стаи)
    [SerializeField] float quietCrowdRadius = 8f;           // толпа: ≥quietCrowdSize ДРУГИХ тёплых в этом радиусе — гремок молчит, добыча брошена
    [SerializeField] int quietCrowdSize = 2;
    [SerializeField] int fleeCrowdSize = 5;                 // ПОЛНАЯ СТАЯ: столько тёплых рядом — хищник стал жертвой, бежим
    [SerializeField] float fleeCheckRadius = 12f;
    [SerializeField, Range(0.3f, 1f)] float fleeSpeedMult = 0.75f; // бежит чуть медленнее волков — настигаема (спасение = стена)

    [Header("Осторожность (сокрытие после бегства; × Caution личности)")]
    [SerializeField] float waryWait = 7f;                    // сколько сидим скрытно ПОСЛЕ того, как зрители разошлись (отсчёт заново при толпе)
    [SerializeField, Range(0f, 1f)] float traverseChance = 0.5f; // шанс не ждать на месте, а ОТПОЛЗТИ по стене прочь от стаи
    [SerializeField] float traverseTime = 4f;                // сколько секунд ползём траверсом вдоль стены

    [Header("Стены-убежище (3e)")]
    [SerializeField] float wallSeekRadius = 12f;            // при бегстве ищем стену-убежище в этом радиусе
    [SerializeField] float perchHeight = 5f;                // высота насеста над землёй (недосягаема для наземной стаи)
    [SerializeField] float climbSpeed = 5f;
    [SerializeField] float wallHugSpeed = 3f;               // как быстро прилипаем к плоскости стены (XZ)
    [SerializeField] float wallHugOffset = 0.4f;           // центр тела ПЕРЕД поверхностью (сторона арены) ~на радиус тела: прижат к стене, но не проваливается сквозь (стены тонкие, 1 м)
    [SerializeField, Range(0.3f, 1f)] float carrySpeedMult = 0.7f; // с ношей ползём к стене медленнее (тащит тушу)
    [SerializeField] float carryDrop = 1.2f;               // 3e-ii: насколько НИЖЕ головы висит жертва в кольцах на стене
    [SerializeField] float carryStandoff = 0.7f;           // отжатие жертвы ОТ плоскости стены (не влипает в неё)
    [SerializeField] float lonelyRadius = 9f;               // «одиночка» = нет ДРУГИХ тёплых в этом радиусе вокруг жертвы (× Caution:
                                                            // осторожная требует большей изоляции; ориентир — радиус спасения стаи ~12)
    [SerializeField] float retargetInterval = 0.5f;         // как часто пересматриваем выбор жертвы
    [SerializeField] float revealMemory = 2f;               // камуфляж: «раскрыта» столько после приёма (> кулдауна — не мигает в мили)
    [SerializeField] float rattleInterval = 3f;             // гремок: как часто затаившаяся змея выдаёт себя
    [SerializeField] float rattleCue = 0.4f;                // длительность мигания погремушки (звук ляжет сверху позже)
    [SerializeField, Range(0f, 1f)] float scentStrength = 0.35f; // запах слабый: не потеет, мало движется

    [Header("Кулдаун")]
    [SerializeField] float attackCooldown = 1.6f;

    [Header("Обхват (удушающий захват)")]
    [SerializeField, Range(0f, 1f)] float grabChance = 0.5f; // шанс обвить вместо простого укуса (в упор)
    [SerializeField] float grabWindup = 0.35f;               // замах перед обхватом (телеграф — увернись)
    [SerializeField] float tightenRate = 1f;                 // сжатие/сек: тянет к удушению (игрок)
    [SerializeField] float stage2At = 1.5f;                  // сжатие ≥ этого → стадия 2 (рывок/пинок не пускают)
    [SerializeField] float stage3At = 3.5f;                  // сжатие ≥ этого → стадия 3 (удушение-DoT)
    [SerializeField] float loosenPerDamage = 0.12f;          // 1 HP урона по змее откатывает сжатие (игрок)
    [SerializeField] float chokeDamage = 4f;                 // удушение игрока (ст.3): урон за тик
    [SerializeField] float chokeInterval = 0.5f;
    [SerializeField, Range(0f, 1f)] float grabSlow1 = 0.35f; // замедление игрока по стадиям (ход И рывок)
    [SerializeField, Range(0f, 1f)] float grabSlow2 = 0.15f;
    [SerializeField, Range(0f, 1f)] float grabSlow3 = 0f;    // ст.3 — полный корень
    [SerializeField] int ripSelfKnock = 5;                   // отлёт змеи, когда игрок сорвался рывком (ст.1)
    [SerializeField] int npcChokeDamage = 6;                 // удушение NPC-жертвы: урон за тик (со ст.3, как у игрока)
    [SerializeField] float npcChokeInterval = 0.6f;
    [SerializeField] float escapeMin = 2.6f;                 // NPC-жертва на слабом хвате (ст.1) вырывается через случайное время.
    [SerializeField] float escapeMax = 4f;                   // Окно ПОЗЖЕ защёлка (~1.8с): ОДИНОЧКА гонку не выигрывает — змея точно
                                                             // душит волка; таймер спасает лишь сильно ослабившую хват жертву
    [SerializeField] float npcLoosenPerDamage = 0.04f;       // укус ЖЕРТВЫ ослабляет хват, но не сбрасывает гонку (её спасение — СТАЯ:
                                                             // внешний урон рвёт всегда); 0.12 игрока — его контр-игра, не трогать

    CharacterController controller;
    Stagger stagger;
    Knockback knockback;
    Health ownHealth;
    PlayerController playerCtl;
    Telegraph telegraph;
    NavLocomotion nav;
    SpawnVariance variance;
    BiteAbility bite;
    LeapAbility leap;
    WindupAbility activeAbility;   // укус/рывок в процессе — психика его тикает
    Camouflage camo;               // камуфляж (Чешуя): раскрываем на время боя (лениво — вешается после нашего Awake)
    AlertState alert;              // S1: общая машина восприятия (зеркало — поведение ещё не завязано на State)
    Senses senses;                 // S1: сенсорный профиль — термо-дальность идёт через него (пер-состоянчато; множитель=1 пока)

    // текущая ЖЕРТВА охоты (игрок или NPC) — выбирается сканом «тёплая одиночка»
    Transform target;
    Health targetHealth;

    // жертва ОБХВАТА фиксируется на входе (retarget её не трогает)
    Health heldHealth;
    Grabbed heldGrabbed;   // единый статус захвата на NPC-жертве (импульс-стагер, защёлк-стан — держит он)
    Grabbed grabbedStatus; // а это НАС схватили (хвост игрока) — на слабом хвате кусаемся в ответ
    bool heldIsPlayer;
    int heldStageCap;      // Massive-жертва — на стадию слабее (универсальное правило хвата)
    float escapeAt;        // NPC-жертва: момент вырывания со слабого хвата (гонка)

    float nextAttackTime, verticalVel, windupEnd, nextRattle, rattleBlinkUntil, nextScan, nextFleeCheck, groundY;
    bool fleeing;
    Vector3 fleeDir;

    enum ClimbPhase { None, Approach, Rise, Perch, Descend }
    ClimbPhase climb;
    Personality personality; // ЛИЧНОСТЬ (вешает тело): ось ОСТОРОЖНОСТИ — выжидание в сокрытии/склонность отползать
    bool wary, traversing;   // сокрытие после бегства (насест = укрытие, НЕ засада: манок молчит); отползание по стене
    int traverseSign;        // направление траверса вдоль стены (прочь от стаи)
    float waryUntil, traverseUntil;

    float Caution => personality != null ? personality.Caution : 1f;
    Digestion digestion; // ПЕРЕВАРИВАНИЕ — физиология ТЕЛА (шасси змеи, вешает CreatureBody после нашего Awake — лениво).
                         // Сытость/бонус-реген/пробуждение уроном — там; наша СЫТАЯ ОСТОРОЖНОСТЬ (насест, невидимость) — здесь
    float nextWallSeek;  // ретрай поиска стены при переваривании на земле
    Vector3 wallPoint, wallNormal;

    // сыты? (тело решает; нет компонента переваривания — вечно голодная охотница)
    bool Digesting
    {
        get
        {
            if (digestion == null) TryGetComponent(out digestion);
            return digestion != null && digestion.IsDigesting;
        }
    }
    bool windingUp, constricting;                 // windingUp — только замах ОБХВАТА
    float grip, gripFloor, chokeNext;             // игрок: «сжатие» + ратчет (ниже достигнутой стадии не откат)
    int stage, maxStage, lastHp;                  // stage 1..3; maxStage — яд впрыскивается раз на новую стадию
    Renderer rattleRenderer;                      // жёлтая погремушка: гремок мигает ИМЕННО ей

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        if (!TryGetComponent(out leap)) leap = gameObject.AddComponent<LeapAbility>();
        if (!TryGetComponent(out variance)) variance = gameObject.AddComponent<SpawnVariance>();
        if (!TryGetComponent(out alert)) alert = gameObject.AddComponent<AlertState>(); // общая машина восприятия (S1)
        if (!TryGetComponent(out senses)) senses = gameObject.AddComponent<Senses>(); // сенсорный профиль (S1)
        senses.Seed(SenseKind.Thermal, thermalRange); // сид базовой термо-дальности (если профиль не задан на префабе)

        if (!TryGetComponent<ScentTrail>(out var scent)) scent = gameObject.AddComponent<ScentTrail>(); // змея тоже пахнет — нюх её ловит (RPS)…
        scent.SetStrength(scentStrength); // …но слабо: не потеет, мало движется — облака запаха почти не разносит
        if (!TryGetComponent<HeatSignature>(out _)) gameObject.AddComponent<HeatSignature>(); // подпись гаснет сама: змея холоднокровна
        if (!TryGetComponent<StunTint>(out _)) gameObject.AddComponent<StunTint>(); // статус-сигнал «выключен» (стан/схвачен)
    }

    void Start()
    {
        playerCtl = FindAnyObjectByType<PlayerController>();

        var r = transform.Find("Rattle");
        if (r != null) rattleRenderer = r.GetComponentInChildren<Renderer>();

        groundY = transform.position.y; // уровень земли на спавне — от него меряем высоту насеста
        if (ownHealth != null) ownHealth.onDamaged.AddListener(OnHurt); // ударили → обидчик становится добычей (реактивный агр)
        TryGetComponent(out personality); // личность вешает ТЕЛО (CreatureBody) в своём Awake — читаем здесь (после всех Awake)
    }

    void OnDisable()
    {
        if (constricting) ReleaseHeld(); // убили на обхвате — отпустить жертву
    }

    // холодный расчёт (НЕ паника): ударивший становится ДОБЫЧЕЙ, если он тёплый. Так удар по змее её натравливает —
    // в т.ч. когда призрак только что раскрылся атакой (боец стал тёплым к моменту onDamaged: BreakGhost в Hit.Apply идёт до урона).
    void OnHurt()
    {
        // разбуженное перевариванием тело займётся само (Digestion слушает onDamaged) — тут решение психики
        var attacker = ownHealth != null ? ownHealth.LastAttacker : null;
        if (constricting || attacker == null || !Perception.IsWarm(attacker.transform)) return; // держим кого-то / нет источника / холодный-призрак

        // ХОЛОДНЫЙ РАСЧЁТ, не месть: обидчик-ОДИНОЧКА становится добычей; обидчик ПРИ СТАЕ — драка
        // безнадёжна, отступаем на стену в сокрытие (побили → спряталась, ждёт по осторожности)
        if (IsLonely(attacker))
        {
            target = attacker.transform; targetHealth = attacker;
            nextScan = Time.time + retargetInterval; // не дать скану тут же переклинить цель
            return;
        }
        if (climb != ClimbPhase.None) return; // уже лезем/сидим — план не меняем
        if (windingUp) { windingUp = false; telegraph.Clear(); }
        target = null; targetHealth = null;
        Vector3 away = transform.position - attacker.transform.position; away.y = 0f;
        fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward; // направление траверса — прочь от обидчика
        if (FindRefugeWall(out wallPoint, out wallNormal)) { climb = ClimbPhase.Approach; BeginWary(); }
    }

    float Speed => moveSpeed * (variance != null ? variance.SpeedMult : 1f);

    // тело-на-шасси Змея кормит деривированное (урон укуса, скорость); яд/обхват — фирменные, на компонентах/психике
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite != null) { bite.SetDamage(damage); bite.SetVenom(venom); bite.SetBleed(bleed); } // яд/кровь — теперь из органа (data-driven)
    }

    // камуфляж: раскрыть себя на время боя (лениво берём компонент — CreatureBody вешает его после нашего Awake)
    void RevealSelf()
    {
        if (camo == null) TryGetComponent(out camo);
        if (camo != null) camo.Reveal(revealMemory);
    }

    // прокрадывание-поиск: держим камуфляж даже на ходу (память > кадра — снимается сразу, как перестали красться;
    // раскрытие атакой/уроном приоритетнее). БЕГСТВО к стене стелс НЕ зовёт — там змея видима, стая её гонит
    void HoldStealth()
    {
        if (camo == null) TryGetComponent(out camo);
        if (camo != null) camo.HoldStealth(0.25f);
    }

    // ГРЕМОК: пассивный ритм засады (редкий, ТИХИЙ) — зацепка на невидимку И самозарядная ловушка:
    // любопытный волк придёт проверить звук, станет одиночкой у змеи — станет добычей
    void TryRattle() => DoRattle(rattleInterval, rattleHearRadius);

    // ПРИМАНКА (3d): жертва-одиночка видна в термо, но вне броска — гремим ЧАСТО и ГРОМКО, маним подойти
    void TryLure() => DoRattle(lureInterval, lureHearRadius);

    // ОСТОРОЖНОСТЬ: бегство на стену = СОКРЫТИЕ, не новая засада (манок в сокрытии молчит — иначе сами
    // же пере-агрим только что отогнанную стаю). Решение «ждать на месте / отползти по стене прочь» —
    // ВЕРОЯТНОСТНОЕ, раз на бегство; и шанс, и длительность выжидания взвешены личной Caution
    // (осторожная змея ждёт дольше и охотнее отползает) — ось змеи, как у волков мораль/храбрость
    void BeginWary()
    {
        wary = true;
        waryUntil = Time.time + waryWait * Caution;
        traversing = Random.value < Mathf.Clamp01(traverseChance * Caution);
        traverseUntil = 0f; // отсчёт траверса запустим, когда ДОБРАЛИСЬ до насеста (Perch)
        if (traversing)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal);
            traverseSign = Vector3.Dot(tangent, fleeDir) >= 0f ? 1 : -1; // вдоль стены ПРОЧЬ от центроида стаи
        }
    }

    // траверс сокрытия: скользим вдоль стены на высоте насеста — якорь ClimbMove ведём сами, зондируя
    // стену чуть впереди; стена кончилась (угол/проём) или время вышло — останавливаемся и ждём тут
    void TraverseWall()
    {
        if (traverseUntil <= 0f) traverseUntil = Time.time + traverseTime;
        if (Time.time >= traverseUntil) { traversing = false; return; }

        Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal).normalized * traverseSign;
        Vector3 probe = transform.position + tangent * 1.2f + wallNormal * 1.5f;
        if (Physics.Raycast(probe, -wallNormal, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)
            && Mathf.Abs(hit.normal.y) < 0.3f
            && hit.collider.GetComponentInParent<Health>() == null)
        { wallPoint = hit.point; wallNormal = hit.normal; }
        else traversing = false;
    }

    // ищем ближайшую ВЕРТИКАЛЬНУЮ стену без Health (стена лабиринта) кольцом лучей — убежище от стаи
    bool FindRefugeWall(out Vector3 point, out Vector3 normal)
    {
        point = Vector3.zero; normal = Vector3.zero;
        float best = wallSeekRadius; bool found = false;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        for (int i = 0; i < 12; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
            foreach (var hit in Physics.RaycastAll(origin, dir, wallSeekRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(hit.normal.y) > 0.3f) continue;                        // не вертикаль — пол/скос
                if (hit.collider.GetComponentInParent<Health>() != null) continue;  // живое (или своё тело) — не стена
                if (hit.distance < best) { best = hit.distance; point = hit.point; normal = hit.normal; found = true; }
            }
        }
        return found;
    }

    // стейт-машина убежища: доползти к стене → подняться на насест → сидеть-манить → спуск, когда стая ушла.
    // При ПЕРЕВАРИВАНИИ те же фазы, но осторожные: ползём/сидим невидимкой, НЕ маним, вниз — только переварив
    void UpdateClimb()
    {
        bool sated = Digesting;                // сытость — у тела (Digestion); поведение сытой — наше
        bool safe = !sated && !CheckFlee();    // сытая не спускается (голод снимет сытость сам); охотница — когда стая разошлась

        switch (climb)
        {
            case ClimbPhase.Approach:
            {
                if (sated || wary) HoldStealth(); // сытая/скрывающаяся ползёт к стене в камуфляже — не собирает хвост из стаи
                Vector3 to = wallPoint - transform.position; to.y = 0f;
                if (to.magnitude <= 1.6f) { climb = ClimbPhase.Rise; break; }
                if (safe) { climb = ClimbPhase.None; wary = false; traversing = false; break; } // угроза ушла по пути — незачем лезть
                Vector3 dir = nav.DirTo(wallPoint);
                if (dir.sqrMagnitude > 0.001f) Face(dir);
                Settle(dir * Speed);
                break;
            }
            case ClimbPhase.Rise:
                FaceWall();
                ClimbMove(groundY + perchHeight);
                if (transform.position.y >= groundY + perchHeight - 0.05f) climb = ClimbPhase.Perch;
                break;
            case ClimbPhase.Perch:
                FaceWall();
                ClimbMove(groundY + perchHeight);   // держим позицию у стены
                if (sated) HoldStealth();            // сытая: полная невидимость, тихо перевариваем
                else if (wary)                       // СОКРЫТИЕ после бегства: невидимость, манок МОЛЧИТ (не пере-агрим стаю)
                {
                    HoldStealth();
                    if (traversing) TraverseWall();  // решила отползти — траверсом вдоль стены прочь от стаи
                    if (CrowdNear()) waryUntil = Time.time + waryWait * Caution; // зрители под стеной — отсчёт заново
                    else if (Time.time >= waryUntil) { wary = false; traversing = false; climb = ClimbPhase.Descend; }
                }
                else if (safe) climb = ClimbPhase.Descend; // редкий не-wary насест (страховка) — старое правило
                break;
            case ClimbPhase.Descend:
                FaceWall();
                ClimbMove(groundY);
                if (transform.position.y <= groundY + 0.15f) climb = ClimbPhase.None;
                break;
        }
    }

    // морда ВВЕРХ вдоль стены, «спиной» от стены (up = нормаль) — не перпендикулярно, как на полу
    void FaceWall()
    {
        Quaternion wallRot = Quaternion.LookRotation(Vector3.up, wallNormal);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wallRot, rotationSpeed * Time.deltaTime);
    }

    // держим XZ у плоскости стены (в CC не толкаем — не проваливается) и рампой ведём Y к цели
    void ClimbMove(float targetY)
    {
        Vector3 anchor = wallPoint + wallNormal * wallHugOffset; // ближе к стене — голова прижимается (CC при климбе невидим, клип не важен)
        Vector3 p = transform.position;
        p.x = Mathf.MoveTowards(p.x, anchor.x, wallHugSpeed * Time.deltaTime);
        p.z = Mathf.MoveTowards(p.z, anchor.z, wallHugSpeed * Time.deltaTime);
        p.y = Mathf.MoveTowards(p.y, targetY, climbSpeed * Time.deltaTime);
        transform.position = p;
    }

    // полная стая рядом → РАЦИОНАЛЬНОЕ отступление к стене (проверка раз в 0.3с). ХОЛОДНЫЙ РАСЧЁТ, НЕ Страх:
    // змея не боится (холоднокровна → иммунна к эффекту Fear, S1 срез 5) — она сама РЕШАЕТ выйти из безнадёги
    bool CheckFlee()
    {
        if (Time.time >= nextFleeCheck)
        {
            nextFleeCheck = Time.time + 0.3f;
            Vector3 centroid = Vector3.zero;
            int warm = 0;
            foreach (var col in Physics.OverlapSphere(transform.position, fleeCheckRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.transform == transform || hp == heldHealth) continue;
                if (!Perception.IsWarm(hp.transform)) continue;
                centroid += hp.transform.position; warm++;
            }
            fleeing = warm >= fleeCrowdSize;
            if (fleeing)
            {
                Vector3 away = transform.position - centroid / warm; away.y = 0f;
                fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            }
        }
        return fleeing;
    }

    // «толпа рядом»: ≥N других тёплых вокруг змеи (схваченная жертва не в счёт) — шуметь опасно и незачем
    bool CrowdNear()
    {
        int warm = 0;
        foreach (var col in Physics.OverlapSphere(transform.position, quietCrowdRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || hp == heldHealth) continue;
            if (!Perception.IsWarm(hp.transform)) continue;
            if (++warm >= quietCrowdSize) return true;
        }
        return false;
    }

    void DoRattle(float interval, float hearRadius)
    {
        if (Time.time < nextRattle) return;
        if (CrowdNear()) { nextRattle = Time.time + interval; return; } // стая пришла — гремок молчит (любопытство угаснет, разбредутся)
        nextRattle = Time.time + interval;
        rattleBlinkUntil = Time.time + rattleCue;

        // звук будит любопытство зверья вокруг; пока змея гремит, память любопытства ОСВЕЖАЕТСЯ —
        // волк доходит и издалека (игрока манит сам сигнал — видит мигание; аудио позже).
        // СИЛА ЗОВА убывает с расстоянием (1 в упор → 0 на краю слышимости): волк тянется к большей силе
        // и скатывается в ВОРОНКУ ближней змеи — конкурирующие манки не пинг-понгят стаю между собой
        foreach (var col in Physics.OverlapSphere(transform.position, hearRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var w = col.GetComponentInParent<WolfPsyche>();
            if (w == null) continue;
            float d = (w.transform.position - transform.position).magnitude;
            w.HearRattle(transform.position, 1f - Mathf.Clamp01(d / hearRadius));
        }
    }

    // видимость погремушки: вместе с телом (камуфляж её не трогает) ЛИБО мигание гремка
    void LateUpdate()
    {
        if (rattleRenderer == null) return;
        if (camo == null) TryGetComponent(out camo);
        rattleRenderer.enabled = camo == null || !camo.Hidden || Time.time < rattleBlinkUntil;
    }

    void Update()
    {
        UpdateAlert(); // S1: кормим машину восприятия каждый кадр, до любых ранних return (зеркало)

        // ОБХВАТ имеет приоритет: змея закоммичена в дуэль (стоит и душит), знает свои срывы сама
        if (constricting) { RevealSelf(); UpdateConstrict(); return; }

        // пинок рвёт активный приём / замах обхвата (вне обхвата)
        if (knockback != null && knockback.IsActive)
        {
            if (activeAbility != null) { activeAbility.Abort(true); activeAbility = null; }
            if (windingUp) { windingUp = false; telegraph.Clear(); }
            Settle(Vector3.zero);
            return;
        }

        // активный приём (укус/рывок) тикает сам
        if (activeAbility != null)
        {
            RevealSelf(); // приём в процессе — змея видна (и ещё revealMemory после)
            if (stagger != null && stagger.IsStaggered) activeAbility.Abort(false); // полёт рывка сам решит
            var st = activeAbility.Tick();
            if (st == AbilityRun.Running) return;
            activeAbility = null;
            nextAttackTime = Time.time + (st == AbilityRun.Done ? attackCooldown : 0.3f);
            return;
        }

        // НАС СХВАТИЛИ (хвост игрока): единый захват — на слабом хвате разворачиваемся и КУСАЕМ схватившего
        // (обидчик = добыча, холодный расчёт); защёлк/импульс-стагер глушит гейт ниже. Уйти из хвата нельзя.
        if (grabbedStatus == null) TryGetComponent(out grabbedStatus);
        if (grabbedStatus != null && grabbedStatus.IsHeld)
        {
            RevealSelf(); // борьба в хвате — не засада
            if (windingUp) { windingUp = false; telegraph.Clear(); }
            var gb = grabbedStatus.Grabber;
            if (gb != null && (stagger == null || !stagger.IsStaggered))
            {
                if (!ReferenceEquals(targetHealth, gb)) { target = gb.transform; targetHealth = gb; bite.SetTarget(gb); leap.SetTarget(gb); }
                Vector3 gto = gb.transform.position - transform.position; gto.y = 0f;
                if (gto.sqrMagnitude > 0.001f) Face(gto.normalized);
                if (Time.time >= nextAttackTime && gto.magnitude <= bite.Range && bite.TryUse()) activeAbility = bite;
            }
            Settle(Vector3.zero);
            return;
        }

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        // СЫТАЯ ОСТОРОЖНОСТЬ: тело переваривает (Digestion: сытость/бонус-реген/пробуждение уроном — его дело) —
        // наше дело ПОВЕДЕНИЕ: бросить охоту, спрятаться на стене (ветка climb ниже) или замереть невидимкой
        if (Digesting)
        {
            target = null; targetHealth = null; // сытой добыча не интересна
            if (climb == ClimbPhase.Descend) climb = ClimbPhase.Rise; // концовка обхвата отправила вниз — сытой лучше наверх
            if (climb == ClimbPhase.None)
            {
                if (Time.time >= nextWallSeek) // стены ещё нет — периодически ищем убежище (и после убийства, и на ретрае)
                {
                    nextWallSeek = Time.time + 1.5f;
                    if (FindRefugeWall(out wallPoint, out wallNormal)) climb = ClimbPhase.Approach;
                }
                if (climb == ClimbPhase.None) { HoldStealth(); Settle(Vector3.zero); return; } // так и нет — перевариваем невидимкой на месте
            }
        }

        // УБЕЖИЩЕ НА СТЕНЕ: если уже лезем/сидим — приоритетное состояние (спуск при исчезновении угрозы)
        if (climb != ClimbPhase.None) { UpdateClimb(); return; }

        // ХИЩНИК СТАЛ ЖЕРТВОЙ: полная стая рядом — бросаем всё, ищем СТЕНУ и лезем на недосягаемый насест;
        // стены рядом нет — бежим по земле от центроида (настигаема), пока не найдём стену
        if (CheckFlee())
        {
            if (windingUp) { windingUp = false; telegraph.Clear(); }
            target = null; targetHealth = null;
            if (FindRefugeWall(out wallPoint, out wallNormal)) { climb = ClimbPhase.Approach; BeginWary(); UpdateClimb(); return; }
            Vector3 mv = nav.DirTo(transform.position + fleeDir * 8f);
            if (mv.sqrMagnitude > 0.001f) Face(mv);
            Settle(mv * Speed * fleeSpeedMult);
            return;
        }

        if (windingUp) { RevealSelf(); UpdateGrabWindup(); return; }

        // ХОЛОДНЫЙ РАСЧЁТ: пересматриваем жертву (тёплая одиночка в термо-радиусе; стая рядом — никого не трогаем)
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + retargetInterval;
            ChooseTarget();
        }

        // жертвы нет ЛИБО пропала из термо (умерла/призрак/ушла) → не застываем засадой навечно, а ПРОКРАДЫВАЕМСЯ
        // искать тёплую жизнь (иначе стационарные точки-приманки сгоняют волков в осциллирующие кучи между змеями)
        if (target == null || !Perception.SeesThermal(transform.position, target, senses.Range(SenseKind.Thermal)))
        {
            Prowl();
            return;
        }

        // ОДИНОЧЕСТВО — НЕПРЕРЫВНЫЙ предикат, не снимок скана: воронка манка стягивает волков КУЧНО,
        // передний на миг «одинок», но пока мы замахиваемся — хвост воронки догоняет. Компания у жертвы
        // появилась → бросаем ДО прыжка, не после (холодный расчёт передумывает мгновенно)
        if (!IsLonely(targetHealth)) { target = null; targetHealth = null; Prowl(); return; }

        if (heldIsPlayerTarget()) targetHealth.MarkInCombat(); // охота на ИГРОКА → он в бою (чужая охота его реген не трогает)

        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Face(dist > 0.001f ? to / dist : transform.forward);

        if (Time.time >= nextAttackTime)
        {
            if (dist <= bite.Range)
            {
                // в упор: обвить (обхват) либо простой ядовитый укус
                if (Random.value < grabChance) { BeginGrabWindup(); Settle(Vector3.zero); return; }
                if (bite.TryUse()) activeAbility = bite;
                Settle(Vector3.zero); return;
            }
            if (dist >= leap.MinRange && dist <= leap.MaxRange) { if (leap.TryUse()) activeAbility = leap; Settle(Vector3.zero); return; }
        }

        // вне броска: ЗАМРИ И МАНИ — подкрадывание выдало бы засаду, пусть любопытная жертва подойдёт сама;
        // в зоне броска на откате — доползаем в упор
        if (dist > leap.MaxRange) { TryLure(); Settle(Vector3.zero); return; }
        Settle(dist > bite.Range ? nav.DirTo(target.position) * Speed * creepSpeed : Vector3.zero);
    }

    bool heldIsPlayerTarget() => targetHealth != null && playerCtl != null && targetHealth.transform == playerCtl.transform;

    // S1: маппинг восприятия змеи на общую машину (зеркало). Атака = держит/бьёт/есть тёплая цель в термо;
    // Настороженность = реагирует (бежит/на стене) ЛИБО прокрадывается к почуянной тёплой жизни; иначе Спокойствие (засада)
    void UpdateAlert()
    {
        bool attacking = constricting || activeAbility != null || windingUp
                         || (target != null && Perception.SeesThermal(transform.position, target, senses.Range(SenseKind.Thermal)));
        bool cue = !attacking && (fleeing || climb != ClimbPhase.None || NearestWarm(roamSenseRadius, out _) != null);
        alert.Observe(attacking, cue);
    }

    // выбор жертвы: ближайшая ТЁПЛАЯ ОДИНОЧКА (термо гейтит и холодных, и призрака); Massive не по зубам
    void ChooseTarget()
    {
        Health best = null;
        float bestD = float.MaxValue;
        float tr = senses.Range(SenseKind.Thermal); // S1: термо-дальность через профиль (пер-состоянчато; множитель=1 пока)
        foreach (var col in Physics.OverlapSphere(transform.position, tr, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || hp == best) continue;
            if (!Perception.SeesThermal(transform.position, hp.transform, tr)) continue;
            if (hp.GetComponent<Massive>() != null) continue; // на массивную тушу холодный расчёт не пойдёт
            if (!IsLonely(hp)) continue;
            float d = (hp.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = hp; }
        }
        target = best != null ? best.transform : null;
        targetHealth = best;
        bite.SetTarget(best); // доставки бьют по текущей жертве (activeAbility сейчас нет — скан идёт после его ветки)
        leap.SetTarget(best);
    }

    // «одиночка» = рядом с жертвой нет ДРУГИХ тёплых (стая/компания отпугивает; сама змея холодная — не мешает).
    // Радиус изоляции взвешен ОСТОРОЖНОСТЬЮ особи: дерзкая берёт жертву в ~6м от стаи, трусиха ждёт ~13
    bool IsLonely(Health candidate)
    {
        foreach (var col in Physics.OverlapSphere(candidate.transform.position, lonelyRadius * Caution, ~0, QueryTriggerInteraction.Ignore))
        {
            var other = col.GetComponentInParent<Health>();
            if (other == null || other == candidate || other.transform == transform) continue;
            if (Perception.IsWarm(other.transform)) return false;
        }
        return true;
    }

    // ПРАЗДНЫЙ ПОИСК: добычи в термо нет — не застываем (стационарные засады сгоняют волков в осциллирующие кучи).
    // Крадёмся к ближайшей тёплой жизни: одиночка — манить и сближаться; толпа — уйти в сторону искать отбившихся;
    // пусто — тихо бродить (пассивный гремок-ловушка). Всё на засадном темпе (creepSpeed) — не превращаемся в гончую
    void Prowl()
    {
        HoldStealth(); // прокрадываемся-ищем в камуфляже: охотник-невидимка не собирает вокруг себя роящуюся стаю
        Health warm = NearestWarm(roamSenseRadius, out bool lonely);
        if (warm == null) { TryRattle(); CreepTo(nav.Wander(wanderRadius)); return; } // никого — бродим, изредка гремим
        if (lonely) { TryLure(); CreepTo(warm.transform.position); return; }          // одинокая жизнь за термо — манить и красться навстречу
        // рядом ТОЛПА без одиночек: не кормим осцилляцию — тихо уходим прочь от центра массы искать отбившихся
        Vector3 away = transform.position - warm.transform.position; away.y = 0f;
        Vector3 spot = transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * (roamSenseRadius * 0.5f);
        CreepTo(spot);
    }

    // ближайшая тёплая жизнь в радиусе (термочутьё сквозь стены; Massive и своя ноша не в счёт); заодно — одиночка ли она
    Health NearestWarm(float radius, out bool lonely)
    {
        Health best = null; float bestD = radius * radius;
        foreach (var col in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || hp == heldHealth || hp == best) continue;
            if (hp.GetComponent<Massive>() != null) continue;
            if (!Perception.IsWarm(hp.transform)) continue;
            float d = (hp.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = hp; }
        }
        lonely = best != null && IsLonely(best);
        return best;
    }

    // засадный шаг к точке: доворот + ход на creepSpeed (крадёмся, не мчим)
    void CreepTo(Vector3 dest)
    {
        Vector3 mv = nav.DirTo(dest);
        if (mv.sqrMagnitude > 0.001f) Face(mv);
        Settle(mv * Speed * creepSpeed);
    }

    // замах обхвата: жертва увернулась из радиуса — сорван; выдержала — обвивает.
    // Компания подтянулась за время замаха — тоже сорван (одиночество непрерывно, обвивать при стае безумие)
    void UpdateGrabWindup()
    {
        if (target == null) { windingUp = false; telegraph.Clear(); Settle(Vector3.zero); return; }
        if (!IsLonely(targetHealth)) { windingUp = false; telegraph.Clear(); target = null; targetHealth = null; Settle(Vector3.zero); return; }
        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Face(d > 0.001f ? to / d : transform.forward);

        if (d > bite.Range * 1.2f) { windingUp = false; telegraph.Clear(); nextAttackTime = Time.time + 0.3f; }
        else if (Time.time >= windupEnd) { windingUp = false; StartConstrict(); }
        Settle(Vector3.zero);
    }

    void BeginGrabWindup()
    {
        windingUp = true;
        windupEnd = Time.time + grabWindup;
        telegraph.Set(true, TelegraphColors.Grab);
    }

    void StartConstrict()
    {
        if (targetHealth == null) return;
        constricting = true;
        heldHealth = targetHealth;
        heldIsPlayer = playerCtl != null && heldHealth.transform == playerCtl.transform;
        heldStageCap = heldHealth.GetComponent<Massive>() != null ? 2 : 3; // массивная туша — на стадию слабее (единое правило)

        grip = 0f; gripFloor = 0f; stage = 1; maxStage = 1;
        lastHp = ownHealth != null ? ownHealth.Current : 0;
        chokeNext = 0f;
        telegraph.Set(true, TelegraphColors.Grab);
        if (heldIsPlayer) playerCtl.ApplyGrab(this, grabSlow1); // игрок: режем ход и рывок (усилится к ст.3)
        else
        {
            heldGrabbed = Grabbed.Apply(heldHealth.gameObject, ownHealth, 1, false); // слабый хват: импульс-стагер, жертва дальше ДЕРЁТСЯ
            escapeAt = Time.time + Random.Range(escapeMin, escapeMax);               // гонка: дожми до защёлка, пока не вырвалась
        }
    }

    void UpdateConstrict()
    {
        if (heldHealth == null) { EndConstrict(attackCooldown); return; } // жертва умерла — хвост свободен (переваривание запустила вахта OnPreyDeath)
        if (ownHealth == null) { EndConstrict(0.3f); return; }

        // чёрный ход/призрак: иммунитет игрока распускает хват НА ИГРОКЕ (жертву-NPC призрак не спасает)
        if (heldIsPlayer && playerCtl != null && playerCtl.GrabImmune) { EndConstrict(attackCooldown); return; }

        // СТАН (вой волчьей Пасти) рвёт обхват — но не мёртвую хватку 3-й стадии
        if (stagger != null && stagger.IsStunned && stage < 3) { EndConstrict(attackCooldown); return; }

        if (heldIsPlayer) UpdatePlayerConstrict();
        else UpdateNpcConstrict();
    }

    // обхват ИГРОКА: стадийная машина с ратчетом (контр-игра расписана в спеке змеи)
    void UpdatePlayerConstrict()
    {
        // пинок: в 1-й стадии срывает (окно), в 2+ сжатие держит — гасим отлёт
        if (knockback != null && knockback.IsActive)
        {
            if (stage <= 1) { EndConstrict(attackCooldown); return; }
            knockback.Cancel();
        }

        heldHealth.MarkInCombat();

        // сжатие тикает вверх (время); урон по змее откатывает вниз, но НЕ ниже пола достигнутой стадии (ратчет)
        grip += tightenRate * Time.deltaTime;
        int dmg = lastHp - ownHealth.Current;
        if (dmg > 0) grip -= dmg * loosenPerDamage;
        grip = Mathf.Max(gripFloor, grip);
        lastHp = ownHealth.Current;

        int newStage = Mathf.Min(grip >= stage3At ? 3 : grip >= stage2At ? 2 : 1, heldStageCap);
        if (newStage != stage) SetStage(newStage);

        // стадия 3 — удушение: DoT-гонка (минует i-frames — рывком из удушения не спрятаться)
        if (stage >= 3 && Time.time >= chokeNext)
        {
            heldHealth.LastAttacker = ownHealth; // смерть от удушения — убийство змеи
            heldHealth.TakeDamage(Mathf.RoundToInt(chokeDamage), true);
            chokeNext = Time.time + chokeInterval;
        }

        HoldNearVictim();
    }

    // обхват NPC-ЖЕРТВЫ — ТА ЖЕ стадийная машина, что у игрока (единый захват): ст.1 слабый хват (жертва
    // ДЕРЁТСЯ — её укусы ослабляют grip; вырывается по таймеру — гонка «дожать до защёлка»), ст.2 защёлк
    // (стан через Grabbed, StunTint), ст.3 удушение-DoT. Удар СПАСАТЕЛЯ извне рвёт хват в любой фазе (стая
    // отбивает своего). Стратегия 3e-ii: защёлкнутую (ст.2+) тушу при толпе УТАЩИТЬ на стену-насест
    // и додушить там, где наземные волки бессильны (фазы переиспользуют ClimbPhase).
    void UpdateNpcConstrict()
    {
        // урон по змее: от САМОЙ жертвы — ослабляет хват (гонка), ИЗВНЕ — рвёт (спасатели)
        int dmg = lastHp - ownHealth.Current;
        if (dmg > 0 && !ReferenceEquals(ownHealth.LastAttacker, heldHealth)) { EndConstrict(attackCooldown); return; }
        lastHp = ownHealth.Current;

        // сжатие тикает вверх; укусы жертвы откатывают вниз (слабее игрока — соло-добыча гонку не выигрывает),
        // но не ниже пола достигнутой стадии (ратчет)
        grip += tightenRate * Time.deltaTime;
        if (dmg > 0) grip -= dmg * npcLoosenPerDamage;
        grip = Mathf.Max(gripFloor, grip);
        int newStage = Mathf.Min(grip >= stage3At ? 3 : grip >= stage2At ? 2 : 1, heldStageCap);
        if (newStage != stage) SetStage(newStage);

        // слабый хват: жертва вырвалась по таймеру — не дожал (её укусы двигали гонку в её пользу)
        if (stage < 2 && Time.time >= escapeAt) { EndConstrict(attackCooldown); return; }

        if (stage >= 3) ChokeTick(); // удушение-DoT — только с полного защёлка (ст.3), как у игрока

        // УЖЕ НЕСЁМ (тащим по земле / лезем): CrowdNear отключён — на стене волки не достают; рвёт только удар извне
        if (climb == ClimbPhase.Approach) { CarryApproach(); return; }
        if (climb == ClimbPhase.Rise || climb == ClimbPhase.Perch) { CarryRise(); return; }

        // ЕЩЁ НА ЗЕМЛЕ. Стая-спасатели пришла отбивать (CrowdNear ≥2)?
        if (CrowdNear())
        {
            // защёлкнутую тушу тащим наверх (спасти мясо от стаи); не защёлкнул — дерущаяся жертва + толпа = бросаем
            if (stage >= 2 && FindRefugeWall(out wallPoint, out wallNormal)) { climb = ClimbPhase.Approach; BeginCarry(); return; }
            EndConstrict(attackCooldown); return;
        }

        // жертву оттолкнуло далеко — соскользнула
        Vector3 to = heldHealth.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > bite.Range * 1.6f) { EndConstrict(0.5f); return; }

        HoldNearVictim();
    }

    // тик удушения (ст.3): DoT до смерти — смерть жертвы отпустит хват сама (heldHealth → null)
    void ChokeTick()
    {
        if (Time.time < chokeNext) return;
        heldHealth.LastAttacker = ownHealth; // жертва «достаётся змее» (родство — убийце, задел эволюции)
        heldHealth.TakeDamage(npcChokeDamage, true);
        chokeNext = Time.time + npcChokeInterval;
    }

    // взяли ношу: жертва становится куклой (глушит свою локомоцию/гравитацию — позицией владеем мы)
    void BeginCarry()
    {
        if (heldHealth != null && heldHealth.TryGetComponent<ICarried>(out var carried)) carried.SetCarried(true);
    }

    // тащим тушу по земле к основанию стены; спасатели-стая достают (гейт CrowdNear отработал выше)
    void CarryApproach()
    {
        Vector3 to = wallPoint - transform.position; to.y = 0f;
        if (to.magnitude <= 1.6f) { climb = ClimbPhase.Rise; return; } // у стены — лезем
        Vector3 dir = nav.DirTo(wallPoint);
        if (dir.sqrMagnitude > 0.001f) Face(dir);
        CarryVictim();
        Settle(dir * Speed * carrySpeedMult);
    }

    // несём вверх на насест и держим там, додушивая; наземная стая внизу бессильна (воет/паникует)
    void CarryRise()
    {
        FaceWall();
        float perchY = groundY + perchHeight;
        ClimbMove(perchY);
        CarryVictim();
        if (climb == ClimbPhase.Rise && transform.position.y >= perchY - 0.05f) climb = ClimbPhase.Perch;
    }

    // позиционируем ношу: на земле — позади (в кольцах, морда змеи к стене свободна); на стене — висит ниже головы
    void CarryVictim()
    {
        if (heldHealth == null) return;
        Vector3 pos = climb == ClimbPhase.Approach
            ? transform.position - transform.forward * (bite.Range * 0.5f)   // ПОЗАДИ — своя CC-морда к стене свободна (не упирается в ношу)
            : transform.position - Vector3.up * carryDrop + wallNormal * carryStandoff; // на стене — висит ниже головы, отжата от плоскости
        heldHealth.transform.position = pos;
    }

    // стоим у жертвы, морда к ней, мягко держим дистанцию удержания (не таскаем её за собой)
    void HoldNearVictim()
    {
        Vector3 to = heldHealth.transform.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        Face(dir);
        float hold = bite.Range * 0.6f;
        float err = d - hold;
        // анти-тремор: дедзона у дистанции удержания + мягкий гейн (был ×8) — не долбим CC жертвы туда-сюда,
        // особенно игрока (взаимный CC-push давал дрожь). В пределах ±0.3м просто стоим
        Settle(Mathf.Abs(err) > 0.3f ? Vector3.ClampMagnitude(dir * err * 3f, moveSpeed) : Vector3.zero);
    }

    void SetStage(int s)
    {
        if (s > maxStage)
        {
            InjectVenom();                                    // туже сжатие → впрыск яда (раз на каждую новую стадию)
            maxStage = s;
            gripFloor = s >= 3 ? stage3At : stage2At;         // ратчет: защёлкнулись — назад за порог стадии не пускаем
        }
        stage = s;
        telegraph.SetGradient(TelegraphColors.Grab, s / 3f); // стадии обхвата — ГРАДИЕНТ родной→фиолетовый по сжатию
        if (heldIsPlayer && playerCtl != null) playerCtl.ApplyGrab(this, SlowFor(s));
        else if (heldHealth != null) heldGrabbed = Grabbed.Apply(heldHealth.gameObject, ownHealth, s, s >= 2); // укрепление: импульс-стагер; ст.2+ = защёлк-стан
    }

    // яд со стадий — С ИСТОЧНИКОМ: смерть от него атрибутируется змее (родство убийце + вахта переваривания)
    void InjectVenom()
    {
        if (heldHealth != null) new Hit(ownHealth, transform.position).Apply(heldHealth, HitEffect.Venom());
    }

    void ReleaseHeld()
    {
        if (heldIsPlayer && playerCtl != null) playerCtl.ReleaseGrab(this);
        else if (heldHealth != null && heldHealth.TryGetComponent<ICarried>(out var carried)) carried.SetCarried(false); // ноша отпущена — оживает и падает
        if (heldGrabbed != null) heldGrabbed.Release(); // снять единый статус (мёртвая жертва — Unity-null пропустит)
        heldHealth = null; heldGrabbed = null; heldIsPlayer = false;
    }

    void EndConstrict(float cooldown)
    {
        constricting = false; windingUp = false;
        telegraph.Clear();
        ReleaseHeld();
        stage = 0; grip = 0f; gripFloor = 0f;
        // конец переноски: были на стене (Rise/Perch) → сползаем вниз (не падаем); тащили по земле (Approach) → просто отпускаем.
        // У обхвата ИГРОКА climb всегда None — его концовки это не касается
        if (climb == ClimbPhase.Rise || climb == ClimbPhase.Perch) climb = ClimbPhase.Descend;
        else if (climb == ClimbPhase.Approach) climb = ClimbPhase.None;
        nextAttackTime = Time.time + cooldown;
    }

    // IGrabber: игрок рвётся рывком. Отпускаем ТОЛЬКО в 1-й стадии (урон + отлёт змеи); в 2+ сжатие держит.
    public bool BreakFree(int damage)
    {
        if (!constricting || !heldIsPlayer) return true; // игрока не держим — считаем свободным
        if (stage >= 2) return false;                    // сжатие: рывок бесполезен, не отпускаем
        if (ownHealth != null && damage > 0)
        {
            ownHealth.LastAttacker = heldHealth; // рывок-срыв ранит змею — это удар игрока
            ownHealth.TakeDamage(damage);
        }
        if (knockback != null && heldHealth != null)
        {
            Vector3 away = transform.position - heldHealth.transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * ripSelfKnock);
        }
        EndConstrict(attackCooldown);
        return true;
    }

    float SlowFor(int s) => s >= 3 ? grabSlow3 : s == 2 ? grabSlow2 : grabSlow1;

    void Face(Vector3 d) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), rotationSpeed * Time.deltaTime);

    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal; motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }
}
