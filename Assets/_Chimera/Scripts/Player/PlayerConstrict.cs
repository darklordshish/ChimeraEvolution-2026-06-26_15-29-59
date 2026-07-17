using UnityEngine;

/// <summary>
/// ОБХВАТ игрока (орган «Удушающий хвост» в химерном слоте — хвост как ДОП-ЧАСТЬ тела, ноги на месте;
/// нага/кентавр = сплав шасси, далёкое будущее). Держишь ХВОСТОМ, не телом:
/// руки свободны (меч/укус работают — яд+контроль = синергия), цена = ТВОЯ мобильность (SelfSlow).
/// Стадии — ЕДИНЫЙ захват (статус Grabbed, спека 2026-07-17): ст.1 — слабый хват: жертва на месте,
/// но ДЕРЁТСЯ (кусает/бодает тебя) и вырывается через случайное время (гонка «продержи до защёлка»);
/// ст.2 — ЗАЩЁЛКНУТО (Locked: стан, сама не вырвется). DoT-удушения у аугумент-версии НЕТ (кап
/// maxStage=2) — полное удушение придёт со змеиным шасси. Massive-жертва (босс) — на стадию слабее →
/// защёлк недостижим: всегда отбивается и уходит. Срывы: разовый урон ИЗВНЕ ≥ breakDamage (стая
/// спасает своего; удары самой жертвы — просто боль), ушёл дальше хвата, тебя схватили, повторное F.
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
    Camouflage victimCamo;
    Grabbed victimGrabbed; // единый статус захвата: импульс-стагер на укрепление, защёлк-стан — держит он сам
    // визуальный сигнал НЕ наш: защёлкнутая жертва в стане → StunTint сам красит её «выключенным» (единая легенда)
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
            if (lastHp - ownHealth.Current >= breakDamage && !ReferenceEquals(ownHealth.LastAttacker, victim))
            { Release(push: true); return; }
            lastHp = ownHealth.Current;
        }
        if (move != null && move.IsGrabbed) { Release(push: false); return; }
        Vector3 to = victim.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > holdRange) { Release(push: false); return; } // рывок/отход/отлёт от рогов рвёт сам

        // сжатие: ст.1 → слабый хват (жертва дерётся, гонка с вырыванием); ст.2 (в пределах капа) — защёлкнуто
        grip += tightenRate * Time.deltaTime;
        int newStage = grip >= stage2At && stageCap >= 2 ? 2 : 1;
        if (newStage != stage)
        {
            stage = newStage;
            victimGrabbed = Grabbed.Apply(victim.gameObject, ownHealth, stage, stage >= 2); // укрепление: импульс-стагер; защёлк-стан держит статус
        }
        if (stage < 2 && Time.time >= escapeAt) { Release(push: true); return; } // вырвалась (босс — всегда)

        // боль выдаёт камуфляж (стан защёлка — уже на статусе Grabbed, свой цикл не держим)
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
        if (victimGrabbed != null) victimGrabbed.Release(); // снять единый статус (жертва мертва — Unity-null пропустит)
        victim = null; victimCamo = null; victimGrabbed = null;
        stage = 0; grip = 0f;
        nextTime = Time.time + cooldown;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = TelegraphColors.Grab;
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}
