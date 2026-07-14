using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Переиспользуемое здоровье. Повесим и на врагов (волки), и при желании на игрока.
/// onDamaged / onDeath — точки для фидбека (вспышка, звук) и логики (родство на смерть врага).
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 30;
    [SerializeField] bool destroyOnDeath = true;

    public int Current { get; private set; }
    public int Max => maxHealth;
    public bool Invulnerable { get; set; }       // i-frames (напр. на время рывка)
    public float DamageReduction { get; set; }   // 0..1, броня (слот «Кожа»)
    public bool GodMode { get; set; }            // отладка (G): не убивает — урон ВИДЕН, но пол в 1 HP (тестить эффекты не умирая)
    public float RegenPerSecond { get; set; }    // реген всегда, в т.ч. в бою (слот «Сердце»; волки)
    public float OutOfCombatRegen { get; set; }  // реген только вне боя (база человека)
    // «в бою» = недавно тебя АКТИВНО преследовал враг (любой: волк/змея/босс зовёт MarkInCombat). Таймер: гаснет сам.
    float combatUntil;
    public bool InCombat => Time.time < combatUntil;
    public void MarkInCombat(float seconds = 1f) => combatUntil = Mathf.Max(combatUntil, Time.time + seconds);
    public int OverhealCap { get; set; }         // на сколько можно перелечиться свыше макс. (temp HP боссa; не регенится)
    public Health LastAttacker { get; set; }     // кто ударил последним: родство за смерть — УБИЙЦЕ (ставят Hit.Apply/яд/удушение)

    public UnityEvent onDamaged = new(); // = new(), чтобы не были null при AddComponent в рантайме (босс)
    public UnityEvent onDeath = new();

    bool dead;
    float regenAccum;
    float regenSuppressUntil, regenSuppressFactor = 1f; // дебафф регена (укус Пасти игрока)
    Rage rage; // ярость: входящий урон больше (может добавиться в рантайме — ленивый поиск)
    Venom venom; // яд: 2+ стака поднимают входящий урон (стадия уязвимости)

    void Awake() => Current = maxHealth;

    void Update()
    {
        // тихая регенерация (без лога): постоянная + добавочная вне боя; копим дробные HP, не выше максимума
        if (dead || Current >= maxHealth) return;
        float rate = RegenPerSecond;
        if (!InCombat) rate += OutOfCombatRegen;
        if (Time.time < regenSuppressUntil) rate *= regenSuppressFactor; // сбит укусом Пасти
        if (rate <= 0f) return;

        regenAccum += rate * Time.deltaTime;
        if (regenAccum >= 1f)
        {
            int whole = Mathf.FloorToInt(regenAccum);
            regenAccum -= whole;
            Current = Mathf.Min(maxHealth, Current + whole);
        }
    }

    // конструктор меняет макс. HP при смене органа в слоте «Сердце» (разницу даём/забираем у текущего)
    public void SetMaxHealth(int newMax)
    {
        int delta = newMax - maxHealth;
        maxHealth = Mathf.Max(1, newMax);
        Current = Mathf.Clamp(Current + delta, 1, maxHealth);
    }

    // лечение (слот «Пасть» — вампиризм)
    public void Heal(int amount)
    {
        if (dead || amount <= 0) return;
        Current = Mathf.Min(maxHealth + OverhealCap, Current + amount); // вампиризм может уйти свыше макс. (temp HP)
    }

    // временно сбить реген цели (укус Пасти): factor<1 уменьшает реген на duration секунд (рефрешится)
    public void SuppressRegen(float factor, float duration)
    {
        regenSuppressFactor = Mathf.Clamp01(factor);
        regenSuppressUntil = Time.time + duration;
    }

    public void ClearOverheal() { if (Current > maxHealth) Current = maxHealth; } // сброс temp HP (босс потерял цель)

    public void TakeDamage(int amount) => TakeDamage(amount, false);

    // ignoreInvuln — для урона, который должен пройти сквозь i-frames (напр. сам себя рвёшь из захвата рывком).
    // Режим бога и смерть по-прежнему защищают.
    public void TakeDamage(int amount, bool ignoreInvuln)
    {
        if (dead || amount <= 0) return;
        if (Invulnerable && !ignoreInvuln) return;

        if (rage == null) TryGetComponent(out rage);
        if (venom == null) TryGetComponent(out venom);
        float incoming = (rage != null ? rage.IncomingMult : 1f) * (venom != null ? venom.IncomingMult : 1f); // ярость + яд роняют защиту
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - DamageReduction) * incoming)); // броня (слот «Кожа»)
        // РЕЖИМ БОГА: урон ПРИМЕНЯЕТСЯ (виден — тестить яд/кровь/эффекты), но не убивает — пол в 1 HP
        Current = Mathf.Max(GodMode ? 1 : 0, Current - amount);
        onDamaged?.Invoke();

        if (Current == 0)
        {
            dead = true;
            onDeath?.Invoke();
            if (destroyOnDeath) Destroy(gameObject);
        }
    }
}
