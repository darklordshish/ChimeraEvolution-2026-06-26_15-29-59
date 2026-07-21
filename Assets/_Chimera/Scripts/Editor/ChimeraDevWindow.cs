using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dev-панель: докаемое окно для быстрой итерации. Меню: Chimera → Dev-панель. Работает в Play Mode.
///
/// Раскладка (три блока, каждый — про своё):
///  • ИГРОК — всё, что делаем С игроком: витальность, читы, его чувства и заметность миру;
///  • ВИДЫ — строка на вид: родство игрока к нему И поголовье в сцене (это про один и тот же вид — нечего разносить);
///  • НАБЛЮДЕНИЕ / СОСТАВ ТЕЛА — диагностика; на широком окне встают в две колонки.
///
/// Сюда переехали бывшие экранные хоткеи (G/K/L/;/N/T) — экран освобождён под настоящий HUD. Editor-only.
/// </summary>
public class ChimeraDevWindow : EditorWindow
{
    const float TwoColumnWidth = 760f; // шире этого — наблюдение и состав тела встают рядом, а не друг под другом
    const float WideRowWidth = 580f;   // уже этого — строка вида ломается надвое (родство / поголовье)

    Vector2 scroll;
    bool showPlayer = true, showSpecies = true, showWatch = true, showBody;

    [MenuItem("Chimera/Dev-панель")]
    public static void Open() => GetWindow<ChimeraDevWindow>("CHIMERA Dev");

    void Update() { if (Application.isPlaying) Repaint(); } // живые значения в Play

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("Инструменты разработки", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Работает в Play Mode — зайди в Play.", MessageType.Info);
            return;
        }

        var pc = Object.FindAnyObjectByType<PlayerController>();
        var health = pc != null ? pc.GetComponent<Health>() : null;
        var pb = CreatureBody.PlayerBody;

        // ТОЛЬКО вертикальный скролл: горизонтальный ползунок = раскладка не подстроилась под ширину.
        // Пусть такие места ломаются на глазах, а не прячутся за прокруткой.
        scroll = EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);

        DrawPlayer(health);
        DrawSpecies(pb);

        // диагностика — в колонки, если окно позволяет
        if (position.width >= TwoColumnWidth)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.5f - 14f)))
                    DrawWatch(pc, health);
                using (new EditorGUILayout.VerticalScope())
                    DrawBody(pb);
            }
        }
        else
        {
            DrawWatch(pc, health);
            DrawBody(pb);
        }

        EditorGUILayout.EndScrollView();
    }

    // ── ИГРОК: витальность + читы + чувства/заметность ───────────────────────
    void DrawPlayer(Health health)
    {
        EditorGUILayout.Space();
        showPlayer = EditorGUILayout.Foldout(showPlayer, "Игрок", true, EditorStyles.foldoutHeader);
        if (!showPlayer) return;

        if (health == null)
        {
            EditorGUILayout.HelpBox("Игрок (PlayerController) не найден.", MessageType.Warning);
            return;
        }

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

        // ── чувства и заметность: то же «про игрока», только со стороны мира ──
        EditorGUILayout.Space(2);
        // ПРИЗРАК: чувства NPC игрока не воспринимают (наблюдение за психикой в естественной среде).
        // Атака раскрывает (дальше всё натурально: вой, сбор стаи); повторное ВКЛ сбрасывает интерес.
        if (GUILayout.Button(Perception.PlayerGhost ? "ПРИЗРАК: ВКЛ — не видят/не чуют (атака раскроет)" : "Призрак: выкл"))
        {
            Perception.PlayerGhost = !Perception.PlayerGhost;
            if (Perception.PlayerGhost) ResetNpcInterest(); // подошёл → триггернул → отошёл → снова призрак
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"Свой запах: {(Perception.ShowOwnScent ? "ВКЛ" : "выкл")}"))
                Perception.ShowOwnScent = !Perception.ShowOwnScent;
            if (GUILayout.Button($"Термо (форс): {(Perception.DevThermal ? "ВКЛ" : "выкл")}"))
                Perception.DevThermal = !Perception.DevThermal;
        }

        var noise = health.GetComponent<Noise>(); // ось звука: 0 = беззвучен, 1 = бег/рывок (слышит лосиное ухо)
        string noiseStr = noise != null
            ? $"   Шум: {noise.Loudness:0.00}{(noise.Loudness < 0.15f ? " (тихо)" : noise.Loudness > 0.6f ? " (ГРОМКО)" : "")}" : "";
        EditorGUILayout.LabelField($"Запах-орган: {(Perception.WolfScent ? "есть" : "нет")}   " +
                                   $"Термо: {(Perception.ThermalOn ? (Perception.SnakeThermal ? "орган" : "дев") : "выкл")}{noiseStr}",
                                   EditorStyles.wordWrappedLabel);
    }

    // ── ВИДЫ: родство игрока + поголовье в одной строке (это про один и тот же вид) ──
    void DrawSpecies(CreatureBody pb)
    {
        EditorGUILayout.Space();
        showSpecies = EditorGUILayout.Foldout(showSpecies, "Виды: родство и поголовье", true, EditorStyles.foldoutHeader);
        if (!showSpecies) return;

        var wolves = Object.FindObjectsByType<WolfPsyche>();
        var wolfSpawner = Object.FindAnyObjectByType<WolfSpawner>();
        SpeciesRow(pb, "Волк", wolves.Length,
            "+1", wolfSpawner != null ? () => wolfSpawner.SpawnBurst(1) : null,
            "+3", wolfSpawner != null ? () => wolfSpawner.SpawnBurst(3) : null,
            () => KillAll(wolves));

        var snakes = Object.FindObjectsByType<SnakePsyche>();
        var snakeSpawner = Object.FindAnyObjectByType<SnakeSpawner>();
        SpeciesRow(pb, "Змея", snakes.Length,
            "рядом", SpawnSnake,
            "+3", snakeSpawner != null ? () => snakeSpawner.SpawnBurst(3) : null,
            () => KillAll(snakes));

        var moose = Object.FindObjectsByType<MoosePsyche>();
        var mooseSpawner = Object.FindAnyObjectByType<MooseSpawner>();
        SpeciesRow(pb, "Лось", moose.Length,
            "рядом", SpawnMoose,
            "+1", mooseSpawner != null ? () => mooseSpawner.SpawnBurst(1) : null,
            () => KillAll(moose));

        // Человек — полноценный вид со своим родством, но в сцене не водится: только левая половина строки
        SpeciesRow(pb, "Человек", -1, null, null, null, null, null);

        // босс — отдельная строка: спавнится в одном экземпляре, плюс тумблер автоспавна
        var boss = Object.FindAnyObjectByType<WerewolfPsyche>();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Вервольф", EditorStyles.boldLabel, GUILayout.Width(62));
            string bossHp = boss != null && boss.TryGetComponent<Health>(out var bh)
                ? $"HP {bh.Current}/{bh.Max}{(bh.Current > bh.Max ? $" +{bh.Current - bh.Max}t" : "")}" : "нет в сцене";
            EditorGUILayout.LabelField(bossHp, GUILayout.Width(150));
            using (new EditorGUI.DisabledScope(boss != null))
                if (GUILayout.Button("спавн", GUILayout.Width(56))) SpawnWerewolf();
            using (new EditorGUI.DisabledScope(boss == null))
                if (GUILayout.Button("убить", GUILayout.Width(56)) && boss.TryGetComponent<Health>(out var bk))
                    bk.TakeDamage(999999, true);
            var wwSpawner = Object.FindAnyObjectByType<WerewolfSpawner>();
            using (new EditorGUI.DisabledScope(wwSpawner == null))
                if (GUILayout.Button(wwSpawner != null && wwSpawner.AutoSpawn ? "авто: ВКЛ" : "авто: выкл"))
                    wwSpawner.AutoSpawn = !wwSpawner.AutoSpawn;
        }

        // разброс особей: множители/личность живут в get-only свойствах (в инспекторе НЕ видны) — дамп в консоль
        if (GUILayout.Button("Разброс волков → консоль"))
            foreach (var w in wolves)
            {
                string t = w.name;
                if (w.TryGetComponent<Health>(out var h)) t += $"  HP {h.Current}/{h.Max}";
                if (w.TryGetComponent<SpawnVariance>(out var v)) t += $"  hp×{v.HpMult:0.00} ск×{v.SpeedMult:0.00} ур×{v.DamageMult:0.00}";
                if (w.TryGetComponent<Personality>(out var p)) t += $"  храбр {p.Bravery:0.0} агр {p.Aggression:0.00} любоп {p.Curiosity:0.00}";
                Debug.Log(t, w);
            }
    }

    /// <summary>Строка вида: родство игрока к нему + (если вид водится в сцене) поголовье и спавн.
    /// alive &lt; 0 — вид без представителей в мире (человек): рисуем только родство.
    /// В УЗКОМ окне строка ломается надвое — раскладка подстраивается, а не уезжает под горизонтальный ползунок.</summary>
    void SpeciesRow(CreatureBody pb, string species, int alive,
                    string labelA, System.Action spawnA,
                    string labelB, System.Action spawnB,
                    System.Action killAll)
    {
        bool narrow = position.width < WideRowWidth;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (narrow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(species, EditorStyles.boldLabel, GUILayout.Width(62));
                    AffinityButtons(pb, species);
                }
                if (alive >= 0)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(62); // выравниваем под имя вида — вторая строка читается как его продолжение
                        PopulationButtons(alive, labelA, spawnA, labelB, spawnB, killAll);
                    }
            }
            else
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(species, EditorStyles.boldLabel, GUILayout.Width(62));
                    AffinityButtons(pb, species);
                    if (alive < 0) GUILayout.FlexibleSpace();               // человек: в сцене не водится
                    else PopulationButtons(alive, labelA, spawnA, labelB, spawnB, killAll);
                }
        }
    }

    void AffinityButtons(CreatureBody pb, string species)
    {
        using (new EditorGUI.DisabledScope(pb == null))
        {
            EditorGUILayout.LabelField($"родство {(pb != null ? pb.GetAffinity(species) : 0)}", GUILayout.Width(84));
            if (GUILayout.Button("0", GUILayout.Width(26))) pb.SetAffinity(species, 0);
            if (GUILayout.Button("+10", GUILayout.Width(38))) pb.AddAffinity(species, 10);
            if (GUILayout.Button("80", GUILayout.Width(32))) pb.SetAffinity(species, 80);   // порог безопасности
            if (GUILayout.Button("100", GUILayout.Width(38))) pb.SetAffinity(species, 100); // потолок родства
        }
    }

    void PopulationButtons(int alive, string labelA, System.Action spawnA,
                           string labelB, System.Action spawnB, System.Action killAll)
    {
        EditorGUILayout.LabelField($"живых: {alive}", GUILayout.Width(72));
        using (new EditorGUI.DisabledScope(spawnA == null))
            if (GUILayout.Button(labelA, GUILayout.Width(54))) spawnA();
        using (new EditorGUI.DisabledScope(spawnB == null))
            if (GUILayout.Button(labelB, GUILayout.Width(54))) spawnB();
        using (new EditorGUI.DisabledScope(alive == 0))
            if (GUILayout.Button("зачистить")) killAll();
    }

    // ── НАБЛЮДЕНИЕ: диагностика живого мира (до прихода полосок над целями — единственное окно в NPC) ──
    void DrawWatch(PlayerController pc, Health health)
    {
        showWatch = EditorGUILayout.Foldout(showWatch, "Наблюдение", true, EditorStyles.foldoutHeader);
        if (!showWatch) return;

        var pack = PackCoordinator.Instance;
        string packMorale = pack.AnyRouting() ? "БЕГСТВО" : pack.Fearless ? "ЯРОСТЬ" : "норма";
        EditorGUILayout.LabelField($"Стая: атакуют {pack.AttackerCount}/{pack.MaxAttackers}", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField($"захват: {(pack.GrabActive ? "да" : "нет")}   мораль: {packMorale}");

        // ближайшее существо: HP + стаки — видно, как цель истекает от твоего укуса
        var nearHp = NearestOther(pc, health);
        if (nearHp != null)
        {
            string tags = "";
            if (nearHp.TryGetComponent<Bleed>(out var b) && b.Stacks > 0) tags += $"   кровь {b.Stacks}";
            if (nearHp.TryGetComponent<Venom>(out var v) && v.Stacks > 0) tags += $"   яд {v.Stacks}";
            EditorGUILayout.LabelField($"Ближ.: {nearHp.name}  HP {nearHp.Current}/{nearHp.Max}{tags}", EditorStyles.wordWrappedLabel);
            string traits = NearestTraits(nearHp);
            if (traits != "") EditorGUILayout.LabelField(traits, EditorStyles.wordWrappedLabel);
        }

        // машина восприятия Спок→Настор→Атака по видам. Строку показываем ВСЕГДА, даже когда вида нет
        // в сцене: молчащая строка неотличима от сломанной диагностики.
        EditorGUILayout.LabelField(NearestAlert<WolfPsyche>(pc, "Волк"));
        EditorGUILayout.LabelField(NearestAlert<SnakePsyche>(pc, "Змея"));
        EditorGUILayout.LabelField(NearestAlert<MoosePsyche>(pc, "Лось"));
    }

    // ── СОСТАВ ТЕЛА: кем тебя считают и во что обошлась сборка (дубль конструктора — но без Tab) ──
    void DrawBody(CreatureBody pb)
    {
        showBody = EditorGUILayout.Foldout(showBody, "Состав тела игрока", true, EditorStyles.foldoutHeader);
        if (!showBody || pb == null) return;

        EditorGUILayout.LabelField($"Пул мутагена: {pb.PoolUsed}/{pb.Pool}");
        EditorGUILayout.LabelField($"Звериных слотов: {pb.BeastSlots}/{pb.MaxSlots}   бонус ×{pb.BonusMult:0.00}");
        EditorGUILayout.LabelField("Идентичность:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(pb.IdentityInfo, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Слоты:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(pb.SlotsInfo, EditorStyles.wordWrappedLabel);
    }

    static void KillAll<T>(T[] items) where T : Component
    {
        foreach (var it in items)
            if (it.TryGetComponent<Health>(out var h)) h.TakeDamage(99999, true);
    }

    // ── хелперы наблюдения (переехали с экранного HUD) ───────────────────────
    static string AlertRu(Alert s) => s == Alert.Attack ? "АТАКА" : s == Alert.Wary ? "настороже" : "спокоен";

    /// <summary>Ближайшее к игроку существо типа T: состояние восприятия + мораль + дистанция.
    /// Никогда не молчит: нет вида в сцене — так и пишет, иначе пустота читается как сломанная диагностика.</summary>
    static string NearestAlert<T>(PlayerController pc, string label) where T : Component
    {
        if (pc == null) return $"{label}: игрок не найден";
        T near = null; float best = float.MaxValue;
        foreach (var c in Object.FindObjectsByType<T>())
        {
            float d = (c.transform.position - pc.transform.position).sqrMagnitude;
            if (d < best) { best = d; near = c; }
        }
        if (near == null) return $"{label}: — нет в сцене";
        if (!near.TryGetComponent<AlertState>(out var a)) return $"{label}: без AlertState";
        string mor = near.TryGetComponent<Morale>(out var m) ? $"  мораль {m.Current:+0.#;-0.#;0}" : "";
        return $"{label}: {AlertRu(a.State)}{mor}  [{Mathf.Sqrt(best):0} м]";
    }

    /// <summary>Ближайшее к игроку чужое тело (для показа HP и стаков эффектов).</summary>
    static Health NearestOther(PlayerController pc, Health own)
    {
        if (pc == null) return null;
        Health near = null; float best = float.MaxValue;
        foreach (var h in Object.FindObjectsByType<Health>())
        {
            if (h == own || h.transform == pc.transform) continue;
            float d = (h.transform.position - pc.transform.position).sqrMagnitude;
            if (d < best) { best = d; near = h; }
        }
        return near;
    }

    /// <summary>Разброс ОСОБИ любого вида: личность + множители спавна (get-only, в инспекторе не видны).</summary>
    static string NearestTraits(Health near)
    {
        if (near == null) return "";
        string s = "   особь:";
        bool any = false;
        if (near.TryGetComponent<Personality>(out var p))
        {
            s += $"  храбр {p.Bravery:0.0} · агр {p.Aggression:0.00} · любоп {p.Curiosity:0.00}";
            any = true;
        }
        if (near.TryGetComponent<SpawnVariance>(out var v))
        {
            s += $"   |  hp×{v.HpMult:0.00} ск×{v.SpeedMult:0.00} ур×{v.DamageMult:0.00}";
            any = true;
        }
        return any ? s : "";
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

    static void SpawnMoose()
    {
        var pc = Object.FindAnyObjectByType<PlayerController>();
        Vector3 pos = (pc != null ? pc.transform.position : Vector3.zero) + new Vector3(10f, 0f, 8f);
        if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoosePrefab.Path);
        var go = prefab != null ? Object.Instantiate(prefab) : MoosePrefab.BuildMoose();
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
