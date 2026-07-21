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

    [Header("Обновление")]
    [SerializeField] float rescanInterval = 0.25f; // как часто пересобираем список существ (не каждый кадр)

    // цвета: здоровье — от зелёного к красному; статусы — из общей легенды F1
    static readonly Color HpFull = new(0.35f, 0.75f, 0.30f);
    static readonly Color HpLow = new(0.80f, 0.18f, 0.15f);
    static readonly Color BarBack = new(0.05f, 0.05f, 0.05f, 0.65f);
    static readonly Color OwnText = new(0.94f, 0.92f, 0.86f);

    /// <summary>Показывать полоски ВСЕМ воспринимаемым (наблюдение за сценами в призраке) или только тем,
    /// кто дерётся. Тумблер — клавиша H.</summary>
    public static bool ShowAll;

    Canvas canvas;
    Font font;
    InputAction toggleAll;

    PlayerController player;
    Health playerHealth;
    PlayerConstrict constrict;

    readonly List<Bar> pool = new();
    readonly List<Health> targets = new();
    float nextRescan;

    // своя панель
    Image ownFill;
    Text ownText, grabText;

    class Bar
    {
        public RectTransform root;
        public Image bg, fill;
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
    }

    void OnEnable() => toggleAll.Enable();
    void OnDisable() => toggleAll.Disable();

    void LateUpdate() // после движения камеры — иначе полоски дрожат на кадр позади
    {
        if (toggleAll.WasPressedThisFrame()) ShowAll = !ShowAll;

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                playerHealth = player.GetComponent<Health>();
                player.TryGetComponent(out constrict);
            }
        }

        DrawOwn();
        DrawWorld();
    }

    // ── свои данные: HP и статусы видны ЧИСЛОМ всегда — своё тело учёный знает изнутри ──
    void DrawOwn()
    {
        if (playerHealth == null) return;

        float t = playerHealth.Max > 0 ? Mathf.Clamp01((float)playerHealth.Current / playerHealth.Max) : 0f;
        ownFill.rectTransform.sizeDelta = new Vector2(ownBarWidth * t, ownBarHeight);
        ownFill.color = Color.Lerp(HpLow, HpFull, t);

        string combat = !playerHealth.InCombat
            ? (playerHealth.OutOfCombatRegen > 0f ? "   вне боя — реген" : "   вне боя") : "";
        ownText.text = $"{playerHealth.Current}/{playerHealth.Max}{combat}{StatusText(playerHealth)}";

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
            bar.status.text = StatusText(h);
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

    // строка статусов: глиф + число, цветов легенды. Глифы — из БАЗОВОЙ плоскости Unicode:
    // встроенный шрифт эмодзи не знает, настоящие иконки придут вместе со спрайтовым атласом
    static string StatusText(Health h)
    {
        string s = "";
        if (h.TryGetComponent<Venom>(out var v) && v.Stacks > 0) s += $"  <color=#73D933>☠{v.Stacks}</color>";
        if (h.TryGetComponent<Bleed>(out var b) && b.Stacks > 0) s += $"  <color=#B80A1F>♦♦{b.Stacks}</color>";
        if (h.TryGetComponent<Stagger>(out var st) && st.IsStunned) s += "  <color=#EBEBD9>✱</color>";
        if (h.TryGetComponent<Morale>(out var m) && Mathf.Abs(m.Current) >= 0.5f)
            s += m.Current > 0f ? $"  <color=#B81A1A>☺{m.Current:0}</color>" : $"  <color=#4069E6>☹{m.Current:0}</color>";
        return s;
    }

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

        // своя полоса — левый низ
        var ownRoot = new GameObject("Own", typeof(RectTransform));
        ownRoot.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)ownRoot.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(28f, 28f);
        rt.sizeDelta = new Vector2(ownBarWidth, ownBarHeight);

        var back = NewImage("Back", ownRoot.transform, BarBack);
        back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = Vector2.zero;
        back.rectTransform.sizeDelta = new Vector2(ownBarWidth, ownBarHeight);

        ownFill = NewImage("Fill", ownRoot.transform, HpFull);
        ownFill.rectTransform.anchorMin = ownFill.rectTransform.anchorMax = ownFill.rectTransform.pivot = Vector2.zero;
        ownFill.rectTransform.sizeDelta = new Vector2(ownBarWidth, ownBarHeight);

        ownText = NewText("Text", ownRoot.transform, 17, OwnText, TextAnchor.LowerLeft);
        ownText.rectTransform.anchorMin = ownText.rectTransform.anchorMax = ownText.rectTransform.pivot = Vector2.zero;
        ownText.rectTransform.anchoredPosition = new Vector2(2f, ownBarHeight + 3f);
        ownText.rectTransform.sizeDelta = new Vector2(760f, 24f);

        grabText = NewText("Grab", ownRoot.transform, 17, new Color(1f, 0.83f, 0.45f), TextAnchor.LowerLeft);
        grabText.rectTransform.anchorMin = grabText.rectTransform.anchorMax = grabText.rectTransform.pivot = Vector2.zero;
        grabText.rectTransform.anchoredPosition = new Vector2(2f, ownBarHeight + 26f);
        grabText.rectTransform.sizeDelta = new Vector2(860f, 24f);
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

            var st = NewText("Status", root.transform, 15, Color.white, TextAnchor.MiddleCenter);
            st.rectTransform.anchorMin = st.rectTransform.anchorMax = st.rectTransform.pivot = new Vector2(0.5f, 0f);
            st.rectTransform.anchoredPosition = new Vector2(0f, barHeight * 0.5f + 2f);
            st.rectTransform.sizeDelta = new Vector2(220f, 20f);
            st.supportRichText = true;

            pool.Add(new Bar { root = rt, bg = bg, fill = fill, status = st });
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
