using System.Linq; using System.Text; using UnityEditor; using UnityEngine;
public static class CheckW
{
    /// Сколько вершин детали держит каждая кость (по весам FBX как его видит Unity). path — FBX детали в Assets.
    public static string Run(string path)
    {
        AssetDatabase.Refresh();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var sb = new StringBuilder();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bw = smr.sharedMesh.boneWeights; var cnt = new int[smr.bones.Length];
            foreach (var w in bw) { if (w.weight0 > 0) cnt[w.boneIndex0]++; if (w.weight1 > 0) cnt[w.boneIndex1]++; if (w.weight2 > 0) cnt[w.boneIndex2]++; if (w.weight3 > 0) cnt[w.boneIndex3]++; }
            sb.Append(smr.name + ": ");
            for (int i = 0; i < cnt.Length; i++) if (cnt[i] > 0) sb.Append(smr.bones[i].name + " " + cnt[i] + ", ");
            sb.Append("; ");
        }
        return sb.ToString();
    }
}
