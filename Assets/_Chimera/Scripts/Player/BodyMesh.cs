using System.Collections.Generic;
using UnityEngine;

/// <summary>ЧИСТЫЙ ВИД — ЦЕЛЬНЫМ МЕШЕМ (спека `2026-10-09-chistyj-vid-meshem.md`, §2 п.3–4). Меш вида — модель с
/// арматурой и объектом на слот (`SpeciesSO.bodyMesh`); в игре он встаёт на СКЕЛЕТ ГРАФА, а не на свой: кости ищутся
/// по именам, поэтому гнёзда органов, метки суставов и всё, что двигает кости, работают как у поля.
///
/// ПОЗЕ КОСТЕЙ МОДЕЛИ НЕ ВЕРИМ — только именам. Экспорт Blender→FBX крутит оси костей по-своему, и привязка «как в
/// файле» требовала бы совпадения поворотов до градуса. Вместо этого привязка пересчитывается здесь: вершины
/// переводятся в систему тела (она у меша и скелета общая — модель подогнана к выгруженному скелету), а матрица
/// привязки каждой кости берётся от НАШЕЙ кости, как это делает `BoneMesher` для поля.
///
/// КОСТЬ СВЕРХ ГРАФА (у волка — `челюсть`): её в скелете нет, она создаётся под своим родителем по имени, в том месте,
/// где стоит в модели.
///
/// СВОЙ ЦВЕТ — ОТДЕЛЬНЫМ ОБЪЕКТОМ. Игра красит рендерер целиком (цвет по составу, эмоции, телеграф), поэтому кусок со
/// своей окраской — полость рта, зубы — в модели отдельный объект `слот.что` (`голова.полость`, `голова.зубы`) с
/// материалом нужного цвета. Рендерер получает имя слота (контракт имён цел), а цвет материала уходит в `PartMark`:
/// микшер держит его базой вместо цвета состава, как у глаз из блоков.</summary>
public static class BodyMesh
{
    /// <summary>Выключатель для инструментов: стенд и кадры сравнивают меш вида с его же полем. Тест, который его
    /// трогает, возвращает значение в `TearDown` — статика переживает прогон.</summary>
    public static bool Enabled = true;

    /// <summary>ДЕТЕКТОРЫ КОНСТРУКТОРА МЕРЯЮТ ЯЗЫК КОНСТРУКТОРА. Матрица химер, карта тел и выгрузка скелета считают детали
    /// органов и кости оболочки — у меша вида их нет, и чистый донор дал бы «деталей 0»: поломки химер «ушли» бы сами
    /// (поймано 10.10 на первом же FBX — четыре И1 у Змея+Волк). `using (BodyMesh.FieldOnly())` строит тело полем.</summary>
    public static System.IDisposable FieldOnly() => new Off();

    sealed class Off : System.IDisposable
    {
        readonly bool was = Enabled;
        public Off() => Enabled = false;
        public void Dispose() => Enabled = was;
    }

    // привязка зависит от позы скелета, а она — от данных вида: ключ по содержимому (исходный меш × матрицы),
    // иначе после пересоздания видов меш остался бы привязан к прежним костям молча
    static readonly BoundedCache<string, Mesh> cache = new(64);

    /// <summary>ПЕРЕИМПОРТ МОДЕЛИ НЕ МЕНЯЕТ ИДЕНТИФИКАТОР МЕША: Unity подменяет содержимое под тем же `GetInstanceID`, и
    /// кэш молча отдавал прежнюю геометрию (поймано 10.10 модельной линией: в ассете «голова» 1 686 тр, в собранном
    /// теле — 796). В редакторе кэш сбрасывает импорт модели (`BodyMeshImport`); в игре переимпорта не бывает.</summary>
    public static void ResetCache() => cache.Clear();

    /// <summary>Тело рисуется мешем вида: меш есть, смешения пропорций нет и каждый надетый орган — родной.</summary>
    public static bool Fits(SpeciesSO chassis, IReadOnlyList<Organ> worn, BodySocket[] plan)
    {
        if (!Enabled || chassis == null || chassis.bodyMesh == null || plan != null || worn == null) return false;
        if (chassis.bones == null || chassis.bones.Length == 0 || chassis.organs == null) return false;
        foreach (var o in worn)
            if (o != null && System.Array.IndexOf(chassis.organs, o) < 0) return false;
        return true;
    }

    /// <summary>Поставить объекты модели на скелет: рендерер на объект, имя рендерера = имя объекта (слот).
    /// Возвращает число поставленных рендереров.</summary>
    public static int Place(Transform container, Transform skeleton, GameObject model, Material mat)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (var t in skeleton.GetComponentsInChildren<Transform>(true))
            if (t != skeleton && !bones.ContainsKey(t.name)) bones[t.name] = t;

        var srcBones = new HashSet<Transform>();
        var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in renderers)
            foreach (var b in r.bones) if (b != null) srcBones.Add(b);

        int placed = 0;
        foreach (var src in renderers)
        {
            var mesh = src.sharedMesh;
            if (mesh == null || src.bones.Length == 0) continue;
            var srcBind = mesh.bindposes;
            int k = System.Array.FindIndex(src.bones, b => b != null);
            if (k < 0 || srcBind.Length != src.bones.Length)
            {
                Debug.LogError($"[меш вида] {model.name}/{src.name}: костей {src.bones.Length}, матриц привязки {srcBind.Length} — объект пропущен");
                continue;
            }
            // вершины → система модели: (кость в модели) × (её привязка) одна и та же для любой кости, пока модель в позе привязки
            Matrix4x4 toBody = InModel(src.bones[k], model.transform) * srcBind[k];

            var ours = new Transform[src.bones.Length];
            var bind = new Matrix4x4[src.bones.Length];
            var stamp = new System.Text.StringBuilder().Append(mesh.GetInstanceID());
            for (int i = 0; i < ours.Length; i++)
            {
                ours[i] = src.bones[i] != null ? Resolve(src.bones[i], bones, srcBones, model.transform, container, skeleton) : skeleton;
                bind[i] = ours[i].worldToLocalMatrix * container.localToWorldMatrix * toBody;
                stamp.Append('|').Append(ours[i].name).Append(bind[i].GetHashCode());
            }
            string key = stamp.ToString();
            if (!cache.TryGetValue(key, out var skinned) || skinned == null)   // запись бывает мёртвой, как у оболочки поля
            {
                skinned = Object.Instantiate(mesh);
                skinned.name = src.name;
                skinned.hideFlags = HideFlags.HideAndDontSave;
                skinned.bindposes = bind;
                cache[key] = skinned;
            }

            int dot = src.name.IndexOf('.');
            var go = new GameObject(dot > 0 ? src.name.Substring(0, dot) : src.name);   // ИМЯ = СЛОТ: контракт имён частей
            if (dot > 0 && src.sharedMaterial != null && src.sharedMaterial.HasProperty("_BaseColor"))
            {
                var own = src.sharedMaterial.GetColor("_BaseColor"); own.a = 1f;
                go.AddComponent<PartMark>().own = own;
            }
            go.transform.SetParent(container, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = skinned;
            smr.bones = ours;
            smr.rootBone = skeleton;
            if (Application.isPlaying)
            {
                // границы — в системе скелета, а меш живёт в своей (у FBX из Blender бывает ×100 и поворот)
                smr.updateWhenOffscreen = false;
                var b = InSpace(mesh.bounds, toBody); b.Expand(0.6f);
                smr.localBounds = b;
            }
            else smr.updateWhenOffscreen = true;
            if (mat != null) smr.sharedMaterial = mat;
            placed++;
        }
        return placed;
    }

    /// <summary>Наша кость для кости модели: по имени; нет такой — создаём под родителем там, где она стоит в модели.</summary>
    static Transform Resolve(Transform src, Dictionary<string, Transform> bones, HashSet<Transform> srcBones,
                             Transform model, Transform container, Transform skeleton)
    {
        if (bones.TryGetValue(src.name, out var hit)) return hit;
        // родитель — ближайший предок, который сам кость (между костями в модели бывают пустые узлы экспорта)
        Transform p = src.parent;
        while (p != null && p != model && !srcBones.Contains(p) && !bones.ContainsKey(p.name)) p = p.parent;
        var parent = p != null && p != model ? Resolve(p, bones, srcBones, model, container, skeleton) : skeleton;

        var m = InModel(src, model);
        var t = new GameObject(src.name).transform;
        t.SetParent(parent, false);
        t.position = container.TransformPoint(m.GetColumn(3));
        t.rotation = container.rotation * Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        bones[src.name] = t;
        return t;
    }

    /// <summary>Матрица узла в системе модели, как если бы модель стояла в нуле без поворота.</summary>
    static Matrix4x4 InModel(Transform t, Transform model)
    {
        var m = Matrix4x4.identity;
        for (; t != null; t = t.parent)
        {
            m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
            if (t == model) break;
        }
        return m;
    }

    static Bounds InSpace(Bounds b, Matrix4x4 m)
    {
        var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1))));
        return r;
    }
}
