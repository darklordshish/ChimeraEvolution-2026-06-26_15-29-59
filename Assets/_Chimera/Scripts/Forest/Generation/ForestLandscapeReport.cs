using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// S5: сборка снимка мира из метрик <see cref="ForestLandscapeMetrics"/>. Одна
/// последовательность вызовов на сид — её используют и отчёт (<c>Tools/Agent/ForestVerify</c>),
/// и ворота (<c>ForestLandscapeGateTests</c>). Формулы — только в
/// <see cref="ForestLandscapeMetrics"/>; здесь нет своей математики.
/// </summary>
public static class ForestLandscapeReport
{
    public sealed class Snapshot
    {
        public long seed;
        public int worldVersion;
        public int quads;
        public float meshStep;
        public float BuildMs;
        public int chunks, trees, treeColliders, deadLogs, lairs;
        public ulong fingerprint;
        public ForestLandscapeMetrics.SlopeResult Slope;
        public float Energy, Meso, Local;
        public int MesoWindows, MesoGood;
        public List<ForestLandscapeMetrics.FormInfo> Forms;
        public ForestLandscapeMetrics.BalanceResult Balance;
        public ForestLandscapeMetrics.SeamResult Seams;
        public ForestLandscapeMetrics.ClimbResult Climb;
        public ForestLandscapeMetrics.LosResult Los;
        public ForestLandscapeMetrics.PoiZoneResult Poi;
        public ForestLandscapeMetrics.PatchResult Patches;
        public ForestLandscapeMetrics.RoadResult Roads;
        public ForestLandscapeMetrics.WaterLairResult Water;
        public ForestLandscapeMetrics.HydroResult Hydro;
        public ForestLandscapeMetrics.BudgetResult Budget;
    }

    public static Snapshot Measure(ForestBuilder b, float buildMs, int slopeRes = 128)
    {
        var cfg = ConfigFor(b);
        float rin = DomeRockRing.InnerRadius(cfg);
        Func<float, float, float> ground = (x, z) => b.SampleGround(x, z);
        var forms = b.CompositionForms;
        var snap = new Snapshot
        {
            seed = b.ResolvedSeed,
            worldVersion = b.WorldVersion,
            quads = b.terrainQuads,
            meshStep = b.Contract.meshStep,
            BuildMs = buildMs,
            chunks = b.Chunks,
            trees = b.Trees,
            treeColliders = b.TreeColliders,
            deadLogs = b.DeadLogs,
            lairs = b.Lairs.Count,
            fingerprint = b.Fingerprint,
        };
        snap.Slope = ForestLandscapeMetrics.Slope(ground, cfg, rin, slopeRes,
            b.Contract.meshStep, forms);
        var h0 = DomeHeightField.DomeHeightParams.From(cfg);
        snap.Energy = ForestLandscapeMetrics.LongEnergy(
            (x, z) => DomeHeightField.SampleRaw(h0, x, z), rin, 96);
        snap.Meso = ForestLandscapeMetrics.MesoCoverage(ground, rin, 96,
            out snap.MesoWindows, out snap.MesoGood);
        snap.Local = ForestLandscapeMetrics.LocalRelief(ground, rin, 128);
        snap.Forms = ForestLandscapeMetrics.Landforms(forms);
        snap.Balance = ForestLandscapeMetrics.Balance(forms);
        snap.Seams = ForestLandscapeMetrics.Seams(ground, cfg, rin, forms);

        var entry = new Vector2(b.Entry.x, b.Entry.z);
        var door = new Vector2(b.LabDoor.x, b.LabDoor.z);
        if (door.sqrMagnitude < 1f) door = b.LabCenter;
        snap.Climb = ForestLandscapeMetrics.Climb(ground, cfg, rin, 96, entry, door, snap.Slope);
        snap.Los = ForestLandscapeMetrics.Los(b.Zones, b.Links, b.TreeColliderPositions,
            b.CoverPods, ground, b.Entry, b.LabDoor);
        snap.Poi = ForestLandscapeMetrics.PoiZones(b.Zones, b.TreeColliderPositions,
            b.CoverPods, b.Links, ground, cfg);

        var poiAxes = PoiAxes(b);
        snap.Patches = ForestLandscapeMetrics.Patches(b.TreeColliderPositions, rin,
            b.Zones, poiAxes, b.Hydro, ground, cfg, b.CrownCover);
        snap.Roads = ForestLandscapeMetrics.Roads(b.Roads, b.Pois.All, ground, cfg);
        snap.Water = ForestLandscapeMetrics.WaterLairs(LairPositions(b), b.Waterholes,
            b.CoverPods, b.Hydro, ground, cfg, rin);
        snap.Hydro = ForestLandscapeMetrics.Hydro(b);
        snap.Budget = ForestLandscapeMetrics.Budget(b);
        UnityEngine.Object.DestroyImmediate(cfg);
        return snap;
    }

    public static DomeGenConfigSO ConfigFor(ForestBuilder b)
    {
        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.mapDiameter = b.mapDiameter;
        cfg.chunkSize = b.chunkSize;
        cfg.worldVersion = b.WorldVersion;
        cfg.walkMaxDegrees = b.Contract.walkMaxDegrees;
        cfg.maxSlopeDegrees = b.Contract.wallMinDegrees;
        return cfg;
    }

    public static List<Vector2> PoiAxes(ForestBuilder b)
    {
        var list = new List<Vector2>();
        foreach (var g in b.Gates) list.Add(g.center);
        list.Add(new Vector2(b.Entry.x, b.Entry.z));
        if (b.LabDoor != Vector3.zero)
            list.Add(new Vector2(b.LabDoor.x, b.LabDoor.z));
        return list;
    }

    public static List<Vector3> LairPositions(ForestBuilder b)
    {
        var list = new List<Vector3>();
        foreach (var l in b.Lairs) if (l != null) list.Add(l.transform.position);
        return list;
    }

    /// <summary>
    /// Карта высот и трёх классов уклона из ЕДИНСТВЕННОГО пола <see cref="ForestBuilder.SampleGround"/>:
    /// зелёный — проходимо, оранжевый — лазаемо (22–35°), красный — стена (&gt;35°).
    /// PNG пишется вне Assets: это отчёт, не ассет.
    /// </summary>
    public static void WriteMap(ForestBuilder b, string path, int size = 512)
    {
        if (b == null || b.BuiltRoot == null) return;
        var cfg = ConfigFor(b);
        float rin = DomeRockRing.InnerRadius(cfg);
        size = Mathf.Clamp(size, 64, 2048);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        var heights = new float[size * size];
        float min = float.MaxValue, max = float.MinValue;
        for (int iz = 0; iz < size; iz++)
            for (int ix = 0; ix < size; ix++)
            {
                float x = Mathf.Lerp(-rin, rin, ix / (size - 1f));
                float z = Mathf.Lerp(-rin, rin, iz / (size - 1f));
                float y = b.SampleGround(x, z);
                heights[iz * size + ix] = y;
                if (y < min) min = y;
                if (y > max) max = y;
            }
        for (int iz = 0; iz < size; iz++)
            for (int ix = 0; ix < size; ix++)
            {
                float x = Mathf.Lerp(-rin, rin, ix / (size - 1f));
                float z = Mathf.Lerp(-rin, rin, iz / (size - 1f));
                if (x * x + z * z > rin * rin)
                {
                    colors[iz * size + ix] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }
                float t = max > min ? Mathf.InverseLerp(min, max, heights[iz * size + ix]) : 0f;
                var c = Color.Lerp(new Color(0.10f, 0.25f, 0.12f),
                    new Color(0.95f, 0.92f, 0.78f), t);
                float slope = SlopeClasses.SlopeAt(cfg, x, z,
                    (a, b2) => b.SampleGround(a, b2), SlopeClasses.ClassEpsilon);
                var cls = SlopeClasses.Classify(slope, cfg);
                if (cls == SlopeClass.Climbable)
                    c = Color.Lerp(c, new Color(0.95f, 0.65f, 0.15f), 0.55f);
                else if (cls == SlopeClass.Wall)
                    c = Color.Lerp(c, new Color(0.75f, 0.12f, 0.12f), 0.75f);
                colors[iz * size + ix] = c;
            }
        tex.SetPixels(colors);
        tex.Apply();
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(cfg);
    }
}
