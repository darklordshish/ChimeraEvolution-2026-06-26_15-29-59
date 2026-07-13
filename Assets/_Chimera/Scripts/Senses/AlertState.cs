using UnityEngine;

/// <summary>
/// Общая машина ВОСПРИЯТИЯ (сенсорный слайс S1): три крупнозернистых состояния —
/// Спокойствие → Настороженность → Атака — единые для ВСЕХ NPC. Психика КАЖДЫЙ кадр скармливает
/// восприятие (`Observe`): есть ли подтверждённая боевая цель и/или зацепка. Машина резолвит состояние
/// с затуханием: потерял цель → ещё держит Настороженность (память), затем Спокойствие. Тонкая тактика
/// («как именно атакую») живёт в психике вида — здесь только УРОВЕНЬ тревоги. Пер-состоянчатая сенсорика
/// (`Senses`) и эффекты читают `State`. Ярость/Страх — НЕ состояния (это боевые эффекты, `Rage`/`Fear`).
/// </summary>
public enum Alert { Calm, Wary, Attack }

public class AlertState : MonoBehaviour
{
    [SerializeField] float waryMemory = 4f;    // держим Настороженность столько секунд после потери зацепки
    [SerializeField] float attackMemory = 2f;  // держим Атаку столько секунд после потери цели

    float waryUntil, attackUntil;

    public Alert State { get; private set; }

    /// <summary>Психика зовёт каждый кадр: target = подтверждённая боевая цель, cue = замеченная зацепка (не цель).</summary>
    public void Observe(bool target, bool cue)
    {
        float t = Time.time;
        if (target) attackUntil = t + attackMemory;
        if (target || cue) waryUntil = t + waryMemory;
        State = t < attackUntil ? Alert.Attack : t < waryUntil ? Alert.Wary : Alert.Calm;
    }
}
