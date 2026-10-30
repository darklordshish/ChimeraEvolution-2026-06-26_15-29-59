using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>ОПИСЬ ВИДИМЫХ ОРГАНОВ НА ШАСИ ЧЕЛОВЕКА (30.10, ГеймБосс: долг №2 — «без него альфа-химера и коллега-босс выглядят
/// людьми»). Для каждого донора и каждого его органа (кроме хребта) химера «Человек + орган донора» собирается тем же путём,
/// что в игре (`ShotSpecies.Chimera` → `BodyProbe.ChimeraPlan` → `MorphBuilder`), и сравнивается с чистым человеком:
/// какие рендереры прибавились/пропали (кусок донора, орган на шаблоне `слот@вид`), какие формы-ключи меша включены (ключ
/// области `вид·слот`, ключ ступени), насколько изменилась рамка тела. Ничего не правит — только читает собранную сцену.</summary>
public static class OrganOpis
{
    const string H = "Assets/_Chimera/Data/Человек.asset";

    static (HashSet<string> rend, Dictionary<string, float> keys, Bounds b, int verts) Snap()
    {
        var root = GameObject.Find("~ШОТ");
        var rend = new HashSet<string>(); var keys = new Dictionary<string, float>(); var b = new Bounds(); bool first = true; int verts = 0;
        if (root == null) return (rend, keys, b, 0);
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            rend.Add(r.name);
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            if (r is SkinnedMeshRenderer s && s.sharedMesh != null)
            {
                verts += s.sharedMesh.vertexCount;
                for (int i = 0; i < s.sharedMesh.blendShapeCount; i++)
                {
                    float w = s.GetBlendShapeWeight(i);
                    if (w > 0.5f) { var n = s.sharedMesh.GetBlendShapeName(i); keys[n] = Mathf.Max(keys.TryGetValue(n, out var o) ? o : 0, w); }
                }
            }
            else if (r is MeshRenderer && r.GetComponent<MeshFilter>()?.sharedMesh != null) verts += r.GetComponent<MeshFilter>().sharedMesh.vertexCount;
        }
        return (rend, keys, b, verts);
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        ShotSpecies.Build(H, "profile"); var b0 = Snap();
        sb.AppendLine("ЧЕЛОВЕК: рендереров " + b0.rend.Count + " [" + string.Join(", ", b0.rend.OrderBy(x => x)) + "], ключи [" + string.Join(", ", b0.keys.Keys) + "], рамка " + b0.b.size.ToString("F3") + ", вершин " + b0.verts);
        foreach (var dn in new[] { "Волк", "Лось", "Ёж", "Змея" })
        {
            var donor = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + dn + ".asset");
            if (donor == null) { sb.AppendLine(dn + ": нет ассета"); continue; }
            var slots = donor.organs.Where(o => o != null && o.slot != "хребет").Select(o => o.slot).Distinct().ToList();
            foreach (var sl in slots)
            {
                string rep = ShotSpecies.Chimera(H, "Assets/_Chimera/Data/" + dn + ".asset", sl, "profile");
                var c = Snap();
                var plus = c.rend.Except(b0.rend).OrderBy(x => x).ToList(); var minus = b0.rend.Except(c.rend).OrderBy(x => x).ToList();
                var keys = c.keys.Where(kv => !b0.keys.ContainsKey(kv.Key)).Select(kv => kv.Key + " " + kv.Value.ToString("F0")).ToList();
                var d = c.b.size - b0.b.size;
                sb.AppendLine(string.Format("{0} · {1}: +[{2}] −[{3}] ключи [{4}] Δрамка ({5:F3}, {6:F3}, {7:F3}) Δвершин {8} | {9}",
                    dn, sl, string.Join(", ", plus), string.Join(", ", minus), string.Join(", ", keys), d.x, d.y, d.z, c.verts - b0.verts, rep));
            }
        }
        ShotSpecies.Wipe();
        return sb.ToString();
    }
}
