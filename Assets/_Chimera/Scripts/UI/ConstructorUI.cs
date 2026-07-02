using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Экран конструктора химеры. Открывается на Tab, НЕ ставит игру на паузу, а замедляет время ×10
/// (компромисс: чувствуешь давление боя, но успеваешь думать). Сам UI живёт на unscaledDeltaTime,
/// поэтому анимации панели идут в реальном времени независимо от timeScale.
///
/// Готово: Tab → замедление + пергаментная панель со списком слотов органов (клик/1–6 = химеризация)
/// и сводкой сборки (пул/звериные слоты/бонус). Дальше: анатомическая фигура + созвездия родства.
///
/// Само-бутстрап: как ScentEmitter/ScentTrail у игрока — вешать в сцене руками не нужно,
/// объект создаётся автоматически при загрузке сцены.
/// </summary>
public class ConstructorUI : MonoBehaviour
{
    [SerializeField] float timeScaleWhenOpen = 0.1f; // ×10 замедление
    [SerializeField] float fadeSpeed = 8f;           // скорость появления панели (в реальном времени)

    // da Vinci: пергамент + сепия
    static readonly Color DimColor = new(0.05f, 0.04f, 0.03f, 0.55f);
    static readonly Color Parchment = new(0.86f, 0.79f, 0.64f, 1f);
    static readonly Color Border = new(0.42f, 0.33f, 0.20f, 1f);
    static readonly Color Sepia = new(0.24f, 0.17f, 0.10f, 1f);
    static readonly Color SepiaFaint = new(0.34f, 0.26f, 0.17f, 1f);
    // строки слотов
    static readonly Color RowColor = new(0.80f, 0.72f, 0.55f, 1f);     // человеческий орган
    static readonly Color RowInstalled = new(0.60f, 0.66f, 0.42f, 1f); // звериный надет (оливковый)
    static readonly Color RowLocked = new(0.79f, 0.75f, 0.68f, 1f);    // нет альтернативы — слот фиксирован
    static readonly Color CantAfford = new(0.55f, 0.22f, 0.14f, 1f);   // не влезает в пул (текст красным)

    /// <summary>Меню открыто? Боевой ввод игрока (атака/укус/пинок) на это время глушится.</summary>
    public static bool IsOpen { get; private set; }

    InputAction toggleAction;
    CanvasGroup group;   // фейд + блокировка кликов сквозь панель
    bool open;
    Font font;

    CreatureBody body;
    Transform slotsContainer;
    Text metersText;
    readonly List<SlotRow> rows = new();
    bool slotsBuilt;

    class SlotRow { public Image bg; public Button button; public Text label; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<ConstructorUI>() != null) return;
        new GameObject("ConstructorUI").AddComponent<ConstructorUI>();
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        toggleAction = new InputAction("ToggleConstructor", InputActionType.Button);
        toggleAction.AddBinding("<Keyboard>/tab");

        EnsureEventSystem();
        BuildUI();
        SetOpen(false, instant: true);
    }

    void OnEnable() => toggleAction.Enable();
    void OnDisable() => toggleAction.Disable();

    void OnDestroy()
    {
        if (open) { Time.timeScale = 1f; IsOpen = false; } // не оставляем мир замедленным / ввод заглушённым, если объект убили открытым
    }

    void Update()
    {
        if (toggleAction.WasPressedThisFrame()) SetOpen(!open);

        if (open)
        {
            // держим замедление насильно: хитстоп удара и прочие охотники за timeScale не должны его сбросить
            Time.timeScale = timeScaleWhenOpen;
            if (!slotsBuilt) BuildSlotRows();
            RefreshSlots();   // хоткеи 1–6 тоже переключают слоты — держим UI в синхроне
            RefreshMeters();
        }

        // анимация панели идёт в реальном времени — timeScale на UI не влияет
        float target = open ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
    }

    void SetOpen(bool value, bool instant = false)
    {
        open = value;
        IsOpen = value;
        Time.timeScale = open ? timeScaleWhenOpen : 1f;

        group.blocksRaycasts = open;
        group.interactable = open;
        if (instant) group.alpha = open ? 1f : 0f;

        // курсор: в конструкторе он всегда нужен; при закрытии возвращаем как было в игре
        if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else RestoreGameCursor();
    }

    void RestoreGameCursor()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        bool fps = pc != null && pc.FirstPerson;
        Cursor.lockState = fps ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !fps;
    }

    // Собираем Canvas → затемнение → пергаментную панель с заголовком. Всё кодом, чтобы не трогать редактор.
    void BuildUI()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // поверх любого игрового HUD
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        group = canvasGo.AddComponent<CanvasGroup>();

        // затемнение фона (клики по игре не проходят)
        var dim = CreateImage("Dim", canvasGo.transform, DimColor);
        Stretch(dim.rectTransform);

        // центральная пергаментная панель
        var panel = CreateImage("Panel", canvasGo.transform, Parchment);
        Center(panel.rectTransform, 1180, 760);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(3, -3);

        // тонкая рамка-инсет (второй контур — «страница блокнота»)
        var inset = CreateImage("Inset", panel.transform, new Color(0, 0, 0, 0));
        Stretch(inset.rectTransform, 22);
        var insetOutline = inset.gameObject.AddComponent<Outline>();
        insetOutline.effectColor = new Color(Border.r, Border.g, Border.b, 0.5f);
        insetOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // заголовок
        var title = CreateText("Title", panel.transform, "КОНСТРУКТОР ХИМЕРЫ", 46, Sepia, TextAnchor.UpperCenter);
        Top(title.rectTransform, 44, 60);

        var subtitle = CreateText("Subtitle", panel.transform,
            "лаборатория · слоты · созвездия родства", 22, SepiaFaint, TextAnchor.UpperCenter);
        Top(subtitle.rectTransform, 104, 34);

        // список слотов органов (клик = химеризация); анатомическую фигуру/созвездия добавим следующими шагами
        var containerGo = new GameObject("Slots", typeof(RectTransform));
        containerGo.transform.SetParent(panel.transform, false);
        slotsContainer = containerGo.transform;
        Center((RectTransform)slotsContainer, 820, 520);
        ((RectTransform)slotsContainer).anchoredPosition = new Vector2(0, -20);
        var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        // сводка сборки: пул / звериные слоты / бонус (зона «loadout meters» из макета, пока текстом)
        metersText = CreateText("Meters", panel.transform, "", 22, Sepia, TextAnchor.LowerCenter);
        Bottom(metersText.rectTransform, 60, 30);

        var hint = CreateText("Hint", panel.transform, "1–6 или клик — сменить орган · Tab — закрыть", 20, SepiaFaint, TextAnchor.LowerCenter);
        Bottom(hint.rectTransform, 28, 30);
    }

    // строим по строке на слот (один раз, когда в сцене найдено тело игрока)
    void BuildSlotRows()
    {
        body = FindAnyObjectByType<CreatureBody>();
        if (body == null || body.SlotCount == 0) return;

        for (int i = 0; i < body.SlotCount; i++)
        {
            int idx = i;
            var bg = CreateImage("Slot" + i, slotsContainer, RowColor);
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 60;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None; // цвет строки ведём сами через RefreshSlots
            btn.onClick.AddListener(() => { if (body != null) { body.ToggleSlot(idx); RefreshSlots(); RefreshMeters(); } });

            var label = CreateText("Label", bg.transform, "", 22, Sepia, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(22, 0);
            label.rectTransform.offsetMax = new Vector2(-22, 0);

            rows.Add(new SlotRow { bg = bg, button = btn, label = label });
        }
        slotsBuilt = true;
    }

    void RefreshSlots()
    {
        if (body == null) return;
        for (int i = 0; i < rows.Count && i < body.SlotCount; i++)
        {
            var v = body.GetSlot(i);
            var r = rows[i];

            string check = v.installed ? "  ✓" : "";
            string tail = !v.hasBeast ? ""                       // нет альтернативы — слот фиксирован
                        : v.installed ? $"      ← {v.humanName}"  // клик вернёт человеческий
                        : $"      → {v.beastName}";               // клик поставит звериный
            string key = string.IsNullOrEmpty(v.hotkey) ? "   " : v.hotkey;
            r.label.text = $"{key}   {v.slot}:  {v.organName}   ({v.cost}){check}{tail}";

            bool cantAfford = v.hasBeast && !v.installed && !v.canToggle;
            r.button.interactable = v.hasBeast && v.canToggle;
            r.bg.color = !v.hasBeast ? RowLocked : v.installed ? RowInstalled : RowColor;
            r.label.color = cantAfford ? CantAfford : Sepia;
        }
    }

    void RefreshMeters()
    {
        if (body == null || metersText == null) return;
        metersText.text = $"Пул мутагена: {body.PoolUsed}/{body.Pool}        " +
                          $"Звериных слотов: {body.BeastSlots}/{body.MaxSlots}        " +
                          $"Бонус органов ×{body.BonusMult:0.00}";
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>(); // проект на new Input System
    }

    // ── хелперы UI ──────────────────────────────────────────────────────────
    Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    static void Top(RectTransform rt, float fromTop, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(20, 0); rt.offsetMax = new Vector2(-20, 0);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        rt.anchoredPosition = new Vector2(0, -fromTop);
    }

    static void Bottom(RectTransform rt, float fromBottom, float h)
    {
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(20, 0); rt.offsetMax = new Vector2(-20, 0);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        rt.anchoredPosition = new Vector2(0, fromBottom);
    }
}
