using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ForestSandbox / ForestBuilder: slice C wiring done by the agent (scene only, game code untouched).
/// Writes speciesPool (exactly one SpeciesSO) and the snake lair tuning, then saves ONLY ForestSandbox.
/// The species asset is addressed by GUID (AssetDatabase.GUIDToAssetPath) so this file stays ASCII and
/// the Cyrillic asset path never has to travel through a shell argument.
/// </summary>
public static class ForestSpawnerSetup
{
    const string SandboxPath = "Assets/Scenes/ForestSandbox.unity";
    const string SnakeGuid   = "dab52963bf459da4b934b85751aeac96"; // Assets/_Chimera/Data/Змея.asset

    public static string Apply()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        if (!scene.IsValid() || scene.path != SandboxPath) return "OPEN FAILED: " + scene.path;
        sb.AppendLine("opened: " + scene.path + " | scenes open = " + EditorSceneManager.loadedSceneCount);

        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest == null) return "FAILED: no root object 'Forest'";
        var builder = forest.GetComponent<ForestBuilder>();
        if (builder == null) return "FAILED: 'Forest' has no ForestBuilder";
        sb.AppendLine("builder: Forest@" + forest.transform.position + " seed=" + builder.seed +
                      " randomSeed=" + builder.randomSeed + " diameter=" + builder.mapDiameter);

        // ---- speciesPool ----------------------------------------------------------------
        var snakePath = AssetDatabase.GUIDToAssetPath(SnakeGuid);
        var snake = AssetDatabase.LoadAssetAtPath<SpeciesSO>(snakePath);
        if (snake == null) return "FAILED: no SpeciesSO at guid " + SnakeGuid + " (path='" + snakePath + "')";
        var file = System.IO.Path.GetFileNameWithoutExtension(snakePath);
        sb.AppendLine("asset: " + snakePath + " | speciesName='" + snake.speciesName + "'" +
                      " | fileName='" + file + "' | match=" + (snake.speciesName == file) +
                      " | mass=" + snake.mass + " | formScale=" + snake.formScale);

        var so = new SerializedObject(builder);

        var pool = so.FindProperty("speciesPool");
        if (pool == null) return "FAILED: no serialized field speciesPool";
        int before = pool.arraySize;
        pool.arraySize = 1;
        pool.GetArrayElementAtIndex(0).objectReferenceValue = snake;

        // ---- snake lair tuning (slice C defaults) ---------------------------------------
        var props = new[]
        {
            so.FindProperty("snakeCapacity"),
            so.FindProperty("snakeTier"),
            so.FindProperty("snakeHomeRadius"),
            so.FindProperty("snakeSpawnRadius"),
            so.FindProperty("snakeFearRadius"),
            so.FindProperty("snakeRespawnSeconds"),
        };
        foreach (var p in props)
            if (p == null) return "FAILED: missing serialized field on ForestBuilder";
        props[0].intValue = 3;
        props[1].intValue = 2;
        props[2].floatValue = 12f;
        props[3].floatValue = 3f;
        props[4].floatValue = 18f;
        props[5].floatValue = 30f;

        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("speciesPool: " + before + " -> " + pool.arraySize + " elements");

        // ---- read back from the component itself (not from the SerializedObject) --------
        sb.AppendLine("--- readback before save ---");
        sb.AppendLine("pool.Count=" + builder.speciesPool.Count +
                      " pool[0]=" + (builder.speciesPool.Count > 0 && builder.speciesPool[0] != null
                          ? builder.speciesPool[0].speciesName : "NULL"));
        sb.AppendLine("tuning: capacity=" + builder.snakeCapacity + " tier=" + builder.snakeTier +
                      " home=" + builder.snakeHomeRadius + " spawn=" + builder.snakeSpawnRadius +
                      " fear=" + builder.snakeFearRadius + " respawn=" + builder.snakeRespawnSeconds);

        // ---- save ONLY the sandbox ------------------------------------------------------
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("SaveScene(" + scene.path + ") = " + saved + " | dirty now = " + scene.isDirty);
        sb.AppendLine("scenes open after save = " + EditorSceneManager.loadedSceneCount +
                      " | active = " + SceneManager.GetActiveScene().path);
        return sb.ToString();
    }

    /// <summary>Re-open the scene from disk and dump what is actually serialized.</summary>
    public static string Read()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        sb.AppendLine("opened: " + scene.path);
        foreach (var g in scene.GetRootGameObjects())
            sb.AppendLine("  root: " + g.name + " :: " +
                          string.Join(", ", g.GetComponents<Component>().Select(c => c.GetType().Name)));

        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest == null) return sb.AppendLine("FAILED: no 'Forest'").ToString();
        var b = forest.GetComponent<ForestBuilder>();
        if (b == null) return sb.AppendLine("FAILED: no ForestBuilder").ToString();

        sb.AppendLine("seed=" + b.seed + " randomSeed=" + b.randomSeed + " diameter=" + b.mapDiameter +
                      " quads=" + b.terrainQuads + " buildOnAwake=" + b.buildOnAwake +
                      " config=" + (b.config == null ? "null" : b.config.name) +
                      " playerSpawn=" + (b.playerSpawn == null ? "null" : b.playerSpawn.name));
        sb.AppendLine("speciesPool.Count=" + b.speciesPool.Count);
        for (int i = 0; i < b.speciesPool.Count; i++)
            sb.AppendLine("  pool[" + i + "]=" + (b.speciesPool[i] == null ? "NULL" : b.speciesPool[i].speciesName) +
                          " path=" + (b.speciesPool[i] == null ? "-" : AssetDatabase.GetAssetPath(b.speciesPool[i])));
        sb.AppendLine("tuning: capacity=" + b.snakeCapacity + " tier=" + b.snakeTier +
                      " home=" + b.snakeHomeRadius + " spawn=" + b.snakeSpawnRadius +
                      " fear=" + b.snakeFearRadius + " respawn=" + b.snakeRespawnSeconds);
        sb.AppendLine("scene dirty = " + scene.isDirty + " | scenes open = " + EditorSceneManager.loadedSceneCount);
        return sb.ToString();
    }
}
