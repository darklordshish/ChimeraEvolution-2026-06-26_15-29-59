using UnityEngine;

/// <summary>
/// Доставка «прыжок-наскок»: замах (хоуминг до последнего кадра) → полёт по дуге → укус на
/// приземлении, если цель рядом (с приземления можно увернуться). Замах отменяется стаггером;
/// ПОЛЁТ закоммичен — мягкий срыв игнорит, жёсткий (нокбэк) рвёт.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА НОГ (<see cref="LeapData"/>, спека «данные в органах»): нет записи — наскока нет.
/// </summary>
public class LeapAbility : WindupAbility, IOrganAbility
{
    LeapData data; // раскрытая запись наскока; null — наскока нет

    public System.Type DataType => typeof(LeapData);
    public void Configure(AbilityData d) => data = d as LeapData;
    public bool Available => data != null;
    protected override bool Ready => data != null;
    protected override float WindupTime => data.windupTime;

    // психика читает окно дистанций; нет наскока — пустое окно [0, 0]
    public float MinRange => data != null ? data.minRange : 0f;
    public float MaxRange => data != null ? data.maxRange : 0f;
    public override float WindowMin => MinRange;            // окно арсенала: с минимальной дистанции записи
    public override float WindowMax => MaxRange;

    protected override float GizmoRange => MaxRange; // хитбокс — дальность наскока
    protected override float GizmoHalfAngle => 20f;

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
            flightEnd = Time.time + data.duration;
            vel = DirToTarget() * data.speed + Vector3.up * data.up;
        }

        vel.y += gravity * Time.deltaTime;
        controller.Move(vel * Time.deltaTime);
        if (Time.time < flightEnd) return AbilityRun.Running;

        flying = false;
        if (targetHealth != null && DistToTarget() <= data.hitRadius) // приземлили наскок — кусаем
        {
            // единый паёк (см. MeleeBlow) — тот же укус на приземлении; мощь масштабирует урон
            var blow = new MeleeBlow { Damage = data.damage, LifeSteal = BossLifeSteal };
            blow.Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
        }
        return AbilityRun.Done;
    }

    // полёт закоммичен: стаггер (мягкий срыв) не рвёт; нокбэк (hard) рвёт
    public override void Abort(bool hard)
    {
        if (flying && !hard) return;
        flying = false;
        base.Abort(hard);
    }
}
