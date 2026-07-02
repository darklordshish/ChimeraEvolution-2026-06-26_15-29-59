using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dev-панель: докаемое окно с кнопками для быстрой итерации (родство, игрок, волки).
/// Меню: Chimera → Dev-панель. Работает в Play Mode. Editor-only.
/// </summary>
public class ChimeraDevWindow : EditorWindow
{
    const string Species = "Волк";
    int affinityField = 100;

    [MenuItem("Chimera/Dev-панель")]
    public static void Open() => GetWindow<ChimeraDevWindow>("CHIMERA Dev");

    void Update() { if (Application.isPlaying) Repaint(); } // живые значения в Play

    void OnGUI()
    {
        EditorGUILayout.LabelField("Инструменты разработки", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Работает в Play Mode — зайди в Play.", MessageType.Info);
            return;
        }

        var pc = Object.FindAnyObjectByType<PlayerController>();
        var health = pc != null ? pc.GetComponent<Health>() : null;

        // ── Родство ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Родство [{Species}]: {AffinityTracker.Get(Species)}", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Обнулить")) AffinityTracker.Set(Species, 0);
            if (GUILayout.Button("+10")) AffinityTracker.Add(Species, 10);
            if (GUILayout.Button("= 80")) AffinityTracker.Set(Species, 80);
            if (GUILayout.Button("= 100")) AffinityTracker.Set(Species, 100);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            affinityField = EditorGUILayout.IntField("Точно:", affinityField);
            if (GUILayout.Button("Set", GUILayout.Width(60))) AffinityTracker.Set(Species, affinityField);
        }

        // ── Игрок ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Игрок", EditorStyles.boldLabel);
        if (health != null)
        {
            EditorGUILayout.LabelField($"HP {health.Current}/{health.Max}   God: {(health.GodMode ? "ВКЛ" : "выкл")}   В бою: {(health.InCombat ? "да" : "нет")}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(health.GodMode ? "God: выкл" : "God: вкл")) health.GodMode = !health.GodMode;
                if (GUILayout.Button("Лечить до полного")) health.Heal(health.Max);
                if (GUILayout.Button("−20 HP")) health.TakeDamage(20, true);
            }
        }
        else EditorGUILayout.HelpBox("Игрок (PlayerController) не найден.", MessageType.Warning);

        // ── Волки ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Волки", EditorStyles.boldLabel);
        var wolves = Object.FindObjectsByType<WolfPsyche>();
        EditorGUILayout.LabelField($"Живых: {wolves.Length}");
        using (new EditorGUILayout.HorizontalScope())
        {
            var spawner = Object.FindAnyObjectByType<WolfSpawner>();
            using (new EditorGUI.DisabledScope(spawner == null))
                if (GUILayout.Button("+3 волка")) spawner.SpawnBurst(3);
            if (GUILayout.Button("Убить всех"))
                foreach (var w in wolves)
                    if (w.TryGetComponent<Health>(out var h)) h.TakeDamage(99999, true);
        }

        // ── Босс ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Босс — Вервольф", EditorStyles.boldLabel);
        var boss = Object.FindAnyObjectByType<WerewolfPsyche>();
        if (boss != null && boss.TryGetComponent<Health>(out var bh))
            EditorGUILayout.LabelField($"HP {bh.Current}/{bh.Max}{(bh.Current > bh.Max ? $"  (+{bh.Current - bh.Max} temp)" : "")}");
        using (new EditorGUI.DisabledScope(boss != null))
            if (GUILayout.Button("Спавн Вервольфа")) SpawnWerewolf();
        if (boss != null && GUILayout.Button("Убить босса") && boss.TryGetComponent<Health>(out var bk))
            bk.TakeDamage(999999, true);
    }

    static void SpawnWerewolf()
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        Vector3 pos = (pc != null ? pc.transform.position : Vector3.zero) + new Vector3(14f, 0f, 0f);
        if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;

        // префаб (с твоим тюнингом), если создан через «Chimera → Создать префаб Вервольфа»; иначе — сборка с нуля
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WerewolfPrefab.Path);
        var go = prefab != null ? Object.Instantiate(prefab) : WerewolfPrefab.BuildWerewolf();
        go.transform.position = pos;
    }
}
