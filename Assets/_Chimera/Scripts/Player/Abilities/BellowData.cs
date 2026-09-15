using System;
using UnityEngine;

/// <summary>
/// РЁВ — запись лосиной Глотки. Детонация на месте, контраст волчьему вою: свои (кины) в радиусе цепи впадают в берсерк,
/// чужих в радиусе ужаса давит по духу. У игрока — `PlayerBellow`, у лося рёв и «теллы» (фырк, топот) решает психика и читает
/// запись у тела. Радиус ужаса — личное пространство лося: им же меряются теллы, поэтому число одно.
/// </summary>
[Serializable]
public class BellowData : AbilityData
{
    public override Type NpcCarrier => null;                 // рёв лося — решение психики, она читает запись у тела
    public override Type PlayerCarrier => typeof(PlayerBellow);

    [Tooltip("Радиус УЖАСА, м: чужие ближе этого получают удар по духу. У лося это личное пространство — граница лесенки и радиус теллов.")]
    [MustBePositive] public float fearRadius = 10f;

    [Tooltip("Радиус ЦЕПИ, м: свои (кины) в нём детонируют берсерком. Рёв туши слышен далеко.")]
    [MustBePositive] public float rallyRadius = 40f;

    [Tooltip("Удар по морали чужих в радиусе ужаса. Рёв туши весит два воя.")]
    [MustBePositive] public float fearMoraleHit = 2f;

    [Tooltip("Перезарядка рёва игрока, с. Как часто ревёт лось, решает психика (bellowCooldown) — это тактика.")]
    [LowerIsBetter, MustBePositive] public float cooldown = 10f;
}
