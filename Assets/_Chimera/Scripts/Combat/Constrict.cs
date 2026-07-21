using UnityEngine;

/// <summary>Итог тика захвата: держим / жертва вырвалась / сорвали извне / жертвы больше нет.</summary>
public enum GrabTick { Holding, Escaped, Broken, Gone }

/// <summary>
/// ЕДИНАЯ МАШИНА ЗАХВАТА (ядро) — фича органа «Хвост», а не психики. Держит ОДНО: сжатие → стадии →
/// защёлк → удушение. Кто решает «когда хватать» и «куда тащить» — драйвер (ввод игрока / психика):
/// он зовёт Begin/Tick/End и читает Stage/Victim. Локомоция, carry-на-стену, телеграф и BreakFree —
/// НЕ здесь (законно расходятся у носителей), как виндап и разбег в семье удара.
///
/// СТАДИИ (статус Grabbed; спеки: единый захват 2026-07-17, хвост-эталон 2026-07-19):
///  ст.1 — слабый хват: жертва ДЕРЁТСЯ (её урон ослабляет сжатие) и вырывается по таймеру (гонка);
///  ст.2 — ЗАЩЁЛК: стан, сама не выйдет;
///  ст.3 — ПАРТЕР: сжатие то же, но хват до предела + чок-DoT; жертва только зовёт своих.
/// РАТЧЕТ: достигнутая стадия — пол, ниже её порога сжатие не откатывается.
/// КАП — ось экспрессии nativeChassis (тело зовёт SetMaxStage): РОДНОЕ шасси → 3, чужое → 2.
/// Massive-жертва — на стадию слабее (единое правило хвата).
/// Числа перенесены ДОСЛОВНО из проверенной машины змеи (в т.ч. баланс «одна змея душит волка»).
/// </summary>
public class Constrict : MonoBehaviour
{
    [Header("Сжатие")]
    [SerializeField] float tightenRate = 1f;    // сжатие/сек
    [SerializeField] float stage2At = 1.5f;     // сжатие ≥ этого → ст.2 (защёлк)
    [SerializeField] float stage3At = 3.5f;     // сжатие ≥ этого → ст.3 (партер + чок)

    [Header("Жертва-ИГРОК")]
    [SerializeField] float loosenPerDamage = 0.12f;           // 1 HP урона по захватчику откатывает сжатие (контр-игра игрока — не трогать)
    [SerializeField] float chokeDamage = 4f;                  // ст.3: урон за тик
    [SerializeField] float chokeInterval = 0.5f;
    [SerializeField, Range(0f, 1f)] float grabSlow1 = 0.35f;  // замедление жертвы-игрока по стадиям (ход И рывок)
    [SerializeField, Range(0f, 1f)] float grabSlow2 = 0.15f;
    [SerializeField, Range(0f, 1f)] float grabSlow3 = 0f;     // ст.3 — полный корень

    [Header("Жертва-NPC")]
    [SerializeField] float npcLoosenPerDamage = 0.04f;        // укус жертвы ослабляет хват слабее (одиночка гонку не выигрывает)
    [SerializeField] int npcChokeDamage = 6;                  // ст.3: урон за тик
    [SerializeField] float npcChokeInterval = 0.6f;
    [SerializeField] float escapeMin = 2.6f;                  // ст.1: NPC-жертва вырывается через случайное время (окно ПОЗЖЕ защёлка)
    [SerializeField] float escapeMax = 4f;

    [Header("Профиль носителя")]
    [SerializeField] int breakRawThreshold = 0; // спасатели: внешний СЫРОЙ удар ≥ порога рвёт хват (сырой — броня держателя
                                                // не «помогает держать»). 0 = рвёт любой (змея); хвост игрока ставит 5
    [SerializeField] bool venomOnStage = true;  // яд с каждой новой стадии (змея); аугумент-хвост — без (яд — фича клыков, не хвата)

    int maxStageAllowed = 3; // кап от ТЕЛА (nativeChassis); дефолт 3 — родное шасси

    Health ownHealth;
    Health held;
    Grabbed heldGrabbed;
    PlayerController heldPlayer;
    IGrabber owner;                 // для ApplyGrab/ReleaseGrab жертвы-игрока (срыв рывком — у драйвера)
    float grip, gripFloor, chokeNext, escapeAt;
    int stage, reached, heldCap, lastHp;

    public bool Holding => held != null;
    public int Stage => stage;
    public Health Victim => held;
    public bool VictimIsPlayer => heldPlayer != null;
    public float StageT => Mathf.Clamp01(stage / 3f);   // для градиента телеграфа у драйвера
    public float PlayerSlow => SlowFor(stage);

    void Awake() => TryGetComponent(out ownHealth);

    /// <summary>Кап стадий от ТЕЛА (ось nativeChassis): родное шасси → 3 (партер+чок), чужое → 2.</summary>
    public void SetMaxStage(int v) => maxStageAllowed = Mathf.Clamp(v, 1, 3);

    /// <summary>Профиль носителя (зовёт драйвер в Awake): гонка вырывания NPC-жертвы, порог срыва спасателем,
    /// яд со стадий. Змея живёт на дефолтах (2.6–4 / любой удар / яд), хвост игрока — мягче и без яда.</summary>
    public void ConfigureHolder(float npcEscapeMin, float npcEscapeMax, int rawBreakThreshold, bool venomStages)
    {
        escapeMin = npcEscapeMin; escapeMax = npcEscapeMax;
        breakRawThreshold = rawBreakThreshold; venomOnStage = venomStages;
    }

    /// <summary>Взять жертву. owner нужен только для жертвы-игрока (ApplyGrab/ReleaseGrab).</summary>
    public bool Begin(Health victim, IGrabber grabOwner = null)
    {
        if (victim == null || Holding) return false;
        held = victim;
        owner = grabOwner;
        victim.TryGetComponent(out heldPlayer);
        // Massive-туша — на стадию слабее (единое правило); кап тела режет сверху
        heldCap = victim.GetComponent<Massive>() != null ? maxStageAllowed - 1 : maxStageAllowed;
        if (heldCap < 1) { held = null; heldPlayer = null; return false; }

        grip = 0f; gripFloor = 0f; stage = 1; reached = 1; chokeNext = 0f;
        lastHp = ownHealth != null ? ownHealth.Current : 0;
        if (heldPlayer != null && owner != null) heldPlayer.ApplyGrab(owner, grabSlow1); // игрок: режем ход и рывок
        else
        {
            heldGrabbed = Grabbed.Apply(held.gameObject, ownHealth, 1, false); // слабый хват: импульс-стагер, жертва ДЕРЁТСЯ
            escapeAt = Time.time + Random.Range(escapeMin, escapeMax);         // гонка: дожми до защёлка
        }
        return true;
    }

    /// <summary>Тик машины (зовёт драйвер, пока держим). Локомоцию/carry драйвер делает сам.</summary>
    public GrabTick Tick()
    {
        if (held == null) return GrabTick.Gone;      // жертва умерла — хват свободен
        if (ownHealth == null) return GrabTick.Broken;

        bool victimIsPlayer = heldPlayer != null;
        int dmg = lastHp - ownHealth.Current;
        lastHp = ownHealth.Current;

        // урон ИЗВНЕ рвёт хват (спасатели отбивают своего); порог — по СЫРОМУ удару (тик яда не рвёт у игрока);
        // урон от САМОЙ жертвы — только ослабляет сжатие. У жертвы-игрока «извне» не разбираем: его контр-игра —
        // откат сжатия и рывок (BreakFree у драйвера)
        if (!victimIsPlayer && dmg > 0 && ownHealth.LastRawDamage >= breakRawThreshold
            && !ReferenceEquals(ownHealth.LastAttacker, held)) return GrabTick.Broken;

        grip += tightenRate * Time.deltaTime;
        if (dmg > 0) grip -= dmg * (victimIsPlayer ? loosenPerDamage : npcLoosenPerDamage);
        grip = Mathf.Max(gripFloor, grip); // РАТЧЕТ: ниже порога достигнутой стадии не пускаем

        int newStage = Mathf.Min(grip >= stage3At ? 3 : grip >= stage2At ? 2 : 1, heldCap);
        if (newStage != stage) SetStage(newStage);

        // слабый хват: не дожал — жертва вырвалась (у игрока свой срыв, таймера нет)
        if (!victimIsPlayer && stage < 2 && Time.time >= escapeAt) return GrabTick.Escaped;

        // ПАРТЕР (ст.3): удушение тиками. Минует i-frames — рывком из удушения не спрятаться
        if (stage >= 3 && Time.time >= chokeNext)
        {
            chokeNext = Time.time + (victimIsPlayer ? chokeInterval : npcChokeInterval);
            held.LastAttacker = ownHealth; // смерть от удушения — убийство захватчика (родство/переваривание)
            held.TakeDamage(victimIsPlayer ? Mathf.RoundToInt(chokeDamage) : npcChokeDamage, true);
        }
        if (victimIsPlayer) held.MarkInCombat();
        return GrabTick.Holding;
    }

    /// <summary>Отпустить (драйвер сам решает кулдаун/последствия).</summary>
    public void End()
    {
        if (heldPlayer != null && owner != null) heldPlayer.ReleaseGrab(owner);
        else if (held != null && held.TryGetComponent<ICarried>(out var carried)) carried.SetCarried(false); // ноша оживает
        if (heldGrabbed != null) heldGrabbed.Release();
        held = null; heldGrabbed = null; heldPlayer = null; owner = null;
        stage = 0; reached = 0; grip = 0f; gripFloor = 0f;
    }

    void SetStage(int s)
    {
        if (s > reached)
        {
            if (venomOnStage) InjectVenom();              // туже сжатие → впрыск яда (раз на новую стадию; хвост игрока — без)
            reached = s;
            gripFloor = s >= 3 ? stage3At : stage2At;     // ратчет: защёлкнулись — назад за порог не пускаем
        }
        stage = s;
        if (heldPlayer != null && owner != null) heldPlayer.ApplyGrab(owner, SlowFor(s));
        else if (held != null) heldGrabbed = Grabbed.Apply(held.gameObject, ownHealth, s, s >= 2); // ст.2+ = защёлк-стан
    }

    // яд со стадий — С ИСТОЧНИКОМ: смерть от него атрибутируется захватчику
    void InjectVenom()
    {
        if (held != null) new Hit(ownHealth, transform.position).Apply(held, HitEffect.Venom());
    }

    float SlowFor(int s) => s >= 3 ? grabSlow3 : s == 2 ? grabSlow2 : grabSlow1;
}
