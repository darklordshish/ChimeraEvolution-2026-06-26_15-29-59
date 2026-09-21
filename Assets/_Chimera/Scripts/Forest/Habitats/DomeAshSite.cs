using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ясень-исполин и сова (s10g-1): место — 100–400м от лаборатории, уклон <15°,
/// суша (вне озера и русла); гнездо — LairSite «Сова»/Nest в кроне.
/// Чистые данные + конфигуратор логова; геометрия — превью следующим заходом.
/// </summary>
public static class DomeAshSite
{
    public struct AshSpot
    {
        public Vector2 pos;
        public float groundY;
    }

    /// <summary>Место ясеня: первое годное по детерминированному обходу сетки.</summary>
    public static AshSpot FindSpot(DomeGenConfigSO cfg, long seed, Vector2 labCenter,
        DomeHydro.State hydro, int gridRes)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (hydro == null) throw new ArgumentNullException(nameof(hydro));
        gridRes = Mathf.Clamp(gridRes, 32, 160);
        float d = cfg.mapDiameter;
        float cell = d / gridRes;
        var fallback = new AshSpot { pos = new Vector2(labCenter.x + 150f, labCenter.y), groundY = 0f };
        bool hasFallback = false;
        for (int ix = 0; ix < gridRes; ix++)
            for (int iz = 0; iz < gridRes; iz++)
            {
                float x = -d / 2 + (ix + 0.5f) * cell;
                float z = -d / 2 + (iz + 0.5f) * cell;
                var p = new Vector2(x, z);
                if (p.magnitude > d / 2) continue;
                float distLab = Vector2.Distance(p, labCenter);
                if (distLab < 100f || distLab > 400f) continue;
                if (DomeSlope.SlopeAt(cfg, x, z) >= 15f) continue;
                float dx = x - hydro.lakeCenter.x, dz = z - hydro.lakeCenter.y;
                if (dx * dx + dz * dz < (hydro.lakeRadius + 10f) * (hydro.lakeRadius + 10f)) continue;
                bool nearRiver = false;
                foreach (var rp in hydro.riverPts)
                {
                    float rx = x - rp.x, rz = z - rp.y;
                    if (rx * rx + rz * rz < (hydro.riverHalfWidth + 5f) * (hydro.riverHalfWidth + 5f)) { nearRiver = true; break; }
                }
                if (nearRiver) continue;
                if (!hasFallback)
                {
                    fallback = new AshSpot { pos = p, groundY = DomeHeightField.SampleHeight(cfg, x, z) };
                    hasFallback = true;
                }
                long roll = (long)(SeededHash.Hash(seed, 1000, ix * 1000 + iz) % 7);
                if (roll == 0)
                    return new AshSpot { pos = p, groundY = DomeHeightField.SampleHeight(cfg, x, z) };
            }
        if (!hasFallback) throw new InvalidOperationException("места ясеню нет (смени сид)");
        return fallback;
    }

    /// <summary>Гнездо совы на логове: вид, тип, радиусы под крону исполина.</summary>
    public static void ConfigureNest(LairSite site, Vector3 crownPos)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        site.speciesName = "Сова";
        site.areaType = LairSite.LairAreaType.Nest;
        site.homeRadius = 30f;
        site.spawnRadius = 4f;
        site.fearRadius = 25f;
        site.tier = 2;
        site.capacity = 2;
        site.transform.position = crownPos;
    }
}
