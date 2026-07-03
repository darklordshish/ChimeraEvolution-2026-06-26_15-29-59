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

    [MenuItem("Chimera/Создать префаб Змеи")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildSnake();

        var body = go.transform.Find("Body").GetComponent<Renderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(body.sharedMaterial);
            mat.SetColor("_BaseColor", new Color(0.25f, 0.4f, 0.22f)); // тёмно-зелёная
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        body.sharedMaterial = mat;

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

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.GetComponent<Collider>().enabled = false; // коллайдер — у CharacterController
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // лежит горизонтально — «ползёт»
        body.transform.localScale = new Vector3(0.6f, 1.6f, 0.6f);    // длинная тонкая

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
