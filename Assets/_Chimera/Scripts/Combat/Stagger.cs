using UnityEngine;

/// <summary>
/// Оглушение при получении урона (через Health.onDamaged). Пока оглушён — ИИ должен стоять и не атаковать.
/// Читается из WolfPsyche через свойство IsStaggered.
/// </summary>
[RequireComponent(typeof(Health))]
public class Stagger : MonoBehaviour
{
    [SerializeField] float staggerTime = 0.35f;

    float until;
    public bool IsStaggered => Time.time < until;

    /// <summary>Прямое оглушение (без урона) — например, вой-стан. Не укорачивает уже идущий стаггер.</summary>
    public void Stun(float duration) => until = Mathf.Max(until, Time.time + duration);

    void Awake() => GetComponent<Health>().onDamaged.AddListener(() => Stun(staggerTime));
}
