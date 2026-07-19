using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб лося (крупная капсула-плейсхолдер + компоненты). Меню: Chimera → Создать префаб Лося.
/// Тело на шасси «Лось» (CreatureBody × экспрессия 0.5). Массивный (Massive): обхват/нокбэк по нему слабее/нет.
/// Таран — ChargeAbility. Dev-спавн берёт этот префаб. Editor-only.
/// </summary>
public static class MoosePrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Moose.prefab";
    const string MatPath = "Assets/_Chimera/Materials/MooseBody.mat";

    [MenuItem("Chimera/Создать префаб Лося")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildMoose();

        var anyRenderer = go.GetComponentInChildren<Renderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(anyRenderer.sharedMaterial);
            mat.SetColor("_BaseColor", new Color(0.42f, 0.32f, 0.22f)); // тёмно-бурый
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб лося создан: " + Path + ". Тюнь MoosePsyche/ChargeAbility. Dev-спавн берёт этот префаб.");
    }

    public static GameObject BuildMoose()
    {
        var go = new GameObject("Moose");
        var cc = go.AddComponent<CharacterController>();
        cc.height = 2.2f; cc.radius = 0.9f; cc.center = new Vector3(0f, 1.1f, 0f); // крупная туша

        // корпус — вытянутый бокс-плейсхолдер; морда-бокс спереди для читаемости направления
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        body.transform.localScale = new Vector3(1.1f, 1.6f, 2.6f);
        Object.DestroyImmediate(body.GetComponent<Collider>()); // коллизия — на CharacterController

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.5f, 1.4f);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.7f);
        Object.DestroyImmediate(head.GetComponent<Collider>());

        // РОГА (плейсхолдер, по-лосиному): короткий столбик у МАКУШКИ + прямоугольная ЛОПАСТЬ большой стороной
        // ВДОЛЬ тела (ось z), сдвинутая НАЗАД — столбик подходит к её нижне-переднему углу. Лопасть выше корпуса
        // (y≈2.28 > верх тела 1.9) → веер идёт над головой/шеей, в спину не врезается.
        for (int side = -1; side <= 1; side += 2)
        {
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cube); // столбик у макушки
            stem.name = side < 0 ? "AntlerStemL" : "AntlerStemR";
            stem.transform.SetParent(go.transform, false);
            stem.transform.localPosition = new Vector3(0.15f * side, 1.95f, 1.4f); // ближе к центру-макушке; верх ≈ y2.15
            stem.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);          // пеньки мельче
            Object.DestroyImmediate(stem.GetComponent<Collider>());

            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube); // прямоугольная лопасть, длинной стороной вдоль тела
            palm.name = side < 0 ? "AntlerPalmL" : "AntlerPalmR";
            palm.transform.SetParent(go.transform, false);
            palm.transform.localPosition = new Vector3(0.35f * side, 2.2f, 1.25f); // чуть ВПЕРЁД — столбик входит отступя от переднего края
            palm.transform.localRotation = Quaternion.Euler(0f, 0f, 30f * side);   // круче вывернута вверх-наружу
            palm.transform.localScale = new Vector3(0.4f, 0.08f, 0.7f);            // большая сторона (0.7) — вдоль тела (z)
            Object.DestroyImmediate(palm.GetComponent<Collider>());
        }

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();
        go.AddComponent<Massive>(); // массивная туша: обхват слабее, нокбэк не берёт

        var charge = go.AddComponent<ChargeAbility>();
        // ДЛИННЫЙ мощный таран: быстрее волчьего рывка (35 > 30) + долгий разбег (1.1c ≈ 38м) + дальний завод (18м) →
        // догоняет убегающего волка по прямой; damagePerMeter скручен (1.0), чтобы длинный разбег не делал ваншотом
        WerewolfPrefab.Configure(charge, ("windupTime", 0.5f), ("minRange", 4f), ("maxRange", 18f),
                                          ("chargeSpeed", 35f), ("duration", 1.1f), ("damage", 22), ("damagePerMeter", 1.0f),
                                          ("hitRadius", 1.8f), ("knockForce", 12f), ("staggerTime", 0.5f));
        var antler = go.AddComponent<AntlerAbility>(); // удар рогами по липнущим вплотную (урон+отлёт+кровь)
        WerewolfPrefab.Configure(antler, ("windupTime", 0.35f), ("range", 2.5f), ("damage", 12), ("knockForce", 9f), ("bleedStacks", 2));

        // тело на шасси Лось (природная особь: экспрессия 0.5; витальность/скорость из органов)
        var cbody = go.AddComponent<CreatureBody>();
        var moose = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Лось.asset");
        if (moose == null) Debug.LogWarning("MoosePrefab: ассет Лось не найден — прогони «Chimera → Создать дефолтные виды» и пересоздай префаб.");
        var so = new SerializedObject(cbody);
        so.FindProperty("chassis").objectReferenceValue = moose;
        so.FindProperty("expression").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<MoosePsyche>();
        return go;
    }
}
