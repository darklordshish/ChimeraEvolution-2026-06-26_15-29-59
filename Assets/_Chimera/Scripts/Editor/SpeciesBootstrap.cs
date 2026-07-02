using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Утилита разработки: создаёт/обновляет дефолтные виды (Человек, Волк) как ассеты с готовыми числами
/// и прицепляет их к CreatureBody в открытой сцене. Идемпотентно — повторный запуск обновляет значения
/// существующих ассетов (удобно гонять баланс). Меню: Chimera → Создать дефолтные виды. Editor-only.
/// </summary>
public static class SpeciesBootstrap
{
    const string Dir = "Assets/_Chimera/Data";

    [MenuItem("Chimera/Создать дефолтные виды (Человек, Волк)")]
    public static void CreateDefaults()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Chimera", "Data");

        // ── Человек: шасси + органы по умолчанию. Человеческие органы ТОЖЕ занимают пул (цена 2),
        //    чистый человек = 12/16 → свободно 4 = стартовый бюджет химеризации ──
        var human = GetOrCreate("Человек");
        human.speciesName = "Человек";
        human.tint = new Color(0.8f, 0.7f, 0.6f);
        human.mutagenPool = 16;
        human.organs = new[]
        {
            new Organ { organName = "Кисть",  slot = "Руки",   hotkey = "1", cost = 2, damage = 10, range = 1.6f },
            new Organ { organName = "Ноги",   slot = "Ноги",   hotkey = "2", cost = 2, moveSpeed = 6f, dashSpeed = 20f, enablesKick = true }, // пинок — фича человеческих ног
            new Organ { organName = "Сердце", slot = "Сердце", hotkey = "3", cost = 2, atkCooldown = 0.45f, maxHp = 100, regen = 0f, regenOOC = 1f },
            new Organ { organName = "Чутьё",  slot = "Чутьё",  hotkey = "4", cost = 2, dashCooldown = 0.7f },
            new Organ { organName = "Рот",    slot = "Пасть",  hotkey = "5", cost = 2, enablesBite = false },
            new Organ { organName = "Кожа",   slot = "Шкура",  hotkey = "6", cost = 2, damageReduction = 0f },
        };
        EditorUtility.SetDirty(human);

        // ── Волк: донор органов (абсолютные значения = человек + прежняя дельта) ──
        var wolf = GetOrCreate("Волк");
        wolf.speciesName = "Волк";
        wolf.tint = new Color(0.5f, 0.38f, 0.36f);
        wolf.mutagenPool = 16;
        wolf.organs = new[]
        {
            new Organ { organName = "Коготь",        slot = "Руки",   hotkey = "1", cost = 4, damage = 18, range = 1.5f },
            new Organ { organName = "Волчьи ноги",   slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 9f, dashSpeed = 30f },
            new Organ { organName = "Волчье сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.30f, maxHp = 150, regen = 2f, regenOOC = 1f },
            new Organ { organName = "Нюх",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.45f, enablesScent = true },
            new Organ { organName = "Пасть",         slot = "Пасть",  hotkey = "5", cost = 5, enablesBite = true, enablesHowl = true }, // укус + вой-стан
            new Organ { organName = "Шкура",         slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.3f },
        };
        EditorUtility.SetDirty(wolf);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── авто-привязка к телу игрока (CreatureBody) в открытой сцене ──
        var body = Object.FindAnyObjectByType<CreatureBody>();
        if (body != null)
        {
            var so = new SerializedObject(body);
            so.FindProperty("chassis").objectReferenceValue = human;
            var donorsProp = so.FindProperty("donors");
            donorsProp.arraySize = 1;
            donorsProp.GetArrayElementAtIndex(0).objectReferenceValue = wolf;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
            Debug.Log("Виды обновлены в " + Dir + " и привязаны к CreatureBody. Сохрани сцену (Ctrl+S).");
        }
        else
        {
            Debug.Log("Виды обновлены в " + Dir + ". CreatureBody в сцене не найден — назначь chassis/donors вручную.");
        }
    }

    // загрузить существующий ассет или создать новый (идемпотентность)
    static SpeciesSO GetOrCreate(string name)
    {
        string path = $"{Dir}/{name}.asset";
        var so = AssetDatabase.LoadAssetAtPath<SpeciesSO>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<SpeciesSO>();
            AssetDatabase.CreateAsset(so, path);
        }
        return so;
    }
}
