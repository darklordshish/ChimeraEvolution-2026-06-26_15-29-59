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
        public float riverHalfWidth = 2.5f;
        public float riverDepth = 0.9f;
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

        // Кластер озера: самый ГЛУБОКИЙ (max filled−orig), не самый большой —
        // иначе за озеро сходит пологая низина на полкарты. Радиус капается spec max.
        var seen = new bool[gridRes, gridRes];
        List<Vector2Int> best = null;
        float bestDepth = 0f;
        for (int ix = 0; ix < gridRes; ix++)
            for (int iz = 0; iz < gridRes; iz++)
            {
                if (!lake[ix, iz] || seen[ix, iz]) continue;
                var cluster = new List<Vector2Int>();
                float depth = 0f;
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(ix, iz));
                seen[ix, iz] = true;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    cluster.Add(c);
                    float dd = filled[c.x, c.y] - h[c.x, c.y];
                    if (dd > depth) depth = dd;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                        int nz = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= gridRes || nz >= gridRes || !lake[nx, nz] || seen[nx, nz]) continue;
                        seen[nx, nz] = true;
                        queue.Enqueue(new Vector2Int(nx, nz));
                    }
                }
                if (best == null || depth > bestDepth) { best = cluster; bestDepth = depth; }
            }
        if (best == null || best.Count == 0)
            throw new InvalidOperationException("озеро не нашлось: поле без впадин (смени сид)");
        var inLake = new bool[gridRes, gridRes];
        float sumX = 0f, sumZ = 0f;
        foreach (var c in best) { inLake[c.x, c.y] = true; sumX += c.x; sumZ += c.y; }
        float cx = sumX / best.Count, cz = sumZ / best.Count;
        state.lakeCenter = new Vector2(-d / 2 + (cx + 0.5f) * cell, -d / 2 + (cz + 0.5f) * cell);
        state.lakeRadius = Math.Min(Mathf.Sqrt(best.Count / Mathf.PI) * cell, 150f);
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
        // Вода — только капированный диск: за его пределами марш, река течёт по нему.
        // (Полный кластер впадины может накрывать полкарты и душить треки.)
        for (int ix = 0; ix < gridRes; ix++)
            for (int iz = 0; iz < gridRes; iz++)
            {
                float wx = -d / 2 + (ix + 0.5f) * cell;
                float wz = -d / 2 + (iz + 0.5f) * cell;
                float dx = wx - state.lakeCenter.x;
                float dz = wz - state.lakeCenter.y;
                inLake[ix, iz] = dx * dx + dz * dz <= state.lakeRadius * state.lakeRadius;
            }

        // Исток: самый ДЛИННЫЙ трек среди настоящих ручьёв (acc ≥ 10), не короче 150м.
        // Максимум аккумуляции даёт толстую, но короткую протоку у озера — спеке нужна длина.
        var dir = DomeWater.FlowDir(filled);
        var acc = DomeWater.Accumulation(filled, dir);
        var candidates = new List<Vector2Int>();
        for (int ix = 1; ix < gridRes - 1; ix++)
            for (int iz = 1; iz < gridRes - 1; iz++)
            {
                if (inLake[ix, iz]) continue;
                candidates.Add(new Vector2Int(ix, iz));
            }
        List<Vector2Int> gridPath = null;
        int minCells = Mathf.CeilToInt(150f / cell);
        int bestLen = 0;
        float bestLenAcc = 0f;
        foreach (var cand in candidates)
        {
            if (acc[cand.x, cand.y] < 10f) continue;
            var traced = DomeWater.TraceRiver(dir, cand.x, cand.y);
            int usable = 0;
            while (usable < traced.Count && !inLake[traced[usable].x, traced[usable].y]) usable++;
            if (usable >= minCells && (usable > bestLen || (usable == bestLen && acc[cand.x, cand.y] > bestLenAcc)))
            {
                bestLen = usable;
                bestLenAcc = acc[cand.x, cand.y];
                gridPath = traced.GetRange(0, usable);
            }
        }
        if (gridPath == null)
            throw new InvalidOperationException("река короче 150м (смени сид)");
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
        // Спека: речушка 300–600м. Хвост (устье) держим, лишнее верховье отрезаем.
        while (state.riverPts.Count > 2 && PolyLength(state.riverPts) > 600f)
        {
            state.riverPts.RemoveAt(0);
            state.riverBed.RemoveAt(0);
        }
        return state;
    }

    static float PolyLength(List<Vector2> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
        return len;
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
