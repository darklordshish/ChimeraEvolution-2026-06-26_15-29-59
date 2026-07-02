using UnityEngine;

/// <summary>
/// Доставка «прыжок-наскок»: замах (хоуминг до последнего кадра) → полёт по дуге → укус на
/// приземлении, если цель рядом (с приземления можно увернуться). Замах отменяется стаггером;
/// ПОЛЁТ закоммичен — мягкий срыв игнорит, жёсткий (нокбэк) рвёт. Дефолты = волк.
/// </summary>
public class LeapAbility : WindupAbility
{
    [Header("Прыжок")]
    [SerializeField] float minRange = 5.0f;
    [SerializeField] float maxRange = 6.5f;
    [SerializeField] float speed = 13f;
    [SerializeField] float up = 5f;
    [SerializeField] float duration = 0.5f;
    [SerializeField] int damage = 12;
    [SerializeField] int lifeSteal = 0;   // вервольф лечится и наскоком
    [SerializeField] float hitRadius = 1.3f;

    public float MinRange => minRange; // психика читает окно дистанций для решения
    public float MaxRange => maxRange;

    bool flying;
    float flightEnd;
    Vector3 vel;

    protected override Color TelegraphColor => TelegraphColors.Leap;

    protected override AbilityRun OnTick()
    {
        if (!flying)
        {
            if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; }
            flying = true;              // взлёт: направление берём в последний кадр замаха
            telegraph.Clear();
            flightEnd = Time.time + duration;
            vel = DirToTarget() * speed + Vector3.up * up;
        }

        vel.y += gravity * Time.deltaTime;
        controller.Move(vel * Time.deltaTime);
        if (Time.time < flightEnd) return AbilityRun.Running;

        flying = false;
        if (targetHealth != null && DistToTarget() <= hitRadius) // приземлили наскок — кусаем
        {
            var hit = new Hit(ownHealth, transform.position);
            hit.Apply(targetHealth, HitEffect.Damage(Mathf.RoundToInt(damage * DamageMult)));
            if (lifeSteal > 0) hit.Apply(targetHealth, HitEffect.LifeSteal(lifeSteal));
        }
        return AbilityRun.Done;
    }

    /// <summary>Мгновенный наскок без замаха: чардж-разбег уже был телеграфом. Взлёт на первом же тике.</summary>
    public bool TryPounceNow()
    {
        if (!TryUse()) return false;
        windupEnd = Time.time;
        return true;
    }

    // полёт закоммичен: стаггер (мягкий срыв) не рвёт; нокбэк (hard) рвёт
    public override void Abort(bool hard)
    {
        if (flying && !hard) return;
        flying = false;
        base.Abort(hard);
    }
}
