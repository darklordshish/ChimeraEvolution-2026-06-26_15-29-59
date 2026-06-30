using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Утилита разработки: создаёт дефолтные виды (Человек, Волк) как ассеты с готовыми числами
/// и сразу прицепляет их к ChimeraBody в открытой сцене — чтобы не вбивать значения руками.
/// Меню: Chimera → Создать дефолтные виды. Editor-only.
/// </summary>
public static class SpeciesBootstrap
{
    const string Dir = "Assets/_Chimera/Data";

    [MenuItem("Chimera/Создать дефолтные виды (Человек, Волк)")]
    public static void CreateDefaults()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Chimera", "Data");

        // ── Человек: шасси + органы по умолчанию (нынешние «базовые» числа, цена 0) ──
        var human = ScriptableObject.CreateInstance<SpeciesSO>();
        human.speciesName = "Человек";
        human.tint = new Color(0.8f, 0.7f, 0.6f);
        human.mutagenPool = 10;
        human.organs = new[]
        {
            new Organ { organName = "Кисть",  slot = "Руки",   hotkey = "1", cost = 0, damage = 10, range = 1.6f },
            new Organ { organName = "Ноги",   slot = "Ноги",   hotkey = "2", cost = 0, moveSpeed = 6f, dashSpeed = 20f },
            new Organ { organName = "Сердце", slot = "Сердце", hotkey = "3", cost = 0, atkCooldown = 0.45f, maxHp = 100, regen = 0f, regenOOC = 1f },
            new Organ { organName = "Чутьё",  slot = "Чутьё",  hotkey = "4", cost = 0, dashCooldown = 0.7f },
            new Organ { organName = "Рот",    slot = "Пасть",  hotkey = "5", cost = 0, enablesBite = false },
            new Organ { organName = "Кожа",   slot = "Шкура",  hotkey = "6", cost = 0, damageReduction = 0f },
        };
        Save(human, "Человек");

        // ── Волк: донор органов (абсолютные значения = человек + прежняя дельта) ──
        var wolf = ScriptableObject.CreateInstance<SpeciesSO>();
        wolf.speciesName = "Волк";
        wolf.tint = new Color(0.5f, 0.38f, 0.36f);
        wolf.mutagenPool = 10;
        wolf.organs = new[]
        {
            new Organ { organName = "Коготь",        slot = "Руки",   hotkey = "1", cost = 4, damage = 18, range = 1.5f },
            new Organ { organName = "Волчьи ноги",   slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 9f, dashSpeed = 30f },
            new Organ { organName = "Волчье сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.30f, maxHp = 150, regen = 2f, regenOOC = 1f },
            new Organ { organName = "Нюх",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.45f },
            new Organ { organName = "Пасть",         slot = "Пасть",  hotkey = "5", cost = 5, enablesBite = true },
            new Organ { organName = "Шкура",         slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.3f },
        };
        Save(wolf, "Волк");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── авто-привязка к ChimeraBody в открытой сцене ──
        var body = Object.FindAnyObjectByType<ChimeraBody>();
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
            Debug.Log("Виды созданы в " + Dir + " и привязаны к ChimeraBody. Сохрани сцену (Ctrl+S).");
        }
        else
        {
            Debug.Log("Виды созданы в " + Dir + ". ChimeraBody в сцене не найден — назначь chassis/donors вручную.");
        }
    }

    static void Save(SpeciesSO so, string name) => AssetDatabase.CreateAsset(so, $"{Dir}/{name}.asset");
}
