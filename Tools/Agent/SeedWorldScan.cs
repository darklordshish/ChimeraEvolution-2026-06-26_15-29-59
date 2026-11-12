using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class SeedWorldScan
{
    public static string Run()
    {
        long[] seeds = { 1, 2, 3, 4, 7, 9, 12, 18, 22, 24 };
        var sb = new StringBuilder();
        sb.AppendLine("seed\tstyle\tdist\tentryX\tentryZ\tcenterX\tcenterZ\tdoorX\tdoorZ\tchunks\ttrees\tmouths\tnests\tparts\tinterior\texterior");
        foreach (long seed in seeds)
        {
            var host = new GameObject("~SeedWorldScan");
            var b = host.AddComponent<ForestBuilder>();
            b.buildOnAwake = false;
            b.randomSeed = false;
            b.seed = seed;
            b.mapDiameter = 400f;
            b.chunkSize = 100f;
            b.terrainQuads = 24;
            b.buildAsh = false;
            b.spawnAtEntry = true;
            b.buildLabComplex = true;
            b.labStyle = LabStyle.InSlope;
            b.minLabDistance = 150f;
            b.speciesPool = new List<SpeciesSO>();
            b.Build();
            float dist = b.LabComplex != null
                ? Vector2.Distance(new Vector2(b.Entry.x, b.Entry.z), b.LabCenter)
                : 0f;
            int mouths = 0, nests = 0;
            for (int i = 0; i < b.Lairs.Count; i++)
            {
                if (b.Lairs[i].areaType == LairSite.LairAreaType.Den) mouths++;
                else nests++;
            }
            sb.AppendLine($"{seed}\t{b.ResolvedLabStyle}\t{dist:F1}\t{b.Entry.x:F1}\t{b.Entry.z:F1}\t" +
                          $"{b.LabCenter.x:F1}\t{b.LabCenter.y:F1}\t{b.LabDoor.x:F1}\t{b.LabDoor.z:F1}\t" +
                          $"{b.Chunks}\t{b.Trees}\t{mouths}\t{nests}\t" +
                          $"{b.LabComplex?.parts ?? 0}\t{b.LabComplex?.interiorParts ?? 0}\t{b.LabComplex?.exteriorParts ?? 0}");
            b.Clear();
            Object.DestroyImmediate(host);
        }
        return sb.ToString();
    }
}
