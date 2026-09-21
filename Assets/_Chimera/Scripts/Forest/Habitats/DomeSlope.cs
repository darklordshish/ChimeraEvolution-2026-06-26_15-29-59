using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Каменистый склон павильона (s10e-1): маска крутизны по полю купола, устья пещер
/// (склон 35–55° + ровная площадка 4–6м перед входом <15°) и гнёзда змей (15–40м от устья).
/// Чистые функции — геометрия ниш и регистрация логовищ следующим заходом.
/// Пещеры — только ниши-обманки: heightfield не умеет козырьки (вердикт эколога).
/// </summary>
public static class DomeSlope
{
    public struct MouthSpot
    {
        public Vector2 pos;
        public float facing; // радианы, направление наружу-вниз (дверь смотрит сюда)
        public float padSlope;
    }

    public struct NestSpot
    {
        public Vector2 pos;
        public float radius;
        public int mouth;
    }

    /// <summary>Уклон в градусах центральными разностями (шаг 1м — масштаб купола).</summary>
    public static float SlopeAt(DomeGenConfigSO cfg, float x, float z)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        const float e = 1f;
        float gx = (DomeHeightField.SampleHeight(cfg, x + e, z) - DomeHeightField.SampleHeight(cfg, x - e, z)) / (2f * e);
        float gz = (DomeHeightField.SampleHeight(cfg, x, z + e) - DomeHeightField.SampleHeight(cfg, x, z - e)) / (2f * e);
        return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
    }

    /// <summary>Направление downhill (куда смотрит устье).</summary>
    public static Vector2 DownhillDir(DomeGenConfigSO cfg, float x, float z)
    {
        const float e = 1f;
        float gx = (DomeHeightField.SampleHeight(cfg, x + e, z) - DomeHeightField.SampleHeight(cfg, x - e, z)) / (2f * e);
        float gz = (DomeHeightField.SampleHeight(cfg, x, z + e) - DomeHeightField.SampleHeight(cfg, x, z - e)) / (2f * e);
        var g = new Vector2(gx, gz);
        return g.sqrMagnitude > 1e-8f ? -g.normalized : Vector2.right;
    }

    /// <summary>Маска клеток с уклоном ≥ minSlope на квадрате (центр, сторона, res).</summary>
    public static bool[,] SlopeMask(DomeGenConfigSO cfg, float cx, float cz, float size, int res, float minSlope)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var mask = new bool[res, res];
        for (int ix = 0; ix < res; ix++)
            for (int iz = 0; iz < res; iz++)
            {
                float x = cx - size / 2 + (ix + 0.5f) * size / res;
                float z = cz - size / 2 + (iz + 0.5f) * size / res;
                mask[ix, iz] = SlopeAt(cfg, x, z) >= minSlope;
            }
        return mask;
    }

    /// <summary>
    /// Устья (want штук): склон 35–55° + площадка 5м вниз по склону <15°.
    /// Разнос: не ближе 150м друг к другу (компактный склон, не россыпь).
    /// </summary>
    public static List<MouthSpot> FindMouths(DomeGenConfigSO cfg, int want, int gridRes)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var spots = new List<MouthSpot>();
        float d = cfg.mapDiameter;
        gridRes = Mathf.Clamp(gridRes, 32, 160);
        float cell = d / gridRes;
        for (int ix = 0; ix < gridRes && spots.Count < want * 4; ix++)
            for (int iz = 0; iz < gridRes && spots.Count < want * 4; iz++)
            {
                float x = -d / 2 + (ix + 0.5f) * cell;
                float z = -d / 2 + (iz + 0.5f) * cell;
                if (new Vector2(x, z).magnitude > d / 2) continue; // только круг павильона
                float slope = SlopeAt(cfg, x, z);
                if (slope < 35f || slope > 55f) continue;
                Vector2 down = DownhillDir(cfg, x, z);
                Vector2 pad = new Vector2(x, z) + down * 5f;
                float padSlope = SlopeAt(cfg, pad.x, pad.y);
                if (padSlope >= 15f) continue;
                bool far = true;
                foreach (var s in spots)
                    if (Vector2.Distance(s.pos, new Vector2(x, z)) < 150f) { far = false; break; }
                if (!far) continue;
                spots.Add(new MouthSpot
                {
                    pos = new Vector2(x, z),
                    facing = Mathf.Atan2(down.y, down.x),
                    padSlope = padSlope,
                });
            }
        if (spots.Count > want) spots.RemoveRange(want, spots.Count - want);
        return spots;
    }

    /// <summary>Гнёзда: perMouth штук на устье, 15–40м, уклон <30° (детерминированный джиттер сида).</summary>
    public static List<NestSpot> FindNests(DomeGenConfigSO cfg, long seed, List<MouthSpot> mouths, int perMouth)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var nests = new List<NestSpot>();
        for (int m = 0; m < mouths.Count; m++)
        {
            int placed = 0;
            for (int j = 0; j < perMouth * 8 && placed < perMouth; j++)
            {
                float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 600 + m, j)) * Mathf.PI * 2f;
                float dist = 15f + SeededHash.ToFloat01(SeededHash.Hash(seed, 700 + m, j)) * 25f;
                Vector2 p = mouths[m].pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                if (p.magnitude > cfg.mapDiameter / 2) continue;
                if (SlopeAt(cfg, p.x, p.y) >= 30f) continue;
                nests.Add(new NestSpot { pos = p, radius = 3f, mouth = m });
                placed++;
            }
        }
        return nests;
    }
}
