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
        // СНОСИМ ВСЕ прошлые сборки. Гоча: `Object.Destroy` ОТЛОЖЕН до конца кадра, а `Recompute` за кадр
        // проходит не раз (установка органа + пересчёт родства + Refeed) — `Find` подбирал уже помеченный на
        // снос контейнер, «сносил» его повторно, и ЖИВОЙ прошлый оставался: старая часть висела «с лагом в один»
        // (поставил игломёт — хвост ещё на модели). Гасим сразу и переименовываем, чтобы Find не подобрал.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (c.name != Container) continue;
            c.name = Container + "~dead";
            c.gameObject.SetActive(false);
            Object.Destroy(c.gameObject);
        }
        if (chassis == null || chassis.skeleton == null || chassis.skeleton.Length == 0 || wornOrgans == null) return;

        // орган на part (шасси-фёрст: первый в списке — родные раньше химерных); невидимые (пустой visualPart) не в счёт
        var organByPart = new Dictionary<string, Organ>();
        foreach (var o in wornOrgans)
            if (o != null && !string.IsNullOrEmpty(o.visualPart) && !organByPart.ContainsKey(o.visualPart))
                organByPart[o.visualPart] = o;

        // ВЫСОТЫ ЯКОРЕЙ ЗАДАНЫ ОТ ЗЕМЛИ, а корень объекта у всех разный: у волка он на земле, у игрока — ЦЕНТР
        // капсулы (низ на −1). Сдвигаем контейнер к НИЗУ CharacterController, иначе тело игрока висит в метре
        // над землёй. Так одни и те же данные годятся любому носителю (тот же приём был в статичной PlayerModel)
        float footY = root.TryGetComponent<CharacterController>(out var cc) ? cc.center.y - cc.height * 0.5f : 0f;

        var container = new GameObject(Container);
        container.transform.SetParent(root, false);
        container.transform.localPosition = Vector3.up * footY;
        container.transform.localRotation = Quaternion.identity;

        // КАЖДЫЙ якорь = часть: орган своего part (деталь) ЛИБО базовый куб (голая тушка шасси).
        // Парный якорь (mirrorX) даёт ДВЕ части зеркально — 4 лапы/2 уха одной записью данных
        foreach (var anchor in chassis.skeleton)
        {
            if (anchor == null || string.IsNullOrEmpty(anchor.part)) continue;
            organByPart.TryGetValue(anchor.part, out var organ);
            if (organ == null && anchor.organOnly) continue; // гнездо под графт: без органа не рисуем (у человека нет хвоста)

            Piece(container.transform, anchor, organ, +1f);
            if (anchor.mirrorX) Piece(container.transform, anchor, organ, -1f);
        }
    }

    // одна куб-часть на якоре. side = +1/-1 — сторона парного якоря (зеркалим вынос по X и рыскание/крен,
    // тангаж общий: левая лапа не должна «смотреть» иначе правой)
    static void Piece(Transform parent, SkeletonAnchor anchor, Organ organ, float side)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (cube.TryGetComponent<Collider>(out var col)) Object.Destroy(col); // визуал без физики (коллайдер — у CharacterController)
        // ИМЯ = ЯКОРЬ (стабильный словарь), не орган: по именам частей работают ПЕРВОЕ ЛИЦО (прячет свою голову)
        // и ЭМОЦ-ТИНТ (красит морду-градусник). Имя органа менялось бы от сборки и ломало обе системы
        cube.name = anchor.part;

        Vector3 pos = anchor.localPos + (organ != null ? organ.visualOffset : Vector3.zero);
        Vector3 euler = anchor.baseEuler + (organ != null ? organ.visualEuler : Vector3.zero);
        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }

        var t = cube.transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = organ != null ? Vector3.Scale(anchor.baseSize, organ.visualScale) : anchor.baseSize;
    }
}
