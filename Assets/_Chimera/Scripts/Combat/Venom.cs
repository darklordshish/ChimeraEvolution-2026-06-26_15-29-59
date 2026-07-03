using UnityEngine;

/// <summary>
/// ЯД — накопительный статус-эффект (эскалирующий по стакам; тот же паттерн, что будущий обхват).
/// Стаки набегают от укусов (`HitEffect.Venom`), выцветают за stackDuration без нового укуса.
/// Пороги: 1 стак — стоп реген; 2 — уязвимость (входящий урон ↑, читает `Health.TakeDamage`); 3 — тик урона (DoT).
/// До-создаётся на цели в рантайме при первом укусе (параметры — свойство ЯДА, не жертвы; дефолты ок).
/// </summary>
public class Venom : MonoBehaviour
{
    [SerializeField] int maxStacks = 3;
    [SerializeField] float stackDuration = 4f;       // сколько живёт стак без нового укуса
    [SerializeField] float vulnerabilityMult = 1.4f; // 2 стака: входящий урон ×
    [SerializeField] int dotDamage = 3;              // 3 стака: урон за тик
    [SerializeField] float dotInterval = 0.6f;

    int stacks;
    float expireAt, nextDot;
    Health health;

    public int Stacks => Time.time < expireAt ? stacks : 0;
    public float IncomingMult => Stacks >= 2 ? vulnerabilityMult : 1f; // читает Health.TakeDamage

    void Awake() => TryGetComponent(out health);

    public void AddStack()
    {
        stacks = Mathf.Min(maxStacks, Stacks + 1);
        expireAt = Time.time + stackDuration;
    }

    void Update()
    {
        int s = Stacks;
        if (s <= 0 || health == null) return;
        health.SuppressRegen(0f, 0.3f);       // стадия 1: реген стоит, пока отравлен (пуш через существующий API)
        if (s >= 3 && Time.time >= nextDot)   // стадия 3: DoT
        {
            nextDot = Time.time + dotInterval;
            health.TakeDamage(dotDamage, true); // яд минует i-frames — от того, что уже внутри, не увернёшься
        }
    }
}
