using UnityEditor;
using UnityEngine;

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
        var wolves = Object.FindObjectsByType<WolfAI>();
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
    }
}
