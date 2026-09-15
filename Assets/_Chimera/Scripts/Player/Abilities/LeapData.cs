using System;
using UnityEngine;

/// <summary>
/// НАСКОК — запись органа ног (волчьи ноги, тело-хвост змеи): замах → полёт по дуге → укус на приземлении, если цель
/// рядом. Только у NPC (`LeapAbility`): у игрока наскока нет, его рывок — от органа ног напрямую.
/// </summary>
[Serializable]
public class LeapData : AbilityData
{
    public override Type NpcCarrier => typeof(LeapAbility);
    public override Type PlayerCarrier => null;

    [Tooltip("Урон на приземлении. Раскрывается экспрессией, как урон любого приёма (спека 16.09): у природного зверя — доля записи, у игрока растёт с мощью органа.")]
    [Expressed, MustBePositive] public int damage = 12;

    [Tooltip("Ближе этого прыгать незачем, м: психика кусает с места.")]
    [MustBePositive] public float minRange = 5f;

    [Tooltip("Дальний край наскока, м.")]
    [MustBePositive] public float maxRange = 6.5f;

    [Tooltip("Горизонтальная скорость полёта, м/с.")]
    [MustBePositive] public float speed = 13f;

    [Tooltip("Начальная вертикальная скорость, м/с: высота дуги. У змеи низкий бросок, у волка — прыжок.")]
    [MustBePositive] public float up = 5f;

    [Tooltip("Длина полёта, с. Полёт закоммичен: мягкий сбив его не рвёт, жёсткий отлёт — рвёт.")]
    [MustBePositive] public float duration = 0.5f;

    [Tooltip("Радиус укуса на приземлении, м: цель ближе — получает.")]
    [MustBePositive] public float hitRadius = 1.3f;

    [Tooltip("Замах перед прыжком, с. Наведение идёт до последнего кадра замаха.")]
    [LowerIsBetter, MustBePositive] public float windupTime = 0.5f;
}
