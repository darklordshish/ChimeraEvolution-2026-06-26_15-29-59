using System;
using UnityEngine;

/// <summary>
/// Маршрутный граф купола по ОДНОМУ полу (<see cref="DomeWorld.SampleGround"/>):
/// скальное кольцо, вода, крутизна. Не заменяет NavMesh — это дешёвый детектор
/// связности для сравнения сидов и срезов.
/// </summary>
public static class ForestRouteGraph
{
    public static bool[,] Build(DomeGenConfigSO cfg, DomeHydro.State hydro,
        Func<float, float, float> ground, int res = 128,
        float maxSlopeDegrees = 22f, float ringInset = 2f)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (ground == null) throw new ArgumentNullException(nameof(ground));
        res = Mathf.Clamp(res, 32, 256);
        float d = cfg.mapDiameter;
        float cell = d / res;
        float rin = DomeRockRing.InnerRadius(cfg) - ringInset;
        var blocked = new bool[res, res];
        for (int ix = 0; ix < res; ix++)
            for (int iz = 0; iz < res; iz++)
            {
                float x = -d / 2 + (ix + 0.5f) * cell;
                float z = -d / 2 + (iz + 0.5f) * cell;
                if (new Vector2(x, z).magnitude > rin) { blocked[ix, iz] = true; continue; }
                if (DomeSlope.SlopeAtS(cfg, x, z, ground) > maxSlopeDegrees)
                {
                    blocked[ix, iz] = true;
                    continue;
                }
                if (hydro != null)
                {
                    float dx = x - hydro.lakeCenter.x, dz = z - hydro.lakeCenter.y;
                    if (dx * dx + dz * dz < hydro.lakeRadius * hydro.lakeRadius)
                    {
                        blocked[ix, iz] = true;
                        continue;
                    }
                    foreach (var rp in hydro.riverPts)
                    {
                        float rx = x - rp.x, rz = z - rp.y;
                        if (rx * rx + rz * rz <
                            (hydro.riverHalfWidth + 1f) * (hydro.riverHalfWidth + 1f))
                        {
                            blocked[ix, iz] = true;
                            break;
                        }
                    }
                }
            }
        return blocked;
    }

    public static bool Connected(bool[,] blocked, float diameter, Vector2 from, Vector2 to) =>
        DomePass.Connected(blocked, diameter, from, to);
}
