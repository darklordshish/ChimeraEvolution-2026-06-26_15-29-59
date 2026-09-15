using UnityEngine;

/// <summary>Результат тика активного приёма: идёт / исполнился / сорвался (уворот, стаггер).</summary>
public enum AbilityRun { Running, Done, Cancelled }

/// <summary>
/// База замаховых приёмов ИИ: TryUse запускает замах с телеграфом, психика тикает Tick(), пока Busy
/// (перф-правило: один Update на существо — своего Update у способностей нет). Правила срыва живут
/// в наследниках (Abort): укус сдаётся всегда, прыжок в полёте закоммичен. Кулдаун атак НЕ здесь —
/// общий ритм атак существа держит психика (это её решение, не свойство доставки).
/// </summary>
public abstract class WindupAbility : MonoBehaviour, IAbility, IAbilityCarrier
{
    [SerializeField, NotOrganData("физика тела: гравитация полёта одна у всех")] protected float gravity = -20f;

    protected CharacterController controller;
    protected Telegraph telegraph;
    protected Health ownHealth;
    protected Rage rage;
    SpawnVariance variance;
    protected Transform target;
    protected Health targetHealth;
    protected float windupEnd;
    float verticalVel;

    public bool Busy { get; private set; }

    /// <summary>ИДЁТ ЗАМАХ — наводиться ещё можно. С концом окна приём КОММИТИТСЯ: направление замирает,
    /// и дальше он летит туда, куда был заведён.
    ///     Это правило боя, а не деталь одного зверя. Уворот в игре держится на ТАЙМИНГЕ: игрок читает
    /// телеграф, ждёт последний момент и уходит вбок. Если враг доворачивается и на кадре удара, уворот
    /// перестаёт быть навыком — увернуться нельзя вовсе, потому что удар следует за целью. Если же не
    /// доворачивается вовсе, бить вплотную он не может: цель успевает отойти за время замаха.
    ///     Отсюда две фазы: ведёт → бьёт. Психики наводят приём, пока здесь true, и не трогают после.</summary>
    public bool InWindup => Busy && Time.time < windupEnd;

    protected virtual void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
        TryGetComponent(out ownHealth);
        TryGetComponent(out rage);
        TryGetComponent(out variance);
    }

    Morale morale;   // шкала духа стайных (вешает психика ПОСЛЕ нашего Awake — берём лениво)
    Satiety satiety; // шкала сытости-голода (тело вешает в Awake): истощённый бьёт слабее
    Bossness bossness; // модуль боссовости: множитель урона и вампиризм всех приёмов (лениво — порядок навески не важен)

    // урон доставки: ярость × разброс особи × ДУХ (раскачанная мораль бьёт больнее) × ВИГОР (истощённый — слабее)
    protected float DamageMult
    {
        get
        {
            if (morale == null) TryGetComponent(out morale);
            if (satiety == null) TryGetComponent(out satiety);
            if (bossness == null) TryGetComponent(out bossness);
            return (rage != null ? rage.DamageMult : 1f) * (variance != null ? variance.DamageMult : 1f)
                 * (morale != null ? morale.DamageMult : 1f) * (satiety != null ? satiety.Vigor : 1f)
                 * (bossness != null ? bossness.DamageMult : 1f);
        }
    }

    // ВАМПИРИЗМ — не черта приёма, а модуля боссовости: у приёмов было своё поле, и ставил его только вервольф
    protected int BossLifeSteal
    {
        get
        {
            if (bossness == null) TryGetComponent(out bossness);
            return bossness != null ? bossness.LifeSteal : 0;
        }
    }

    protected virtual void Start()
    {
        // дефолтная цель — игрок (волк охотится только на него); психика змеи переключает
        // цель на ЛЮБУЮ тёплую жертву через SetTarget (NPC-против-NPC)
        if (target == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) { target = pc.transform; targetHealth = pc.GetComponent<Health>(); }
        }
    }

    /// <summary>Сменить цель доставки (охота на NPC). null — вернуться к «нет цели». Не звать, пока Busy.</summary>
    public void SetTarget(Health h)
    {
        targetHealth = h;
        target = h != null ? h.transform : null;
    }

    // ГОТОВНОСТЬ И ЗАМАХ — ТОЛЬКО ИЗ ЗАПИСИ ОРГАНА (спека «данные в органах»): у базы своих чисел нет. Абстрактные
    // намеренно — новая доставка без записи не скомпилируется, а не заработает молча на дефолте
    protected abstract bool Ready { get; }
    protected abstract float WindupTime { get; }

    // ЦЕНА И ПЕРЕЗАРЯДКА ПРИЁМА — ТОЖЕ ИЗ ЗАПИСИ ОРГАНА (спека 16.09): доставка платит и ждёт сама, психика только решает.
    // Химера с лосиными ногами платит за таран ту же цену, что лось: цена принадлежит ногам, а не виду
    protected virtual float StaminaCost => 0f;
    protected virtual float Cooldown => 0f;
    [SerializeField, NotOrganData("правило боя: сорванный замах стоит долю перезарядки — пауза видна, беспомощности нет")]
    float cancelledCooldownShare = 0.25f;
    float readyAt;
    Stamina breath;
    Stamina Breath { get { if (breath == null) TryGetComponent(out breath); return breath; } } // бак тело до-создаёт в Recompute

    /// <summary>Можно ли запустить приём сейчас: запись есть, перезарядка прошла, дыхалки на цену хватает.
    /// Психика спрашивает это, а не держит свой таймер и не считает цену.</summary>
    public bool CanUse => Ready && Time.time >= readyAt && (StaminaCost <= 0f || Breath == null || Breath.Has(StaminaCost));

    void RefundCooldown() => readyAt = Mathf.Min(readyAt, Time.time + Cooldown * cancelledCooldownShare);

    /// <summary>ОКНО СРАБАТЫВАНИЯ по дистанции до цели — из записи органа: ближний приём бьёт от 0 до досягаемости,
    /// разбег, наскок и залп — с минимальной дистанции. Нет записи — окно пустое (0). Читает арбитр арсенала
    /// (`Arsenal`) у химеры-альфы. Верх абстрактный по той же причине, что замах: доставка без окна не скомпилируется.</summary>
    public virtual float WindowMin => 0f;
    public abstract float WindowMax { get; }

    // запуск замаха; false — занят, нет цели, приём недоступен, не прошла перезарядка или не хватает дыхалки
    public bool TryUse()
    {
        if (Busy || target == null || !CanUse) return false;
        if (StaminaCost > 0f && Breath != null && !Breath.TrySpend(StaminaCost)) return false; // цену платит сам носитель
        Busy = true;
        readyAt = Time.time + Cooldown; // перезарядка идёт с запуска
        windupEnd = Time.time + WindupTime;
        telegraph.Set(true, TelegraphColor, intent: true); // ЗАМАХ = намерение: цвет приёма читает лишь Чутьё
        OnBegin();
        return true;
    }

    /// <summary>Тик активного приёма (зовёт психика, пока Busy). Done/Cancelled завершают приём.</summary>
    public AbilityRun Tick()
    {
        if (!Busy) return AbilityRun.Cancelled; // сорван извне (Abort) — психика уйдёт в короткий откат
        if (!Ready) { Busy = false; telegraph.Clear(); return AbilityRun.Cancelled; } // орган сняли посреди замаха — приёма больше нет
        if (target == null || targetHealth == null) { Busy = false; telegraph.Clear(); return AbilityRun.Cancelled; } // цель умерла посреди приёма (NPC-жертва)
        var st = OnTick();
        if (st != AbilityRun.Running) { Busy = false; telegraph.Clear(); if (st == AbilityRun.Cancelled) RefundCooldown(); }
        return st;
    }

    /// <summary>Внешний срыв: hard (нокбэк) рвёт всё; мягкий (стаггер) — на усмотрение приёма.</summary>
    public virtual void Abort(bool hard)
    {
        if (!Busy) return;
        Busy = false;
        telegraph.Clear();
        RefundCooldown(); // сорван извне — не удар, полной перезарядки не стоит
    }

    protected abstract Color TelegraphColor { get; }
    protected virtual void OnBegin() { }
    protected abstract AbilityRun OnTick();

    // ── ОТЛАДОЧНЫЙ ХИТБОКС (единая система): конус приёма (луч + сектор) в ЦВЕТЕ ТЕЛЕГРАФА этого приёма —
    //    легенда хитбоксов = легенда сигналов (TelegraphColors). Каждый приём рисует себя САМ; психики гизмо не держат.
    //    Наследник задаёт дальность/угол; в эдит-режиме читает сериализованные поля (Awake не нужен).
    protected virtual float GizmoRange => 2f;
    protected virtual float GizmoHalfAngle => 45f;
    [SerializeField, NotOrganData("отладка: высота отрисовки хитбокса")] float gizmoHeight = 0.5f; // высота отрисовки хитбокса: низким (волк/змея) 0.5, высоким (лось) ставит префаб

    void OnDrawGizmos()
    {
        Vector3 o = transform.position + Vector3.up * gizmoHeight;
        Vector3 f = transform.forward;
        Gizmos.color = TelegraphColor;
        Gizmos.DrawLine(o, o + f * GizmoRange);
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(-GizmoHalfAngle, Vector3.up) * f * GizmoRange);
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(GizmoHalfAngle, Vector3.up) * f * GizmoRange);
    }

    // стоим на замахе: горизонталь ноль, гравитация работает
    protected void SettleInPlace()
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        controller.Move(new Vector3(0f, verticalVel, 0f) * Time.deltaTime);
    }

    protected Vector3 DirToTarget()
    {
        Vector3 d = target.position - transform.position; d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    protected float DistToTarget()
    {
        Vector3 d = target.position - transform.position; d.y = 0f;
        return d.magnitude;
    }
}
