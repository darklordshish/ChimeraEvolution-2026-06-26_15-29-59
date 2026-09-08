using UnityEngine;

/// <summary>
/// Доставка «удар рогами»: короткий замах (телеграф) → удар по близкой цели: урон + Knockback (резист Massive внутри
/// Knockback.Push) + Bleed (протыкание, стаки). Наказывает липнущих вплотную — кого таран не достаёт (dist < разбег).
/// Замах прерывается стаггером (базовый Abort, в отличие от закоммиченного тарана).
/// </summary>
public class AntlerAbility : WindupAbility
{
    [Header("Рога")]
    [SerializeField] float range = 2.5f;
    [SerializeField] float halfAngle = 50f;
    // НОЛЬ ЧИТАЕТСЯ КАК «НЕ НАСТРОЕНО» — рецепт проекта против известной гочи: новое [SerializeField]
    // у компонента, УЖЕ лежащего в префабе, приходит нулём (инициализатор применяется только к
    // свежесозданным). Без обёртки лось на старом префабе получил бы конус 0° и не попал бы рогами
    // НИКОГДА — молча, без единой ошибки. Через свойство префаб можно не перегенерировать
    float Cone => halfAngle > 0f ? halfAngle : 50f;
    [SerializeField] int damage = 20; // рога протыкают ощутимо (+ кровь стаками)
    [SerializeField] float knockForce = 9f;
    [SerializeField] int bleedStacks = 2;

    public float Range => range; // психика читает дистанцию удара
    public float HalfAngle => Cone;

    protected override float GizmoRange => range; // хитбокс — ближний радиус рогов
    protected override float GizmoHalfAngle => Cone;

    protected override Color TelegraphColor => TelegraphColors.Antler; // рога — свой цвет (протыкание ≠ таран)

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= Cone;
        if (!(dist <= range && inCone))
        {
            return AbilityRun.Cancelled; // увернулся из зоны/конуса — замах сорван (как BiteAbility:56)
        }
        if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; }
        if (targetHealth != null)
        {
            // единый паёк рогов (см. MeleeBlow) — тот же удар льёт и игрок; мощь NPC масштабирует урон
            var blow = new MeleeBlow { Damage = damage, KnockForce = knockForce, BleedStacks = bleedStacks };
            blow.Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
        }
        return AbilityRun.Done;
    }
}
