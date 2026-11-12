using System.Text;
using UnityEngine;

public static class RockProbe
{
    public static string Run()
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null) return "no builder";
        Vector3 door = b.LabDoor;
        Vector3 center = new Vector3(b.LabCenter.x, door.y, b.LabCenter.y);
        Vector3 outward = door - center; outward.y = 0f; outward.Normalize();
        var sb = new StringBuilder();
        sb.AppendLine($"door={door} center={b.LabCenter} lake={b.Hydro.lakeLevel:F2}");
        for (int d = 0; d <= 24; d += 2)
        {
            Vector3 p = door + outward * d;
            float h = b.SampleGround(p.x, p.z);
            float best = 999f; int bi = -1;
            for (int i = 0; i < b.Hydro.riverPts.Count; i++)
            {
                float dx = b.Hydro.riverPts[i].x - p.x;
                float dz = b.Hydro.riverPts[i].y - p.z;
                float dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd < best) { best = dd; bi = i; }
            }
            sb.AppendLine($"d={d,2} p=({p.x:F1},{p.z:F1}) ground={h:F2} water={b.Hydro.lakeLevel:F2} nearestRiver={best:F1}");
        }
        return sb.ToString();
    }
}
