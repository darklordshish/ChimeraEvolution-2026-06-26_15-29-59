using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev-утилита: собирает префаб волка (визуал + компоненты). Меню: Chimera → Создать префаб Волка.
/// Тело на шасси Волк (`CreatureBody`: органы × экспрессия 0.45 → природная особь). Editor-only.
///
/// ВИЗУАЛ: если в `Models/Wolf.fbx` лежит модель из Blender — берём её, иначе строим кубы-плейсхолдеры.
/// Кубовая сборка НЕ удалена намеренно: её голову надевает вервольф (`AttachWolfHead`), и она же
/// страхует, если модель не найдётся — префаб без визуала отлаживать невозможно.
/// </summary>
public static class WolfPrefab
{
    public const string Path = "Assets/_Chimera/Prefabs/Wolf.prefab";
    const string ModelPath = "Assets/_Chimera/Models/Wolf.fbx";

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

        if (!TryAttachModel(go)) BuildBlocky(go); // модель из Blender, иначе кубы-плейсхолдеры

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

    /// <summary>Модель из Blender. Вставляем как ВЛОЖЕННЫЙ префаб (`InstantiatePrefab`, не `Instantiate`):
    /// связь с FBX живая, и перегенерация модели в соседней линии работ подхватится сама, без пересборки
    /// префаба руками.
    ///
    /// ПОВОРОТ КОРНЯ (−90,0,0) НЕ ТРОГАТЬ. Так экспортёр Blender записывает конвертацию осей у любой
    /// модели с арматурой. Пока он на месте — волк стоит правильно; обнулить его = поставить волка на нос
    /// (README моделей, раздел «Ориентация»).</summary>
    static bool TryAttachModel(GameObject go)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (fbx == null)
        {
            Debug.LogWarning($"WolfPrefab: модель {ModelPath} не найдена — собираю кубы-плейсхолдеры.");
            return false;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        inst.name = "Model";
        inst.transform.SetParent(go.transform, false); // локальный трансформ модели остаётся КАК В ФАЙЛЕ
        return true;
    }

    /// <summary>КУБЫ-ПЛЕЙСХОЛДЕРЫ (запасной визуал): корпус двумя блоками — грудь выше и шире, круп ниже
    /// и уже, — шея-брус, ноги с «собачьим» изломом сзади и хвост-полено.</summary>
    static void BuildBlocky(GameObject go)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; // грудная клетка
        body.GetComponent<Collider>().enabled = false; // коллайдер — у CharacterController
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.76f, 0.3f);
        body.transform.localScale = new Vector3(0.54f, 0.6f, 0.82f);

        var rump = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rump.name = "Rump"; // круп — ниже и уже груди
        rump.GetComponent<Collider>().enabled = false;
        rump.transform.SetParent(go.transform, false);
        rump.transform.localPosition = new Vector3(0f, 0.74f, -0.42f); // приподнят — ПОДТЯНУТЫЙ живот
        rump.transform.localScale = new Vector3(0.42f, 0.45f, 0.62f);

        // ШЕЯ — брус от груди вперёд-вверх к голове (волк несёт голову низко-вперёд)
        var neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        neck.name = "Neck";
        neck.GetComponent<Collider>().enabled = false;
        neck.transform.SetParent(go.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.98f, 0.63f);
        neck.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        neck.transform.localScale = new Vector3(0.32f, 0.32f, 0.42f); // короткая: перекрывает грудь и затылок

        AttachWolfHead(go.transform, new Vector3(0f, 1.15f, 0.9f), 1f); // голова на верху шеи

        // НОГИ: передние — прямые столбики с лапками; ЗАДНИЕ — «собачий» излом
        void WolfPart(string name, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.GetComponent<Collider>().enabled = false;
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
        }
        for (int side = -1; side <= 1; side += 2)
        {
            WolfPart("LegFront", new Vector3(0.2f * side, 0.24f, 0.42f),   Vector3.zero,              new Vector3(0.13f, 0.44f, 0.13f));
            WolfPart("PawFront", new Vector3(0.2f * side, 0.04f, 0.46f),   Vector3.zero,              new Vector3(0.12f, 0.08f, 0.2f));
            WolfPart("Thigh",    new Vector3(0.19f * side, 0.44f, -0.48f), new Vector3(-25f, 0f, 0f), new Vector3(0.16f, 0.38f, 0.2f));
            WolfPart("Shin",     new Vector3(0.19f * side, 0.18f, -0.44f), new Vector3(22f, 0f, 0f),  new Vector3(0.1f, 0.3f, 0.1f));
            WolfPart("PawRear",  new Vector3(0.19f * side, 0.04f, -0.38f), Vector3.zero,              new Vector3(0.12f, 0.08f, 0.2f));
        }

        // ХВОСТ-ПОЛЕНО — назад-вверх (читаемость зада; поза «хвост вниз» — будущий боди-ленгвидж страха)
        var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "Tail";
        tail.GetComponent<Collider>().enabled = false;
        tail.transform.SetParent(go.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.9f, -0.76f);
        tail.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        tail.transform.localScale = new Vector3(0.15f, 0.15f, 0.55f);
    }

    /// <summary>
    /// ВОЛЧЬЯ ГОЛОВА из кубов (переиспользуемая): двухблочная морда (куб-череп + узкий нос спереди-снизу) +
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
