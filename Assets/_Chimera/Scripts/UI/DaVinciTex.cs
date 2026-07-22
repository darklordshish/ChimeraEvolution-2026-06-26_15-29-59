using UnityEngine;

/// <summary>
/// Процедурные текстуры для тетради да Винчи (конструктор C3). У проекта нет спрайтов, поэтому «красота»
/// рисуется КОДОМ: состаренный пергамент, витрувианская фигура пером, звёздные карты созвездий.
/// Всё генерится один раз при сборке экрана — не рантайм-нагрузка. Пиксель (0,0) в Texture2D — снизу-слева.
/// </summary>
public static class DaVinciTex
{
    // ── публичные генераторы ────────────────────────────────────────────────

    /// <summary>Лист пергамента: тёплая база + перлин-разводы + затемнённые края (виньетка) и редкие пятна.</summary>
    public static Texture2D Parchment(int w, int h)
    {
        var buf = new Color32[w * h];
        Color baseC = new(0.85f, 0.78f, 0.62f);
        float off = Random.value * 100f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.012f + off, y * 0.012f + off) - 0.5f;      // крупные разводы
                float g = Mathf.PerlinNoise(x * 0.08f + off, y * 0.08f + off) - 0.5f;        // мелкое зерно
                float lum = 1f + n * 0.14f + g * 0.05f;
                // виньетка: к краям темнее (старая бумага по кромке засалена)
                float dx = (x / (float)w - 0.5f) * 2f, dy = (y / (float)h - 0.5f) * 2f;
                float vig = 1f - Mathf.Clamp01((dx * dx + dy * dy) * 0.35f);
                Color c = baseC * (lum * Mathf.Lerp(0.82f, 1f, vig));
                buf[y * w + x] = c;
            }
        // ЕДИНИЧНЫЕ бледные пятнышки — намёк на старость, не грязь (раньше было густо и темно = битая текстура)
        for (int i = 0; i < (w * h) / 45000; i++)
            Disc(buf, w, h, Random.Range(0, w), Random.Range(0, h), Random.Range(2, 5),
                 new Color(0.45f, 0.36f, 0.24f), 0.06f);
        return Bake(buf, w, h);
    }

    /// <summary>Витрувианская фигура пером: круг + квадрат + человек в двойной позе. Прозрачный фон —
    /// ложится поверх пергамента. Иконический силуэт читается даже схематичным.</summary>
    public static Texture2D Vitruvian(int w, int h, Color ink)
    {
        var buf = new Color32[w * h];
        for (int i = 0; i < buf.Length; i++) buf[i] = new Color(0, 0, 0, 0);

        float cx = w * 0.5f;
        float cyGeom = h * 0.5f;                        // центр круга/квадрата
        float R = Mathf.Min(w * 0.5f, h * 0.5f) - 6f;   // радиус круга (впритык к рамке рисунка)

        Circle(buf, w, h, cx, cyGeom, R, 2.2f, ink);
        Square(buf, w, h, cx, cyGeom, R, 2.0f, ink);    // квадрат касается круга серединами сторон (классика)

        float footY = cyGeom - R * 0.98f;  // сдвинутые стопы стоят на НИЖНЕЙ кромке круга/квадрата (как у Леонардо)
        float headTop = cyGeom + R * 0.98f; // макушка — у верхней кромки
        float height = headTop - footY;
        float headR = height * 0.072f;
        float headCy = headTop - headR;
        float neck = headCy - headR;
        float shoulderY = neck - height * 0.03f;
        float hipY = footY + height * 0.46f;

        // ── ТУЛОВИЩЕ силуэтом (не палка): голова, шея, плечи, тор с талией, таз ──
        Circle(buf, w, h, cx, headCy, headR, 2f, ink);                 // голова
        Line(buf, w, h, cx, headCy - headR, cx, neck, 1.8f, ink);       // шея
        float shW = height * 0.115f, waistW = height * 0.072f, hipW = height * 0.092f;
        float waistY = Mathf.Lerp(shoulderY, hipY, 0.62f);
        Line(buf, w, h, cx - shW, shoulderY, cx + shW, shoulderY, 2f, ink);   // плечи
        // бока корпуса: плечо → талия → бедро (мягкий изгиб)
        Line(buf, w, h, cx - shW, shoulderY, cx - waistW, waistY, 1.7f, ink);
        Line(buf, w, h, cx - waistW, waistY, cx - hipW, hipY, 1.7f, ink);
        Line(buf, w, h, cx + shW, shoulderY, cx + waistW, waistY, 1.7f, ink);
        Line(buf, w, h, cx + waistW, waistY, cx + hipW, hipY, 1.7f, ink);
        Line(buf, w, h, cx - hipW, hipY, cx + hipW, hipY, 1.7f, ink);         // таз
        Line(buf, w, h, cx, neck, cx, hipY, 1.4f, ink);                       // грудина (тоньше боков)

        // КОНЦЫ КОНЕЧНОСТЕЙ ЛЕЖАТ НА КРУГЕ — ничего не торчит наружу (у Леонардо пальцы/стопы на окружности)
        float dyS = shoulderY - cyGeom;
        float horizX = Mathf.Sqrt(Mathf.Max(1f, R * R - dyS * dyS));          // где круг на высоте плеч
        float ra = 42f * Mathf.Deg2Rad, la = 72f * Mathf.Deg2Rad;
        // точки-цели рук/ног
        float hx = cx + horizX, hxR = cx + R * Mathf.Cos(ra), hyR = cyGeom + R * Mathf.Sin(ra);
        float lx = cx + R * Mathf.Cos(la), ly = cyGeom - R * Mathf.Sin(la);
        // РУКИ от плеча: горизонтально + приподнято (двойная поза)
        Arm(buf, w, h, cx - shW, shoulderY, 2f * cx - hx, shoulderY, ink);    // левая горизонт
        Arm(buf, w, h, cx + shW, shoulderY, hx, shoulderY, ink);             // правая горизонт
        Arm(buf, w, h, cx - shW, shoulderY, 2f * cx - hxR, hyR, ink);         // левая вверх
        Arm(buf, w, h, cx + shW, shoulderY, hxR, hyR, ink);                  // правая вверх
        // НОГИ от таза: вместе (низ) + расставлены (стопы на круге)
        Arm(buf, w, h, cx - hipW * 0.5f, hipY, cx - w * 0.02f, footY, ink);
        Arm(buf, w, h, cx + hipW * 0.5f, hipY, cx + w * 0.02f, footY, ink);
        Arm(buf, w, h, cx - hipW * 0.5f, hipY, 2f * cx - lx, ly, ink);
        Arm(buf, w, h, cx + hipW * 0.5f, hipY, lx, ly, ink);

        return Bake(buf, w, h);
    }

    // конечность с «кистью/стопой» — линия + точка на конце: даёт фигуре завершённость
    static void Arm(Color32[] buf, int w, int h, float x0, float y0, float x1, float y1, Color ink)
    {
        Line(buf, w, h, x0, y0, x1, y1, 1.7f, ink);
        Dot(buf, w, h, x1, y1, 3.2f, ink);
    }

    /// <summary>Мягкая звезда-точка с ореолом: сердцевина + затухающее свечение. Для узлов созвездий.</summary>
    public static Texture2D Star(int size, Color core)
    {
        var buf = new Color32[size * size];
        float c = size * 0.5f, rMax = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rMax;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;                                   // резкая сердцевина, мягкий спад
                // четырёхлучевой блик
                float ray = Mathf.Max(0f, 1f - Mathf.Abs(x - c) / (size * 0.5f)) *
                            Mathf.Max(0f, 1f - Mathf.Abs(y - c) / (size * 0.06f));
                float ray2 = Mathf.Max(0f, 1f - Mathf.Abs(y - c) / (size * 0.5f)) *
                             Mathf.Max(0f, 1f - Mathf.Abs(x - c) / (size * 0.06f));
                a = Mathf.Clamp01(a + (ray + ray2) * 0.5f);
                var col = core; col.a = a;
                buf[y * size + x] = col;
            }
        return Bake(buf, size, size);
    }

    // ── рисование по буферу ─────────────────────────────────────────────────
    static void Plot(Color32[] buf, int w, int h, int x, int y, Color c, float a)
    {
        if (x < 0 || x >= w || y < 0 || y >= h || a <= 0f) return;
        int i = y * w + x;
        Color dst = buf[i];
        float na = Mathf.Clamp01(a + dst.a / 255f * (1f - a));
        Color outC = Color.Lerp(dst, c, a);
        outC.a = na;
        buf[i] = outC;
    }

    static void Line(Color32[] buf, int w, int h, float x0, float y0, float x1, float y1, float thick, Color c)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float len = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
        int steps = Mathf.CeilToInt(len);
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            Dot(buf, w, h, x0 + dx * t, y0 + dy * t, thick, c);
        }
    }

    static void Dot(Color32[] buf, int w, int h, float px, float py, float r, Color c)
    {
        int x0 = Mathf.FloorToInt(px - r), x1 = Mathf.CeilToInt(px + r);
        int y0 = Mathf.FloorToInt(py - r), y1 = Mathf.CeilToInt(py + r);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                Plot(buf, w, h, x, y, c, Mathf.Clamp01(r - d)); // мягкий край = антиалиас
            }
    }

    static void Circle(Color32[] buf, int w, int h, float cx, float cy, float r, float thick, Color c)
    {
        int steps = Mathf.CeilToInt(2f * Mathf.PI * r);
        for (int s = 0; s < steps; s++)
        {
            float a = s / (float)steps * 2f * Mathf.PI;
            Dot(buf, w, h, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, thick, c);
        }
    }

    static void Square(Color32[] buf, int w, int h, float cx, float cy, float half, float thick, Color c)
    {
        Line(buf, w, h, cx - half, cy - half, cx + half, cy - half, thick, c);
        Line(buf, w, h, cx + half, cy - half, cx + half, cy + half, thick, c);
        Line(buf, w, h, cx + half, cy + half, cx - half, cy + half, thick, c);
        Line(buf, w, h, cx - half, cy + half, cx - half, cy - half, thick, c);
    }

    static void Disc(Color32[] buf, int w, int h, int cx, int cy, int r, Color c, float a)
    {
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) Plot(buf, w, h, cx + x, cy + y, c, a * (1f - Mathf.Sqrt(x * x + y * y) / r));
    }

    static Texture2D Bake(Color32[] buf, int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        t.SetPixels32(buf);
        t.Apply();
        return t;
    }

    /// <summary>Готовый спрайт из текстуры (весь размер, центр-пивот).</summary>
    public static Sprite Sprite(Texture2D t) =>
        UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
}
