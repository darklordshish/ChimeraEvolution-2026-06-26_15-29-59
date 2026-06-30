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
    public bool Invulnerable { get; set; }      // i-frames (напр. на время рывка)
    public float DamageReduction { get; set; }  // 0..1, броня (слот «Кожа»)
    public bool GodMode { get; set; }            // отладка: неуязвимость (клавиша G)

    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    bool dead;

    void Awake() => Current = maxHealth;

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
        int before = Current;
        Current = Mathf.Min(maxHealth, Current + amount);
        Debug.Log($"{name} +{Current - before} HP (лечение) → {Current}/{maxHealth}");
    }

    public void TakeDamage(int amount) => TakeDamage(amount, false);

    // ignoreInvuln — для урона, который должен пройти сквозь i-frames (напр. сам себя рвёшь из захвата рывком).
    // Режим бога и смерть по-прежнему защищают.
    public void TakeDamage(int amount, bool ignoreInvuln)
    {
        if (dead || GodMode || amount <= 0) return;
        if (Invulnerable && !ignoreInvuln) return;

        amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - DamageReduction))); // броня (слот «Кожа»)
        Current = Mathf.Max(0, Current - amount);
        Debug.Log($"{name} получил {amount} урона → {Current}/{maxHealth}"); // временно — видно, что удар регистрируется
        onDamaged?.Invoke();

        if (Current == 0)
        {
            dead = true;
            onDeath?.Invoke();
            if (destroyOnDeath) Destroy(gameObject);
        }
    }
}
