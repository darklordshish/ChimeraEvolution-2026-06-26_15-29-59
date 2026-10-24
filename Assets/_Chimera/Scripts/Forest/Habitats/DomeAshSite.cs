using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ясень-исполин и сова (s10g-1): место — 100–400м от лаборатории, уклон <15°,
/// суша (вне озера и русла); гнездо — LairSite «Сова»/Nest в кроне.
/// Числа ясеня, геометрия и конфигуратор логова — один источник для превью и рантайма.
/// </summary>
public static class DomeAshSite
{
    // Принятые числа ясеня «под панораму» (14.10). Один источник для превью и рантайма:
    // высота 85 м, крона 140 м в поперечнике, ствол 16 м на середине высоты, корни 50 м.
    public const float Height = 85f;
    public const float CrownDiameter = 140f;
    public const float CrownHeight = 50f;
    public const float CrownBase = 39f;
    public const float TrunkDiameterMid = 16f;
    public const float TrunkTop = 42f;
    public const float RootSpread = 50f;
    public const float NestHeight = 36f;
    public const float NestLateral = 12f;

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

    /// <summary>
    /// Геометрия ясеня по принятым числам: ствол 42 м (16 м в поперечнике на середине),
    /// крона-купол 140 м в размахе с верхом на 85 м, корни 50 м у земли, присада-гнездо.
    /// Один источник для DomePreview и DomeWorld; меши можно отдать track на снос.
    /// </summary>
    public static GameObject BuildGeometry(Transform parent, Vector3 ground,
        Material bark, Material crown, Material owl, ICollection<Mesh> track = null)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (bark == null) throw new ArgumentNullException(nameof(bark));
        if (crown == null) throw new ArgumentNullException(nameof(crown));
        if (owl == null) throw new ArgumentNullException(nameof(owl));

        var root = new GameObject("Ash");
        root.transform.SetParent(parent, false);
        // ground — координаты поля, то есть локальные для корня мира.
        root.transform.localPosition = ground;

        // Ствол: r 12 у земли → 4 на 42 м; на середине r 8 = диаметр 16.
        float r0 = TrunkDiameterMid * 0.75f;
        float r1 = TrunkDiameterMid * 0.25f;
        Part(root.transform, "AshTrunk", FloraMeshKit.Trunk(r0, r1, TrunkTop), bark,
            Vector3.zero, track, withCollider: true);
        Part(root.transform, "AshRoots", FloraMeshKit.AshRoots(RootSpread * 0.5f, 10), bark,
            Vector3.zero, track);
        // Верх кроны = CrownBase + 0.92·CrownHeight = 39 + 46 = 85 м.
        Part(root.transform, "AshCrown",
            FloraMeshKit.AshCrownWide(CrownDiameter * 0.5f, CrownHeight), crown,
            new Vector3(0f, CrownBase, 0f), track);

        // Присада-гнездо: ветвь от ствола, площадка и болванка совы (масштаб, не модель).
        Part(root.transform, "AshBranch", FloraMeshKit.Slab(4.5f, 0.4f, 0.8f), bark,
            new Vector3(NestLateral * 0.55f, NestHeight - 0.5f, 0f), track);
        Part(root.transform, "AshNestPerch", FloraMeshKit.Slab(3.6f, 0.35f, 3.6f), bark,
            new Vector3(NestLateral, NestHeight - 0.2f, 0f), track);
        Part(root.transform, "AshOwl", FloraMeshKit.Icosahedron(0.55f), owl,
            new Vector3(NestLateral, NestHeight + 0.75f, 0f), track);
        Part(root.transform, "AshOwlHead", FloraMeshKit.Icosahedron(0.32f), owl,
            new Vector3(NestLateral, NestHeight + 1.35f, 0.35f), track);
        return root;
    }

    static void Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos,
        ICollection<Mesh> track, bool withCollider = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (withCollider)
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
        if (track != null) track.Add(mesh);
    }
}
