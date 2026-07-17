using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб волка (капсула-плейсхолдер + компоненты). Меню: Chimera → Создать префаб Волка.
/// Тело на шасси Волк (`CreatureBody`: органы × экспрессия 0.45 → природная особь). Цвет запечён в материал
/// = `Волк.tint` (как у игрока на полном волчьем сете, светлее вервольфа). Editor-only.
/// </summary>
public static class WolfPrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Wolf.prefab";
    const string MatPath = "Assets/_Chimera/Materials/WolfBody.mat";
    static readonly Color WolfTint = new(0.5f, 0.5f, 0.52f); // = Волк.tint (серый — отличимо от бурого лося)

    [MenuItem("Chimera/Создать префаб Волка")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildWolf();

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(go.GetComponentInChildren<Renderer>().sharedMaterial);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.SetColor("_BaseColor", WolfTint); // светлее бордового вервольфа
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat; // тело + морда + уши

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб волка создан: " + Path + " (GUID сохранён — ссылки в спавнере/сцене живы). Тюнь WolfPsyche/Bite/Leap.");
    }

    public static GameObject BuildWolf()
    {
        var go = new GameObject("Wolf");
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.6f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.8f, 0f);

        // тело: капсула ВДОЛЬ forward (как у тебя было) + куб-морда спереди, где пасть — видно, куда смотрит
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.GetComponent<Collider>().enabled = false; // коллайдер — у CharacterController
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // длинная ось вдоль Z (вперёд)
        body.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);

        AttachWolfHead(go.transform, new Vector3(0f, 0.85f, 0.65f), 1f); // голова над телом, мордой вперёд

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();

        // укус и прыжок — общие доставки с числами волка (урон укуса приходит из органов через CreatureBody)
        var bite = go.AddComponent<BiteAbility>();
        WerewolfPrefab.Configure(bite, ("windupTime", 0.45f), ("range", 2f), ("halfAngle", 55f));
        var leap = go.AddComponent<LeapAbility>();
        WerewolfPrefab.Configure(leap, ("windupTime", 0.5f), ("minRange", 5f), ("maxRange", 6.5f), ("speed", 13f),
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

    /// <summary>
    /// ВОЛЧЬЯ ГОЛОВА (переиспользуемая): двухблочная морда (куб-череп + узкий нос спереди-снизу) +
    /// уши-«треугольнички» (плоские кубики ромбом 45°, утоплены в макушку — торчат кончики).
    /// skullCenter — центр черепа в локальных координатах носителя, k — масштаб (волк 1, вервольф крупнее).
    /// Той же головой вервольф «надевает волка» — химера собирается из частей (задел конструктора моделек).
    /// </summary>
    public static void AttachWolfHead(Transform parent, Vector3 skullCenter, float k)
    {
        var skull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        skull.name = "Muzzle"; // череп
        skull.GetComponent<Collider>().enabled = false;
        skull.transform.SetParent(parent, false);
        skull.transform.localPosition = skullCenter;
        skull.transform.localScale = new Vector3(0.35f, 0.35f, 0.38f) * k;

        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Nose"; // нос: уже и чуть ниже черепа, торчит вперёд
        nose.GetComponent<Collider>().enabled = false;
        nose.transform.SetParent(parent, false);
        nose.transform.localPosition = skullCenter + new Vector3(0f, -0.04f, 0.3f) * k;
        nose.transform.localScale = new Vector3(0.2f, 0.24f, 0.28f) * k;

        for (int side = -1; side <= 1; side += 2)
        {
            var ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ear.name = side < 0 ? "EarL" : "EarR";
            ear.GetComponent<Collider>().enabled = false;
            ear.transform.SetParent(parent, false);
            ear.transform.localPosition = skullCenter + new Vector3(0.1f * side, 0.17f, -0.07f) * k;
            ear.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // ромб: верхний угол = треугольное ухо
            ear.transform.localScale = new Vector3(0.11f, 0.11f, 0.06f) * k;
        }
    }
}
