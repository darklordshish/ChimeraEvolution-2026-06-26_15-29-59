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

    /// <summary>ЧУТЬЁ УЧЁНОГО (человеческое Чутьё) — наблюдательность: игрок РАСПОЗНАЁТ намерение по замаху
    /// (цвет приёма вместо безымянного «что-то готовит») и читает состояния ЧИСЛОМ. Ставит тело игрока.
    /// Граница проекта: качественное видно всем безусловно, количественное — сила аналитика.</summary>
    public static bool Insight;

    /// <summary>ОСТРЫЙ СЛУХ (лосиное Ухо): вдвое дальше + различение ВИДА источника + ВОЛНЫ ЗВУКА на экране.
    /// Отображение сенсорики принадлежит органу этой сенсорики: волны видит тот, у кого есть ухо.
    /// Разделение с Чутьём: слух отвечает «КТО шумит», Чутьё — «ЧТО происходит».</summary>
    public static bool KeenHearing;

    public static bool SnakeThermal;         // надет Пит-орган → термозрение игрока (тепло сквозь стены)
    public static float ThermalRange;        // радиус термо игрока (из органа)
    public static bool DevThermal;           // dev-тоггл T: форс термо без органа (леса до настоящего UI)

    /// <summary>DEV-ТИШИНА: игрок не издаёт звука вовсе. Отдельно от призрака — чтобы различать, ЧЕМ тебя
    /// засекли: выключил звук и зверь всё равно идёт → дело не в слухе (берсерк, память, другой канал).</summary>
    public static bool DevSilent;
    public static bool ThermalOn => SnakeThermal || DevThermal;
    public static float ThermalRadius => SnakeThermal && ThermalRange > 0f ? ThermalRange : 14f; // dev-дефолт = радиус змеи

    // «тёплый» = живой (есть Health) и НЕ холоднокровный — единый источник правды для термо:
    // им пользуются и ИИ змеи (SeesThermal + прямые опросы CrowdNear/IsLonely/NearestWarm), и визуал игрока
    // (HeatSignature). Расхождение = баг. Dev-призрак тепла НЕ излучает — иначе змея кралась бы тыкаться в него.
    public static bool IsWarm(Transform t) =>
        t != null && !GhostHides(t) && t.GetComponent<Health>() != null && t.GetComponent<ColdBlooded>() == null;

    /// <summary>Прямая видимость от точки до цели: стена между = нет. Вблизи считаем, что видно.
    /// `observer` — чей это взгляд: его собственный коллайдер пропускаем.
    /// БЫЛО: луч стартовал с отступом 0.9 м, чтобы не задеть себя, — и проскакивал стену, в которую
    /// наблюдатель упёрся вплотную (за ней «видно» всё). Теперь стреляем от глаза и перешагиваем
    /// ровно себя, а не всё в радиусе метра.</summary>
    public static bool HasLineOfSight(Vector3 from, Transform target, Transform observer = null)
    {
        if (GhostHides(target)) return false; // dev-призрак невидим для зрения
        Vector3 to = target.position + Vector3.up * 0.6f;
        Vector3 eye = from + Vector3.up * 1.0f;
        Vector3 delta = to - eye;
        float dist = delta.magnitude;
        if (dist < 1.6f) return true;
        Vector3 dir = delta / dist;

        if (!Physics.Raycast(eye, dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore)) return true;
        if (IsPartOf(hit.collider.transform, target)) return true;   // первым попалась цель — видно

        if (observer != null && IsPartOf(hit.collider.transform, observer))
        {
            // упёрлись в себя — перешагиваем СВОЙ коллайдер и смотрим, что дальше
            float skip = hit.distance + 0.05f;
            if (skip >= dist) return true;
            if (!Physics.Raycast(eye + dir * skip, dir, out hit, dist - skip, ~0, QueryTriggerInteraction.Ignore)) return true;
            return IsPartOf(hit.collider.transform, target);
        }
        return false; // что-то загородило
    }

    static bool IsPartOf(Transform t, Transform root) => t == root || t.IsChildOf(root);

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

        // ЗРЕНИЕ: в кадре + не загорожено + цель не растворилась. КАМУФЛЯЖ (Чешуя змеи) глаза обманывает —
        // но он сам гаснет, когда змея гремит манком или бьёт (телеграф раскрывает), так что «вижу» честно
        // мигает в такт погремушке, а между вспышками остаётся только след
        float sight = PlayerSenses.Range(SenseKind.Sight);
        bool veiled = target.TryGetComponent<Camouflage>(out var camo) && camo.Hidden;
        if (sight > 0f && !veiled && distSq <= sight * sight
            && InFrame(target) && HasLineOfSight(from, target, PlayerSenses.transform)) return true;

        // термо берём через ThermalOn/ThermalRadius — там уже сведены орган и dev-тумблер T (один источник правды)
        if (ThermalOn && SeesThermal(from, target, ThermalRadius)) { by = SenseKind.Thermal; return true; } // сквозь стены

        // СЛУХ ловит не существо, а СОБЫТИЕ: шумит — слышно, замер — беззвучен. Слышимая дальность =
        // громкость × радиус уха (та же формула, что у NPC в Noise.Hear). Отсюда манок змеи работает и на
        // игрока: невидимая в камуфляже, она ГРЕМИТ — и попадает в сводку звуком, не показываясь
        float hearing = PlayerSenses.Range(SenseKind.Hearing);
        if (hearing > 0f && target.TryGetComponent<Noise>(out var noise))
        {
            float audible = noise.Loudness * hearing;
            if (audible > 0.01f && distSq <= audible * audible) { by = SenseKind.Hearing; return true; }
        }

        // ЗАПАХ прямой видимости не требует (тем и ценен): за углом чуешь, глазами не видишь
        float scent = PlayerSenses.Range(SenseKind.Scent);
        if (scent > 0f && distSq <= scent * scent) { by = SenseKind.Scent; return true; }

        return false;
    }

    /// <summary>ЗРЕНИЕ = ТО, ЧТО ПОПАЛО В КАДР. Направление взгляда решает: упёрся в стену — волков за спиной
    /// не видишь, хотя они в двух шагах. Именно этим зрение отличается от прочих каналов: тепло, запах и слух
    /// направления не требуют, потому и ценны — они закрывают ровно ту зону, которую глаза не держат.</summary>
    static bool InFrame(Transform target)
    {
        var cam = Camera.main;
        if (cam == null) return true; // без камеры судить не о чем — не отнимаем зрение молча
        Vector3 v = cam.WorldToViewportPoint(target.position + Vector3.up * 0.6f);
        const float edge = 0.04f;     // мизерный допуск по краям: цель на самой кромке кадра ещё «видна»
        return v.z > 0f && v.x >= -edge && v.x <= 1f + edge && v.y >= -edge && v.y <= 1f + edge;
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
