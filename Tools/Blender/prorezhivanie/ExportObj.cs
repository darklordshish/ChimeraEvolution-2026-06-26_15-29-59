using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// ПРОТОТИП «мельче клетка + прореживание» (модельная линия, 23.09): собрать вид с графом из поставки игровым путём
/// (`SpeciesHandoff.ApplyLayouts`, `MorphBuilder.Build`) и выгрузить оболочку и блоки в OBJ (мир, Y вверх, X зеркален).
public static class ExportObj
{
    [System.Serializable] class Delivery { public string species; public string[] hides; public Bone[] nodes; }
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static string Run(string species, string graph, string head, string organs, float cell, string outPath)
    {
        var src = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + species + ".asset");
        var so = Object.Instantiate(src);
        so.speciesName = species + " ~obj " + System.DateTime.Now.Ticks;
        var d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(graph));
        so.bones = d.nodes; so.skeletonHides = d.hides;
        SpeciesHandoff.ApplyLayouts(so, string.IsNullOrEmpty(head) ? null : System.IO.File.ReadAllText(head),
            string.IsNullOrEmpty(organs) ? null : System.IO.File.ReadAllText(organs));
        so.skinCell = cell;
        var root = new GameObject("~OBJ");
        var cc = root.AddComponent<CharacterController>(); cc.height = 2f; cc.center = new Vector3(0, 1, 0);
        MorphBuilder.Build(root.transform, so, so.organs.Where(o => o != null).ToList());
        var sb = new StringBuilder();
        int vbase = 1, fieldTris = 0, blockTris = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            Mesh m; Matrix4x4 w;
            if (r is SkinnedMeshRenderer smr)
            {
                m = new Mesh(); smr.BakeMesh(m, true);
                w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                sb.AppendLine("o field"); fieldTris += m.triangles.Length / 3;
            }
            else
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                m = mf.sharedMesh; w = r.transform.localToWorldMatrix;
                sb.AppendLine("o block_" + r.name.Replace(' ', '_')); blockTris += m.triangles.Length / 3;
            }
            foreach (var v in m.vertices)
            {
                var p = w.MultiplyPoint3x4(v);
                sb.AppendFormat(CI, "v {0:0.#####} {1:0.#####} {2:0.#####}\n", -p.x, p.y, p.z);   // OBJ правый: X зеркалим
            }
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
                sb.AppendFormat("f {0} {1} {2}\n", t[i] + vbase, t[i + 2] + vbase, t[i + 1] + vbase);
            vbase += m.vertexCount;
        }
        Object.DestroyImmediate(root);
        System.IO.File.WriteAllText(outPath, sb.ToString());
        return string.Format(CI, "поле {0} тр, блоки {1} тр → {2}", fieldTris, blockTris, outPath);
    }
}
