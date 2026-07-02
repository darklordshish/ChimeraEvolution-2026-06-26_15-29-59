using UnityEngine;

/// <summary>
/// Индивидуальный разброс особи (Ф6): при спавне ОДИН раз катает множители HP/урона/скорости —
/// стая из «30±5» вместо клонов. Вешается на рядовых мобов (RequireComponent у WolfPsyche);
/// босс и игрок — штучные, без разброса (детерминированная база).
/// Потребители множителей — те же точки, что у Rage: доставки (урон), психика (скорость); HP применяется сам.
/// </summary>
public class SpawnVariance : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)] float hpSpread = 0.15f;     // ±15% к макс. HP
    [SerializeField, Range(0f, 0.5f)] float damageSpread = 0.15f; // ±15% к урону доставок
    [SerializeField, Range(0f, 0.5f)] float speedSpread = 0.1f;   // ±10% к скорости

    public float DamageMult { get; private set; } = 1f;
    public float SpeedMult { get; private set; } = 1f;

    void Awake()
    {
        DamageMult = Roll(damageSpread);
        SpeedMult = Roll(speedSpread);
    }

    void Start() // HP один раз при спавне; SetMaxHealth сам подведёт текущее
    {
        if (TryGetComponent<Health>(out var hp))
            hp.SetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(hp.Max * Roll(hpSpread))));
    }

    static float Roll(float spread) => 1f + Random.Range(-spread, spread);
}
