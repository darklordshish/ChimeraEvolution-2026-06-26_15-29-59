using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб волка (визуал + компоненты). Меню: Chimera → Создать префаб Волка.
/// Тело на шасси Волк (`CreatureBody`: органы × экспрессия 0.45 → природная особь). Editor-only.
///
/// ВИЗУАЛ: тело собирает морфология из сокетов. Ветка «подхватить цельный `Wolf.fbx`» УДАЛЕНА 11.09:
/// файл вынесен в архив 22.08, метод с тех пор никем не звался, а новые модели идут ЧАСТЯМИ по слотам
/// (`Docs/models/SPEC-konstruktor-formy.md`) — цельный зверь в префабе больше не предусмотрен.
/// </summary>
public static class WolfPrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Wolf.prefab";

    const string FurMatPath = "Assets/_Chimera/Materials/WolfBody.mat";
    const string NoseMatPath = "Assets/_Chimera/Materials/WolfNose.mat";
    const string TeethMatPath = "Assets/_Chimera/Materials/WolfTeeth.mat";

    // цвета и блеск — из README моделей (материалы FBX намеренно не импортируются, красим своими)
    static readonly Color WolfTint = new(0.5f, 0.5f, 0.52f);      // = Волк.tint (серый — отличимо от бурого лося)
    static readonly Color NoseTint = new(0.055f, 0.05f, 0.058f);  // мокрый нос: тёмный и бликующий
    static readonly Color TeethTint = new(0.88f, 0.86f, 0.79f);   // КОСТЯНОЙ, не белый — чистое белое выжигается в пятно

    [MenuItem("Chimera/Создать префаб Волка")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildWolf();
        Paint(go);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб волка создан: " + Path + " (GUID сохранён — ссылки в спавнере/сцене живы). Тюнь WolfPsyche/Bite/Leap.");
    }

    /// <summary>Красим ПО ИМЕНАМ деталей, а не одним материалом на всё: нос и клыки — свои.
    /// `HitFlash` с несколькими материалами работает без правок — он помнит базовый цвет каждого
    /// рендерера отдельно, поэтому после вспышки каждая деталь вернётся к своему.</summary>
    static void Paint(GameObject go)
    {
        var fur = GetOrCreateMat(FurMatPath, WolfTint, 0.15f);
        var nose = GetOrCreateMat(NoseMatPath, NoseTint, 0.7f);
        var teeth = GetOrCreateMat(TeethMatPath, TeethTint, 0.45f);

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            string n = r.gameObject.name;
            r.sharedMaterial = n == "Nose" ? nose : n.StartsWith("Fangs") ? teeth : fur;
        }
    }

    public static GameObject BuildWolf()
    {
        var go = new GameObject("Wolf");
        var cc = go.AddComponent<CharacterController>();
        // габариты модели: холка 1.06, с ушами 1.44, длина 2.11, ширина 0.50. Капсула НАМЕРЕННО шире тела:
        // её радиус держит волков на расстоянии друг от друга, и сепарация стаи настроена под это число
        cc.height = 1.6f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.8f, 0f);

        // ВИЗУАЛ собирает МОРФОЛОГИЯ в рантайме (MorphBuilder из состава: якоря-база + органы-детали) —
        // визуал префаба пуст — морф строит на Start/Recompute. Кубовая сборка BuildBlocky и голова AttachWolfHead
        // сняты 12.09: первая не звалась ниоткуда, вторую звал только вервольф

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();

        // укус и прыжок — общие доставки с числами волка (урон укуса приходит из органов через CreatureBody)
        var bite = go.AddComponent<BiteAbility>();
        PrefabConfig.Set(bite, ("windupTime", 0.45f), ("range", 2f), ("halfAngle", 55f));
        var leap = go.AddComponent<LeapAbility>();
        PrefabConfig.Set(leap, ("windupTime", 0.5f), ("minRange", 5f), ("maxRange", 6.5f), ("speed", 13f),
                                       ("up", 5f), ("duration", 0.5f), ("damage", 12), ("hitRadius", 1.3f));
        go.AddComponent<Rage>();          // может взбеситься от воя вожака
        go.AddComponent<SpawnVariance>(); // разброс особи
        // родство на смерть начисляет САМО тело (CreatureBody ниже): +1 за видо-флаг шасси «Волк»

        // тело на шасси Волк (природная особь: экспрессия 0.45; витальность/урон/скорость — из органов)
        var cbody = go.AddComponent<CreatureBody>();
        var wolf = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
        if (wolf == null) Debug.LogWarning("WolfPrefab: ассет Волк не найден — прогони «Chimera → Создать дефолтные виды».");
        var so = new SerializedObject(cbody);
        so.FindProperty("chassis").objectReferenceValue = wolf;
        so.FindProperty("expression").floatValue = 0.45f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<WolfPsyche>();
        return go;
    }
    // материал по пути: загрузить или создать с цветом и блеском (идемпотентно, как у других генераторов)
    static Material GetOrCreateMat(string path, Color color, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube); // взять дефолтный шейдер пайплайна
            mat = new Material(probe.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(probe);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
