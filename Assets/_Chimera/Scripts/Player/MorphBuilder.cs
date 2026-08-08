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

        // ГРАФ ХРЕБТА: место с `parent` не хранит своих координат — считаем их от родителя и НАСЛЕДУЕМ
        // его поворот. Поэтому наклон шеи тянет за собой голову, морду, уши и рога, а не оставляет их
        // висеть на прежней абсолютной высоте (спека 2026-08-05)
        var byName = new Dictionary<string, BodySocket>();
        foreach (var s in chassis.sockets)
            if (s != null && !string.IsNullOrEmpty(s.name)) byName[s.name] = s;
        var placed = new Dictionary<string, (Vector3 pos, Quaternion rot)>();

        // КАЖДЫЙ якорь = часть: орган своего part (деталь) ЛИБО базовый куб (голая тушка шасси).
        // Парный якорь (mirrorX) даёт ДВЕ части зеркально — 4 лапы/2 уха одной записью данных
        foreach (var socket in chassis.sockets)
        {
            if (socket == null || string.IsNullOrEmpty(socket.name)) continue;
            if (socket.hidden) continue;        // служебное (хребет) — своей формы нет и быть не может
            if (socket.codeDriven) continue;    // [ANIM] позицию места каждый кадр считает своя система (цепь змеи) — морф не вмешивается
            organBySocket.TryGetValue(socket.name, out var organ);
            // БЕЗ ОРГАНА НЕ РИСУЕМ в двух случаях: место закрыто у этого шасси (у человека нет хвоста) либо
            // лежит ВНУТРИ тела и проступает только формой органа (Сердце → грудная клетка)
            if (organ == null && (socket.graft || socket.inner)) continue;
            // ВНУТРЕННЕЕ МЕСТО ВИДНО РОВНО ТОГДА, КОГДА ЕСТЬ ЧТО ПОКАЗАТЬ. Ни у органа, ни у места нет
            // формы — детали нет, и «голую тушку» кубом сюда подставлять нельзя (Чутьё вылезло бы ящиком
            // из груди). Раньше это решал флаг `hidden` на каждом виде: нюх скрывали руками, и, реши мы
            // однажды дать ему форму (термо-ямки, вибриссы), пришлось бы править флаги у всех пяти видов
            if (socket.inner && (organ.visualParts == null || organ.visualParts.Length == 0)
                             && (socket.parts == null || socket.parts.Length == 0)) continue;

            var (pos, rot) = Place(socket, byName, placed, 0);
            Piece(container.transform, socket, organ, +1f, pos, rot);
            if (socket.mirrorX) Piece(container.transform, socket, organ, -1f, pos, rot);

            // КОСТЬ ОТ ОРГАНА + МЯСО ОТ ШАССИ. У внутреннего места форма органа НЕ вытесняет форму места:
            // Сердце даёт грудную КЛЕТКУ, а место — мышцы поверх неё. Мышцы заданы в долях грудной коробки,
            // поэтому перестраиваются вместе с ней: поставил волчье сердце — коробка стала глубокой и узкой,
            // пекторали с трапецией поехали следом сами. Иначе кость тонула в мышцах, живущих отдельно
            // (у прочих мест правило прежнее — орган ЗАМЕЩАЕТ: волчья морда встаёт вместо человечьей)
            if (socket.inner && organ != null && organ.visualParts != null && organ.visualParts.Length > 0
                             && socket.parts != null && socket.parts.Length > 0)
            {
                Piece(container.transform, socket, null, +1f, pos, rot);
                if (socket.mirrorX) Piece(container.transform, socket, null, -1f, pos, rot);
            }
        }
    }

    /// <summary>Позиция и поворот места. Корень (без `parent`) стоит по своим `localPos`/`baseEuler`;
    /// у ребёнка обе величины считаются ОТ РОДИТЕЛЯ — отсюда невозможность разъехаться.
    /// `depth` страхует от цикла в данных (его же ловит `ValidateSockets`, но билдер не должен зависать).</summary>
    static (Vector3, Quaternion) Place(BodySocket s, Dictionary<string, BodySocket> byName,
                                       Dictionary<string, (Vector3, Quaternion)> placed, int depth)
    {
        if (placed.TryGetValue(s.name, out var done)) return done;

        var rot = Quaternion.Euler(s.baseEuler);
        var pos = s.localPos;

        if (depth < 16 && !string.IsNullOrEmpty(s.parent) && byName.TryGetValue(s.parent, out var par) && par != s)
        {
            var (ppos, prot) = Place(par, byName, placed, depth + 1);
            // СТЫК на ДЛИННОЙ оси родителя: `attach` = доля вдоль неё (0 — начало, 1 — конец).
            // Смещение — в КАЛИБРАХ родителя, поэтому переживает масштабирование вида
            var b = par.baseSize;
            Vector3 axis = b.z >= b.x && b.z >= b.y ? Vector3.forward : (b.y >= b.x ? Vector3.up : Vector3.right);
            float len = Mathf.Abs(Vector3.Dot(b, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));

            int links = Mathf.Max(1, par.chain);
            if (links > 1)
            {
                // РОДИТЕЛЬ — ЦЕПЬ (тело змеи): его «длина» это вся вереница, а `localPos` — центр ПЕРВОГО
                // звена, не середина места. Считаем стык от начала цепи вдоль её роста, иначе хвост сел бы
                // в середину туловища. attach 1 = начало (у головы), 0 = конец (кончик)
                Vector3 grow = ChainDir(b);
                pos = ppos + prot * (grow * ((1f - s.attach) * ChainLength(par, len) - len * 0.5f)
                                     + Vector3.Scale(s.attachOffset, b));
            }
            else
                pos = ppos + prot * (axis * ((s.attach - 0.5f) * len) + Vector3.Scale(s.attachOffset, b));
            rot = prot * rot;   // поворот НАСЛЕДУЕТСЯ: наклонили родителя — вся ветка поехала
        }

        placed[s.name] = (pos, rot);
        return (pos, rot);
    }

    // одна куб-часть на якоре. side = +1/-1 — сторона парного якоря (зеркалим вынос по X и рыскание/крен,
    // тангаж общий: левая лапа не должна «смотреть» иначе правой)
    static void Piece(Transform parent, BodySocket socket, Organ organ, float side, Vector3 basePos, Quaternion baseRot)
    {
        // basePos/baseRot приходят из графа (уже с учётом родителя) — своих координат у места может и не быть
        Vector3 pos = basePos + (organ != null ? organ.visualOffset : Vector3.zero);
        Vector3 euler = (baseRot * Quaternion.Euler(organ != null ? organ.visualEuler : Vector3.zero)).eulerAngles;
        Vector3 size = organ != null ? Vector3.Scale(socket.baseSize, organ.visualScale) : socket.baseSize;

        // СОСТАВНАЯ ФОРМА («лего 8+»): орган из нескольких частей — рога = стебель+лопасть+отростки,
        // иглы = щетина. Смещения частей заданы В КАЛИБРАХ МЕСТА, поэтому одна форма годится и мелкому
        // ежу, и крупному лосю: она ужимается/разрастается вместе с местом
        var parts = organ != null && organ.visualParts != null && organ.visualParts.Length > 0 ? organ.visualParts : null;

        // ЦЕПЬ ЗВЕНЬЕВ. Сколько — говорит ОРГАН (привитый змеиный хвост сегментен на любом носителе),
        // иначе МЕСТО (тело самой змеи — `chain`). Раньше цепь была ОТДЕЛЬНОЙ веткой и работала только
        // там, где формы нет: место не умело быть цепью вовсе. Теперь это внешний цикл, и каждое звено
        // несёт свою форму — одним кодом строятся и тело змеи, и привитый ей же хвост на чужом шасси
        bool organChain = organ != null && organ.visualSegments > 1;
        int links = organChain ? organ.visualSegments : Mathf.Max(1, socket.chain);
        float chainTaper = organChain ? (organ.visualTaper > 0f ? organ.visualTaper : 0.85f)
                                      : (socket.chainTaper > 0f ? socket.chainTaper : 0.94f);

        // ФОРМА МЕСТА — силуэт шасси. Торс/голова/шея органа не имеют вовсе, и без своей формы туша обречена
        // быть бруском: волк неотличим от ящика. Место собирается из кусков ТЕМ ЖЕ механизмом, что и орган.
        // Приоритет: форма ОРГАНА → форма МЕСТА. НО сегментный ОРГАН форму места не берёт: змеиный хвост,
        // привитый волку, иначе повторил бы волчий хвост трижды — три шарнира подряд
        if (parts == null && !organChain) parts = socket.parts;

        if (parts != null && parts.Length > 0)
        {
            // ПОВОРОТ МЕСТА ВРАЩАЕТ ФОРМУ ЦЕЛИКОМ — и углы частей, И их смещения (иначе доворот крутил бы
            // каждую деталь по отдельности, а расстановка оставалась бы прежней)
            Quaternion socketRot = Quaternion.Euler(euler);

            // АВТО-ОРИЕНТАЦИЯ ПО ТЕЛУ: части описаны в каноничном кадре (X поперёк, Y НАРУЖУ, Z ВДОЛЬ тела) и
            // раскладываются на РЕАЛЬНЫЕ оси места: вдоль = длинная сторона, наружу = короткая. Ежиный торс
            // ЛЕЖИТ → щетина встаёт на спину; человечий СТОИТ → та же щетина ложится гребнем вдоль позвоночника
            // со спины, а не втыкается в шею. Одна форма, разные тела — без спец-полей у видов
            bool align = organ != null && organ.visualAlignToBody;
            int across = 0, outward = 1, along = 2;
            Quaternion frameRot = Quaternion.identity;
            if (align)
            {
                var b = socket.baseSize;
                along = b.x >= b.y && b.x >= b.z ? 0 : (b.y >= b.z ? 1 : 2);
                outward = b.x <= b.y && b.x <= b.z ? 0 : (b.y <= b.z ? 1 : 2);
                if (outward == along) outward = (along + 1) % 3;
                across = 3 - along - outward;
                // ЗНАК «наружу»: если короткая ось совпала с осью ВЗГЛЯДА (Z), наружу = НАЗАД, иначе вперёд лезло бы
                // в грудь (у человека щетина выстраивалась рядком по бокам — «капитолийская волчица»). У четвероногих
                // короткая ось вертикальная, и «наружу» естественно вверх = спина
                Vector3 outDir = outward == 2 ? Vector3.back : Axis(outward);
                frameRot = Quaternion.LookRotation(Axis(along), outDir); // канон: Z→вдоль тела, Y→наружу
            }

            // ВСЁ ОСТАЁТСЯ В КАНОНИЧНОМ КАДРЕ, поворот — ОДИН, в самом конце. (Перекладывать компоненты И
            // вращать нельзя: форма развернётся дважды — из ежиной щетины выходит крест из плит.)
            Vector3 canonBase = align ? Pick(socket.baseSize, across, outward, along) : socket.baseSize;
            Vector3 canonSize = align ? Pick(size, across, outward, along) : size;
            Quaternion place = socketRot * frameRot;

            Vector3 grow = ChainDir(socket.baseSize);
            float linkLen = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(grow.x), Mathf.Abs(grow.y), Mathf.Abs(grow.z))));
            float run = 0f, prev = 0f;
            for (int i = 0; i < links; i++)
            {
                float k = Mathf.Pow(chainTaper, i);       // links == 1 → k = 1, run = 0: ровно прежнее поведение
                if (i > 0) run += (prev + linkLen * k) * 0.5f;   // встык, с сужением
                prev = linkLen * k;
                Vector3 linkPos = pos + socketRot * (grow * run);
                foreach (var pt in parts)
                {
                    if (pt == null) continue;
                    Spawn(parent, socket.name,
                          linkPos + place * Vector3.Scale(pt.offset, canonBase * k),
                          (place * Quaternion.Euler(pt.euler)).eulerAngles,
                          Vector3.Scale(canonSize, pt.scale) * k, side, pt.shape);
                }
            }
            return;
        }

        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }
        Quaternion rot = Quaternion.Euler(euler);

        // ЦЕПЬ БЕЗ ФОРМЫ — голые звенья (привитый хвост на чужом шасси: места своей формы не имеет)
        Vector3 dir = ChainDir(socket.baseSize);
        float axisLen = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))));

        float travel = 0f, prevLen = 0f;
        for (int i = 0; i < links; i++)
        {
            float k = Mathf.Pow(chainTaper, i);
            float len = axisLen * k;
            if (i > 0) travel += (prevLen + len) * 0.5f; // встык, с сужением
            prevLen = len;
            Spawn(parent, socket.name, pos + rot * (dir * travel), euler, size * k, 1f); // зеркалирование уже учтено выше
        }
    }

    /// <summary>КУДА РАСТЁТ ЦЕПЬ: по длинной оси места. Хвост/тело лежат вдоль Z → тянутся НАЗАД;
    /// рог вытянут по Y → растёт ВВЕРХ. Отдельного поля-направления не нужно — его говорит габарит.
    /// Считаем по `baseSize`, а не по итоговому размеру: та же ось, что берёт `Place` для стыка детей,
    /// иначе хвост цеплялся бы к одному концу, а рос в другой.</summary>
    static Vector3 ChainDir(Vector3 b) => b.z >= b.x && b.z >= b.y ? Vector3.back
                                        : b.y >= b.x ? Vector3.up : Vector3.right;

    /// <summary>Полная длина цепи со схождением на конус: геометрическая прогрессия, а не длина × число
    /// звеньев. Нужна `Place`, чтобы ребёнок сел на КОНЕЦ вереницы (хвост змеи — за туловищем).</summary>
    static float ChainLength(BodySocket s, float linkLen)
    {
        int n = Mathf.Max(1, s.chain);
        float t = s.chainTaper > 0f ? s.chainTaper : 0.94f;
        return Mathf.Approximately(t, 1f) ? linkLen * n : linkLen * (1f - Mathf.Pow(t, n)) / (1f - t);
    }

    static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    static Vector3 Pick(Vector3 v, int a, int b, int c) => new(v[a], v[b], v[c]);          // взять компоненты в порядке осей формы

    // одна деталь. side = -1 зеркалит вынос по X и рыскание/крен (тангаж общий: левая лапа не «смотрит» иначе правой)
    static void Spawn(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 size, float side,
                      PartShape shape = PartShape.Cube)
    {
        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }

        // КАПСУЛА И ЦИЛИНДР у Unity ВДВОЕ ВЫШЕ куба при том же масштабе (высота примитива 2, диаметр 1).
        // Делим Y, чтобы `scale` во всех данных значил ОДНО И ТО ЖЕ — габарит куска, а не масштаб примитива
        if (shape == PartShape.Capsule || shape == PartShape.Cylinder) size.y *= 0.5f;

        var cube = GameObject.CreatePrimitive(shape == PartShape.Sphere ? PrimitiveType.Sphere
                                            : shape == PartShape.Capsule ? PrimitiveType.Capsule
                                            : shape == PartShape.Cylinder ? PrimitiveType.Cylinder
                                            : PrimitiveType.Cube);
        if (cube.TryGetComponent<Collider>(out var col)) Object.Destroy(col); // визуал без физики (коллайдер — у CharacterController)
        // ИМЯ = СОКЕТ (стабильный словарь), не орган: по именам частей работают ПЕРВОЕ ЛИЦО (прячет свою
        // голову) и ЭМОЦ-ТИНТ (красит морду-градусник). Имя органа менялось бы от сборки и ломало обе системы
        cube.name = name;

        var t = cube.transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = size;
    }
}
