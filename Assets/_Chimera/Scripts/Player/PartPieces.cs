using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>КУСКИ МЕША ВИДА (пилот Б, спека `2026-10-09-chistyj-vid-meshem.md` §7). Модель вида нарезана по тому, чему
/// принадлежит часть: кусок ОРГАНА приезжает на чужое тело графтом (морда — Пасть, уши и мочка — Чутьё), кусок-ЧЕРТА —
/// ступенью идентичности (грива, хвост, целая голова как общая форма вида; спека 27.09, решения 4 и 11, и спека 08.10
/// §2 п.3). Кусок не переносится «кость к кости», как деталь конечности: он встаёт ЖЁСТКО, по шву — центр шва донора
/// в центр шва носителя, один масштаб по отношению эллипсов. Шов носителя — его посадочное место, кольцо его детали
/// или, если модельная линия их ещё не дала, сустав графа с той же меткой.</summary>
public static partial class PartAssembly
{
    /// <summary>КУСКИ ВКЛЮЧАЮТСЯ ТОЛЬКО ДЛЯ КАДРОВ И СТОРОЖЕЙ ПИЛОТА; в игре выключено, пока нарезаны не все виды и нет
    /// ответных мест у носителей (лицо и уши человека). Выключено — графт рисуется как прежде, блоками и полем.</summary>
    public static bool MeshPieces = false;

    /// <summary>СЛИЯНИЕ ИЛИ ЗАМЕНА — РЕШАЮТ ДАННЫЕ НОСИТЕЛЯ (идея геймдизайнера 13.10: «не заменять, а сливать… дизъюнкция»).
    /// Для непрозрачных тел объединение бесплатно: нарисуй оба — и буфер глубины покажет внешнюю оболочку A ∪ B, щели на
    /// стыке нет по построению. Шов носителя — ПОСАДОЧНОЕ МЕСТО на сплошной коже: кусок садится ПОВЕРХ, свой объект
    /// носителя под ним остаётся, а кусок обязан накрыть область места. Шов носителя — кольцо его снимаемой детали
    /// (уши): кусок её ЗАМЕНЯЕТ. Место целиком (голова, хвост) — всегда замена.</summary>
    public static bool Over(SpeciesSO body, BodyPart part, SpeciesSO donor) => Mode(body, part, donor) == Fit.Over;

    /// <summary>СПОСОБ ПОСАДКИ КУСКА. `Over` — слияние поверх; `Replace` — замена снимаемой детали носителя, кусок как есть;
    /// `Weld` — замена со СВАРКОЙ КОЛЕЦ: кольцо куска и ответное кольцо носителя сводятся к общему, сдвиг затухает в
    /// полосе у шва по обе стороны. Сварка — обычная практика модульных персонажей (кольцо шва с одинаковыми вершинами,
    /// полоса затухания; FaceGen держит такое кольцо шеи), а подгонка двух несовпадающих краёв друг к другу — то, что
    /// SnapPaste (Sharf 2006) делает мягкой регистрацией; у нас соответствие вершин задано нарезкой, искать его не надо.
    /// `Rule` — решают данные носителя.</summary>
    public enum Fit { Rule, Over, Replace, Weld }

    /// <summary>Навязать способ всем кускам органов — для ряда кадром («разные слияния на разных частях», геймдизайнер 13.10).</summary>
    public static Fit ForceFit = Fit.Rule;

    /// <summary>Общее кольцо сварки — степенное среднее радиусов двух колец: −∞ — меньшее (кусок сжимается к проёму),
    /// 0 — геометрическое, +∞ — большее (проём растягивается к куску).</summary>
    public static float WeldNorm = 0f;

    /// <summary>Ширина полосы затухания сварки в средних радиусах общего кольца.</summary>
    public static float WeldBand = 1.5f;

    public static Fit Mode(SpeciesSO body, BodyPart part, SpeciesSO donor)
    {
        if (Claimed(part, donor) != null) return Fit.Replace;
        bool seat = Seam(body, part.seam, out var s) && s.skin;
        bool ring = CarrierRing(body, part.seam) != null;
        if (ForceFit == Fit.Over && seat) return Fit.Over;
        if (ForceFit == Fit.Replace && ring) return Fit.Replace;
        if (ForceFit == Fit.Weld && ring && part.ringM != null && part.ringM.Length == CarrierRing(body, part.seam).ringM.Length) return Fit.Weld;
        return seat ? Fit.Over : Fit.Replace;
    }

    /// <summary>Снимаемая деталь носителя с кольцом этого шва (лицо человека под морду): её кольцо — ответное кольцо проёма.</summary>
    static BodyPart CarrierRing(SpeciesSO body, string seam) =>
        body.parts?.FirstOrDefault(p => p != null && p.seam == seam && p.seamFrame && p.ringM != null && p.ringM.Length > 2);

    /// <summary>Сварки, ждущие тела носителя: кусок свой край уже подвинул, проём носителя двигает `ApplyWelds`.</summary>
    struct Weld { public Vector3[] ring, shift; public float band; }
    static readonly List<Weld> welds = new();

    static Vector3 Blend(Vector3 v, Vector3[] ring, Vector3[] shift, float band)
    {
        float near = float.MaxValue, total = 0f; var acc = Vector3.zero;
        for (int i = 0; i < ring.Length; i++)
        {
            float d2 = (v - ring[i]).sqrMagnitude;
            near = Mathf.Min(near, d2);
            float w = 1f / (d2 + 1e-6f);
            acc += shift[i] * w; total += w;
        }
        float t = Mathf.Clamp01(1f - Mathf.Sqrt(near) / band);
        return v + acc / total * (t * t * (3f - 2f * t));
    }

    /// <summary>Подвинуть проёмы носителя к общим кольцам: рендереры тела (не куски), у которых есть вершины на кольце.</summary>
    public static void ApplyWelds(Transform container)
    {
        if (welds.Count == 0) return;
        foreach (var smr in container.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.GetComponent<BodyPartView>() != null || smr.sharedMesh == null || !smr.sharedMesh.isReadable || smr.bones.Length == 0) continue;
            // вершины меша → система тела: (кость в покое) × (её привязка)
            var toBody = container.worldToLocalMatrix * smr.bones[0].localToWorldMatrix * smr.sharedMesh.bindposes[0];
            var v = smr.sharedMesh.vertices.Select(p => toBody.MultiplyPoint3x4(p)).ToArray();
            bool moved = false;
            foreach (var w in welds)
            {
                if (!v.Any(p => w.ring.Any(r => (p - r).sqrMagnitude < 4e-6f))) continue;   // кольца этого шва у объекта нет
                for (int i = 0; i < v.Length; i++) v[i] = Blend(v[i], w.ring, w.shift, w.band);
                moved = true;
            }
            if (!moved) continue;
            var mesh = Object.Instantiate(smr.sharedMesh);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            var back = toBody.inverse;
            mesh.vertices = v.Select(p => back.MultiplyPoint3x4(p)).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            smr.sharedMesh = mesh;
        }
        welds.Clear();
    }

    /// <summary>ФУНКЦИОНАЛ МАСШТАБА — степенное среднее отношений полуосей шва (носитель к донору): p = 0 — геометрическое
    /// (площадь шва), 1 — арифметическое, 2 — среднеквадратичное, +∞ — супремум (большее отношение: кусок накрывает
    /// область целиком). По умолчанию правило выбирает само: слияние — супремум, замена — геометрическое. Значение
    /// здесь — ручка для ряда кадром («поиграть с функционалами», геймдизайнер 13.10); NaN — правило.</summary>
    public static float RingNorm = float.NaN;

    public static float PowerMean(float a, float b, float p)
    {
        if (float.IsPositiveInfinity(p)) return Mathf.Max(a, b);
        if (float.IsNegativeInfinity(p)) return Mathf.Min(a, b);
        if (Mathf.Abs(p) < 1e-4f) return Mathf.Sqrt(a * b);
        return Mathf.Pow((Mathf.Pow(a, p) + Mathf.Pow(b, p)) * 0.5f, 1f / p);
    }

    /// <summary>Показать черты этого вида на любом теле, не дожидаясь ступени по составу, — для кадров и сторожей.</summary>
    public static SpeciesSO ForceTraits;
    public static int ForceStep = 2;

    /// <summary>Куски-черты, проступившие на теле: черты вида-цели, чья ступень уже достигнута.</summary>
    public static List<(BodyPart part, SpeciesSO donor)> Traits(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var res = new List<(BodyPart, SpeciesSO)>();
        if (!MeshPieces || chassis == null) return res;
        var (target, step) = GlobalLayer.Step(chassis, worn);
        if (ForceTraits != null && ForceTraits != chassis) { target = ForceTraits; step = ForceStep; }
        if (target?.traits == null || step <= 0) return res;
        foreach (var t in target.traits)
            if (t != null && t.mesh != null && t.bones != null && t.bones.Length > 0 && Mathf.Max(1, t.step) <= step) res.Add((t, target));
        return res;
    }

    /// <summary>МЕСТО, КОТОРОЕ КУСОК НЕСЁТ ЦЕЛИКОМ — со всем, что на нём висит. Кусок-голова (объект `голова`) несёт место
    /// `голова`: уши, глаза и нос носителя под ним не рисуются. Грива и морда сидят на той же кости, но места не забирают:
    /// имя их объекта — другой слот. Null — кусок место не забирает.</summary>
    public static string Claimed(BodyPart part, SpeciesSO donor)
    {
        if (part == null || !part.ringFit || donor?.bones == null) return null;
        var root = ChainRoot(part, donor);
        var place = root != null ? donor.bones.FirstOrDefault(b => b.name == root)?.socket : null;
        if (string.IsNullOrEmpty(place)) return null;
        return !part.piece || (part.mainSlot ?? part.objectSlot) == place ? place : null;
    }

    /// <summary>ШОВ НА ТЕЛЕ: посадочное место (кольцо на сплошной коже), иначе кольцо своей детали или черты с этим швом,
    /// иначе сустав графа с такой меткой (центр — на оси кости, эллипс — радиус × сечение и × глубина).</summary>
    public static bool Seam(SpeciesSO sp, string seam, out BodySeat found)
    {
        found = null;
        if (sp == null || string.IsNullOrEmpty(seam)) return false;
        var seat = sp.seats?.FirstOrDefault(s => s != null && s.seam == seam);
        if (seat != null)
        {
            found = new BodySeat { seam = seat.seam, bone = seat.bone, center = seat.center, axis = seat.axis, up = seat.up, ellipse = seat.ellipse, skin = true };
            return true;
        }
        var own = (sp.parts ?? new BodyPart[0]).Concat(sp.traits ?? new BodyPart[0])
                  .FirstOrDefault(p => p != null && p.seam == seam && p.seamFrame && p.ellipse.x > 0f && p.ellipse.y > 0f);
        if (own != null)
        {
            found = new BodySeat { seam = seam, bone = own.seamBone, center = own.seamCenter, axis = own.seamAxis, up = own.seamUp, ellipse = own.ellipse };
            return true;
        }
        if (sp.bones == null) return false;
        var by = sp.bones.Where(b => b != null).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
        var posed = new Dictionary<string, (Vector3, Quaternion)>();
        var end = sp.bones.FirstOrDefault(b => b?.mark?.b == seam);
        var start = end == null ? sp.bones.FirstOrDefault(b => b?.mark?.a == seam) : null;
        var bone = end ?? start;
        if (bone == null) return false;
        var (p, r) = SkeletonBuilder.Place(bone, by, posed);
        float rad = end != null ? bone.r1 : bone.r0;
        found = new BodySeat
        {
            seam = seam, bone = bone.name, center = end != null ? SkeletonBuilder.Tip(bone, p, r) : p, axis = r * Vector3.up, up = r * Vector3.forward,
            ellipse = new Vector2(rad * Mathf.Max(0.05f, bone.section), rad * Mathf.Max(0.05f, bone.depth)),
        };
        return found.ellipse.x > 0f && found.ellipse.y > 0f;
    }

    public static Vector2 SeamEllipse(SpeciesSO sp, string seam) => Seam(sp, seam, out var s) ? s.ellipse : Vector2.zero;

    /// <summary>Масштаб куска: площадь шва носителя к площади шва донора, корнем. Форма кольца не правится — кусок не
    /// сшивается, стык закрывает юбка или перекрытие.</summary>
    public static float RingScale(SpeciesSO body, SpeciesSO donor, BodyPart part)
    {
        if (string.IsNullOrEmpty(part.seam) || part.ellipse.x <= 0f || part.ellipse.y <= 0f) return 1f;
        var c = SeamEllipse(body, part.seam);
        if (c.x <= 0f || c.y <= 0f) return 1f;
        // есть посадочное место — кусок обязан его накрыть (супремум), как бы он ни сел; иначе — по площади кольца
        bool cover = Claimed(part, donor) == null && Seam(body, part.seam, out var seat) && seat.skin;
        float p = !float.IsNaN(RingNorm) ? RingNorm : cover ? float.PositiveInfinity : 0f;
        return PowerMean(c.x / part.ellipse.x, c.y / part.ellipse.y, p);
    }

    /// <summary>ГОЛОВА — ВЗГЛЯД (кадр 01.10): кусок, ЗАБРАВШИЙ место на цепи головы (целая голова со своей мордой), держит
    /// мировой поворот донора — морда смотрит вперёд у любого носителя. Шеи у видов не гомологичны по наклону: совмести
    /// кадры швов — и волчья морда на отвесной шее человека уставится в небо. Остальные куски встают кадром шва, ось к
    /// оси и «верх» к «верху»: хвост — и воротник гривы, который иначе торчит назад вдоль волчьей шеи, а манишкой
    /// закрывает лицо (кадр и разбор модельной линии 13.10).
    ///     То же у кусков, чей шов лежит на самой голове (морда, уши, мочка): волчье ухо стоит торчком, а посадочное
    /// кольцо уха человека смотрит вбок — кадром шва уши легли горизонтально, «коровьи» (кадр 13.10).</summary>
    static bool Gaze(BodyPart part, SpeciesSO donor)
    {
        var root = ChainRoot(part, donor);
        if (root == null || donor.bones.FirstOrDefault(b => b.name == root)?.limb != "голова") return false;
        return Claimed(part, donor) != null || donor.bones.FirstOrDefault(b => b.name == part.seamBone)?.limb == "голова";
    }

    static bool CanPlaceRing(SpeciesSO body, BodyPart part, SpeciesSO donor) =>
        Seam(body, part.seam, out var seat) && (part.seamFrame || Seam(donor, part.seam, out _))
        && (!string.IsNullOrEmpty(seat.bone) || Weighted(part).Any(n => SkinBone(body, donor, part, n) != null));

    static GameObject PlaceRing(Transform container, SpeciesSO body, BodyPart part, SpeciesSO donor, Material mat)
    {
        var skeleton = container.Find("Skeleton");
        if (skeleton == null || part.mesh == null || !part.mesh.isReadable || part.bones == null) return null;
        if (!Seam(body, part.seam, out var c)) return null;
        // замена и сварка сажают кусок на кольцо снимаемой детали носителя, слияние — на посадочное место
        var mode = Mode(body, part, donor);
        var carrierRing = mode == Fit.Over ? null : CarrierRing(body, part.seam);
        if (carrierRing != null)
            c = new BodySeat { seam = part.seam, bone = carrierRing.seamBone ?? c.bone, center = carrierRing.seamCenter, axis = carrierRing.seamAxis, up = carrierRing.seamUp, ellipse = c.ellipse };
        BodySeat d;
        if (part.seamFrame) d = new BodySeat { center = part.seamCenter, axis = part.seamAxis, up = part.seamUp, ellipse = part.ellipse };
        else if (!Seam(donor, part.seam, out d)) return null;

        var xf = new Dictionary<string, Transform>();
        foreach (var t in skeleton.GetComponentsInChildren<Transform>(true)) if (!xf.ContainsKey(t.name)) xf[t.name] = t;

        float k = RingScale(body, donor, part);
        bool gaze = Gaze(part, donor);
        var toBody = part.toBody == default ? Matrix4x4.identity : part.toBody;
        // ФОРМА-КЛЮЧ ПЛАНА НОСИТЕЛЯ (спека конструктора §4, как у деталей конечностей): воротник гривы сделан под шею
        // четвероногого — масса внизу, под шеей; на двуногом это борода-нагрудник. Ключ «двуногий» перекладывает её на
        // плечи и спину (поставка модельной линии 13.10). Ключ — форма, прибавляется ДО посадки; нет ключа — меш как есть
        var raw = part.mesh.vertices;
        if (UsePlanKey && !string.IsNullOrEmpty(part.plan) && part.plan != body.Plan)
        {
            int key = part.mesh.GetBlendShapeIndex(body.Plan);
            if (key >= 0)
            {
                var dv = new Vector3[raw.Length];
                part.mesh.GetBlendShapeFrameVertices(key, part.mesh.GetBlendShapeFrameCount(key) - 1, dv, null, null);
                for (int i = 0; i < raw.Length; i++) raw[i] += dv[i];
            }
        }
        var sv = raw.Select(v => toBody.MultiplyPoint3x4(v)).ToArray();
        var sw = part.mesh.boneWeights; var st = part.mesh.triangles;
        bool flip = toBody.determinant < 0f;
        int n = sv.Length, sides = part.mirror ? 2 : 1;
        var verts = new List<Vector3>(n * sides); var weights = new List<BoneWeight>(n * sides); var tris = new List<int>(st.Length * sides);
        var bones = new List<Transform>(); var bind = new List<Matrix4x4>();
        int BoneIndex(Transform t)
        {
            int i = bones.IndexOf(t);
            if (i >= 0) return i;
            bones.Add(t); bind.Add(t.worldToLocalMatrix * container.localToWorldMatrix);
            return bones.Count - 1;
        }

        static Vector3 MirrorX(Vector3 v) => new(-v.x, v.y, v.z);
        static Quaternion Frame(Vector3 axis, Vector3 up) => Quaternion.LookRotation(axis.normalized, up.normalized);
        for (int side = +1, s = 0; s < sides; s++, side = -1)
        {
            Vector3 dC = d.center, dA = d.axis, dU = d.up, cC = c.center, cA = c.axis, cU = c.up;
            if (part.seamBiped && body.Plan == "двуногий" && part.plan != body.Plan) dC = part.seamCenterBiped;
            if (side < 0) { dC = MirrorX(dC); dA = MirrorX(dA); dU = MirrorX(dU); cC = MirrorX(cC); cA = MirrorX(cA); cU = MirrorX(cU); }
            var rot = gaze ? Quaternion.identity : Frame(cA, cU) * Quaternion.Inverse(Frame(dA, dU));

            // кость, ведущая вершины: своя на носителе, а если у него такой нет (хвост у человека) — кость посадочного места
            var lead = new int[part.bones.Length];
            for (int i = 0; i < lead.Length; i++)
            {
                string name = SkinBone(body, donor, part, part.bones[i]) ?? c.bone;
                Transform t = null;
                if (name != null && !(side < 0 && xf.TryGetValue(name + ".L", out t))) xf.TryGetValue(name, out t);
                if (t == null && c.bone != null) xf.TryGetValue(c.bone, out t);
                lead[i] = t != null ? BoneIndex(t) : -1;
            }

            // ПАРНЫЙ КУСОК В ОДНОМ ОБЪЕКТЕ (оба уха — объект `Чутьё`): шов дан для одной стороны, и на каждую сторону идёт
            // только её половина меша — иначе оба уха садятся на одно кольцо и зеркалятся вторым заходом (четыре уха, 13.10)
            bool half = part.mirror && Mathf.Abs(d.center.x) > 1e-4f;
            var map = new int[n];

            for (int v = 0; v < n; v++)
            {
                map[v] = -1;
                if (half && sv[v].x * d.center.x < 0f) continue;
                map[v] = verts.Count;
                var p = sv[v];
                if (side < 0) p = MirrorX(p);
                verts.Add(cC + rot * (p - dC) * k);
                var w = sw.Length == n ? sw[v] : new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                var bw = new BoneWeight(); float total = 0f;
                void Add(int bi, float wt, int slot)
                {
                    if (wt <= 0f || bi < 0 || bi >= lead.Length || lead[bi] < 0) return;
                    total += wt;
                    switch (slot) { case 0: bw.boneIndex0 = lead[bi]; bw.weight0 = wt; break; case 1: bw.boneIndex1 = lead[bi]; bw.weight1 = wt; break;
                                    case 2: bw.boneIndex2 = lead[bi]; bw.weight2 = wt; break; default: bw.boneIndex3 = lead[bi]; bw.weight3 = wt; break; }
                }
                Add(w.boneIndex0, w.weight0, 0); Add(w.boneIndex1, w.weight1, 1); Add(w.boneIndex2, w.weight2, 2); Add(w.boneIndex3, w.weight3, 3);
                if (total <= 0f) return null;   // вершину некому вести — кусок не встаёт
                bw.weight0 /= total; bw.weight1 /= total; bw.weight2 /= total; bw.weight3 /= total;
                weights.Add(bw);
            }
            bool swap = flip ^ (side < 0);
            for (int i = 0; i < st.Length; i += 3)
            {
                if (map[st[i]] < 0 || map[st[i + 1]] < 0 || map[st[i + 2]] < 0) continue;
                tris.Add(map[st[i]]);
                tris.Add(map[swap ? st[i + 2] : st[i + 1]]);
                tris.Add(map[swap ? st[i + 1] : st[i + 2]]);
            }
        }

        // СВАРКА КОЛЕЦ: кольцо куска (уже на носителе) и ответное кольцо проёма сводятся к общему
        if (mode == Fit.Weld && sides == 1 && carrierRing != null)
        {
            var rot0 = gaze ? Quaternion.identity : Quaternion.LookRotation(c.axis.normalized, c.up.normalized) * Quaternion.Inverse(Quaternion.LookRotation(d.axis.normalized, d.up.normalized));
            int m = part.ringM.Length;
            var q = new Vector3[m]; var target = new Vector3[m]; float mean = 0f;
            for (int i = 0; i < m; i++)
            {
                q[i] = c.center + rot0 * (part.ringM[i] - d.center) * k;
                Vector3 a = carrierRing.ringM[i] - c.center, b = q[i] - c.center;
                target[i] = c.center + (a + b).normalized * PowerMean(a.magnitude, b.magnitude, WeldNorm) + c.axis.normalized * Vector3.Dot((a + b) * 0.5f, c.axis.normalized);
                mean += (target[i] - c.center).magnitude / m;
            }
            float band = mean * WeldBand;
            var own = Enumerable.Range(0, m).Select(i => target[i] - q[i]).ToArray();
            for (int i = 0; i < verts.Count; i++) verts[i] = Blend(verts[i], q, own, band);
            welds.Add(new Weld { ring = carrierRing.ringM, shift = Enumerable.Range(0, m).Select(i => target[i] - carrierRing.ringM[i]).ToArray(), band = band });
        }

        string name0 = !string.IsNullOrEmpty(part.slot) ? part.slot : part.objectSlot;
        var mesh = new Mesh { name = $"{name0} ({donor.speciesName}, кусок)" };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = bind.ToArray();
        mesh.RecalculateBounds();

        var go = new GameObject(name0);   // ИМЯ = СЛОТ: контракт имён частей
        go.transform.SetParent(container, false);
        go.AddComponent<BodyPartView>();
        if (part.own.a > 0f) go.AddComponent<PartMark>().own = part.own;   // свой цвет куска (полость, зубы, мочка)
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones.ToArray();
        smr.rootBone = skeleton;
        smr.sharedMaterial = mat;
        BoneMesher.Cull(smr, mesh);
        return go;
    }
}
