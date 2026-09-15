using UnityEngine;

/// <summary>
/// ХВАТ игрока — тонкий ДРАЙВЕР над ЕДИНОЙ машиной захвата (Constrict — та же, что у змеи, волка и ежа;
/// хвост-эталон 2026-07-19). Грэпл-орган (Хвост в химерном слоте, волчья Пасть…): держишь ДОП-ЧАСТЬЮ тела —
/// руки свободны (меч/укус работают), цена = ТВОЯ мобильность (SelfSlow). Машина ведёт сжатие/стадии/ратчет/
/// Grabbed/гонку вырывания/чок/срыв спасателем; драйвер решает КОГДА (F-тумблер), КУДА (волочение за собой) и свои срывы:
///  РЫВОК (резко дёрнулся — цена волочения: двигайся плавно), тебя схватили, жертва ушла дальше holdRange, повторное F.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="ConstrictData"/>): блок «Игрок» читает драйвер, остальное — машина.
/// Это та же запись, что держит змея: у хвоста игрока нет своего «профиля носителя».
/// КАП стадий — тоже запись: на человечьем шасси хвост держит до ст.2, партер (ст.3) — дома, у змеи.
/// С защёлка (ст.2) жертва — НОША: едет за тобой (право тащить даёт сам захват;
/// змея тем же правом тащит на стену — у неё «куда» решает психика, у тебя — ноги).
/// Massive-жертва (босс) — на стадию слабее (правило машины).
/// </summary>
public class PlayerConstrict : MonoBehaviour, IAbility, IOrganAbility
{
    ConstrictData data; // раскрытая запись грэпл-органа; null — хватать нечем

    public System.Type DataType => typeof(ConstrictData);
    public bool Available => data != null;

    /// <summary>Запись от тела — драйверу и его машине. Сняли орган посреди хвата — отпускаем без отлёта.</summary>
    public void Configure(AbilityData d)
    {
        var next = d as ConstrictData;
        if (next == null && Holding) Release(push: false); // ещё со старой записью: её перезарядка
        data = next;
        if (data != null || machine != null) Machine.Configure(data); // без записи машину зря не заводим
    }

    public bool Holding => machine != null && machine.Holding;
    public int Stage => Holding ? machine.Stage : 0;                  // HUD: стадия обхвата
    public Health Victim => machine != null ? machine.Victim : null;  // HUD: HP жертвы
    public bool Presenting => presenting;                             // HUD: ноша подставлена под удары
    public float SelfSlow => !Holding || data == null ? 1f : (machine.Stage >= 2 ? data.selfSlow2 : data.selfSlow1); // PlayerController читает

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
            if (machine == null && !TryGetComponent(out machine)) machine = gameObject.AddComponent<Constrict>();
            return machine;
        }
    }

    void Awake() => move = GetComponent<PlayerController>();

    /// <summary>Тумблер позиции ноши (кнопка C): за спину ↔ подставить под свои удары. Осмыслен с защёлка (ст.2+).</summary>
    public void TogglePresent() { if (Holding && machine.Stage >= 2) presenting = !presenting; }

    // водитель зовёт по F: схватить ближайшего в упор / отпустить, если уже держим
    public bool TryUse()
    {
        if (data == null) return false;
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
        foreach (var hp in TargetScan.Healths(transform.position, data.grabRange, transform))
        {
            if (hp.GetComponent<Stagger>() == null) continue; // обхват держит через стан — цель должна его уметь
            float d = (hp.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = hp; }
        }
        return best;
    }

    void Update()
    {
        if (!Holding || data == null) return;

        // драйверные срывы (машина про них не знает)
        if (move != null && move.IsGrabbed) { Release(push: false); return; } // тебя схватили — не до обхватов
        if (move != null && move.IsDashing) { Release(push: true); return; }  // РЕЗКО ДЁРНУЛСЯ — сорвал (цена волочения)
        var v = machine.Victim;
        Vector3 to = v.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > data.holdRange) { Release(push: false); return; }  // ушла дальше хвата — соскользнула

        // тик МАШИНЫ: сжатие/стадии/Grabbed/гонка/срыв спасателем — её дело; нам — исход
        var tick = machine.Tick();
        if (tick == GrabTick.Gone) { Release(push: false); return; }   // жертва умерла — хвост свободен
        if (tick != GrabTick.Holding) { Release(push: true); return; } // вырвалась / спасатель отбил — с отлётом

        // ЗАЩЁЛКНУЛ (ст.2+) → жертва НОША: волочение («куда тащить» решаешь ногами; C — тумблер позиции:
        // за спиной = походный хват, перед собой = подставил под свои удары — добивание связанного)
        if (machine.Stage >= 2)
        {
            if (!carriedOn && v.TryGetComponent<ICarried>(out var c)) { carriedOn = true; c.SetCarried(true); }
            v.transform.position = transform.position + (presenting ? transform.forward : -transform.forward) * data.dragOffset;
        }

        // боль/хват выдаёт камуфляж (стан защёлка — уже на статусе Grabbed)
        if (victimCamo != null) victimCamo.Reveal(0.4f);
    }

    // push: жертва вырвалась/сбили — отлетает; добровольное отпускание/смерть — без отлёта
    void Release(bool push)
    {
        var v = machine != null ? machine.Victim : null;
        if (push && v != null && data != null && v.TryGetComponent<Knockback>(out var kb))
        {
            Vector3 away = v.transform.position - transform.position; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) kb.Push(away.normalized * data.escapeKnock);
        }
        if (machine != null) machine.End(); // снимет единый статус Grabbed и ношу (ICarried) сам
        victimCamo = null; carriedOn = false;
        nextTime = Time.time + (data != null ? data.cooldown : 0f);
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = TelegraphColors.Grab;
        Gizmos.DrawWireSphere(transform.position, data.grabRange);
    }
}
