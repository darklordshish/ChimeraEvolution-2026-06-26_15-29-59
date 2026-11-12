using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Срез B0: две сцены леса руками агента (код мира и SampleScene не трогаем).
///   A/C — Assets/Scenes/ForestSandbox.unity: buildAsh=true, chunkSize=250, spawnAtEntry=false.
///   B0  — Assets/Scenes/ForestFragment.unity: копия ForestSandbox с профилем Ø400 / сид 4.
/// Плюс build settings: SampleScene 0, ForestSandbox 1, ForestFragment 2.
/// Каждое поле пишется ЧЕРЕЗ SerializedObject, поэтому отсутствующее поле — ошибка, а не молчание.
/// </summary>
public static class ForestB0Scene
{
    const string SandboxPath = "Assets/Scenes/ForestSandbox.unity";
    const string FragmentPath = "Assets/Scenes/ForestFragment.unity";
    const string SamplePath = "Assets/Scenes/SampleScene.unity";

    // ---------- осмотр -------------------------------------------------------------
    public static string Inspect()
    {
        var sb = new StringBuilder();
        var active = SceneManager.GetActiveScene();
        sb.AppendLine("active=" + active.path + " dirty=" + active.isDirty);
        sb.AppendLine("loaded=" + string.Join(" | ", System.Linq.Enumerable.Select(
            System.Linq.Enumerable.Range(0, SceneManager.sceneCount),
            i => SceneManager.GetSceneAt(i).path)));
        foreach (var s in new[] { SandboxPath, FragmentPath })
            sb.AppendLine(System.IO.File.Exists(s) ? "exists: " + s : "MISSING: " + s);
        sb.AppendLine("build=" + BuildSettingsLine());
        var forest = active.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest != null) sb.Append(DumpBuilder(forest.GetComponent<ForestBuilder>()));
        else sb.AppendLine("active scene has no Forest");
        return sb.ToString();
    }

    static string BuildSettingsLine()
    {
        return string.Join(" | ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "+" : "-") + s.path));
    }

    static string DumpBuilder(ForestBuilder fb)
    {
        if (fb == null) return "  ForestBuilder: MISSING\n";
        var sb = new StringBuilder();
        sb.AppendLine("  ForestBuilder: seed=" + fb.seed + " randomSeed=" + fb.randomSeed +
                      " mapDiameter=" + fb.mapDiameter + " chunkSize=" + fb.chunkSize +
                      " terrainQuads=" + fb.terrainQuads + " buildOnAwake=" + fb.buildOnAwake +
                      " spawnAtEntry=" + fb.spawnAtEntry + " buildAsh=" + fb.buildAsh);
        sb.AppendLine("    config=" + (fb.config == null ? "null" : fb.config.name) +
                      " playerSpawn=" + (fb.playerSpawn == null ? "null" : fb.playerSpawn.name) +
                      " speciesPool=[" + string.Join(",", fb.speciesPool.Select(s => s != null ? s.name : "null")) + "]");
        return sb.ToString();
    }

    // ---------- A/C: ForestSandbox -------------------------------------------------
    public static string RunSandbox()
    {
        var sb = new StringBuilder();
        if (!OpenSingle(SandboxPath, sb)) return sb.ToString();
        var scene = SceneManager.GetActiveScene();

        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest == null) return "FAILED: Forest root not found in ForestSandbox";
        var fb = forest.GetComponent<ForestBuilder>();
        if (fb == null) return "FAILED: ForestBuilder missing on Forest";

        var so = new SerializedObject(fb);
        SetBool(so, "buildAsh", true);
        SetFloat(so, "chunkSize", 250f);
        SetBool(so, "spawnAtEntry", false);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("saved=" + EditorSceneManager.SaveScene(scene));
        sb.AppendLine("readback from ASSET:");
        sb.Append(ReadBuilderFromAsset(SandboxPath));
        return sb.ToString();
    }

    // ---------- B0: ForestFragment ------------------------------------------------
    public static string RunFragment()
    {
        var sb = new StringBuilder();
        if (!OpenSingle(SandboxPath, sb)) return sb.ToString();

        var sandbox = SceneManager.GetActiveScene();
        if (sandbox.isDirty) return "FAILED: ForestSandbox is dirty before copy — A must be saved first";

        // SaveScene as copy: исходная сцена остаётся активной и НЕ меняется.
        bool copied = EditorSceneManager.SaveScene(sandbox, FragmentPath, true);
        sb.AppendLine("SaveScene as copy -> " + FragmentPath + ": " + copied +
                      " (ForestSandbox dirty=" + sandbox.isDirty + ")");

        var frag = EditorSceneManager.OpenScene(FragmentPath, OpenSceneMode.Single);
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine("opened=" + scene.path);

        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        if (forest == null) return "FAILED: Forest root not found in ForestFragment";
        var fb = forest.GetComponent<ForestBuilder>();
        if (fb == null) return "FAILED: ForestBuilder missing on Forest";

        // Сначала запоминаем Player ЭТОЙ сцены — камера должна смотреть на него, а не на чужого.
        var player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        if (player == null)
            player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                         .FirstOrDefault(t => t.name == "Player")?.gameObject;
        sb.AppendLine("Player in fragment: " + (player != null ? player.name : "MISSING"));

        var so = new SerializedObject(fb);
        SetLong(so, "seed", 4L);
        SetBool(so, "randomSeed", false);
        SetFloat(so, "mapDiameter", 400f);
        SetFloat(so, "chunkSize", 100f);
        SetInt(so, "terrainQuads", 24);
        SetBool(so, "buildOnAwake", true);
        SetBool(so, "spawnAtEntry", true);
        SetBool(so, "buildAsh", false);
        SetObject(so, "playerSpawn", null);
        SetObject(so, "config", null);
        var pool = so.FindProperty("speciesPool");
        if (pool == null) return "FAILED: speciesPool field missing";
        pool.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("ForestBuilder written: seed=4 randomSeed=false Ø400 chunk=100 quads=24 " +
                      "buildOnAwake=true spawnAtEntry=true buildAsh=false pool=0 playerSpawn=null config=null");

        // Камеры: TopCam ортo 300 / far 6000, SandboxCam и Main Camera far 6000,
        // CameraFollow.target — Player ЭТОЙ сцены.
        foreach (var g in scene.GetRootGameObjects())
        {
            var cam = g.GetComponent<Camera>();
            if (cam == null) continue;
            if (g.name == "TopCam")
            {
                cam.orthographic = true;
                cam.orthographicSize = 300f;
                cam.farClipPlane = 6000f;
                sb.AppendLine("TopCam: ortho size 300 far 6000");
            }
            else if (g.name == "SandboxCam" || g.name == "Main Camera")
            {
                cam.farClipPlane = 6000f;
                sb.AppendLine(g.name + ": far 6000");
            }
            var follow = g.GetComponent<CameraFollow>();
            if (follow != null)
            {
                var fso = new SerializedObject(follow);
                var tp = fso.FindProperty("target");
                if (tp == null) { sb.AppendLine(g.name + ": CameraFollow has no 'target'"); continue; }
                var current = tp.objectReferenceValue as Transform;
                bool wrong = current == null || current.gameObject.scene != scene;
                if (wrong)
                {
                    tp.objectReferenceValue = player != null ? player.transform : null;
                    fso.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine(g.name + ": CameraFollow.target " +
                                  (current == null ? "was NULL" : "was FOREIGN (" + current.name + " @ " +
                                   current.gameObject.scene.path + ")") +
                                  " -> " + (player != null ? player.name : "null"));
                }
                else sb.AppendLine(g.name + ": CameraFollow.target already local (" + current.name + ")");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("saved=" + EditorSceneManager.SaveScene(scene));
        return sb.ToString();
    }

    // ---------- build settings ----------------------------------------------------
    public static string RunBuildSettings()
    {
        var sb = new StringBuilder();
        var scenes = EditorBuildSettings.scenes.ToList();

        // SampleScene — 0 (не трогаем содержимое, только место), ForestSandbox — 1, ForestFragment — 2.
        var ordered = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        void Place(string path, int index)
        {
            var existing = scenes.FirstOrDefault(s => s.path == path);
            if (existing != null) { existing.enabled = true; }
            else existing = new EditorBuildSettingsScene(path, true);
            ordered.Add(existing);
        }
        Place(SamplePath, 0);
        Place(SandboxPath, 1);
        Place(FragmentPath, 2);
        // всё прочее — следом, как было
        foreach (var s in scenes)
            if (ordered.All(o => o.path != s.path)) ordered.Add(s);

        EditorBuildSettings.scenes = ordered.ToArray();
        // В headless-редакторе запись в память НЕ доезжает до ProjectSettings/*.asset сама:
        // без явного Save Project файл на диске остаётся прежним (проверено 24.10).
        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("File/Save Project");
        AssetDatabase.Refresh();
        sb.AppendLine("build(memory)=" + BuildSettingsLine());
        sb.AppendLine("build(FILE)=" + BuildSettingsFromDisk());
        return sb.ToString();
    }

    /// <summary>Читает m_Scenes прямо из файла проекта — проверка, что настройки доехали до диска.</summary>
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
            if (t.StartsWith("- enabled:"))
            {
                string enabled = t.Substring("- enabled:".Length).Trim();
                string scene = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                if (scene.StartsWith("path:")) scene = scene.Substring("path:".Length).Trim();
                sb.Append((sb.Length > 0 ? " | " : "") + (enabled == "1" ? "+" : "-") + scene);
            }
        }
        return sb.ToString();
    }

    // ---------- проверка -----------------------------------------------------------
    public static string Verify()
    {
        var sb = new StringBuilder();
        // A
        var sandbox = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        sb.AppendLine("=== ForestSandbox (A/C, Ø2000) ===");
        sb.AppendLine("dirty=" + sandbox.isDirty);
        sb.Append(VerifyScene(sandbox, new Expect
        {
            Chunk = 250f, Diameter = 2000f, Quads = 16, Seed = 1337L, Ash = true,
            Entry = false, Pool = 1
        }, 1200f));
        sb.AppendLine("NOTE: A ortho size not asserted (kept as is); farClip=" + FarOf(sandbox, "TopCam"));

        // B0
        var frag = EditorSceneManager.OpenScene(FragmentPath, OpenSceneMode.Single);
        sb.AppendLine("=== ForestFragment (B0, Ø400) ===");
        sb.AppendLine("dirty=" + frag.isDirty);
        sb.Append(VerifyScene(frag, new Expect
        {
            Chunk = 100f, Diameter = 400f, Quads = 24, Seed = 4L, Ash = false,
            Entry = true, Pool = 0
        }, 300f));

        sb.AppendLine("build=" + BuildSettingsLine());
        return sb.ToString();
    }

    struct Expect
    {
        public float Chunk, Diameter; public int Quads; public long Seed;
        public bool Ash, Entry; public int Pool;
    }

    static string VerifyScene(Scene scene, Expect e, float topOrtho)
    {
        var sb = new StringBuilder();
        var roots = scene.GetRootGameObjects();
        sb.AppendLine("roots: " + string.Join(", ", roots.Select(Describe)));
        sb.AppendLine("counts: Player=" + roots.Count(g => g.name == "Player") +
                      " Forest=" + roots.Count(g => g.name == "Forest") +
                      " cameras=" + roots.Count(g => g.GetComponent<Camera>() != null) +
                      " ForestBuilder=" + roots.Count(g => g.GetComponent<ForestBuilder>() != null));

        var forest = roots.FirstOrDefault(g => g.name == "Forest");
        var fb = forest != null ? forest.GetComponent<ForestBuilder>() : null;
        if (fb != null)
        {
            sb.Append(DumpBuilder(fb));
            var so = new SerializedObject(fb);
            sb.AppendLine("  ASSERT seed=" + Eq(so, "seed", p => p.longValue == e.Seed, e.Seed.ToString()) +
                          " diameter=" + Eq(so, "mapDiameter", p => Mathf.Approximately(p.floatValue, e.Diameter), e.Diameter.ToString()) +
                          " chunk=" + Eq(so, "chunkSize", p => Mathf.Approximately(p.floatValue, e.Chunk), e.Chunk.ToString()) +
                          " quads=" + Eq(so, "terrainQuads", p => p.intValue == e.Quads, e.Quads.ToString()) +
                          " buildAsh=" + Eq(so, "buildAsh", p => p.boolValue == e.Ash, e.Ash.ToString()) +
                          " spawnAtEntry=" + Eq(so, "spawnAtEntry", p => p.boolValue == e.Entry, e.Entry.ToString()) +
                          " pool=" + Eq(so, "speciesPool", p => p.arraySize == e.Pool, e.Pool.ToString()));
            sb.AppendLine("  nulls: config=" + (fb.config == null) + " playerSpawn=" + (fb.playerSpawn == null));
        }
        else sb.AppendLine("  ForestBuilder: MISSING");

        foreach (var g in roots)
        {
            var cam = g.GetComponent<Camera>();
            if (cam == null) continue;
            sb.AppendLine("  cam " + g.name + ": ortho=" + cam.orthographic + " size=" + cam.orthographicSize +
                          " far=" + cam.farClipPlane + " local=" + (g.scene == scene));
            var follow = g.GetComponent<CameraFollow>();
            if (follow != null)
            {
                var t = new SerializedObject(follow).FindProperty("target").objectReferenceValue as Transform;
                sb.AppendLine("    target=" + (t != null ? t.name + " @ " + t.gameObject.scene.path : "NULL") +
                              " -> " + (t != null && t.gameObject.scene == scene ? "OK (this scene)" : "BROKEN"));
            }
        }
        return sb.ToString();
    }

    static string Eq(SerializedObject so, string path, System.Func<SerializedProperty, bool> ok, string want)
    {
        var p = so.FindProperty(path);
        if (p == null) return "NO FIELD(" + path + ")";
        return (ok(p) ? "ok" : "MISMATCH want " + want);
    }

    static string Describe(GameObject g)
    {
        var comps = g.GetComponents<Component>().Select(c => c.GetType().Name).Where(n => n != "Transform");
        return g.name + "[" + string.Join(",", comps) + "]";
    }

    static float FarOf(Scene scene, string name)
    {
        var g = scene.GetRootGameObjects().FirstOrDefault(x => x.name == name);
        var c = g != null ? g.GetComponent<Camera>() : null;
        return c != null ? c.farClipPlane : -1f;
    }

    static string ReadBuilderFromAsset(string path)
    {
        // Читаем ФАЙЛ, а не память: проверка «после сохранения» обязана быть про ассет.
        // Перезагружаем сцену с диска — то, что окажется в памяти, и есть сохранённое.
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        return DumpBuilder(forest != null ? forest.GetComponent<ForestBuilder>() : null);
    }

    // ---------- helpers ------------------------------------------------------------
    static bool OpenSingle(string path, StringBuilder sb)
    {
        // Не теряем чужие несохранённые правки молча: если активная сцена грязна — стоп.
        foreach (var before in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if (before.isDirty) { sb.AppendLine("STOP: scene " + before.path + " is dirty, refusing to open others"); return false; }
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        sb.AppendLine("opened " + scene.path);
        return true;
    }

    static void SetBool(SerializedObject so, string path, bool v)
    {
        var p = so.FindProperty(path); if (p == null) throw new System.Exception("no field " + path); p.boolValue = v;
    }
    static void SetFloat(SerializedObject so, string path, float v)
    {
        var p = so.FindProperty(path); if (p == null) throw new System.Exception("no field " + path); p.floatValue = v;
    }
    static void SetInt(SerializedObject so, string path, int v)
    {
        var p = so.FindProperty(path); if (p == null) throw new System.Exception("no field " + path); p.intValue = v;
    }
    static void SetLong(SerializedObject so, string path, long v)
    {
        var p = so.FindProperty(path); if (p == null) throw new System.Exception("no field " + path); p.longValue = v;
    }
    static void SetObject(SerializedObject so, string path, Object v)
    {
        var p = so.FindProperty(path); if (p == null) throw new System.Exception("no field " + path); p.objectReferenceValue = v;
    }
}
