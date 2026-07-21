using UnityEngine;

/// <summary>
/// ХВАТ ХВОСТОМ игрока — тонкий ДРАЙВЕР над ЕДИНОЙ машиной захвата (Constrict — та же, что у змеи;
/// хвост-эталон 2026-07-19). Орган «Хвост» в химерном слоте: держишь ДОП-ЧАСТЬЮ тела — руки свободны
/// (меч/укус работают), цена = ТВОЯ мобильность (SelfSlow). Машина ведёт сжатие/стадии/ратчет/Grabbed/
/// гонку вырывания/чок; драйвер решает КОГДА (F-тумблер), КУДА (волочение за собой) и свои срывы:
///  РЫВОК (резко дёрнулся — цена волочения: двигайся плавно), СПАСАТЕЛЬ (сырой удар ≥ breakDamage —
///  тик яда не рвёт, укус волка рвёт), тебя схватили, жертва ушла дальше holdRange, повторное F.
/// КАП стадий — ось nativeChassis (тело зовёт SetMaxStage): человечье шасси → 2, ст.3-партер придёт
/// со змеиным шасси. С защёлка (ст.2) жертва — НОША: едет за тобой (право тащить даёт сам захват;
/// змея тем же правом тащит на стену — у неё «куда» решает психика, у тебя — ноги).
/// Massive-жертва (босс) — на стадию слабее (правило машины).
/// </summary>
public class PlayerConstrict : MonoBehaviour, IAbility
{
    [Header("Захват (орган «Хвост») — драйверные ручки; машина сжатия — компонент Constrict")]
    [SerializeField] float grabRange = 2.2f;      // дальность подбора цели (в упор)
    [SerializeField] float holdRange = 3.2f;      // ушёл дальше — хватка соскользнула
    [SerializeField] float cooldown = 2.5f;
    [SerializeField] float escapeMin = 1f;        // ст.1: NPC-жертва вырывается через случайное время (мягче змеиных 2.6–4:
    [SerializeField] float escapeMax = 2.5f;      // хвост на чужом шасси держит слабее — кандидат на ось nativeChassis)
    [SerializeField, Range(0f, 1f)] float selfSlow1 = 0.8f;  // твоё замедление: держишь ОТДЕЛЬНОЙ частью тела (−20%)
    [SerializeField, Range(0f, 1f)] float selfSlow2 = 0.6f;  // ст.2+ (−40%); на партере слоу не растёт — растёт хват
    [SerializeField] int breakDamage = 5;         // спасатель: СЫРОЙ удар ≥ этого рвёт хват (тик яда 3 — нет, укус волка 8 — да)
    [SerializeField] float escapeKnock = 6f;      // отлёт жертвы при вырывании/срыве
    [SerializeField] float dragOffset = 1.1f;     // ст.2+: на каком выносе ПОЗАДИ тебя едет ноша

    public bool ConstrictEnabled { get; set; }    // включается органом «Хвост»
    public bool Holding => machine != null && machine.Holding;
    public int Stage => Holding ? machine.Stage : 0;                  // HUD: стадия обхвата
    public Health Victim => machine != null ? machine.Victim : null;  // HUD: HP жертвы
    public bool Presenting => presenting;                             // HUD: ноша подставлена под удары
    public float SelfSlow => !Holding ? 1f : (machine.Stage >= 2 ? selfSlow2 : selfSlow1); // PlayerController читает

    Constrict machine; // ЕДИНАЯ машина захвата (общая со змеёй) — лениво: порядок Awake с CreatureBody не гарантирован
    Camouflage victimCamo;
    PlayerController move;
    float nextTime;
    bool carriedOn;    // ноша взята (ICarried) — раз на защёлк
    bool presenting;   // позиция ноши: false — ЗА СПИНОЙ (походный хват, идёшь свободно); true — ПЕРЕД
                       // СОБОЙ, ПОД СВОИ УДАРЫ (стойка разделки: её капсула мешает идти вперёд — намеренно)

    Constrict Machine
    {
        get
        {
            if (machine == null)
            {
                if (!TryGetComponent(out machine)) machine = gameObject.AddComponent<Constrict>();
                // профиль НОСИТЕЛЯ-аугумента: мягкая гонка вырывания + порог срыва спасателем
                machine.ConfigureHolder(escapeMin, escapeMax, breakDamage);
            }
            return machine;
        }
    }

    void Awake() => move = GetComponent<PlayerController>();

    /// <summary>Кап стадий от ТЕЛА (ось nativeChassis): родное шасси → 3 (партер+чок), чужое → 2.</summary>
    public void SetMaxStage(int v) => Machine.SetMaxStage(v);

    /// <summary>Тумблер позиции ноши (кнопка C): за спину ↔ подставить под свои удары. Осмыслен с защёлка (ст.2+).</summary>
    public void TogglePresent() { if (Holding && machine.Stage >= 2) presenting = !presenting; }

    // водитель зовёт по F: схватить ближайшего в упор / отпустить, если уже держим
    public bool TryUse()
    {
        if (!ConstrictEnabled) return false;
        if (Holding) { Release(push: false); return true; } // добровольно отпустил — без отлёта
        if (Time.time < nextTime) return false;
        if (move != null && move.IsGrabbed) return false;   // сам в чьей-то пасти — не до обхватов

        var target = FindVictim();
        if (target == null || !Machine.Begin(target)) return false;
        target.TryGetComponent(out victimCamo);
        carriedOn = false; presenting = false; // новый хват — походный (за спиной), подставляешь кнопкой
        Perception.BreakGhost(); // dev-призрак: обхват раскрывает
        return true;
    }

    Health FindVictim()
    {
        Health best = null; float bestDist = float.MaxValue;
        foreach (var hp in TargetScan.Healths(transform.position, grabRange, transform))
        {
            if (hp.GetComponent<Stagger>() == null) continue; // обхват держит через стан — цель должна его уметь
            float d = (hp.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = hp; }
        }
        return best;
    }

    void Update()
    {
        if (!Holding) return;

        // драйверные срывы (машина про них не знает)
        if (move != null && move.IsGrabbed) { Release(push: false); return; } // тебя схватили — не до обхватов
        if (move != null && move.IsDashing) { Release(push: true); return; }  // РЕЗКО ДЁРНУЛСЯ — сорвал (цена волочения)
        var v = machine.Victim;
        Vector3 to = v.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > holdRange) { Release(push: false); return; }       // ушла дальше хвата — соскользнула

        // тик МАШИНЫ: сжатие/стадии/Grabbed/гонка/срыв спасателем — её дело; нам — исход
        var tick = machine.Tick();
        if (tick == GrabTick.Gone) { Release(push: false); return; }   // жертва умерла — хвост свободен
        if (tick != GrabTick.Holding) { Release(push: true); return; } // вырвалась / спасатель отбил — с отлётом

        // ЗАЩЁЛКНУЛ (ст.2+) → жертва НОША: волочение («куда тащить» решаешь ногами; C — тумблер позиции:
        // за спиной = походный хват, перед собой = подставил под свои удары — добивание связанного)
        if (machine.Stage >= 2)
        {
            if (!carriedOn && v.TryGetComponent<ICarried>(out var c)) { carriedOn = true; c.SetCarried(true); }
            v.transform.position = transform.position + (presenting ? transform.forward : -transform.forward) * dragOffset;
        }

        // боль/хват выдаёт камуфляж (стан защёлка — уже на статусе Grabbed)
        if (victimCamo != null) victimCamo.Reveal(0.4f);
    }

    // push: жертва вырвалась/сбили — отлетает; добровольное отпускание/смерть — без отлёта
    void Release(bool push)
    {
        var v = machine != null ? machine.Victim : null;
        if (push && v != null && v.TryGetComponent<Knockback>(out var kb))
        {
            Vector3 away = v.transform.position - transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) kb.Push(away.normalized * escapeKnock);
        }
        if (machine != null) machine.End(); // снимет единый статус Grabbed и ношу (ICarried) сам
        victimCamo = null; carriedOn = false;
        nextTime = Time.time + cooldown;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = TelegraphColors.Grab;
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}
