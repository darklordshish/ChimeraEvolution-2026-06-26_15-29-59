using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dev-панель: докаемое окно с кнопками для быстрой итерации (родство, игрок, волки).
/// Меню: Chimera → Dev-панель. Работает в Play Mode. Editor-only.
/// </summary>
public class ChimeraDevWindow : EditorWindow
{
    static readonly string[] SpeciesList = { "Волк", "Змея", "Человек" }; // новые виды дописывать сюда
    int speciesIdx;
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

        // ── Родство (кап 100 на вид) ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Родство: Волк {AffinityTracker.Get("Волк")} · Змея {AffinityTracker.Get("Змея")} · Человек {AffinityTracker.Get("Человек")}", EditorStyles.boldLabel);
        speciesIdx = GUILayout.Toolbar(speciesIdx, SpeciesList);
        string sp = SpeciesList[speciesIdx];
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Обнулить")) AffinityTracker.Set(sp, 0);
            if (GUILayout.Button("+10")) AffinityTracker.Add(sp, 10);
            if (GUILayout.Button("= 80")) AffinityTracker.Set(sp, 80);
            if (GUILayout.Button("= 100")) AffinityTracker.Set(sp, 100);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            affinityField = EditorGUILayout.IntField("Точно:", affinityField);
            if (GUILayout.Button("Set", GUILayout.Width(60))) AffinityTracker.Set(sp, affinityField);
        }

        // ── ПРИЗРАК: чувства NPC игрока не воспринимают (наблюдение за психикой в естественной среде).
        //    Атака раскрывает (дальше всё натурально: вой, сбор стаи); повторное ВКЛ сбрасывает интерес.
        EditorGUILayout.Space();
        if (GUILayout.Button(Perception.PlayerGhost ? "ПРИЗРАК: ВКЛ — не видят/не чуют (атака раскроет)" : "Призрак: выкл"))
        {
            Perception.PlayerGhost = !Perception.PlayerGhost;
            if (Perception.PlayerGhost) ResetNpcInterest(); // подошёл → триггернул → отошёл → снова призрак
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
            using (new EditorGUILayout.HorizontalScope())
            {
                var v = health.GetComponent<Venom>();
                EditorGUILayout.LabelField($"Яд: {(v != null ? v.Stacks : 0)} стак(ов)", GUILayout.Width(120));
                if (GUILayout.Button("Отравить +1")) (health.GetComponent<Venom>() ?? health.gameObject.AddComponent<Venom>()).AddStack();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var body = health.GetComponent<CreatureBody>();
                EditorGUILayout.LabelField($"Химерных слотов: {(body != null ? body.ChimeraSlots : 0)}", GUILayout.Width(160));
                using (new EditorGUI.DisabledScope(body == null))
                {
                    if (GUILayout.Button("+1 химерный слот")) body.GrantChimeraSlot();
                    using (new EditorGUI.DisabledScope(body != null && body.ChimeraSlots == 0))
                        if (GUILayout.Button("−1")) body.RemoveChimeraSlot(); // надетый орган снимется, пул вернётся
                }
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
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(boss != null))
                if (GUILayout.Button("Спавн Вервольфа")) SpawnWerewolf();
            var wwSpawner = Object.FindAnyObjectByType<WerewolfSpawner>();
            using (new EditorGUI.DisabledScope(wwSpawner == null))
                if (GUILayout.Button(wwSpawner != null && wwSpawner.AutoSpawn ? "Автоспавн: ВКЛ" : "Автоспавн: выкл"))
                    wwSpawner.AutoSpawn = !wwSpawner.AutoSpawn;
        }
        if (boss != null && GUILayout.Button("Убить босса") && boss.TryGetComponent<Health>(out var bk))
            bk.TakeDamage(999999, true);

        // ── Змея ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Змея", EditorStyles.boldLabel);
        var snakes = Object.FindObjectsByType<SnakePsyche>();
        EditorGUILayout.LabelField($"Живых: {snakes.Length}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Спавн змеи")) SpawnSnake(); // одна, рядом с игроком (посмотреть вблизи)
            var snakeSpawner = Object.FindAnyObjectByType<SnakeSpawner>();
            using (new EditorGUI.DisabledScope(snakeSpawner == null))
                if (GUILayout.Button("+3 по карте")) snakeSpawner.SpawnBurst(3);
            if (GUILayout.Button("Убить всех змей"))
                foreach (var s in snakes)
                    if (s.TryGetComponent<Health>(out var sh)) sh.TakeDamage(99999, true);
        }
    }

    // сброс интереса NPC к игроку: тревоги волков и запаховый след стираются (свежий «чистый лист»)
    static void ResetNpcInterest()
    {
        ScentField.Instance.Clear();
        foreach (var w in Object.FindObjectsByType<WolfPsyche>()) w.ForgetAlert();
    }

    static void SpawnSnake()
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        Vector3 pos = (pc != null ? pc.transform.position : Vector3.zero) + new Vector3(8f, 0f, 6f);
        if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SnakePrefab.Path);
        var go = prefab != null ? Object.Instantiate(prefab) : SnakePrefab.BuildSnake();
        go.transform.position = pos;
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
