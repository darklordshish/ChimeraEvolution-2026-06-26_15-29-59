using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Починка лесных сцен перед плейтестом (01.11). Только сцены и ProjectSettings,
/// C# игры не трогает. Запуск:
///   unity command run_script --file Tools/Agent/PlaytestFix.cs --entry PlaytestFix.Run
///   unity command run_script --file Tools/Agent/PlaytestFix.cs --entry PlaytestFix.Inspect
///   unity command run_script --file Tools/Agent/PlaytestFix.cs --entry PlaytestFix.NavCheck
///   unity command run_script --file Tools/Agent/PlaytestFix.cs --entry PlaytestFix.PlayReport
/// </summary>
public static class PlaytestFix
{
    const string Pavilion = "Assets/Scenes/ForestPavilion.unity";
    const string Sandbox = "Assets/Scenes/ForestSandbox.unity";
    const string Fragment = "Assets/Scenes/ForestFragment.unity";
    const string _Змея = "\u0417\u043C\u0435\u044F"; // Змея — ASCII-safe литерал

    // ---------- 1. правка сцен ----------------------------------------------------
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var open in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if (open.isDirty)
                return "STOP: scene " + open.path + " is dirty, refusing to edit";

        ApplyOne(Pavilion, true, sb);
        ApplyOne(Sandbox, false, sb);
        ApplyOne(Fragment, false, sb);

        NavClimb(0.3f, sb);

        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("File/Save Project");

        sb.AppendLine();
        sb.AppendLine("--- final ---");
        sb.AppendLine("agentClimb(from NavMesh): " + NavMesh.GetSettingsByID(0).agentClimb);
        var pav = EditorSceneManager.OpenScene(Pavilion, OpenSceneMode.Single);
        sb.AppendLine("reopened " + pav.path + " dirty=" + pav.isDirty);
        sb.Append(DescribeScene(pav));
        return sb.ToString();
    }

    static void ApplyOne(string path, bool isPavilion, StringBuilder sb)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        sb.AppendLine("=== " + path + " ===");

        var roots = scene.GetRootGameObjects();
        var fb = roots.Select(g => g.GetComponent<ForestBuilder>()).FirstOrDefault(b => b != null);
        if (fb == null)
        {
            sb.AppendLine("FAIL: no ForestBuilder");
            return;
        }

        var so = new SerializedObject(fb);
        if (isPavilion)
        {
            var snake = FindSpecies(_Змея);
            var pool = so.FindProperty("speciesPool");
            if (snake == null) sb.AppendLine("FAIL: species " + _Змея + " not found");
            else
            {
                pool.arraySize = 1;
                pool.GetArrayElementAtIndex(0).objectReferenceValue = snake;
                sb.AppendLine("speciesPool = [" + snake.name + "]");
            }

            // Rock = 0; Buried = 3. Плейтест не смешиваем с непринятым Buried.
            so.FindProperty("labEntrance").enumValueIndex = (int)LabEntrance.Rock;
            sb.AppendLine("labEntrance = Rock (" + (int)LabEntrance.Rock + "); was " +
                          ((LabEntrance)fb.labEntrance));
            // Поворот лица в чашу делает SpawnPlayer; меняем только то, что просили.
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureEvolution(scene, sb);
        EnsureReward(scene, sb);
        if (isPavilion) EnsureCameras(scene, sb);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("saved=" + saved + " dirty=" + scene.isDirty);
        sb.Append(DescribeScene(scene));
    }

    static SpeciesSO FindSpecies(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO", new[] { "Assets/_Chimera/Data" }))
        {
            var s = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null && s.name == name) return s;
        }
        return null;
    }

    static List<SpeciesSO> AllSpecies()
    {
        var list = new List<SpeciesSO>();
        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO", new[] { "Assets/_Chimera/Data" }))
        {
            var s = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null) list.Add(s);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static void EnsureEvolution(Scene scene, StringBuilder sb)
    {
        var roots = scene.GetRootGameObjects();
        var comp = roots.Select(g => g.GetComponent<EvolutionConfig>()).FirstOrDefault(c => c != null);
        if (comp == null)
        {
            var go = new GameObject("Evolution");
            SceneManager.MoveGameObjectToScene(go, scene);
            comp = go.AddComponent<EvolutionConfig>();
            sb.AppendLine("Evolution: created");
        }
        else sb.AppendLine("Evolution: reused on " + comp.gameObject.name);

        var all = AllSpecies();
        var so = new SerializedObject(comp);
        var pAll = so.FindProperty("allSpecies");
        pAll.arraySize = all.Count;
        for (int i = 0; i < all.Count; i++)
            pAll.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        so.FindProperty("evolveNpc").boolValue = true;
        so.FindProperty("startAffinity").intValue = 1;
        so.FindProperty("chimerizeMultiplier").floatValue = 1f;
        so.FindProperty("verboseLog").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("Evolution: allSpecies=" + all.Count + " [" +
                      string.Join(",", all.Select(s => s.name)) + "] evolveNpc=true startAffinity=1 verboseLog=true");
    }

    static void EnsureReward(Scene scene, StringBuilder sb)
    {
        var roots = scene.GetRootGameObjects();
        var comp = roots.Select(g => g.GetComponent<ChimeraSlotReward>()).FirstOrDefault(c => c != null);
        if (comp == null)
        {
            var go = new GameObject("ChimeraSlotReward");
            SceneManager.MoveGameObjectToScene(go, scene);
            comp = go.AddComponent<ChimeraSlotReward>();
            sb.AppendLine("ChimeraSlotReward: created");
        }
        else sb.AppendLine("ChimeraSlotReward: reused on " + comp.gameObject.name);

        var so = new SerializedObject(comp);
        so.FindProperty("triggerAffinity").intValue = 100;
        so.FindProperty("slotsToGrant").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("ChimeraSlotReward: triggerAffinity=100 slotsToGrant=1");
    }

    static void EnsureCameras(Scene scene, StringBuilder sb)
    {
        foreach (var g in scene.GetRootGameObjects())
        {
            var cam = g.GetComponent<Camera>();
            if (cam == null) continue;
            bool main = g.name == "Main Camera";
            cam.enabled = main;
            sb.AppendLine("camera " + g.name + ": enabled=" + cam.enabled);
        }
    }

    static string DescribeScene(Scene scene)
    {
        var sb = new StringBuilder();
        var roots = scene.GetRootGameObjects();
        sb.AppendLine("roots: " + string.Join(", ", roots.Select(g => g.name)));
        foreach (var g in roots)
        {
            var cam = g.GetComponent<Camera>();
            if (cam != null) sb.AppendLine("  cam " + g.name + ": enabled=" + cam.enabled + " tag=" + g.tag);
        }
        var fb = roots.Select(g => g.GetComponent<ForestBuilder>()).FirstOrDefault(b => b != null);
        if (fb != null)
            sb.AppendLine("  builder: pool=[" + string.Join(",", fb.speciesPool.Select(s => s != null ? s.name : "null")) +
                          "]" +
                          " entrance=" + fb.labEntrance + " style=" + fb.labStyle +
                          " stationLoop=" + fb.buildStationLoop + " stationHill=" + fb.buildStationHill);
        var evo = roots.Select(g => g.GetComponent<EvolutionConfig>()).FirstOrDefault(c => c != null);
        if (evo != null)
        {
            var so = new SerializedObject(evo);
            var all = so.FindProperty("allSpecies");
            sb.AppendLine("  evolution: allSpecies=" + all.arraySize +
                          " evolveNpc=" + so.FindProperty("evolveNpc").boolValue +
                          " startAffinity=" + so.FindProperty("startAffinity").intValue +
                          " chimerizeMultiplier=" + so.FindProperty("chimerizeMultiplier").floatValue +
                          " verboseLog=" + so.FindProperty("verboseLog").boolValue);
        }
        var rew = roots.Select(g => g.GetComponent<ChimeraSlotReward>()).FirstOrDefault(c => c != null);
        if (rew != null)
        {
            var so = new SerializedObject(rew);
            sb.AppendLine("  reward: triggerAffinity=" + so.FindProperty("triggerAffinity").intValue +
                          " slotsToGrant=" + so.FindProperty("slotsToGrant").intValue);
        }
        var player = roots.SelectMany(g => g.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            sb.AppendLine("  player: pos=" + player.transform.position + " rot=" + player.transform.eulerAngles +
                          (cc != null ? " stepOffset=" + cc.stepOffset + " slopeLimit=" + cc.slopeLimit : " no CC"));
        }
        return sb.ToString();
    }

    // ---------- 2. ProjectSettings/NavMeshAreas --------------------------------
    static void NavClimb(float value, StringBuilder sb)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (assets == null || assets.Length == 0)
        {
            sb.AppendLine("NavMeshAreas: NOT LOADABLE from AssetDatabase");
            return;
        }
        bool any = false;
        foreach (var a in assets)
        {
            var so = new SerializedObject(a);
            var settings = so.FindProperty("m_Settings");
            if (settings == null) continue;
            for (int i = 0; i < settings.arraySize; i++)
            {
                var s = settings.GetArrayElementAtIndex(i);
                var climb = s.FindPropertyRelative("agentClimb");
                if (climb == null) continue;
                climb.floatValue = value;
                any = true;
                sb.AppendLine("agentClimb[" + i + "] = " + value);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
        }
        if (!any) sb.AppendLine("NavMeshAreas: m_Settings/agentClimb not found");
        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("File/Save Project");
    }

    /// <summary>Временная ручка для проверки: set_agent_climb 0.75 / 0.3.</summary>
    public static string SetAgentClimb(string value)
    {
        float v = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        NavClimb(v, sb);
        return sb.ToString() + " NavMesh.GetSettingsByID(0).agentClimb=" + NavMesh.GetSettingsByID(0).agentClimb;
    }

    public static string NavCheck()
    {
        var sb = new StringBuilder();
        sb.AppendLine("NavMesh.GetSettingsByID(0).agentClimb = " + NavMesh.GetSettingsByID(0).agentClimb);
        foreach (var path in new[] { Pavilion, Sandbox, Fragment })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var p = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<CharacterController>(true)).FirstOrDefault();
            sb.AppendLine(path + " characterController.stepOffset = " +
                          (p != null ? p.stepOffset.ToString() : "none"));
        }
        EditorSceneManager.OpenScene(Pavilion, OpenSceneMode.Single);
        return sb.ToString();
    }

    // ---------- 3. проверки ---------------------------------------------------
    public static string Inspect()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { Pavilion, Sandbox, Fragment })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine("=== " + path + " ===");
            sb.Append(DescribeScene(scene));
        }
        EditorSceneManager.OpenScene(Pavilion, OpenSceneMode.Single);
        return sb.ToString();
    }

    public static string PlayReport()
    {
        var sb = new StringBuilder();
        var builder = UnityEngine.Object.FindAnyObjectByType<ForestBuilder>();
        if (builder == null) return "no ForestBuilder in play";
        if (builder.BuiltRoot == null) return "world not built yet";

        sb.AppendLine("seed=" + builder.ResolvedSeed + " fingerprint=" + builder.FingerprintHex);
        sb.AppendLine("chunks=" + builder.Chunks + " trees=" + builder.Trees +
                      " colliders=" + builder.TreeColliders + " clearings=" +
                      (builder.Zones != null ? builder.Zones.Count : -1) +
                      " corridors=" + (builder.Links != null ? builder.Links.Count : -1) +
                      " pods=" + builder.CoverPodsBuilt + " crownCover=" + builder.CrownCover.ToString("F3"));
        sb.AppendLine("ravines=" + builder.Ravines + " fallenTrees=" + builder.FallenTrees +
                      " visualNests=" + builder.VisualNests + " hasAsh=" + builder.HasAsh +
                      " labComplex=" + (builder.LabComplex != null) + " entrance=" + builder.ResolvedLabEntrance +
                      " style=" + builder.ResolvedLabStyle);
        sb.AppendLine("lairs=" + (builder.Lairs != null ? builder.Lairs.Count : -1) +
                      " registry=" + (builder.Registry != null ? builder.Registry.Count : -1) +
                      " spawner=" + (builder.Spawner != null ? "yes" : "no") +
                      " spawned=" + (builder.Spawner != null ? builder.Spawner.SpawnedCount : -1));

        var bodies = UnityEngine.Object.FindObjectsByType<CreatureBody>();
        var snakes = bodies.Where(b => b != null && b.Chassis != null && b.Chassis.name == _Змея).ToList();
        sb.AppendLine("creatureBodies=" + bodies.Length + " snakes=" + snakes.Count);
        foreach (var s in snakes.Take(12))
        {
            float ground = builder.SampleGround(s.transform.position.x, s.transform.position.z);
            float best = -1f;
            if (builder.Lairs != null)
                foreach (var l in builder.Lairs)
                {
                    float d = Vector3.Distance(l.transform.position, s.transform.position);
                    if (best < 0f || d < best) best = d;
                }
            sb.AppendLine("  snake " + s.name + " pos=" + s.transform.position +
                          " ground=" + ground.ToString("F2") + " dGround=" + (s.transform.position.y - ground).ToString("F2") +
                          " nearestLair=" + best.ToString("F1"));
        }

        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            var cc = pc.GetComponent<CharacterController>();
            var p = pc.transform.position;
            float g = builder.SampleGround(p.x, p.z);
            Vector2 inward = -new Vector2(p.x, p.z);
            float faceDot = inward.sqrMagnitude > 1e-4f
                ? Vector3.Dot(pc.transform.forward, new Vector3(inward.x, 0f, inward.y).normalized)
                : 0f;
            var t = typeof(PlayerController);
            var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object sup = t.GetField("supportY", f) != null ? t.GetField("supportY", f).GetValue(pc) : null;
            object off = t.GetField("standOffset", f) != null ? t.GetField("standOffset", f).GetValue(pc) : null;
            sb.AppendLine("player pos=" + p + " rot=" + pc.transform.eulerAngles +
                          " ground=" + g.ToString("F2") + " dGround=" + (p.y - g).ToString("F2") +
                          " faceInwardDot=" + faceDot.ToString("F2") +
                          " grounded=" + (cc != null && cc.isGrounded) +
                          " supportY=" + sup + " standOffset=" + off);
            var follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
            if (follow != null)
            {
                var target = new SerializedObject(follow).FindProperty("target").objectReferenceValue;
                sb.AppendLine("CameraFollow target=" + (target != null ? target.name : "null"));
            }
        }

        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            sb.AppendLine("cam " + c.name + " enabled=" + c.enabled + " tag=" + c.tag + " depth=" + c.depth);
        return sb.ToString();
    }

    // ---------- 4. проба спуска (анти-телепорт вверх) -------------------------
    static Vector3 dropStart;

    public static string DropProbe()
    {
        var builder = UnityEngine.Object.FindAnyObjectByType<ForestBuilder>();
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (builder == null || pc == null) return "no builder/player";
        var p = pc.transform.position;
        Vector3 best = p;
        float bestDrop = 0f;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            for (float r = 8f; r <= 40f; r += 8f)
            {
                float x = p.x + Mathf.Cos(a) * r;
                float z = p.z + Mathf.Sin(a) * r;
                float g = builder.SampleGround(x, z);
                float drop = p.y - g;
                if (drop > bestDrop) { bestDrop = drop; best = new Vector3(x, g + 1f, z); }
            }
        }
        if (bestDrop < 2f) return "no descent >=2m found around player";
        dropStart = best;
        var cc = pc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        pc.transform.position = best;
        if (cc != null) cc.enabled = true;
        return "probe: moved player to " + best + " (drop was " + bestDrop.ToString("F2") + "m)";
    }

    public static string DropReport()
    {
        var builder = UnityEngine.Object.FindAnyObjectByType<ForestBuilder>();
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (builder == null || pc == null) return "no builder/player";
        var p = pc.transform.position;
        float g = builder.SampleGround(p.x, p.z);
        var t = typeof(PlayerController);
        var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object sup = t.GetField("supportY", f) != null ? t.GetField("supportY", f).GetValue(pc) : null;
        return "after probe: pos=" + p + " ground=" + g.ToString("F2") +
               " dGround=" + (p.y - g).ToString("F2") + " supportY=" + sup;
    }
}
