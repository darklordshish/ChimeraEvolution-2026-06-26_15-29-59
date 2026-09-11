using UnityEngine;

/// <summary>
/// Индивидуальный разброс особи (Ф6): при спавне ОДИН раз катает множители HP/урона/скорости —
/// стая из «30±5» вместо клонов. Вешает ТЕЛО каждому NPC (`CreatureBody.Awake`, как `Personality`) — любому
/// виду и химере, какая бы психика ни встала; босса делает штучным `Bossness`, игроку не вешается.
/// Потребители множителей — те же точки, что у Rage: доставки (урон), психика (скорость); HP применяется сам.
/// </summary>
public class SpawnVariance : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)] float hpSpread = 0.15f;     // ±15% к макс. HP
    [SerializeField, Range(0f, 0.5f)] float damageSpread = 0.15f; // ±15% к урону доставок
    [SerializeField, Range(0f, 0.5f)] float speedSpread = 0.1f;   // ±10% к скорости

    public float DamageMult { get; private set; } = 1f;
    public float SpeedMult { get; private set; } = 1f;
    public float HpMult { get; private set; } = 1f;

    void Awake()
    {
        DamageMult = Roll(damageSpread);
        SpeedMult = Roll(speedSpread);
        HpMult = Roll(hpSpread);
    }

    // HP: с телом-на-шасси (CreatureBody) разброс учитывает само тело при раздаче витальности
    // (иначе гонка Start'ов); без тела — применяем сами один раз.
    void Start()
    {
        if (GetComponent<CreatureBody>() == null && TryGetComponent<Health>(out var hp))
            hp.SetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(hp.Max * HpMult)));
    }

    /// <summary>ШТУЧНАЯ ОСОБЬ — разброс снят. Нужна боссу: тело вешает разброс каждому NPC, а босс, как и игрок, —
    /// детерминированная база. Зовёт `Bossness`; HP после этого пересчитывает тело.</summary>
    public void MakeUnique() { DamageMult = 1f; SpeedMult = 1f; HpMult = 1f; }

    static float Roll(float spread) => 1f + Random.Range(-spread, spread);
}
