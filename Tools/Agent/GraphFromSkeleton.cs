using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>СКЕЛЕТ В МЕТРАХ → УЗЛЫ ГРАФА — обратное к `SkeletonExport`. Модельная линия переставляет кости внутрь меша
/// вида (`Docs/models/rig/<вид>-skelet.json`: начало, конец и «верх» каждой кости в метрах тела, оси Blender), а граф вида
/// записан по правилам `SkeletonBuilder` и `BodyChains.ResolveRel`: корень — точкой и поворотом, остальные — от родителя
/// (начало в кадре родителя, поворот относительно него), узлы шаблона — в долях родителя (`rel.at/offX/offZ/len`).
/// Инструмент считает эти числа из новых позиций и пишет их списком; `Docs/tools/graph_patch.py` вносит их в
/// `<вид>-graph.json`. Радиусы в долях (`rel.r0/r1`) и все прочие поля узла не трогаются — это форма поля, не поза.
/// Мышцы (`endBone`) пропускаются: их длина и угол выводятся из креплений.
///
///   unity command run_script --file Tools/Agent/GraphFromSkeleton.cs --entry GraphFromSkeleton.Run \
///     --args '["Assets/_Chimera/Data/Лось.asset","C:/.../los-skelet.json","C:/.../los-graph-poza.json"]'</summary>
public static class GraphFromSkeleton
{
    class SB { public string name, parent; public float[] head, tail, z_axis; }


    static Vector3 U(float[] b) => new(-b[0], b[2], -b[1]);   // Blender → Unity: X = −X_b, Y = Z_b, Z = −Y_b
    static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"{{\"x\":{F(v.x)},\"y\":{F(v.y)},\"z\":{F(v.z)}}}";

    public static string Run(string speciesAsset, string skeletJson, string outPath)
    {
        var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(speciesAsset);
        if (sp == null) return "вида нет: " + speciesAsset;
        // разбор без JsonUtility: тот на этом файле молча отдаёт пустой список (14.10). Берём из каждого объекта кости
        // поля name, head, tail, z_axis регулярными выражениями — формат пишет наш же SkeletonExport или генератор линии
        var text = File.ReadAllText(skeletJson);
        var bonesPart = text.Substring(text.IndexOf("\"bones\""));
        int socketsAt = bonesPart.IndexOf("\"sockets\"");
        if (socketsAt > 0) bonesPart = bonesPart.Substring(0, socketsAt);
        var target = new Dictionary<string, SB>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(bonesPart, @"\{[^{}]*\}"))
        {
            var obj = m.Value;
            string Str(string key) { var r = System.Text.RegularExpressions.Regex.Match(obj, "\"" + key + @"""\s*:\s*""([^""]*)"""); return r.Success ? r.Groups[1].Value : null; }
            float[] Arr(string key)
            {
                var r = System.Text.RegularExpressions.Regex.Match(obj, "\"" + key + @"""\s*:\s*\[([^\]]*)\]");
                return r.Success ? r.Groups[1].Value.Split(',').Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray() : null;
            }
            var b = new SB { name = Str("name"), parent = Str("parent"), head = Arr("head"), tail = Arr("tail"), z_axis = Arr("z_axis") };
            if (!string.IsNullOrEmpty(b.name) && b.head != null && b.tail != null && b.z_axis != null && !target.ContainsKey(b.name)) target[b.name] = b;
        }

        // поза каждой кости из позиций: +Y — вдоль кости, +Z — «верх» кадра (ортогонализованный)
        var pose = new Dictionary<string, (Vector3 head, Quaternion rot, float len)>();
        foreach (var (name, b) in target)
        {
            Vector3 h = U(b.head), t = U(b.tail), z = U(b.z_axis);
            var up = (t - h).normalized;
            var fwd = Vector3.ProjectOnPlane(z, up).normalized;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            pose[name] = (h, Quaternion.LookRotation(fwd, up), (t - h).magnitude);
        }

        var by = sp.bones.Where(b => b != null).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
        var newLen = new Dictionary<string, float>();
        var sb = new StringBuilder("{\"species\":\"").Append(sp.speciesName).Append("\",\"nodes\":[");
        int n = 0, skipped = 0;
        foreach (var b in sp.bones)
        {
            if (b == null || !string.IsNullOrEmpty(b.endBone)) continue;
            if (!pose.TryGetValue(b.name, out var me)) { skipped++; continue; }
            newLen[b.name] = me.len;
            string body;
            if (string.IsNullOrEmpty(b.parent) || !pose.TryGetValue(b.parent, out var pp))
                body = $"\"origin\":{V(me.head)},\"dir\":{V(me.rot.eulerAngles)},\"length\":{F(me.len)}";
            else
            {
                var inv = Quaternion.Inverse(pp.rot);
                var o = inv * (me.head - pp.head);
                var dir = (inv * me.rot).eulerAngles;
                if (b.rel != null && b.rel.On && by.TryGetValue(b.parent, out var p))
                {
                    float plen = pp.len > 1e-5f ? pp.len : p.length;
                    float at = o.y / plen;
                    float r = BodyChains.RadiusAt(p, at);
                    float offX = r * p.section > 1e-6f ? o.x / (r * p.section) : 0f;
                    float offZ = r * p.depth > 1e-6f ? o.z / (r * p.depth) : 0f;
                    body = $"\"dir\":{V(dir)},\"rel\":{{\"at\":{F(at)},\"offX\":{F(offX)},\"offZ\":{F(offZ)},\"len\":{F(me.len / plen)}}}";
                }
                else body = $"\"freeOrigin\":true,\"origin\":{V(o)},\"dir\":{V(dir)},\"length\":{F(me.len)}";
            }
            if (n++ > 0) sb.Append(',');
            sb.Append("{\"name\":\"").Append(b.name).Append("\",").Append(body).Append('}');
        }
        sb.Append("]}");
        File.WriteAllText(outPath, sb.ToString());
        return $"{sp.speciesName}: узлов {n}, без позиции в скелете {skipped} → {outPath}";
    }
}
