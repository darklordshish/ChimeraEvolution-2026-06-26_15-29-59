using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Срезы всей фигуры плоскостями Y = h: каждый треугольник (поле и блоки) даёт отрезок пересечения с плоскостью, проекции
/// отрезков на X сливаются в куски — воздух между рукой и корпусом виден разрывом. По куску: X от..до и Z от..до.
public static class Bands
{
    const string Rig = "~СТЕНД";
    [System.Serializable] class Delivery { public string species; public string[] hides; public Bone[] nodes; }
    static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;

    /// ЛУЧ ВДОЛЬ +Z через (x, y): самое переднее пересечение с гранями всех рендереров — как меряют механики (письмо 23.09c §2).
    /// points: "x,y;x,y;..." — выдаёт z переда (или «нет»)
    public static string RayZ(string species, string graph, string head, string organs, float cell, string points)
    {
        var tris = Build(species, graph, head, organs, cell, out var root);
        var sb = new StringBuilder();
        foreach (var p in points.Split(';'))
        {
            var xy = p.Split(','); float x = float.Parse(xy[0], CI), y = float.Parse(xy[1], CI);
            // ЛУЧ ПО ОСИ ПРОХОДИТ МИМО ГРАНЕЙ: на сетке main по оси тела стоит ребро (узлы на ±полклетки), и луч в x = 0
            // скользит по нему, возвращая заднюю стенку головы (письмо механик 23.09f). Осевые точки — со сдвигом 2.5 мм
            if (Mathf.Abs(x) < 0.001f) x = 0.0025f;
            float best = float.NegativeInfinity;
            foreach (var t in tris)
            {
                // барицентрика в плоскости XY
                Vector2 a = new Vector2(t[0].x, t[0].y), b = new Vector2(t[1].x, t[1].y), c = new Vector2(t[2].x, t[2].y), q = new Vector2(x, y);
                float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float l1 = ((b.y - c.y) * (q.x - c.x) + (c.x - b.x) * (q.y - c.y)) / d;
                float l2 = ((c.y - a.y) * (q.x - c.x) + (a.x - c.x) * (q.y - c.y)) / d;
                float l3 = 1 - l1 - l2;
                if (l1 < 0 || l2 < 0 || l3 < 0) continue;
                float z = l1 * t[0].z + l2 * t[1].z + l3 * t[2].z;
                if (z > best) best = z;
            }
            sb.AppendFormat(CI, "({0:0.000}, {1:0.000}): {2} | ", x, y, float.IsNegativeInfinity(best) ? "нет" : best.ToString("0.000", CI));
        }
        Object.DestroyImmediate(root);
        return sb.ToString();
    }

    /// ЛУЧ ВДОЛЬ +Y через (x, z) снизу: все пересечения с гранями по высоте — низ подбородка, горло, макушка
    public static string RayY(string species, string graph, string head, string organs, float cell, string points)
    {
        var tris = Build(species, graph, head, organs, cell, out var root);
        var sb = new StringBuilder();
        foreach (var p in points.Split(';'))
        {
            var xz = p.Split(','); float x = float.Parse(xz[0], CI), z = float.Parse(xz[1], CI);
            if (Mathf.Abs(x) < 0.001f) x = 0.0025f;   // см. RayZ: ребро по оси
            var hits = new List<float>();
            foreach (var t in tris)
            {
                Vector2 a = new Vector2(t[0].x, t[0].z), b = new Vector2(t[1].x, t[1].z), c = new Vector2(t[2].x, t[2].z), q = new Vector2(x, z);
                float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float l1 = ((b.y - c.y) * (q.x - c.x) + (c.x - b.x) * (q.y - c.y)) / d;
                float l2 = ((c.y - a.y) * (q.x - c.x) + (a.x - c.x) * (q.y - c.y)) / d;
                float l3 = 1 - l1 - l2;
                if (l1 < 0 || l2 < 0 || l3 < 0) continue;
                hits.Add(l1 * t[0].y + l2 * t[1].y + l3 * t[2].y);
            }
            hits.Sort();
            sb.AppendFormat(CI, "({0:0.000}, {1:0.000}): {2} | ", x, z, string.Join(" ", hits.Select(h => h.ToString("0.000", CI))));
        }
        Object.DestroyImmediate(root);
        return sb.ToString();
    }

    static List<Vector3[]> Build(string species, string graph, string head, string organs, float cell, out GameObject root)
    {
        foreach (var g in Object.FindObjectsByType<GameObject>().Where(o => o != null && o.name == Rig).ToList()) Object.DestroyImmediate(g);
        var src = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + species + ".asset");
        var so = Object.Instantiate(src);
        so.speciesName = species + " ~луч " + System.DateTime.Now.Ticks;
        var d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(graph));
        so.bones = d.nodes; so.skeletonHides = d.hides;
        BodyChains.ResolveRel(so.bones);   // узлы в долях (поставка 31): без этого тело строилось со сломанными узлами молча (как Kadr.cs до 01.10)
        SpeciesHandoff.ApplyLayouts(so, string.IsNullOrEmpty(head) ? null : System.IO.File.ReadAllText(head),
            string.IsNullOrEmpty(organs) ? null : System.IO.File.ReadAllText(organs));
        if (cell > 0) so.skinCell = cell;
        root = new GameObject(Rig);
        var cc = root.AddComponent<CharacterController>(); cc.height = 2f; cc.center = new Vector3(0, 1, 0);
        MorphBuilder.Build(root.transform, so, so.organs.Where(o => o != null).ToList());
        var tris = new List<Vector3[]>();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            Mesh m; Transform tr;
            if (r is SkinnedMeshRenderer smr) { m = smr.sharedMesh; tr = smr.transform.parent; }
            else { var mf = r.GetComponent<MeshFilter>(); if (mf == null) continue; m = mf.sharedMesh; tr = r.transform; }
            if (m == null) continue;
            var v = m.vertices.Select(p => tr.TransformPoint(p)).ToArray();
            var ix = m.triangles;
            for (int i = 0; i < ix.Length; i += 3) tris.Add(new[] { v[ix[i]], v[ix[i + 1]], v[ix[i + 2]] });
        }
        return tris;
    }

    public static string Run(string species, string graph, string head, string organs, float cell, string heights, float gap)
    {
        foreach (var g in Object.FindObjectsByType<GameObject>().Where(o => o != null && o.name == Rig).ToList()) Object.DestroyImmediate(g);
        var src = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + species + ".asset");
        var so = Object.Instantiate(src);
        so.speciesName = species + " ~срезы " + System.DateTime.Now.Ticks;
        var d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(graph));
        so.bones = d.nodes; so.skeletonHides = d.hides;
        BodyChains.ResolveRel(so.bones);   // узлы в долях (поставка 31): без этого тело строилось со сломанными узлами молча (как Kadr.cs до 01.10)
        SpeciesHandoff.ApplyLayouts(so, string.IsNullOrEmpty(head) ? null : System.IO.File.ReadAllText(head),
            string.IsNullOrEmpty(organs) ? null : System.IO.File.ReadAllText(organs));
        if (cell > 0) so.skinCell = cell;
        var root = new GameObject(Rig);
        var cc = root.AddComponent<CharacterController>(); cc.height = 2f; cc.center = new Vector3(0, 1, 0);
        MorphBuilder.Build(root.transform, so, so.organs.Where(o => o != null).ToList());

        var tris = new List<Vector3[]>();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            Mesh m; Transform t;
            if (r is SkinnedMeshRenderer smr) { m = smr.sharedMesh; t = smr.transform.parent; }
            else { var mf = r.GetComponent<MeshFilter>(); if (mf == null) continue; m = mf.sharedMesh; t = r.transform; }
            if (m == null) continue;
            var v = m.vertices.Select(p => t.TransformPoint(p)).ToArray();
            var ix = m.triangles;
            for (int i = 0; i < ix.Length; i += 3) tris.Add(new[] { v[ix[i]], v[ix[i + 1]], v[ix[i + 2]] });
        }
        var sb = new StringBuilder();
        foreach (var hs in heights.Split(','))
        {
            float h = float.Parse(hs, CI);
            var segs = new List<float[]>();
            foreach (var tr in tris)
            {
                var ps = new List<Vector3>();
                for (int k = 0; k < 3; k++)
                {
                    var a = tr[k]; var b = tr[(k + 1) % 3];
                    if ((a.y - h) * (b.y - h) < 0) { float s = (h - a.y) / (b.y - a.y); ps.Add(a + (b - a) * s); }
                }
                if (ps.Count == 2)
                    segs.Add(new[] { Mathf.Min(ps[0].x, ps[1].x), Mathf.Max(ps[0].x, ps[1].x), Mathf.Min(ps[0].z, ps[1].z), Mathf.Max(ps[0].z, ps[1].z) });
            }
            if (segs.Count == 0) { sb.AppendFormat(CI, "{0:0.00}: пусто | ", h); continue; }
            segs.Sort((p, q) => p[0].CompareTo(q[0]));
            var parts = new List<float[]>();
            foreach (var sg in segs)
            {
                var last = parts.Count > 0 ? parts[parts.Count - 1] : null;
                if (last != null && sg[0] <= last[1] + gap)
                { last[1] = Mathf.Max(last[1], sg[1]); last[2] = Mathf.Min(last[2], sg[2]); last[3] = Mathf.Max(last[3], sg[3]); }
                else parts.Add((float[])sg.Clone());
            }
            sb.AppendFormat(CI, "{0:0.00}:", h);
            foreach (var q in parts.Where(q => q[1] >= -0.001f))
                sb.AppendFormat(CI, " [x {0:0.000}..{1:0.000} ш {2:0.000} / z {3:0.000}..{4:0.000} г {5:0.000}]", q[0], q[1], q[1] - q[0], q[2], q[3], q[3] - q[2]);
            sb.Append(" | ");
        }
        Object.DestroyImmediate(root);
        return sb.ToString();
    }
}
