using System.Collections.Generic; using System.Linq; using UnityEngine;
public static class Pick
{
    /// Луч камеры стенда через точку кадра (доли 0..1, y снизу) — первые пересечения с гранями ВСЕХ рендереров (скиннинг запечён),
    /// с нормалью: «что за грань в этом пикселе». ГОЧА: вне съёмки у камеры аспект окна (1.33), а кадр снимается своим
    /// (1600×1000 → 1.6) — без `aspect` луч бьёт мимо по X.
    public static string Run(float vx, float vy, float aspect)
    {
        var cam = GameObject.Find("СтендCam").GetComponent<Camera>();
        cam.aspect = aspect; var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0));
        var hits = new List<(float d, string n, Vector3 nrm, Vector3 p)>();
        foreach (var r in Object.FindObjectsByType<Renderer>())
        {
            if (!r.enabled) continue;
            Mesh m; Matrix4x4 M;
            if (r is SkinnedMeshRenderer s) { m = new Mesh(); s.BakeMesh(m, true); M = s.transform.localToWorldMatrix; }
            else { var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; m = mf.sharedMesh; M = r.transform.localToWorldMatrix; }
            var v = m.vertices; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = M.MultiplyPoint3x4(v[t[i]]), b = M.MultiplyPoint3x4(v[t[i+1]]), c = M.MultiplyPoint3x4(v[t[i+2]]);
                Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(ray.direction, e2); float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-9f) continue; float inv = 1 / det; Vector3 tv = ray.origin - a;
                float u = Vector3.Dot(tv, pv) * inv; if (u < 0 || u > 1) continue; Vector3 q = Vector3.Cross(tv, e1);
                float w = Vector3.Dot(ray.direction, q) * inv; if (w < 0 || u + w > 1) continue; float d = Vector3.Dot(e2, q) * inv;
                if (d > 0) hits.Add((d, r.name + "[" + m.name + "]", Vector3.Cross(e1, e2).normalized, ray.origin + ray.direction * d));
            }
        }
        cam.ResetAspect(); return string.Join(" | ", hits.OrderBy(h => h.d).Take(5).Select(h => $"{h.n} d={h.d:F3} n={h.nrm.ToString("F2")} p={h.p.ToString("F3")}"));
    }
}
