using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Инварианты корпусов (s10f-1): боксы замкнуты, проёмы есть, крыша только где положено,
/// POI не пересекаются, ангар в кольце лицом в центр. Пусто = чисто.
/// </summary>
public static class DomeFacilityRules
{
    public static List<string> Check(DomeFacilityLayout.Facility f, DomeGenConfigSO cfg)
    {
        var issues = new List<string>();
        if (f == null) { issues.Add("раскладка — null"); return issues; }
        if (f.parts.Count < 40) issues.Add($"деталей мало: {f.parts.Count} (жди 60+)");
        if (f.parts.Count > 400) issues.Add($"деталей много: {f.parts.Count} (кит распух)");
        bool hasArena = false, hasBars = false, hasTable = false, hasHangar = false, hasRoof = false;
        foreach (var p in f.parts)
        {
            if (p.size.x <= 0f || p.size.y <= 0f || p.size.z <= 0f)
            {
                issues.Add($"нулевой габарит детали {p.mesh}");
                break;
            }
            if (p.mesh == DomeFacilityLayout.FacMesh.Bars) hasBars = true;
            if (p.mat == DomeFacilityLayout.FacMat.Void) hasHangar = true;
        }
        foreach (var poi in f.pois)
        {
            if (poi.id == "Arena") hasArena = true;
            if (poi.id == "Surgery") hasTable = true;
        }
        foreach (var p in f.parts)
            if (p.mat == DomeFacilityLayout.FacMat.Dark && p.size.y < 0.5f && p.size.x > 5f) { hasRoof = true; break; }
        if (!hasArena) issues.Add("нет POI арены");
        if (!hasBars) issues.Add("нет решёток клеток");
        if (!hasTable) issues.Add("нет POI хирургии");
        if (!hasHangar) issues.Add("нет шторки ангара");
        if (!hasRoof) issues.Add("нет крыш боксов");
        for (int i = 0; i < f.pois.Count; i++)
            for (int j = i + 1; j < f.pois.Count; j++)
            {
                float d = Vector2.Distance(f.pois[i].pos, f.pois[j].pos);
                if (d < f.pois[i].radius + f.pois[j].radius && f.pois[i].id != "Lab" && f.pois[j].id != "Lab")
                    issues.Add($"POI пересекаются: {f.pois[i].id} × {f.pois[j].id}");
            }
        if (cfg != null)
        {
            float r = f.hangarPos.magnitude;
            float rin = cfg.mapDiameter * 0.5f - 80f;
            float rout = cfg.mapDiameter * 0.5f + 140f;
            if (r < rin || r > rout) issues.Add($"ангар вне кольца: r={r:F0}");
            Vector2 toCenter = (f.labCenter - f.hangarPos).normalized;
            Vector2 facing = new Vector2(
                Mathf.Sin(f.hangarYaw * Mathf.Deg2Rad), Mathf.Cos(f.hangarYaw * Mathf.Deg2Rad));
            if (Vector2.Dot(toCenter, facing) < 0.99f) issues.Add("ангар смотрит не в центр");
        }
        return issues;
    }
}
