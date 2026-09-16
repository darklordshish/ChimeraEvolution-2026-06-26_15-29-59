using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>КАДР ТЕЛА — собирает существо НАСТОЯЩИМ билдером прямо в редакторе и ставит камеру по оси,
/// чтобы снимок сделала команда пайплайна `capture_game_view --camera ПрофильCam`.
///
/// ЗАЧЕМ ЭТО ЕСТЬ. До 17.09 силуэт мог увидеть только человек: Claude просил скрин «строго в профиль,
/// крупно, один зверь», и половина итераций уходила на ракурс и перспективу. Пайплайн живого редактора
/// умеет рендерить камеру в PNG — значит кадр берётся сам, ортографический, точно по оси, с ровным фоном.
/// Перспектива и «не тот ракурс» перестают быть источником ошибок вовсе.
///
/// ЛЕЖИТ ВНЕ `Assets/` НАМЕРЕННО: это инструмент агента, а не код игры — Unity его не компилирует,
/// в сборку он не попадает. Пайплайн компилирует файл в памяти по запросу (`run_script`).
///
/// КАК ЗВАТЬ (скилл `chimera-unity`, раздел «Кадры»):
///   unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Build
///                            --args '["Assets/_Chimera/Data/Волк.asset","profile"]'
///   unity command capture_game_view --camera ПрофильCam --width 1000 --height 800 --save_path Кадр.png
///   unity command run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Wipe
///
/// ХИМЕРА — тем же способом: `Chimera` подменяет орган нужного слота органом донора, то есть строит ровно
/// то, что увидит игрок после графта. Чистый вид и химера снимаются ОДНИМ инструментом — иначе сравнивать
/// их нечем (ровно этого не хватало карте тел, см. п.4 спеки идентичности).</summary>
public static class ShotSpecies
{
    const string Rig = "~ШОТ";

    /// <summary>Собрать вид на его родном составе и навести камеру. `view`: profile | front | top.</summary>
    public static string Build(string speciesAsset, string view)
    {
        var species = Load(speciesAsset);
        if (species == null) return "вид не найден: " + speciesAsset;

        var worn = NativeOrgans(species);
        return Stage(species, worn, view, species.speciesName);
    }

    /// <summary>Собрать ХИМЕРУ: шасси со своим составом, но в слоте `slot` — орган донора.
    /// Это и есть графт по данным: MorphBuilder берёт первый орган на слот, донорский кладётся раньше.</summary>
    public static string Chimera(string chassisAsset, string donorAsset, string slot, string view)
    {
        var chassis = Load(chassisAsset);
        var donor = Load(donorAsset);
        if (chassis == null) return "шасси не найдено: " + chassisAsset;
        if (donor == null) return "донор не найден: " + donorAsset;

        // слотов можно назвать несколько через запятую: приёмка смешения идёт на «человек + Пасть + Чутьё»
        var worn = new List<Organ>();
        var taken = new List<string>();
        foreach (var raw in slot.Split(','))
        {
            var name = raw.Trim();
            if (name.Length == 0) continue;
            Organ graft = null;
            if (donor.organs != null)
                foreach (var o in donor.organs)
                    if (o != null && o.slot == name) { graft = o; break; }
            if (graft == null) return "у донора нет органа на слот «" + name + "»";
            worn.Add(graft);          // ПЕРВЫЙ ЗАНЯВШИЙ СЛОТ ПОБЕЖДАЕТ — графты идут впереди родных
            taken.Add(name);
        }
        if (worn.Count == 0) return "не названо ни одного слота";

        worn.AddRange(NativeOrgans(chassis));
        return Stage(chassis, worn, view,
                     chassis.speciesName + " + " + donor.speciesName + " (" + string.Join(", ", taken) + ")");
    }

    /// <summary>Снести сцену кадра. Зовётся всегда после съёмки: сцена не сохраняется, но мусор в ней мешает.</summary>
    public static string Wipe()
    {
        int n = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.name == Rig) { Object.DestroyImmediate(go); n++; }
        return "снесено: " + n;
    }

    // ── внутреннее ──────────────────────────────────────────────────────────────────────

    static SpeciesSO Load(string path) => AssetDatabase.LoadAssetAtPath<SpeciesSO>(path);

    static List<Organ> NativeOrgans(SpeciesSO species)
    {
        var worn = new List<Organ>();
        if (species.organs != null)
            foreach (var o in species.organs) if (o != null) worn.Add(o);
        return worn;
    }

    static string Stage(SpeciesSO chassis, List<Organ> worn, string view, string label)
    {
        Wipe();

        var root = new GameObject(Rig);
        var body = new GameObject("тело");
        body.transform.SetParent(root.transform, false);

        // КОНТРОЛЛЕР НУЖЕН БИЛДЕРУ: высоты в данных заданы ОТ ЗЕМЛИ, а корень объекта у видов разный —
        // без него сборка уедет по вертикали (тот же рецепт, что у `BodyProbe`)
        var cc = body.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.center = new Vector3(0f, 1f, 0f);

        MorphBuilder.Build(body.transform, chassis, worn);

        // РЕАЛЬНЫЕ границы НАРИСОВАННЫХ деталей, а не габариты мест: место может быть не заполнено,
        // и кадр по коробкам мест показал бы пустоту вместо зверя
        var rends = body.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Object.DestroyImmediate(root); return "нечего снимать: рендереров нет"; }
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);

        Vector3 dir = view == "front" ? Vector3.back : view == "top" ? Vector3.up : Vector3.right;
        float span = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

        var cam = new GameObject("ПрофильCam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform, false);
        cam.transform.position = b.center + dir * (span * 3f);
        cam.transform.rotation = Quaternion.LookRotation(-dir, view == "top" ? Vector3.forward : Vector3.up);
        cam.orthographic = true;              // ОРТО, А НЕ ПЕРСПЕКТИВА: пропорции читаются без искажения
        cam.orthographicSize = span * 0.62f;  // запас по краям, чтобы не срезать уши и хвост
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);   // ровный фон: силуэт читается целиком
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = span * 20f;

        // СВОЙ СВЕТ — кадр не должен зависеть от освещения открытой сцены, иначе снимки несравнимы
        var lamp = new GameObject("Свет").AddComponent<Light>();
        lamp.transform.SetParent(root.transform, false);
        lamp.type = LightType.Directional;
        lamp.intensity = 1.1f;
        lamp.transform.rotation = Quaternion.Euler(35f, view == "front" ? 200f : 140f, 0f);

        return string.Format("{0} [{1}]: деталей {2}, габарит {3:0.000}×{4:0.000}×{5:0.000} м",
                             label, view, rends.Length, b.size.x, b.size.y, b.size.z);
    }
}
