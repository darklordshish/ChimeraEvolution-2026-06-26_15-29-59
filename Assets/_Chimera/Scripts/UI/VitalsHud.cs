using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// МИРОВОЙ HUD (слайс B1, спека `2026-07-21-vzglyad-uchyonogo-hud-chutyo`): состояние существ живёт НАД НИМИ,
/// а не текстом в углу — информация там, куда игрок и так смотрит.
///
/// Граница «зверь чувствует — учёный измеряет»: полоска и значок положены всем безусловно, числа стаков —
/// сила человеческого Чутья (гейт придёт в B2 вместе с `Perception.Insight`).
///
/// ВИДИМОСТЬ ПО СЕНСОРИКЕ, не по радиусу: полоска есть у того, кого игрок ВОСПРИНИМАЕТ
/// (`Perception.PlayerPerceives`). Змея в засаде вне чувств полоски не имеет — засада остаётся засадой,
/// а сборка меняет не только силу, но и картину мира.
///
/// Само-бутстрап, как `ConstructorUI`: если положить объект с этим компонентом в сцену руками, авто-создание
/// не сработает и поля станут тюнящимися в инспекторе.
/// </summary>
public class VitalsHud : MonoBehaviour
{
    [Header("Полоска над существом")]
    [SerializeField] float barWidth = 88f;
    [SerializeField] float barHeight = 7f;
    [SerializeField] float headroom = 2.4f;   // на сколько метров выше точки существа висит полоска
    [SerializeField] int maxBars = 24;        // потолок одновременных полосок (дальние отсекаются)

    [Header("Свои данные (угол экрана)")]
    [SerializeField] float ownBarWidth = 260f;
    [SerializeField] float ownBarHeight = 16f;

    [Header("Сводка обстановки (фича Чутья, клавиша J)")]
    [SerializeField] int summaryRows = 7;      // сколько ближайших держим в списке
    [SerializeField] float summaryWidth = 330f;

    [Header("Обновление")]
    [SerializeField] float rescanInterval = 0.25f; // как часто пересобираем список существ (не каждый кадр)

    // цвета: здоровье — от зелёного к красному; статусы — из общей легенды F1
    static readonly Color HpFull = new(0.35f, 0.75f, 0.30f);
    static readonly Color HpLow = new(0.80f, 0.18f, 0.15f);
    static readonly Color BarBack = new(0.05f, 0.05f, 0.05f, 0.65f);
    static readonly Color OwnText = new(0.94f, 0.92f, 0.86f);
    // СТАМИНА — своя пара цветов, НЕ из палитры HP: две зелёные полоски друг под другом читались бы как одна
    static readonly Color StamFull = new(0.85f, 0.72f, 0.25f);   // тёплая охра — дыхание
    static readonly Color StamWinded = new(0.55f, 0.25f, 0.55f); // отдышка: полоска пустая И цвет чужой

    /// <summary>Показывать полоски ВСЕМ воспринимаемым (наблюдение за сценами в призраке) или только тем,
    /// кто дерётся. Тумблер — клавиша H.</summary>
    public static bool ShowAll;

    /// <summary>Показывать сводку обстановки (только с Чутьём). Тумблер — клавиша J.</summary>
    public static bool ShowSummary = true;

    Canvas canvas;
    Font font;
    InputAction toggleAll, toggleSummary;

    PlayerController player;
    Health playerHealth;
    PlayerConstrict constrict;

    readonly List<Bar> pool = new();
    readonly List<Health> targets = new();
    float nextRescan;

    // свои данные + сводка
    Image ownFill, stamFill;
    GameObject crosshair;   // прицел залпа — центр экрана, включает Чутьё (аналитика = прицельность)
    Stamina playerStamina;
    Text ownText, grabText, summaryText;
    RectTransform summaryPanel;
    readonly List<(float dist, Health h, SenseKind by)> summary = new();

    class Bar
    {
        public RectTransform root;
        public Image bg, fill;
        public Image stamBg, stamFill; // ДЫХАЛКА — только под Чутьём: измерение, а не то, что видит зверь
        public Text status;   // значки статусов + стаки одной строкой (глифы из легенды)
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<VitalsHud>() != null) return; // положен в сцену руками — не дублируем
        var go = new GameObject("VitalsHud");
        go.AddComponent<VitalsHud>();
        // ПЕРЕЖИТЬ ПЕРЕЗАГРУЗКУ СЦЕНЫ: смерть игрока делает LoadScene, а этот атрибут срабатывает ОДИН РАЗ
        // за запуск игры — без DontDestroyOnLoad HUD исчезал бы после первой смерти навсегда.
        // Экземпляр, положенный в сцену руками, живёт по правилам сцены: он пересоздаётся вместе с ней
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
        toggleAll = new InputAction("ToggleAllVitals", InputActionType.Button);
        toggleAll.AddBinding("<Keyboard>/h");
        toggleSummary = new InputAction("ToggleSummary", InputActionType.Button);
        toggleSummary.AddBinding("<Keyboard>/j");
    }

    void OnEnable() { toggleAll.Enable(); toggleSummary.Enable(); }
    void OnDisable() { toggleAll.Disable(); toggleSummary.Disable(); }

    void LateUpdate() // после движения камеры — иначе полоски дрожат на кадр позади
    {
        if (toggleAll.WasPressedThisFrame()) ShowAll = !ShowAll;
        if (toggleSummary.WasPressedThisFrame()) ShowSummary = !ShowSummary;

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                playerHealth = player.GetComponent<Health>();
                player.TryGetComponent(out constrict);
            }
        }

        if (crosshair != null) crosshair.SetActive(Perception.Insight); // прицел — фича Чутья

        DrawOwn();
        DrawWorld();
        DrawSummary();
    }

    /// <summary>СВОДКА ОБСТАНОВКИ — фича Чутья: «кто вокруг и каким чувством засечён». Здесь честно живут
    /// косвенные каналы (запах, слух): полоску они не дают — состояния не выдают, — но присутствие
    /// и направление показать обязаны. Учёный держит в голове карту происходящего, зверь только чует.</summary>
    void DrawSummary()
    {
        bool on = ShowSummary && Perception.Insight && player != null;
        summaryPanel.gameObject.SetActive(on);
        if (!on) return;

        Vector3 eye = player.transform.position;
        summary.Clear();
        foreach (var h in targets)
        {
            if (h == null) continue;
            if (!Perception.PlayerPerceives(eye, h.transform, out var by)) continue;
            summary.Add((Vector3.Distance(eye, h.transform.position), h, by));
        }
        summary.Sort((a, b) => a.dist.CompareTo(b.dist)); // ближние важнее — они и решают бой

        var sb = new System.Text.StringBuilder();
        sb.Append("<color=#C9C2AC>СВОДКА [J]</color>\n");
        int n = Mathf.Min(summaryRows, summary.Count);
        for (int i = 0; i < n; i++)
        {
            var (dist, h, by) = summary[i];
            string name = h.TryGetComponent<CreatureBody>(out var b) && b.Chassis != null ? b.Chassis.speciesName : h.name;
            // РАЗЛИЧЕНИЕ ВИДА НА СЛУХ — фича острого уха: без органа слышишь «кто-то», с ним узнаёшь, КТО
            if (by == SenseKind.Hearing && !Perception.KeenHearing) name = "кто-то";
            // КАНАЛ РЕШАЕТ, ЧТО ИМЕННО ЗНАЕШЬ: намерение читается по позе — значит только ГЛАЗАМИ.
            // Запах говорит «кто и откуда», тепло — «жив и где», слух — «шумит». Дистанция по косвенным
            // каналам приблизительна (~), точную даёт лишь взгляд
            string what = by == SenseKind.Sight
                ? (h.TryGetComponent<AlertState>(out var a) ? AlertNames.Ru(a.State) : "—")
                : by == SenseKind.Thermal ? "тёплый"
                : by == SenseKind.Scent ? "след" : "шум";
            string range = by == SenseKind.Sight ? $"{dist:0} м" : $"~{dist:0} м";
            sb.Append($"{name} · {ChannelRu(by)} · {what} · {range}\n");
        }
        if (summary.Count == 0) sb.Append("<color=#8C8778>никого не чувствую</color>");
        else if (summary.Count > n) sb.Append($"<color=#8C8778>…и ещё {summary.Count - n}</color>");
        summaryText.text = sb.ToString();
    }

    static string ChannelRu(SenseKind k) => k switch
    {
        SenseKind.Sight => "вижу",
        SenseKind.Thermal => "тепло",
        SenseKind.Scent => "запах",
        _ => "слышу",
    };


    // ── свои данные: HP и статусы видны ЧИСЛОМ всегда — своё тело учёный знает изнутри ──
    void DrawOwn()
    {
        if (playerHealth == null) return;

        float t = playerHealth.Max > 0 ? Mathf.Clamp01((float)playerHealth.Current / playerHealth.Max) : 0f;
        ownFill.rectTransform.sizeDelta = new Vector2(ownBarWidth * t, ownBarHeight);
        ownFill.color = Color.Lerp(HpLow, HpFull, t);

        // ДЫХАЛКА: длина — запас, цвет — отдышка. Ползёшь и не понимаешь почему — полоска уже объяснила
        if (playerStamina == null && player != null) player.TryGetComponent(out playerStamina);
        if (stamFill != null && playerStamina != null)
        {
            float s = playerStamina.Normalized;
            stamFill.rectTransform.sizeDelta = new Vector2(ownBarWidth * s, stamFill.rectTransform.sizeDelta.y);
            stamFill.color = playerStamina.Exhausted ? StamWinded : StamFull;
        }

        string combat = !playerHealth.InCombat
            ? (playerHealth.OutOfCombatRegen > 0f ? "   вне боя — реген" : "   вне боя") : "";
        // СВОИ данные — числом ВСЕГДА: своё тело учёный знает изнутри, органом это не отнимается
        ownText.text = $"{playerHealth.Current}/{playerHealth.Max}{combat}{StatusText(playerHealth, numbers: true)}";

        // ЗАХВАТ, двусторонне: что делают со мной и что делаю я
        string grab = "";
        if (player != null && player.IsGrabbed) grab = "СХВАЧЕН — рывок или пинок!";
        else if (constrict != null && constrict.Holding)
        {
            grab = $"ОБХВАТ ст.{constrict.Stage}" +
                   (constrict.Stage >= 2 ? (constrict.Presenting ? " — ПОД УДАРОМ" : " — ЗАЩЁЛКНУТО") : " — держи, вырывается") +
                   (constrict.Victim != null ? $"   жертва {constrict.Victim.Current}/{constrict.Victim.Max}" : "") +
                   (constrict.Stage >= 2 ? "   [F отпустить · C подставить]" : "   [F отпустить]");
        }
        grabText.text = grab;
    }

    // ── полоски над воспринимаемыми существами ──
    void DrawWorld()
    {
        var cam = Camera.main;
        if (cam == null || player == null) { HideFrom(0); return; }

        if (Time.time >= nextRescan) { Rescan(); nextRescan = Time.time + rescanInterval; }

        Vector3 eye = player.transform.position;
        int used = 0;

        foreach (var h in targets)
        {
            if (h == null || used >= maxBars) continue;
            if (!Perception.PlayerPerceives(eye, h.transform, out var by)) continue; // не чувствую — не показываю
            if (by == SenseKind.Scent || by == SenseKind.Hearing) continue;          // косвенный канал состояния не выдаёт (см. ниже)
            if (!ShowAll && !Relevant(h)) continue;                                  // спокойных скрываем, пока не попросили всех

            Vector3 world = h.transform.position + Vector3.up * headroom;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) continue; // за спиной камеры

            var bar = Bar_(used++);
            bar.root.gameObject.SetActive(true);
            bar.root.position = sp;

            float t = h.Max > 0 ? Mathf.Clamp01((float)h.Current / h.Max) : 0f;
            bar.fill.rectTransform.sizeDelta = new Vector2(barWidth * t, barHeight);
            bar.fill.color = Color.Lerp(HpLow, HpFull, t);

            // ДЫХАЛКА ЧУЖОГО — фича Чутья («зверь чувствует — учёный измеряет»): без органа полоски нет,
            // и выдохшегося врага читаешь по поведению — реже таранит, перестал дожимать хват
            Stamina breath = null;
            bool showStam = Perception.Insight && h.TryGetComponent(out breath) && breath.Max > 0f;
            bar.stamBg.enabled = showStam;
            bar.stamFill.enabled = showStam;
            if (showStam)
            {
                bar.stamFill.rectTransform.sizeDelta =
                    new Vector2(barWidth * breath.Normalized, bar.stamFill.rectTransform.sizeDelta.y);
                bar.stamFill.color = breath.Exhausted ? StamWinded : StamFull;
            }
            // ЧУЖОЕ состояние: полоска — доля («ранен»), точные числа открывает Чутьё («47/150, яд 3»)
            bar.status.text = (Perception.Insight ? $"<color=#E8E4D8>{h.Current}/{h.Max}</color>" : "")
                            + StatusText(h, numbers: Perception.Insight);
        }

        HideFrom(used);
    }

    /// <summary>Кому полоска нужна БЕЗ тумблера «показать всех»: кто дерётся или уже ранен.
    /// `Health.InCombat` для этого не годится — он значит «недавно преследовали ИГРОКА» и на NPC не ставится.</summary>
    static bool Relevant(Health h)
    {
        if (h.Current < h.Max) return true;                                  // ранен — состояние уже важно
        if (h.TryGetComponent<AlertState>(out var a) && a.State == Alert.Attack) return true; // дерётся
        if (h.TryGetComponent<Grabbed>(out var g) && g.IsHeld) return true;  // его держат — сцена, за которой следят
        return false;
    }

    /// <summary>Строка статусов: глиф из общей легенды + (с Чутьём) число. Граница проекта в одной строчке —
    /// ЧТО с существом происходит видно всем, СКОЛЬКО именно измеряет только наблюдательность учёного.
    /// Глифы из БАЗОВОЙ плоскости Unicode: встроенный шрифт эмодзи не знает (иконки придут с атласом).</summary>
    static string StatusText(Health h, bool numbers)
    {
        string s = "";
        if (h.TryGetComponent<Venom>(out var v) && v.Stacks > 0) s += $"  <color=#73D933>☠{N(v.Stacks, numbers)}</color>";
        if (h.TryGetComponent<Bleed>(out var b) && b.Stacks > 0) s += $"  <color=#B80A1F>♦♦{N(b.Stacks, numbers)}</color>";
        if (h.TryGetComponent<Slow>(out var sl) && sl.Stacks > 0) s += $"  <color=#73B3F2>❄{N(sl.Stacks, numbers)}</color>"; // замедление — стылый голубой
        if (h.TryGetComponent<Satiety>(out var sat) && sat.IsSated) s += "  <color=#7BE0A0>✚</color>"; // сыт — восстанавливается (зелёный крест)
        if (h.TryGetComponent<Stagger>(out var st) && st.IsStunned) s += "  <color=#EBEBD9>✱</color>";
        if (h.TryGetComponent<Morale>(out var m) && Mathf.Abs(m.Current) >= 0.5f)
        {
            string val = numbers ? m.Current.ToString("0") : "";
            s += m.Current > 0f ? $"  <color=#B81A1A>☺{val}</color>" : $"  <color=#4069E6>☹{val}</color>";
        }
        return s;
    }

    static string N(int stacks, bool numbers) => numbers ? stacks.ToString() : "";

    void HideFrom(int index)
    {
        for (int i = index; i < pool.Count; i++)
            if (pool[i].root != null) pool[i].root.gameObject.SetActive(false);
    }

    void Rescan()
    {
        targets.Clear();
        foreach (var h in FindObjectsByType<Health>())
        {
            if (h == playerHealth) continue;
            targets.Add(h);
        }
    }

    // ── построение UI ────────────────────────────────────────────────────────
    void BuildCanvas()
    {
        var go = new GameObject("Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // ниже конструктора (200): он перекрывает мир целиком
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ПЕРЕКРЕСТЬЕ — центр экрана, две тонкие планки крестом. Метит, куда уйдёт залп (= центр камеры).
        // Видно только с Чутьём: прицельная стрельба — аналитика, сила учёного (без органа иглы летят навскидку)
        crosshair = new GameObject("Crosshair", typeof(RectTransform));
        crosshair.transform.SetParent(canvas.transform, false);
        var chRt = (RectTransform)crosshair.transform;
        chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 0.5f);
        chRt.anchoredPosition = Vector2.zero;
        var chColor = new Color(0.94f, 0.92f, 0.86f, 0.65f);
        var chH = NewImage("H", crosshair.transform, chColor); chH.rectTransform.sizeDelta = new Vector2(18f, 2f);
        var chV = NewImage("V", crosshair.transform, chColor); chV.rectTransform.sizeDelta = new Vector2(2f, 18f);
        crosshair.SetActive(false);

        // своя полоса — левый низ
        var ownRoot = new GameObject("Own", typeof(RectTransform));
        ownRoot.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)ownRoot.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(28f, 44f); // поднято: под HP встала полоска стамины
        rt.sizeDelta = new Vector2(ownBarWidth, ownBarHeight);

        var back = NewImage("Back", ownRoot.transform, BarBack);
        back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = Vector2.zero;
        back.rectTransform.sizeDelta = new Vector2(ownBarWidth, ownBarHeight);

        ownFill = NewImage("Fill", ownRoot.transform, HpFull);
        ownFill.rectTransform.anchorMin = ownFill.rectTransform.anchorMax = ownFill.rectTransform.pivot = Vector2.zero;
        ownFill.rectTransform.sizeDelta = new Vector2(ownBarWidth, ownBarHeight);

        // СТАМИНА — тонкая полоска ПОД здоровьем: то же тело, тот же угол экрана. Тоньше HP намеренно —
        // дыхалка важна, но не важнее жизни, и иерархия должна читаться без подписи
        const float stamH = 9f;
        var stamBack = NewImage("StamBack", ownRoot.transform, BarBack);
        stamBack.rectTransform.anchorMin = stamBack.rectTransform.anchorMax = stamBack.rectTransform.pivot = Vector2.zero;
        stamBack.rectTransform.sizeDelta = new Vector2(ownBarWidth, stamH);
        stamBack.rectTransform.anchoredPosition = new Vector2(0f, -(stamH + 3f));

        stamFill = NewImage("StamFill", ownRoot.transform, StamFull);
        stamFill.rectTransform.anchorMin = stamFill.rectTransform.anchorMax = stamFill.rectTransform.pivot = Vector2.zero;
        stamFill.rectTransform.sizeDelta = new Vector2(ownBarWidth, stamH);
        stamFill.rectTransform.anchoredPosition = new Vector2(0f, -(stamH + 3f));

        ownText = NewText("Text", ownRoot.transform, 17, OwnText, TextAnchor.LowerLeft);
        ownText.rectTransform.anchorMin = ownText.rectTransform.anchorMax = ownText.rectTransform.pivot = Vector2.zero;
        ownText.rectTransform.anchoredPosition = new Vector2(2f, ownBarHeight + 3f);
        ownText.rectTransform.sizeDelta = new Vector2(760f, 24f);

        grabText = NewText("Grab", ownRoot.transform, 17, new Color(1f, 0.83f, 0.45f), TextAnchor.LowerLeft);
        grabText.rectTransform.anchorMin = grabText.rectTransform.anchorMax = grabText.rectTransform.pivot = Vector2.zero;
        grabText.rectTransform.anchoredPosition = new Vector2(2f, ownBarHeight + 26f);
        grabText.rectTransform.sizeDelta = new Vector2(860f, 24f);

        // СВОДКА — правый низ: список того, кого чувствуешь, с каналом засечки
        var panel = NewImage("Summary", canvas.transform, BarBack);
        summaryPanel = panel.rectTransform;
        summaryPanel.anchorMin = summaryPanel.anchorMax = summaryPanel.pivot = new Vector2(1f, 0f);
        summaryPanel.anchoredPosition = new Vector2(-24f, 24f);
        summaryPanel.sizeDelta = new Vector2(summaryWidth, 24f + 20f * (summaryRows + 1));

        summaryText = NewText("Text", panel.transform, 16, OwnText, TextAnchor.LowerLeft);
        summaryText.rectTransform.anchorMin = Vector2.zero;
        summaryText.rectTransform.anchorMax = Vector2.one;
        summaryText.rectTransform.offsetMin = new Vector2(12f, 10f);
        summaryText.rectTransform.offsetMax = new Vector2(-12f, -10f);
        summaryText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    Bar Bar_(int i)
    {
        while (pool.Count <= i)
        {
            var root = new GameObject("Bar", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)root.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(barWidth, barHeight);

            var bg = NewImage("Back", root.transform, BarBack);
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = bg.rectTransform.pivot = new Vector2(0f, 0.5f);
            bg.rectTransform.anchoredPosition = new Vector2(-barWidth * 0.5f, 0f);
            bg.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);

            var fill = NewImage("Fill", root.transform, HpFull);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(-barWidth * 0.5f, 0f);
            fill.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);

            // полоска дыхалки — ПОД здоровьем и тоньше него, как и у себя в углу экрана: один язык
            float stamH = Mathf.Max(3f, barHeight * 0.45f);
            float stamY = -(barHeight * 0.5f + stamH * 0.5f + 1f);

            var sbg = NewImage("StamBack", root.transform, BarBack);
            sbg.rectTransform.anchorMin = sbg.rectTransform.anchorMax = sbg.rectTransform.pivot = new Vector2(0f, 0.5f);
            sbg.rectTransform.anchoredPosition = new Vector2(-barWidth * 0.5f, stamY);
            sbg.rectTransform.sizeDelta = new Vector2(barWidth, stamH);

            var sfill = NewImage("StamFill", root.transform, StamFull);
            sfill.rectTransform.anchorMin = sfill.rectTransform.anchorMax = sfill.rectTransform.pivot = new Vector2(0f, 0.5f);
            sfill.rectTransform.anchoredPosition = new Vector2(-barWidth * 0.5f, stamY);
            sfill.rectTransform.sizeDelta = new Vector2(barWidth, stamH);

            var st = NewText("Status", root.transform, 15, Color.white, TextAnchor.MiddleCenter);
            st.rectTransform.anchorMin = st.rectTransform.anchorMax = st.rectTransform.pivot = new Vector2(0.5f, 0f);
            st.rectTransform.anchoredPosition = new Vector2(0f, barHeight * 0.5f + 2f);
            st.rectTransform.sizeDelta = new Vector2(220f, 20f);
            st.supportRichText = true;

            pool.Add(new Bar { root = rt, bg = bg, fill = fill, stamBg = sbg, stamFill = sfill, status = st });
        }
        return pool[i];
    }

    Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false; // HUD не должен перехватывать клики
        return img;
    }

    Text NewText(string name, Transform parent, int size, Color c, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = c;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        return t;
    }
}
