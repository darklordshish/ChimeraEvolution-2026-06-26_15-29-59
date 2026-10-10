using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>СКЕЛЕТ ВИДА В ПОЗЕ ПРИВЯЗКИ — для рига меша вида в Blender (спека `2026-10-09-chistyj-vid-meshem.md`, путь А).
/// Тело строится настоящим билдером, как в игре; кости — те трансформы, на которые скиннится и оболочка поля
/// (`BoneMesher.Build`: имя кости графа, левая сторона пары — `.L`). Меш, привязанный к арматуре ровно с этими именами и
/// этой позой, игра поставит на свой скелет по именам — ни перевода, ни подгонки.
///
/// Координаты — метры тела от его корня на земле, оси Unity → Blender: X_b = −X_u, Y_b = −Z_u, Z_b = Y_u (как у
/// `ExportBody`: морда в −Y Blender). Для каждой кости: голова (начало), хвост (начало + ось кости × длина) и вектор
/// «вверх» кадра кости (её +Z) — по нему ставится крен (roll) в Blender. Отдельно `sockets` — гнёзда органов: узел
/// места под костью (имя места, кость-носитель, точка); к ним игра вешает детали органов, скиннить на них нельзя.
///
///   unity command run_script --file Tools/Agent/SkeletonExport.cs --entry SkeletonExport.Run \
///     --args '["Assets/_Chimera/Data/Волк.asset","C:/путь/volk-skelet.json"]'</summary>
public static class SkeletonExport
{
    public static string Run(string speciesAsset, string outPath)
    {
        var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(speciesAsset);
        if (sp == null) return "вида нет: " + speciesAsset;
        var len = sp.bones.Where(b => b != null).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First().length);
        var go = new GameObject("~СКЕЛЕТ");
        try
        {
            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(go.transform, sp, sp.organs?.Where(o => o != null).ToList(), null);
            var skeleton = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Skeleton");
            if (skeleton == null) return "скелет не найден — тело собрано без поля?";
            string V(Vector3 u) => string.Format(CultureInfo.InvariantCulture, "[{0:0.#####},{1:0.#####},{2:0.#####}]", -u.x, -u.z, u.y);
            var sb = new StringBuilder();
            sb.Append("{\"species\":\"").Append(sp.speciesName).Append("\",\"axes\":\"blender: x=-x_unity, y=-z_unity, z=y_unity\",\"bones\":[");
            // КОСТИ РИГА — ровно те, на которые скиннится оболочка (`smr.bones`). Под ними же висят узлы органов
            // (`Ноги`, `Пасть`, `глаза` — имена мест, повторяются): это не кости, а ГНЁЗДА, они идут отдельным списком
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.rootBone == skeleton);
            if (smr == null) return "оболочки поля нет — костей рига взять неоткуда";
            var rig = new System.Collections.Generic.HashSet<Transform>(smr.bones);
            bool first = true;
            foreach (var t in smr.bones)
            {
                string baseName = t.name.EndsWith(".L") ? t.name.Substring(0, t.name.Length - 2) : t.name;
                float l = len.TryGetValue(baseName, out var v) ? v : 0.05f;
                Vector3 head = go.transform.InverseTransformPoint(t.position);
                Vector3 tail = go.transform.InverseTransformPoint(t.position + t.up * l);
                Vector3 roll = go.transform.InverseTransformDirection(t.forward);
                string parent = t.parent == skeleton ? "" : t.parent.name;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"name\":\"").Append(t.name).Append("\",\"parent\":\"").Append(parent)
                  .Append("\",\"head\":").Append(V(head)).Append(",\"tail\":").Append(V(tail)).Append(",\"z_axis\":").Append(V(roll)).Append('}');
            }
            sb.Append("],\"sockets\":[");
            first = true; int sockets = 0;
            foreach (var t in skeleton.GetComponentsInChildren<Transform>(true).Where(t => t != skeleton && !rig.Contains(t) && rig.Contains(t.parent)))
            {
                if (!first) sb.Append(',');
                first = false; sockets++;
                sb.Append("{\"name\":\"").Append(t.name).Append("\",\"bone\":\"").Append(t.parent.name)
                  .Append("\",\"at\":").Append(V(go.transform.InverseTransformPoint(t.position))).Append('}');
            }
            sb.Append("]}");
            File.WriteAllText(outPath, sb.ToString());
            return $"{sp.speciesName}: костей {smr.bones.Length}, гнёзд {sockets} → {outPath}";
        }
        finally { Object.DestroyImmediate(go); }
    }
}
