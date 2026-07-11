using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб змеи (капсула-плейсхолдер + компоненты). Меню: Chimera → Создать префаб Змеи.
/// Тело на шасси Змея (`CreatureBody`: органы × экспрессия 0.5 → природная особь; Сердце даёт холоднокровность).
/// Укус несёт яд (`BiteAbility.venomStacks`), рывок — фаст-страйк из засады. Dev-спавн берёт этот префаб. Editor-only.
/// </summary>
public static class SnakePrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Snake.prefab";
    const string MatPath = "Assets/_Chimera/Materials/SnakeBody.mat";
    const string RattleMatPath = "Assets/_Chimera/Materials/SnakeRattle.mat";

    [MenuItem("Chimera/Создать префаб Змеи")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildSnake();

        var anyRenderer = go.GetComponentInChildren<Renderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(anyRenderer.sharedMaterial);
            mat.SetColor("_BaseColor", new Color(0.25f, 0.4f, 0.22f)); // тёмно-зелёная
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        var rattleMat = AssetDatabase.LoadAssetAtPath<Material>(RattleMatPath);
        if (rattleMat == null)
        {
            rattleMat = new Material(anyRenderer.sharedMaterial);
            rattleMat.SetColor("_BaseColor", new Color(0.95f, 0.85f, 0.2f)); // жёлтая погремушка
            AssetDatabase.CreateAsset(rattleMat, RattleMatPath);
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = r.gameObject.name == "RattleMesh" ? rattleMat : mat;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб змеи создан: " + Path + ". Тюнь SnakePsyche/BiteAbility/LeapAbility. Dev-спавн берёт этот префаб.");
    }

    public static GameObject BuildSnake()
    {
        var go = new GameObject("Snake");
        var cc = go.AddComponent<CharacterController>();
        cc.height = 0.8f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.4f, 0f); // низкий силуэт

        // ГОЛОВА — капсула покрупнее, вдоль forward. Коллайдеры головы/сегментов ВКЛЮЧЕНЫ:
        // тело плотное по всей длине (свой CC игнорирует их — SnakeBodyChain.Awake)
        var head = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        head.name = "Head";
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.42f, 0.35f);
        head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        // ТЕЛО — 5 шариков-сегментов, тянутся за головой (SnakeBodyChain раскладывает по пути)
        float[] sizes = { 0.55f, 0.52f, 0.48f, 0.45f, 0.42f };
        var segments = new Transform[sizes.Length + 1];
        for (int i = 0; i < sizes.Length; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Seg" + i;
            s.transform.SetParent(go.transform, false);
            s.transform.localPosition = new Vector3(0f, 0.3f, -0.35f - 0.45f * (i + 1)); // стартовая раскладка позади
            s.transform.localScale = Vector3.one * sizes[i];
            segments[i] = s.transform;
        }

        // ПОГРЕМУШКА — маленькая ЖЁЛТАЯ капсула на кончике хвоста: гремок мигает именно ей
        var rattle = new GameObject("Rattle");
        rattle.transform.SetParent(go.transform, false);
        rattle.transform.localPosition = new Vector3(0f, 0.3f, -0.35f - 0.45f * (sizes.Length + 1));
        var rattleMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rattleMesh.name = "RattleMesh";
        Object.DestroyImmediate(rattleMesh.GetComponent<Collider>()); // мелочь, коллайдер не нужен
        rattleMesh.transform.SetParent(rattle.transform, false);
        rattleMesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // вдоль пути (Rattle ориентирует цепочка)
        rattleMesh.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
        segments[sizes.Length] = rattle.transform;

        // цепочка тела: раскладывает сегменты по пути головы + игнорирует их коллайдеры для своего CC
        var chain = go.AddComponent<SnakeBodyChain>();
        var chainSo = new SerializedObject(chain);
        var arr = chainSo.FindProperty("segments");
        arr.arraySize = segments.Length;
        for (int i = 0; i < segments.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
        chainSo.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();
        // родство на смерть начисляет САМО тело (CreatureBody ниже): +1 за видо-флаг шасси «Змея» (нужно для донора/наги в слайсе 2)

        // укус с ЯДОМ; рывок — быстрый низкий страйк из засады
        var bite = go.AddComponent<BiteAbility>();
        WerewolfPrefab.Configure(bite, ("windupTime", 0.4f), ("range", 2f), ("halfAngle", 55f), ("venomStacks", 1));
        var leap = go.AddComponent<LeapAbility>();
        WerewolfPrefab.Configure(leap, ("windupTime", 0.35f), ("minRange", 4f), ("maxRange", 9f), ("speed", 20f),
                                       ("up", 2.5f), ("duration", 0.35f), ("damage", 8), ("hitRadius", 1.5f));

        // тело на шасси Змея (природная особь: экспрессия 0.5; витальность из органов; холоднокровность от Сердца)
        var cbody = go.AddComponent<CreatureBody>();
        var snake = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Змея.asset");
        if (snake == null) Debug.LogWarning("SnakePrefab: ассет Змея не найден — прогони «Chimera → Создать дефолтные виды» и пересоздай префаб.");
        var so = new SerializedObject(cbody);
        so.FindProperty("chassis").objectReferenceValue = snake;
        so.FindProperty("expression").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<SnakePsyche>();
        return go;
    }
}
