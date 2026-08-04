using System.Collections.Generic;
using UnityEngine;

/// <summary>МОРФОЛОГИЯ (ось 2): собирает КУБ-МОДЕЛЬ тела из данных — ОДНА система (без статичного BuildBlocky,
/// потому нет дубля). База: куб на КАЖДОМ СОКЕТЕ шасси (голая тушка). Орган ЗАМЕНЯЕТ куб СВОЕГО сокета
/// деталью — место следует из `Organ.slot`, отдельного адреса нет (единый источник правды, спека сокет-плана).
/// Пересобирается при смене состава. Эмерджентность: волчья Пасть на ЧЕЛОВЕЧЬЕМ сокете «Пасть» = морда
/// вервольфа. Кубы грубо, без подгонки.</summary>
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
        if (chassis == null || chassis.sockets == null || chassis.sockets.Length == 0 || wornOrgans == null) return;

        // орган на СОКЕТ по его `slot` — место следует из механики, отдельного поля-адреса нет.
        // ПЕРВИЧЕН орган РОДНОГО слота шасси (он раньше в списке), химерный — вторичный: на общем сокете
        // виден первичный. Слияние дизайна двух органов на одном месте — отдельная фича (сокет морфный)
        var organBySocket = new Dictionary<string, Organ>();
        foreach (var o in wornOrgans)
            if (o != null && !string.IsNullOrEmpty(o.slot) && !organBySocket.ContainsKey(o.slot))
                organBySocket[o.slot] = o;

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
        foreach (var socket in chassis.sockets)
        {
            if (socket == null || string.IsNullOrEmpty(socket.name) || socket.hidden) continue; // внутренний (Сердце/Чутьё) — места на теле нет
            organBySocket.TryGetValue(socket.name, out var organ);
            if (organ == null && socket.graft) continue; // закрытое место: без органа не рисуем (у человека нет хвоста)

            Piece(container.transform, socket, organ, +1f);
            if (socket.mirrorX) Piece(container.transform, socket, organ, -1f);
        }
    }

    // одна куб-часть на якоре. side = +1/-1 — сторона парного якоря (зеркалим вынос по X и рыскание/крен,
    // тангаж общий: левая лапа не должна «смотреть» иначе правой)
    static void Piece(Transform parent, BodySocket socket, Organ organ, float side)
    {
        Vector3 pos = socket.localPos + (organ != null ? organ.visualOffset : Vector3.zero);
        Vector3 euler = socket.baseEuler + (organ != null ? organ.visualEuler : Vector3.zero);
        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }
        Quaternion rot = Quaternion.Euler(euler);
        Vector3 size = organ != null ? Vector3.Scale(socket.baseSize, organ.visualScale) : socket.baseSize;

        // СЕГМЕНТНАЯ ЦЕПЬ (змеиный хвост, отростки рога): ось — ДЛИННАЯ сторона места. Хвост лежит вдоль Z
        // → тянется назад; рог вытянут по Y → растёт вверх. Отдельного поля-направления не нужно
        int n = Mathf.Max(1, organ != null ? organ.visualSegments : 1);
        float taper = organ != null && organ.visualTaper > 0f ? organ.visualTaper : 0.85f;
        Vector3 dir = size.z >= size.x && size.z >= size.y ? Vector3.back
                    : size.y >= size.x ? Vector3.up : Vector3.right;
        float axisLen = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))));

        float travel = 0f, prevLen = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = Mathf.Pow(taper, i);
            float len = axisLen * k;
            if (i > 0) travel += (prevLen + len) * 0.5f; // встык, с сужением
            prevLen = len;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (cube.TryGetComponent<Collider>(out var col)) Object.Destroy(col); // визуал без физики (коллайдер — у CharacterController)
            // ИМЯ = СОКЕТ (стабильный словарь), не орган: по именам частей работают ПЕРВОЕ ЛИЦО (прячет свою
            // голову) и ЭМОЦ-ТИНТ (красит морду-градусник). Имя органа менялось бы от сборки и ломало обе системы
            cube.name = socket.name;

            var t = cube.transform;
            t.SetParent(parent, false);
            t.localPosition = pos + rot * (dir * travel);
            t.localRotation = rot;
            t.localScale = size * k;
        }
    }
}
