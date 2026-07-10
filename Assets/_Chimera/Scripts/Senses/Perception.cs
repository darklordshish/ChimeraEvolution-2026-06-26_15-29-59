using UnityEngine;

/// <summary>
/// Восприятие игрока (зерно GDD-принципа «камера-от-восприятия») + хелперы чувств.
/// `WolfScent` — надето волчье Чутьё (видно запах). `ShowOwnScent` — показывать ли СВОЙ след.
/// `HasLineOfSight` — общий луч прямой видимости (для зрения врагов: стена рвёт видимость → тропят по запаху).
/// </summary>
public static class Perception
{
    public static bool WolfScent;            // надето волчье Чутьё → видно запах
    public static bool ShowOwnScent = true;  // показывать свой запаховый след (тоггл)

    public static bool SnakeThermal;         // надет Пит-орган → термозрение игрока (тепло сквозь стены)
    public static float ThermalRange;        // радиус термо игрока (из органа)
    public static bool DevThermal;           // dev-тоггл T: форс термо без органа (леса до настоящего UI)
    public static bool ThermalOn => SnakeThermal || DevThermal;
    public static float ThermalRadius => SnakeThermal && ThermalRange > 0f ? ThermalRange : 14f; // dev-дефолт = радиус змеи

    // «тёплый» = живой (есть Health) и НЕ холоднокровный — единый источник правды для термо:
    // им пользуются и ИИ змеи (SeesThermal), и визуал игрока (HeatSignature). Расхождение = баг.
    public static bool IsWarm(Transform t) =>
        t != null && t.GetComponent<Health>() != null && t.GetComponent<ColdBlooded>() == null;

    // прямая видимость от точки до цели: стена между = нет. Вблизи считаем, что видно.
    public static bool HasLineOfSight(Vector3 from, Transform target)
    {
        Vector3 to = target.position + Vector3.up * 0.6f;
        Vector3 eye = from + Vector3.up * 1.0f;
        Vector3 delta = to - eye;
        float dist = delta.magnitude;
        if (dist < 1.6f) return true;
        Vector3 dir = delta / dist;
        // стартуем чуть впереди, чтобы не зацепить свой коллайдер; первым делом ловим стену перед целью
        if (Physics.Raycast(eye + dir * 0.9f, dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.transform == target;
        return true;
    }

    // термозрение (Пит-орган): видит ТЁПЛОГО в радиусе СКВОЗЬ укрытия — прямой видимости не требует,
    // этим и отличается от зрения. Холоднокровный не излучает тепло — невидим (см. IsWarm).
    public static bool SeesThermal(Vector3 from, Transform target, float range)
    {
        if (target == null) return false;
        if ((target.position - from).sqrMagnitude > range * range) return false;
        return IsWarm(target);
    }
}
