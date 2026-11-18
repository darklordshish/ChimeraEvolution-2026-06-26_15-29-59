using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КОРМ В КАРТЕ (спека `2026-11-18-forest-karta-kak-istochnik` §6 п. 4, шаг 7; ГеймБосс 18.11): кусты корма — слой карты
/// по сиду, вместо случайной раскладки по навмешу. Раскладку меняет только сид; плотность прежняя — числа сцены павильона
/// (160 кустов в квадрате ±850 м, разнос 8 м; решение 13.11 «плотность меряем плейтестом»). Отрастание и съеденность —
/// рантайм (`Forage`), не карта.
///
/// Куст ставится на проходимую сушу внутри кольца: не в воде (тот же признак, что `IsWater`), уклон не круче ходьбы.
/// Попыток на куст — 50, как у прежнего `ForageSpawner`.
/// </summary>
public static class ForestForage
{
    public const int DefaultCount = 160;
    public const float DefaultHalfExtent = 850f;
    public const float DefaultSpacing = 8f;
    const int Attempts = 50;
    /// <summary>Вид корма — пока один: куст лося.</summary>
    public const string Bush = "куст";

    [Serializable]
    public struct Item
    {
        public Vector2 pos;
        public float y;
        public string kind;
        /// <summary>Индекс зоны карты, внутри которой куст; −1 — вне зон.</summary>
        public int zone;
    }

    public static List<Item> Plan(ForestMap m, long seed, int count, float halfExtent, float spacing)
    {
        if (m == null) throw new ArgumentNullException(nameof(m));
        var list = new List<Item>(count);
        if (m.relief == null || count <= 0) return list;
        var rng = new System.Random(unchecked((int)(seed * 2654435761L) ^ 0x5F0A6E));
        float walk = m.relief.Config.WalkMaxDegrees;
        float inner2 = m.ring.inner * m.ring.inner;
        float sp2 = spacing * spacing;
        for (int i = 0; i < count; i++)
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent, (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent);
                if (p.sqrMagnitude > inner2) continue;
                if (ForestMapRaster.WaterDistance(m.water, p) < 0f) continue;
                if (SlopeAt(m, p) > walk) continue;
                bool close = false;
                foreach (var q in list) if ((q.pos - p).sqrMagnitude < sp2) { close = true; break; }
                if (close) continue;
                list.Add(new Item { pos = p, y = m.relief.Height(p.x, p.y), kind = Bush, zone = ZoneOf(m, p) });
                break;
            }
        return list;
    }

    /// <summary>Уклон рельефа карты в точке, градусы (центральная разность по 2 м).</summary>
    public static float SlopeAt(ForestMap m, Vector2 p)
    {
        const float d = 2f;
        float dx = (m.relief.Height(p.x + d, p.y) - m.relief.Height(p.x - d, p.y)) / (2f * d);
        float dz = (m.relief.Height(p.x, p.y + d) - m.relief.Height(p.x, p.y - d)) / (2f * d);
        return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
    }

    static int ZoneOf(ForestMap m, Vector2 p)
    {
        for (int i = 0; i < m.zones.Count; i++)
            if (Vector2.Distance(p, m.zones[i].center) <= m.zones[i].radius) return i;
        return -1;
    }
}
