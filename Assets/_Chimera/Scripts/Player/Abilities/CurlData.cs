using System;
using UnityEngine;

/// <summary>
/// КЛУБОК — запись органа «Ежиные ноги»: свернуться в шар (броня, цена выносливостью) и катиться под давлением.
/// ДОМАШНИЙ ПРИЁМ: открыт только на родном шасси органа (ежином). Перекат и клубок — одна способность ежиных ног на
/// двух глубинах: перекат сквозь (`RollData`) открыт всем, полный шар с бронёй и катанием — только дома.
/// Урон проката берётся из `RollData` того же органа, здесь его нет — одно число живёт в одном месте.
/// </summary>
[Serializable]
public class CurlData : AbilityData
{
    public override Type NpcCarrier => typeof(CurlDefense);
    public override Type PlayerCarrier => typeof(CurlDefense); // игроку тоже — если он на ежином шасси
    public override bool NativeOnly => true;

    [Header("Клубок")]
    [Tooltip("Броня в клубке (доля урона, которую гасит). Берётся максимум с базовой бронёй, не сумма.")]
    [Range(0f, 0.9f), MustBePositive] public float curlArmor = 0.6f;

    [Tooltip("Расход выносливости в секунду, пока свёрнут. Быстро — иначе катание не успевает проявиться.")]
    [MustBePositive] public float staminaDrain = 30f;

    [Header("Катание (продавили клубок)")]
    [Tooltip("Скорость катящегося шара, м/с.")]
    [MustBePositive] public float rollSpeed = 9f;

    [Tooltip("Расход выносливости в катании, в секунду. Быстрее клубка: прорыв дорог, потому и последний рывок.")]
    [MustBePositive] public float rollDrain = 40f;

    [Tooltip("Доворот шара к цели, градусы в секунду. МАЛОУПРАВЛЯЕМО: медленно, но не строго по прямой.")]
    [MustBePositive] public float rollTurnSpeed = 90f;

    [Tooltip("Прижатие к земле в катании: controller двигается напрямую, без гравитации контроллера.")]
    [MustBePositive] public float rollGravity = 20f;
}
