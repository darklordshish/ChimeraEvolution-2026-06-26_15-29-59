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

    // ярость поднимает урон доставки; разброс особи делает волков разными
    protected float DamageMult => (rage != null ? rage.DamageMult : 1f) * (variance != null ? variance.DamageMult : 1f);

    protected virtual void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { target = pc.transform; targetHealth = pc.GetComponent<Health>(); }
    }

    // запуск замаха; false — если уже занят или нет цели
    public bool TryUse()
    {
        if (Busy || target == null) return false;
        Busy = true;
        windupEnd = Time.time + windupTime;
        telegraph.Set(true, TelegraphColor);
        OnBegin();
        return true;
    }

    /// <summary>Тик активного приёма (зовёт психика, пока Busy). Done/Cancelled завершают приём.</summary>
    public AbilityRun Tick()
    {
        if (!Busy) return AbilityRun.Cancelled; // сорван извне (Abort) — психика уйдёт в короткий откат
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
