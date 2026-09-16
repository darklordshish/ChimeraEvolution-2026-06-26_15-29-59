using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ПОЛИГОН — стенд для сравнения тел. Строит НЕСКОЛЬКО существ В ОДИН РЯД с общей линейкой и
/// одной камерой, чтобы разницу было видно глазом, а не по памяти между двумя кадрами.
///
/// ЗАЧЕМ РЯД, А НЕ ОТДЕЛЬНЫЕ КАДРЫ. Смешение по идентичности читается только в сравнении: кадр химеры
/// сам по себе выглядит «нормальным телом», и лишь рядом с чистым шасси и чистым донором видно, потянулась
/// ли пропорция и куда. Ровно этого не хватало 17.09, когда «человек + волчьи Пасть и Чутьё» оказался
/// неотличим от человека — вывод стоил отдельного захода, а на стенде был бы виден сразу.
///
/// ЧТО ЛЕЖИТ В СЦЕНЕ, А ЧТО СТРОИТСЯ. В `Assets/Scenes/Полигон.unity` сохранён только СВЕТ и якорь.
/// Существа, линейка и камера строятся командой и НЕ СОХРАНЯЮТСЯ: сцена с запечёнными телами стала бы
/// вторым источником правды рядом с данными видов и разошлась бы с ними молча — та самая болезнь, от
/// которой написана спека «одна линия партии».
///
/// КАК ЗВАТЬ (скилл `chimera-unity`, раздел «Полигон»):
///   unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Species --args '["profile",1.6]'
///   unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Compare
///        --args '["Assets/_Chimera/Data/Человек.asset","Assets/_Chimera/Data/Волк.asset","Пасть,Чутьё","profile",1.6]'
///   unity command capture_game_view --camera СтендCam --width 1600 --height 1000 --save_path Кадры/Ряд.png
///   unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Wipe</summary>
public static class Stand
{
    const string Rig = "~СТЕНД";
    const float Gap = 0.6f;          // просвет между фигурами, м
    static readonly string[] All = { "Человек", "Волк", "Лось", "Ёж", "Змея" };

    /// <summary>Все пять видов в ряд. `view`: profile | front. `aspect` — ширина кадра к высоте.</summary>
    public static string Species(string view, float aspect)
    {
        var items = new List<(SpeciesSO chassis, List<Organ> worn, string label, BodySocket[] plan)>();
        foreach (var name in All)
        {
            var so = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + name + ".asset");
            if (so == null) return "нет ассета вида: " + name;
            items.Add((so, Native(so), so.speciesName, null));
        }
        return Row(items, view, aspect);
    }

    /// <summary>ТРОЙКА СРАВНЕНИЯ: чистое шасси · химера · чистый донор. Именно в таком порядке — смесь
    /// стоит между своими крайностями, и «потянулась ли пропорция» читается за один взгляд.</summary>
    public static string Compare(string chassisAsset, string donorAsset, string slots, string view, float aspect)
    {
        var chassis = AssetDatabase.LoadAssetAtPath<SpeciesSO>(chassisAsset);
        var donor = AssetDatabase.LoadAssetAtPath<SpeciesSO>(donorAsset);
        if (chassis == null || donor == null) return "вид не найден";

        // ХИМЕРА — ТЕМ ЖЕ ПУТЁМ, ЧТО В ИГРЕ И В КАРТЕ ТЕЛ: конструктор ставит графт и отдаёт смешанный
        // план. До 17.09 стенд складывал органы сам и звал билдер БЕЗ плана — пропорции донора на кадре не
        // появлялись по построению, и «химера неотличима от шасси» наполовину была артефактом стенда
        var plan = BodyProbe.ChimeraPlan(chassis, donor, slots, out var mixed, out var grafted);
        if (mixed == null) return "графт не встал: у донора нет органа на «" + slots + "»";

        var items = new List<(SpeciesSO, List<Organ>, string, BodySocket[])>
        {
            (chassis, Native(chassis), chassis.speciesName, null),
            (chassis, mixed, chassis.speciesName + "+" + donor.speciesName + " (" + grafted + ")"
                             + (plan == null ? " БЕЗ ПЛАНА" : ""), plan),
            (donor, Native(donor), donor.speciesName, null),
        };
        return Row(items, view, aspect);
    }

    /// <summary>ПОЛОСКА ПО ОСИ: одно и то же тело при `min · канон · max` одного параметра, в ряд.
    ///
    /// Зачем: пока ось проверяется только на готовом звере, поздно и дорого. Полоска ловит три вещи —
    /// где генератор ломается, где форма перестаёт быть анатомичной и **какие оси вообще ничего не
    /// меняют** (если ×0,3 и ×3 дают один силуэт и те же треугольники, ось не стоит держать как ось).
    ///
    /// Данные вида НЕ ПРАВЯТСЯ: ассет копируется `Instantiate` (Unity клонирует его сериализацией, то
    /// есть вместе с костями и местами), правится копия, копия сносится. Иначе полоска молча испортила
    /// бы вид — ровно тот класс ошибок, против которого написан весь этот инструмент.</summary>
    public static string Axis(string speciesAsset, string axis, float lo, float hi, string view, float aspect)
    {
        var src = AssetDatabase.LoadAssetAtPath<SpeciesSO>(speciesAsset);
        if (src == null) return "вид не найден: " + speciesAsset;

        var items = new List<(SpeciesSO, List<Organ>, string, BodySocket[])>();
        foreach (var k in new[] { lo, 1f, hi })
        {
            var copy = Object.Instantiate(src);
            // ИМЯ ВИДА МЕНЯЕМ ОБЯЗАТЕЛЬНО: `BoneMesher` кэширует оболочку по ключу
            // «speciesName # число костей # слои» — содержимое костей, `skinCell` и `skinBlend` в ключ
            // НЕ входят. Оставь копиям одно имя — и все три полоски получат один и тот же меш из кэша,
            // а полоска покажет «параметр ничего не меняет» там, где он меняет всё. Поймано первым же
            // прогоном 17.09: blend ×0,3 и ×3 дали ровно 48 332 треугольника и тот же габарит
            copy.speciesName = src.speciesName + " ×" + k.ToString("0.##") + " " + axis;
            copy.name = copy.speciesName;
            if (!Apply(copy, axis, k)) { return "ось не знаю: " + axis + " (есть: blend, cell, thickness, section, depth, length, sizeRel, organScale)"; }
            items.Add((copy, Native(copy), axis + " ×" + k.ToString("0.##"), null));
        }
        var report = Row(items, view, aspect);
        return axis + ": " + report;
    }

    /// <summary>Правка одной оси в КОПИИ вида. `false` — оси с таким именем нет.</summary>
    static bool Apply(SpeciesSO s, string axis, float k)
    {
        switch (axis)
        {
            case "blend":                                   // слияние соседних объёмов поля
                s.skinBlend = Mathf.Max(0.0001f, s.skinBlend * k);
                return true;
            case "cell":                                    // РАЗРЕШЕНИЕ СЕТКИ поля — прямо про бюджет
                s.skinCell = Mathf.Max(0.001f, s.skinCell * k);
                return true;
            case "thickness":                               // толщина костей у обоих концов
                if (s.bones != null) foreach (var b in s.bones) { if (b == null) continue; b.r0 *= k; b.r1 *= k; }
                return true;
            case "section":                                 // сечение поперёк (плоский бок против бочки)
                if (s.bones != null) foreach (var b in s.bones) { if (b == null) continue; b.section = (b.section <= 0f ? 1f : b.section) * k; }
                return true;
            case "depth":                                   // сечение вдоль взгляда (ухо, лопасть рога)
                if (s.bones != null) foreach (var b in s.bones) { if (b == null) continue; b.depth = (b.depth <= 0f ? 1f : b.depth) * k; }
                return true;
            case "length":                                  // длины костей — пропорции скелета
                if (s.bones != null) foreach (var b in s.bones) { if (b == null) continue; b.length *= k; }
                return true;
            case "sizeRel":                                 // доля места от родителя (граф мест)
                if (s.sockets != null) foreach (var so in s.sockets) { if (so == null) continue; if (so.sizeRel != Vector3.zero) so.sizeRel *= k; }
                return true;
            case "organScale":                              // габарит формы органа в его месте
                if (s.organs != null) foreach (var o in s.organs) { if (o == null) continue; o.visualScale *= k; }
                return true;
        }
        return false;
    }

    public static string Wipe()
    {
        int n = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.name == Rig) { Object.DestroyImmediate(go); n++; }
        return "снесено: " + n;
    }

    // ── внутреннее ──────────────────────────────────────────────────────────────────────

    static List<Organ> Native(SpeciesSO species)
    {
        var worn = new List<Organ>();
        if (species.organs != null)
            foreach (var o in species.organs) if (o != null) worn.Add(o);
        return worn;
    }

    /// <summary>ТРЕУГОЛЬНИКИ СОБРАННОГО ТЕЛА — то число, которым мерится бюджет (контракт §7.4: зверь
    /// 600–1 500). Считается по факту построенных мешей, а не по данным: разойтись с игрой не может.</summary>
    static int Triangles(Renderer[] rends)
    {
        int n = 0;
        foreach (var r in rends)
        {
            Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh
                   : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
            if (m == null) continue;
            for (int s = 0; s < m.subMeshCount; s++) n += (int)(m.GetIndexCount(s) / 3);
        }
        return n;
    }

    static string Row(List<(SpeciesSO chassis, List<Organ> worn, string label, BodySocket[] plan)> items, string view, float aspect)
    {
        Wipe();
        if (aspect <= 0f) aspect = 1.6f;
        bool front = view == "front";

        var root = new GameObject(Rig);
        // РЯД ИДЁТ ПОПЕРЁК ВЗГЛЯДА: в профиль фигуры расставляются вдоль своей длины (Z), анфас — вдоль
        // ширины (X). Иначе орто-камера сложит их друг в друга и ряд превратится в кашу
        Vector3 along = front ? Vector3.right : Vector3.forward;

        float cursor = 0f, top = 0f;
        var report = new List<string>();

        foreach (var (chassis, worn, label, plan) in items)
        {
            var body = new GameObject(label);
            body.transform.SetParent(root.transform, false);
            var cc = body.AddComponent<CharacterController>();     // билдеру нужен низ капсулы: высоты от земли
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);

            MorphBuilder.Build(body.transform, chassis, worn, plan);

            var rends = body.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Object.DestroyImmediate(body); report.Add(label + ": пусто"); continue; }
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);

            float width = front ? b.size.x : b.size.z;
            // ставим так, чтобы ЛЕВЫЙ край фигуры пришёлся на курсор: ряд идёт встык с просветом
            body.transform.position = along * (cursor - Vector3.Dot(b.min, along));
            cursor += width + Gap;
            top = Mathf.Max(top, b.max.y);
            report.Add(string.Format("{0}: {1} дет., {2} тр, {3:0.00}×{4:0.00}×{5:0.00} м",
                                     label, rends.Length, Triangles(rends), b.size.x, b.size.y, b.size.z));
        }
        float span = Mathf.Max(0.001f, cursor - Gap);

        Ruler(root.transform, along, span, top);

        // ОДНА КАМЕРА НА ВЕСЬ РЯД, рамка считается от ряда — поэтому кадры разных прогонов сравнимы.
        // Вертикаль ведётся от ЗЕМЛИ, а не от центра: ряд длинный, орто-размер задаёт ширина, и если
        // целиться в середину фигур, половина кадра уходит в пустое небо (так и вышло в первом прогоне)
        float orthoSize = Mathf.Max(top * 0.5f + 0.3f, (span * 0.5f + 0.4f) / aspect);
        var cam = new GameObject("СтендCam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform, false);
        Vector3 eye = front ? Vector3.back : Vector3.right;
        Vector3 center = along * (span * 0.5f) + Vector3.up * (orthoSize * 0.8f);   // земля у нижнего края
        cam.transform.position = center + eye * (span + top + 5f);
        cam.transform.rotation = Quaternion.LookRotation(-eye, Vector3.up);
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = (span + top) * 20f + 50f;

        var lamp = new GameObject("Свет").AddComponent<Light>();
        lamp.transform.SetParent(root.transform, false);
        lamp.type = LightType.Directional;
        lamp.intensity = 1.1f;
        lamp.transform.rotation = Quaternion.Euler(35f, front ? 200f : 140f, 0f);

        return string.Join(" · ", report) + string.Format("  [ряд {0:0.00} м, верх {1:0.00} м]", span, top);
    }

    /// <summary>ЛИНЕЙКА В КАДРЕ: линия земли по всему ряду и столб с рисками через 0,5 м. Дефект тогда
    /// читается числом прямо с картинки («холка ниже метки 0,5») — это и есть наше правило «где и
    /// насколько» вместо «выглядит плохо».</summary>
    static void Ruler(Transform parent, Vector3 along, float span, float top)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.45f, 0.5f, 0.55f, 1f) };

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "земля";
        ground.transform.SetParent(parent, false);
        ground.transform.position = along * (span * 0.5f) + Vector3.down * 0.01f;
        ground.transform.localScale = along * (span + 1f) + new Vector3(0.02f, 0.02f, 0.02f) + Vector3.up * 0.0f
                                    + (along == Vector3.right ? new Vector3(0f, 0f, 0.6f) : new Vector3(0.6f, 0f, 0f));
        ground.transform.localScale = new Vector3(Mathf.Max(0.05f, ground.transform.localScale.x),
                                                  0.02f, Mathf.Max(0.05f, ground.transform.localScale.z));
        Paint(ground, mat);

        float marks = Mathf.Ceil(Mathf.Max(top, 1f) / 0.5f);
        for (int i = 1; i <= marks; i++)
        {
            float h = i * 0.5f;
            var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tick.name = "метка " + h.ToString("0.0") + " м";
            tick.transform.SetParent(parent, false);
            bool whole = Mathf.Approximately(h % 1f, 0f);
            tick.transform.position = -along * 0.35f + Vector3.up * h;
            tick.transform.localScale = new Vector3(whole ? 0.30f : 0.16f, 0.015f, whole ? 0.30f : 0.16f);
            Paint(tick, mat);
        }
    }

    static void Paint(GameObject go, Material mat)
    {
        if (go.TryGetComponent<Collider>(out var col)) Object.DestroyImmediate(col);
        if (go.TryGetComponent<Renderer>(out var r)) r.sharedMaterial = mat;
    }
}
