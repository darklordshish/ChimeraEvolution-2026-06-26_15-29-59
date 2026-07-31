using System.Collections.Generic;
using UnityEngine;

/// <summary>МОРФОЛОГИЯ (ось 2): собирает КУБ-МОДЕЛЬ тела из данных — часть каждого видимого органа на якоре
/// ШАССИ. Скелет от шасси (поза/рост), форма от органа. Пересобирается при смене состава (эволюция). Кубы грубо,
/// без подгонки. Эмерджентность: волчья морда (орган) на ЧЕЛОВЕЧЬЕМ якоре-голове = морда вервольфа.</summary>
public static class MorphBuilder
{
    const string Container = "Morph";

    /// <summary>Пересобрать куб-модель под `root` по надетым органам и скелету `chassis`.
    /// wornOrgans — надетые органы В ПОРЯДКЕ приоритета (РОДНЫЕ раньше химерных → шасси-фёрст: первый занявший
    /// якорь побеждает). Возвращает созданные рендереры (для тинта — вызывающий добавит их в свой список).</summary>
    public static List<Renderer> Build(Transform root, SpeciesSO chassis, IReadOnlyList<Organ> wornOrgans)
    {
        var built = new List<Renderer>();

        // снести прошлую сборку
        var old = root.Find(Container);
        if (old != null) Object.Destroy(old.gameObject);
        if (chassis == null || chassis.skeleton == null || chassis.skeleton.Length == 0 || wornOrgans == null) return built;

        var container = new GameObject(Container);
        container.transform.SetParent(root, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        var usedParts = new HashSet<string>(); // шасси-фёрст: один part — одна часть (первый в списке побеждает)
        foreach (var organ in wornOrgans)
        {
            if (organ == null || string.IsNullOrEmpty(organ.visualPart)) continue; // невидимый орган (Сердце/Чутьё)
            if (!usedParts.Add(organ.visualPart)) continue;                          // part уже занят родным → химерный дубль скрыт
            var anchor = FindAnchor(chassis, organ.visualPart);
            if (anchor == null) continue;                                            // нет якоря под part у шасси → часть не рисуется (форм-лимит)

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (cube.TryGetComponent<Collider>(out var col)) Object.Destroy(col);     // визуал без физики (коллайдер — у CharacterController)
            cube.name = organ.organName;
            var t = cube.transform;
            t.SetParent(container.transform, false);
            t.localPosition = anchor.localPos + organ.visualOffset;
            t.localRotation = Quaternion.Euler(organ.visualEuler);
            t.localScale = Vector3.Scale(anchor.baseSize, organ.visualScale);
            if (cube.TryGetComponent<Renderer>(out var r)) built.Add(r);
        }
        return built;
    }

    static SkeletonAnchor FindAnchor(SpeciesSO chassis, string part)
    {
        foreach (var a in chassis.skeleton)
            if (a != null && a.part == part) return a;
        return null;
    }
}
