using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Task 3 большого павильона (27.10): новая сцена Assets/Scenes/ForestPavilion.unity.
/// Копия ForestSandbox (A/C НЕ меняется), профиль Ø2000/сид 1337 с включённой географией:
/// терраса + каньон, лаборатория с планом, климат, овраги выключены, пул видов пуст.
/// Все поля пишутся через SerializedObject — отсутствующее поле падает, а не молчит.
/// </summary>
public static class ForestPavilionScene
{
    const string SandboxPath = "Assets/Scenes/ForestSandbox.unity";
    const string FragmentPath = "Assets/Scenes/ForestFragment.unity";
    const string PavilionPath = "Assets/Scenes/ForestPavilion.unity";
    const string SamplePath = "Assets/Scenes/SampleScene.unity";

    // ---------- создание -----------------------------------------------------------
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var open in Enumerable.Range(0, SceneManager.sceneCount)
                     .Select(SceneManager.GetSceneAt))
            if (open.isDirty)
                return "STOP: scene " + open.path + " is dirty, refusing to copy";

        var sandbox = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        sb.AppendLine("source opened: " + sandbox.path + " dirty=" + sandbox.isDirty);

        // SaveScene as copy: ForestSandbox остаётся активной и НЕ меняется.
        bool copied = EditorSceneManager.SaveScene(sandbox, PavilionPath, true);
        sb.AppendLine("copy -> " + PavilionPath + ": " + copied +
                      " (source dirty=" + sandbox.isDirty + ")");
        if (!copied) return sb.ToString();

        var scene = EditorSceneManager.OpenScene(PavilionPath, OpenSceneMode.Single);
        sb.AppendLine("opened " + scene.path);

        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        var fb = forest != null ? forest.GetComponent<ForestBuilder>() : null;
        if (fb == null) return sb.ToString() + "FAILED: ForestBuilder missing";

        var so = new SerializedObject(fb);
        // Сид и размер — как A/C: тот же мир, другая география.
        SetLong(so, "seed", 1337L);
        SetBool(so, "randomSeed", false);
        SetFloat(so, "mapDiameter", 2000f);
        SetFloat(so, "chunkSize", 250f);
        SetInt(so, "terrainQuads", 48);
        SetBool(so, "buildOnAwake", true);
        SetBool(so, "spawnAtEntry", true);
        SetBool(so, "buildAsh", true);
        SetBool(so, "buildRavines", false);
        SetBool(so, "buildLabComplex", true);
        SetBool(so, "buildLabPlan", true);
        SetInt(so, "labPlanVersion", 1);
        SetBool(so, "buildClimate", true);
        SetFloat(so, "climateTimeMinutes", 10.5f);
        SetFloat(so, "climateTimeScale", 0f);
        SetBool(so, "climateAutoWeather", false);
        SetBool(so, "buildTerrace", true);
        SetBool(so, "buildCanyon", true);
        SetFloat(so, "minLabDistance", 150f);
        SetEnum(so, "labStyle", (int)LabStyle.Bastion);
        SetEnum(so, "labEntrance", (int)LabEntrance.Rock);

        // Никаких чужих ссылок из Sandbox: мир параметрический.
        SetObject(so, "config", null);
        SetObject(so, "playerSpawn", null);
        SetObject(so, "snakePrefab", null);
        var pool = so.FindProperty("speciesPool");
        if (pool == null) return sb.ToString() + "FAILED: speciesPool field missing";
        pool.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("ForestBuilder written: Ø2000 seed=1337 chunk=250 quads=48 " +
                      "buildOnAwake/spawnAtEntry/buildAsh=true ravine=false " +
                      "labComplex/labPlan/climate/terrace/canyon=true minLabDistance=150 pool=0");

        // Камеры: в игре одна Main Camera, стендовые выключены (ForestSceneCameraTests).
        foreach (var g in scene.GetRootGameObjects())
        {
            var cam = g.GetComponent<Camera>();
            if (cam == null) continue;
            bool main = g.name == "Main Camera";
            cam.enabled = main;
            if (g.name == "TopCam")
            {
                cam.orthographic = true;
                cam.orthographicSize = 1200f;
                cam.farClipPlane = 6000f;
            }
            else if (g.name == "SandboxCam" || main)
            {
                cam.farClipPlane = 6000f;
            }
            sb.AppendLine("cam " + g.name + ": enabled=" + cam.enabled +
                          " tag=" + g.tag + " far=" + cam.farClipPlane);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("saved=" + saved);
        sb.Append(ReadbackFromAsset());
        return sb.ToString();
    }

    // ---------- build settings -----------------------------------------------------
    public static string BuildSettings()
    {
        var sb = new StringBuilder();
        var all = EditorBuildSettings.scenes.ToList();
        var ordered = new List<EditorBuildSettingsScene>();
        void Place(string path)
        {
            var existing = all.FirstOrDefault(s => s.path == path);
            if (existing != null) existing.enabled = true;
            else existing = new EditorBuildSettingsScene(path, true);
            ordered.Add(existing);
        }
        Place(SamplePath);     // 0
        Place(SandboxPath);    // 1
        Place(FragmentPath);   // 2
        Place(PavilionPath);   // 3
        foreach (var s in all)
        {
            bool already = ordered.Any(o => o.path == s.path);
            if (!already) ordered.Add(s);
        }

        EditorBuildSettings.scenes = ordered.ToArray();
        // В headless запись в память не доезжает до ProjectSettings без явного Save Project.
        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("File/Save Project");
        AssetDatabase.Refresh();
        sb.AppendLine("build(memory)=" + BuildSettingsLine());
        sb.AppendLine("build(FILE)=" + BuildSettingsFromDisk());
        return sb.ToString();
    }

    // ---------- проверка -----------------------------------------------------------
    public static string Verify()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(PavilionPath, OpenSceneMode.Single);
        sb.AppendLine("=== ForestPavilion ===");
        sb.AppendLine("dirty=" + scene.isDirty);
        var roots = scene.GetRootGameObjects();
        sb.AppendLine("roots: " + string.Join(", ", roots.Select(Describe)));
        sb.AppendLine("counts: Player=" + roots.Count(g => g.name == "Player") +
                      " Forest=" + roots.Count(g => g.name == "Forest") +
                      " cameras=" + roots.Count(g => g.GetComponent<Camera>() != null) +
                      " enabledCameras=" + roots.Count(g =>
                      {
                          var c = g.GetComponent<Camera>();
                          return c != null && c.enabled;
                      }));

        var forest = roots.FirstOrDefault(g => g.name == "Forest");
        var fb = forest != null ? forest.GetComponent<ForestBuilder>() : null;
        if (fb == null) sb.AppendLine("ForestBuilder: MISSING");
        else
        {
            sb.AppendLine("  " + DumpBuilder(fb));
            var so = new SerializedObject(fb);
            sb.AppendLine("  ASSERT " +
                A(so, "seed", p => p.longValue == 1337L, "1337") +
                A(so, "randomSeed", p => !p.boolValue, "false") +
                A(so, "mapDiameter", p => Mathf.Approximately(p.floatValue, 2000f), "2000") +
                A(so, "chunkSize", p => Mathf.Approximately(p.floatValue, 250f), "250") +
                A(so, "terrainQuads", p => p.intValue == 48, "48") +
                A(so, "buildOnAwake", p => p.boolValue, "true") +
                A(so, "spawnAtEntry", p => p.boolValue, "true") +
                A(so, "buildAsh", p => p.boolValue, "true") +
                A(so, "buildRavines", p => !p.boolValue, "false") +
                A(so, "buildLabComplex", p => p.boolValue, "true") +
                A(so, "buildLabPlan", p => p.boolValue, "true") +
                A(so, "buildClimate", p => p.boolValue, "true") +
                A(so, "buildTerrace", p => p.boolValue, "true") +
                A(so, "buildCanyon", p => p.boolValue, "true") +
                A(so, "minLabDistance", p => Mathf.Approximately(p.floatValue, 150f), "150") +
                A(so, "speciesPool", p => p.arraySize == 0, "0"));
        }

        foreach (var g in roots)
        {
            var cam = g.GetComponent<Camera>();
            if (cam == null) continue;
            sb.AppendLine("  cam " + g.name + ": enabled=" + cam.enabled +
                          " tag=" + g.tag + " ortho=" + cam.orthographic +
                          " far=" + cam.farClipPlane);
        }

        sb.AppendLine("build(memory)=" + BuildSettingsLine());
        sb.AppendLine("build(FILE)=" + BuildSettingsFromDisk());
        return sb.ToString();
    }

    // ---------- helpers ------------------------------------------------------------
    static string ReadbackFromAsset()
    {
        var scene = EditorSceneManager.OpenScene(PavilionPath, OpenSceneMode.Single);
        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        var fb = forest != null ? forest.GetComponent<ForestBuilder>() : null;
        return "readback from ASSET:\n" + (fb != null ? DumpBuilder(fb) : "ForestBuilder: MISSING\n");
    }

    static string DumpBuilder(ForestBuilder fb)
    {
        return "seed=" + fb.seed + " randomSeed=" + fb.randomSeed +
               " mapDiameter=" + fb.mapDiameter + " chunkSize=" + fb.chunkSize +
               " terrainQuads=" + fb.terrainQuads + " buildOnAwake=" + fb.buildOnAwake +
               " spawnAtEntry=" + fb.spawnAtEntry + " buildAsh=" + fb.buildAsh +
               " buildRavines=" + fb.buildRavines +
               " buildLabComplex=" + fb.buildLabComplex + " buildLabPlan=" + fb.buildLabPlan +
               " buildClimate=" + fb.buildClimate +
               " buildTerrace=" + fb.buildTerrace + " buildCanyon=" + fb.buildCanyon +
               " minLabDistance=" + fb.minLabDistance +
               " pool=[" + string.Join(",", fb.speciesPool.Select(s => s != null ? s.name : "null")) + "]" +
               " snakePrefab=" + (fb.snakePrefab == null ? "null" : fb.snakePrefab.name);
    }

    static string BuildSettingsLine() =>
        string.Join(" | ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "+" : "-") + s.path));

    static string BuildSettingsFromDisk()
    {
        const string path = "ProjectSettings/EditorBuildSettings.asset";
        if (!System.IO.File.Exists(path)) return "FILE MISSING";
        var lines = System.IO.File.ReadAllLines(path);
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "m_Scenes:") continue;
            string t = lines[i].Trim();
            if (!t.StartsWith("- enabled:")) continue;
            string enabled = t.Substring("- enabled:".Length).Trim();
            string scene = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            if (scene.StartsWith("path:")) scene = scene.Substring("path:".Length).Trim();
            sb.Append((sb.Length > 0 ? " | " : "") + (enabled == "1" ? "+" : "-") + scene);
        }
        return sb.ToString();
    }

    static string Describe(GameObject g)
    {
        var comps = g.GetComponents<Component>()
            .Select(c => c.GetType().Name)
            .Where(n => n != "Transform");
        return g.name + "[" + string.Join(",", comps) + "]";
    }

    static string A(SerializedObject so, string path,
        System.Func<SerializedProperty, bool> ok, string want)
    {
        var p = so.FindProperty(path);
        if (p == null) return path + "=NO_FIELD ";
        return path + "=" + (ok(p) ? "ok " : "MISMATCH(want " + want + ") ");
    }

    static void SetBool(SerializedObject so, string path, bool v)
    {
        var p = so.FindProperty(path) ?? throw new System.Exception("no field " + path);
        p.boolValue = v;
    }
    static void SetFloat(SerializedObject so, string path, float v)
    {
        var p = so.FindProperty(path) ?? throw new System.Exception("no field " + path);
        p.floatValue = v;
    }
    static void SetInt(SerializedObject so, string path, int v)
    {
        var p = so.FindProperty(path) ?? throw new System.Exception("no field " + path);
        p.intValue = v;
    }
    static void SetLong(SerializedObject so, string path, long v)
    {
        var p = so.FindProperty(path) ?? throw new System.Exception("no field " + path);
        p.longValue = v;
    }
    static void SetEnum(SerializedObject so, string path, int v)
    {
        var p = so.FindProperty(path) ?? throw new System.Exception("no field " + path);
        p.enumValueIndex = v;
    }
    static void SetObject(SerializedObject so, string path, Object v)
    {
        var p = so.FindProperty(path) ?? throw new System.Exception("no field " + path);
        p.objectReferenceValue = v;
    }
}
