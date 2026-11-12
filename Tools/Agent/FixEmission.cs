using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FixEmission
{
    public static string Run()
    {
        string[] paths =
        {
            "Assets/_Chimera/Materials/SnakeBody.mat",
            "Assets/_Chimera/Materials/SnakeRattle.mat",
        };
        var sb = new System.Text.StringBuilder();
        foreach (string path in paths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { sb.AppendLine("missing " + path); continue; }
            var kw = new LocalKeyword(mat.shader, "_EMISSION");
            mat.EnableKeyword(kw);
            if (mat.HasProperty("_EmissionColor") && mat.GetColor("_EmissionColor").maxColorComponent <= 0f)
                mat.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(mat);
            sb.AppendLine(path + " emission=" + mat.IsKeywordEnabled("_EMISSION"));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return sb.ToString();
    }
}