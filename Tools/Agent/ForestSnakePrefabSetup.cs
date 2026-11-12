using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Assigns the snake prefab (chain body) to ForestBuilder in ForestSandbox. Scene only.</summary>
public static class ForestSnakePrefabSetup
{
    const string SandboxPath = "Assets/Scenes/ForestSandbox.unity";
    const string SnakePrefabGuid = "8bd5a3162d1968f4d8f4fc47643aea72";

    public static string Apply()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        if (!scene.IsValid()) return "OPEN FAILED";
        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest == null) return "FAILED: no Forest";
        var builder = forest.GetComponent<ForestBuilder>();
        if (builder == null) return "FAILED: no ForestBuilder";

        var path = AssetDatabase.GUIDToAssetPath(SnakePrefabGuid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return "FAILED: no prefab at " + path;

        var so = new SerializedObject(builder);
        var p = so.FindProperty("snakePrefab");
        if (p == null) return "FAILED: no snakePrefab field";
        p.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("prefab: " + path);
        sb.AppendLine("builder.snakePrefab=" + (builder.snakePrefab != null ? builder.snakePrefab.name : "NULL"));
        sb.AppendLine("saved=" + saved + " dirty=" + scene.isDirty);
        return sb.ToString();
    }

    public static string Read()
    {
        var scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        var b = forest != null ? forest.GetComponent<ForestBuilder>() : null;
        if (b == null) return "FAILED: no ForestBuilder";
        return "snakePrefab=" + (b.snakePrefab != null ? b.snakePrefab.name : "NULL") +
               " | pool=" + b.speciesPool.Count +
               " | tuning=" + b.snakeCapacity + "/" + b.snakeRespawnSeconds;
    }
}
