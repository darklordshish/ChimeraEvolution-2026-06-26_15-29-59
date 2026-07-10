using UnityEngine;

/// <summary>
/// ОБХВАТ игрока (орган «Удушающий хвост» в химерном слоте — хвост как ДОП-ЧАСТЬ тела, ноги на месте;
/// нага/кентавр = сплав шасси, далёкое будущее). Держишь ХВОСТОМ, не телом:
/// руки свободны (меч/укус работают — яд+контроль = синергия), цена = ТВОЯ мобильность (SelfSlow).
/// Стадии — зеркало змеи, но время работает НА тебя: ст.1 — жертва обездвижена (продлеваемый стан),
/// вырывается через случайное время (гонка «продержи до защёлка»); ст.2 — ЗАЩЁЛКНУТО (сама не вырвется).
/// DoT-удушения у аугумент-версии НЕТ (кап maxStage=2) — полное удушение придёт со змеиным шасси.
/// Massive-жертва (босс) держится на стадию слабее. Срывы: разовый урон по тебе ≥ breakDamage
/// (стая спасает своего), ушёл дальше хвата (рывок рвёт сам), тебя схватили, повторное F.
/// </summary>
public class PlayerConstrict : MonoBehaviour, IAbility
{
    [Header("Обхват (Удушающий хвост)")]
    [SerializeField] float grabRange = 2.2f;      // дальность подбора цели (в упор)
    [SerializeField] float holdRange = 3.2f;      // ушёл дальше — хватка соскользнула (рывок рвёт сам)
    [SerializeField] float cooldown = 2.5f;
    [SerializeField] float tightenRate = 1f;      // сжатие/сек
    [SerializeField] float stage2At = 1.5f;       // защёлк: жертва сама не вырвется
    [SerializeField] float escapeMin = 1f;        // ст.1: жертва вырывается через случайное время из диапазона
    [SerializeField] float escapeMax = 2.5f;
    [SerializeField, Range(0f, 1f)] float selfSlow1 = 0.55f; // твоё замедление: держишь отдельной частью тела
    [SerializeField, Range(0f, 1f)] float selfSlow2 = 0.3f;
    [SerializeField] int breakDamage = 5;         // разовый урон по тебе ≥ этого рвёт обхват (яд-тик не рвёт)
    [SerializeField] int maxStage = 2;            // аугумент-хвост: кап ст.2 (ст.3-удушение — у змеиного шасси)
    [SerializeField] float escapeKnock = 6f;      // отлёт жертвы при вырывании/отпускании

    public bool ConstrictEnabled { get; set; }    // включается органом «Удушающий хвост»
    public bool Holding => victim != null;
    public int Stage => Holding ? stage : 0;      // HUD: стадия обхвата
    public Health Victim => victim;               // HUD: HP жертвы
    public float SelfSlow => !Holding ? 1f : (stage >= 2 ? selfSlow2 : selfSlow1); // PlayerController читает

    Health ownHealth, victim;
    Stagger victimStagger;
    Camouflage victimCamo;
    // визуальный сигнал НЕ наш: жертва в стане → StunTint сам красит её статус-цветом «выключен» (единая легенда)
    PlayerController move;
    float grip, escapeAt, nextTime;
    int stage, stageCap, lastHp;

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
        victim.TryGetComponent(out victimStagger);
        victim.TryGetComponent(out victimCamo);
        stageCap = victim.GetComponent<Massive>() != null ? maxStage - 1 : maxStage; // массивная туша — на стадию слабее
        if (stageCap < 1) { victim = null; return false; }
        grip = 0f; stage = 1;
        escapeAt = Time.time + Random.Range(escapeMin, escapeMax); // гонка: продержи до защёлка
        lastHp = ownHealth != null ? ownHealth.Current : 0;
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

        // срывы: тебя ударили (стая спасает своего), тебя схватили, ты ушёл дальше хвата
        if (ownHealth != null)
        {
            if (lastHp - ownHealth.Current >= breakDamage) { Release(push: true); return; }
            lastHp = ownHealth.Current;
        }
        if (move != null && move.IsGrabbed) { Release(push: false); return; }
        Vector3 to = victim.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > holdRange) { Release(push: false); return; } // рывок/отход рвёт сам

        // сжатие: ст.1 → гонка с вырыванием; ст.2 (в пределах капа) — защёлкнуто
        grip += tightenRate * Time.deltaTime;
        stage = grip >= stage2At && stageCap >= 2 ? 2 : 1;
        if (stage < 2 && Time.time >= escapeAt) { Release(push: true); return; } // вырвалась (босс — всегда)

        // держим: продлеваемый стан (психики его уважают, StunTint красит статус-цветом) + боль выдаёт камуфляж
        if (victimStagger != null) victimStagger.Stun(0.3f);
        if (victimCamo != null) victimCamo.Reveal(0.4f);
    }

    // push: жертва вырвалась/сбили — отлетает; добровольное отпускание/смерть — без отлёта
    void Release(bool push)
    {
        if (victim != null && push && victim.TryGetComponent<Knockback>(out var kb))
        {
            Vector3 away = victim.transform.position - transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) kb.Push(away.normalized * escapeKnock);
        }
        victim = null; victimStagger = null; victimCamo = null;
        stage = 0; grip = 0f;
        nextTime = Time.time + cooldown;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}
