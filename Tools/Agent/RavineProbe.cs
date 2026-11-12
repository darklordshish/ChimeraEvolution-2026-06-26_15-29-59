using System.Text;
using UnityEngine;

public static class RavineProbe
{
    public static string Run()
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null || b.RavineList.Count == 0) return "no ravines";
        var r = b.RavineList[0];
        var sb = new StringBuilder();
        sb.AppendLine($"center={r.center} len={r.length} width={r.width} depth={r.depth} rim={r.rim}");
        for (int i = -4; i <= 4; i++)
        {
            float x = r.center.x + i * 4f;
            float z = r.center.y;
            float raw = b.RawGround(x, z);
            float applied = DomeRavines.Apply(raw, b.RavineList, x, z, b.Hydro.lakeLevel);
            sb.AppendLine($"x+{i * 4}: sample={b.SampleGround(x, z):F2} raw={raw:F2} applied={applied:F2}");
        }
        return sb.ToString();
    }
}
