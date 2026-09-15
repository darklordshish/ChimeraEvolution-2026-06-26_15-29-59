using System;
using UnityEngine;

/// <summary>
/// УДАР КОНЕЧНОСТЬЮ — запись органа слота «Руки» (кисть, коготь, копыто). Одна запись кормит удар NPC (`LimbStrikeAbility`) и
/// удар игрока (`PlayerAttack`, левая кнопка). Слот «Руки» держит РОЛЬ, а не анатомию: у волка там оружие, у лося
/// опора-оружие, у человека манипулятор. Темпа атак здесь нет: темп — свойство тела от Сердца, модификатор к удару.
/// </summary>
[Serializable]
public class LimbStrikeData : AbilityData
{
    public override Type NpcCarrier => typeof(LimbStrikeAbility);
    public override Type PlayerCarrier => typeof(PlayerAttack);

    [Header("Сила")]
    [Tooltip("Урон удара конечностью. Раскрывается экспрессией, как любой урон органа (так урон конечности считался и до переезда).")]
    [Expressed, MustBePositive] public int damage = 10;

    [Tooltip("Толчок цели. Толчок, а не отлёт: сшибает с ног таран, не копыто. У кисти и когтя 0.")]
    [Min(0f)] public float knockForce;

    [Tooltip("Стаков кровотечения за попадание: рассечение — по виду.")]
    [Min(0)] public int bleedStacks;

    [Header("Форма удара")]
    [Tooltip("Досягаемость конечности, м: расстояние между центрами на плоскости.")]
    [MustBePositive] public float range = 1.6f;

    [Tooltip("Полуугол конуса, градусы. Шире рогов: конечностью машут, а не целятся корпусом.")]
    [MustBePositive] public float halfAngle = 60f;

    [Tooltip("Замах NPC, с: телеграф для уворота. У игрока замаха нет. Как часто бьёт NPC, решает психика (hoofCooldown).")]
    [LowerIsBetter, MustBePositive] public float windupTime = 0.45f;
}
