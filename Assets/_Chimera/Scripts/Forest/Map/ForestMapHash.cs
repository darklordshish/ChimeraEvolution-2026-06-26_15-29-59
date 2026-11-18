using UnityEngine;

/// <summary>
/// Отпечаток содержания карты: FNV-1a по квантованным (мм) числам слоёв в фиксированном порядке полей. Поле `hash`
/// в него не входит; время, имена объектов сцены и порядок словарей — тоже (их в карте нет).
/// </summary>
public static class ForestMapHash
{
    const ulong Offset = 14695981039346656037UL;
    const ulong Prime = 1099511628211UL;

    public static ulong Of(ForestMap m)
    {
        ulong h = Offset;
        if (m == null) return h;
        Mix(ref h, m.schema);
        Mix(ref h, (int)(m.seed & 0xffffffff)); Mix(ref h, (int)(m.seed >> 32));
        Mix(ref h, m.worldVersion);
        Q(ref h, m.diameter);

        var w = m.water;
        Mix(ref h, w.hasLake ? 1 : 0);
        Q(ref h, w.lakeCenter.x); Q(ref h, w.lakeCenter.y);
        Q(ref h, w.lakeRadius); Q(ref h, w.lakeLevel); Q(ref h, w.lakeDepth);
        Mix(ref h, w.river.Count);
        foreach (var p in w.river) { Q(ref h, p.x); Q(ref h, p.y); }
        Q(ref h, w.riverHalfWidth); Q(ref h, w.riverDepth);

        Q(ref h, m.ring.inner); Q(ref h, m.ring.outer);

        V3(ref h, m.points.entry); V3(ref h, m.points.spawn);

        var l = m.lab;
        Mix(ref h, l.has ? 1 : 0);
        Q(ref h, l.center.x); Q(ref h, l.center.y); Q(ref h, l.yawDeg);
        V3(ref h, l.door); Q(ref h, l.outward.x); Q(ref h, l.outward.y);

        Q(ref h, m.areas.totalKm2); Q(ref h, m.areas.landKm2);
        return h;
    }

    static void V3(ref ulong h, Vector3 v) { Q(ref h, v.x); Q(ref h, v.y); Q(ref h, v.z); }

    static void Q(ref ulong h, float v) => Mix(ref h, Mathf.RoundToInt(v * 1000f));

    static void Mix(ref ulong h, int v)
    {
        unchecked
        {
            for (int i = 0; i < 4; i++)
            {
                h ^= (byte)(v >> (i * 8));
                h *= Prime;
            }
        }
    }
}
