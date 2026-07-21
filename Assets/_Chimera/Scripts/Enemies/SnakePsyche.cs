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
    [SerializeField] float rescueThreatRadius = 13f;        // спасатели НА ПОДХОДЕ: тёплый в этом радиусе во время удушения →
                                                            // защёлкнутую тушу заранее тащим на стену (гонка по вертикали)
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
    [SerializeField] int ripSelfKnock = 5;                   // отлёт змеи, когда игрок сорвался рывком (ст.1)
    [SerializeField] float grabBiteInterval = 1.2f;          // как часто грызёт того, кого держит (яд/урон — ИЗ ОРГАНА клыков)
    // САМА МАШИНА хвата (сжатие/стадии/ратчет/чок/яд + тюнинг обеих жертв) — общий компонент Constrict
    // (фича органа «Хвост», хвост-эталон 2026-07-19); здесь остались только решения ДРАЙВЕРА (когда/куда)

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

    // жертва ОБХВАТА живёт в машине (constrictM.Victim, фиксируется на входе — retarget её не трогает)
    Constrict constrictM;  // ЕДИНАЯ машина захвата (общая с хвостом игрока): стадии/слоу/Grabbed/гонка — её дело
    Grabbed grabbedStatus; // а это НАС схватили (хвост игрока) — на слабом хвате кусаемся в ответ

    float nextAttackTime, windupEnd, nextRattle, rattleBlinkUntil, nextScan, nextFleeCheck, groundY; // вертикаль — в NavLocomotion
    bool fleeing;
    Vector3 fleeDir;

    enum ClimbPhase { None, Approach, Rise, Perch, Descend }
    ClimbPhase climb;
    Personality personality; // ЛИЧНОСТЬ (вешает тело): ось ОСТОРОЖНОСТИ — выжидание в сокрытии/склонность отползать
    Noise noiseSrc;          // источник звука (вешает тело): гремок/приманка — всплески громкости (ось Noise)
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
    int shownStage;                               // последняя показанная стадия хвата (градиент телеграфа — наш, драйверный)
    float nextGrabBite;                           // ритм укусов в хвате (сам укус — органный, BiteAbility.BiteNow)
    Renderer rattleRenderer;                      // жёлтая погремушка: гремок мигает ИМЕННО ей

    void Awake()
    {
        stagger = GetComponent<Stagger>();
        knockback = GetComponent<Knockback>();
        ownHealth = GetComponent<Health>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
        if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
        if (!TryGetComponent(out bite)) bite = gameObject.AddComponent<BiteAbility>();
        if (!TryGetComponent(out leap)) leap = gameObject.AddComponent<LeapAbility>();
        if (!TryGetComponent(out constrictM)) constrictM = gameObject.AddComponent<Constrict>(); // единая машина хвата; дефолт капа = 3 (змея на РОДНОМ шасси — nativeChassis); чужеродным NPC-химерам кап скормит тело
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
        if (constricting) { constrictM.End(); constricting = false; } // убили на обхвате — отпустить жертву
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
    public void OnBodyStats(int damage, float bodyMoveSpeed, int venom, int bleed, float howlRange)
    {
        moveSpeed = bodyMoveSpeed; // голос (howlRange) змее не нужен: её Пасть не воет (0 из данных)
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
    // змея не боится (холоднокровна → мораль инертна, S1 срез 5) — она сама РЕШАЕТ выйти из безнадёги
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
                if (hp == null || hp.transform == transform || hp == constrictM.Victim) continue;
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

    // спасатели НА ПОДХОДЕ: хоть один ДРУГОЙ тёплый в радиусе угрозы. Жертва была изолированной
    // (гейт одиночества) — новый тёплый рядом почти наверняка бежит отбивать (SenseGrabbedMate стаи)
    bool RescuersIncoming()
    {
        foreach (var col in Physics.OverlapSphere(transform.position, rescueThreatRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || hp == constrictM.Victim) continue;
            if (Perception.IsWarm(hp.transform)) return true;
        }
        return false;
    }

    // «толпа рядом»: ≥N других тёплых вокруг змеи (схваченная жертва не в счёт) — шуметь опасно и незачем
    bool CrowdNear()
    {
        int warm = 0;
        foreach (var col in Physics.OverlapSphere(transform.position, quietCrowdRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || hp == constrictM.Victim) continue;
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

        // ЗВУК (ось Noise, B2): гремок — ВСПЛЕСК громкости, физика доносит сама (слышимая дальность =
        // громкость × ухо слушателя, воронка силы — в Hear). Приманка гремит на полную (1.0), пассивный
        // гремок тихий (~0.55 — прежние радиусы 15/28 переведены в громкость). Кто и как реагирует —
        // решают уши: волчье любопытство ловит гремок, лось ходит проверять любой шум
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(hearRadius / lureHearRadius, rattleCue + 0.2f);
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

        if (targetIsPlayer()) targetHealth.MarkInCombat(); // охота на ИГРОКА → он в бою (чужая охота его реген не трогает)

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
        Settle(nav.Arrive(target.position, Speed * creepSpeed, stopAt: bite.Range * 0.85f)); // плавный подползок (без дрожи на границе)
    }

    bool targetIsPlayer() => targetHealth != null && playerCtl != null && targetHealth.transform == playerCtl.transform; // (бывш. heldIsPlayerTarget — имя стухло после H3)

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
            if (hp == null || hp.transform == transform || hp == constrictM.Victim || hp == best) continue;
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
        // вся механика хвата (стадии/Massive-кап/слоу жертвы-игрока/Grabbed/гонка вырывания) — в машине
        constricting = constrictM.Begin(targetHealth, this);
        if (!constricting) return;
        shownStage = 1;
        nextGrabBite = Time.time + grabBiteInterval; // первый укус — не в тот же кадр, что захват
        telegraph.Set(true, TelegraphColors.Grab);
    }

    void UpdateConstrict()
    {
        if (ownHealth == null) { EndConstrict(0.3f); return; }
        bool victimIsPlayer = constrictM.VictimIsPlayer;

        // ДРАЙВЕРНЫЕ срывы (решения психики — машина про них не знает):
        // чёрный ход/призрак: иммунитет игрока распускает хват НА ИГРОКЕ (жертву-NPC призрак не спасает)
        if (victimIsPlayer && playerCtl != null && playerCtl.GrabImmune) { EndConstrict(attackCooldown); return; }
        // СТАН (вой волчьей Пасти) рвёт обхват — но не мёртвую хватку 3-й стадии
        if (stagger != null && stagger.IsStunned && constrictM.Stage < 3) { EndConstrict(attackCooldown); return; }
        // пинок жертвы-игрока: в 1-й стадии срывает (окно), в 2+ сжатие держит — гасим отлёт
        if (victimIsPlayer && knockback != null && knockback.IsActive)
        {
            if (constrictM.Stage <= 1) { EndConstrict(attackCooldown); return; }
            knockback.Cancel();
        }

        // тик МАШИНЫ (сжатие/ратчет/стадии/гонка/чок/яд — её дело; нам — исход).
        // умерла/вырвалась/спасли — хвост свободен (переваривание запустит вахта OnPreyDeath)
        if (constrictM.Tick() != GrabTick.Holding) { EndConstrict(attackCooldown); return; }
        if (constrictM.Stage != shownStage)
        {
            shownStage = constrictM.Stage;
            telegraph.SetGradient(TelegraphColors.Grab, constrictM.StageT); // стадии — градиент родной→фиолетовый по сжатию
        }

        // ГРЫЗЁТ В ХВАТЕ: констриктор держит зубами. Укус — ОРГАННЫЙ (BiteAbility: урон/яд/кровь из данных
        // клыков), поэтому яд копится стаками сам и к 3-му выходит на DoT — без хардкода в машине хвата
        if (Time.time >= nextGrabBite)
        {
            nextGrabBite = Time.time + grabBiteInterval;
            bite.BiteNow(constrictM.Victim);
        }

        if (victimIsPlayer) { HoldNearVictim(); return; }

        // жертва-NPC: «КУДА тащить» — драйверное (машина только держит; право тащить даёт защёлк).
        // УЖЕ НЕСЁМ (тащим по земле / лезем): CrowdNear отключён — на стене волки не достают; рвёт только удар извне
        if (climb == ClimbPhase.Approach) { CarryApproach(); return; }
        if (climb == ClimbPhase.Rise || climb == ClimbPhase.Perch) { CarryRise(); return; }

        // ЕЩЁ НА ЗЕМЛЕ. СПАСАТЕЛИ НА ПОДХОДЕ (издалека, не вплотную!) — защёлкнутую (ст.2+) тушу ЗАРАНЕЕ
        // тащим на стену: гонка по вертикали — успели укусить до высоты (внешний урон рвёт хват) → отбили,
        // не успели → стая воет внизу, а мы додушиваем на насесте. Поздний триггер (толпа в упор) гонку
        // всегда проигрывал — с ношей ползём медленно
        if (constrictM.Stage >= 2 && RescuersIncoming() && Time.time >= nextWallSeek)
        {
            nextWallSeek = Time.time + 0.5f; // сканы стены не каждый кадр (12 лучей)
            if (FindRefugeWall(out wallPoint, out wallNormal)) { climb = ClimbPhase.Approach; BeginCarry(); return; }
        }

        // толпа уже вплотную, а защёлка/стены нет — дерущаяся жертва + стая = безнадёга, бросаем
        if (CrowdNear()) { EndConstrict(attackCooldown); return; }

        // жертву оттолкнуло далеко — соскользнула
        Vector3 to = constrictM.Victim.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > bite.Range * 1.6f) { EndConstrict(0.5f); return; }

        HoldNearVictim();
    }

    // взяли ношу: жертва становится куклой (глушит свою локомоцию/гравитацию — позицией владеем мы)
    void BeginCarry()
    {
        var v = constrictM.Victim;
        if (v != null && v.TryGetComponent<ICarried>(out var carried)) carried.SetCarried(true);
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
        var v = constrictM.Victim;
        if (v == null) return;
        Vector3 pos = climb == ClimbPhase.Approach
            ? transform.position - transform.forward * (bite.Range * 0.5f)   // ПОЗАДИ — своя CC-морда к стене свободна (не упирается в ношу)
            : transform.position - Vector3.up * carryDrop + wallNormal * carryStandoff; // на стене — висит ниже головы, отжата от плоскости
        v.transform.position = pos;
    }

    // стоим у жертвы, морда к ней, мягко держим дистанцию удержания (не таскаем её за собой)
    void HoldNearVictim()
    {
        var v = constrictM.Victim;
        if (v == null) return;
        Vector3 to = v.transform.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        Face(dir);
        float err = d - bite.Range * 0.6f; // держим дистанцию удержания
        // анти-тремор: дедзона + мягкий гейн — не долбим CC жертвы туда-сюда (особенно игрока: взаимный
        // CC-push давал дрожь). Сглаживание скорости добавит общая локомоция
        Settle(Mathf.Abs(err) > 0.3f ? Vector3.ClampMagnitude(dir * err * 3f, moveSpeed) : Vector3.zero);
    }

    void EndConstrict(float cooldown)
    {
        constricting = false; windingUp = false;
        telegraph.Clear();
        constrictM.End(); // машина сама снимет статус/слоу/ношу и обнулит сжатие
        shownStage = 0;
        // конец переноски: были на стене (Rise/Perch) → сползаем вниз (не падаем); тащили по земле (Approach) → просто отпускаем.
        // У обхвата ИГРОКА climb всегда None — его концовки это не касается
        if (climb == ClimbPhase.Rise || climb == ClimbPhase.Perch) climb = ClimbPhase.Descend;
        else if (climb == ClimbPhase.Approach) climb = ClimbPhase.None;
        nextAttackTime = Time.time + cooldown;
    }

    // IGrabber: игрок рвётся рывком. Отпускаем ТОЛЬКО в 1-й стадии (урон + отлёт змеи); в 2+ сжатие держит.
    public bool BreakFree(int damage)
    {
        if (!constricting || !constrictM.VictimIsPlayer) return true; // игрока не держим — считаем свободным
        if (constrictM.Stage >= 2) return false;                     // сжатие: рывок бесполезен, не отпускаем
        var victim = constrictM.Victim;
        if (ownHealth != null && damage > 0)
        {
            ownHealth.LastAttacker = victim; // рывок-срыв ранит змею — это удар игрока
            ownHealth.TakeDamage(damage);
        }
        if (knockback != null && victim != null)
        {
            Vector3 away = transform.position - victim.transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) knockback.Push(away.normalized * ripSelfKnock);
        }
        EndConstrict(attackCooldown);
        return true;
    }

    void Face(Vector3 d) =>
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), rotationSpeed * Time.deltaTime);

    void Settle(Vector3 horizontal) => nav.Move(horizontal); // ход — общая локомоция (сглаживание/гравитация там)
}
