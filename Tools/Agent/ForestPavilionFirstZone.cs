using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// П-3 леса (13.11): `ForestPavilion` — первая зона вечного цикла (решение геймдизайнера 13.11, спека вечной
/// эволюции §3б). Что делает:
///  • профиль: замороженное выключено — каньон (ассеты в теге-архиве), акведук; пул видов — все звери леса;
///  • из `SampleScene` переносит объекты вечного цикла вместе с их настройками: игровые часы, босс, стая,
///    кусты-корм, разброс особей, дев-панель, волны звука;
///  • кусты под Ø2000: охват — весь павильон, число — тюнинг для плейтеста;
///  • Build Settings: павильон — индекс 0, `SampleScene` — 1, стенды леса вне сборки.
/// Объекты сцены, которые уже есть, не дублирует (повторный запуск безопасен).
/// </summary>
public static class ForestPavilionFirstZone
{
    const string PavilionPath = "Assets/Scenes/ForestPavilion.unity";
    const string SamplePath = "Assets/Scenes/SampleScene.unity";

    // Объекты вечного цикла в SampleScene: имя объекта → компонент, по которому узнаём, что он уже есть.
    static readonly (string go, System.Type type)[] Carry =
    {
        ("WorldClock", typeof(WorldClock)),
        ("BossChimeraSpawner", typeof(BossChimeraSpawner)),
        ("PackCoordinator", typeof(PackCoordinator)),
        ("ForageSpawner", typeof(ForageSpawner)),
        ("IndividualityConfig", typeof(IndividualityConfig)),
        ("Debug", typeof(DebugHud)),
        ("NoiseWaves", typeof(NoiseWaves)),
    };

    static readonly string[] Pool = { "Волк", "Змея", "Лось", "Ёж" };

    // Кусты под Ø2000: охват — полкруга павильона без скального кольца; число — тюнинг для плейтеста.
    const float ForageHalfExtent = 850f;
    const int ForageCount = 160;

    public static string Run()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                return "STOP: открыта несохранённая сцена " + SceneManager.GetSceneAt(i).path;

        var scene = EditorSceneManager.OpenScene(PavilionPath, OpenSceneMode.Single);
        var fb = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<ForestBuilder>(true))
            .FirstOrDefault(b => b != null);
        if (fb == null) return "FAILED: в павильоне нет ForestBuilder";

        // ---- профиль ----
        var so = new SerializedObject(fb);
        SetBool(so, "buildCanyon", false);
        SetBool(so, "buildAqueduct", false);
        SetBool(so, "buildAnimalLairs", true); // логова волка, лося, ежа (спека «логово по законам» §4.4)
        var pool = so.FindProperty("speciesPool");
        if (pool == null) return "FAILED: нет поля speciesPool";
        pool.arraySize = Pool.Length;
        for (int i = 0; i < Pool.Length; i++)
        {
            var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{Pool[i]}.asset");
            if (sp == null) return $"FAILED: вид «{Pool[i]}» не найден";
            pool.GetArrayElementAtIndex(i).objectReferenceValue = sp;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("профиль: buildCanyon=false buildAqueduct=false buildAnimalLairs=true пул=[" + string.Join(",", Pool) + "]");

        // ---- объекты вечного цикла из SampleScene ----
        var sample = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        foreach (var (goName, type) in Carry)
        {
            if (scene.GetRootGameObjects().Any(g => g.GetComponentInChildren(type, true) != null))
            {
                sb.AppendLine($"  {goName}: уже есть");
                continue;
            }
            var src = sample.GetRootGameObjects().FirstOrDefault(g => g.name == goName && g.GetComponent(type) != null);
            if (src == null) { sb.AppendLine($"  {goName}: НЕТ в SampleScene"); continue; }
            var copy = Object.Instantiate(src);
            copy.name = src.name;
            SceneManager.MoveGameObjectToScene(copy, scene);
            copy.transform.position = Vector3.zero;
            sb.AppendLine($"  {goName}: перенесён ({string.Join(",", copy.GetComponents<Component>().Skip(1).Select(c => c.GetType().Name))})");
        }
        EditorSceneManager.CloseScene(sample, true);

        // ---- кусты под павильон ----
        var forage = scene.GetRootGameObjects().Select(g => g.GetComponent<ForageSpawner>()).FirstOrDefault(f => f != null);
        if (forage != null)
        {
            var fso = new SerializedObject(forage);
            SetFloat(fso, "mapHalfExtent", ForageHalfExtent);
            SetInt(fso, "count", ForageCount);
            fso.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine($"  кусты: охват ±{ForageHalfExtent} м, {ForageCount} шт.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("сцена сохранена: " + saved);

        // ---- Build Settings: павильон первым, арена второй ----
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(PavilionPath, true),
            new EditorBuildSettingsScene(SamplePath, true),
        };
        sb.AppendLine("сборка: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
        return sb.ToString();
    }

    static void SetBool(SerializedObject so, string name, bool v)
    {
        var p = so.FindProperty(name) ?? throw new System.Exception("нет поля " + name);
        p.boolValue = v;
    }

    static void SetFloat(SerializedObject so, string name, float v)
    {
        var p = so.FindProperty(name) ?? throw new System.Exception("нет поля " + name);
        p.floatValue = v;
    }

    static void SetInt(SerializedObject so, string name, int v)
    {
        var p = so.FindProperty(name) ?? throw new System.Exception("нет поля " + name);
        p.intValue = v;
    }
}
