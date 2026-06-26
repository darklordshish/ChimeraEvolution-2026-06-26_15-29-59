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

    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    bool dead;

    void Awake() => Current = maxHealth;

    public void TakeDamage(int amount)
    {
        if (dead || amount <= 0) return;

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
