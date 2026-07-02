using UnityEngine;

/// <summary>
/// Оглушения существа, два калибра (граница — 1 секунда):
///  • СТАГГЕР (&lt;1с) — боёвочная динамика: короткий сбив от попаданий (Health.onDamaged), рвёт замахи;
///  • СТАН (≥1с) — полноценный контроль-эффект (вой и т.п.); позже обвесим спецэффектами (хук IsStunned).
/// Стан включает стаггер-поведение: IsStaggered истинно и под станом — психикам достаточно одной проверки.
/// </summary>
[RequireComponent(typeof(Health))]
public class Stagger : MonoBehaviour
{
    [SerializeField] float staggerTime = 0.35f; // сбив от попадания

    float staggerUntil, stunUntil;

    public bool IsStaggered => Time.time < staggerUntil || IsStunned;
    public bool IsStunned => Time.time < stunUntil; // хук под спецэффекты стана (VFX/анимация — потом)

    /// <summary>Короткий сбив (боёвка, &lt;1с). Не укорачивает уже идущий.</summary>
    public void Hitstun(float duration) => staggerUntil = Mathf.Max(staggerUntil, Time.time + duration);

    /// <summary>Полноценный стан (контроль, ≥1с).</summary>
    public void Stun(float duration) => stunUntil = Mathf.Max(stunUntil, Time.time + duration);

    void Awake() => GetComponent<Health>().onDamaged.AddListener(() => Hitstun(staggerTime));
}
