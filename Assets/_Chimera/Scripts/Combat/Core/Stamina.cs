using UnityEngine;

/// <summary>
/// СТАМИНА — кор-механика уровня HP и близнец `Health`: бак есть у ВСЕХ существ, различаются только числа
/// (база — от шасси, прибавка и реген — от Сердца; см. `CreatureBody.Recompute`). Тратят её ЛОКОМОЦИЯ и
/// спецрежимы (рывок, таран, прыжок, клубок ежа) — но НЕ атаки: игра «по флоу», а удары уже гейтятся
/// замахом и кулдауном; стамина на них превратила бы бой в бухгалтерию.
///
/// НОЛЬ — НЕ СТЕНКА, А ЦЕНА РИСКА: опустошил бак — «отдышка» (`Exhausted`): пауза перед восстановлением
/// ДЛИННЕЕ обычной и замедление на это время. Поэтому перерасход наказывает открытостью, а не просто
/// запретом кнопки — стамина становится предметом менеджмента, а не индикатором.
/// </summary>
public class Stamina : MonoBehaviour
{
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float regenDelay = 0.5f;      // пауза после ЛЮБОЙ траты: спам не превращается в вечный бег
    [SerializeField] float exhaustRecovery = 1.6f; // ОТДЫШКА: пауза после опустошения — заметно длиннее обычной
    [SerializeField, Range(0.1f, 1f)] float exhaustSlow = 0.6f; // насколько медленнее двигаешься, пока отдышка

    public float Current { get; private set; }
    public float Max => maxStamina;
    public float Normalized => maxStamina > 0f ? Mathf.Clamp01(Current / maxStamina) : 0f;
    public float RegenPerSecond { get; set; }
    public bool Exhausted { get; private set; }

    /// <summary>Множитель скорости от выдоха: 1 в норме, меньше — пока отдышка. Читают локомоции.</summary>
    public float MoveMult => Exhausted ? exhaustSlow : 1f;

    float readyAt;
    Rage rage; // ЯРОСТЬ КАЧАЕТ ДЫХАЛКУ (решение ревью): к «+урон +скорость −защита» добавляется второе дыхание

    void Awake() => Current = maxStamina;

    void Update()
    {
        if (Time.time < readyAt) return;
        Exhausted = false;                       // пауза вышла — отдышался, замедление снято
        if (Current >= maxStamina) return;

        if (rage == null) TryGetComponent(out rage); // ленивая привязка: ярость могут повесить в рантайме
        float rate = RegenPerSecond * (rage != null ? rage.StaminaRegenMult : 1f);
        if (rate <= 0f) return;
        Current = Mathf.Min(maxStamina, Current + rate * Time.deltaTime);
    }

    /// <summary>Хватит ли на действие. На отдышке НЕ хватает ничего — в этом её смысл.</summary>
    public bool Has(float cost) => !Exhausted && Current >= cost;

    /// <summary>Потратить, если хватает. false = действие не состоялось (кнопка не сработала).</summary>
    public bool TrySpend(float cost)
    {
        if (cost <= 0f) return true;
        if (!Has(cost)) return false;

        Current -= cost;
        readyAt = Time.time + regenDelay;
        if (Current <= 0.01f) // выжал досуха — отдышка (длинная пауза + замедление)
        {
            Current = 0f;
            Exhausted = true;
            readyAt = Time.time + exhaustRecovery;
        }
        return true;
    }

    /// <summary>Конструктор меняет бак при смене органа Сердца — разницу даём/забираем у текущего
    /// (как `Health.SetMaxHealth`: снял сердце в бою — не полное восстановление и не мгновенная смерть).</summary>
    public void SetMax(float newMax)
    {
        float delta = newMax - maxStamina;
        maxStamina = Mathf.Max(1f, newMax);
        Current = Mathf.Clamp(Current + delta, 0f, maxStamina);
    }
}
