using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ПОЗА СТУПЕНИ (решение 21.10, развилка модельной линии «Б»; спека двух слоёв: поза — глобальный слой; консилиум
/// формы: «длины и поза — в костях, в ключе только масса»). Форма-ключ несёт тело цели В ПОКОЕ шасси, а сутулость, вынос
/// головы и согнутые колени цели — это поворот костей. Поэтому после привязки меша кости поворачиваются от покоя к позе
/// цели (`SpeciesSO.keyPose`, поставка `klyuchi/<вид>-<цель>-poza.json`) на вес ключа — сферически, и кожа гнётся за ними.
///     Как в Blender у модельной линии: меш привязан в покое шасси, каждая кость из файла встаёт рамкой позы целиком —
/// положение head (лерп) и поворот (slerp) от покоя шасси на вес ключа. Сверху вниз: кости без записи едут за родителем.
/// Суставы цели в покое (`sustavy`) при позе не применяются — поза их уже содержит (22.10: суставы поверх позы
/// запрокидывали голову и разводили гриву веером).
///     Прежде поворот считался разностью «поза · покой шасси⁻¹» и ставился поверх покоя суставов. Это требует, чтобы рамки
/// покоя шасси в графе и в риге модельной линии совпадали. У бедра и шеи они расходились на 17–18°, и стопа уезжала на
/// 26 см (21.10). Рамка позы напрямую от рамок покоя шасси не зависит. Сторож — `KeyPoseTests`.</summary>
public static class KeyPose
{
    /// <summary>Есть ли поза ступени у активного ключа — тогда суставы цели ставятся деформацией после привязки, вместе с позой.</summary>
    public static bool HasPose(SpeciesSO species, IReadOnlyDictionary<string, float> keys)
    {
        return Select(species, keys).pose.Count > 0;
    }

    /// <summary>ПОЗА СВОЕЙ СТУПЕНИ (22.10): есть поза ступени, на которой стоит ключ, — она ставится целиком (вес 1); нет — поза
    /// ключа (`step` 0) на вес ступени, как прежде. Ступень читается по весу ключа: вес 1 — ступень 2, меньше — ступень 1.</summary>
    static (Dictionary<string, KeyJoint> pose, float w) Select(SpeciesSO species, IReadOnlyDictionary<string, float> keys)
    {
        var none = (new Dictionary<string, KeyJoint>(), 0f);
        if (species?.keyPose == null || species.keyPose.Length == 0 || keys == null) return none;
        var (key, w) = keys.Where(kv => kv.Value > 1e-4f).Select(kv => (kv.Key, Mathf.Clamp01(kv.Value))).FirstOrDefault();
        if (key == null) return none;
        int step = w >= 1f - 1e-4f ? 2 : 1;
        var own = species.keyPose.Where(j => j != null && j.key == key && j.step == step).ToList();
        var src = own.Count > 0 ? own : species.keyPose.Where(j => j != null && j.key == key && j.step == 0).ToList();
        return (src.GroupBy(j => j.bone).ToDictionary(g => g.Key, g => g.First()), own.Count > 0 ? 1f : w);
    }

    /// <summary>Поза — последней, после привязки всех мешей в покое шасси: каждая кость из файла встаёт РАМКОЙ позы целиком —
    /// положение head и направление (как модельная линия ставит её в Blender). Суставы цели в покое (`sustavy`) при позе не
    /// нужны: поза уже несёт суставы цели (22.10: суставы поверх позы ставили голову запрокинутой, грива веером).
    /// Без позы суставы ставятся до привязки (`BodyMesh.Place`), и здесь ничего не происходит.</summary>
    public static void ApplyAfterBind(Transform container, Transform skeleton, SpeciesSO species, IReadOnlyDictionary<string, float> keys)
    {
        if (!HasPose(species, keys)) return;
        Apply(container, skeleton, species, keys);
    }

    public static int Apply(Transform container, Transform skeleton, SpeciesSO species, IReadOnlyDictionary<string, float> keys)
    {
        if (skeleton == null) return 0;
        var (pose, w) = Select(species, keys);
        if (pose.Count == 0) return 0;

        // ПОКОЙ РИГА (22.10): рамки графа и рига модельной линии у части костей расходятся (хребет 35°, шея, бедро 17–18°).
        // Есть покой рига — поворот позы считается в его соглашении, D = поза · покой⁻¹, и прикладывается к кости графа;
        // без него кость ставится в рамку позы напрямую (верно, только если соглашения совпадают — иначе корпус гнёт назад)
        var rest = (species.keyRigRest ?? new KeyJoint[0]).Where(j => j != null).GroupBy(j => j.bone).ToDictionary(g => g.Key, g => g.First());
        var bones = skeleton.GetComponentsInChildren<Transform>(true).Where(t => t != skeleton && pose.ContainsKey(t.name)).ToList();
        // покой (после суставов цели) — снимается до поворотов: родитель повернётся и потянет детей
        var now = bones.ToDictionary(t => t, t => (pos: container.InverseTransformPoint(t.position), rot: Quaternion.Inverse(container.rotation) * t.rotation));
        int moved = 0;
        foreach (var t in bones.OrderBy(Depth))
        {
            var j = pose[t.name];
            var frame = Quaternion.LookRotation(j.forward, j.along);
            t.rotation = container.rotation * (rest.TryGetValue(t.name, out var r)
                ? Quaternion.Slerp(Quaternion.identity, frame * Quaternion.Inverse(Quaternion.LookRotation(r.forward, r.along)), w) * now[t].rot
                : Quaternion.Slerp(now[t].rot, frame, w));
            t.position = container.TransformPoint(Vector3.Lerp(now[t].pos, j.head, w));   // рамка позы целиком: и положение сустава
            moved++;
        }
        return moved;

        int Depth(Transform t) { int n = 0; for (var p = t; p != null && p != skeleton; p = p.parent) n++; return n; }
    }
}
