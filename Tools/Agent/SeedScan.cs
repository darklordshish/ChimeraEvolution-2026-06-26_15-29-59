using System.Text;
using UnityEngine;

public static class SeedScan
{
    public static string Run()
    {
        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.mapDiameter = 400f;
        cfg.chunkSize = 100f;
        var sb = new StringBuilder();
        sb.AppendLine("seed\tfound\trelaxed\tdist\tcenterX\tcenterZ\tnote");
        for (long seed = 1; seed <= 24; seed++)
        {
            cfg.seed = seed;
            var hydro = DomeHydro.Build(cfg, 256);
            Vector2 dir = -DomeFacilityLayout.HangarSpot(cfg, seed);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
            dir.Normalize();
            float radius = Mathf.Max(0f, DomeRockRing.InnerRadius(cfg) - DomeWorld.EntryInset);
            Vector2 entry = dir * radius;
            var s = DomeLabSite.Find(cfg, seed, entry, hydro, LabStyle.InSlope, 150f);
            sb.AppendLine($"{seed}\t{s.found}\t{s.relaxed}\t{s.distanceToEntry:F1}\t{s.center.x:F1}\t{s.center.y:F1}\t{s.note}");
        }
        Object.DestroyImmediate(cfg);
        return sb.ToString();
    }
}
