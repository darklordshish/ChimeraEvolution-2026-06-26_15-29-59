using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 §1.2: семь форм композиции, поставленных ОТ ЯКОРЕЙ (<c>Entry → LabDoor</c>),
/// а не по соли. Каждая форма — примитив S1 (<see cref="DomeForm"/>): бровка/подошва
/// и <c>H/D ≤ 0,135</c>; выше 22° не создаётся.
/// <para>
/// S5 G1/G2 добавили к семи формам: четыре <b>сухие выемки</b> (баланс P/N объёмом,
/// не подкруткой метрики) и <b>полосы лазания</b> на бровках плеч ворот — своя форма
/// у каждой ≤22°, в сумме с плечом борт попадает в 22–35° (S2 §10.5), стены не создаются.
/// </para>
/// <para>
/// Это замена случайного P5-lite (<see cref="DomeRelief"/>) для v2. v1 остаётся на
/// старом P5-lite бит-в-бит; сюда он не заходит.
/// </para>
/// <para>
/// Плато станции объявлено в таблице, но <b>не строится здесь</b>: по плану S2 шаг 5
/// его делает <see cref="DomeHill"/> (верх 70×55, борт 37 м). Иначе плато и холм
/// сложились бы в +16 м.
/// </para>
/// </summary>
public static class DomeComposition
{
    public enum Role
    {
        Ridge = 0,
        CapeA = 1,
        CapeB = 2,
        StationPlateau = 3,
        Bowl = 4,
        Hollow = 5,
        Saddle = 6,
        /// <summary>S5 G2: выемка на высоком борту (баланс P/N объёмом).</summary>
        BasinC = 7,
        BasinS = 8,
        BasinE = 9,
        BasinW = 10,
    }

    public struct Placed
    {
        public Role role;
        public string name;
        public bool applied;
        public float shiftV;
        public int pieces;
        public string note;
    }

    public sealed class State
    {
        /// <summary>Формы, которые реально входят в пол (порядок применения).</summary>
        public List<DomeForm> forms = new List<DomeForm>();
        /// <summary>Все семь ролей: применённые и отклонённые с причиной.</summary>
        public List<Placed> declared = new List<Placed>();
        public string note;
        /// <summary>Какая сторона v выбрана (отражение раскладки). Диагностика/отчёт S5.</summary>
        public bool flipped;
        /// <summary>Ось u (Entry → LabDoor) и v выбранной раскладки — для карты и проб.</summary>
        public Vector2 origin, uAxis, vAxis;
    }

    public const int DeclaredRoles = 11;

    /// <summary>Заявленные числа S2 §1.2: D, бровка, H (для отчёта и тестов).</summary>
    public static DomeForm Ridge() =>
        CrestCapsule("Гряда-силь", Vector2.zero, Vector2.up, 150f, 245f, 24f, 24f,
            DomeFormSign.Lift);

    public static DomeForm CapeA() =>
        DomeForms.Plateau(Vector2.zero, 202f, 34f, 22f, DomeFormSign.Lift, "Мыс A");

    public static DomeForm CapeB() =>
        DomeForms.Plateau(Vector2.zero, 166f, 29f, 18f, DomeFormSign.Lift, "Мыс B");

    public static DomeForm StationPlateau() =>
        DomeForms.Plateau(Vector2.zero, 137f, 62f, 8f, DomeFormSign.Lift, "Плато станции");

    public static DomeForm Bowl() =>
        DomeForms.Plateau(Vector2.zero, 200f, 50f, 10f, DomeFormSign.Carve, "Чаша");

    /// <summary>S5 G2: сухие выемки на высоком борту — баланс P/N объёмом (не подкрутка метрики).</summary>
    public static DomeForm BasinC() => Basin("Котловина С");
    public static DomeForm BasinS() => Basin("Котловина Ю");
    public static DomeForm BasinE() => Basin("Котловина В");
    public static DomeForm BasinW() => Basin("Котловина З");

    static DomeForm Basin(string name) =>
        DomeForms.Plateau(Vector2.zero, 200f, 60f, 12f, DomeFormSign.Carve, name);

    public static DomeForm Hollow() =>
        CrestCapsule("Ложбина", Vector2.zero, Vector2.right, 105f, 60f, 10f, 3f,
            DomeFormSign.Carve);

    public static DomeForm SaddleShoulder() =>
        CrestCapsule("Плечо ворот", Vector2.zero, Vector2.up, 60f, 114f, 12f, 13.4f,
            DomeFormSign.Lift);

    public static DomeForm SaddleNotch() =>
        CrestCapsule("Проход ворот", Vector2.zero, Vector2.right, 60f, 90f, 17f, 6f,
            DomeFormSign.Carve);

    /// <summary>
    /// Поставить формы вокруг <c>Entry → LabDoor</c> по таблице S2 §1.2.
    /// Коллизия: сдвиг по v до 100 м, затем уменьшение D/H до 15 % дважды, затем пропуск
    /// с причиной. Порядок детерминирован; <c>UnityEngine.Random</c> нет.
    /// </summary>
    static System.Func<DomeFootprint, bool> forbiddenNow;
    static System.Func<DomeFootprint, string> forbiddenWhyNow;

    /// <summary>G7 шаг 1: текущая композиция выражается планом, геометрия не меняется.</summary>
    public static MapPlan ToPlan(State state, DomeGenConfigSO cfg, int terrainQuads = 0,
        bool buildTerrace = false, bool buildCanyon = false, bool buildAsh = false,
        bool buildLabComplex = false) =>
        MapPlan.FromLegacy(state, cfg, terrainQuads, buildTerrace, buildCanyon, buildAsh, buildLabComplex);

    public static State Build(DomeGenConfigSO cfg, long seed, Vector2 entry, Vector3 labDoor,
        Vector2 labCenter, DomeHydro.State hydro, DomeMasks masks,
        System.Func<float, float, float> ground, Vector2? ash = null,
        IReadOnlyList<Vector2> lairs = null)
    {
        // Сторона v (лево/право от маршрута) на карте не задана — это отражение.
        // Детерминированно пробуем обе: та, где больше форм встало, и есть раскладка.
        var a = BuildInternal(cfg, seed, entry, labDoor, labCenter, hydro, masks, ground,
            flipV: false, ash, lairs);
        if (AppliedCount(a) >= 5) return a;
        var b = BuildInternal(cfg, seed, entry, labDoor, labCenter, hydro, masks, ground,
            flipV: true, ash, lairs);
        return AppliedCount(b) > AppliedCount(a) ? b : a;
    }

    static int AppliedCount(State s)
    {
        int n = 0;
        foreach (var p in s.declared) if (p.applied) n++;
        return n;
    }

    static State BuildInternal(DomeGenConfigSO cfg, long seed, Vector2 entry, Vector3 labDoor,
        Vector2 labCenter, DomeHydro.State hydro, DomeMasks masks,
        System.Func<float, float, float> ground, bool flipV, Vector2? ash, IReadOnlyList<Vector2> lairs)
    {
        var state = new State();
        if (cfg == null) return state;

        Vector2 doorXZ = new Vector2(labDoor.x, labDoor.z);
        if ((doorXZ - entry).sqrMagnitude < 1f) doorXZ = labCenter;
        Vector2 uAxis = doorXZ - entry;
        if (uAxis.sqrMagnitude < 1f) uAxis = Vector2.right;
        uAxis.Normalize();
        Vector2 vAxis = flipV
            ? new Vector2(uAxis.y, -uAxis.x)
            : new Vector2(-uAxis.y, uAxis.x);
        Vector2 At(float u, float v) => entry + uAxis * u + vAxis * v;
        state.flipped = flipV;
        state.origin = entry;
        state.uAxis = uAxis;
        state.vAxis = vAxis;

        float lake = hydro != null ? hydro.lakeLevel : float.NegativeInfinity;
        System.Func<DomeFootprint, bool> forbidden = MakeForbidden(entry, labCenter, hydro, ash, lairs);
        forbiddenNow = forbidden;
        forbiddenWhyNow = MakeForbiddenWhy(entry, labCenter, hydro, ash, lairs);
        var shifts = new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f, 100f, -100f };
        var scales = new[] { 1f, 0.85f, 0.72f };

        // 1. Гряда-силуэт: капсула вдоль v (длина 300), D 245, бровка 24, H 24.
        Place(state, "Гряда-силь", Role.Ridge, At(1150f, 0f), shifts, scales, masks,
            lake, ground, vAxis, false, (c, s) =>
            {
                var f = CrestCapsule("Гряда-силь", c, vAxis, 150f * s, 245f * s, 24f * s,
                    24f * s, DomeFormSign.Lift);
                return new[] { f };
            });

        // 2. Мыс A: плато 202×34, H 16.
        Place(state, "Мыс A", Role.CapeA, At(430f, 230f), shifts, scales, masks,
            lake, ground, vAxis, false, (c, s) =>
            {
                var f = DomeForms.Plateau(c, 202f * s, 34f * s, 22f * s,
                    DomeFormSign.Lift, "Мыс A");
                return new[] { f };
            });

        // 3. Мыс B: плато 166×29, H 14.
        Place(state, "Мыс B", Role.CapeB, At(690f, -180f), shifts, scales, masks,
            lake, ground, vAxis, false, (c, s) =>
            {
                var f = DomeForms.Plateau(c, 166f * s, 29f * s, 18f * s,
                    DomeFormSign.Lift, "Мыс B");
                return new[] { f };
            });

        // 4. Плато станции — объявлено, строится шагом 5 (DomeHill/плато).
        state.declared.Add(new Placed
        {
            role = Role.StationPlateau,
            name = "Плато станции",
            applied = false,
            note = "S2 шаг 5: верх 70×55, борт 37 м, H+8 делает DomeHill; " +
                   "здесь не строим, иначе плато и холм сложатся",
        });

        // 5. Чаша сухая: шире и мельче — дно остаётся выше воды, объём растёт
        //    площадью, а не глубиной (дно чаши обязано быть сухим, S2 §1.1).
        Place(state, "Чаша", Role.Bowl, At(600f, 430f), shifts, scales, masks,
            lake, ground, vAxis, true, (c, s) =>
            {
                var f = DomeForms.Plateau(c, 200f * s, 50f * s, 10f * s,
                    DomeFormSign.Carve, "Чаша");
                return new[] { f };
            });

        // 6. Сухая ложбина: капсула вдоль оси (240,-80)→(390,-215), D 60, бровка 10, H 3.
        Vector2 hollowA = At(240f, -80f);
        Vector2 hollowB = At(390f, -215f);
        Vector2 hollowCenter = (hollowA + hollowB) * 0.5f;
        Vector2 hollowAxis = hollowB - hollowA;
        if (hollowAxis.sqrMagnitude < 1f) hollowAxis = uAxis;
        hollowAxis.Normalize();
        Place(state, "Ложбина", Role.Hollow, hollowCenter, shifts, scales, masks,
            lake, ground, vAxis, true, (c, s) =>
            {
                var f = CrestCapsule("Ложбина", c, hollowAxis, 105f * s, 60f * s,
                    10f * s, 3f * s, DomeFormSign.Carve);
                return new[] { f };
            });

        // 7. Седло-ворота: два плеча (плато 114, бровка 12, H 13.4) и проход 17 м, H 6.
        Place(state, "Седло-ворота", Role.Saddle, At(840f, 170f), shifts, scales, masks,
            lake, ground, vAxis, false, (c, s) =>
            {
                float gapHalf = (17f * s) * 0.5f + 57f * s;
                var left = CrestCapsule("Плечо ворот L", c + vAxis * gapHalf, vAxis,
                    60f * s, 114f * s, 12f * s, 13.4f * s, DomeFormSign.Lift);
                var right = CrestCapsule("Плечо ворот R", c - vAxis * gapHalf, vAxis,
                    60f * s, 114f * s, 12f * s, 13.4f * s, DomeFormSign.Lift);
                // Проход 17 м остаётся, но борт шире: излом ≤2° (S5 G2) и объём
                // выемки растёт без второй «стены» (S2 §10.5).
                var notch = CrestCapsule("Проход ворот", c, uAxis,
                    60f * s, 90f * s, 17f * s, 6f * s, DomeFormSign.Carve);
                // S2 §10.5, G1: лазаемая полоса на бровке плеча. Своя форма ≤22°
                // (H/D 0.057), но в сумме с плечом её борт попадает в 22–35° —
                // полоса, а не стена: >35° не создаётся.
                var bandL = CrestCapsule("Полоса лазания L", c + vAxis * gapHalf, vAxis,
                    50f * s, 70f * s, 0f, 4f * s, DomeFormSign.Lift);
                var bandR = CrestCapsule("Полоса лазания R", c - vAxis * gapHalf, vAxis,
                    50f * s, 70f * s, 0f, 4f * s, DomeFormSign.Lift);
                return new[] { left, right, notch, bandL, bandR };
            });

        // 8–11. Сухие выемки на высоком борту: форма, а не подкрутка P/N.
        //    Все — «выемка/чаша» из S2 §1.2; следующие — запас, если первая
        //    упирается в маску/воду. Дно выше lakeLevel + 2.5 (проверка Place).
        Place(state, "Котловина С", Role.BasinC, At(750f, 250f), shifts, scales, masks,
            lake, ground, vAxis, true, (c, s) => new[] { BasinAt("Котловина С", c, s) });

        Place(state, "Котловина Ю", Role.BasinS, At(800f, 100f), shifts, scales, masks,
            lake, ground, vAxis, true, (c, s) => new[] { BasinAt("Котловина Ю", c, s) });

        Place(state, "Котловина В", Role.BasinE, At(1000f, 350f), shifts, scales, masks,
            lake, ground, vAxis, true, (c, s) => new[] { BasinAt("Котловина В", c, s) });

        Place(state, "Котловина З", Role.BasinW, At(650f, 200f), shifts, scales, masks,
            lake, ground, vAxis, true, (c, s) => new[] { BasinAt("Котловина З", c, s) });

        int applied = 0;
        foreach (var p in state.declared) if (p.applied) applied++;
        state.note = $"S2 §1.2: объявлено {DeclaredRoles}, применено {applied}; " +
                     (applied < DeclaredRoles
                         ? "отказы — коллизия с масками/сухостью (см. declared)"
                         : "все формы встали");
        return state;
    }

    /// <summary>Сумма форм композиции: знаковая, порядок важен только для «плечи → проход».</summary>
    public static float Apply(State state, float h, float x, float z)
    {
        if (state == null || state.forms == null || state.forms.Count == 0) return h;
        return DomeForms.Apply(state.forms, h, x, z);
    }

    /// <summary>
    /// S2 §1.1/З1–З19: Entry R120, LabCenter 80, озеро R+15, русло halfWidth+10,
    /// ясень R75, логова R25. Проверяется ГАБАРИТ формы, а не центр.
    /// </summary>
    static System.Func<DomeFootprint, bool> MakeForbidden(Vector2 entry, Vector2 labCenter,
        DomeHydro.State hydro, Vector2? ash, IReadOnlyList<Vector2> lairs)
    {
        return f =>
        {
            if (DomeFootprints.Overlaps(f, DomeFootprint.Circle(entry, 120f))) return true;
            if (DomeFootprints.Overlaps(f, DomeFootprint.Circle(labCenter, 80f))) return true;
            if (hydro != null)
            {
                if (hydro.lakeRadius > 0f &&
                    DomeFootprints.Overlaps(f, DomeFootprint.Circle(hydro.lakeCenter,
                        hydro.lakeRadius + 15f))) return true;
                for (int i = 1; i < hydro.riverPts.Count; i++)
                    if (DomeFootprints.Overlaps(f, DomeFootprint.Capsule(
                            hydro.riverPts[i - 1], hydro.riverPts[i],
                            hydro.riverHalfWidth + 10f))) return true;
            }
            if (ash.HasValue &&
                DomeFootprints.Overlaps(f, DomeFootprint.Circle(ash.Value, 75f))) return true;
            if (lairs != null)
                foreach (var l in lairs)
                    if (DomeFootprints.Overlaps(f, DomeFootprint.Circle(l, 25f))) return true;
            return false;
        };
    }

    static System.Func<DomeFootprint, string> MakeForbiddenWhy(Vector2 entry, Vector2 labCenter,
        DomeHydro.State hydro, Vector2? ash, IReadOnlyList<Vector2> lairs)
    {
        return f =>
        {
            if (DomeFootprints.Overlaps(f, DomeFootprint.Circle(entry, 120f))) return "entry120";
            if (DomeFootprints.Overlaps(f, DomeFootprint.Circle(labCenter, 80f))) return "lab80";
            if (hydro != null)
            {
                if (hydro.lakeRadius > 0f && DomeFootprints.Overlaps(f,
                        DomeFootprint.Circle(hydro.lakeCenter, hydro.lakeRadius + 15f))) return "lake15";
                for (int i = 1; i < hydro.riverPts.Count; i++)
                    if (DomeFootprints.Overlaps(f, DomeFootprint.Capsule(hydro.riverPts[i - 1],
                            hydro.riverPts[i], hydro.riverHalfWidth + 10f))) return "river10";
            }
            if (ash.HasValue && DomeFootprints.Overlaps(f,
                    DomeFootprint.Circle(ash.Value, 75f))) return "ash75";
            if (lairs != null)
                foreach (var l in lairs)
                    if (DomeFootprints.Overlaps(f, DomeFootprint.Circle(l, 25f))) return "lair25";
            return "anchor";
        };
    }

    /// <summary>Сухая выемка S5 G2: широкая и неглубокая — дно выше воды, объём площадью.</summary>
    static DomeForm BasinAt(string name, Vector2 center, float s) =>
        DomeForms.Plateau(center, 200f * s, 60f * s, 12f * s, DomeFormSign.Carve, name);

    static DomeForm CrestCapsule(string name, Vector2 center, Vector2 axis, float halfLength,
        float diameter, float crest, float height, DomeFormSign sign)
    {
        return new DomeForm
        {
            name = name,
            sign = sign,
            kind = DomeFormKind.Capsule,
            center = center,
            axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector2.right,
            halfLength = Mathf.Max(0f, halfLength),
            diameter = Mathf.Max(0.01f, diameter),
            crest = Mathf.Clamp(crest, 0f, Mathf.Max(0.02f, diameter - 0.01f)),
            height = Mathf.Abs(height),
        };
    }

    static void Place(State state, string name, Role role, Vector2 baseCenter,
        float[] shifts, float[] scales, DomeMasks masks, float lake,
        System.Func<float, float, float> ground, Vector2 vAxis, bool carve,
        System.Func<Vector2, float, DomeForm[]> make)
    {
        string why = "";
        foreach (float s in scales)
        {
            foreach (float dv in shifts)
            {
                Vector2 c = baseCenter + vAxis * dv;
                var forms = make(c, s);
                if (forms == null || forms.Length == 0) continue;
                bool blocked = false;
                if (masks != null || forbiddenNow != null)
                    foreach (var f in forms)
                    {
                        var foot = DomeMasks.ToFootprint(f);
                        bool bad = (masks != null && masks.BlocksForm(foot)) ||
                                   (forbiddenNow != null && forbiddenNow(foot));
                        if (!bad) continue;
                        blocked = true;
                        if (string.IsNullOrEmpty(why))
                            why = (forbiddenNow != null && forbiddenNow(foot) ? (forbiddenWhyNow?.Invoke(foot) ?? "anchor") :
                                (masks.FirstConflict(foot) ?? "mask")) +
                                $" r={c.magnitude:F0} dv={dv}";
                        break;
                    }
                if (blocked) continue;
                if (carve && ground != null && ground(c.x, c.y) - forms[0].height < lake + 2.5f)
                { why = "dry"; continue; }
                foreach (var f in forms) state.forms.Add(f);
                state.declared.Add(new Placed
                {
                    role = role,
                    name = name,
                    applied = true,
                    shiftV = dv,
                    pieces = forms.Length,
                    note = s < 1f ? $"D/H уменьшены ×{s:F2}" : (dv != 0f ? $"сдвиг v {dv:F0} м" : ""),
                });
                return;
            }
        }
        state.declared.Add(new Placed
        {
            role = role,
            name = name,
            applied = false,
            note = "форма не встала: маски/вода/сухость" +
                   (string.IsNullOrEmpty(why) ? "" : $" ({why})") + "; см. S5 и кадр-разрез",
        });
    }

}
