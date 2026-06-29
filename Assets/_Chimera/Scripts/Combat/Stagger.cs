using UnityEngine;

/// <summary>
/// Оглушение при получении урона (через Health.onDamaged). Пока оглушён — ИИ должен стоять и не атаковать.
/// Читается из WolfAI через свойство IsStaggered.
/// </summary>
[RequireComponent(typeof(Health))]
public class Stagger : MonoBehaviour
{
    [SerializeField] float staggerTime = 0.35f;

    float until;
    public bool IsStaggered => Time.time < until;

    void Awake() => GetComponent<Health>().onDamaged.AddListener(() => until = Time.time + staggerTime);
}
