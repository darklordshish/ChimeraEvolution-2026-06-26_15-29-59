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

        // Деревья: всё, что знает план (точка, масштаб, поворот, порода, состояние, твёрдость).
        Mix(ref h, m.trees != null ? m.trees.Count : -1);
        if (m.trees != null)
            foreach (var t in m.trees)
            {
                V3(ref h, t.pos); Q(ref h, t.scale); Q(ref h, t.rotY);
                Mix(ref h, (int)t.kind); Mix(ref h, (int)t.state); Mix(ref h, t.solid ? 1 : 0);
            }

        // Рельеф: рецепт и ключ эрозии. Сетка эрозии (`relief.erosion.delta`) в хеш НЕ входит — ГеймБосс 18.11.
        var r = m.relief;
        Mix(ref h, r != null ? 1 : 0);
        if (r != null)
        {
            Mix(ref h, r.worldVersion);
            S(ref h, r.cfgJson);
            S(ref h, r.erosionKey);
            if (r.hydro != null)
            {
                Mix(ref h, r.hydro.riverBed.Count);
                foreach (var b in r.hydro.riverBed) Q(ref h, b);
            }
            var t = r.terrace;
            Mix(ref h, t.valid ? 1 : 0); Q(ref h, t.center.x); Q(ref h, t.center.y); Q(ref h, t.radius); Q(ref h, t.height); Q(ref h, t.blend);
            Mix(ref h, r.forms.Count);
            foreach (var f in r.forms)
            {
                S(ref h, f.name); Mix(ref h, (int)f.sign); Mix(ref h, (int)f.kind);
                Q(ref h, f.center.x); Q(ref h, f.center.y); Q(ref h, f.axis.x); Q(ref h, f.axis.y);
                Q(ref h, f.halfLength); Q(ref h, f.diameter); Q(ref h, f.crest); Q(ref h, f.height);
            }
            var hl = r.hill;
            Mix(ref h, hl.valid ? 1 : 0); Q(ref h, hl.center.x); Q(ref h, hl.center.y); Q(ref h, hl.radius); Q(ref h, hl.height);
            Mix(ref h, r.corridorPads.Count);
            foreach (var p in r.corridorPads) { Q(ref h, p.a.x); Q(ref h, p.a.y); Q(ref h, p.b.x); Q(ref h, p.b.y); Q(ref h, p.halfWidth); Q(ref h, p.blend); Q(ref h, p.y); }
            Mix(ref h, r.padEnabled ? 1 : 0); Mix(ref h, r.padRect ? 1 : 0); Mix(ref h, r.northCrevice ? 1 : 0);
            Q(ref h, r.labCenter.x); Q(ref h, r.labCenter.y); Q(ref h, r.padYaw); Q(ref h, r.platformY); Mix(ref h, (int)r.entrance);
        }
        return h;
    }

    static void S(ref ulong h, string s)
    {
        if (s == null) { Mix(ref h, -1); return; }
        Mix(ref h, s.Length);
        foreach (char c in s) Mix(ref h, c);
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
