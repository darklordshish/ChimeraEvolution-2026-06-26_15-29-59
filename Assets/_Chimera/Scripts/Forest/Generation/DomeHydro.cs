using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Гидрология павильона на реальном поле (s10b-2): грубая сетка → заливка DomeWater →
/// крупнейший кластер впадины (озеро) + исток максимальной аккумуляции + трек D8 →
/// мировая полилиния реки с монотонными отметками. Carve — аналитический:
/// SampleHydro вычитает профиль русла/чаши из поля, сетка нужна только для трассировки.
/// Это и есть WaterMask спеки (скаляр waterLevel старого пайплайна не тронут).
/// </summary>
public static class DomeHydro
{
    public class State
    {
        public List<Vector2> riverPts = new List<Vector2>();
        public List<float> riverBed = new List<float>();
        public Vector2 lakeCenter;
        public float lakeRadius;
        public float lakeLevel;
        public float riverHalfWidth = 4f;
        public float riverDepth = 1.2f;
        public float lakeDepth = 2.5f;
    }

    public static State Build(DomeGenConfigSO cfg, int gridRes)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        gridRes = Mathf.Clamp(gridRes, 32, 256);
        float d = cfg.mapDiameter;
        float cell = d / gridRes;
        var h = new float[gridRes, gridRes];
        for (int ix = 0; ix < gridRes; ix++)
            for (int iz = 0; iz < gridRes; iz++)
                h[ix, iz] = DomeHeightField.SampleHeight(cfg, -d / 2 + (ix + 0.5f) * cell, -d / 2 + (iz + 0.5f) * cell);

        var filled = DomeWater.PriorityFlood(h, 0.01f);
        var lake = DomeWater.LakeMask(h, filled, 0.01f);
        var state = new State();

        // Крупнейший кластер озера (BFS).
        var seen = new bool[gridRes, gridRes];
        List<Vector2Int> best = null;
        for (int ix = 0; ix < gridRes; ix++)
            for (int iz = 0; iz < gridRes; iz++)
            {
                if (!lake[ix, iz] || seen[ix, iz]) continue;
                var cluster = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(ix, iz));
                seen[ix, iz] = true;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    cluster.Add(c);
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                        int nz = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= gridRes || nz >= gridRes || !lake[nx, nz] || seen[nx, nz]) continue;
                        seen[nx, nz] = true;
                        queue.Enqueue(new Vector2Int(nx, nz));
                    }
                }
                if (best == null || cluster.Count > best.Count) best = cluster;
            }
        if (best == null || best.Count == 0)
            throw new InvalidOperationException("озеро не нашлось: поле без впадин (смени сид)");
        var inLake = new bool[gridRes, gridRes];
        float sumX = 0f, sumZ = 0f;
        foreach (var c in best) { inLake[c.x, c.y] = true; sumX += c.x; sumZ += c.y; }
        float cx = sumX / best.Count, cz = sumZ / best.Count;
        state.lakeCenter = new Vector2(-d / 2 + (cx + 0.5f) * cell, -d / 2 + (cz + 0.5f) * cell);
        state.lakeRadius = Mathf.Sqrt(best.Count / Mathf.PI) * cell;
        // Уровень = минимальный обод (min filled у соседей кластера).
        float spill = float.PositiveInfinity;
        foreach (var c in best)
            for (int k = 0; k < 8; k++)
            {
                int nx = c.x + DomeWater.Dx[k];
                int nz = c.y + DomeWater.Dz[k];
                if (nx < 0 || nz < 0 || nx >= gridRes || nz >= gridRes || inLake[nx, nz]) continue;
                if (filled[nx, nz] < spill) spill = filled[nx, nz];
            }
        state.lakeLevel = spill;

        // Исток: максимальная аккумуляция вне озера, но с запасом трека:
        // трек обязан дать ≥8 клеток до озера/границы, иначе это лужа у берега.
        var dir = DomeWater.FlowDir(filled);
        var acc = DomeWater.Accumulation(filled, dir);
        var candidates = new List<Vector2Int>();
        for (int ix = 1; ix < gridRes - 1; ix++)
            for (int iz = 1; iz < gridRes - 1; iz++)
            {
                if (inLake[ix, iz]) continue;
                candidates.Add(new Vector2Int(ix, iz));
            }
        candidates.Sort((a, b) => acc[b.x, b.y].CompareTo(acc[a.x, a.y]));
        List<Vector2Int> gridPath = null;
        foreach (var cand in candidates)
        {
            var traced = DomeWater.TraceRiver(dir, cand.x, cand.y);
            int usable = 0;
            while (usable < traced.Count && !inLake[traced[usable].x, traced[usable].y]) usable++;
            if (usable >= 8) { gridPath = traced.GetRange(0, usable); break; }
        }
        if (gridPath == null)
            throw new InvalidOperationException("река короче 8 клеток (смени сид)");
        // Мировая полилиния (прореживание ×2) + монотонные отметки русла.
        float ceiling = float.PositiveInfinity;
        for (int i = 0; i < gridPath.Count; i += 2)
        {
            var c = gridPath[i];
            var wp = new Vector2(-d / 2 + (c.x + 0.5f) * cell, -d / 2 + (c.y + 0.5f) * cell);
            if (inLake[c.x, c.y]) break; // влилась в озеро
            state.riverPts.Add(wp);
            float carved = Math.Min(DomeHeightField.SampleHeight(cfg, wp.x, wp.y) - state.riverDepth, ceiling);
            state.riverBed.Add(carved);
            ceiling = carved - 0.01f;
        }
        if (state.riverPts.Count < 2)
            throw new InvalidOperationException("река короче 2 точек (смени сид)");
        return state;
    }

    /// <summary>Поле с вырезом: чаша озера + русло реки поверх DomeHeightField.</summary>
    public static float SampleHydro(DomeGenConfigSO cfg, State s, float x, float z)
    {
        float h = DomeHeightField.SampleHeight(cfg, x, z);
        float dx = x - s.lakeCenter.x;
        float dz = z - s.lakeCenter.y;
        float dl = Mathf.Sqrt(dx * dx + dz * dz);
        if (dl < s.lakeRadius)
        {
            float t = 1f - dl / s.lakeRadius;
            float bowl = s.lakeLevel + 0.5f - (0.5f + s.lakeDepth) * (t * t * (3f - 2f * t));
            h = Math.Min(h, bowl);
        }
        int near = NearestRiverPoint(s, x, z, out float dist);
        if (near >= 0)
        {
            float w = s.riverHalfWidth * 3f;
            if (dist < w)
            {
                float u = dist / w;
                float blend = 1f - u * u * (3f - 2f * u);
                h = Mathf.Lerp(h, Math.Min(h, s.riverBed[near]), blend);
            }
        }
        return h;
    }

    static int NearestRiverPoint(State s, float x, float z, out float dist)
    {
        int best = -1;
        dist = float.PositiveInfinity;
        float range = s.riverHalfWidth * 3f;
        for (int i = 0; i < s.riverPts.Count; i++)
        {
            float dx = x - s.riverPts[i].x;
            float dz = z - s.riverPts[i].y;
            if (Mathf.Abs(dx) > range || Mathf.Abs(dz) > range) continue;
            float d = dx * dx + dz * dz;
            if (best < 0 || d < dist) { dist = d; best = i; }
        }
        dist = best < 0 ? float.PositiveInfinity : Mathf.Sqrt(dist);
        return best;
    }
}
