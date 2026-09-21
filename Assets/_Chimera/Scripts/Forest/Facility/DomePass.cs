using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Связность павильона (s10f-2): сетка проходимости (склон >22° / вода / корпуса) +
/// BFS от лаборатории до POI. Чистые функции — acceptance-тест генерации.
/// </summary>
public static class DomePass
{
    /// <summary>
    /// Карта блоков res×res на квадрат диаметра: склон, вода (озеро/русло),
    /// круги корпусов (r = половина габарита + 1м).
    /// </summary>
    public static bool[,] BlockedGrid(DomeGenConfigSO cfg, DomeHydro.State hydro,
        DomeFacilityLayout.Facility f, int res)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        res = Mathf.Clamp(res, 32, 256);
        float d = cfg.mapDiameter;
        float cell = d / res;
        var blocked = new bool[res, res];
        for (int ix = 0; ix < res; ix++)
            for (int iz = 0; iz < res; iz++)
            {
                float x = -d / 2 + (ix + 0.5f) * cell;
                float z = -d / 2 + (iz + 0.5f) * cell;
                if (new Vector2(x, z).magnitude > d / 2) { blocked[ix, iz] = true; continue; }
                if (DomeSlope.SlopeAt(cfg, x, z) > 22f) { blocked[ix, iz] = true; continue; }
                if (hydro != null)
                {
                    float dx = x - hydro.lakeCenter.x, dz = z - hydro.lakeCenter.y;
                    if (dx * dx + dz * dz < hydro.lakeRadius * hydro.lakeRadius) { blocked[ix, iz] = true; continue; }
                    bool wet = false;
                    foreach (var rp in hydro.riverPts)
                    {
                        float rx = x - rp.x, rz = z - rp.y;
                        if (rx * rx + rz * rz < (hydro.riverHalfWidth + 1f) * (hydro.riverHalfWidth + 1f)) { wet = true; break; }
                    }
                    if (wet) { blocked[ix, iz] = true; continue; }
                }
                if (f != null)
                {
                    foreach (var p in f.parts)
                    {
                        if (!p.collider) continue;
                        float wx = f.labCenter.x + p.pos.x, wz = f.labCenter.y + p.pos.z;
                        float rr = Mathf.Max(p.size.x, p.size.z) * 0.5f + 1f;
                        // Ангарные детали уже в мировых (смещение учтено в pos) — эвристика:
                        // детали дальше 60м от лабы считаем мировыми.
                        if (Mathf.Abs(p.pos.x) > 60f || Mathf.Abs(p.pos.z) > 60f) { wx = p.pos.x; wz = p.pos.z; }
                        float ddx = x - wx, ddz = z - wz;
                        if (ddx * ddx + ddz * ddz < rr * rr) { blocked[ix, iz] = true; break; }
                    }
                }
            }
        return blocked;
    }

    static Vector2Int ToCell(float x, float z, float d, int res)
    {
        int ix = Mathf.Clamp(Mathf.FloorToInt((x + d / 2) / d * res), 0, res - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt((z + d / 2) / d * res), 0, res - 1);
        return new Vector2Int(ix, iz);
    }

    /// <summary>BFS-достижимость между двумя точками (4-связность).</summary>
    public static bool Connected(bool[,] blocked, float d, Vector2 from, Vector2 to)
    {
        int res = blocked.GetLength(0);
        var start = NearestOpen(blocked, ToCell(from.x, from.y, d, res), 3);
        var goal = NearestOpen(blocked, ToCell(to.x, to.y, d, res), 3);
        if (!start.HasValue || !goal.HasValue) return false;
        return Bfs(blocked, start.Value, goal.Value);
    }

    /// <summary>
    /// Ближайшая проходимая клетка (аналог NavMesh.SamplePosition): POI — точка,
    /// встать можно рядом. Без этого старт/цель в крутой клетке 15м запирают всё.
    /// </summary>
    public static Vector2Int? NearestOpen(bool[,] blocked, Vector2Int cell, int maxR)
    {
        int res = blocked.GetLength(0);
        if (cell.x >= 0 && cell.y >= 0 && cell.x < res && cell.y < res && !blocked[cell.x, cell.y])
            return cell;
        for (int r = 1; r <= maxR; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    int nx = cell.x + dx, nz = cell.y + dz;
                    if (nx < 0 || nz < 0 || nx >= res || nz >= res || blocked[nx, nz]) continue;
                    return new Vector2Int(nx, nz);
                }
        return null;
    }

    static bool Bfs(bool[,] blocked, Vector2Int start, Vector2Int goal)
    {
        int res = blocked.GetLength(0);
        var seen = new bool[res, res];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        seen[start.x, start.y] = true;
        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == goal) return true;
            for (int k = 0; k < 4; k++)
            {
                int nx = c.x + dx[k], nz = c.y + dz[k];
                if (nx < 0 || nz < 0 || nx >= res || nz >= res || blocked[nx, nz] || seen[nx, nz]) continue;
                seen[nx, nz] = true;
                queue.Enqueue(new Vector2Int(nx, nz));
            }
        }
        return false;
    }
}
