using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ПОЗА СТУПЕНИ (решение 21.10, развилка модельной линии «Б»; спека двух слоёв: поза — глобальный слой; консилиум
/// формы: «длины и поза — в костях, в ключе только масса»). Форма-ключ несёт тело цели В ПОКОЕ шасси, а сутулость, вынос
/// головы и согнутые колени цели — это поворот костей. Поэтому после привязки меша кости поворачиваются от покоя к позе
/// цели (`SpeciesSO.keyPose`, поставка `klyuchi/<вид>-<цель>-poza.json`) на вес ключа — сферически, и кожа гнётся за ними.
///     Рамка кости — slerp(покой, поза, w): покой — после суставов цели (`KeySkeleton`), поза — рамка из файла. Сверху вниз:
/// поворот родителя тянет детей (их головы встают туда, куда их ведёт цепь), свою рамку ребёнок ставит сам. Корень ещё и
/// сдвигается (таз цели ниже и сзади): лерп от покоя к head позы. Суставы цели в покое (`sustavy`) модельная линия выводит
/// этой же цепью, поэтому при весе 1 все суставы встают ровно в позу файла.
///     Прежде поворот считался разностью «поза · покой шасси⁻¹» и ставился поверх покоя суставов. Это требует, чтобы рамки
/// покоя шасси в графе и в риге модельной линии совпадали. У бедра и шеи они расходились на 17–18°, и стопа уезжала на
/// 26 см (21.10). Рамка позы напрямую от рамок покоя шасси не зависит. Сторож — `KeyPoseTests`.</summary>
public static class KeyPose
{
    public static int Apply(Transform container, Transform skeleton, SpeciesSO species, IReadOnlyDictionary<string, float> keys)
    {
        if (skeleton == null || species?.keyPose == null || species.keyPose.Length == 0 || keys == null) return 0;
        var (key, w) = keys.Where(kv => kv.Value > 1e-4f).Select(kv => (kv.Key, Mathf.Clamp01(kv.Value))).FirstOrDefault();
        if (key == null) return 0;
        var pose = species.keyPose.Where(j => j != null && j.key == key).GroupBy(j => j.bone).ToDictionary(g => g.Key, g => g.First());
        if (pose.Count == 0) return 0;

        var bones = skeleton.GetComponentsInChildren<Transform>(true).Where(t => t != skeleton && pose.ContainsKey(t.name)).ToList();
        // покой (после суставов цели) — снимается до поворотов: родитель повернётся и потянет детей
        var now = bones.ToDictionary(t => t, t => (pos: container.InverseTransformPoint(t.position), rot: Quaternion.Inverse(container.rotation) * t.rotation));
        int moved = 0;
        foreach (var t in bones.OrderBy(Depth))
        {
            var j = pose[t.name];
            t.rotation = container.rotation * Quaternion.Slerp(now[t].rot, Quaternion.LookRotation(j.forward, j.along), w);
            if (IsRoot(t)) t.position = container.TransformPoint(Vector3.Lerp(now[t].pos, j.head, w));   // в полной позе корень — точка позы
            moved++;
        }
        return moved;

        int Depth(Transform t) { int n = 0; for (var p = t; p != null && p != skeleton; p = p.parent) n++; return n; }
        bool IsRoot(Transform t) { for (var p = t.parent; p != null && p != skeleton; p = p.parent) if (pose.ContainsKey(p.name)) return false; return true; }
    }
}
