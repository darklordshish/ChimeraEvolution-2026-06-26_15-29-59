using UnityEngine;

/// <summary>Итог тика захвата: держим / жертва вырвалась / сорвали извне / жертвы больше нет.</summary>
public enum GrabTick { Holding, Escaped, Broken, Gone }

/// <summary>
/// ЕДИНАЯ МАШИНА ЗАХВАТА (ядро) — фича грэпл-органа, а не психики. Держит ОДНО: сжатие → стадии →
/// защёлк → удушение. ЯДА ЗДЕСЬ НЕТ: яд — фича КЛЫКОВ (орган), в хвате его льют укусы драйвера.
/// Кто решает «когда хватать» и «куда тащить» — драйвер (ввод игрока / психика):
/// он зовёт Begin/Tick/End и читает Stage/Victim. Локомоция, carry-на-стену, телеграф и BreakFree —
/// НЕ здесь (законно расходятся у носителей), как виндап и разбег в семье удара.
///
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="ConstrictData"/>): сжатие, стадии и кап, окно вырывания, порог срыва,
/// удушение, кровь на входе, расход дыхалки. Нет записи — нет захвата: Begin откажет, активный хват отпустится.
///
/// СТАДИИ (статус Grabbed; спеки: единый захват 2026-07-17, хвост-эталон 2026-07-19):
///  ст.1 — слабый хват: жертва ДЕРЁТСЯ (её урон ослабляет сжатие) и вырывается по таймеру (гонка);
///  ст.2 — ЗАЩЁЛК: стан, сама не выйдет;
///  ст.3 — ПАРТЕР: сжатие то же, но хват до предела + чок-DoT; жертва только зовёт своих.
/// РАТЧЕТ: достигнутая стадия — пол, ниже её порога сжатие не откатывается. СПАСАТЕЛИ: удар ИЗВНЕ сбивает
/// на стадию (3→2→1→сорван), опуская и ратчет — отбить своего можно только НАСТОЙЧИВОСТЬЮ, зато один
/// укус переживается (успеешь утащить добычу — додушишь там, куда стае не достать).
/// КАП — запись: дома `maxStage`, на чужом шасси не выше `foreignMaxStage` (режет тело при раскрытии записи).
/// Massive-жертва — на стадию слабее (единое правило хвата).
/// </summary>
public class Constrict : MonoBehaviour, IOrganAbility
{
    ConstrictData data; // раскрытая запись грэпл-органа; null — держать нечем

    public System.Type DataType => typeof(ConstrictData);
    public bool Available => data != null;

    /// <summary>Запись от тела (или от драйвера игрока). Сняли орган посреди хвата — хват отпускается.</summary>
    public void Configure(AbilityData d)
    {
        data = d as ConstrictData;
        if (data == null && Holding) End();
    }

    float wearNext;
    Stamina breath;
    // ленивая привязка: бак до-создаёт тело в Recompute — он бывает позже нашего Awake
    Stamina Breath { get { if (breath == null) TryGetComponent(out breath); return breath; } }

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

    /// <summary>Взять жертву. owner нужен только для жертвы-игрока (ApplyGrab/ReleaseGrab).</summary>
    public bool Begin(Health victim, IGrabber grabOwner = null)
    {
        if (data == null || victim == null || Holding) return false;
        held = victim;
        owner = grabOwner;
        victim.TryGetComponent(out heldPlayer);
        // Massive-туша — на стадию слабее (единое правило); кап записи режет сверху
        heldCap = victim.GetComponent<Massive>() != null ? data.maxStage - 1 : data.maxStage;
        if (heldCap < 1) { held = null; heldPlayer = null; owner = null; return false; }

        grip = 0f; gripFloor = 0f; stage = 1; reached = 1; chokeNext = 0f;
        lastHp = ownHealth != null ? ownHealth.Current : 0;
        if (heldPlayer != null && owner != null) heldPlayer.ApplyGrab(owner, data.grabSlow1); // игрок: режем ход и рывок
        else
        {
            heldGrabbed = Grabbed.Apply(held.gameObject, ownHealth, 1, false);     // слабый хват: импульс-стагер, жертва ДЕРЁТСЯ
            escapeAt = Time.time + Random.Range(data.escapeMin, data.escapeMax);   // гонка: дожми до защёлка
        }

        // ЗУБЫ СОМКНУЛИСЬ: разовая кровь за вход в хват (у челюстей волка; у хвоста её нет)
        if (data.grabBleedStacks > 0)
        {
            var hit = new Hit(ownHealth, transform.position);
            for (int i = 0; i < data.grabBleedStacks; i++) hit.Apply(victim, HitEffect.Bleed());
        }
        return true;
    }

    /// <summary>Тик машины (зовёт драйвер, пока держим). Локомоцию/carry драйвер делает сам.</summary>
    public GrabTick Tick()
    {
        if (held == null) return GrabTick.Gone;      // жертва умерла — хват свободен
        if (ownHealth == null || data == null) return GrabTick.Broken;

        bool victimIsPlayer = heldPlayer != null;
        int dmg = lastHp - ownHealth.Current;
        lastHp = ownHealth.Current;

        // СПАСАТЕЛИ отбивают своего: удар ИЗВНЕ не рвёт хват разом, а СБИВАЕТ НА СТАДИЮ — гонка на истощение
        // хватки (ст.3→2→1→сорван). Один укус переживается: держащая на партере змея успеет уволочь добычу
        // к стене, если стая не добавит. Работает для ЛЮБОЙ жертвы, включая игрока (у него бывают
        // кин-союзники, и один волк обязан выручить). Порог — по СЫРОМУ удару (броня держателя не «помогает
        // держать»). Урон от САМОЙ жертвы срывом не считается — это её контр-игра: откат сжатия
        // (loosenPerDamage) плюс рывок через BreakFree у драйвера
        if (dmg > 0 && ownHealth.LastRawDamage >= data.breakRawThreshold
            && !ReferenceEquals(ownHealth.LastAttacker, held))
        {
            if (stage <= 1) return GrabTick.Broken; // ниже некуда — отбили
            KnockDown();
            return GrabTick.Holding; // держим дальше, но слабее — удар «съел» тик сжатия
        }

        // ВЫДОХСЯ — ХВАТ СЛАБЕЕТ (не рвётся): тот же механизм, что удар спасателя, только источник другой.
        // Не роняет добычу разом — перестаёт дожимать, и стадии осыпаются по одной. Жертве это даёт честный
        // выход «на измор»: не бей, а тяни время
        if (Breath != null && !Breath.TrySpend(data.holdDrain * Time.deltaTime) && Time.time >= wearNext)
        {
            wearNext = Time.time + data.wearInterval;
            if (stage <= 1) return GrabTick.Broken; // выдохся вчистую — разжал
            KnockDown();
            return GrabTick.Holding;
        }

        grip += data.tightenRate * Time.deltaTime;
        if (dmg > 0) grip -= dmg * (victimIsPlayer ? data.loosenPerDamage : data.npcLoosenPerDamage);
        grip = Mathf.Max(gripFloor, grip); // РАТЧЕТ: ниже порога достигнутой стадии не пускаем

        int newStage = Mathf.Min(grip >= data.stage3At ? 3 : grip >= data.stage2At ? 2 : 1, heldCap);
        if (newStage != stage) SetStage(newStage);

        // слабый хват: не дожал — жертва вырвалась (у игрока свой срыв, таймера нет)
        if (!victimIsPlayer && stage < 2 && Time.time >= escapeAt) return GrabTick.Escaped;

        // ПАРТЕР (ст.3): удушение тиками. Минует i-frames — рывком из удушения не спрятаться
        if (stage >= 3 && Time.time >= chokeNext)
        {
            chokeNext = Time.time + (victimIsPlayer ? data.chokeInterval : data.npcChokeInterval);
            held.LastAttacker = ownHealth; // смерть от удушения — убийство захватчика (родство/переваривание)
            held.TakeDamage(victimIsPlayer ? data.chokeDamage : data.npcChokeDamage, true);
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

    // стадия вниз вместе с РАТЧЕТОМ — иначе сжатие тут же вернёт сбитое (удар спасателя и выдохшаяся хватка)
    void KnockDown()
    {
        int down = stage - 1;
        reached = down;
        gripFloor = down >= 3 ? data.stage3At : down >= 2 ? data.stage2At : 0f;
        grip = gripFloor;
        SetStage(down);
    }

    void SetStage(int s)
    {
        if (s > reached)
        {
            reached = s;
            gripFloor = s >= 3 ? data.stage3At : data.stage2At; // ратчет: защёлкнулись — назад за порог не пускаем
        }
        stage = s;
        if (heldPlayer != null && owner != null) heldPlayer.ApplyGrab(owner, SlowFor(s));
        else if (held != null) heldGrabbed = Grabbed.Apply(held.gameObject, ownHealth, s, s >= 2); // ст.2+ = защёлк-стан
    }

    float SlowFor(int s) => data == null ? 1f : s >= 3 ? data.grabSlow3 : s == 2 ? data.grabSlow2 : data.grabSlow1;
}
