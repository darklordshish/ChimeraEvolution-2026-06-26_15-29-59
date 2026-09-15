using System;
using UnityEngine;

/// <summary>
/// БОЕВОЙ КЛИЧ — запись человечьего Рта: единственный голос, направленный НА СЕБЯ. Кричишь — рвёшь себе горло (стак крови) и
/// входишь в ярость, тем злее, чем больше на тебе крови; держится, пока идёт кровотечение. Гейта по шасси нет: человечий Рот
/// кричит на любом теле — цена (своя кровь) и так не даёт этим злоупотреблять. Только у игрока (`PlayerScream`).
/// </summary>
[Serializable]
public class ScreamData : AbilityData
{
    public override Type NpcCarrier => null;
    public override Type PlayerCarrier => typeof(PlayerScream);

    [Tooltip("Перезарядка клича, с.")]
    [LowerIsBetter, MustBePositive] public float cooldown = 12f;

    [Tooltip("Прибавка к ярости за каждый стак крови на себе: 0.12 = +12%.")]
    [MustBePositive] public float boostPerStack = 0.12f;

    [Tooltip("Потолок усиления ярости. Кровопотеря сама себя ограничивает.")]
    [MustBePositive] public float maxBoost = 2f;
}
