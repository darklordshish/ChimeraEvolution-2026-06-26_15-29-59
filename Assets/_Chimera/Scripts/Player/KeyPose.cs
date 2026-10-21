using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ПОЗА СТУПЕНИ (решение 21.10, развилка модельной линии «Б»; спека двух слоёв: поза — глобальный слой; консилиум
/// формы: «длины и поза — в костях, в ключе только масса»). Форма-ключ несёт тело цели В ПОКОЕ шасси, а сутулость, вынос
/// головы и согнутые колени цели — это поворот костей. Поэтому после привязки меша кости поворачиваются от покоя шасси к позе
/// цели (`SpeciesSO.keyPose`, поставка `klyuchi/<вид>-<цель>-poza.json`) на вес ключа — сферически, и кожа гнётся за ними.
///     Поворот кости — её мировой сдвиг от покоя ШАССИ (рамки до суставов цели) к позе: D = поза · покой⁻¹. На вес w кость
/// встаёт в slerp(1, D, w) · (её покой после суставов цели) — длины остаются от суставов, поза — от файла. Сверху вниз:
/// дети едут за родителем, свою рамку ставят сами. Корень ещё и сдвигается (таз цели ниже и сзади) — лерп тем же весом.
/// При весе 0 тело ровно в покое — сторож `KeyPoseTests`.</summary>
public static class KeyPose
{
    /// <summary>Рамки костей в системе тела — снимать ДО суставов цели: от них считается поворот позы.</summary>
    public static Dictionary<string, (Vector3 pos, Quaternion rot)> Rest(Transform container, Transform skeleton)
    {
        var res = new Dictionary<string, (Vector3, Quaternion)>();
        if (skeleton == null) return res;
        foreach (var t in skeleton.GetComponentsInChildren<Transform>(true))
            if (t != skeleton && !res.ContainsKey(t.name))
                res[t.name] = (container.InverseTransformPoint(t.position), Quaternion.Inverse(container.rotation) * t.rotation);
        return res;
    }

    public static int Apply(Transform container, Transform skeleton, SpeciesSO species, IReadOnlyDictionary<string, float> keys,
                            Dictionary<string, (Vector3 pos, Quaternion rot)> chassisRest)
    {
        if (skeleton == null || species?.keyPose == null || species.keyPose.Length == 0 || keys == null || chassisRest == null) return 0;
        var (key, w) = keys.Where(kv => kv.Value > 1e-4f).Select(kv => (kv.Key, Mathf.Clamp01(kv.Value))).FirstOrDefault();
        if (key == null) return 0;
        var pose = species.keyPose.Where(j => j != null && j.key == key).GroupBy(j => j.bone).ToDictionary(g => g.Key, g => g.First());
        if (pose.Count == 0) return 0;

        var bones = skeleton.GetComponentsInChildren<Transform>(true)
                            .Where(t => t != skeleton && pose.ContainsKey(t.name) && chassisRest.ContainsKey(t.name)).ToList();
        // покой ПОСЛЕ суставов цели — снимается до поворотов: родитель повернётся и потянет детей
        var now = bones.ToDictionary(t => t, t => (pos: container.InverseTransformPoint(t.position), rot: Quaternion.Inverse(container.rotation) * t.rotation));
        int moved = 0;
        foreach (var t in bones.OrderBy(Depth))
        {
            var j = pose[t.name];
            var (k2Pos, k2Rot) = chassisRest[t.name];
            var d = Quaternion.LookRotation(j.forward, j.along) * Quaternion.Inverse(k2Rot);
            t.rotation = container.rotation * Quaternion.Slerp(Quaternion.identity, d, w) * now[t].rot;
            if (IsRoot(t)) t.position = container.TransformPoint(now[t].pos + (j.head - k2Pos) * w);
            moved++;
        }
        return moved;

        int Depth(Transform t) { int n = 0; for (var p = t; p != null && p != skeleton; p = p.parent) n++; return n; }
        bool IsRoot(Transform t) { for (var p = t.parent; p != null && p != skeleton; p = p.parent) if (pose.ContainsKey(p.name)) return false; return true; }
    }
}
