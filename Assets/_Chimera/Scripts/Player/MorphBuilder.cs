using System.Collections.Generic;
using UnityEngine;

/// <summary>МОРФОЛОГИЯ (ось 2): собирает КУБ-МОДЕЛЬ тела из данных — ОДНА система (без статичного BuildBlocky,
/// потому нет дубля). База: куб на КАЖДОМ якоре скелета шасси (голая тушка). Орган с visualPart ЗАМЕНЯЕТ куб
/// своего якоря деталью (форма органа). Пересобирается при смене состава. Эмерджентность: волчья морда (орган
/// Пасть, visualPart=голова) на ЧЕЛОВЕЧЬЕМ якоре-голове = морда вервольфа. Кубы грубо, без подгонки.</summary>
public static class MorphBuilder
{
    const string Container = "Morph";

    /// <summary>Пересобрать куб-модель под `root` по надетым органам и скелету `chassis`.
    /// wornOrgans — надетые органы В ПОРЯДКЕ приоритета (РОДНЫЕ раньше химерных → шасси-фёрст: первый занявший
    /// part побеждает). worn == null → только СНОСИТ старый Morph (для игрока: остаётся его PlayerModel).</summary>
    public static void Build(Transform root, SpeciesSO chassis, IReadOnlyList<Organ> wornOrgans)
    {
        var old = root.Find(Container);
        if (old != null) Object.Destroy(old.gameObject);
        if (chassis == null || chassis.skeleton == null || chassis.skeleton.Length == 0 || wornOrgans == null) return;

        // орган на part (шасси-фёрст: первый в списке — родные раньше химерных); невидимые (пустой visualPart) не в счёт
        var organByPart = new Dictionary<string, Organ>();
        foreach (var o in wornOrgans)
            if (o != null && !string.IsNullOrEmpty(o.visualPart) && !organByPart.ContainsKey(o.visualPart))
                organByPart[o.visualPart] = o;

        var container = new GameObject(Container);
        container.transform.SetParent(root, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        // КАЖДЫЙ якорь = куб: орган своего part (деталь) ЛИБО базовый куб (голая тушка шасси)
        foreach (var anchor in chassis.skeleton)
        {
            if (anchor == null || string.IsNullOrEmpty(anchor.part)) continue;
            organByPart.TryGetValue(anchor.part, out var organ);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (cube.TryGetComponent<Collider>(out var col)) Object.Destroy(col); // визуал без физики (коллайдер — у CharacterController)
            cube.name = organ != null ? organ.organName : anchor.part;
            var t = cube.transform;
            t.SetParent(container.transform, false);
            t.localPosition = anchor.localPos + (organ != null ? organ.visualOffset : Vector3.zero);
            t.localRotation = Quaternion.Euler(organ != null ? organ.visualEuler : Vector3.zero);
            t.localScale = organ != null ? Vector3.Scale(anchor.baseSize, organ.visualScale) : anchor.baseSize;
        }
    }
}
