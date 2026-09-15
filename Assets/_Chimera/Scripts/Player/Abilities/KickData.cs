using System;
using UnityEngine;

/// <summary>
/// ПИНОК — запись человечьих ног: оттолкнуть врагов перед собой + лёгкий урон (через него идут вспышка и сбив).
/// Инструмент создания пространства под кайтинг. Только у игрока (`PlayerKick`, кнопка E): двойника у NPC нет,
/// поэтому форма пинка — сфера на выносе — сохранена как есть, сводить её не с чем.
/// </summary>
[Serializable]
public class KickData : AbilityData
{
    public override Type NpcCarrier => null;
    public override Type PlayerCarrier => typeof(PlayerKick);

    [Tooltip("Урон пинка: лёгкий, главное — толчок.")]
    [Expressed, MustBePositive] public int damage = 4;

    [Tooltip("Сила отлёта от пинка.")]
    [Min(0f)] public float knockForce = 12f;

    [Tooltip("Вынос центра сферы вперёд, м: нога бьёт перед собой.")]
    [MustBePositive] public float reach = 1.8f;

    [Tooltip("Радиус сферы пинка, м. Широкий — толкаем клин стаи.")]
    [MustBePositive] public float radius = 1.6f;

    [Tooltip("Перезарядка пинка, с.")]
    [LowerIsBetter, MustBePositive] public float cooldown = 1f;
}
