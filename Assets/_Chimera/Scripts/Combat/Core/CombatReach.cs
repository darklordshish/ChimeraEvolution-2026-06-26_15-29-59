using UnityEngine;

/// <summary>
/// ОБЩЕЕ ПРАВИЛО «ДОСТАЁТ ЛИ УДАР» ДЛЯ БЛИЖНЕГО БОЯ.
///
/// Зачем отдельный помощник. Все ближние приёмы (укус, коготь, рога, пинок, таран, захват) выбирают цель
/// по центру в плоскости XZ: конус/радиус. На ровном поле этого хватало, но в лесу и в бункере это значит
/// «бью сквозь скалу» и «кусаю того, кто на десять метров выше по склону». Дальние приёмы (залп, наскок)
/// сюда НЕ ходят: у них своя траектория и своя логика попадания.
///
/// Правило одно и то же для игрока и NPC:
///  1) горизонталь — конус/радиус (чистая математика, без физики);
///  2) вертикаль — тело цели пересекается по высоте с телом атакующего;
///  3) между телами нет стены/скалы (луч от груди к груди).
///
/// Стены меряются лучом, а не коллайдером-триггером. Существа друг друга не загораживают: союзник в
/// конусе не должен работать щитом для врага за ним.
/// </summary>
public static class CombatReach
{
    // Откуда и куда меряем «линию удара»: грудь, а не корень. У видов корень разный (у игрока — центр
    // капсулы, у волка — земля), поэтому общая добавка делает замер одинаковым для всех.
    const float ChestHeight = 0.6f;

    // Допуск на рассинхрон кадров/склона: луч не должен считаться стеной, если до цели меньше этого.
    const float WallEpsilon = 0.05f;
    static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    /// <summary>ЧИСТАЯ математика конуса в плоскости XZ: цель в радиусе и в полуугле вперёд.
    /// Вынесена отдельно, чтобы сторожить инвариант тестом без физики и сцены.</summary>
    public static bool InCone(Vector3 origin, Vector3 forward, Vector3 point, float range, float halfAngle)
    {
        if (range <= 0f) return false;
        Vector3 to = point - origin;
        to.y = 0f;
        if (to.sqrMagnitude > range * range) return false;
        if (to.sqrMagnitude < 0.0001f) return true; // цель вплотную — угол не определён
        Vector3 fwd = forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return false; // у атакующего нет направления — бить некуда
        return Vector3.Angle(fwd, to) <= halfAngle;
    }

    /// <summary>ЧИСТАЯ проверка вертикали: насколько высоко/низко цель относительно атакующего.
    /// Для «плоских» проверок, где коллайдеров нет.</summary>
    public static bool WithinVertical(float originY, float pointY, float tolerance) =>
        Mathf.Abs(pointY - originY) <= Mathf.Max(0f, tolerance);

    /// <summary>Пересекаются ли тела по высоте. Берём габариты коллайдеров, а не центры: волк и человек
    /// на одной земле разной высоты, и «центр в центре» ошибочно отказывал бы низкому зверю.
    /// Нет коллайдеров (пустой префаб/тест) — откат к центрам с запасом роста.</summary>
    public static bool VerticallyReachable(Transform self, Transform target, float slack = 0f)
    {
        if (self == null || target == null) return false;
        slack = Mathf.Max(0f, slack);
        if (TryBounds(self, out var a) && TryBounds(target, out var b))
            return a.max.y + slack >= b.min.y && b.max.y + slack >= a.min.y;
        return WithinVertical(self.position.y, target.position.y, 2f + slack);
    }

    /// <summary>Горизонтальный конус + вертикаль + отсутствие стены. Основной вход для конусных приёмов.</summary>
    public static bool InMelee(Transform self, Transform target, float range, float halfAngle, float verticalSlack = 0f)
    {
        if (self == null || target == null) return false;
        if (!InCone(self.position, self.forward, target.position, range, halfAngle)) return false;
        return CanHit(self, target, verticalSlack);
    }

    /// <summary>Радиусные приёмы (пинок, таран, захват) сами меряют расстояние, поэтому здесь только
    /// вертикаль и стена.</summary>
    public static bool CanHit(Transform self, Transform target, float verticalSlack = 0f)
    {
        if (self == null || target == null) return false;
        if (!VerticallyReachable(self, target, verticalSlack)) return false;
        return !WallBetween(Chest(self), Chest(target), self, target);
    }

    /// <summary>Есть ли непроходимая геометрия между двумя точками. Свои и чужие тела пропускаем,
    /// существ — тоже: они не укрытие от удара.</summary>
    public static bool WallBetween(Vector3 from, Vector3 to, Transform self, Transform target)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < WallEpsilon) return false;

        // Ignore Raycast — служебный слой (камера им же пользуется). Всё остальное — мир.
        int mask = ~LayerMask.GetMask("Ignore Raycast");
        int count = Physics.RaycastNonAlloc(from, delta / dist, hitBuffer, dist, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var col = hitBuffer[i].collider;
            if (col == null || col.isTrigger) continue;
            if (IsPartOf(col.transform, self) || IsPartOf(col.transform, target)) continue;
            if (col.GetComponentInParent<Health>() != null) continue; // живое тело — не стена
            if (hitBuffer[i].distance < dist - WallEpsilon) return true;   // что-то раньше цели
        }
        return false;
    }

    static Vector3 Chest(Transform t) => t.position + Vector3.up * ChestHeight;

    static bool IsPartOf(Transform t, Transform root)
    {
        if (t == null || root == null) return false;
        return t == root || t.IsChildOf(root);
    }

    static bool TryBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root != null ? root.position : Vector3.zero, Vector3.zero);
        var cols = root != null ? root.GetComponentsInChildren<Collider>() : null;
        bool any = false;
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!any) { bounds = c.bounds; any = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }
        return any;
    }
}
