using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Мировые узлы графа вида (по ассету, `rel` уже выведен импортом) — для колец плеча D. Оси OBJ стенда: X зеркалим.
public static class DumpGraph
{
    public static string Run(string species, string outPath)
    {
        var so = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + species + ".asset");
        var by = new Dictionary<string, Bone>();
        foreach (var b in so.bones) by[b.name] = b;
        var done = new Dictionary<string, (Vector3, Quaternion)>();
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("[\n");
        for (int i = 0; i < so.bones.Length; i++)
        {
            var b = so.bones[i];
            var (p, r) = SkeletonBuilder.Place(b, by, done);
            var t = SkeletonBuilder.Tip(b, p, r);
            var x = r * Vector3.right;
            sb.AppendFormat(ci, "{{\"name\":\"{0}\",\"parent\":\"{1}\",\"limb\":\"{2}\",\"a\":[{3:F5},{4:F5},{5:F5}],\"b\":[{6:F5},{7:F5},{8:F5}],\"x\":[{9:F5},{10:F5},{11:F5}],\"r0\":{12:F5},\"r1\":{13:F5},\"section\":{14:F4},\"depth\":{15:F4},\"mirror\":{16}}}{17}\n",
                b.name, b.parent, b.limb, -p.x, p.y, p.z, -t.x, t.y, t.z, -x.x, x.y, x.z, b.r0, b.r1, b.section, b.depth,
                b.mirrorX ? "true" : "false", i + 1 < so.bones.Length ? "," : "");
        }
        System.IO.File.WriteAllText(outPath, sb.Append("]\n").ToString());
        return "узлов " + so.bones.Length;
    }
}
