using UnityEngine;

/// <summary>
/// ЕДИНАЯ МАШИНА ЗАХВАТА (орган «Хвост» в химерном слоте — хвост как ДОП-ЧАСТЬ тела, ноги на месте;
/// нага/кентавр = сплав шасси, далёкое будущее). Держишь ХВОСТОМ, не телом: руки свободны (меч/укус
/// работают — яд+контроль = синергия), цена = ТВОЯ мобильность (SelfSlow).
/// СТАДИИ (статус Grabbed; спеки: единый захват 2026-07-17, хвост-эталон 2026-07-19):
///  ст.1 — слабый хват: жертва на месте, но ДЕРЁТСЯ и вырывается через случайное время (гонка
///         «продержи до защёлка»), слоу −20%;
///  ст.2 — ЗАЩЁЛКНУТО (Locked: стан, сама не вырвется), слоу −40%; жертва становится НОШЕЙ — ТАЩИШЬ её за собой;
///  ст.3 — ПАРТЕР: слоу ТОТ ЖЕ (−40%), но хват крепчает до предела + чок-DoT; жертва только зовёт своих.
/// КАП СТАДИЙ — ось экспрессии nativeChassis (кормит тело через SetMaxStage): РОДНОЕ шасси → 3,
/// чужое → 2 («хвост — не тело змеи»: конструкция не даёт идеально обхватить крупную добычу).
/// Massive-жертва (босс) — на стадию слабее. Срывы: РЫВОК (резко дёрнулся — цена волочения: двигайся
/// плавно), разовый урон ИЗВНЕ ≥ breakDamage (стая спасает своего; удары самой жертвы — просто боль),
/// ушёл дальше хвата, тебя схватили, повторное F.
/// </summary>
public class PlayerConstrict : MonoBehaviour, IAbility
{
    [Header("Захват (орган «Хвост»)")]
    [SerializeField] float grabRange = 2.2f;      // дальность подбора цели (в упор)
    [SerializeField] float holdRange = 3.2f;      // ушёл дальше — хватка соскользнула (рывок рвёт сам)
    [SerializeField] float cooldown = 2.5f;
    [SerializeField] float tightenRate = 1f;      // сжатие/сек
    [SerializeField] float stage2At = 1.5f;       // защёлк: жертва сама не вырвется
    [SerializeField] float stage3At = 3f;         // ПАРТЕР (только на родном шасси): хват крепчает до предела + чок
    [SerializeField] float escapeMin = 1f;        // ст.1: жертва вырывается через случайное время из диапазона
    [SerializeField] float escapeMax = 2.5f;
    [SerializeField, Range(0f, 1f)] float selfSlow1 = 0.8f;  // твоё замедление: держишь ОТДЕЛЬНОЙ частью тела (−20%)
    [SerializeField, Range(0f, 1f)] float selfSlow2 = 0.6f;  // ст.2 И ст.3 (−40%): на партере слоу НЕ растёт — растёт ХВАТ
    [SerializeField] int breakDamage = 5;         // разовый урон по тебе ≥ этого рвёт обхват (яд-тик не рвёт)
    [SerializeField] int maxStage = 2;            // кап стадий — кормит ТЕЛО по nativeChassis (родное шасси → 3, чужое → 2)
    [SerializeField] float chokeInterval = 0.6f;  // ст.3: период тика удушения
    [SerializeField] int chokeDamage = 4;         // ст.3: урон за тик (DoT партера)
    [SerializeField] float escapeKnock = 6f;      // отлёт жертвы при вырывании/отпускании
    [SerializeField] float dragOffset = 1.1f;     // ст.2+: на каком выносе ПОЗАДИ тебя едет ноша

    public bool ConstrictEnabled { get; set; }    // включается органом «Хвост»
    public bool Holding => victim != null;
    public int Stage => Holding ? stage : 0;      // HUD: стадия обхвата
    public Health Victim => victim;               // HUD: HP жертвы
    public float SelfSlow => !Holding ? 1f : (stage >= 2 ? selfSlow2 : selfSlow1); // PlayerController читает

    Health ownHealth, victim;
    Camouflage victimCamo;
    Grabbed victimGrabbed; // единый статус захвата: импульс-стагер на укрепление, защёлк-стан — держит он сам
    ICarried victimCarried; // ст.2+: жертва — НОША (глушит свою локомоцию, позицией владеем мы)
    // визуальный сигнал НЕ наш: защёлкнутая жертва в стане → StunTint сам красит её «выключенным» (единая легенда)
    PlayerController move;
    float grip, escapeAt, nextTime, chokeNext;
    int stage, stageCap, lastHp;  // stage 1..3 (3 — партер, только на родном шасси)

    /// <summary>Кап стадий от ТЕЛА (ось экспрессии nativeChassis): родное шасси → 3 (партер+чок), чужое → 2.</summary>
    public void SetMaxStage(int v) => maxStage = Mathf.Clamp(v, 1, 3);

    void Awake()
    {
        ownHealth = GetComponent<Health>();
        move = GetComponent<PlayerController>();
    }

    // водитель зовёт по F: схватить ближайшего в упор / отпустить, если уже держим
    public bool TryUse()
    {
        if (!ConstrictEnabled) return false;
        if (Holding) { Release(push: false); return true; } // добровольно отпустил — без отлёта
        if (Time.time < nextTime) return false;
        if (move != null && move.IsGrabbed) return false;   // сам в чьей-то пасти — не до обхватов

        var target = FindVictim();
        if (target == null) return false;

        victim = target;
        victim.TryGetComponent(out victimCamo);
        stageCap = victim.GetComponent<Massive>() != null ? maxStage - 1 : maxStage; // массивная туша — на стадию слабее
        if (stageCap < 1) { victim = null; return false; }
        grip = 0f; stage = 1;
        victimGrabbed = Grabbed.Apply(victim.gameObject, ownHealth, 1, false); // слабый хват: импульс-стагер, жертва дальше дерётся
        escapeAt = Time.time + Random.Range(escapeMin, escapeMax); // гонка: продержи до защёлка
        lastHp = ownHealth != null ? ownHealth.Current : 0;
        Perception.BreakGhost(); // dev-призрак: обхват раскрывает
        return true;
    }

    Health FindVictim()
    {
        Health best = null; float bestDist = float.MaxValue;
        foreach (var col in Physics.OverlapSphere(transform.position, grabRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform) continue;
            if (hp.GetComponent<Stagger>() == null) continue; // обхват держит через стан — цель должна его уметь
            float d = (hp.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = hp; }
        }
        return best;
    }

    void Update()
    {
        if (!Holding) return;
        if (victim == null) { Release(push: false); return; } // жертва умерла — хвост свободен

        // срывы: удар ИЗВНЕ ≥ breakDamage рвёт (стая спасает своего); удары САМОЙ жертвы — просто боль,
        // цена удержания дерущегося зверя (единый захват: жертва работает с хватом, спасатели рвут)
        if (ownHealth != null)
        {
            // СРЫВ СПАСАТЕЛЕМ: судим по СЫРОМУ удару (до брони) — иначе толстая шкура «помогала» бы держать,
            // и укус волка-товарища (8) проваливался под порог, а тик яда (3) порог не берёт на любой броне
            if (lastHp - ownHealth.Current > 0 && ownHealth.LastRawDamage >= breakDamage
                && !ReferenceEquals(ownHealth.LastAttacker, victim))
            { Release(push: true); return; }
            lastHp = ownHealth.Current;
        }
        if (move != null && move.IsGrabbed) { Release(push: false); return; }
        // РЕЗКО ДЁРНУЛСЯ — СОРВАЛ: рывок несовместим с удержанием (цена волочения — двигайся плавно)
        if (move != null && move.IsDashing) { Release(push: true); return; }
        Vector3 to = victim.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > holdRange) { Release(push: false); return; } // рывок/отход/отлёт от рогов рвёт сам

        // сжатие: ст.1 → слабый хват (жертва дерётся, гонка с вырыванием); ст.2 (в пределах капа) — защёлкнуто
        grip += tightenRate * Time.deltaTime;
        int newStage = grip >= stage3At && stageCap >= 3 ? 3 : grip >= stage2At && stageCap >= 2 ? 2 : 1;
        if (newStage != stage)
        {
            stage = newStage;
            victimGrabbed = Grabbed.Apply(victim.gameObject, ownHealth, stage, stage >= 2); // укрепление: импульс-стагер; защёлк-стан держит статус
            // ЗАЩЁЛКНУЛ → жертва становится НОШЕЙ: сопротивляться не может, тащи куда хочешь
            if (stage >= 2 && victimCarried == null && victim.TryGetComponent(out victimCarried))
                victimCarried.SetCarried(true);
        }
        if (stage >= 2) DragVictim(); // волочение: ноша едет позади тебя
        if (stage < 2 && Time.time >= escapeAt) { Release(push: true); return; } // вырвалась (босс — всегда)

        // ПАРТЕР (ст.3, доступна только на РОДНОМ шасси): удушение тиками — жертва сама не выйдет, только зовёт своих
        if (stage >= 3 && Time.time >= chokeNext)
        {
            chokeNext = Time.time + chokeInterval;
            new Hit(ownHealth, transform.position).Apply(victim, HitEffect.Damage(chokeDamage));
        }

        // боль выдаёт камуфляж (стан защёлка — уже на статусе Grabbed, свой цикл не держим)
        if (victimCamo != null) victimCamo.Reveal(0.4f);
    }

    // ВОЛОЧЕНИЕ (ст.2+): позицией ноши владеем мы — едет позади, чтобы не упираться в твою капсулу.
    // «Куда тащить» решает драйвер (у змеи — на стену-насест), само право тащить даёт ЗАХВАТ
    void DragVictim()
    {
        if (victim == null) return;
        victim.transform.position = transform.position - transform.forward * dragOffset;
    }

    // push: жертва вырвалась/сбили — отлетает; добровольное отпускание/смерть — без отлёта
    void Release(bool push)
    {
        if (victim != null && push && victim.TryGetComponent<Knockback>(out var kb))
        {
            Vector3 away = victim.transform.position - transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) kb.Push(away.normalized * escapeKnock);
        }
        if (victimCarried != null) victimCarried.SetCarried(false); // ноша отпущена — оживает и падает
        if (victimGrabbed != null) victimGrabbed.Release(); // снять единый статус (жертва мертва — Unity-null пропустит)
        victim = null; victimCamo = null; victimGrabbed = null; victimCarried = null;
        stage = 0; grip = 0f;
        nextTime = Time.time + cooldown;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = TelegraphColors.Grab;
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}
