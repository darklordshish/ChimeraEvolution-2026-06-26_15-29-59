using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// ЭКРАН КОНСТРУКТОРА — созвездия (слайс C2, спека `2026-07-21-konstruktor-sozvezdiya`, GDD разд. 6).
/// Анатомическая тетрадь да Винчи: в центре ФИГУРА с гнёздами-слотами, по сторонам СОЗВЕЗДИЯ видов,
/// звезда тянется в гнездо.
///
/// Ключевое уточнение пользователя к GDD: **фигура и есть созвездие человека**. Он не «дефолт слота»,
/// а равноправный донор, просто нарисованный телом — родные органы висят звёздами у своих гнёзд.
/// Отсюда «снять звериное» = тот же жест, что «надеть»: перетащить родную звезду обратно.
///
/// Открывается на Tab, ставит ПОЛНУЮ ПАУЗУ (решение пользователя: над сборкой думают, а не отбиваются).
/// Сам UI живёт на unscaledDeltaTime, поэтому анимация идёт при timeScale = 0.
/// Арт ЧЕРНОВОЙ (решение пользователя: сначала функция) — контур из примитивов, созвездия схематичны;
/// витрувианский рисунок, настоящие звёздные карты и морф фигуры придут в C3.
/// </summary>
public class ConstructorUI : MonoBehaviour
{
    [SerializeField] float fadeSpeed = 8f; // скорость появления панели (в реальном времени)

    // da Vinci: пергамент + сепия
    static readonly Color DimColor = new(0.05f, 0.04f, 0.03f, 0.62f);
    static readonly Color Parchment = new(0.86f, 0.79f, 0.64f, 1f);
    static readonly Color Border = new(0.42f, 0.33f, 0.20f, 1f);
    static readonly Color Sepia = new(0.24f, 0.17f, 0.10f, 1f);
    static readonly Color SepiaFaint = new(0.34f, 0.26f, 0.17f, 1f);
    static readonly Color Ink = new(0.30f, 0.23f, 0.14f, 0.85f);      // линии фигуры
    static readonly Color SocketIdle = new(0.72f, 0.64f, 0.47f, 1f);  // гнездо в покое
    static readonly Color SocketOk = new(0.55f, 0.68f, 0.38f, 1f);    // подсвечено: сюда можно
    static readonly Color SocketNo = new(0.62f, 0.25f, 0.18f, 1f);    // отказ: не по карману / уже носишь
    static readonly Color StarNative = new(0.80f, 0.73f, 0.56f, 1f);  // звезда человека
    static readonly Color StarBeast = new(0.62f, 0.60f, 0.44f, 1f);   // звезда донора
    static readonly Color StarWorn = new(0.58f, 0.66f, 0.40f, 1f);    // надета
    static readonly Color StarDim = new(0.66f, 0.62f, 0.58f, 0.55f);  // недоступна (цена/дубль)

    public static bool IsOpen { get; private set; }

    InputAction toggleAction;
    CanvasGroup group;
    Canvas canvas;
    bool open;
    Font font;

    CreatureBody body;
    RectTransform figure, skyLeft, skyRight;
    Text metersText, hintText;

    readonly List<Socket> sockets = new();
    readonly List<Star> stars = new();
    bool built;

    /// <summary>Гнездо на фигуре = слот тела. Позиция задаётся ТИПОМ слота, а не порядком: раскладка
    /// анатомична и не едет, когда выдают химерный слот.</summary>
    class Socket
    {
        public int index;
        public RectTransform rt;
        public Image img;
        public Text label;
    }

    /// <summary>Звезда = ОРГАН вида. Одна звезда может подходить нескольким гнёздам (родное + химерные),
    /// поэтому хранит все свои посадочные места.</summary>
    class Star
    {
        public string species, organ;
        public bool native;
        public RectTransform rt;
        public Image img;
        public Text label;
        public readonly List<(int slot, int variant)> fits = new();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<ConstructorUI>() != null) return;
        var go = new GameObject("ConstructorUI");
        go.AddComponent<ConstructorUI>();
        // атрибут срабатывает ОДИН РАЗ за запуск, а смерть игрока перезагружает сцену (PlayerDeath) —
        // без этого Tab переставал открывать конструктор после первой же смерти
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        toggleAction = new InputAction("ToggleConstructor", InputActionType.Button);
        toggleAction.AddBinding("<Keyboard>/tab");

        EnsureEventSystem();
        BuildChrome();
        SetOpen(false, instant: true);
    }

    void OnEnable() => toggleAction.Enable();
    void OnDisable() => toggleAction.Disable();

    void OnDestroy() { if (open) { Time.timeScale = 1f; IsOpen = false; } }

    void Update()
    {
        if (toggleAction.WasPressedThisFrame()) SetOpen(!open);

        if (open)
        {
            Time.timeScale = 0f; // держим паузу насильно: хитстоп и прочие охотники за timeScale её не собьют
            if (!built || body == null) Rebuild();
            else if (sockets.Count != body.SlotCount) Rebuild(); // выдали химерный слот — дострой гнездо
            HandleRightClick();
            Refresh();
        }

        group.alpha = Mathf.MoveTowards(group.alpha, open ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
    }

    void SetOpen(bool value, bool instant = false)
    {
        open = value;
        IsOpen = value;
        Time.timeScale = open ? 0f : 1f; // ПОЛНАЯ пауза: сборка — это размышление, а не бой

        group.blocksRaycasts = open;
        group.interactable = open;
        if (instant) group.alpha = open ? 1f : 0f;

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

    // ── неизменная обвязка: холст, затемнение, лист, заголовок, подписи ──────
    void BuildChrome()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // поверх мирового HUD (100)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        group = canvasGo.AddComponent<CanvasGroup>();

        var dim = NewImage("Dim", canvasGo.transform, DimColor);
        Stretch(dim.rectTransform);

        var panel = NewImage("Panel", canvasGo.transform, Parchment);
        Center(panel.rectTransform, 1500, 880);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(3, -3);

        var inset = NewImage("Inset", panel.transform, new Color(0, 0, 0, 0));
        Stretch(inset.rectTransform, 22);
        var insetOutline = inset.gameObject.AddComponent<Outline>();
        insetOutline.effectColor = new Color(Border.r, Border.g, Border.b, 0.5f);
        insetOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var title = NewText("Title", panel.transform, "КОНСТРУКТОР ХИМЕРЫ", 42, Sepia, TextAnchor.UpperCenter);
        Top(title.rectTransform, 34, 56);

        var subtitle = NewText("Subtitle", panel.transform, "тяни звезду в гнездо · ПКМ по гнезду — снять · 1–6 цикл · Tab — закрыть",
                               20, SepiaFaint, TextAnchor.UpperCenter);
        Top(subtitle.rectTransform, 92, 30);

        // ЦЕНТР — фигура (созвездие человека): гнёзда на анатомических местах, родные звёзды рядом
        var fig = new GameObject("Figure", typeof(RectTransform));
        fig.transform.SetParent(panel.transform, false);
        figure = (RectTransform)fig.transform;
        Center(figure, 420, 620);
        figure.anchoredPosition = new Vector2(0, -20);
        DrawBody(figure);

        // НЕБО по сторонам — созвездия доноров
        skyLeft = NewPane("SkyLeft", panel.transform, new Vector2(-520, -20), new Vector2(420, 620));
        skyRight = NewPane("SkyRight", panel.transform, new Vector2(520, -20), new Vector2(420, 620));

        metersText = NewText("Meters", panel.transform, "", 22, Sepia, TextAnchor.LowerCenter);
        Bottom(metersText.rectTransform, 54, 30);

        hintText = NewText("Hint", panel.transform, "", 19, SepiaFaint, TextAnchor.LowerCenter);
        Bottom(hintText.rectTransform, 26, 28);
    }

    /// <summary>Черновой контур тела — палочный человек из примитивов. Это ЗАГЛУШКА под витрувианский
    /// рисунок (C3): её задача — дать гнёздам анатомический смысл, а не быть красивой.</summary>
    void DrawBody(RectTransform parent)
    {
        Line(parent, new Vector2(0, 250), new Vector2(78, 78));    // голова
        Line(parent, new Vector2(0, 90), new Vector2(96, 210));    // торс
        Line(parent, new Vector2(-104, 96), new Vector2(38, 170)); // руки
        Line(parent, new Vector2(104, 96), new Vector2(38, 170));
        Line(parent, new Vector2(-40, -130), new Vector2(38, 200)); // ноги
        Line(parent, new Vector2(40, -130), new Vector2(38, 200));
    }

    void Line(RectTransform parent, Vector2 pos, Vector2 size)
    {
        var img = NewImage("Part", parent, new Color(0, 0, 0, 0));
        Center(img.rectTransform, size.x, size.y);
        img.rectTransform.anchoredPosition = pos;
        var o = img.gameObject.AddComponent<Outline>();
        o.effectColor = Ink;
        o.effectDistance = new Vector2(2, -2);
    }

    // анатомические места гнёзд по ТИПУ слота. Нет в словаре → химерный/будущий слот, уходит столбцом сбоку
    // (новый вид не требует правки раскладки)
    static readonly Dictionary<string, Vector2> SlotPlaces = new()
    {
        ["Пасть"] = new Vector2(0, 292),
        ["Чутьё"] = new Vector2(-86, 262),
        ["Рога"] = new Vector2(86, 330),
        ["Руки"] = new Vector2(-150, 150),
        ["Сердце"] = new Vector2(0, 140),
        ["Шкура"] = new Vector2(0, 40),
        ["Тело"] = new Vector2(120, 40),
        ["Ноги"] = new Vector2(0, -190),
        ["Хвост"] = new Vector2(150, -120),
    };

    static Vector2 SocketPos(string slot, int chimeraOrder) =>
        SlotPlaces.TryGetValue(slot, out var p) ? p : new Vector2(178, 250 - chimeraOrder * 74);

    // ── сборка гнёзд и созвездий из данных тела ─────────────────────────────
    void Rebuild()
    {
        foreach (var s in sockets) if (s.rt != null) Destroy(s.rt.gameObject);
        foreach (var s in stars) if (s.rt != null) Destroy(s.rt.gameObject);
        sockets.Clear();
        stars.Clear();
        built = false;

        var pc = FindAnyObjectByType<PlayerController>();
        body = pc != null ? pc.GetComponent<CreatureBody>() : null;
        if (body == null || body.SlotCount == 0) return;

        // ГНЁЗДА
        int chimeraOrder = 0;
        for (int i = 0; i < body.SlotCount; i++)
        {
            var v = body.GetSlot(i);
            bool known = SlotPlaces.ContainsKey(v.slot);
            var pos = SocketPos(v.slot, known ? 0 : chimeraOrder++);

            var img = NewImage("Socket_" + v.slot, figure, SocketIdle);
            Center(img.rectTransform, 132, 46);
            img.rectTransform.anchoredPosition = pos;
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = Border;
            o.effectDistance = new Vector2(1.5f, -1.5f);

            var label = NewText("Label", img.transform, "", 15, Sepia, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 4);

            // ПКМ по гнезду ловим напрямую через Mouse (см. HandleRightClick): IPointerClickHandler зависит
            // от привязки rightClick в InputSystemUIInputModule сцены, а она может быть не задана — тогда
            // правый клик молча не срабатывает (левый drag при этом работает)
            sockets.Add(new Socket { index = i, rt = img.rectTransform, img = img, label = label });
        }

        // ЗВЁЗДЫ: КАЖДЫЙ вид — созвездие в небе, ВКЛЮЧАЯ ЧЕЛОВЕКА. Фигура показывает собранное тело (гнёзда),
        // а органы берутся из созвездий — оттуда человеческий орган и тянут в химерный слот («второе сердце»).
        // Группируем по (вид, орган): дубли одного органа в разных слотах — одна звезда с многими посадочными
        var byKey = new Dictionary<string, Star>();
        for (int i = 0; i < body.SlotCount; i++)
        {
            var variants = body.GetVariants(i);
            for (int v = 0; v < variants.Count; v++)
            {
                var vv = variants[v];
                string key = vv.species + "|" + vv.organName;
                if (!byKey.TryGetValue(key, out var star))
                {
                    star = new Star { species = vv.species, organ = vv.organName, native = vv.native };
                    byKey[key] = star;
                    stars.Add(star);
                }
                star.fits.Add((i, v));
            }
        }

        // РАЗМЕЩЕНИЕ: созвездия по левому и правому небу, каждый вид своей колонкой. Человек первым (его
        // органы встречаются в слоте 0) — он и его созвездие узнаются, донорские следом
        var donors = new List<string>();
        foreach (var s in stars) if (!donors.Contains(s.species)) donors.Add(s.species);

        var perSpecies = new Dictionary<string, int>();
        foreach (var s in stars)
        {
            int di = donors.IndexOf(s.species);
            RectTransform host = (di % 2 == 0) ? skyLeft : skyRight;
            perSpecies.TryGetValue(s.species, out int n);
            perSpecies[s.species] = n + 1;
            int col = di / 2;                       // вид занимает свою колонку на своём небе
            Vector2 pos = new Vector2(-100 + col * 200, 250 - n * 62);

            var img = NewImage("Star", host, StarBeast);
            Center(img.rectTransform, 176, 52);
            img.rectTransform.anchoredPosition = pos;
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = Border;
            o.effectDistance = new Vector2(1.5f, -1.5f);

            var label = NewText("Label", img.transform, "", 15, Sepia, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 4);

            var drag = img.gameObject.AddComponent<StarDrag>();
            drag.Init(this, s);

            s.rt = img.rectTransform;
            s.img = img;
            s.label = label;
        }

        // подписи созвездий
        foreach (var sp in donors)
        {
            int di = donors.IndexOf(sp);
            var host = (di % 2 == 0) ? skyLeft : skyRight;
            var cap = NewText("Cap_" + sp, host, "— созвездие: " + sp + " —", 19, SepiaFaint, TextAnchor.UpperCenter);
            Center(cap.rectTransform, 260, 26);
            cap.rectTransform.anchoredPosition = new Vector2(-100 + (di / 2) * 200, 296);
        }
        // фигура — не созвездие, а собранное ТЕЛО (созвездие Человека теперь в небе наравне с донорами)
        var figCap = NewText("FigCap", figure, "— тело химеры —", 19, SepiaFaint, TextAnchor.UpperCenter);
        Center(figCap.rectTransform, 300, 26);
        figCap.rectTransform.anchoredPosition = new Vector2(0, -270);

        built = true;
    }

    /// <summary>Родное имя органа шасси для слота — ИДЕНТИЧНОСТЬ гнезда (на фигуре человека это «Рот»,
    /// «Кисть», «Сердце», а не тип слота «Пасть»/«Руки»). Химерный слот родного органа не имеет.</summary>
    string NativeName(int slot)
    {
        foreach (var vv in body.GetVariants(slot)) if (vv.native) return vv.organName;
        return null;
    }

    /// <summary>Правый клик по гнезду — напрямую через Mouse (надёжнее IPointerClickHandler, см. Socket).</summary>
    void HandleRightClick()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;
        Vector2 mp = mouse.position.ReadValue();
        foreach (var s in sockets)
            if (RectTransformUtility.RectangleContainsScreenPoint(s.rt, mp, canvas.worldCamera))
            {
                RevertOrEmpty(s.index);
                break;
            }
    }

    /// <summary>ПКМ по гнезду: ХИМЕРНЫЙ опустошаем (у него нет родного органа — там любой человеческий лежит
    /// как вариант, потому и нельзя «вернуть родной»); ОБЫЧНЫЙ возвращаем к родному органу (= снять звериное).
    /// «Снять» и «надеть родное» — один жест, как решено в C1.</summary>
    void RevertOrEmpty(int slot)
    {
        if (body.GetSlot(slot).chimera) { body.Remove(slot); Refresh(); return; }
        var vars = body.GetVariants(slot);
        int nat = vars.FindIndex(v => v.native);
        if (nat >= 0) body.Install(slot, nat);
        Refresh();
    }

    // ── живое состояние ─────────────────────────────────────────────────────
    void Refresh()
    {
        if (body == null) return;

        for (int i = 0; i < sockets.Count && i < body.SlotCount; i++)
        {
            var v = body.GetSlot(i);
            var s = sockets[i];
            string key = string.IsNullOrEmpty(v.hotkey) ? "" : $"[{v.hotkey}] ";
            // ГНЕЗДО НАЗЫВАЕТСЯ ПО РОДНОМУ ОРГАНУ (идентичность части тела: «Рот», «Кисть»); химерный слот
            // родного не имеет — он «Химерный». Звериный графт показываем стрелкой к текущему органу
            string head = key + (v.chimera ? "Химерный" : NativeName(i) ?? v.slot);
            string content = v.installed ? $"↳ {v.organName} ({v.cost})"
                           : v.organName == "—" ? "— пусто —" : $"({v.cost})";
            s.label.text = $"{head}\n{content}";
            s.img.color = dragging == null
                ? (v.installed ? SocketOk : SocketIdle)
                : (FitsDragged(i, out bool ok) ? (ok ? SocketOk : SocketNo) : SocketIdle);
        }

        foreach (var star in stars)
        {
            if (star.rt == null) continue;
            bool worn = false, available = false;
            int cost = int.MaxValue; // разные гнёзда — разная цена (химерное ×2); показываем самую дешёвую посадку
            foreach (var (slot, variant) in star.fits)
            {
                var vv = body.GetVariants(slot)[variant];
                cost = Mathf.Min(cost, vv.cost);
                if (vv.worn) worn = true;
                if (vv.affordable && !vv.duplicate) available = true;
            }
            star.label.text = $"{star.organ}\n{cost}";
            star.img.color = worn ? StarWorn : !available ? StarDim : star.native ? StarNative : StarBeast;
        }

        metersText.text = $"Пул мутагена: {body.PoolUsed}/{body.Pool}        " +
                          $"Звериных слотов: {body.BeastSlots}/{body.MaxSlots}        " +
                          $"Бонус органов ×{body.BonusMult:0.00}";
        hintText.text = dragging != null ? "отпусти над гнездом" : "";
    }

    // ── перетаскивание ──────────────────────────────────────────────────────
    Star dragging;
    Vector2 dragHome;

    public void BeginDrag(object starObj)
    {
        dragging = (Star)starObj;
        dragHome = dragging.rt.anchoredPosition;
        dragging.rt.SetAsLastSibling();
    }

    public void DragTo(Vector2 screenPos)
    {
        if (dragging == null) return;
        var parent = (RectTransform)dragging.rt.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, canvas.worldCamera, out var local))
            dragging.rt.anchoredPosition = local;
    }

    public void EndDrag(Vector2 screenPos)
    {
        if (dragging == null) return;

        // ищем гнездо под курсором среди ПОДХОДЯЩИХ этой звезде
        foreach (var (slot, variant) in dragging.fits)
        {
            var sock = sockets.Find(x => x.index == slot);
            if (sock == null) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(sock.rt, screenPos, canvas.worldCamera)) continue;
            body.Install(slot, variant); // не влезло/дубль — Install откажет сам, звезда просто вернётся
            break;
        }

        dragging.rt.anchoredPosition = dragHome; // звезда всегда возвращается на небо: она не «уходит» в тело
        dragging = null;
        Refresh();
    }

    bool FitsDragged(int slotIndex, out bool ok)
    {
        ok = false;
        if (dragging == null) return false;
        foreach (var (slot, variant) in dragging.fits)
        {
            if (slot != slotIndex) continue;
            var vv = body.GetVariants(slot)[variant];
            ok = vv.affordable && !vv.duplicate;
            return true;
        }
        return false;
    }

    // ── мелкие компоненты ввода ─────────────────────────────────────────────
    class StarDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        ConstructorUI ui;
        object star;
        public void Init(ConstructorUI owner, object s) { ui = owner; star = s; }
        public void OnBeginDrag(PointerEventData e) => ui.BeginDrag(star);
        public void OnDrag(PointerEventData e) => ui.DragTo(e.position);
        public void OnEndDrag(PointerEventData e) => ui.EndDrag(e.position);
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>(); // проект на new Input System
    }

    // ── хелперы UI ──────────────────────────────────────────────────────────
    RectTransform NewPane(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Center(rt, size.x, size.y);
        rt.anchoredPosition = pos;
        return rt;
    }

    Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    Text NewText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false; // подписи не перехватывают перетаскивание
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
