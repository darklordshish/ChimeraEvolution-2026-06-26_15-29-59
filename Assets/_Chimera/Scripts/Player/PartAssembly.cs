using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>СБОРКА ДЕТАЛЕЙ (спека конструктора §7, §10): деталь вида — авторский меш части — встаёт на кости своей цепи на
/// любом шасси вместо оболочки поля.
///
/// КАК ПЕРЕНОСИТСЯ. Меш детали лежит в координатах тела своего вида (метры, поза покоя графа) и скиннингом привязан к
/// узлам графа. Вершина переводится в кадр своей кости ДОНОРА (поза из графа донора), масштабируется вдоль кости
/// отношением длин и поперёк — отношением радиусов кости носителя к кости донора, и ставится в тот же кадр кости
/// НОСИТЕЛЯ. Кости цепи на носителе уже расставлены подстановкой (`ChainSwap`) — с её калибром, осью и зигзагом, поэтому
/// деталь едет туда же, куда цепь, и отдельной геометрии стыка не нужно. На родном шасси кости те же — деталь встаёт как
/// сделана. Левая сторона — зеркалом по X.
///
/// ЧТО ДЕТАЛЬ ЗАМЕЩАЕТ: оболочку поля своей цепи (кости от корня детали вниз — `fieldSkip`) и куски органа своего места
/// (лапы, когти): деталь несёт облик части целиком. Стык с полем — перекрытием (культя детали уходит в тело).</summary>
public static class PartAssembly
{
    /// <summary>Какие детали встают на это тело: для каждого места — деталь вида, чей орган там виден (родной — у шасси).</summary>
    public static List<(BodyPart part, SpeciesSO donor)> Choose(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var chosen = new List<(BodyPart, SpeciesSO)>();
        if (worn == null) return chosen;
        var slots = new HashSet<string>();
        foreach (var o in worn) if (o != null && !string.IsNullOrEmpty(o.slot)) slots.Add(o.slot);
        foreach (var slot in slots)
        {
            var organ = worn.First(o => o != null && o.slot == slot);   // видимый орган места (порядок — `WornInDrawOrder`)
            var donor = chassis.organs != null && System.Array.IndexOf(chassis.organs, organ) >= 0 ? chassis : ChainSwap.OwnerOf(organ);
            var candidates = donor?.parts?.Where(p => p != null && p.slot == slot && p.mesh != null && p.bones != null && p.bones.Length > 0).ToList();
            if (candidates == null || candidates.Count == 0) continue;
            GraftSeam.TryGetValue(slot, out var seam);
            // ЧУЖОМУ ШАССИ — ТОЛЬКО ДЕТАЛЬ ШВА ПРИВИВКИ (спека `2026-10-06-ruki-kist.md`): у «Рук» это кисть от запястья. Вся
            // передняя нога волка — облик самого волка, другому шасси она не ставится. Своему — облик целиком, кисть — запасом
            var part = donor != chassis
                ? (seam == null ? candidates[0] : candidates.FirstOrDefault(p => p.seam == seam))
                : candidates.FirstOrDefault(p => seam == null || p.seam != seam) ?? candidates[0];
            if (part != null) chosen.Add((part, donor));
        }
        return chosen;
    }

    /// <summary>Шов, на котором аугмент места встаёт на чужое шасси. Нет строки — деталь места ставится целиком.</summary>
    /// <summary>Пределы подгонки кисти на опорной конечности к земле (общий масштаб вокруг сустава).</summary>
    public const float StanceFitMin = 0.67f, StanceFitMax = 1.5f;   // волчьей кисти на лосе нужно ×1.05; предел — от раздувания в разы

    public static readonly Dictionary<string, string> GraftSeam = new() { { BodySlots.Arms, "запястье" } };

    /// <summary>Кость тела, на которую встаёт кость детали: одноимённая (`BodyName`), а если такой нет — кость НОСИТЕЛЯ с той же
    /// меткой конца в той же конечности. Так кисть волка (вес на его `пясть`, метка `запястье`) встаёт в кадр человеческого
    /// предплечья, кончающегося тем же запястьем, — без подстановки цепи. Поле по метке не гасится: `FieldSkip` — по именам.</summary>
    public static string Resolve(SpeciesSO body, SpeciesSO donor, string bone)
    {
        var name = BodyName(body, donor, bone);
        if (name != null) return name;
        var d = donor.bones.FirstOrDefault(b => b.name == bone);
        if (string.IsNullOrEmpty(d?.mark?.b)) return null;
        return body.bones.FirstOrDefault(b => b.limb == d.limb && b.mark?.b == d.mark.b)?.name;
    }

    /// <summary>Имя кости детали на собранном теле: как в графе донора, или с суффиксом донора, если имя занял носитель.</summary>
    public static string BodyName(SpeciesSO body, SpeciesSO donor, string bone)
    {
        if (body.bones.Any(b => b.name == bone + "~" + donor.speciesName)) return bone + "~" + donor.speciesName;
        return body.bones.Any(b => b.name == bone) ? bone : null;
    }

    /// <summary>Кости, которыми деталь ВЛАДЕЕТ: те, к которым у её вершин есть вес. Арматура в FBX несёт весь граф вида (так
    /// и надо — поза покоя целиком), но владеет деталь только своей цепью: иначе она выключила бы поле всего тела
    /// (поймано на пилоте 02.10 — волк без единого отрезка поля).</summary>
    public static string[] Weighted(BodyPart part) => Weighted(part.mesh, part.bones);

    static string[] Weighted(Mesh mesh, string[] names)
    {
        if (mesh == null || names == null) return new string[0];
        var used = new HashSet<int>();
        foreach (var w in mesh.boneWeights)
        {
            if (w.weight0 > 0f) used.Add(w.boneIndex0);
            if (w.weight1 > 0f) used.Add(w.boneIndex1);
            if (w.weight2 > 0f) used.Add(w.boneIndex2);
            if (w.weight3 > 0f) used.Add(w.boneIndex3);
        }
        return used.Where(i => i >= 0 && i < names.Length).Select(i => names[i]).ToArray();
    }

    /// <summary>КОРЕНЬ ЦЕПИ ДЕТАЛИ — кость, которая начинается на её шве (метка начала = тип шва, или метка конца родителя).
    /// Не «верхняя кость с весом»: детали скиннятся и к кости носителя выше шва (лопатка — переход к корпусу, поставка
    /// v6d), и такой корень выключил бы поле лопатки даже у родного волка. Шва нет — старое правило: верхняя кость с весом.</summary>
    public static string ChainRoot(BodyPart part, SpeciesSO donor)
    {
        var weighted = Weighted(part);
        var by = donor.bones.ToDictionary(b => b.name);
        if (!string.IsNullOrEmpty(part.seam))
            foreach (var n in weighted)
                if (by.TryGetValue(n, out var b) && (b.mark?.a == part.seam
                    || (b.parent != null && by.TryGetValue(b.parent, out var pb) && pb.mark?.b == part.seam))) return n;
        return weighted.FirstOrDefault(n => by.ContainsKey(n) && !weighted.Contains(by[n].parent ?? ""));
    }

    /// <summary>Встанет ли деталь на это тело: меш читается, и каждая кость, к которой у вершин есть вес (основного меша и
    /// видимой культи), есть у тела. Иначе `Place` вернёт null, и выключенное заранее поле оставит дыру.</summary>
    public static bool CanPlace(SpeciesSO body, BodyPart part, SpeciesSO donor)
    {
        if (part?.mesh == null || !part.mesh.isReadable || part.bones == null || donor?.bones == null) return false;
        var donorBones = new HashSet<string>(donor.bones.Select(b => b.name));
        var need = Weighted(part).AsEnumerable();
        if (part.StumpShown(body.Plan))
        {
            if (!part.stump.isReadable) return false;
            need = need.Concat(Weighted(part.stump, part.stumpBones));
        }
        return need.All(n => donorBones.Contains(n) && SkinBone(body, donor, part, n) != null);
    }

    /// <summary>Кости донора ВЫШЕ корня цепи детали (у ноги — крестец, у руки — лопатка): к ним культя тянет вес к корпусу.</summary>
    public static HashSet<string> AboveRoot(BodyPart part, SpeciesSO donor)
    {
        var above = new HashSet<string>();
        var by = donor.bones.ToDictionary(b => b.name);
        var root = ChainRoot(part, donor);
        for (var a = root != null && by.TryGetValue(root, out var rb) ? rb.parent : null; a != null && by.TryGetValue(a, out var ab); a = ab.parent) above.Add(a);
        return above;
    }

    /// <summary>Кость тела, которая ВЕДЁТ вершину детали: своя (`Resolve`), а для кости выше корня цепи, которой у носителя
    /// нет (крестец волка у человека — `пояс1`), — кость, на которой цепь висит у носителя: переход к корпусу идёт к его
    /// корпусу, а не к чужой кости.</summary>
    public static string SkinBone(SpeciesSO body, SpeciesSO donor, BodyPart part, string bone)
    {
        var name = Resolve(body, donor, bone);
        if (name != null || !AboveRoot(part, donor).Contains(bone)) return name;
        var root = ChainRoot(part, donor);
        var carrierRoot = root != null ? Resolve(body, donor, root) : null;
        return carrierRoot != null ? body.bones.FirstOrDefault(b => b.name == carrierRoot)?.parent : null;
    }

    /// <summary>Кости, которые поле не рисует: поддерево корня цепи каждой детали (`ChainRoot`).</summary>
    public static string[] FieldSkip(SpeciesSO body, IEnumerable<(BodyPart part, SpeciesSO donor)> parts)
    {
        var skip = new HashSet<string>();
        foreach (var (part, donor) in parts)
        {
            var chainRoot = ChainRoot(part, donor);
            var root = chainRoot != null ? BodyName(body, donor, chainRoot) : null;
            if (root != null)
            {
                var set = new HashSet<string> { root };
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    foreach (var b in body.bones)
                        if (!set.Contains(b.name) && b.parent != null && set.Contains(b.parent)) { set.Add(b.name); grew = true; }
                }
                skip.UnionWith(set);
            }
        }
        return skip.ToArray();
    }

    public static bool UsePlanKey = true;   // для сравнения кадром: деталь без формы-ключа плана

    /// <summary>Поставить деталь на тело: меш переносится на кости носителя, скиннинг — к ним же.</summary>
    public static GameObject Place(Transform container, SpeciesSO body, BodyPart part, SpeciesSO donor, Material mat)
    {
        var go = Place(container, body, part, donor, mat, part.mesh, part.bones, part.toBody, part.slot);
        // АДАПТАЦИЯ ПЛАНА ВКЛЮЧАЕТ КУЛЬТЮ: у волка плечевая кость в туше, у двуногого плечо — видимая рука (02.10, кадр
        // модельной линии: без культи волчий локоть садился у плеча человека — «рука краба»)
        if (go != null && part.StumpShown(body.Plan))
        {
            var st = Place(container, body, part, donor, mat, part.stump, part.stumpBones, part.stumpToBody, "культя");
            if (st != null) st.transform.SetParent(go.transform, false);   // пустой узел масштаба 1: кадр тот же
        }
        return go;
    }

    static GameObject Place(Transform container, SpeciesSO body, BodyPart part, SpeciesSO donor, Material mat,
                            Mesh src, string[] partBones, Matrix4x4 toBody, string name)
    {
        var skeleton = container.Find("Skeleton");
        if (skeleton == null || src == null || !src.isReadable || partBones == null) return null;
        var xf = new Dictionary<string, Transform>();
        foreach (var t in skeleton.GetComponentsInChildren<Transform>(true)) if (!xf.ContainsKey(t.name)) xf[t.name] = t;

        var dBy = donor.bones.ToDictionary(b => b.name);
        var dPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var cBy = body.bones.ToDictionary(b => b.name);
        var toLocal = container.worldToLocalMatrix;

        if (toBody == default) toBody = Matrix4x4.identity;
        var raw = src.vertices;
        // АДАПТАЦИЯ ПЛАНА (спека конструктора §4): деталь другого плана встаёт с формой-ключом, названным планом носителя
        // («двуногий» — волчья нога на человеке, «рука-лапа» оборотня с листа). Ключ — форма, не поза: углы идут костями
        string plan = body.Plan;
        if (UsePlanKey && !string.IsNullOrEmpty(part.plan) && part.plan != plan)
        {
            int k = src.GetBlendShapeIndex(plan);
            if (k >= 0)
            {
                var dv = new Vector3[raw.Length];
                src.GetBlendShapeFrameVertices(k, src.GetBlendShapeFrameCount(k) - 1, dv, null, null);
                raw = raw.Select((v, i) => v + dv[i]).ToArray();
            }
        }
        var sv = raw.Select(v => toBody.MultiplyPoint3x4(v)).ToArray(); var sw = src.boneWeights; var st = src.triangles;
        if (toBody.determinant < 0f) for (int i = 0; i < st.Length; i += 3) (st[i + 1], st[i + 2]) = (st[i + 2], st[i + 1]);
        int n = sv.Length;
        int sides = part.mirror ? 2 : 1;
        var verts = new List<Vector3>(n * sides); var weights = new List<BoneWeight>(n * sides); var tris = new List<int>(st.Length * sides);
        var bones = new List<Transform>(); var bind = new List<Matrix4x4>();
        int BoneIndex(Transform t)
        {
            int i = bones.IndexOf(t);
            if (i >= 0) return i;
            bones.Add(t); bind.Add(t.worldToLocalMatrix * container.localToWorldMatrix);
            return bones.Count - 1;
        }

        // КОСТИ ВЫШЕ КОРНЯ ЦЕПИ — НОСИТЕЛЯ, НЕ ДОНОРА. Культя скиннится и к `лопатка` (переход весов к корпусу), но лопатка
        // носителя не подставляется: у волка она длинная вдоль бока, у человека короткая. Переносить такие вершины кадром
        // лопатки донора → лопатки носителя — растянуть их отношением чужих костей (лоскут в подмышке, кадр 02.10). Ставим
        // их кадром КОРНЯ цепи (шов один), а весом оставляем на своей кости — она и поведёт их при движении
        var mainRoot = ChainRoot(part, donor);
        var above = AboveRoot(part, donor);

        // КИСТЬ НА ОПОРНОЙ КОНЕЧНОСТИ НОСИТЕЛЯ СТОИТ НА ЗЕМЛЕ (стойка за шасси, решение 8 спеки двух слоёв): волчья кисть на
        // лосе висела когтем в 4 см над землёй (письмо модельной линии 07.10). Кисть за суставом подгоняется общим масштабом
        // вокруг сустава — форма та же, низ на земле. Земля — y = 0 в кадре контейнера (высоты от земли)
        bool stanceHand = !string.IsNullOrEmpty(part.seam) && GraftSeam.TryGetValue(part.slot, out var graftSeam) && graftSeam == part.seam
                          && body.stanceLimbs != null && System.Array.IndexOf(body.stanceLimbs, part.slot) >= 0;
        for (int side = +1, s = 0; s < sides; s++, side = -1)
        {
            // кадр каждой кости детали: донор (поза графа, своя сторона) и носитель (трансформ собранного скелета)
            var frames = new (Matrix4x4 toDonorLocal, Vector3 scale, Matrix4x4 carrier, int index, float dLen, float cLen, float tip)[partBones.Length];
            for (int i = 0; i < partBones.Length; i++)
            {
                frames[i].index = -1;
                string own = partBones[i], placeBy = above.Contains(own) ? mainRoot : own;
                if (!dBy.TryGetValue(placeBy, out var db)) continue;
                string cName = Resolve(body, donor, placeBy), skinName = SkinBone(body, donor, part, own);
                if (cName == null || skinName == null || !xf.TryGetValue(side < 0 ? cName + ".L" : cName, out var ct)
                    || !(side < 0 && xf.TryGetValue(skinName + ".L", out var skinT) || xf.TryGetValue(skinName, out skinT))) continue;   // осевая кость (пояс) зеркальной пары не имеет — ведёт она сама   // зеркальная кость — «.L»
                var (dp, dr) = SkeletonBuilder.Place(db, dBy, dPlaced);
                if (side < 0) { dp.x = -dp.x; var e = dr.eulerAngles; dr = Quaternion.Euler(e.x, -e.y, -e.z); }
                var cb = cBy[cName];
                float sLen = db.length > 1e-5f ? cb.length / db.length : 1f;
                float sRad = db.r0 > 1e-5f ? cb.r0 / db.r0 : sLen;
                frames[i] = (Matrix4x4.TRS(dp, dr, Vector3.one).inverse, new Vector3(sRad, sLen, sRad), toLocal * ct.localToWorldMatrix, BoneIndex(skinT), db.length, cb.length,
                             db.r1 > 1e-5f ? cb.r1 / db.r1 : sRad);   // за концом кости калибр — по суставу конца (кисть — по запястью)
            }

            int start = verts.Count;
            var past = new List<int>(); var joint = Vector3.zero;   // вершины за концом кости (кисть) и сустав, на котором она сидит
            for (int v = 0; v < n; v++)
            {
                var p0 = sv[v];
                if (side < 0) p0.x = -p0.x;
                var w = sw.Length == n ? sw[v] : new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                Vector3 acc = Vector3.zero, anchor = Vector3.zero; float total = 0f; bool beyond = true;
                var bw = new BoneWeight();
                void Add(int bi, float wt, int slotIdx)
                {
                    if (wt <= 0f || bi < 0 || bi >= frames.Length || frames[bi].index < 0) return;
                    var f = frames[bi];
                    var local = f.toDonorLocal.MultiplyPoint3x4(p0);
                    // ВДОЛЬ КОСТИ ТЯНЕТСЯ ТОЛЬКО ТО, ЧТО НА НЕЙ: за концом кости (лапа волка ниже запястья — своей кости в графе
                    // у неё нет) и до её начала форма идёт жёстко, поперечным калибром. Иначе кость, вытянутая к суставу
                    // носителя, тянет за собой и лапу: на человеке она доставала до щиколоток (кадр 02.10)
                    //     ПОПЕРЁК — НЕПРЕРЫВНО: вдоль кости калибр идёт от начала (r0) к концу (r1), за концом остаётся r1. Прежде
                    // на кости бралось r0, за концом r1 — и на кольце запястья калибр прыгал (лось: 5.0 → 3.27, «манжета»;
                    // письмо модельной линии 07.10)
                    float y = local.y;
                    if (y > f.dLen) { local = new Vector3(local.x * f.tip, f.cLen + (y - f.dLen) * f.tip, local.z * f.tip); beyond &= true; }
                    else
                    {
                        float rad = y <= 0f ? f.scale.x : Mathf.Lerp(f.scale.x, f.tip, f.dLen > 1e-5f ? y / f.dLen : 1f);
                        local = new Vector3(local.x * rad, y < 0f ? y * f.scale.x : y * f.scale.y, local.z * rad);
                        beyond = false;
                    }
                    anchor = f.carrier.MultiplyPoint3x4(new Vector3(0f, f.cLen, 0f));
                    acc += f.carrier.MultiplyPoint3x4(local) * wt;
                    total += wt;
                    switch (slotIdx) { case 0: bw.boneIndex0 = f.index; bw.weight0 = wt; break; case 1: bw.boneIndex1 = f.index; bw.weight1 = wt; break;
                                       case 2: bw.boneIndex2 = f.index; bw.weight2 = wt; break; default: bw.boneIndex3 = f.index; bw.weight3 = wt; break; }
                }
                Add(w.boneIndex0, w.weight0, 0); Add(w.boneIndex1, w.weight1, 1); Add(w.boneIndex2, w.weight2, 2); Add(w.boneIndex3, w.weight3, 3);
                if (total <= 0f) return null;   // вершина без кости носителя — деталь не встаёт, рисуется полем
                verts.Add(acc / total);
                if (beyond) { past.Add(verts.Count - 1); joint = anchor; }
                bw.weight0 /= total; bw.weight1 /= total; bw.weight2 /= total; bw.weight3 /= total;
                weights.Add(bw);
            }
            if (stanceHand && past.Count > 0)
            {
                float low = past.Min(i => verts[i].y);
                if (joint.y - low > 1e-4f)
                {
                    // ПОДГОНКА — ПОПРАВКА, А НЕ ЗАМЕНА КАЛИБРА: волчьей кисти на лосе нужно ≈ +5 %. Больше половины — значит,
                    // не сходится сам калибр или план, и раздувать кисть в разы (тест: 40×) — прятать это
                    float k = Mathf.Clamp(joint.y / (joint.y - low), StanceFitMin, StanceFitMax);
                    foreach (int i in past) verts[i] = joint + (verts[i] - joint) * k;
                }
            }
            for (int i = 0; i < st.Length; i += 3)
            {
                tris.Add(start + st[i]);
                tris.Add(start + (side < 0 ? st[i + 2] : st[i + 1]));
                tris.Add(start + (side < 0 ? st[i + 1] : st[i + 2]));
            }
        }

        var mesh = new Mesh { name = $"{name} ({donor.speciesName}, деталь {part.slot})" };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();   // вершины детали разварены по граням — нормаль грани, как у листа
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = bind.ToArray();
        mesh.RecalculateBounds();

        var go = new GameObject(name);   // ИМЯ = МЕСТО: контракт имён частей (культя — внутри детали)
        go.transform.SetParent(container, false);
        go.AddComponent<BodyPartView>();   // метка для детекторов: этот рендерер — деталь, он несёт облик части целиком
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones.ToArray();
        smr.rootBone = skeleton;
        smr.sharedMaterial = mat;
        BoneMesher.Cull(smr, mesh);
        return go;
    }
}
