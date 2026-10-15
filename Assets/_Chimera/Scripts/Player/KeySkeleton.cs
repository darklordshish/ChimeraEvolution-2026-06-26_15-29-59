using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>СКЕЛЕТ ПО ВЕСУ КЛЮЧА (решение геймдизайнера 15.10, q-skelet-po-vesu). Форма-ключ двигает только вершины: в
/// покое тело — волк, а кости остаются человеческими, и при шаге нога гнулась бы в человеческом голеностопе у самой
/// земли (колено 0.50 м, голеностоп 0.085), а не в изломе лапы (0.55 и 0.27). Поэтому кости тела ДО ПРИВЯЗКИ меша
/// сдвигаются к суставам цели (`SpeciesSO.keyJoints`, поставка модельной линии) на вес ключа. Привязка (`bindposes`)
/// снимается уже со сдвинутых костей, так что в покое тело то же, а сгибается оно суставами цели.
///     Сдвиг — сверху вниз по иерархии: поза каждой кости задаётся в системе тела (смешение исходной и целевой), и
/// дети, которых в файле нет (гнёзда органов), едут за своей костью как есть.</summary>
public static class KeySkeleton
{
    public static int Apply(Transform container, Transform skeleton, SpeciesSO species, IReadOnlyDictionary<string, float> keys)
    {
        if (skeleton == null || species?.keyJoints == null || species.keyJoints.Length == 0 || keys == null) return 0;
        var (key, w) = keys.Where(kv => kv.Value > 1e-4f).Select(kv => (kv.Key, Mathf.Clamp01(kv.Value))).FirstOrDefault();
        if (key == null) return 0;
        var joints = species.keyJoints.Where(j => j != null && j.key == key).GroupBy(j => j.bone).ToDictionary(g => g.Key, g => g.First());
        if (joints.Count == 0) return 0;

        // исходные позы — в системе тела, ДО любых сдвигов (дальше родители двигаются и тянут детей)
        var bones = skeleton.GetComponentsInChildren<Transform>(true).Where(t => t != skeleton && joints.ContainsKey(t.name)).ToList();
        var from = bones.ToDictionary(t => t, t => (pos: container.InverseTransformPoint(t.position), rot: Quaternion.Inverse(container.rotation) * t.rotation));
        int moved = 0;
        foreach (var t in bones.OrderBy(Depth))
        {
            var j = joints[t.name];
            var (p0, r0) = from[t];
            var r1 = Quaternion.LookRotation(j.forward, j.along);
            t.position = container.TransformPoint(Vector3.Lerp(p0, j.head, w));
            t.rotation = container.rotation * Quaternion.Slerp(r0, r1, w);
            moved++;
        }
        return moved;

        int Depth(Transform t) { int d = 0; for (var p = t; p != null && p != skeleton; p = p.parent) d++; return d; }
    }
}
