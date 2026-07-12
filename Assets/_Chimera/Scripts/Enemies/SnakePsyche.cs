using UnityEngine;

/// <summary>
/// Психика змеи — СОЛО-засадный охотник ХОЛОДНОГО РАСЧЁТА. Цель — ближайшая ТЁПЛАЯ ОДИНОЧКА
/// в термо-радиусе: игрок ИЛИ волк — для змеи всё добыча, поведение унифицировано (первое
/// NPC-против-NPC в игре). Рядом с жертвой другие тёплые (стая, компания) — тихо сидит в засаде;
/// Massive-туши не трогает. Рывок из засады (`LeapAbility`) → ядовитый укус (`BiteAbility`) → ОБХВАТ:
/// ИГРОКА — стадийная машина с ратчетом (ст.1 рвётся рывком/пинком → ст.2 защёлк, бить змею →
/// ст.3 корень+удушение; вой-стан рвёт ст.1–2, ст.3 — мёртвая хватка); NPC-ЖЕРТВУ — унифицированный
/// хват: стан + удушение до смерти (урон по змее спасает). Гремок мигает жёлтой погремушкой.
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
    [SerializeField] float creepRange = 11f;                // ближе — подкрадывается; дальше в термо-радиусе — ждёт
    [SerializeField, Range(0f, 1f)] float creepSpeed = 0.5f;
    [SerializeField] float lonelyRadius = 6f;               // «одиночка» = нет ДРУГИХ тёплых в этом радиусе вокруг жертвы
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
    [SerializeField] int npcChokeDamage = 6;                 // удушение NPC-жертвы: урон за тик (хват без стадий)
    [SerializeField] float npcChokeInterval = 0.6f;

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

    // текущая ЖЕРТВА охоты (игрок или NPC) — выбирается сканом «тёплая одиночка»
    Transform target;
    Health targetHealth;

    // жертва ОБХВАТА фиксируется на входе (retarget её не трогает)
    Health heldHealth;
    Stagger heldStagger;
    bool heldIsPlayer;

    float nextAttackTime, verticalVel, windupEnd, nextRattle, rattleBlinkUntil, nextScan;
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
    }

    void OnDisable()
    {
        if (constricting) ReleaseHeld(); // убили на обхвате — отпустить жертву
    }

    float Speed => moveSpeed * (variance != null ? variance.SpeedMult : 1f);

    // тело-на-шасси Змея кормит деривированное (урон укуса, скорость); яд/обхват — фирменные, на компонентах/психике
    public void OnBodyStats(int damage, float bodyMoveSpeed)
    {
        moveSpeed = bodyMoveSpeed;
        if (bite != null) bite.SetDamage(damage);
    }

    // камуфляж: раскрыть себя на время боя (лениво берём компонент — CreatureBody вешает его после нашего Awake)
    void RevealSelf()
    {
        if (camo == null) TryGetComponent(out camo);
        if (camo != null) camo.Reveal(revealMemory);
    }

    // ГРЕМОК: затаившаяся змея периодически мигает ПОГРЕМУШКОЙ (тело невидимо; звук позже) — зацепка/приманка
    void TryRattle()
    {
        if (Time.time < nextRattle) return;
        nextRattle = Time.time + rattleInterval;
        rattleBlinkUntil = Time.time + rattleCue;
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

        if (stagger != null && stagger.IsStaggered) { Settle(Vector3.zero); return; }

        if (windingUp) { RevealSelf(); UpdateGrabWindup(); return; }

        // ХОЛОДНЫЙ РАСЧЁТ: пересматриваем жертву (тёплая одиночка в термо-радиусе; стая рядом — никого не трогаем)
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + retargetInterval;
            ChooseTarget();
        }

        // жертвы нет ЛИБО пропала из термо (умерла/призрак/ушла) → засада: сидим тихо, гремим
        if (target == null || !Perception.SeesThermal(transform.position, target, thermalRange))
        {
            TryRattle();
            Settle(Vector3.zero);
            return;
        }

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

        // подпустила близко → подкрадывается; далеко в термо-радиусе → терпеливо ждёт (засада, не гонит по карте)
        Settle(dist <= creepRange ? nav.DirTo(target.position) * Speed * creepSpeed : Vector3.zero);
    }

    bool heldIsPlayerTarget() => targetHealth != null && playerCtl != null && targetHealth.transform == playerCtl.transform;

    // выбор жертвы: ближайшая ТЁПЛАЯ ОДИНОЧКА (термо гейтит и холодных, и призрака); Massive не по зубам
    void ChooseTarget()
    {
        Health best = null;
        float bestD = float.MaxValue;
        foreach (var col in Physics.OverlapSphere(transform.position, thermalRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || hp == best) continue;
            if (!Perception.SeesThermal(transform.position, hp.transform, thermalRange)) continue;
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

    // «одиночка» = рядом с жертвой нет ДРУГИХ тёплых (стая/компания отпугивает; сама змея холодная — не мешает)
    bool IsLonely(Health candidate)
    {
        foreach (var col in Physics.OverlapSphere(candidate.transform.position, lonelyRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var other = col.GetComponentInParent<Health>();
            if (other == null || other == candidate || other.transform == transform) continue;
            if (Perception.IsWarm(other.transform)) return false;
        }
        return true;
    }

    // замах обхвата: жертва увернулась из радиуса — сорван; выдержала — обвивает
    void UpdateGrabWindup()
    {
        if (target == null) { windingUp = false; telegraph.Clear(); Settle(Vector3.zero); return; }
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
        heldHealth.TryGetComponent(out heldStagger);
        heldIsPlayer = playerCtl != null && heldHealth.transform == playerCtl.transform;

        grip = 0f; gripFloor = 0f; stage = 1; maxStage = 1;
        lastHp = ownHealth != null ? ownHealth.Current : 0;
        chokeNext = 0f;
        telegraph.Set(true, TelegraphColors.Grab);
        if (heldIsPlayer) playerCtl.ApplyGrab(this, grabSlow1); // игрок: режем ход и рывок (усилится к ст.3)
    }

    void UpdateConstrict()
    {
        if (heldHealth == null) { EndConstrict(attackCooldown); return; } // жертва умерла — хвост свободен
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

        int newStage = grip >= stage3At ? 3 : grip >= stage2At ? 2 : 1;
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

    // обхват NPC-ЖЕРТВЫ (волк и будущие тёплые): унифицированный хват — стан + удушение до смерти.
    // Спасение: ЛЮБОЙ урон по змее рвёт хват (игрок может отбить волка — или дождаться конца охоты)
    void UpdateNpcConstrict()
    {
        if (lastHp > ownHealth.Current) { EndConstrict(attackCooldown); return; } // по змее попали — хват сорван
        lastHp = ownHealth.Current;

        Vector3 to = heldHealth.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > bite.Range * 1.6f) { EndConstrict(0.5f); return; } // жертву оттолкнуло — соскользнула

        if (heldStagger != null) heldStagger.Stun(0.3f); // жертва обездвижена (психики уважают стан, StunTint красит)

        if (Time.time >= chokeNext)
        {
            heldHealth.LastAttacker = ownHealth; // жертва «достаётся змее» (родство — убийце, задел эволюции)
            heldHealth.TakeDamage(npcChokeDamage, true); // смерть жертвы отпустит хват сама (heldHealth → null)
            chokeNext = Time.time + npcChokeInterval;
        }

        HoldNearVictim();
    }

    // стоим у жертвы, морда к ней, мягко держим дистанцию удержания (не таскаем её за собой)
    void HoldNearVictim()
    {
        Vector3 to = heldHealth.transform.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.001f ? to / d : transform.forward;
        Face(dir);
        float hold = bite.Range * 0.6f;
        Settle(Vector3.ClampMagnitude(dir * (d - hold) * 8f, moveSpeed));
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
        telegraph.Set(true, StageColor(s));
        if (heldIsPlayer && playerCtl != null) playerCtl.ApplyGrab(this, SlowFor(s));
    }

    void InjectVenom()
    {
        if (heldHealth != null) new Hit(null, transform.position).Apply(heldHealth, HitEffect.Venom());
    }

    void ReleaseHeld()
    {
        if (heldIsPlayer && playerCtl != null) playerCtl.ReleaseGrab(this);
        heldHealth = null; heldStagger = null; heldIsPlayer = false;
    }

    void EndConstrict(float cooldown)
    {
        constricting = false; windingUp = false;
        telegraph.Clear();
        ReleaseHeld();
        stage = 0; grip = 0f; gripFloor = 0f;
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

    Color StageColor(int s) => s >= 3 ? TelegraphColors.Bite : s == 2 ? TelegraphColors.Charge : TelegraphColors.Grab;
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

    void OnDrawGizmos()
    {
        float r = bite != null ? bite.Range : 2f;
        Vector3 o = transform.position + Vector3.up * 0.5f;
        Gizmos.color = constricting ? StageColor(stage) : (windingUp ? TelegraphColors.Grab : new Color(0.4f, 0.8f, 0.4f));
        Gizmos.DrawLine(o, o + transform.forward * r);
    }
}
