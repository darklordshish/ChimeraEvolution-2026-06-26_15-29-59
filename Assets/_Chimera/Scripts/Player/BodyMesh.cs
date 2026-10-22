using System.Collections.Generic;
using System.Linq;
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
        return PartAssembly.Traits(chassis, worn).Count == 0;   // проступили чужие черты — тело уже составное
    }

    /// <summary>Поставить объекты модели на скелет: рендерер на объект, имя рендерера = имя объекта (слот).
    /// Возвращает число поставленных рендереров.</summary>
    /// <summary>ИДЕНТИЧНОСТЬ — ФОРМАМИ-КЛЮЧАМИ НА МЕШЕ ШАССИ (опыт 14.10, консилиум формы 01.10, В3/В6): у меша шасси есть
    /// ключ на каждый вид-цель (имя ключа = имя вида, цель натянута на топологию шасси по ориентирам), и тело ТЕЧЁТ к виду
    /// весом ключа, а не надевает его куски («костюм на утренник» — вердикт геймдизайнера кускам на ступени 2). Ключ на
    /// пару «шасси → вид», а не меш на химеру. Вес — ступень идентичности (`KeyWeight`): ступень 2 — полный ключ.
    ///     ПРАВАЯ КОЛОНКА ЛИСТА СТУПЕНЕЙ — СТУПЕНЬ 2, А НЕ «ЧИСТЫЙ ВИД» (геймдизайнер 15.10: «самые правые — 2 степень
    /// идентичности, ведь 3 достижима на родном шасси только»). Цель ключа снята с правой колонки, значит на чужом
    /// шасси вес 1 = ступень 2; ступень 3 (чистый вид) у ключа не бывает — это родной меш вида.</summary>
    public static Dictionary<string, float> KeysFor(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var keys = new Dictionary<string, float>();
        if (ForceKey != null) { keys[ForceKey] = ForceWeight; return keys; }
        var (target, step) = GlobalLayer.Step(chassis, worn);
        if (target != null && step > 0) keys[target.speciesName] = KeyWeight(step);
        return keys;
    }

    /// <summary>КЛЮЧИ ОБЛАСТЕЙ (спека `2026-10-21-organ-prorastaet-klyuch-oblasti.md`): орган слота прорастает формой своей области
    /// вида-донора — форма-ключ меша шасси «&lt;вид&gt;·&lt;слот&gt;» (маска области × ключ вида, запекает модельная линия). Вес — доля
    /// вида-донора в составе к порогу ступени 2 (на 85% область уже как у ступени 2). С глобальным ключом того же вида — СУПРЕМУМ:
    /// формы-ключи складываются, поэтому область получает только недостающее, max(0, доля − вес глобального).
    /// Свои органы шасси (родной вид) областей не дают.</summary>
    public static Dictionary<string, float> RegionKeysFor(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var res = new Dictionary<string, float>();
        if (chassis == null || worn == null || ForceKey != null) return res;
        var id = GlobalLayer.Identity(chassis, worn);
        var global = KeysFor(chassis, worn);
        foreach (var o in worn)
        {
            if (o == null || o.chassisOnly || string.IsNullOrEmpty(o.slot)) continue;
            if (chassis.organs != null && System.Array.IndexOf(chassis.organs, o) >= 0) continue;
            var owner = ChainSwap.OwnerOf(o);
            if (owner == null || owner == chassis || !id.TryGetValue(owner, out var share)) continue;
            float r = Mathf.Clamp01(share / GlobalLayer.MediumAt);
            float g = global.TryGetValue(owner.speciesName, out var gw) ? gw : 0f;
            res[owner.speciesName + "·" + o.slot] = Mathf.Max(0f, r - g);
        }
        return res;
    }

    /// <summary>ОРГАН НА ШАБЛОНЕ (22.10, спека «орган прорастает» §2.1: роговой кусок по одному на орган × тип тела). Объект
    /// `<слот>@<вид>` в меше шасси — орган вида-донора, нарисованный прямо на шаблоне (уши Чутья на голове, когти Рук на пальцах):
    /// он уже привязан к костям шаблона, сажать его не нужно. Виден, только пока привит орган этого вида на этот слот; снимает
    /// собственные объекты шасси того же слота; кусок органа донора на шве при нём не ставится. Растёт формой-ключом
    /// `рост` по доле вида-донора от нуля до первой ступени (критик модельной линии 22.10: «одна форма растёт от 15 % до ст.1,
    /// без скачка»). Ключ: имя объекта, значение — вес роста.</summary>
    public static Dictionary<string, float> OrganObjectsFor(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var res = new Dictionary<string, float>();
        if (chassis == null || worn == null) return res;
        var id = GlobalLayer.Identity(chassis, worn);
        foreach (var o in worn)
        {
            if (o == null || o.chassisOnly || string.IsNullOrEmpty(o.slot)) continue;
            if (chassis.organs != null && System.Array.IndexOf(chassis.organs, o) >= 0) continue;
            var owner = ChainSwap.OwnerOf(o);
            if (owner == null || owner == chassis || !id.TryGetValue(owner, out var share)) continue;
            res[OrganObject(o.slot, owner.speciesName)] = Mathf.Clamp01(share / GlobalLayer.WeakAt);
        }
        return res;
    }

    public static string OrganObject(string slot, string species) => slot + "@" + species;
    public const string GrowthShape = "рост";
    static bool IsOrganObject(string name) => name.IndexOf('@') > 0;

    /// <summary>Вес ключа по ступени: 1 — ручка (лист: колонка 2 из «база · ст.1 · · ст.2»), 2 — полный ключ.
    /// Не `GlobalLayer.G`: тот смешивает пропорции поля с ЧИСТЫМ видом (вес 1 — родное шасси), а цель ключа — ступень 2.</summary>
    public static float KeyStep1 = 0.5f;
    public static float KeyWeight(int step) => step >= 2 ? 1f : step == 1 ? KeyStep1 : 0f;

    /// <summary>Ключ и вес вручную — для ряда кадром (вес 0…1).</summary>
    public static string ForceKey;
    public static float ForceWeight;

    public static int Place(Transform container, Transform skeleton, GameObject model, Material mat, ICollection<string> skipSlots = null,
                            IReadOnlyDictionary<string, float> keys = null, SpeciesSO jointsOf = null, IReadOnlyDictionary<string, float> regionKeys = null,
                            IReadOnlyDictionary<string, float> organObjects = null)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (var t in skeleton.GetComponentsInChildren<Transform>(true))
            if (t != skeleton && !bones.ContainsKey(t.name)) bones[t.name] = t;

        var srcBones = new HashSet<Transform>();
        var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in renderers)
            foreach (var b in r.bones) if (b != null) srcBones.Add(b);

        // СКЕЛЕТ ПО ВЕСУ КЛЮЧА — ПОСЛЕ ДОСТРОЙКИ КОСТЕЙ МОДЕЛИ, ДО ПРИВЯЗКИ. Модель несёт кости, которых нет в графе
        // (`стопа` человека, 15.10: граф строит и полевое тело, узел там сдвинул бы химеры), — сначала они достраиваются
        // от своих костей-родителей, потом весь скелет едет к суставам цели, и только тогда снимается привязка
        if (jointsOf != null)
        {
            foreach (var src in renderers)
                foreach (var b in src.bones)
                    if (b != null) Resolve(b, bones, srcBones, model.transform, container, skeleton);
            // С ПОЗОЙ СТУПЕНИ суставы цели — тоже деформация, а не перенос костей до привязки: модельная линия снимает форму в покой
            // цепью «покой К2 → поза» и привязывает в покое К2 (22.10: при переносе суставов до привязки голова @Волк в позе
            // оседала на 16 см — голова цели в покое на 0.2 м выше головы К2). Суставы и позу ставит `MorphBuilder` последними
            if (!KeyPose.HasPose(jointsOf, keys)) KeySkeleton.Apply(container, skeleton, jointsOf, keys);
        }

        // ЧЕРТА КЛЮЧА НА СТУПЕНИ: объект `@<ключ>` рисуется только на её ступени и снимает объекты шасси под собой
        var stages = ActiveStages(jointsOf, keys);
        var stageHides = new HashSet<string>(stages.SelectMany(st => st.hides ?? new string[0]));
        // орган на шаблоне снимает собственные объекты шасси своего слота
        var organSlots = new HashSet<string>(renderers.Where(r => IsOrganObject(r.name) && organObjects != null && organObjects.ContainsKey(r.name))
                                                      .Select(r => SlotOf(r.name)));

        int placed = 0;
        foreach (var src in renderers)
        {
            var mesh = src.sharedMesh;
            if (mesh == null || src.bones.Length == 0) continue;
            if (src.name.StartsWith("@") && !stages.Any(st => st.objectName == src.name)) continue;
            bool organ = IsOrganObject(src.name);
            if (organ && (organObjects == null || !organObjects.ContainsKey(src.name))) continue;
            if (stageHides.Contains(SlotOf(src.name))) continue;
            if (!organ && organSlots.Contains(SlotOf(src.name))) continue;
            // место несёт чужой кусок (химера на меше вида): свой объект этого слота со всеми `слот.что` не ставится
            if (!organ && skipSlots != null && skipSlots.Contains(SlotOf(src.name))) continue;
            var srcBind = mesh.bindposes;
            int k = System.Array.FindIndex(src.bones, b => b != null);
            if (k < 0 || srcBind.Length != src.bones.Length)
            {
                Debug.LogError($"[меш вида] {model.name}/{src.name}: костей {src.bones.Length}, матриц привязки {srcBind.Length} — объект пропущен");
                continue;
            }
            // вершины → система модели: (кость в модели) × (её привязка) одна и та же для любой кости, пока модель в позе привязки
            Matrix4x4 toBody = InModel(src.bones[k], model.transform) * srcBind[k];
            // СТОРОЖ ЭТОГО ДОПУЩЕНИЯ (22.10): объект, привязанный в FBX к скелету в другом покое, чем тело, переносится по одной
            // кости верно, а вершины на остальных костях уезжают на разницу покоев (у @Волк голова ушла на 0.2 м под гриву)
            for (int i = 0; i < src.bones.Length; i++)
            {
                if (src.bones[i] == null) continue;
                var other = InModel(src.bones[i], model.transform) * srcBind[i];
                float off = (other.GetColumn(3) - toBody.GetColumn(3)).magnitude;
                if (off > 0.01f) { Debug.LogError($"[меш вида] {model.name}/{src.name}: привязка кости «{src.bones[i].name}» не в покое модели (расхождение {off:0.000} м) — объект привязан к другому скелету, перевыгрузить с общей арматурой"); break; }
            }

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
            var go = new GameObject(organ || dot > 0 ? SlotOf(src.name) : src.name);   // ИМЯ = СЛОТ: контракт имён частей
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
            if (keys != null)
                foreach (var kv in keys)
                {
                    int shape = skinned.GetBlendShapeIndex(kv.Key);
                    if (shape >= 0) smr.SetBlendShapeWeight(shape, Mathf.Clamp01(kv.Value) * 100f);
                }
            if (regionKeys != null)
                foreach (var kv in regionKeys)
                {
                    int shape = skinned.GetBlendShapeIndex(kv.Key);
                    if (shape >= 0) smr.SetBlendShapeWeight(shape, Mathf.Clamp01(kv.Value) * 100f);
                }
            if (organ)
            {
                int shape = skinned.GetBlendShapeIndex(GrowthShape);
                if (shape >= 0) smr.SetBlendShapeWeight(shape, Mathf.Clamp01(organObjects[src.name]) * 100f);
            }
            if (Application.isPlaying)
            {
                // границы — в системе скелета, а меш живёт в своей (у FBX из Blender бывает ×100 и поворот)
                smr.updateWhenOffscreen = false;
                var b = InSpace(mesh.bounds, toBody); b.Expand(0.6f);
                smr.localBounds = b;
            }
            else smr.updateWhenOffscreen = true;
            if (mat != null) smr.sharedMaterial = src.name.StartsWith("@") ? TwoSided(mat) : mat;
            placed++;
        }
        // ПОЗУ СТУПЕНИ ЗДЕСЬ НЕ СТАВИМ: после меша тела на тот же скелет привязываются куски органов, и их привязка обязана
        // сниматься с костей в покое. Позу ставит `MorphBuilder` последней, после всех кусков (22.10: куски Рук висели в
        // покое снаружи кисти, которая ушла по позе)
        return placed;
    }

    /// <summary>ДВУСТОРОННИЙ МАТЕРИАЛ ДЛЯ ЧЕРТ СТУПЕНИ (22.10, решение по разбору модельной линии): грива, хвост, мех `@<ключ>` —
    /// открытые оболочки, как волосы и мех в играх. С отсечением задних граней дальняя сторона гривы показывала изнанку, грудь
    /// выглядела «щитом»; двусторонность граней копиями давала изнанку, прижатую к груди. Тело шасси остаётся односторонним.</summary>
    public static Material TwoSided(Material mat)
    {
        if (mat == null) return null;
        if (twoSided.TryGetValue(mat, out var m) && m != null) return m;
        m = new Material(mat) { name = mat.name + " · двусторонний", hideFlags = HideFlags.HideAndDontSave };
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // URP Lit: 0 = Off
        twoSided[mat] = m;
        return m;
    }
    static readonly Dictionary<Material, Material> twoSided = new();

    /// <summary>Черты ключа, чья ступень достигнута: вес ключа полный (ступень 2 = вес 1, `KeyWeight`).</summary>
    public static List<KeyStage> ActiveStages(SpeciesSO chassis, IReadOnlyDictionary<string, float> keys)
    {
        var res = new List<KeyStage>();
        if (chassis?.keyStages == null || keys == null) return res;
        foreach (var st in chassis.keyStages)
            if (st != null && keys.TryGetValue(st.key, out var w) && w >= KeyWeight(st.step) - 1e-3f && w > 0f) res.Add(st);
        return res;
    }

    static string SlotOf(string objectName)
    {
        int at = objectName.IndexOf('@'); if (at > 0) return objectName.Substring(0, at);   // орган на шаблоне `<слот>@<вид>`
        int dot = objectName.IndexOf('.'); return dot > 0 ? objectName.Substring(0, dot) : objectName;
    }

    /// <summary>ХИМЕРА НА МЕШЕ ВИДА: тело — объекты меша шасси, место, которое несёт чужой кусок целиком, отдано куску.
    ///     ВАРИАНТ Б (решение геймдизайнера 19.10): шасси с мешем вида рисуется мешем ПРИ ЛЮБОЙ ПРИВИВКЕ. Орган без куска
    /// пока не виден на силуэте (механика его работает) и проступит, когда модельная линия нарежет кусок; ступень
    /// идентичности показывает форма-ключ вида (`KeysFor`). Прежде химера уходила на поле, если хоть один чужой орган
    /// был без куска или идентичность дошла до ступени, — так мешем рисовались 3 химеры из 78 (`MeshSweep`, 19.10), а
    /// поле признано тупиком (13.10).</summary>
    public static bool FitsChimera(SpeciesSO chassis, SpeciesSO body, IReadOnlyList<Organ> worn, BodySocket[] plan)
    {
        if (!Enabled || !PartAssembly.MeshPieces || chassis == null || chassis.bodyMesh == null || worn == null) return false;
        return chassis.bones != null && chassis.bones.Length > 0 && chassis.organs != null;
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
