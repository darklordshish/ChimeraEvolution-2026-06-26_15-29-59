using System;
using UnityEngine;

/// <summary>
/// УКУС — запись органа слота «Пасть». Одна запись кормит укус NPC (`BiteAbility`) и укус игрока (`PlayerBite`):
/// волчьей Пастью игрок кусает как волк. Числа по видам — в `SpeciesBootstrap` у органа.
/// </summary>
[Serializable]
public class BiteData : AbilityData
{
    public override Type NpcCarrier => typeof(BiteAbility);
    public override Type PlayerCarrier => typeof(PlayerBite);

    [Header("Сила")]
    [Tooltip("Урон укуса. Раскрывается экспрессией, как любой урон органа: у природного NPC × экспрессия вида.")]
    [Expressed, MustBePositive] public int damage = 10;

    [Tooltip("Стаков кровотечения за попадание (волчьи клыки).")]
    [Min(0)] public int bleedStacks;

    [Tooltip("Стаков яда за попадание (змеиные клыки).")]
    [Min(0)] public int venomStacks;

    [Tooltip("Множитель регенерации цели после укуса: 0.5 — реген вдвое слабее, 1 — не сбивает.")]
    [LowerIsBetter, Range(0f, 1f)] public float regenDebuff = 1f;

    [Tooltip("Сколько секунд держится сбив регенерации.")]
    [Min(0f)] public float regenDebuffTime;

    [Header("Форма удара")]
    [Tooltip("Досягаемость пасти, м: расстояние между центрами на плоскости.")]
    [MustBePositive] public float range = 2f;

    [Tooltip("Полуугол конуса перед мордой, градусы.")]
    [MustBePositive] public float halfAngle = 55f;

    [Tooltip("Замах NPC, с: телеграф, по которому игрок уворачивается. У игрока замаха нет — нажатие и есть решение.")]
    [LowerIsBetter, MustBePositive] public float windupTime = 0.45f;

    [Tooltip("Перезарядка укуса игрока, с. Ритм атак NPC держит психика — это её тактика, а не число приёма.")]
    [LowerIsBetter, MustBePositive] public float cooldown = 0.7f;
}
