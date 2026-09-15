using System;
using UnityEngine;

/// <summary>
/// РОГА — запись органа «Рога» (придаток лося; у человека — химерный слот). Одна запись кормит удар рогами NPC
/// (`AntlerAbility`) и игрока (`PlayerAntler`): фронтальный конус, урон + отлёт + кровотечение.
/// </summary>
[Serializable]
public class AntlerData : AbilityData
{
    public override Type NpcCarrier => typeof(AntlerAbility);
    public override Type PlayerCarrier => typeof(PlayerAntler);

    [Header("Сила")]
    [Tooltip("Урон удара рогами. Как есть: экспрессией не раскрывается (так рога били до переезда; выровнять с уроном органа — решение дизайна).")]
    [MustBePositive] public int damage = 12;

    [Tooltip("Отлёт цели. Массивная цель резистит сама (Knockback).")]
    [Min(0f)] public float knockForce = 9f;

    [Tooltip("Стаков кровотечения за попадание: рога протыкают.")]
    [Min(0)] public int bleedStacks = 2;

    [Header("Форма удара")]
    [Tooltip("Досягаемость рогов, м: расстояние между центрами на плоскости. Длиннее укуса — рога тянутся.")]
    [MustBePositive] public float range = 2.5f;

    [Tooltip("Полуугол конуса перед мордой, градусы.")]
    [MustBePositive] public float halfAngle = 50f;

    [Tooltip("Замах NPC, с: телеграф для уворота. У игрока замаха нет — нажатие и есть решение.")]
    [LowerIsBetter, MustBePositive] public float windupTime = 0.35f;

    [Tooltip("Перезарядка рогов игрока, с. Как часто бодает NPC, решает психика (MoosePsyche.antlerCooldown) — это тактика, а не число приёма.")]
    [LowerIsBetter, MustBePositive] public float cooldown = 1.2f;
}
