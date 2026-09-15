using System;
using UnityEngine;

/// <summary>
/// ТАРАН — запись органа «Лосиные ноги». Одна запись кормит таран NPC (`ChargeAbility`: замах → закоммиченный разбег →
/// удар, топот, пропашка) и таран игрока (`PlayerCharge`: рывок становится горящим). Грань игрока читает только блок
/// «Удар»: разбег у игрока — это рывок от органа ног, а топота и пропашки у рывка нет.
/// </summary>
[Serializable]
public class ChargeData : AbilityData
{
    public override Type NpcCarrier => typeof(ChargeAbility);
    public override Type PlayerCarrier => typeof(PlayerCharge);

    [Header("Удар — читают NPC и игрок")]
    [Tooltip("Базовый урон тарана. Как есть: экспрессией не раскрывается (так таран бил до переезда).")]
    [MustBePositive] public int damage = 22;

    [Tooltip("ФИЗИКА РАЗГОНА: +урон за каждый метр разбега. Скручен до 1.0, чтобы длинная прямая не делала ваншотом.")]
    [Min(0f)] public float damagePerMeter = 1f;

    [Tooltip("Отлёт цели. Массивная цель резистит сама (Knockback).")]
    [Min(0f)] public float knockForce = 12f;

    [Tooltip("ТАРАН-ПО-МАССЕ: доля отлёта, если таранящий сам НЕ массивен. Снести с ног нужна масса — игрок на человечьем шасси толкает слабо.")]
    [Range(0f, 1f)] public float lightChargeMult = 0.25f;

    [Tooltip("Сбив цели при попадании, с.")]
    [Min(0f)] public float staggerTime = 0.5f;

    [Tooltip("Радиус удара, м: цель, чей центр ближе этого к таранящему (на плоскости), получает удар.")]
    [MustBePositive] public float hitRadius = 1.8f;

    [Header("Разбег — читает NPC")]
    [Tooltip("Ближе этого не разогнаться, м: вплотную психика бьёт рогами или копытом.")]
    [MustBePositive] public float minRange = 4f;

    [Tooltip("Дальний завод тарана, м: догоняет убегающего волка по прямой.")]
    [MustBePositive] public float maxRange = 18f;

    [Tooltip("Скорость разбега, м/с. Быстрее волчьего рывка: от тарана не убежать, только уворот вбок.")]
    [MustBePositive] public float chargeSpeed = 35f;

    [Tooltip("Длина разбега, с. ПОДРЕЗАНА НА ТРЕТЬ 08.09, по игре: при 1.1 с (≈38 м с завода 18 м) зверь двадцать метров ехал уже за целью — таран читался «длинным», а не «мощным», и промах наказывал того, кто рядом стоял. 0.73 с ≈ 26 м: добегает с 18 м и проскакивает ещё восемь — хватает на инерцию туши и топот.")]
    [MustBePositive] public float duration = 0.73f;

    [Tooltip("Замах перед разбегом, с. Направление фиксируется в последний кадр замаха — отсюда уворот вбок.")]
    [LowerIsBetter, MustBePositive] public float windupTime = 0.5f;

    [Header("Топот и пропашка — читает NPC")]
    [Tooltip("Радиус топота-приземления в конце тарана, м: разгоняет скопления.")]
    [Min(0f)] public float stompRadius = 4f;

    [Tooltip("Сбив от топота, с.")]
    [Min(0f)] public float stompStagger = 0.4f;

    [Tooltip("Радиальный толчок топота у эпицентра — «землетрясение», спад к краю.")]
    [Min(0f)] public float stompForce = 13f;

    [Tooltip("Снос попавшихся на пути разгона. Только массивный таранящий.")]
    [Min(0f)] public float plowForce = 6f;

    [Tooltip("Ширина пропашки, м: кого задевает туша на ходу.")]
    [Min(0f)] public float plowRadius = 1.6f;
}
