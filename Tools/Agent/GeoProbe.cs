using UnityEngine;

public static class GeoProbe
{
    public static string Run()
    {
        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.mapDiameter = 2000f;
        cfg.chunkSize = 250f;
        cfg.seed = 1337;
        var hydro = DomeHydro.Build(cfg, 256);
        Vector2 entry = new Vector2(-870f, 500f);
        Vector2 lab = new Vector2(890f, 670f);
        float rin = DomeRockRing.InnerRadius(cfg);
        var t = DomeTerrace.Build(cfg, 1337, lab, entry, hydro, rin);
        var c = DomeCanyon.Build(cfg, 1337, hydro, entry, lab, rin, t);
        return $"terrace={t.valid} c={t.center} r={t.radius} h={t.height} lake={hydro.lakeCenter} lr={hydro.lakeRadius} | canyon={c.valid} mouth={c.mouth} lip={c.lip} dist={Vector2.Distance(c.mouth, t.center)}";
    }
}
