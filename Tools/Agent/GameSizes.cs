using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>РАЗМЕРЫ ТЕЛ, КАК В ИГРЕ: человек, человек со всем набором органов вида (ступень 2 по составу), чистый вид
/// (ступень 3) — собраны настоящим `MorphBuilder`, габарит по нарисованным рендерерам: высота (Y), ширина (X), длина (Z).
/// Путь сборки тот же, что в игре: меш вида + ключ + черты, где они есть; поле — у видов без меша.</summary>
public static class GameSizes
{
    public static string Run(string outPath)
    {
        var all = AssetDatabase.FindAssets("t:SpeciesSO", new[] { "Assets/_Chimera/Data" })
                               .Select(g => AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null && s.organs != null).ToList();
        var human = all.First(s => s.speciesName == "Человек");
        var sb = new StringBuilder("тело\tступень\tпуть\tвысота м\tширина м\tдлина м\tрост по массе ×\tдолжно быть м\n");
        var own = new System.Collections.Generic.Dictionary<SpeciesSO, float>();   // рост чистого шасси — база для «должно быть»
        void Row(string name, string st, SpeciesSO chassis, System.Collections.Generic.List<Organ> worn)
        {
            var go = new GameObject("~размер");
            try
            {
                var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
                MorphBuilder.Build(go.transform, chassis, worn, null);
                // по ВЕРШИНАМ, а не по рамкам рендереров: рамка скин-меша в редакторе бывает с запасом
                var pts = new System.Collections.Generic.List<Vector3>();
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                { var m = new Mesh(); r.BakeMesh(m, true); foreach (var v in m.vertices) pts.Add(r.transform.TransformPoint(v)); Object.DestroyImmediate(m); }
                foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null) foreach (var v in f.sharedMesh.vertices) pts.Add(f.transform.TransformPoint(v));
                var b = new Bounds(pts[0], Vector3.zero); foreach (var v in pts) b.Encapsulate(v);
                bool mesh = go.GetComponentsInChildren<MeshRenderer>(true).Length == 0;
                var (target, step) = GlobalLayer.Step(chassis, worn);
                // рост по массе (спека `2026-10-20-rost-i-gabarity-vidov.md`) в сборку ещё не включён: колонка — цель, к которой тело придёт
                float g = GlobalLayer.Growth(chassis, worn);
                if (!own.ContainsKey(chassis)) own[chassis] = b.size.y;
                sb.Append($"{name}\t{st} (по составу: {step})\t{(mesh ? "меш" : "поле")}\t{b.size.y:0.00}\t{b.size.x:0.00}\t{b.size.z:0.00}\t{g:0.000}\t{own[chassis] * g:0.00}\n");
            }
            finally { Object.DestroyImmediate(go); }
        }
        Row("Человек", "0", human, human.organs.Where(o => o != null).ToList());
        foreach (var sp in all.Where(s => s != human))
        {
            var worn = human.organs.Where(o => o != null).Select(o => o.chassisOnly ? o : sp.organs.FirstOrDefault(w => w != null && w.slot == o.slot && !w.chassisOnly) ?? o).ToList();
            Row("Человек + все органы: " + sp.speciesName, "2", human, worn);
            Row(sp.speciesName, "3 (чистый вид)", sp, sp.organs.Where(o => o != null).ToList());
        }
        System.IO.File.WriteAllText(outPath, sb.ToString());
        return sb.ToString();
    }
}
