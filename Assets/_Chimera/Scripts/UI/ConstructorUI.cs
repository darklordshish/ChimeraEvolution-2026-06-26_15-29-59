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
    static readonly Color DimColor = new(0.04f, 0.03f, 0.02f, 0.92f); // почти глухое — игровой HUD за панелью не просвечивает
    static readonly Color Parchment = new(0.86f, 0.79f, 0.64f, 1f);
    static readonly Color Border = new(0.42f, 0.33f, 0.20f, 1f);
    static readonly Color Sepia = new(0.24f, 0.17f, 0.10f, 1f);
    static readonly Color SepiaFaint = new(0.34f, 0.26f, 0.17f, 1f);
    static readonly Color Ink = new(0.30f, 0.23f, 0.14f, 0.85f);      // линии фигуры
    static readonly Color SocketIdle = new(0.72f, 0.64f, 0.47f, 1f);  // гнездо в покое
    static readonly Color SocketOk = new(0.55f, 0.68f, 0.38f, 1f);    // подсвечено: сюда можно
    static readonly Color SocketNo = new(0.62f, 0.25f, 0.18f, 1f);    // отказ: не по карману / уже носишь
    static readonly Color StarBeast = new(0.62f, 0.60f, 0.44f, 1f);   // стартовый цвет карточки (перекрасит Refresh)

    public static bool IsOpen { get; private set; }

    InputAction toggleAction;
    CanvasGroup group;
    Canvas canvas;
    bool open;
    Font font;

    CreatureBody body;
    RectTransform figure, skyLeft, skyRight;
    Text metersText, hintText, identityText;

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
        public string species, organ, slotType; // slotType — по нему звёзды выстраиваются В ОДНУ СТРОКУ у всех видов
        public bool native;
        public RectTransform rt;
        public Image img, glyph;
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

        var panel = NewImage("Panel", canvasGo.transform, Color.white);
        Center(panel.rectTransform, 1500, 880);
        panel.sprite = DaVinciTex.Sprite(DaVinciTex.Parchment(768, 448)); // состаренная бумага вместо плоской заливки
        panel.type = Image.Type.Simple;
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

        // ЦЕНТР — витрувианская фигура (тело химеры): круг+квадрат+человек, гнёзда на анатомических местах
        var fig = new GameObject("Figure", typeof(RectTransform));
        fig.transform.SetParent(panel.transform, false);
        figure = (RectTransform)fig.transform;
        Center(figure, 620, 680);
        figure.anchoredPosition = new Vector2(0, -14);

        var vitr = NewImage("Vitruvian", figure, Color.white);
        Stretch(vitr.rectTransform);
        vitr.sprite = DaVinciTex.Sprite(DaVinciTex.Vitruvian(620, 680, Ink));
        vitr.raycastTarget = false; // рисунок не перехватывает перетаскивание в гнёзда

        // НЕБО по сторонам — созвездия доноров
        skyLeft = NewPane("SkyLeft", panel.transform, new Vector2(-520, -20), new Vector2(420, 620));
        skyRight = NewPane("SkyRight", panel.transform, new Vector2(520, -20), new Vector2(420, 620));

        identityText = NewText("Identity", panel.transform, "", 20, Sepia, TextAnchor.LowerCenter);
        Bottom(identityText.rectTransform, 82, 28);

        metersText = NewText("Meters", panel.transform, "", 22, Sepia, TextAnchor.LowerCenter);
        Bottom(metersText.rectTransform, 54, 30);

        hintText = NewText("Hint", panel.transform, "", 19, SepiaFaint, TextAnchor.LowerCenter);
        Bottom(hintText.rectTransform, 26, 28);
    }

    // анатомические места гнёзд — совмещены с нарисованной витрувианской фигурой (голова ~+200, грудь ~+80,
    // ноги ~−160). Нет в словаре → химерный/будущий слот, уходит столбцом сбоку (новый вид не правит раскладку)
    static readonly Dictionary<string, Vector2> SlotPlaces = new()
    {
        ["Пасть"] = new Vector2(0, 202),    // рот — голова
        ["Чутьё"] = new Vector2(-98, 210),  // чувства — сбоку головы
        ["Рога"] = new Vector2(104, 250),   // придаток — над головой
        ["Руки"] = new Vector2(-152, 128),  // рука
        ["Сердце"] = new Vector2(0, 82),    // грудь
        ["Шкура"] = new Vector2(0, 2),      // торс
        ["Тело"] = new Vector2(142, 2),     // тело-хвост змеи — сбоку торса
        ["Ноги"] = new Vector2(0, -160),    // ноги
        ["Хвост"] = new Vector2(150, -74),  // хвост — поясница
    };

    static Vector2 SocketPos(string slot, int chimeraOrder) =>
        SlotPlaces.TryGetValue(slot, out var p) ? p : new Vector2(240, 250 - chimeraOrder * 74);

    // порядок строк-слотов в небе — сверху вниз, как гнёзда на фигуре (голова → ноги). Незнакомый тип — в конец
    static readonly string[] RowOrder = { "Пасть", "Чутьё", "Рога", "Руки", "Сердце", "Шкура", "Тело", "Ноги", "Хвост" };
    static int RowRank(string slotType)
    {
        int i = System.Array.IndexOf(RowOrder, slotType);
        return i >= 0 ? i : RowOrder.Length;
    }

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
                    star = new Star { species = vv.species, organ = vv.organName, native = vv.native, slotType = vv.slotType };
                    byKey[key] = star;
                    stars.Add(star);
                }
                star.fits.Add((i, v));
            }
        }

        // СТРОКИ = ТИПЫ СЛОТОВ (одинаковый слот на одном уровне у всех видов — сравнивать органы взглядом
        // по горизонтали), СТОЛБЦЫ = ВИДЫ. Порядок строк — анатомический (как гнёзда на фигуре, сверху вниз)
        var rowTypes = new List<string>();
        foreach (var s in stars) if (!rowTypes.Contains(s.slotType)) rowTypes.Add(s.slotType);
        rowTypes.Sort((a, b) => RowRank(a).CompareTo(RowRank(b)));

        var donors = new List<string>();
        foreach (var s in stars) if (!donors.Contains(s.species)) donors.Add(s.species);

        var chains = new Dictionary<string, List<(RectTransform host, Vector2 pos)>>(); // узлы созвездия для линий
        foreach (var s in stars)
        {
            int di = donors.IndexOf(s.species);
            RectTransform host = (di % 2 == 0) ? skyLeft : skyRight;
            int col = di / 2;                       // вид — своя колонка на своём небе
            int row = rowTypes.IndexOf(s.slotType); // слот — своя строка (общая для всех видов)
            Vector2 pos = new Vector2(-100 + col * 200, 250 - row * 60);

            if (!chains.TryGetValue(s.species, out var list)) chains[s.species] = list = new();
            list.Add((host, pos));

            var img = NewImage("Star", host, StarBeast);
            Center(img.rectTransform, 172, 46);
            img.rectTransform.anchoredPosition = pos;
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = Border;
            o.effectDistance = new Vector2(1.5f, -1.5f);

            // ГЛИФ-ЗВЕЗДА — маркер узла (слот читается по СТРОКЕ, вид — по цвету карточки)
            var glyph = NewImage("Glyph", img.transform, Color.white);
            glyph.sprite = StarGlyph();
            glyph.raycastTarget = false;
            Center(glyph.rectTransform, 26, 26);
            glyph.rectTransform.anchoredPosition = new Vector2(-74, 0);

            var label = NewText("Label", img.transform, "", 15, Sepia, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 4);
            label.rectTransform.offsetMin = new Vector2(18, 4);

            var drag = img.gameObject.AddComponent<StarDrag>();
            drag.Init(this, s);

            s.rt = img.rectTransform;
            s.img = img;
            s.label = label;
            s.glyph = glyph;
        }

        // ЛИНИИ СОЗВЕЗДИЙ: соединяем звёзды вида по строкам — вертикальная цепочка светил своего неба
        foreach (var kv in chains)
            for (int i = 1; i < kv.Value.Count; i++)
                ConstellationLine(kv.Value[i - 1].host, kv.Value[i - 1].pos, kv.Value[i].pos);

        // подписи созвездий
        foreach (var sp in donors)
        {
            int di = donors.IndexOf(sp);
            var host = (di % 2 == 0) ? skyLeft : skyRight;
            var cap = NewText("Cap_" + sp, host, "◈ " + sp + " ◈", 20, SepiaFaint, TextAnchor.UpperCenter);
            Center(cap.rectTransform, 260, 26);
            cap.rectTransform.anchoredPosition = new Vector2(-100 + (di / 2) * 200, 300);
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

    Sprite starGlyph;
    Sprite StarGlyph() => starGlyph ??= DaVinciTex.Sprite(DaVinciTex.Star(48, Color.white));

    /// <summary>Тонкая линия созвездия между двумя узлами в одной панели неба (повёрнутый прямоугольник).</summary>
    void ConstellationLine(RectTransform host, Vector2 a, Vector2 b)
    {
        var img = NewImage("Cline", host, new Color(Sepia.r, Sepia.g, Sepia.b, 0.55f));
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Vector2.Distance(a, b), 2.6f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        rt.SetAsFirstSibling(); // под звёздами
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
        var v = body.GetSlot(slot);
        if (v.chimera) { Toast(body.Remove(slot) ? "слот опустошён" : "слот уже пуст"); Refresh(); return; }

        var vars = body.GetVariants(slot);
        int nat = vars.FindIndex(x => x.native);
        if (nat < 0) { Refresh(); return; }
        if (vars[nat].worn) { Toast("родной орган уже на месте"); Refresh(); return; }

        if (body.Install(slot, nat)) Toast($"снято → {vars[nat].organName}");
        else Toast(vars[nat].duplicate ? $"{vars[nat].organName} занят в другом слоте — сними его там"
                 : !vars[nat].affordable ? "не хватает пула вернуть родной" : "не удалось снять");
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
            // родного не имеет — он «Химерный». Имя надетого показываем стрелкой.
            string head = key + (v.chimera ? "Химерный" : NativeName(i) ?? v.slot);
            string content;
            if (v.organName == "—") content = "— пусто —";                     // пустой химерный
            else if (v.chimera || v.installed) content = $"↳ {v.organName} ({v.cost})"; // химерный (в т.ч. родной графт) ИЛИ звериный графт — показываем ЧТО надето
            else content = $"({v.cost})";                                       // регулярный + родной орган: имя уже в head
            s.label.text = $"{head}\n{content}";

            // МОРФ ГНЕЗДА: надет звериный орган → гнездо цвета ДОНОРА, а ЯРКОСТЬ = ИДЕНТИЧНОСТЬ к нему
            // (идея пользователя: чем больше в тебе вида, тем сочнее горят его части — идентичность НА ТЕЛЕ).
            // Родной орган — телесный. Даёт читать сборку и «кем становишься» одним взглядом
            Color rest;
            if (v.installed)
            {
                Color sp = body.SpeciesColor(v.species);
                float id = body.IdentityOf(v.species);                     // 0..1: доля вида в теле
                Color faint = Color.Lerp(SocketIdle, sp, 0.5f);            // еле тронуто видом (мало идентичности)
                Color vivid = Color.Lerp(sp, Color.white, 0.22f);         // сочный вид (много идентичности)
                rest = Color.Lerp(faint, vivid, id);
            }
            else rest = SocketIdle;
            s.img.color = dragging == null
                ? rest
                : (FitsDragged(i, out bool ok) ? (ok ? SocketOk : SocketNo) : rest);
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
            // ВСЯ КАРТОЧКА — цвета ВИДА (крупное пятно видно, в отличие от крохотного глифа): надет ярко,
            // доступен приглушённо, недоступен тускло-серо. Так лосиный бурый и змеиный зелёный не теряются
            Color sp = body.SpeciesColor(star.species);
            star.img.color = worn ? Color.Lerp(sp, Color.white, 0.28f)
                           : !available ? Color.Lerp(sp, new Color(0.5f, 0.47f, 0.42f), 0.62f)
                           : Color.Lerp(sp, Parchment, 0.35f);
            // глиф — светлый маркер узла (слот читается по строке); гаснет у недоступной
            if (star.glyph != null)
                star.glyph.color = new Color(1f, 0.96f, 0.85f, available || worn ? 1f : 0.4f);
        }

        metersText.text = $"Пул мутагена: {body.PoolUsed}/{body.Pool}        " +
                          $"Звериных слотов: {body.BeastSlots}/{body.MaxSlots}        " +
                          $"Бонус органов ×{body.BonusMult:0.00}";
        // ИДЕНТИЧНОСТЬ — кем тебя считают ПО СОСТАВУ (та же строка, что в дев-панели): здесь ей место
        identityText.text = "Признание: " + body.IdentityInfo;
        hintText.text = Time.unscaledTime < toastUntil ? toast
                      : dragging != null ? "отпусти над гнездом" : "";
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
        bool droppedOnSocket = false;
        foreach (var (slot, variant) in dragging.fits)
        {
            var sock = sockets.Find(x => x.index == slot);
            if (sock == null) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(sock.rt, screenPos, canvas.worldCamera)) continue;
            droppedOnSocket = true;

            // ГОВОРИМ, ПОЧЕМУ отказ (раньше молчал): пул / дубль / уже надет
            var vv = body.GetVariants(slot)[variant];
            if (vv.worn) Toast($"{dragging.organ} уже здесь");
            else if (vv.duplicate) Toast($"{dragging.organ} уже надет в другом слоте — сними его там");
            else if (!vv.affordable) Toast("не хватает пула мутагена");
            else if (body.Install(slot, variant)) Toast($"надет: {dragging.organ}");
            else Toast("не удалось надеть");
            break;
        }
        if (!droppedOnSocket && dragging != null) Toast("мимо гнезда");

        if (dragging != null) dragging.rt.anchoredPosition = dragHome; // звезда возвращается на небо
        dragging = null;
        Refresh();
    }

    // короткое сообщение внизу панели: почему приём/отказ. Живёт на unscaledTime (пауза не мешает)
    string toast;
    float toastUntil;
    void Toast(string m) { toast = m; toastUntil = Time.unscaledTime + 3.5f; }

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
