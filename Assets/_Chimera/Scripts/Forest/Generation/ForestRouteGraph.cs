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
        float maxSlopeDegrees = 22f, float ringInset = 2f, float clearance = 0f)
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
        if (clearance > 0f) Erode(ref blocked, cell, clearance);
        return blocked;
    }

    static void Erode(ref bool[,] blocked, float cell, float clearance)
    {
        int res = blocked.GetLength(0);
        int radius = Mathf.CeilToInt(clearance / Mathf.Max(0.01f, cell));
        if (radius <= 0) return;
        var dist = new int[res, res];
        var queue = new System.Collections.Generic.Queue<Vector2Int>();
        for (int x = 0; x < res; x++)
            for (int z = 0; z < res; z++)
            {
                dist[x, z] = int.MaxValue;
                if (!blocked[x, z]) continue;
                dist[x, z] = 0;
                queue.Enqueue(new Vector2Int(x, z));
            }
        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = c.x + dx[k], nz = c.y + dz[k];
                if (nx < 0 || nz < 0 || nx >= res || nz >= res) continue;
                if (dist[nx, nz] <= dist[c.x, c.y] + 1) continue;
                dist[nx, nz] = dist[c.x, c.y] + 1;
                queue.Enqueue(new Vector2Int(nx, nz));
            }
        }
        for (int x = 0; x < res; x++)
            for (int z = 0; z < res; z++)
                if (!blocked[x, z] && dist[x, z] < radius) blocked[x, z] = true;
    }

    public static int Components(bool[,] blocked)
    {
        int res = blocked.GetLength(0);
        var seen = new bool[res, res];
        int count = 0;
        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        for (int x = 0; x < res; x++)
            for (int z = 0; z < res; z++)
            {
                if (blocked[x, z] || seen[x, z]) continue;
                count++;
                var queue = new System.Collections.Generic.Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, z));
                seen[x, z] = true;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = c.x + dx[k], nz = c.y + dz[k];
                        if (nx < 0 || nz < 0 || nx >= res || nz >= res) continue;
                        if (blocked[nx, nz] || seen[nx, nz]) continue;
                        seen[nx, nz] = true;
                        queue.Enqueue(new Vector2Int(nx, nz));
                    }
                }
            }
        return count;
    }

    public static bool Connected(bool[,] blocked, float diameter, Vector2 from, Vector2 to) =>
        DomePass.Connected(blocked, diameter, from, to);
}
