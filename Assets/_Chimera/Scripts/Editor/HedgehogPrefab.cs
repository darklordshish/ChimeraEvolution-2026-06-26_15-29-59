using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб ЕЖА «Хеджхалк». Меню: Chimera → Создать префаб Ежа.
/// Тело на шасси Ёж (`CreatureBody`: органы × экспрессия 0.5 → природная особь). Editor-only.
///
/// Визуал — кубы-плейсхолдеры: низкий широкий корпус, ряды игл по спине, короткие лапы. Читаемость
/// важнее правдоподобия: силуэт должен с первого взгляда говорить «колючий и приземистый», иначе на
/// дистанции ёж сольётся с любым другим мелким зверем.
/// </summary>
public static class HedgehogPrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Hedgehog.prefab";
    const string BodyMatPath = "Assets/_Chimera/Materials/HedgehogBody.mat";
    const string QuillMatPath = "Assets/_Chimera/Materials/HedgehogQuills.mat";

    static readonly Color BodyTint = new(0.58f, 0.33f, 0.28f); // = Ёж.tint (ржаво-кирпичный)
    static readonly Color QuillTint = new(0.28f, 0.25f, 0.22f); // иглы ТЕМНЕЕ тела — контраст читает «колючесть»

    [MenuItem("Chimera/Создать префаб Ежа")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Prefabs")) AssetDatabase.CreateFolder("Assets/_Chimera", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_Chimera/Materials")) AssetDatabase.CreateFolder("Assets/_Chimera", "Materials");

        var go = BuildHedgehog();

        var body = GetOrCreateMat(BodyMatPath, BodyTint, 0.15f);
        var quill = GetOrCreateMat(QuillMatPath, QuillTint, 0.35f);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = r.gameObject.name.StartsWith("Quill") ? quill : body;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб ежа создан: " + Path + ". Положи его в спавнер сцены, чтобы он появлялся в бою.");
    }

    public static GameObject BuildHedgehog()
    {
        var go = new GameObject("Hedgehog");
        var cc = go.AddComponent<CharacterController>();
        // приземистый и широкий: капсула ниже волчьей, но не уже — ёж крупный (лабораторный мутант)
        cc.height = 0.9f; cc.radius = 0.45f; cc.center = new Vector3(0f, 0.45f, 0f);

        GameObject Part(string name, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.GetComponent<Collider>().enabled = false; // коллизия — у CharacterController
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            return p;
        }

        Part("Body", new Vector3(0f, 0.5f, -0.05f), new Vector3(0.86f, 0.62f, 1.05f)); // низкий широкий корпус

        // ГОЛОВА: имена «Muzzle»/«Nose» — конвенция эмоц-тинта (морда краснеет от ярости)
        Part("Muzzle", new Vector3(0f, 0.36f, 0.6f), new Vector3(0.34f, 0.3f, 0.36f));
        Part("Nose", new Vector3(0f, 0.32f, 0.82f), new Vector3(0.16f, 0.16f, 0.16f));

        // ИГЛЫ: три ряда шипов по спине, наклонены назад — «не тронь». Тонкие и частые: именно их
        // рисунок отличает ежа от любого другого низкого зверя на дистанции
        int n = 0;
        for (int row = 0; row < 3; row++)
        {
            float z = 0.28f - row * 0.42f;
            int count = row == 1 ? 5 : 4;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-0.34f, 0.34f, t);
                float lean = Mathf.Lerp(-22f, 22f, t); // веером вбок
                Part($"Quill{n++}", new Vector3(x, 0.82f, z), new Vector3(0.07f, 0.34f, 0.07f),
                     new Vector3(-28f, 0f, lean));
            }
        }

        // ЛАПЫ: короткие столбики — ёж почти не виден из-под игл, ноги лишь намечены
        for (int side = -1; side <= 1; side += 2)
        {
            Part(side < 0 ? "PawFrontL" : "PawFrontR", new Vector3(0.26f * side, 0.11f, 0.34f), new Vector3(0.16f, 0.22f, 0.2f));
            Part(side < 0 ? "PawRearL" : "PawRearR", new Vector3(0.26f * side, 0.11f, -0.34f), new Vector3(0.16f, 0.22f, 0.2f));
        }

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();

        var bite = go.AddComponent<BiteAbility>();
        PrefabConfig.Set(bite, ("windupTime", 0.4f), ("range", 1.7f), ("halfAngle", 60f));

        // ЗАЛП ИГЛАМИ — дальняя грань (первый ranged в игре). Компонент на префабе = психика видит его и
        // стреляет; нет его — ёж чисто ближний. gizmoHeight низкий (ёж приземист)
        var volley = go.AddComponent<QuillVolley>();
        PrefabConfig.Set(volley, ("windupTime", 0.5f), ("minRange", 6f), ("maxRange", 15f),
                                 ("quills", 5), ("spreadAngle", 20f), ("speed", 22f), ("gizmoHeight", 0.5f));

        go.AddComponent<Rage>();          // теплокровный: ПРЕДЕЛ (страх → ярость) придёт слайсом D
        go.AddComponent<SpawnVariance>(); // разброс особи

        // тело на шасси Ёж. ИГЛЫ и ЯДОУПОРНОСТЬ вешает САМО тело по флагам органов (Thorns/VenomResist) —
        // в префабе их нет и быть не должно: снял Шкуру ежа, и ответка исчезла вместе с ней
        var cbody = go.AddComponent<CreatureBody>();
        var hog = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Ёж.asset");
        if (hog == null) Debug.LogWarning("HedgehogPrefab: ассет Ёж не найден — прогони «Chimera → Создать дефолтные виды».");
        var so = new SerializedObject(cbody);
        so.FindProperty("chassis").objectReferenceValue = hog;
        so.FindProperty("expression").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<HedgehogPsyche>();
        return go;
    }

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
