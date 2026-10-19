using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>ОХВАТ ХИМЕР МЕШЕМ: каждое шасси с мешем вида × каждый донор × каждый слот (и весь набор донора) — чем
/// рисуется тело (меш шасси / поле), сколько кусков встало, какой ключ и вес, высота тела. Флаг кусков включается на
/// время прогона. Это не сторож формы, а карта: где путь «меш + куски + ключ» уже закрывает химеру, а где ещё поле.</summary>
public static class MeshSweep
{
    public static string Run(string outPath)
    {
        var all = AssetDatabase.FindAssets("t:SpeciesSO", new[] { "Assets/_Chimera/Data" })
                               .Select(g => AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null && s.organs != null).ToList();
        bool was = PartAssembly.MeshPieces;
        PartAssembly.MeshPieces = true; ChainSwap.ResetCache(); BodyMesh.ResetCache();
        var sb = new StringBuilder("шасси\tдонор\tслоты\tтело\tкусков\tключ\tвысота\n");
        int mesh = 0, field = 0;
        try
        {
            foreach (var chassis in all.Where(s => s.bodyMesh != null))
            {
                var names = new HashSet<string>(chassis.bodyMesh.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name.Split('.')[0]));
                foreach (var donor in all.Where(d => d != chassis))
                {
                    var slots = donor.organs.Where(o => o != null && !o.chassisOnly && chassis.organs.Any(c => c != null && c.slot == o.slot)).Select(o => o.slot).Distinct().ToList();
                    var sets = slots.Select(s => new List<string> { s }).ToList();
                    sets.Add(slots);
                    foreach (var set in sets)
                    {
                        var worn = chassis.organs.Where(o => o != null && !set.Contains(o.slot)).ToList();
                        foreach (var s in set) worn.Insert(0, donor.organs.First(o => o != null && o.slot == s));
                        var go = new GameObject("~охват");
                        try
                        {
                            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
                            MorphBuilder.Build(go.transform, chassis, worn, null);
                            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                            bool onMesh = go.GetComponentsInChildren<MeshRenderer>(true).Length == 0 && smrs.Any(r => r.GetComponent<BodyPartView>() == null && names.Contains(r.name));   // у поля есть детали-блоки (MeshRenderer), у меша — нет
                            int pieces = smrs.Count(r => r.sharedMesh != null && r.sharedMesh.name.Contains("кусок"));
                            var keys = BodyMesh.KeysFor(chassis, worn);
                            var rends = go.GetComponentsInChildren<Renderer>(true);
                            float h = 0f;
                            if (rends.Length > 0) { var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds); h = b.size.y; }
                            if (onMesh) mesh++; else field++;
                            sb.Append(chassis.speciesName).Append('\t').Append(donor.speciesName).Append('\t')
                              .Append(set.Count == slots.Count && set.Count > 1 ? "ВСЕ(" + set.Count + ")" : string.Join(",", set)).Append('\t')
                              .Append(onMesh ? "меш" : "поле").Append('\t').Append(pieces).Append('\t')
                              .Append(keys.Count > 0 ? string.Join(",", keys.Select(k => k.Key + "=" + k.Value.ToString("0.##"))) : "—").Append('\t')
                              .Append(h.ToString("0.00")).Append('\n');
                        }
                        catch (System.Exception e) { sb.Append(chassis.speciesName).Append('\t').Append(donor.speciesName).Append('\t').Append(string.Join(",", set)).Append("\tОШИБКА: ").Append(e.Message).Append('\n'); }
                        finally { Object.DestroyImmediate(go); }
                    }
                }
            }
        }
        finally { PartAssembly.MeshPieces = was; ChainSwap.ResetCache(); BodyMesh.ResetCache(); }
        System.IO.File.WriteAllText(outPath, sb.ToString());
        return $"меш {mesh}, поле {field} → {outPath}";
    }
}
