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

        // серый материал под цвет волка (вервольф = волчья химера); телеграф уважает цвет материала.
        // SetColor вне if — повторный запуск генератора перекрашивает уже существующий материал
        var body = go.transform.Find("Body").GetComponent<Renderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(body.sharedMaterial);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.52f)); // серый как волк (отличается размером/ХП босса)
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat; // туша + волчья голова

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

        // ТОРС — ПЕРЕВЁРНУТЫЙ ТРЕУГОЛЬНИК тремя блоками (канон вервольф-рефов): огромная грудная клетка →
        // узкая талия → таз. Вместе с плечами (1.8) и горбом ниже — ступенчатое сужение сверху вниз
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; // грудная клетка (Create красит по имени Body)
        body.GetComponent<Collider>().enabled = false; // коллайдер — у CharacterController
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 2.0f, 0.06f);
        body.transform.localRotation = Quaternion.Euler(10f, 0f, 0f); // грудь подана вперёд — сутулый наклон
        body.transform.localScale = new Vector3(1.55f, 0.95f, 1.05f);

        var waist = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waist.name = "Waist"; // ПОДТЯНУТЫЙ живот — резко уже и мельче груди, утянут назад (хищная талия)
        waist.GetComponent<Collider>().enabled = false;
        waist.transform.SetParent(go.transform, false);
        waist.transform.localPosition = new Vector3(0f, 1.35f, -0.08f);
        waist.transform.localScale = new Vector3(0.8f, 0.55f, 0.62f);

        var pelvis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pelvis.name = "Pelvis"; // таз — вершина треугольника
        pelvis.GetComponent<Collider>().enabled = false;
        pelvis.transform.SetParent(go.transform, false);
        pelvis.transform.localPosition = new Vector3(0f, 0.95f, -0.08f);
        pelvis.transform.localScale = new Vector3(0.68f, 0.45f, 0.6f);

        // ГОЛОВА ВОЛКА на прямоходящей туше (идея пользователя: «на место головы человека поставить голову
        // волка» — химера собирается из ЧАСТЕЙ, задел конструктора моделек). Крупнее волчьей (босс)
        WolfPrefab.AttachWolfHead(go.transform, new Vector3(0f, 2.62f, 0.68f), 1.5f); // голова ПЕРЕД грудью (затылок у её кромки), НИЖЕ горба — сутулая шея вперёд

        // СУТУЛОСТЬ БОССА: горб за головой + широкий плечевой пояс + руки-лапы с кистями — хищный полу-зверь,
        // а не столб. Всё без коллайдеров (коллизия — CharacterController)
        void Bulk(string name, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.GetComponent<Collider>().enabled = false;
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
        }
        Bulk("Hump",      new Vector3(0f, 2.7f, -0.2f),  new Vector3(15f, 0f, 0f),  new Vector3(0.95f, 0.5f, 0.75f));  // горб-загривок ВЫШЕ макушки — сутулость читается в профиль
        Bulk("Shoulders", new Vector3(0f, 2.32f, 0.12f), Vector3.zero,              new Vector3(1.8f, 0.35f, 0.55f));  // плечевой пояс шире туши
        Bulk("Neck",      new Vector3(0f, 2.46f, 0.4f),  new Vector3(-45f, 0f, 0f), new Vector3(0.5f, 0.42f, 0.55f));  // сутулая шея от плеч вперёд-вверх к голове (была голова на плечах)
        for (int side = -1; side <= 1; side += 2)
        {
            Bulk(side < 0 ? "ArmL" : "ArmR",   new Vector3(0.95f * side, 1.65f, 0.15f), new Vector3(12f, 0f, 0f),  new Vector3(0.3f, 1.35f, 0.3f));   // рука-лапа вниз-чуть-вперёд
            Bulk(side < 0 ? "PawL" : "PawR",   new Vector3(0.95f * side, 0.92f, 0.33f), Vector3.zero,              new Vector3(0.36f, 0.32f, 0.36f)); // кисть-лапа у колен
            // DIGITIGRADE-нога («собачий» обратный излом): бедро вперёд-вниз (колено спереди) →
            // голень назад-вниз (пятка задрана сзади) → стопа-лапа вперёд
            Bulk(side < 0 ? "ThighL" : "ThighR", new Vector3(0.4f * side, 0.78f, 0.14f), new Vector3(-30f, 0f, 0f), new Vector3(0.4f, 0.75f, 0.42f));  // бедро круче вперёд-вниз (колено спереди)
            Bulk(side < 0 ? "ShinL" : "ShinR",   new Vector3(0.4f * side, 0.3f, 0f),     new Vector3(35f, 0f, 0f),  new Vector3(0.24f, 0.62f, 0.24f)); // голень круто назад — задранная пятка
            Bulk(side < 0 ? "FootL" : "FootR",   new Vector3(0.4f * side, 0.07f, 0.24f), Vector3.zero,              new Vector3(0.28f, 0.14f, 0.6f));  // длинная стопа-лапа вперёд
        }

        go.AddComponent<Health>();
        go.AddComponent<Knockback>();
        go.AddComponent<Stagger>();
        go.AddComponent<HitFlash>();

        // доставки босса: те же компоненты, что у волка, но с числами вервольфа (вампиризм)
        var bite = go.AddComponent<BiteAbility>();
        PrefabConfig.Set(bite, ("windupTime", 0.4f), ("range", 2.5f), ("halfAngle", 60f), ("damage", 28), ("lifeSteal", 25), ("gizmoHeight", 1.4f));
        var leap = go.AddComponent<LeapAbility>();
        PrefabConfig.Set(leap, ("windupTime", 0.5f), ("minRange", 6f), ("maxRange", 11f), ("speed", 16f), ("up", 6f),
                        ("duration", 0.55f), ("damage", 30), ("lifeSteal", 25), ("hitRadius", 2f), ("gizmoHeight", 1.4f));

        // вервольф под ВЕЧНОЙ яростью — психика сама поддерживает её каждый кадр (флага permanent больше нет)
        go.AddComponent<Rage>();

        // ШАССИ: человек + фулл волчьи аугументы ×2 (= потолок игрока на 100 родства); превосходство даёт ярость.
        // Витальность (HP/броня/реген) — конституция психики (applyVitals=false), тело кормит урон и скорость.
        var creatureBody = go.AddComponent<CreatureBody>();
        var human = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset");
        var wolfSpecies = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
        if (human == null || wolfSpecies == null)
            Debug.LogWarning("WerewolfPrefab: ассеты видов не найдены — прогони «Chimera → Создать дефолтные виды» и пересоздай префаб.");
        var bodySo = new SerializedObject(creatureBody);
        bodySo.FindProperty("chassis").objectReferenceValue = human;
        var donorsProp = bodySo.FindProperty("donors");
        donorsProp.arraySize = 1;
        donorsProp.GetArrayElementAtIndex(0).objectReferenceValue = wolfSpecies;
        bodySo.FindProperty("installAllBeast").boolValue = true;
        bodySo.FindProperty("expression").floatValue = 2f; // экспрессия = потолок игрока; превосходство даёт ярость
        // ВИТАЛЬНОСТЬ — ИЗ ТЕЛА (костыль откручен): человеческая база × максимум волчьих процентов = 300 HP,
        // ровно столько же, сколько психика ставила руками. Но броня по формуле вышла бы 0.6 (волчья шкура
        // на Э=2), а босс задуман «быстрым убийцей, не танком» — поэтому ему свой ПОТОЛОК брони 0.15.
        // Намерение осталось прежним, только выражено данными тела, а не числом в психике
        bodySo.FindProperty("applyVitals").boolValue = true;
        bodySo.FindProperty("maxDamageReduction").floatValue = 0.15f;
        bodySo.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<WerewolfPsyche>();
        go.AddComponent<SuperBossReward>(); // суперхимера: первое убийство типа — +пул + химерный слот (дефолты компонента)
        go.AddComponent<Massive>();         // массивная туша: обхват игрока держит босса на стадию слабее
        return go;
    }

}
