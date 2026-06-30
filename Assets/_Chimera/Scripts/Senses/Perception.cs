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
}
