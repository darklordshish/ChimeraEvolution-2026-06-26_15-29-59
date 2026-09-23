using System.Globalization;
using System.Linq;
using UnityEngine;

/// ПРОТОТИП (модельная линия, 23.09): на собранном стенде (`~СТЕНД`) подменить оболочку поля мешем из OBJ, прорежённым в Blender.
/// Меш — с плоскими гранями (вершины не делятся), материал — тот же, что у оболочки. Кадр затем снимается той же камерой.
public static class ShotObj
{
    public static string Swap(string objPath)
    {
        var rig = GameObject.Find("~СТЕНД");
        if (rig == null) return "нет стенда";
        var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>();
        var mat = smrs.First().sharedMaterial;
        var body = smrs.First().transform.parent;
        foreach (var s in smrs) s.enabled = false;
        var vs = new System.Collections.Generic.List<Vector3>();
        var tri = new System.Collections.Generic.List<int>();
        var outV = new System.Collections.Generic.List<Vector3>();
        foreach (var line in System.IO.File.ReadLines(objPath))
        {
            var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) continue;
            if (p[0] == "v") vs.Add(new Vector3(-float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture)));
            else if (p[0] == "f")
            {
                var idx = p.Skip(1).Select(q => int.Parse(q.Split('/')[0]) - 1).ToArray();
                for (int k = 1; k + 1 < idx.Length; k++)
                {   // зеркало по X меняет обход — переставляем
                    outV.Add(vs[idx[0]]); outV.Add(vs[idx[k + 1]]); outV.Add(vs[idx[k]]);
                }
            }
        }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(outV); m.SetTriangles(Enumerable.Range(0, outV.Count).ToArray(), 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        var go = new GameObject("поле-OBJ");
        go.transform.SetParent(rig.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return string.Format(CultureInfo.InvariantCulture, "поле из OBJ: {0} тр (оболочек поля скрыто {1})", outV.Count / 3, smrs.Length);
    }
}
