using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>СВЕРКА СИЛУЭТОВ — ДЕТЕКТОР ЧИТАЕМОСТИ (консилиум формы 01.10, итог §4–5): тело строится настоящим билдером,
/// снимается маской ортокамерой и сравнивается с маской силуэта ЛИСТА (IoU). Матрица химер мерит «нет поломок», карта тел —
/// стыки; ни то ни другое не говорит, похоже ли тело на лист, — а цель формы именно лист, не фото.
///
/// Камера — как у стенда модельной линии (`Kadr.cs`): `profile` — глаз с +X, морда вправо; `front` — глаз с +Z. Маски
/// листов — из ВИТРИНЫ референсов (спека `2026-10-08-hranilishche-referensov.md`: мерить только по витрине):
/// `Референсы/витрина/&lt;вид&gt;/masks/орто-&lt;ракурс&gt;.png`, а нет ортопары — `вид-&lt;ракурс&gt;.png` (фигура тёмная на белом,
/// низ — земля). Профиль листа может смотреть в любую сторону — сравнивается и отражённая маска, берётся лучшая. Сравнение — логикой сверки линии (`Tools/Blender/kritik/igra_sverka.py`): масштаб листа подбирается в ±15 % от
/// высоты тела (у листа метров нет), фигуры выравниваются по земле и по центру; итог — лучший IoU и его масштаб. Далёкий
/// от 1 масштаб значит, что пропорции расходятся сильнее, чем высота.
///
/// Маски тел кладутся рядом (`Docs/Диаграммы/Силуэты/`) — модельной линии видно, с чем её мерили.</summary>
public static class SilhouetteDetector
{
    public const string TargetDir = "Референсы/витрина/";
    const string OutDir = "Docs/Диаграммы/Силуэты/";
    const string Report = "Docs/Диаграммы/СИЛУЭТЫ.md";
    const int Px = 384;   // сторона кадра маски тела

    public static readonly (string asset, string file)[] Species =
    {
        ("Волк", "volk"), ("Лось", "los"), ("Ёж", "ezh"), ("Змея", "zmeya"), ("Человек", "chelovek"),   // file — имя маски тела
    };
    public static readonly (string name, Vector3 eye)[] Views = { ("profile", Vector3.right), ("front", Vector3.forward) };

    [MenuItem("Chimera/Выгрузить сверку силуэтов")]
    public static void GenerateMenu() => Debug.Log(Generate());

    /// <summary>Сверить все виды во всех ракурсах, написать отчёт и маски тел. Возвращает строку итога.</summary>
    public static string Generate()
    {
        Directory.CreateDirectory(OutDir);
        var sb = new StringBuilder();
        sb.AppendLine("# Сверка силуэтов с листами");
        sb.AppendLine();
        sb.AppendLine("> Генерируется `chimera-silhouette` (`SilhouetteDetector`). Тело — настоящим билдером, маска — ортокамерой;");
        sb.AppendLine("> лист — маска витрины `Референсы/витрина/<вид>/masks/` (`орто-*`, иначе `вид-*`; профиль — и отражённый). IoU — лучший в ±15 % масштаба листа;");
        sb.AppendLine("> 1.00 — силуэты совпали, масштаб далёкий от 1 — пропорции расходятся сильнее высоты.");
        sb.AppendLine();
        sb.AppendLine("| вид | ракурс | IoU | масштаб листа | маска тела |");
        sb.AppendLine("|---|---|---|---|---|");
        int measured = 0;
        var zones = new StringBuilder();
        foreach (var (asset, file) in Species)
        {
            var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{asset}.asset");
            if (sp == null) continue;
            foreach (var (view, eye) in Views)
            {
                var body = BodyMask(sp, eye, out float mpp);
                string bodyPng = $"{file}-{view}.png";
                File.WriteAllBytes(OutDir + bodyPng, ToPng(body));
                var target = TargetMask(asset, view);
                string iou = "— нет маски листа", scale = "";
                if (target != null)
                {
                    var (best, s) = Compare(body, target);
                    var mirrored = Mirror(target);
                    var (bm, sm) = Compare(body, mirrored);   // профиль листа смотрит в другую сторону
                    if (bm > best) { best = bm; s = sm; target = mirrored; }
                    zones.AppendLine($"### {asset} · {view}");
                    zones.AppendLine();
                    if (best < 0.3f) zones.AppendLine("> IoU ниже 0.3 — тело и лист в разных позах (змея на листе с поднятой головой или клубком): пояса по высоте не говорящие.\n");
                    zones.AppendLine(Zones(body, target, mpp, view == "profile"));
                    iou = best.ToString("F2", CultureInfo.InvariantCulture);
                    scale = s.ToString("F2", CultureInfo.InvariantCulture);
                    measured++;
                }
                sb.AppendLine($"| {asset} | {view} | {iou} | {scale} | `Силуэты/{bodyPng}` |");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Где и насколько — по поясам");
        sb.AppendLine();
        sb.AppendLine("> Лист приведён к ВЫСОТЕ тела (не к масштабу лучшего IoU — тот прячет ошибку пропорций), метры — тела.");
        sb.AppendLine("> «тело к листу» ×1.00 — совпало; ×0.70 — тело на 30 % уже/тоньше листа. Пояса длины — каждый в долях своей длины.");
        sb.AppendLine("> Низ туши — от верха вниз до первого просвета: рога над головой (лось) дают просвет раньше — там пояс длины не о туше.");
        sb.AppendLine();
        sb.Append(zones);
        File.WriteAllText(Report, sb.ToString());
        AssetDatabase.Refresh();
        return $"{Report} обновлена: сверено {measured} из {Species.Length * Views.Length} (маски листов есть не у всех)";
    }

    /// <summary>Маска листа из витрины: папка вида — его имя строчными (`Волк` → `волк`), ортопара важнее вида.</summary>
    static bool[,] TargetMask(string asset, string view)
    {
        string dir = TargetDir + asset.ToLowerInvariant() + "/masks/";
        return LoadMask(dir + $"орто-{view}.png") ?? LoadMask(dir + $"вид-{view}.png");
    }

    static bool[,] Mirror(bool[,] m)
    {
        int w = m.GetLength(0), h = m.GetLength(1);
        var r = new bool[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) r[x, y] = m[w - 1 - x, y];
        return r;
    }

    /// <summary>Маска тела вида в ракурсе `eye`: тело строится в стороне от сцены, ортокамера по оси, фон белый — фигура
    /// всё, что от фона отличается.</summary>
    public static bool[,] BodyMask(SpeciesSO sp, Vector3 eye) => BodyMask(sp, eye, out _);

    /// <summary>То же с масштабом кадра: `metresPerPx` — метров тела на пиксель маски (ортокамера).</summary>
    public static bool[,] BodyMask(SpeciesSO sp, Vector3 eye, out float metresPerPx)
    {
        metresPerPx = 0f;
        var origin = new Vector3(0f, -5000f, 0f);
        var go = new GameObject("~СИЛУЭТ");
        go.transform.position = origin;
        var camGo = new GameObject("~СИЛУЭТ-камера");
        var rt = new RenderTexture(Px, Px, 24);
        try
        {
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(go.transform, sp, sp.organs?.ToList(), null);

            // ГРАНИЦЫ — ПО ВЕРШИНАМ: `Renderer.bounds` у скиннед-меша в кадре создания врёт (поймано 01.10)
            var b = WorldBounds(go);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            float extent = Mathf.Max(b.size.y, Vector3.Scale(b.size, Vector3.one - Abs(eye)).magnitude) * 0.55f;
            cam.orthographicSize = extent;
            metresPerPx = 2f * extent / Px;
            cam.transform.position = b.center + eye * (b.extents.magnitude + 5f);
            cam.transform.rotation = Quaternion.LookRotation(-eye, Vector3.up);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = b.extents.magnitude * 2f + 10f;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Px, Px, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Px, Px), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            var mask = new bool[Px, Px];
            for (int y = 0; y < Px; y++)
                for (int x = 0; x < Px; x++)
                {
                    var c = px[y * Px + x];
                    mask[x, y] = (Mathf.Abs(c.r - 1f) + Mathf.Abs(c.g - 1f) + Mathf.Abs(c.b - 1f)) > 0.05f;
                }
            return mask;
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static Bounds WorldBounds(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            Mesh m;
            if (r is SkinnedMeshRenderer s) { m = new Mesh(); s.BakeMesh(m, true); }
            else m = r.GetComponent<MeshFilter>()?.sharedMesh;
            if (m == null || !m.isReadable) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); continue; }
            foreach (var v in m.vertices)
            {
                var w = r.transform.TransformPoint(v);
                if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
            }
            if (r is SkinnedMeshRenderer) Object.DestroyImmediate(m);
        }
        return b;
    }

    /// <summary>Маска листа: фигура — тёмное или непрозрачное на светлом/прозрачном. Нет файла — null.</summary>
    public static bool[,] LoadMask(string path)
    {
        if (!File.Exists(path)) return null;
        var tex = new Texture2D(2, 2);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
            var px = tex.GetPixels();
            var mask = new bool[tex.width, tex.height];
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    var c = px[y * tex.width + x];
                    mask[x, y] = c.a > 0.5f && (c.r + c.g + c.b) / 3f < 0.5f;
                }
            return mask;
        }
        finally { Object.DestroyImmediate(tex); }
    }

    /// <summary>Лучший IoU маски листа к маске тела: обе обрезаются по фигуре, лист масштабируется к высоте тела × s,
    /// s ∈ [0.85, 1.15], выравнивание — низ к низу, центр к центру.</summary>
    public static (float iou, float scale) Compare(bool[,] body, bool[,] target)
    {
        var bb = Box(body); var tb = Box(target);
        if (bb.w == 0 || tb.w == 0) return (0f, 1f);
        float best = 0f, bestS = 1f;
        for (float s = 0.85f; s <= 1.1501f; s += 0.01f)
        {
            float k = bb.h * s / tb.h;   // пикселей тела на пиксель листа
            int tw = Mathf.RoundToInt(tb.w * k), th = Mathf.RoundToInt(tb.h * k);
            // начало масштабированного листа в кадре тела: низ к низу, центр к центру
            int ox = bb.x + bb.w / 2 - tw / 2, oy = bb.y;
            int x0 = Mathf.Min(bb.x, ox), x1 = Mathf.Max(bb.x + bb.w, ox + tw);
            int y0 = Mathf.Min(bb.y, oy), y1 = Mathf.Max(bb.y + bb.h, oy + th);
            int inter = 0, union = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    bool a = x >= 0 && y >= 0 && x < body.GetLength(0) && y < body.GetLength(1) && body[x, y];
                    int tx = Mathf.FloorToInt((x - ox) / k) + tb.x, ty = Mathf.FloorToInt((y - oy) / k) + tb.y;
                    bool t = x >= ox && y >= oy && x < ox + tw && y < oy + th &&
                             tx >= 0 && ty >= 0 && tx < target.GetLength(0) && ty < target.GetLength(1) && target[tx, ty];
                    if (a && t) inter++;
                    if (a || t) union++;
                }
            float iou = union > 0 ? (float)inter / union : 0f;
            if (iou > best) { best = iou; bestS = s; }
        }
        return (best, bestS);
    }

    /// <summary>ГДЕ И НАСКОЛЬКО (спека хранилища, этап 3): IoU — одно число, а модельной линии нужно «голова уже на 30%,
    /// брюхо ниже на 8 см». Лист приводится к ВЫСОТЕ тела (а не к масштабу лучшего IoU — тот прячет ошибку пропорций в
    /// масштаб), метры — по кадру тела. Пояса по высоте (сверху вниз) — ширина силуэта; в профиль ещё пояса по длине (от
    /// хвоста к морде, каждый в долях СВОЕЙ длины) — верх, низ и толщина. Итог — крупнейшие расхождения словами.</summary>
    public static string Zones(bool[,] body, bool[,] target, float mpp, bool profile, int bands = 10)
    {
        var bb = Box(body); var tb = Box(target);
        if (bb.w == 0 || tb.w == 0 || mpp <= 0f) return "";
        float H = bb.h * mpp, tm = H / tb.h;   // метров на пиксель: тело — по кадру, лист — приведённый к высоте тела
        var sb = new StringBuilder();
        var diffs = new List<(string where, float b, float t, bool level)>();   // level — высота края: «выше/ниже», а не отношение
        string M(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        float lb = bb.w * mpp, lt = tb.w * tm;
        sb.AppendLine($"высота {M(H)} м (лист приведён к ней) · {(profile ? "длина" : "ширина")} силуэта: тело {M(lb)} м, лист {M(lt)} м ({Pct(lb, lt)})");
        if (Mathf.Abs(Mathf.Log(lb / lt)) > 0.05f) diffs.Add(((profile ? "длина" : "ширина") + " силуэта целиком", lb, lt, false));

        sb.AppendLine();
        sb.AppendLine("| пояс высоты, м | ширина тела | ширина листа | тело к листу |");
        sb.AppendLine("|---|---|---|---|");
        for (int i = 0; i < bands; i++)
        {
            float f1 = 1f - (float)i / bands, f0 = 1f - (float)(i + 1) / bands;   // сверху вниз
            float wb = RowExtent(body, bb, f0, f1) * mpp, wt = RowExtent(target, tb, f0, f1) * tm;
            string band = $"{M(f0 * H)}–{M(f1 * H)}";
            sb.AppendLine($"| {band} | {M(wb)} | {M(wt)} | {Pct(wb, wt)} |");
            if (Mathf.Max(wb, wt) > 0.04f * H) diffs.Add(($"ширина на высоте {band} м", wb, wt, false));
        }
        if (profile)
        {
            sb.AppendLine();
            sb.AppendLine("| пояс длины (от хвоста к морде) | верх тела / листа, м | низ туши тела / листа, м | толщина туши: тело к листу |");
            sb.AppendLine("|---|---|---|---|");
            for (int j = 0; j < bands; j++)
            {
                float g0 = (float)j / bands, g1 = (float)(j + 1) / bands;
                var (tb0, bb0) = ColumnSpan(body, bb, g0, g1); var (tt0, bt0) = ColumnSpan(target, tb, g0, g1);
                float topB = tb0 * mpp, botB = bb0 * mpp, topT = tt0 * tm, botT = bt0 * tm;
                string band = $"{j * 100 / bands}–{(j + 1) * 100 / bands}%";
                sb.AppendLine($"| {band} | {M(topB)} / {M(topT)} | {M(botB)} / {M(botT)} | {Pct(topB - botB, topT - botT)} |");
                if (Mathf.Max(topB - botB, topT - botT) > 0.04f * H) diffs.Add(($"толщина туши в поясе длины {band}", topB - botB, topT - botT, false));
                if (Mathf.Abs(topB - topT) > 0.05f * H) diffs.Add(($"верх силуэта в поясе длины {band}", topB, topT, true));
                if (Mathf.Abs(botB - botT) > 0.05f * H) diffs.Add(($"низ туши в поясе длины {band}", botB, botT, true));
            }
        }
        // РАНГ — РАЗНИЦА В МЕТРАХ: отношение у края возле земли (низ 0.01 м против 0.32) раздувается в тысячи процентов
        var top = diffs.OrderByDescending(d => Mathf.Abs(d.b - d.t)).Take(6).ToList();
        if (top.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Крупнейшие расхождения:** " + string.Join("; ", top.Select(d =>
                d.level ? $"{d.where}: тело {M(d.b)} м, лист {M(d.t)} м — {(d.b < d.t ? "ниже" : "выше")} на {M(Mathf.Abs(d.b - d.t))} м"
                        : $"{d.where}: тело {M(d.b)} м, лист {M(d.t)} м — {(d.b < d.t ? "меньше" : "больше")} на {(d.t > 1e-4f ? Mathf.Abs(1f - d.b / d.t) * 100f : 0f):0}%")) + ".");
        }
        return sb.ToString();
    }

    static string Pct(float b, float t) => t <= 1e-6f ? "—" : (b / t).ToString("×0.00", CultureInfo.InvariantCulture);

    /// <summary>Наибольший поперечник маски в полосе высоты [f0, f1] (доли высоты фигуры от низа), пикселей.</summary>
    static float RowExtent(bool[,] m, (int x, int y, int w, int h) b, float f0, float f1)
    {
        int y0 = b.y + Mathf.FloorToInt(f0 * b.h), y1 = b.y + Mathf.Max(Mathf.FloorToInt(f0 * b.h) + 1, Mathf.CeilToInt(f1 * b.h));
        int best = 0;
        for (int y = y0; y < Mathf.Min(y1, b.y + b.h); y++)
        {
            int l = -1, r = -1;
            for (int x = b.x; x < b.x + b.w; x++) if (m[x, y]) { if (l < 0) l = x; r = x; }
            if (l >= 0) best = Mathf.Max(best, r - l + 1);
        }
        return best;
    }

    /// <summary>Верх фигуры и низ ТУШИ (пикселей над её низом) в полосе длины [g0, g1] — доли своей длины слева направо.
    /// Низ туши — от верха вниз до первого просвета: в поясе с лапой «низ силуэта» — земля, и разница поставленных
    /// иначе лап читалась бы как «брюхо на метр выше». По каждому столбцу пояса, затем медиана.</summary>
    static (float top, float bottom) ColumnSpan(bool[,] m, (int x, int y, int w, int h) b, float g0, float g1)
    {
        int x0 = b.x + Mathf.FloorToInt(g0 * b.w), x1 = b.x + Mathf.Max(Mathf.FloorToInt(g0 * b.w) + 1, Mathf.CeilToInt(g1 * b.w));
        var tops = new List<int>(); var bots = new List<int>();
        for (int x = x0; x < Mathf.Min(x1, b.x + b.w); x++)
        {
            int y = b.y + b.h - 1;
            while (y >= b.y && !m[x, y]) y--;
            if (y < b.y) continue;
            int t = y;
            while (y >= b.y && m[x, y]) y--;
            tops.Add(t - b.y + 1); bots.Add(y + 1 - b.y);
        }
        if (tops.Count == 0) return (0f, 0f);
        tops.Sort(); bots.Sort();
        return (tops[tops.Count / 2], bots[bots.Count / 2]);
    }

    static (int x, int y, int w, int h) Box(bool[,] m)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (int y = 0; y < m.GetLength(1); y++)
            for (int x = 0; x < m.GetLength(0); x++)
                if (m[x, y]) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        return x1 < 0 ? (0, 0, 0, 0) : (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    static byte[] ToPng(bool[,] m)
    {
        int w = m.GetLength(0), h = m.GetLength(1);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = m[x, y] ? Color.black : Color.white;
        tex.SetPixels(px);
        tex.Apply();
        var png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return png;
    }
}
