using UnityEditor;
using UnityEngine;

/// <summary>
/// Утилита разработки: собирает префаб вервольфа (капсула-плейсхолдер + компоненты), чтобы можно было
/// тюнить `WerewolfPsyche` руками в инспекторе. Меню: Chimera → Создать префаб Вервольфа.
/// Dev-спавн (ChimeraDevWindow) берёт этот префаб, если он есть. Editor-only.
/// </summary>
public static class WerewolfPrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Werewolf.prefab";
    const string MatPath = "Assets/_Chimera/Materials/WerewolfBody.mat";

    [MenuItem("Chimera/Создать префаб Вервольфа")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials"))
            AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildWerewolf();

        // тёмно-бордовый материал-ассет, чтобы отличать от волков (телеграф уважает цвет материала)
        var body = go.transform.Find("Body").GetComponent<Renderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(body.sharedMaterial);
            mat.SetColor("_BaseColor", new Color(0.32f, 0.11f, 0.12f));
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        body.sharedMaterial = mat;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб вервольфа создан: " + Path + ". Тюнь компонент WerewolfPsyche в инспекторе. Dev-спавн теперь берёт этот префаб.");
    }

    // Собирает GameObject вервольфа (капсула-плейсхолдер + компоненты). Используется и префабом, и dev-спавном (фолбэк).
    public static GameObject BuildWerewolf()
    {
        var go = new GameObject("Werewolf");
        var cc = go.AddComponent<CharacterController>();
        cc.height = 2.6f; cc.radius = 0.7f; cc.center = new Vector3(0f, 1.3f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.GetComponent<Collider>().enabled = false; // коллайдер — у CharacterController
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        body.transform.localScale = new Vector3(1.5f, 1.3f, 1.5f);

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();

        // доставки босса: те же компоненты, что у волка, но с числами вервольфа (вампиризм)
        var bite = go.AddComponent<BiteAbility>();
        Configure(bite, ("windupTime", 0.4f), ("range", 2.5f), ("halfAngle", 60f), ("damage", 28), ("lifeSteal", 25));
        var leap = go.AddComponent<LeapAbility>();
        Configure(leap, ("windupTime", 0.5f), ("minRange", 6f), ("maxRange", 11f), ("speed", 16f), ("up", 6f),
                        ("duration", 0.55f), ("damage", 30), ("lifeSteal", 25), ("hitRadius", 2f));

        // вервольф под ВЕЧНОЙ яростью: урон/скорость выше, входящий урон больше («быстрый убийца, не танк»)
        var rage = go.AddComponent<Rage>();
        Configure(rage, ("permanent", true));

        go.AddComponent<WerewolfPsyche>();
        return go;
    }

    // выставить приватные [SerializeField]-поля компонента по именам (editor-only, через SerializedObject)
    static void Configure(Component c, params (string field, object value)[] values)
    {
        var so = new SerializedObject(c);
        foreach (var (field, value) in values)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"WerewolfPrefab: поле {field} не найдено на {c.GetType().Name}"); continue; }
            if (value is float f) p.floatValue = f;
            else if (value is int i) p.intValue = i;
            else if (value is bool b) p.boolValue = b;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
