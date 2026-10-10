using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>ВЫГРУЗКА СОБРАННОГО ТЕЛА В OBJ — для опытов вне Unity (упрощение оболочки, рендер в Blender). Тело строится
/// настоящим билдером (`MorphBuilder.Build`), как в игре; оболочка поля (`SkinnedMeshRenderer`, запечённая в позе) и
/// детали-блоки (остальные рендереры) пишутся в два файла, в мировых метрах тела, оси Unity → OBJ с отражением X
/// (правая система). Данные вида не правятся.
///
///   unity command run_script --file Tools/Agent/ExportBody.cs --entry ExportBody.Run \
///     --args '["Assets/_Chimera/Data/Волк.asset","C:/путь/volk"]'     → volk-field.obj, volk-parts.obj</summary>
public static class ExportBody
{
    public static string Run(string speciesAsset, string outStem)
    {
        var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(speciesAsset);
        if (sp == null) return "вида нет: " + speciesAsset;
        var go = new GameObject("~ВЫГРУЗКА");
        try
        {
            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            using (BodyMesh.FieldOnly()) MorphBuilder.Build(go.transform, sp, sp.organs?.Where(o => o != null).ToList(), null);
            var field = new StringBuilder(); var parts = new StringBuilder();
            int vf = 0, vp = 0, tf = 0, tp = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Mesh m; bool skinned = r is SkinnedMeshRenderer;
                if (r is SkinnedMeshRenderer s) { m = new Mesh(); s.BakeMesh(m, true); }
                else m = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null || !m.isReadable) continue;
                var sb = skinned ? field : parts;
                int baseV = skinned ? vf : vp;
                sb.AppendLine("o " + r.name.Replace(' ', '_'));
                foreach (var v in m.vertices)
                {
                    var w = r.transform.TransformPoint(v) - go.transform.position;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:0.#####} {1:0.#####} {2:0.#####}", -w.x, w.y, w.z));
                }
                var t = m.triangles;
                for (int i = 0; i < t.Length; i += 3)   // отражение X меняет обход — меняем порядок вершин
                    sb.AppendLine($"f {baseV + t[i] + 1} {baseV + t[i + 2] + 1} {baseV + t[i + 1] + 1}");
                if (skinned) { vf += m.vertexCount; tf += t.Length / 3; Object.DestroyImmediate(m); }
                else { vp += m.vertexCount; tp += t.Length / 3; }
            }
            File.WriteAllText(outStem + "-field.obj", field.ToString());
            File.WriteAllText(outStem + "-parts.obj", parts.ToString());
            return $"поле {tf} тр, детали {tp} тр → {outStem}-field.obj, -parts.obj";
        }
        finally { Object.DestroyImmediate(go); }
    }
}
