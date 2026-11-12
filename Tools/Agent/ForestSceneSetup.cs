using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Лес, срез A: собрать ForestSandbox руками агента (сцена и build settings, код мира не трогаем).
/// ForestBuilder в (0,0,0) с сид-полями; Player и Main Camera копируются из SampleScene
/// аддитивным открытием (SampleScene НЕ сохраняется), затем исходная сцена закрывается без сохранения.
/// Повторный запуск идемпотентен: старые копии в ForestSandbox удаляются.
/// </summary>
public static class ForestSceneSetup
{
    const string SandboxPath = "Assets/Scenes/ForestSandbox.unity";
    const string SamplePath = "Assets/Scenes/SampleScene.unity";

    public static string Connect()
    {
        return Run();
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        var sandbox = SceneManager.GetActiveScene();
        if (sandbox.path != SandboxPath)
            return "ACTIVE SCENE IS NOT ForestSandbox: " + sandbox.path;

        // ---- 1. Forest root with ForestBuilder -------------------------------------------------
        var forest = sandbox.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest == null)
        {
            forest = new GameObject("Forest");
            SceneManager.MoveGameObjectToScene(forest, sandbox);
            sb.AppendLine("Forest: created");
        }
        else sb.AppendLine("Forest: reused");

        forest.transform.position = Vector3.zero;
        forest.transform.rotation = Quaternion.identity;
        forest.transform.localScale = Vector3.one;

        var builder = forest.GetComponent<ForestBuilder>();
        if (builder == null) { builder = forest.AddComponent<ForestBuilder>(); sb.AppendLine("ForestBuilder: added"); }
        else sb.AppendLine("ForestBuilder: reused");

        var so = new SerializedObject(builder);
        so.FindProperty("seed").longValue = 1337L;
        so.FindProperty("randomSeed").boolValue = false;
        so.FindProperty("mapDiameter").floatValue = 2000f;
        so.FindProperty("terrainQuads").intValue = 16;
        so.FindProperty("buildOnAwake").boolValue = true;
        so.FindProperty("config").objectReferenceValue = null;
        so.FindProperty("playerSpawn").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        var surface = forest.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        sb.AppendLine("NavMeshSurface: " + (surface != null ? "present (RequireComponent)" : "MISSING"));

        // ---- 2. wipe stale copies in the sandbox (idempotency) --------------------------------
        foreach (var stale in sandbox.GetRootGameObjects().Where(g => g != forest && (g.name == "Player" || g.name == "Main Camera")).ToArray())
        {
            sb.AppendLine("sandbox: removed stale " + stale.name);
            Object.DestroyImmediate(stale);
        }

        // ---- 3. open SampleScene additively (never saved) -------------------------------------
        var sample = SceneManager.GetSceneByPath(SamplePath);
        bool openedHere = false;
        if (!sample.IsValid() || !sample.isLoaded)
        {
            sample = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        sb.AppendLine("SampleScene additively: " + (openedHere ? "opened for copy" : "already open"));

        var srcPlayer = sample.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                              .FirstOrDefault(t => t.name == "Player");
        var srcCam = sample.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                           .FirstOrDefault(t => t.name == "Main Camera");
        if (srcPlayer == null)
        {
            if (openedHere) EditorSceneManager.CloseScene(sample, true);
            return "FAILED: Player not found in SampleScene";
        }

        // ---- 4. Player clone ------------------------------------------------------------------
        var player = Object.Instantiate(srcPlayer.gameObject);
        player.name = "Player";
        SceneManager.MoveGameObjectToScene(player, sandbox);
        player.transform.position = srcPlayer.position;
        player.transform.rotation = srcPlayer.rotation;
        player.transform.localScale = Vector3.one;
        sb.AppendLine("Player: cloned, components = " + string.Join(", ",
            player.GetComponents<Component>().Select(c => c.GetType().Name)));

        // ---- 5. Main Camera clone + retarget --------------------------------------------------
        Camera cam = null;
        if (srcCam != null)
        {
            var camGo = Object.Instantiate(srcCam.gameObject);
            camGo.name = "Main Camera";
            SceneManager.MoveGameObjectToScene(camGo, sandbox);
            camGo.transform.position = srcCam.position;
            camGo.transform.rotation = srcCam.rotation;
            cam = camGo.GetComponent<Camera>();
            var follow = camGo.GetComponent<CameraFollow>();
            if (follow != null)
            {
                var fso = new SerializedObject(follow);
                fso.FindProperty("target").objectReferenceValue = player.transform;
                fso.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("CameraFollow.target -> " + player.name + " (scene " + sandbox.path + ")");
            }
            sb.AppendLine("Main Camera: cloned, components = " + string.Join(", ",
                camGo.GetComponents<Component>().Select(c => c.GetType().Name)));
        }
        else sb.AppendLine("Main Camera: NOT FOUND in SampleScene");

        // ---- 6. close SampleScene without saving ---------------------------------------------
        if (openedHere)
        {
            EditorSceneManager.CloseScene(sample, true);
            sb.AppendLine("SampleScene: closed WITHOUT saving (dirty=" + sample.isDirty + ")");
        }

        // ---- 7. cameras ----------------------------------------------------------------------
        if (cam != null) { cam.farClipPlane = 6000f; sb.AppendLine("Main Camera: farClip 6000"); }
        foreach (var g in sandbox.GetRootGameObjects())
        {
            if (g.name == "TopCam")
            {
                var c = g.GetComponent<Camera>();
                if (c != null) { c.farClipPlane = 6000f; c.orthographic = true; c.orthographicSize = 1200f; }
                sb.AppendLine("TopCam: farClip 6000, ortho, size 1200");
            }
            else if (g.name == "SandboxCam")
            {
                var c = g.GetComponent<Camera>();
                if (c != null) c.farClipPlane = 6000f;
                sb.AppendLine("SandboxCam: farClip 6000 (3/4 kept)");
            }
        }

        // ---- 8. one directional light: drop scene Sun, DomeSun is built by DomeWorld -----------
        var oldSun = sandbox.GetRootGameObjects().FirstOrDefault(g => g.name == "Sun");
        if (oldSun != null)
        {
            var light = oldSun.GetComponent<Light>();
            sb.AppendLine("Sun: removed (case=" + (light != null ? light.type.ToString() : "no Light") +
                          ") - DomeWorld builds DomeSun, DomeSkyRig dims foreign directionals");
            Object.DestroyImmediate(oldSun);
            RenderSettings.sun = null;
        }
        else sb.AppendLine("Sun: already absent");

        // ---- 9. build settings: SampleScene stays 0, ForestSandbox goes to 1 -----------------
        var scenes = EditorBuildSettings.scenes.ToList();
        var existing = scenes.FirstOrDefault(s => s.path == SandboxPath);
        if (existing != null)
        {
            existing.enabled = true;
            scenes.RemoveAll(s => s != existing && s.path == SandboxPath);
        }
        else
        {
            scenes.Add(new EditorBuildSettingsScene(SandboxPath, true));
        }
        var idx = scenes.FindIndex(s => s.path == SandboxPath);
        if (idx > 1)
        {
            var entry = scenes[idx];
            scenes.RemoveAt(idx);
            scenes.Insert(1, entry);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        sb.AppendLine("BuildSettings: " + string.Join(" | ", EditorBuildSettings.scenes.Select(s =>
            (s.enabled ? "+" : "-") + s.path)));

        // ---- 10. save ONLY the sandbox ------------------------------------------------------
        EditorSceneManager.MarkSceneDirty(sandbox);
        bool saved = EditorSceneManager.SaveScene(sandbox);
        sb.AppendLine("Saved ForestSandbox: " + saved);

        // ---- 11. verification readout -------------------------------------------------------
        sb.AppendLine("--- roots in ForestSandbox ---");
        foreach (var g in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            sb.AppendLine("  " + g.name + " :: " + string.Join(", ",
                g.GetComponents<Component>().Select(c => c.GetType().Name)));
        }
        var p = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        var fp = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        var mc = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Main Camera");
        sb.AppendLine("check Player count=" + SceneManager.GetActiveScene().GetRootGameObjects().Count(g => g.name == "Player"));
        if (p != null)
        {
            sb.AppendLine("check Player: CC=" + (p.GetComponent<CharacterController>() != null) +
                          " PC=" + (p.GetComponent<PlayerController>() != null) +
                          " CreatureBody=" + (p.GetComponent<CreatureBody>() != null));
        }
        if (fp != null)
        {
            var fb = fp.GetComponent<ForestBuilder>();
            sb.AppendLine("check Forest: pos=" + fp.transform.position +
                          " seed=" + fb.seed + " randomSeed=" + fb.randomSeed +
                          " diameter=" + fb.mapDiameter + " quads=" + fb.terrainQuads +
                          " buildOnAwake=" + fb.buildOnAwake +
                          " playerSpawn=" + (fb.playerSpawn == null ? "null" : "SET"));
        }
        if (mc != null)
        {
            var cf = mc.GetComponent<CameraFollow>();
            var cso = cf != null ? new SerializedObject(cf) : null;
            var tp = cso != null ? cso.FindProperty("target") : null;
            var tgt = tp != null ? tp.objectReferenceValue as Transform : null;
            sb.AppendLine("check Camera: far=" + mc.GetComponent<Camera>().farClipPlane +
                          " target=" + (tgt != null ? tgt.name : "NULL") +
                          " inSandbox=" + (tgt != null && tgt.gameObject.scene == SceneManager.GetActiveScene()));
        }
        sb.AppendLine("SampleScene dirty check: " + (System.IO.File.Exists("Assets/Scenes/SampleScene.unity") ? "file present" : "missing"));
        return sb.ToString();
    }
}
