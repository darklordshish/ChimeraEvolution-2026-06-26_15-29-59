using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev-утилита: пересобирает МОДЕЛЬ игрока-человека (кубы-плейсхолдеры) на существующем объекте в сцене.
/// Полуавтомат: сам объект игрока (контроллер/тело/камера/проводка) НЕ трогается — сносятся и строятся
/// заново только визуальные детали-дети. Меню: Chimera → Собрать модель игрока. Идемпотентно.
/// Имена «Head»/«Nose» обязаны сохраняться — их прячет от первого лица PlayerController.SetFirstPerson;
/// тинт-бленд состава (CreatureBody.UpdateTint) красит все дочерние рендеры сам. Editor-only.
/// </summary>
public static class PlayerModel
{
    // все имена деталей, которые генератор считает СВОИМИ (сносит перед пересборкой)
    static readonly string[] Known =
        { "Capsule", "Body", "Chest", "Neck", "Pelvis", "Head", "Nose", "ArmL", "ArmR", "HandL", "HandR", "LegL", "LegR",
          "EyeL", "EyeR", "BrowL", "BrowR", "Beard", "Model" };

    const string ModelPath = "Assets/_Chimera/Models/Player.fbx";
    const string EyeMatPath = "Assets/_Chimera/Materials/PlayerEyes.mat";
    const string BeardMatPath = "Assets/_Chimera/Materials/PlayerBeard.mat";
    const string TeethMatPath = "Assets/_Chimera/Materials/PlayerTeeth.mat";
    const string SkinMatPath = "Assets/_Chimera/Materials/PlayerSkin.mat";

    [MenuItem("Chimera/Собрать модель игрока")]
    public static void Rebuild()
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) { Debug.LogWarning("PlayerModel: PlayerController в открытой сцене не найден."); return; }
        var t = pc.transform;
        // корень игрока — ЦЕНТР капсулы CC (в сцене: center 0, height 2 → низ на root−1). Модель строим
        // ОТ НИЗА КОЛЛАЙДЕРА (= земля), иначе человек парит в метре над землёй
        var cc = pc.GetComponent<CharacterController>();
        float footY = cc != null ? cc.center.y - cc.height * 0.5f : 0f;

        // старая модель: примитив-меш НА КОРНЕ (капсула первых дней) — СНОСИМ, а не гасим. Гасить мало:
        // системы, что собирают рендеры оптом (Camouflage «прячу/показываю тело»), воскрешали её
        // enabled=true и капсула накрывала модель. Коллизией владеет CharacterController
        if (pc.TryGetComponent<MeshRenderer>(out var rootRenderer)) Object.DestroyImmediate(rootRenderer);
        if (pc.TryGetComponent<MeshFilter>(out var rootFilter)) Object.DestroyImmediate(rootFilter);

        // снести известные детали (идемпотентность повторного запуска)
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (System.Array.IndexOf(Known, c.name) >= 0) Object.DestroyImmediate(c.gameObject);
        }

        // МОДЕЛЬ из Blender, если она есть; кубы остаются запасным визуалом (и историей проекта)
        if (TryAttachModel(t, footY))
        {
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Debug.Log("Модель игрока подключена из " + ModelPath + ". Сохрани сцену (Ctrl+S). "
                    + "Тинт состава и FPS-скрытие работают по именам деталей — они совпадают с контрактом.");
            return;
        }

        // ── человек-учёный из кубов (рост ~1.9; худой — «до озверения») ──
        GameObject Part(string name, Vector3 pos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.transform.SetParent(t, false);
            p.transform.localPosition = pos + Vector3.up * footY; // все высоты заданы ОТ ЗЕМЛИ — сдвиг к низу CC
            p.transform.localScale = scale;
            Object.DestroyImmediate(p.GetComponent<Collider>()); // коллизия — на CharacterController
            return p;
        }

        Part("Chest",  new Vector3(0f, 1.32f, 0f),     new Vector3(0.44f, 0.52f, 0.26f)); // грудная клетка (верх ~1.58)
        Part("Neck",   new Vector3(0f, 1.66f, 0f),     new Vector3(0.15f, 0.22f, 0.15f)); // шея в зазоре грудь(1.58)→голова(1.71): видна ~0.13
        Part("Pelvis", new Vector3(0f, 0.95f, 0f),     new Vector3(0.38f, 0.26f, 0.24f)); // таз — уже груди
        Part("Head",   new Vector3(0f, 1.87f, 0f),     new Vector3(0.3f, 0.32f, 0.3f));   // ПРИПОДНЯТА на шею; имя ЖЁСТКОЕ: FPS-скрытие
        Part("Nose",   new Vector3(0f, 1.86f, 0.18f),  new Vector3(0.045f, 0.11f, 0.08f)); // тонкий изящный нос; имя ЖЁСТКОЕ: FPS-скрытие
        for (int side = -1; side <= 1; side += 2)
        {
            Part(side < 0 ? "ArmL" : "ArmR",   new Vector3(0.29f * side, 1.22f, 0f),    new Vector3(0.11f, 0.58f, 0.11f)); // рука вдоль торса
            Part(side < 0 ? "HandL" : "HandR", new Vector3(0.29f * side, 0.88f, 0.01f), new Vector3(0.13f, 0.12f, 0.13f)); // кисть
            Part(side < 0 ? "LegL" : "LegR",   new Vector3(0.11f * side, 0.44f, 0f),    new Vector3(0.16f, 0.88f, 0.16f)); // нога до земли
        }

        // ── ЛИЦО: глаза + кустистые брови + борода-лопата (учёный!). Свои материалы — тинт состава эти
        // детали НЕ красит (CreatureBody исключает по именам), FPS-скрытие прячет вместе с головой ──
        var eyeMat = GetOrCreateMat(EyeMatPath, new Color(0.1f, 0.1f, 0.12f));    // тёмные глаза
        var beardMat = GetOrCreateMat(BeardMatPath, new Color(0.38f, 0.33f, 0.27f)); // седеющая борода
        for (int side = -1; side <= 1; side += 2)
        {
            Part(side < 0 ? "EyeL" : "EyeR",   new Vector3(0.07f * side, 1.92f, 0.155f),  new Vector3(0.055f, 0.05f, 0.02f))
                .GetComponent<Renderer>().sharedMaterial = eyeMat;
            Part(side < 0 ? "BrowL" : "BrowR", new Vector3(0.07f * side, 1.98f, 0.155f), new Vector3(0.09f, 0.025f, 0.02f))
                .GetComponent<Renderer>().sharedMaterial = beardMat; // брови — того же волоса, что борода
        }
        Part("Beard", new Vector3(0f, 1.74f, 0.13f), new Vector3(0.24f, 0.18f, 0.14f))
            .GetComponent<Renderer>().sharedMaterial = beardMat; // борода-лопата: подбородок и ниже головы

        // неизвестные визуальные дети (ручные украшения?) не трогаем — но покажем, чтобы решить их судьбу
        foreach (var r in pc.GetComponentsInChildren<Renderer>())
            if (r.transform != t && System.Array.IndexOf(Known, r.name) < 0)
                Debug.Log($"PlayerModel: осталась НЕизвестная деталь «{r.name}» — скажи, снести или оставить.");

        EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        Debug.Log("Модель игрока пересобрана (человек из кубов, с лицом). Сохрани сцену (Ctrl+S). Тинт состава/FPS-скрытие подхватятся сами.");
    }

    /// <summary>Модель из Blender: вставляем ВЛОЖЕННЫМ префабом (`InstantiatePrefab`, не `Instantiate`) —
    /// связь с FBX живая, и перегенерация модели в соседней линии работ подхватится сама.
    ///
    /// ПОВОРОТ КОРНЯ, приходящий из файла, НЕ ТРОГАЕМ (README моделей: обнуление кладёт фигуру на нос).
    /// Двигаем только по вертикали: модель построена от нуля, а корень игрока — ЦЕНТР капсулы
    /// контроллера, поэтому её надо опустить к подошвам.
    ///
    /// Материалы красим ПО ИМЕНАМ: кожа отдельно от лица не случайно — тинт состава (`CreatureBody`)
    /// перекрашивает тело по мере озверения, но обходит глаза, брови, бороду и зубы. Борода учёного на
    /// озверевшем теле — намеренный образ, а не недосмотр.</summary>
    static bool TryAttachModel(Transform parent, float footY)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (fbx == null) return false;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        inst.name = "Model";
        inst.transform.SetParent(parent, false);
        inst.transform.localPosition = new Vector3(0f, footY, 0f);

        var skin = GetOrCreateMat(SkinMatPath, new Color(0.70f, 0.56f, 0.46f), 0.75f);
        var eyes = GetOrCreateMat(EyeMatPath, new Color(0.10f, 0.10f, 0.12f), 0.25f);
        var hair = GetOrCreateMat(BeardMatPath, new Color(0.38f, 0.33f, 0.27f), 0.85f);
        var teeth = GetOrCreateMat(TeethMatPath, new Color(0.88f, 0.86f, 0.79f), 0.45f);

        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            string n = r.gameObject.name;
            r.sharedMaterial = n == "EyeL" || n == "EyeR" ? eyes
                             : n == "BrowL" || n == "BrowR" || n == "Beard" ? hair
                             : n == "Teeth" ? teeth
                             : skin;
        }
        return true;
    }

    // материал по пути: загрузить или создать с цветом (идемпотентно, как у других генераторов)
    static Material GetOrCreateMat(string path, Color color, float smoothness = -1f)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube); // взять дефолтный шейдер пайплайна
            mat = new Material(probe.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(probe);
            AssetDatabase.CreateAsset(mat, path);
        }
        // цвет и блеск обновляем ВСЕГДА, а не только при создании: иначе правка палитры в коде молча
        // не доезжала бы до уже существующего ассета
        mat.SetColor("_BaseColor", color);
        if (smoothness >= 0f && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
