using UnityEngine;

/// <summary>Результат тика активного приёма: идёт / исполнился / сорвался (уворот, стаггер).</summary>
public enum AbilityRun { Running, Done, Cancelled }

/// <summary>
/// База замаховых приёмов ИИ: TryUse запускает замах с телеграфом, психика тикает Tick(), пока Busy
/// (перф-правило: один Update на существо — своего Update у способностей нет). Правила срыва живут
/// в наследниках (Abort): укус сдаётся всегда, прыжок в полёте закоммичен. Кулдаун атак НЕ здесь —
/// общий ритм атак существа держит психика (это её решение, не свойство доставки).
/// </summary>
public abstract class WindupAbility : MonoBehaviour, IAbility
{
    [SerializeField] protected float windupTime = 0.45f;
    [SerializeField] protected float gravity = -20f;

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

    protected virtual void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
        TryGetComponent(out ownHealth);
        TryGetComponent(out rage);
        TryGetComponent(out variance);
    }

    Morale morale; // шкала духа стайных (вешает психика ПОСЛЕ нашего Awake — берём лениво)

    // урон доставки: ярость × разброс особи × ДУХ (M2: раскачанная мораль бьёт больнее — плавно до +25%)
    protected float DamageMult
    {
        get
        {
            if (morale == null) TryGetComponent(out morale);
            return (rage != null ? rage.DamageMult : 1f) * (variance != null ? variance.DamageMult : 1f)
                 * (morale != null ? morale.DamageMult : 1f);
        }
    }

    protected virtual void Start()
    {
        // дефолтная цель — игрок (волк/вервольф охотятся только на него); психика змеи переключает
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

    // запуск замаха; false — если уже занят или нет цели
    public bool TryUse()
    {
        if (Busy || target == null) return false;
        Busy = true;
        windupEnd = Time.time + windupTime;
        telegraph.Set(true, TelegraphColor, intent: true); // ЗАМАХ = намерение: цвет приёма читает лишь Чутьё
        OnBegin();
        return true;
    }

    /// <summary>Тик активного приёма (зовёт психика, пока Busy). Done/Cancelled завершают приём.</summary>
    public AbilityRun Tick()
    {
        if (!Busy) return AbilityRun.Cancelled; // сорван извне (Abort) — психика уйдёт в короткий откат
        if (target == null || targetHealth == null) { Busy = false; telegraph.Clear(); return AbilityRun.Cancelled; } // цель умерла посреди приёма (NPC-жертва)
        var st = OnTick();
        if (st != AbilityRun.Running) { Busy = false; telegraph.Clear(); }
        return st;
    }

    /// <summary>Внешний срыв: hard (нокбэк) рвёт всё; мягкий (стаггер) — на усмотрение приёма.</summary>
    public virtual void Abort(bool hard)
    {
        if (!Busy) return;
        Busy = false;
        telegraph.Clear();
    }

    protected abstract Color TelegraphColor { get; }
    protected virtual void OnBegin() { }
    protected abstract AbilityRun OnTick();

    // ── ОТЛАДОЧНЫЙ ХИТБОКС (единая система): конус приёма (луч + сектор) в ЦВЕТЕ ТЕЛЕГРАФА этого приёма —
    //    легенда хитбоксов = легенда сигналов (TelegraphColors). Каждый приём рисует себя САМ; психики гизмо не держат.
    //    Наследник задаёт дальность/угол; в эдит-режиме читает сериализованные поля (Awake не нужен).
    protected virtual float GizmoRange => 2f;
    protected virtual float GizmoHalfAngle => 45f;
    [SerializeField] float gizmoHeight = 0.5f; // высота отрисовки хитбокса: низким (волк/змея) 0.5, высоким (лось/вервольф) ставит префаб

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
