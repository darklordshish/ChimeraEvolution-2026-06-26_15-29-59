using System;
using UnityEngine;

/// <summary>
/// ПЕРЕКАТ СКВОЗЬ — запись органа «Ежиные ноги»: кого прокатил насквозь, тот режется. Одна запись кормит перекат
/// игрока на рывке (`PlayerRoll`) и прокат шара ежа в катании (`CurlDefense` читает её у тела). Геометрия одна:
/// сфера впереди на свой радиус. У NPC своего носителя у переката нет — катится машина клубка.
/// </summary>
[Serializable]
public class RollData : AbilityData
{
    public override Type NpcCarrier => null;                // NPC катится машиной клубка, она берёт эту запись у тела
    public override Type PlayerCarrier => typeof(PlayerRoll);

    [Tooltip("Урон тому, кого прокатил. Раз за рывок / прокат.")]
    [MustBePositive] public int damage = 10;

    [Tooltip("Стаков кровотечения: иглы протыкают.")]
    [Min(0)] public int bleedStacks = 1;

    [Tooltip("Толчок прокатом. Слабее тарана: катишься сквозь, а не сносишь.")]
    [Min(0f)] public float knockForce = 8f;

    [Tooltip("Радиус шара, м: сфера удара стоит впереди на этот же радиус.")]
    [MustBePositive] public float radius = 1.2f;
}
