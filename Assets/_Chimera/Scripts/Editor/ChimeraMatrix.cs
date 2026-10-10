using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>МАТРИЦА ХИМЕР — детектор спеки `2026-09-26-adresaciya-detaley-himery.md` §4.
///
/// ЗАЧЕМ. 26.09 восемь тяжёлых химер на стенде оказались сломаны ВСЕ, хотя каждый вид по отдельности был принят: детали
/// донора не рисовались, висели в воздухе, уходили под землю, снятый аугмент уносил низ ноги. Ни карта тел, ни тесты
/// этого не видели — они мерили чистые виды. Здесь каждое шасси собирается с каждым донором тем же путём, что в игре
/// (`BodyProbe.ChimeraPlan` → `MorphBuilder.Build`), и собранное меряется: сначала целиком (донор отдаёт всё
/// переносимое), потом по одному аугменту.
///
/// ЧТО МЕРИТСЯ (номера — из спеки). Деталь — `MeshRenderer` (ригблок или примитив), оболочка поля — `SkinnedMeshRenderer`;
/// имя детали — имя её места (действующий контракт имён морф-частей).
///   И1 — деталей мест, которые кормит аугмент, на химере не меньше, чем у донора: иначе «не рисуется»;
///   И2 — каждая деталь связана с телом цепочкой касаний, начинающейся у оболочки: иначе «висит»;
///   И3 — ни одна деталь не ниже земли;
///   И4 — нет примитивов-заглушек (чистые виды с 20.09 рисуются одними ригблоками — проверяется тут же, на них);
///   И5 — у ОПОРНОЙ конечности шасси низ стоит на земле. Опорность не перечисляется, а МЕРИТСЯ на чистом шасси:
///        конечность опорная, если её нижняя деталь касается земли (волк, лось, ёж — обе пары; человек — ноги).
///   И6 (тотальность гнёзд) включится вместе с чтением раскладки гнёзд — до того гнёзд в данных нет.
///
/// Чистые виды меряются теми же правилами: нарушение на чистом виде — ошибка ПРАВИЛА, а не химеры, и печатается
/// отдельно, первым.</summary>
public static class ChimeraMatrix
{
    const string OutPath = "Docs/Диаграммы/ХИМЕРЫ.md";
    static readonly string[] Names = { "Человек", "Волк", "Лось", "Ёж", "Змея" };

    // Пороги — длины, а не доли: земля и касание мерятся в метрах мира. Касание по коробкам рендереров мягкое (коробка
    // шире детали), поэтому 2 см ловят только настоящий отрыв — морду в полуметре, лапу в воздухе
    const float Touch = 0.02f;      // коробки ближе — касаются
    const float Below = 0.02f;      // ниже земли глубже — «под землёй»
    const float Foot = 0.03f;       // низ опорной конечности выше — «в воздухе»

    static readonly HashSet<string> Primitive = new() { "Cube", "Sphere", "Capsule", "Cylinder" };

    public struct Violation
    {
        public string chassis, donor, graft, inv, place, what;
        public override string ToString() => $"{chassis}+{donor} [{graft}] {inv} {place}: {what}";
    }

    [MenuItem("Chimera/Выгрузить матрицу химер")]
    public static void Generate() => Write(Run(out var native), native);

    /// <summary>РАЗНЫЕ ПОЛОМКИ — строки отчёта: «шасси, донор, аугмент, инвариант, место». По ним и ведётся долг: поставка,
    /// добавившая игл в уже сломанную клетку, умножит отдельные нарушения, но новой поломки не принесёт.</summary>
    public static int Breakages(List<Violation> res) => BreakageKeys(res).Count;

    /// <summary>Те же строки долга — текстом, отсортированно: «шасси+донор [аугмент] инвариант место». По ним сторож сверяет
    /// СПИСОК, а не число: одна починилась, другая появилась — число то же, а поломка новая (аудит Codex 04.10).</summary>
    public static List<string> BreakageKeys(List<Violation> res) =>
        res.Select(v => $"{v.chassis}+{v.donor} [{v.graft}] {v.inv} {v.place}").Distinct().OrderBy(s => s, System.StringComparer.Ordinal).ToList();

    public const string DebtFile = "Assets/_Chimera/Tests/EditMode/ChimeraMatrixDebt.txt";

    /// <summary>Переписать список долга текущими поломками — только когда поломки ушли (починка) или решением; новые —
    /// не вписывать молча.</summary>
    [MenuItem("Chimera/Переписать долг матрицы химер")]
    public static string WriteDebt()
    {
        var keys = BreakageKeys(Run(out _));
        System.IO.File.WriteAllLines(DebtFile, keys);
        AssetDatabase.Refresh();
        return $"{DebtFile}: {keys.Count} строк";
    }

    /// <summary>Все нарушения по матрице. `native` — нарушения тех же правил на чистых видах (должно быть пусто).</summary>
    public static List<Violation> Run(out List<Violation> native)
    {
        // ЛОКАЛЬ МАШИНЫ НЕ ДОЛЖНА ПРОТЕКАТЬ В ОТЧЁТ: на русской «0.35» печаталось «0,35» (та же мина, что в карте тел)
        var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try { return RunInvariant(out native); }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = culture; }
    }

    static List<Violation> RunInvariant(out List<Violation> native)
    {
        var all = Names.Select(n => AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + n + ".asset"))
                       .Where(s => s != null).ToList();
        native = new List<Violation>();
        var stance = new Dictionary<SpeciesSO, List<string>>();
        foreach (var s in all)
        {
            var b = Body.Build(s, Native(s), null);
            stance[s] = b.StanceLimbs();
            Check(b, s, s, "родной", null, null, stance[s], native);
            b.Dispose();
        }

        var res = new List<Violation>();
        foreach (var c in all)
            foreach (var d in all)
            {
                if (c == d) continue;
                var donorBody = Body.Build(d, Native(d), null);
                var transferable = (d.organs ?? new Organ[0]).Where(o => o != null && !o.chassisOnly && !o.nativeOnly).ToList();

                // ЦЕЛИКОМ: донор отдаёт всё переносимое — тяжёлый микс, как на стенде 26.09
                string allSlots = string.Join(",", transferable.Select(o => o.slot).Distinct());
                Graft(c, d, allSlots, "всё", transferable, donorBody, stance[c], res);
                // ПОШТУЧНО: у сломанной детали должен быть один виновник, а не «что-то в тяжёлом миксе»
                foreach (var o in transferable)
                    Graft(c, d, o.slot, o.organName, new List<Organ> { o }, donorBody, stance[c], res);
                donorBody.Dispose();
            }
        return res;
    }

    static void Graft(SpeciesSO c, SpeciesSO d, string slots, string label, List<Organ> organs, Body donorBody,
                      List<string> stance, List<Violation> res)
    {
        var plan = BodyProbe.ChimeraPlan(c, d, slots, out var worn, out _);
        if (worn == null) return;   // графт не встал — это экономика слотов, не форма
        var b = Body.Build(c, worn, plan);
        Check(b, c, d, label, organs.Where(o => worn.Contains(o)).ToList(), donorBody, stance, res);
        b.Dispose();
    }

    static void Check(Body b, SpeciesSO c, SpeciesSO d, string graft, List<Organ> fed, Body donorBody,
                      List<string> stance, List<Violation> res)
    {
        void Add(string inv, string place, string what) =>
            res.Add(new Violation { chassis = c.speciesName, donor = d.speciesName, graft = graft, inv = inv, place = place, what = what });

        // И1 — не рисуется
        if (fed != null && donorBody != null)
            foreach (var o in fed)
                foreach (var place in Fed(d, o.slot))
                {
                    if (b.CarriedWhole(c, place)) continue;   // место несёт кусок целиком (голова с ушами и зубами)
                    int want = donorBody.Count(place), got = b.Count(place);
                    if (want > 0 && got < want) Add("И1", place, $"деталей {got} из {want} ({o.organName})");
                }

        // И2 — висит: деталь не связана с оболочкой цепочкой касаний
        var loose = b.Floating(out var attached);
        foreach (var r in loose) Add("И2", r.name, $"оторвана от тела на {b.Gap(r, attached):0.00} м");

        // И3 — под землёй
        foreach (var r in b.details)
            if (r.bounds.min.y < -Below) Add("И3", r.name, $"ниже земли на {-r.bounds.min.y:0.00} м");

        // И4 — примитив-заглушка
        foreach (var r in b.details)
            if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && Primitive.Contains(mf.sharedMesh.name))
                Add("И4", r.name, "примитив " + mf.sharedMesh.name);

        // И5 — опорная конечность шасси стоит на земле
        foreach (var limb in stance)
        {
            // КОНЕЦ — ДЕТАЛЬ МЕСТА ИЛИ ДЕТАЛЬ-МЕШ ЧАСТИ (спека конструктора, 02.10): лапа теперь бывает частью меша детали,
            // а не отдельным блоком. Низ — по вершинам после скиннинга: границы скиннед-рендерера с запасом и врут
            var ends = b.details.Where(r => r.name == limb).ToList();
            var parts = b.shells.Where(r => r.name == limb && r.GetComponent<BodyPartView>() != null).ToList();
            if (ends.Count == 0 && parts.Count == 0) { Add("И5", limb, "у опорной конечности нет конца"); continue; }
            float low = ends.Select(r => r.bounds.min.y).Concat(parts.Select(Lowest)).DefaultIfEmpty(0f).Min();
            if (low > Foot) Add("И5", limb, $"низ в воздухе на {low:0.00} м");
        }
    }

    static float Lowest(Renderer r)
    {
        var m = new Mesh();
        try
        {
            ((SkinnedMeshRenderer)r).BakeMesh(m, true);
            return m.vertices.Select(v => r.transform.TransformPoint(v).y).DefaultIfEmpty(0f).Min();
        }
        finally { Object.DestroyImmediate(m); }
    }

    /// <summary>МЕСТА, КОТОРЫЕ КОРМИТ АУГМЕНТ: своё место плюс места, берущие форму из него (`formFrom` — глаза,
    /// уши, нос, ямки от Чутья). Берутся у ДОНОРА: сколько деталей рисует аугмент дома, столько он обязан нарисовать
    /// и в гостях.</summary>
    static IEnumerable<string> Fed(SpeciesSO donor, string slot)
    {
        yield return slot;
        foreach (var s in donor.sockets ?? new BodySocket[0])
            if (s != null && s.formFrom == slot) yield return s.name;
    }

    static List<Organ> Native(SpeciesSO s) => (s.organs ?? new Organ[0]).Where(o => o != null).ToList();

    /// <summary>Собранное тело и его замеры. Строится в нуле: низ капсулы — земля (высоты в данных от земли).</summary>
    sealed class Body : System.IDisposable
    {
        GameObject go;
        public List<Renderer> details = new(), shells = new();

        public static Body Build(SpeciesSO s, List<Organ> worn, BodySocket[] plan)
        {
            var b = new Body { go = new GameObject("~МАТРИЦА") };
            var cc = b.go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);
            using (BodyMesh.FieldOnly()) MorphBuilder.Build(b.go.transform, s, worn, plan);
            foreach (var r in b.go.GetComponentsInChildren<Renderer>())
                (r is SkinnedMeshRenderer ? b.shells : b.details).Add(r);
            return b;
        }

        // ДЕТАЛЬ-МЕШ СЧИТАЕТСЯ НАРАВНЕ С КУСКАМИ (02.10): лапы волка теперь внутри его детали, а не отдельными блоками —
        // без этого «дома» у Рук стало бы 0 деталей, и проверка «не рисуется» молча выключилась бы для любого гостя
        /// <summary>Место нарисовано куском, несущим целое место: само это место или любое, что на нём висит.</summary>
        public bool CarriedWhole(SpeciesSO chassis, string place)
        {
            var wholes = shells.Select(r => r.GetComponent<BodyPartView>()).Where(v => v != null && !string.IsNullOrEmpty(v.whole)).Select(v => v.whole).ToList();
            if (wholes.Count == 0) return false;
            var by = chassis.sockets.Where(s => s != null && !string.IsNullOrEmpty(s.name)).GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
            for (int guard = 0; place != null && guard < 16; guard++)
            {
                if (wholes.Contains(place)) return true;
                place = by.TryGetValue(place, out var s) && !string.IsNullOrEmpty(s.parent) ? s.parent : null;
            }
            return false;
        }

        public int Count(string place) => details.Count(r => r.name == place) +
                                          shells.Count(r => r.name == place && r.GetComponent<BodyPartView>() != null);

        public List<string> StanceLimbs()
        {
            var res = new List<string>();
            foreach (var limb in new[] { BodySlots.Arms, BodySlots.Legs })
            {
                var ends = details.Where(r => r.name == limb).ToList();
                if (ends.Count > 0 && ends.Min(r => r.bounds.min.y) <= Foot) res.Add(limb);
            }
            return res;
        }

        static bool Touches(Bounds a, Bounds b)
        {
            var s = BodyProbe.Separation(a, b);
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z)) <= Touch;
        }

        /// <summary>ДЕТАЛИ, НЕ СВЯЗАННЫЕ С ТЕЛОМ. Обход от оболочек поля по касаниям: зуб, сидящий на оторванной морде,
        /// тоже оторван — поэтому связность, а не «касается ли соседа».</summary>
        public List<Renderer> Floating(out HashSet<Renderer> reached)
        {
            reached = new HashSet<Renderer>();
            var queue = new Queue<Renderer>();
            foreach (var d in details)
                if (shells.Any(s => Touches(d.bounds, s.bounds))) { reached.Add(d); queue.Enqueue(d); }
            // тело без оболочки (змея на одной голове) — корнем служит самая крупная деталь
            if (shells.Count == 0 && details.Count > 0)
            {
                var root = details.OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
                if (reached.Add(root)) queue.Enqueue(root);
            }
            while (queue.Count > 0)
            {
                var a = queue.Dequeue();
                foreach (var d in details)
                    if (!reached.Contains(d) && Touches(a.bounds, d.bounds)) { reached.Add(d); queue.Enqueue(d); }
            }
            var got = reached;
            return details.Where(d => !got.Contains(d)).ToList();
        }

        /// <summary>Насколько оторвана: до ближайшей части ТЕЛА (оболочка или связанная с ней деталь), а не до соседа —
        /// иначе зуб на оторванной морде показал бы ноль.</summary>
        public float Gap(Renderer r, HashSet<Renderer> attached)
        {
            float best = float.MaxValue;
            foreach (var o in shells.Concat(attached))
            {
                var s = BodyProbe.Separation(r.bounds, o.bounds);
                best = Mathf.Min(best, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
            }
            return best;
        }

        public void Dispose() { if (go != null) Object.DestroyImmediate(go); }
    }

    static void Write(List<Violation> res, List<Violation> native)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# МАТРИЦА ХИМЕР");
        sb.AppendLine();
        sb.AppendLine("Отчёт СГЕНЕРИРОВАН: `Chimera → Выгрузить матрицу химер` (`unity command chimera-matrix`). Руками не править.");
        sb.AppendLine("Каждое шасси собрано с каждым донором настоящим билдером — целиком и по одному аугменту — и обмеряно.");
        sb.AppendLine("Инварианты — спека `2026-09-26-adresaciya-detaley-himery.md` §4: И1 не рисуется, И2 висит, И3 под");
        sb.AppendLine("землёй, И4 примитив-заглушка, И5 опорная конечность не стоит на земле.");
        sb.AppendLine();

        sb.AppendLine("## Чистые виды — те же правила");
        sb.AppendLine();
        if (native.Count == 0) sb.AppendLine("Нарушений нет: правила не кричат на чистые виды.");
        else foreach (var v in native) sb.AppendLine("- " + v);
        sb.AppendLine();

        sb.AppendLine("## Сводка: нарушений в клетке «шасси × донор» (целиком + поштучно)");
        sb.AppendLine();
        sb.AppendLine("| шасси \\ донор | " + string.Join(" | ", Names) + " |");
        sb.AppendLine("|---|" + string.Concat(Names.Select(_ => "---|")));
        foreach (var c in Names)
        {
            sb.Append("| **" + c + "** |");
            foreach (var d in Names)
            {
                if (c == d) { sb.Append(" — |"); continue; }
                var cell = res.Where(v => v.chassis == c && v.donor == d).ToList();
                sb.Append(cell.Count == 0 ? " ✓ |" : " " + string.Join(" ", cell.GroupBy(v => v.inv).OrderBy(g => g.Key)
                                                                  .Select(g => g.Key + "×" + g.Count())) + " |");
            }
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine($"Разных поломок (строк ниже): **{Breakages(res)}** — это долг сторожа `ChimeraMatrixTests`. Всего нарушений: **{res.Count}** — " +
                      string.Join(", ", res.GroupBy(v => v.inv).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count())));
        sb.AppendLine();

        sb.AppendLine("## Поштучно: виновник — аугмент");
        sb.AppendLine();
        sb.AppendLine("Одинаковые детали одного места свёрнуты в строку (сколько штук, худший случай).");
        sb.AppendLine();
        sb.AppendLine("| шасси | донор | аугмент | инв. | место | что | штук |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var g in res.GroupBy(v => (v.chassis, v.donor, v.graft, v.inv, v.place))
                             .OrderBy(g => g.Key.chassis).ThenBy(g => g.Key.donor).ThenBy(g => g.Key.graft == "всё" ? 0 : 1)
                             .ThenBy(g => g.Key.graft).ThenBy(g => g.Key.inv))
            sb.AppendLine($"| {g.Key.chassis} | {g.Key.donor} | {g.Key.graft} | {g.Key.inv} | {g.Key.place} | {g.First().what} | {g.Count()} |");

        System.IO.File.WriteAllText(OutPath, sb.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        Debug.Log($"[Матрица химер] {res.Count} нарушений, на чистых видах {native.Count} → {OutPath}");
    }
}
