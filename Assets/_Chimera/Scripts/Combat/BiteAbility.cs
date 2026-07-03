using UnityEngine;

/// <summary>
/// Доставка «укус»: фронтальный конус с замахом; отменяется уворотом из зоны/конуса (и стаггером).
/// Эффекты на попадании — урон + опц. вампиризм + опц. сбив регена (словарь Hit).
/// Дефолты = волк; вервольф ставит свои числа на префабе (вампиризм). Одна доставка — разные носители.
/// </summary>
public class BiteAbility : WindupAbility
{
    [Header("Укус")]
    [SerializeField] float range = 2.0f;
    [SerializeField] float halfAngle = 55f;
    [SerializeField] int damage = 8;
    [SerializeField] int lifeSteal = 0;                      // вервольф лечится укусом (может уйти в temp HP)
    [SerializeField, Range(0f, 1f)] float regenDebuff = 1f;  // <1 — сбивает реген цели
    [SerializeField] float regenDebuffTime = 0f;
    [SerializeField] int venomStacks = 0;                   // >0 — укус впрыскивает яд (змея)

    public float Range => range;         // психика читает для решений (дистанция атаки/удержания)
    public float HalfAngle => halfAngle; // и для прицельного конуса

    // тело-на-шасси (CreatureBody, NPC-режим) кормит урон из органов
    public void SetDamage(int v) => damage = v;

    protected override Color TelegraphColor => TelegraphColors.Bite;

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= halfAngle;
        if (!(dist <= range && inCone)) return AbilityRun.Cancelled; // увернулся — замах сорван

        if (Time.time >= windupEnd)
        {
            var hit = new Hit(ownHealth, transform.position);
            hit.Apply(targetHealth, HitEffect.Damage(Mathf.RoundToInt(damage * DamageMult)));
            if (lifeSteal > 0) hit.Apply(targetHealth, HitEffect.LifeSteal(lifeSteal));
            if (regenDebuff < 1f) hit.Apply(targetHealth, HitEffect.RegenDebuff(regenDebuff, regenDebuffTime));
            for (int i = 0; i < venomStacks; i++) hit.Apply(targetHealth, HitEffect.Venom());
            return AbilityRun.Done;
        }

        SettleInPlace();
        return AbilityRun.Running;
    }
}
