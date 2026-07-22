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
        cc.height = 2.7f; cc.radius = 0.9f; cc.center = new Vector3(0f, 1.35f, 0f); // ходульная туша: ноги ≈ полроста

        // ХОДУЛЬНОСТЬ ЛОСЯ: `lift` поднимает всю тушу над землёй, ноги удлинены на ту же высоту (стопы на 0).
        // Одна ручка силуэта — крути её, чтобы сделать длинноногее/приземистее (остальное едет следом)
        const float lift = 0.5f;

        // корпус — вытянутый бокс-плейсхолдер; голова — сборная лосиная (AttachMooseHead ниже)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; // ГРУДНАЯ половина — глубже и выше (к холке)
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.5f + lift, 0.4f); // корпус ПОДНЯТ на длину ног (низ туши ~1.45)
        body.transform.localScale = new Vector3(1.1f, 1.05f, 1.6f);
        Object.DestroyImmediate(body.GetComponent<Collider>()); // коллизия — на CharacterController

        var rump = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rump.name = "Rump"; // круп — ниже и уже: спина покато спадает от холки к заду
        rump.transform.SetParent(go.transform, false);
        rump.transform.localPosition = new Vector3(0f, 1.42f + lift, -0.85f);
        rump.transform.localScale = new Vector3(0.95f, 0.9f, 1.0f);
        Object.DestroyImmediate(rump.GetComponent<Collider>());

        var mooseTail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mooseTail.name = "Tail"; // хвостик-ОБРУБОК: в природе у лося ~10см, почти бесхвост — и у нас так
        mooseTail.transform.SetParent(go.transform, false);
        mooseTail.transform.localPosition = new Vector3(0f, 1.66f + lift, -1.38f);
        mooseTail.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        mooseTail.transform.localScale = new Vector3(0.12f, 0.26f, 0.1f);
        Object.DestroyImmediate(mooseTail.GetComponent<Collider>());

        // НОГИ по-настоящему длинные (полроста лося — ноги): передние прямые с копытами; ЗАДНИЕ с изломом
        // копытного — бедро вперёд-вниз, голень назад-вниз (скакательный сустав), копыто. Удлинены на `lift`,
        // СТОПЫ остаются на земле (y≈0.06): нога тянется вверх к поднятой туше
        void LegPart(string name, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            Object.DestroyImmediate(p.GetComponent<Collider>());
        }
        for (int side = -1; side <= 1; side += 2)
        {
            LegPart("LegFront",  new Vector3(0.35f * side, 0.55f + lift * 0.5f, 0.85f), Vector3.zero,              new Vector3(0.16f, 1.0f + lift, 0.16f));
            LegPart("HoofFront", new Vector3(0.35f * side, 0.06f, 0.87f),               Vector3.zero,              new Vector3(0.18f, 0.12f, 0.2f));
            LegPart("Thigh",     new Vector3(0.35f * side, 0.75f + lift * 0.6f, -0.88f), new Vector3(-18f, 0f, 0f), new Vector3(0.22f, 0.6f + lift * 0.5f, 0.28f));
            LegPart("Shin",      new Vector3(0.35f * side, 0.3f + lift * 0.3f, -0.84f),  new Vector3(15f, 0f, 0f),  new Vector3(0.13f, 0.55f + lift * 0.5f, 0.13f));
            LegPart("HoofRear",  new Vector3(0.35f * side, 0.06f, -0.8f),                Vector3.zero,              new Vector3(0.16f, 0.12f, 0.18f));
        }

        // ШЕЯ — наклонный брус от загривка вверх-вперёд: поднимает голову НАД тушей
        var neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        neck.name = "Neck";
        neck.transform.SetParent(go.transform, false);
        neck.transform.localPosition = new Vector3(0f, 1.9f + lift, 1.25f);
        neck.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        neck.transform.localScale = new Vector3(0.35f, 0.38f, 0.8f);
        Object.DestroyImmediate(neck.GetComponent<Collider>());

        AttachMooseHead(go.transform, new Vector3(0f, 2.15f + lift, 1.55f), 1f); // сборная голова НА шее, выше туши (см. метод)

        // ХОЛКА-ГОРБ над лопатками — фирменный силуэт лося (высшая точка туши, выше крупа)
        var hump = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hump.name = "Hump";
        hump.transform.SetParent(go.transform, false);
        hump.transform.localPosition = new Vector3(0f, 2.12f + lift, 0.45f); // над поднятой грудью
        hump.transform.localScale = new Vector3(0.85f, 0.35f, 0.9f);
        Object.DestroyImmediate(hump.GetComponent<Collider>());

        // РОГА (плейсхолдер, по-лосиному): короткий столбик у МАКУШКИ + прямоугольная ЛОПАСТЬ большой стороной
        // ВДОЛЬ тела (ось z), сдвинутая НАЗАД — столбик подходит к её нижне-переднему углу. Лопасть выше корпуса
        // (y≈2.28 > верх тела 1.9) → веер идёт над головой/шеей, в спину не врезается.
        for (int side = -1; side <= 1; side += 2)
        {
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cube); // столбик у макушки
            stem.name = side < 0 ? "AntlerStemL" : "AntlerStemR";
            stem.transform.SetParent(go.transform, false);
            stem.transform.localPosition = new Vector3(0.15f * side, 2.45f + lift, 1.55f); // у макушки поднятой головы
            stem.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);          // пеньки мельче
            Object.DestroyImmediate(stem.GetComponent<Collider>());

            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube); // прямоугольная лопасть, длинной стороной вдоль тела
            palm.name = side < 0 ? "AntlerPalmL" : "AntlerPalmR";
            palm.transform.SetParent(go.transform, false);
            palm.transform.localPosition = new Vector3(0.35f * side, 2.72f + lift, 1.4f); // над поднятой головой; столбик входит отступя от переднего края
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
        PrefabConfig.Set(charge, ("windupTime", 0.5f), ("minRange", 4f), ("maxRange", 18f),
                                          ("chargeSpeed", 35f), ("duration", 1.1f), ("damage", 22), ("damagePerMeter", 1.0f),
                                          ("hitRadius", 1.8f), ("knockForce", 12f), ("staggerTime", 0.5f), ("gizmoHeight", 1.2f));
        var antler = go.AddComponent<AntlerAbility>(); // удар рогами по липнущим вплотную (урон+отлёт+кровь)
        PrefabConfig.Set(antler, ("windupTime", 0.35f), ("range", 2.5f), ("damage", 12), ("knockForce", 9f), ("bleedStacks", 2), ("gizmoHeight", 2.2f));

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

    /// <summary>
    /// ЛОСИНАЯ ГОЛОВА (переиспользуемая, как AttachWolfHead): череп + длинная ГОРБАТАЯ морда, опущенная
    /// вниз + нос-бульба шире морды (нависающая губа — фирменный профиль лося) + лопоухие уши врастопырку +
    /// борода-серьга под подбородком. Имена частей — из белого списка Telegraph.IsHeadName
    /// (Head/Muzzle/Nose/EarL/EarR): морда-градиент лесенки и эмоц-тинт красят ВСЮ голову; борода (Dewlap) —
    /// шея, не красится. k — масштаб (задел химер-деталей: босс «наденет» лосиное той же функцией).
    /// </summary>
    public static void AttachMooseHead(Transform parent, Vector3 skullCenter, float k)
    {
        void Part(string name, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = skullCenter + pos * k;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale * k;
            Object.DestroyImmediate(p.GetComponent<Collider>());
        }

        Part("Head",   Vector3.zero,                   Vector3.zero,             new Vector3(0.42f, 0.42f, 0.5f));  // череп
        Part("Muzzle", new Vector3(0f, -0.12f, 0.4f),  new Vector3(22f, 0f, 0f), new Vector3(0.3f, 0.32f, 0.62f));  // длинная морда с горбинкой, вперёд-вниз
        Part("Nose",   new Vector3(0f, -0.3f, 0.68f),  Vector3.zero,             new Vector3(0.32f, 0.26f, 0.22f)); // бульба-нос ШИРЕ морды (нависающая губа)
        Part("Dewlap", new Vector3(0f, -0.4f, 0.1f),   Vector3.zero,             new Vector3(0.1f, 0.28f, 0.16f));  // борода-серьга
        for (int side = -1; side <= 1; side += 2)
            Part(side < 0 ? "EarL" : "EarR",
                 new Vector3(0.27f * side, 0.27f, -0.1f), new Vector3(0f, 0f, -40f * side), new Vector3(0.09f, 0.3f, 0.14f)); // лопоухие уши, развал наружу
    }
}
