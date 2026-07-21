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

    // DEV-ПРИЗРАК: чувства NPC игрока НЕ воспринимают (зрение/термо/запах), модель обычная — наблюдение
    // за психикой «в естественной среде». Атака игрока РАСКРЫВАЕТ (BreakGhost) — дальше всё натурально;
    // повторное включение (Dev-панель) заново прячет и сбрасывает интерес NPC.
    public static bool PlayerGhost;
    public static void BreakGhost() { PlayerGhost = false; }
    static bool GhostHides(Transform target) =>
        PlayerGhost && target != null && target.GetComponent<PlayerController>() != null;

    public static bool SnakeThermal;         // надет Пит-орган → термозрение игрока (тепло сквозь стены)
    public static float ThermalRange;        // радиус термо игрока (из органа)
    public static bool DevThermal;           // dev-тоггл T: форс термо без органа (леса до настоящего UI)
    public static bool ThermalOn => SnakeThermal || DevThermal;
    public static float ThermalRadius => SnakeThermal && ThermalRange > 0f ? ThermalRange : 14f; // dev-дефолт = радиус змеи

    // «тёплый» = живой (есть Health) и НЕ холоднокровный — единый источник правды для термо:
    // им пользуются и ИИ змеи (SeesThermal + прямые опросы CrowdNear/IsLonely/NearestWarm), и визуал игрока
    // (HeatSignature). Расхождение = баг. Dev-призрак тепла НЕ излучает — иначе змея кралась бы тыкаться в него.
    public static bool IsWarm(Transform t) =>
        t != null && !GhostHides(t) && t.GetComponent<Health>() != null && t.GetComponent<ColdBlooded>() == null;

    // прямая видимость от точки до цели: стена между = нет. Вблизи считаем, что видно.
    public static bool HasLineOfSight(Vector3 from, Transform target)
    {
        if (GhostHides(target)) return false; // dev-призрак невидим для зрения
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

    /// <summary>ПРОФИЛЬ ЧУВСТВ ИГРОКА — ставит его собственное тело (`CreatureBody`), каналы задаёт сборка.
    /// Раньше у игрока чувств не было вовсе, только статические флаги: теперь он такое же существо, как NPC.</summary>
    public static Senses PlayerSenses;

    /// <summary>ВОСПРИНИМАЕТ ЛИ ИГРОК цель — и каким каналом. Единственный источник правды для всего,
    /// что показывает состояние мира: полоски, сканер, сводка. Кого не чувствуешь — того на экране нет,
    /// поэтому засада остаётся засадой, а сборка меняет не только силу, но и картину мира.
    /// Порядок проверки — от самого «прямого» канала к косвенным: увидел глазами / почуял тепло / унюхал.</summary>
    public static bool PlayerPerceives(Vector3 from, Transform target, out SenseKind by)
    {
        by = SenseKind.Sight;
        if (target == null || PlayerSenses == null) return false;

        float distSq = (target.position - from).sqrMagnitude;

        float sight = PlayerSenses.Range(SenseKind.Sight);
        if (sight > 0f && distSq <= sight * sight && HasLineOfSight(from, target)) return true;

        // термо берём через ThermalOn/ThermalRadius — там уже сведены орган и dev-тумблер T (один источник правды)
        if (ThermalOn && SeesThermal(from, target, ThermalRadius)) { by = SenseKind.Thermal; return true; } // сквозь стены

        // ЗАПАХ прямой видимости не требует (тем и ценен): за углом чуешь, глазами не видишь
        float scent = PlayerSenses.Range(SenseKind.Scent);
        if (scent > 0f && distSq <= scent * scent) { by = SenseKind.Scent; return true; }

        return false;
    }

    // термозрение (Пит-орган): видит ТЁПЛОГО в радиусе СКВОЗЬ укрытия — прямой видимости не требует,
    // этим и отличается от зрения. Холоднокровный не излучает тепло — невидим (см. IsWarm).
    public static bool SeesThermal(Vector3 from, Transform target, float range)
    {
        if (target == null) return false;
        if (GhostHides(target)) return false; // dev-призрак не излучает и тепла
        if ((target.position - from).sqrMagnitude > range * range) return false;
        return IsWarm(target);
    }
}
