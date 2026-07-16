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
    [SerializeField] int damage = 12;
    [SerializeField] float knockForce = 9f;
    [SerializeField] int bleedStacks = 2;

    public float Range => range; // психика читает дистанцию удара

    protected override float GizmoRange => range; // хитбокс — ближний радиус рогов
    protected override float GizmoHalfAngle => 50f;

    protected override Color TelegraphColor => TelegraphColors.Antler; // рога — свой цвет (протыкание ≠ таран)

    protected override AbilityRun OnTick()
    {
        if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; }
        if (targetHealth != null && DistToTarget() <= range)
        {
            var h = new Hit(ownHealth, transform.position);
            h.Apply(targetHealth, HitEffect.Damage(Mathf.RoundToInt(damage * DamageMult)));
            h.Apply(targetHealth, HitEffect.Knockback(knockForce));           // резист Massive внутри Knockback.Push
            for (int i = 0; i < bleedStacks; i++) h.Apply(targetHealth, HitEffect.Bleed()); // протыкание — кровь стаками
        }
        return AbilityRun.Done;
    }
}
