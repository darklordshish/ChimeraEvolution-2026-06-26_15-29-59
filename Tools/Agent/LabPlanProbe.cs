using System.Text;
using UnityEngine;

public static class LabPlanProbe
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (long seed in new long[] { 1, 4, 9 })
        {
            var p = LabPlanGenerator.Generate(seed, 1, LabEntrance.Underground, LabStyle.InSlope);
            sb.AppendLine($"seed={seed} template={p.template} fallback={p.fallback} rooms={p.rooms.Count} doors={p.doors.Count}");
            foreach (var r in p.rooms)
                sb.AppendLine($"  {r.id}: x={r.x:F1} z={r.z:F1} {r.width:F1}x{r.depth:F1}");
            foreach (var d in p.doors)
                sb.AppendLine($"  door {d.from}->{d.to}: x={d.x:F1} z={d.z:F1} w={d.width:F1}");
            var host = new GameObject("PlanProbe");
            var built = DomeLabComplex.Build(host, Vector2.zero, 0f, 0f, LabStyle.Bastion, LabEntrance.Rock, false, p);
            DomeLabDamage.Apply(built, seed, 1, p);
            sb.AppendLine($"  damage={built.damageSignature} parts={built.damageParts}");
            Object.DestroyImmediate(host);
        }
        return sb.ToString();
    }
}
