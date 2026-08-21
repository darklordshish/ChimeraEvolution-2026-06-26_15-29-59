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
    public static void Build(Transform root, SpeciesSO chassis, IReadOnlyList<Organ> wornOrgans,
                             BodySocket[] plan = null)
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
            Kill(c.gameObject);
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

        // СКЕЛЕТ ЗАБИРАЕТ СВОИ МЕСТА (спека 2026-08-18). Кость называет место, форму которого строит она;
        // старый сокет-план это место пропускает, иначе кость и место лепят одно и то же вдвоём. Само место
        // остаётся в данных — его имя есть `Organ.slot`, то есть механика слота не трогается вовсе.
        // Так прототип живёт РЯДОМ со старой системой: у кого `bones` пуст (четыре вида из пяти), сборка
        // идёт до микрона как прежде
        var boneSockets = new HashSet<string>();
        if (chassis.skeletonHides != null)
            foreach (var h in chassis.skeletonHides)
                if (!string.IsNullOrEmpty(h)) boneSockets.Add(h);

        var byBone = new Dictionary<string, Bone>();
        var bonePos = new Dictionary<string, (Vector3, Quaternion)>();
        if (chassis.bones != null && chassis.bones.Length > 0)
        {
            foreach (var b in chassis.bones)
                if (b != null && !string.IsNullOrEmpty(b.name)) byBone[b.name] = b;

            // ШКУРА ВМЕСТО ШАРОВ: один `SkinnedMeshRenderer` НА СЛОТ поверх общей иерархии костей.
            // Слот — единица химеризации, поэтому он же единица тела: графт перестраивает меш своего
            // модуля, имя рендерера совпадает с именем слота (контракт имён частей цел), телеграф может
            // подсветить ту часть, которой бьют. Цена шаров была известна заранее: 315 рендереров на
            // волка при ~160 тыс. вершин против 8 и ~2 тыс. здесь
            //     БЕЗ ПОПРАВКИ НА `footY`: контейнер уже сдвинут к низу капсулы, а кости живут в ЕГО
            // системе координат — той же, в какой меряет карта тел. Второй сдвиг поднял бы зверя над
            // землёй ровно на его рост, и это тот самый класс ошибок «мерь там же, где расставляешь»
            BoneMesher.Build(container.transform, chassis, PrimitiveMaterial());
        }

        // ГРАФ ХРЕБТА: место с `parent` не хранит своих координат — считаем их от родителя и НАСЛЕДУЕМ
        // его поворот. Поэтому наклон шеи тянет за собой голову, морду, уши и рога, а не оставляет их
        // висеть на прежней абсолютной высоте (спека 2026-08-05)
        // ПЛАН МЕСТ вместо прямого чтения шасси: `plan` — тот же сокет-план, но пропорции в нём может
        // пересчитать морфология по идентичности (спека 2026-08-14). Пусто = чистое шасси, то есть
        // сегодняшнее поведение до микрона. Топология и калибр в плане остаются шассийными всегда
        var sockets = plan ?? chassis.sockets;
        var byName = new Dictionary<string, BodySocket>();
        foreach (var s in sockets)
            if (s != null && !string.IsNullOrEmpty(s.name)) byName[s.name] = s;

        // ОСЬ ПРИКАЛЫВАЕТСЯ ПО ЧИСТОМУ ШАССИ И НЕ ЗАВИСИТ ОТ ПЛАНА. Длинная ось выбирается по максимальной
        // стороне, и это мина: у головы человека она Y (0.270), у волка Z (0.589) — при смешении пропорций
        // они пересекаются на весе ≈0.32, то есть первый же слабый графт развернул бы ВСЮ ветку головы.
        // Пасть уехала бы с лица на макушку, смещения ушли бы в другую ось, и всё это без единой ошибки.
        // Закон: ВЛИЯНИЕ МЕНЯЕТ ПРОПОРЦИИ, НО НИКОГДА ТОПОЛОГИЮ
        var pure = new Dictionary<string, BodySocket>();
        foreach (var s in chassis.sockets)
            if (s != null && !string.IsNullOrEmpty(s.name)) pure[s.name] = s;
        var axisOf = new Dictionary<string, Vector3>();
        foreach (var s in chassis.sockets)
            if (s != null && !string.IsNullOrEmpty(s.name)) axisOf[s.name] = LongAxis(SizeOf(s, pure, 0));
        var placed = new Dictionary<string, (Vector3 pos, Quaternion rot)>();
        // ЗВЕНЬЯ ПОСТРОЕННЫХ ЦЕПЕЙ — чтобы ребёнок цепи сел НА ЗВЕНО, а не рядом с ним (см. ниже)
        var chainLinks = new Dictionary<string, List<GameObject>>();

        // КАЖДЫЙ якорь = часть: орган своего part (деталь) ЛИБО базовый куб (голая тушка шасси).
        // Парный якорь (mirrorX) даёт ДВЕ части зеркально — 4 лапы/2 уха одной записью данных
        foreach (var socket in sockets)
        {
            if (socket == null || string.IsNullOrEmpty(socket.name)) continue;
            if (boneSockets.Contains(socket.name)) continue;   // форму этого места строит скелет
            // [ANIM] codeDriven НЕ пропускаем: морф СТРОИТ ФОРМУ и ставит звенья в стартовую позу, а дальше
            // позицию каждый кадр перезаписывает своя система (SnakeBodyChain ведёт цепь по пути головы).
            // Раньше здесь стоял continue — отсюда невидимая змея: место есть, а рисовать его было некому
            organBySocket.TryGetValue(socket.name, out var organ);
            // БЕЗ ОРГАНА НЕ РИСУЕМ в двух случаях: место закрыто у этого шасси (у человека нет хвоста) либо
            // лежит ВНУТРИ тела и проступает только формой органа (Сердце → грудная клетка)
            if (organ == null && (socket.graft || socket.inner)) continue;
            // ВНУТРЕННЕЕ МЕСТО ВИДНО РОВНО ТОГДА, КОГДА ЕСТЬ ЧТО ПОКАЗАТЬ. Ни у органа, ни у места нет
            // формы — детали нет, и «голую тушку» кубом сюда подставлять нельзя (Чутьё вылезло бы ящиком
            // из груди). Раньше это решал флаг `hidden` на каждом виде: нюх скрывали руками, и, реши мы
            // однажды дать ему форму (термо-ямки, вибриссы), пришлось бы править флаги у всех пяти видов.
            // Флага больше НЕТ ВОВСЕ: последним его носителем было фиктивное место `Тело` ежа, и с переездом
            // клубка на ноги оно ушло. Место без формы — фикция, и словарь мест это теперь утверждает
            // ...и части считаем ТЕ, ЧТО ПРЕДНАЗНАЧЕНЫ ЭТОМУ МЕСТУ: у Чутья теперь есть форма глаз, но она
            // адресована месту «глаза», а самому Чутью показывать по-прежнему нечего — позиции у него нет
            if (socket.inner && PickParts(organ.visualParts, socket.formRole) == null
                             && (socket.parts == null || socket.parts.Length == 0)) continue;

            // ФОРМА ИЗ ЧУЖОГО ОРГАНА: место на голове рисуется частями органа-источника с нужной ролью
            // (глаза/уши/нос/ямки — от Чутья). Нет источника или нет частей этой роли — место рисует свою
            OrganPart[] fromOther = null;
            if (!string.IsNullOrEmpty(socket.formFrom) && organBySocket.TryGetValue(socket.formFrom, out var src)
                                                       && src != null && src.visualParts != null)
            {
                var picked = new List<OrganPart>();
                foreach (var p in src.visualParts) if (p != null && p.role == socket.formRole) picked.Add(p);
                if (picked.Count > 0) fromOther = picked.ToArray();
            }

            var (pos, rot) = Place(socket, byName, placed, 0, axisOf, byBone, bonePos);
            var made = new List<GameObject>();
            float linkD = ChainDiameter(socket, byName, 0);
            Vector3 sz = SizeOf(socket, byName, 0);   // габарит: свой или доля родителя
            Piece(container.transform, socket, organ, +1f, pos, rot, made, linkD, fromOther, sz);
            if (socket.mirrorX) Piece(container.transform, socket, organ, -1f, pos, rot, made, linkD, fromOther, sz);

            // РЕБЁНОК ЦЕПИ СИДИТ НА ЗВЕНЕ. Место, висящее на цепном родителе, становится ПОТОМКОМ того звена,
            // на которое указывает `attach` (0 — последнее, у кончика). Иначе оно остаётся соседом звеньев, и
            // движок цепи обязан выгораживать его СПИСКОМ ИМЁН — как было с погремушкой: её перечисляли, чтобы
            // не растащить, и она всё равно рассыпалась. Теперь цепь её не двигает не потому, что мы её
            // вычеркнули, а потому что её там нет: звено едет — погремушка едет с ним, наследованием.
            // Правило общее, не про змею: так же сядет жало, шип или привитый игломёт на конце хвоста
            // ...но САМА ЦЕПЬ на звено не садится: она продолжение хребта, и ведёт её движок цепи, который
            // ищет звенья среди ПРЯМЫХ детей контейнера. Посади её внутрь — тело станет внуком последнего
            // звена шеи, хвост внуком тела, и змея поползёт одной шеей, волоча за собой жёсткий кусок
            if (socket.linkLength <= 0f && !string.IsNullOrEmpty(socket.parent)
                && chainLinks.TryGetValue(socket.parent, out var links) && links.Count > 0)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt((1f - socket.attach) * (links.Count - 1)), 0, links.Count - 1);
                foreach (var g in made) g.transform.SetParent(links[idx].transform, true);
            }
            if (socket.linkLength > 0f && made.Count > 0) chainLinks[socket.name] = made;

            // КОСТЬ ОТ ОРГАНА + МЯСО ОТ ШАССИ. У внутреннего места форма органа НЕ вытесняет форму места:
            // Сердце даёт грудную КЛЕТКУ, а место — мышцы поверх неё. Мышцы заданы в долях грудной коробки,
            // поэтому перестраиваются вместе с ней: поставил волчье сердце — коробка стала глубокой и узкой,
            // пекторали с трапецией поехали следом сами. Иначе кость тонула в мышцах, живущих отдельно
            // (у прочих мест правило прежнее — орган ЗАМЕЩАЕТ: волчья морда встаёт вместо человечьей)
            if (socket.inner && organ != null && PickParts(organ.visualParts, socket.formRole) != null
                             && socket.parts != null && socket.parts.Length > 0)
            {
                Piece(container.transform, socket, null, +1f, pos, rot, made, linkD, null, sz);
                if (socket.mirrorX) Piece(container.transform, socket, null, -1f, pos, rot, made, linkD, null, sz);
            }
        }
    }

    /// <summary>Позиция и поворот места. Корень (без `parent`) стоит по своим `localPos`/`baseEuler`;
    /// у ребёнка обе величины считаются ОТ РОДИТЕЛЯ — отсюда невозможность разъехаться.
    /// `depth` страхует от цикла в данных (его же ловит `ValidateSockets`, но билдер не должен зависать).</summary>
    static (Vector3, Quaternion) Place(BodySocket s, Dictionary<string, BodySocket> byName,
                                       Dictionary<string, (Vector3, Quaternion)> placed, int depth,
                                       Dictionary<string, Vector3> axisOf = null,
                                       Dictionary<string, Bone> byBone = null,
                                       Dictionary<string, (Vector3, Quaternion)> bonePos = null)
    {
        if (placed.TryGetValue(s.name, out var done)) return done;

        var rot = Quaternion.Euler(s.baseEuler);
        var pos = s.localPos;

        // МЕСТО НА КОСТИ. Скелет забирает несущее (позвоночник, рёбра, конечности), а голова, хвост и
        // закрытые места остаются сокетами — и родителя им надо где-то взять. Берут его на КОСТИ: место
        // садится в точку `attach` вдоль неё и наследует её поворот, ровно как кость садится на кость.
        // Это и есть будущий адрес аугумента: привитый рог сядет на череп, а не «рядом с местом головы».
        //     СМЕЩЕНИЕ ЗДЕСЬ В МЕТРАХ, а не в калибрах родителя. У кости нет габаритной коробки, делить
        // не на что — зато вся кость и так задана метрами, поэтому смешения единиц не возникает
        if (depth < 16 && !string.IsNullOrEmpty(s.parent) && byBone != null && bonePos != null
                       && byBone.TryGetValue(s.parent, out var pbone))
        {
            var (bp, br) = SkeletonBuilder.Place(pbone, byBone, bonePos);
            // РАЗВОРОТ ОСЕЙ СНИМАЕТСЯ. Кость растёт по своему +Y, а тело смотрит в +Z, поэтому у кости
            // вдоль хребта поворот около +90° по X — чистая техника роста, а не наклон зверя. Место,
            // унаследовав его как есть, легло бы набок: голова волка ушла бы носом в землю на 82°, и
            // по скриншоту это читалось бы как «голова оторвалась», а не как разворот системы координат
            var body = br * Quaternion.Euler(-90f, 0f, 0f);
            pos = bp + br * (Vector3.up * (pbone.length * s.attach)) + body * s.attachOffset;
            rot = body * rot;
            placed[s.name] = (pos, rot);
            return (pos, rot);
        }

        if (depth < 16 && !string.IsNullOrEmpty(s.parent) && byName.TryGetValue(s.parent, out var par) && par != s)
        {
            var (ppos, prot) = Place(par, byName, placed, depth + 1, axisOf, byBone, bonePos);
            // СТЫК на ДЛИННОЙ оси родителя: `attach` = доля вдоль неё (0 — начало, 1 — конец).
            // Смещение — в КАЛИБРАХ родителя, поэтому переживает масштабирование вида
            var b = SizeOf(par, byName, 0);
            // ось — приколотая (см. `Build`), а не выведенная из текущего габарита
            Vector3 axis = axisOf != null && axisOf.TryGetValue(par.name, out var pinned) ? pinned : LongAxis(b);
            float len = Mathf.Abs(Vector3.Dot(b, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));

            int links = Mathf.Max(1, par.chain);
            if (links > 1)
            {
                // РОДИТЕЛЬ — ЦЕПЬ (тело змеи): его «длина» это вся вереница, а точка места — НАЧАЛО первого
                // звена (билдер центрирует звено i на `len*(i+0.5)`, то есть цепь стартует ровно в месте).
                // Поэтому доля отсчитывается от начала: attach 1 = у головы, 0 = кончик — и ноль даёт РОВНО
                // конец вереницы. Прежняя поправка `− len/2` приводила в ЦЕНТР последнего звена, и вся ветка
                // садилась внахлёст на ползвена: хвост тонул в туловище, погремушка — в хвосте
                Vector3 grow = ChainDir(par, b);
                pos = ppos + prot * (grow * ((1f - s.attach) * ChainLength(par, len))
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
    // `made` собирает СОЗДАННЫЕ КОРНЕВЫЕ детали места: по ним `Build` пересаживает ветку на звено цепи
    static void Piece(Transform parent, BodySocket socket, Organ organ, float side, Vector3 basePos,
                      Quaternion baseRot, List<GameObject> made, float linkD, OrganPart[] fromOther, Vector3 sz)
    {
        // basePos/baseRot приходят из графа (уже с учётом родителя) — своих координат у места может и не быть
        Vector3 pos = basePos + (organ != null ? organ.visualOffset : Vector3.zero);
        Vector3 euler = (baseRot * Quaternion.Euler(organ != null ? organ.visualEuler : Vector3.zero)).eulerAngles;
        Vector3 size = organ != null ? Vector3.Scale(sz, organ.visualScale) : sz;

        // СОСТАВНАЯ ФОРМА («лего 8+»): орган из нескольких частей — рога = стебель+лопасть+отростки,
        // иглы = щетина. Смещения частей заданы В КАЛИБРАХ МЕСТА, поэтому одна форма годится и мелкому
        // ежу, и крупному лосю: она ужимается/разрастается вместе с местом
        // ПРИОРИТЕТ ФОРМЫ: части ЧУЖОГО органа-источника (место так объявило) → форма своего органа → форма
        // места. Источник стоит первым потому, что место само его назвало: «глаза рисует Чутьё» — это
        // решение места, а не случайность сборки
        // ПРИОРИТЕТ ФОРМЫ: части ЧУЖОГО органа-источника (место так объявило) → форма своего органа → форма
        // места. Источник стоит первым потому, что место само его назвало: «глаза рисует Чутьё» — это
        // решение места, а не случайность сборки.
        // ФИЛЬТР ПО РОЛИ ВЕЗДЕ: место берёт только те части, что предназначены ЕМУ. Иначе орган Чутья,
        // получив форму глаз, нарисовал бы её ещё и на своём месте — а у «Чутья» позиции нет вовсе, и
        // деталь родилась бы в начале координат (тот самый ящик на голове). Роль по умолчанию `None`,
        // поэтому все прежние органы — рога, иглы, хвост — работают как работали
        var parts = fromOther ?? PickParts(organ != null ? organ.visualParts : null, socket.formRole);

        // ЦЕПЬ ЗВЕНЬЕВ. Сколько — говорит ОРГАН (привитый змеиный хвост сегментен на любом носителе),
        // иначе МЕСТО (тело самой змеи — `chain`). Раньше цепь была ОТДЕЛЬНОЙ веткой и работала только
        // там, где формы нет: место не умело быть цепью вовсе. Теперь это внешний цикл, и каждое звено
        // несёт свою форму — одним кодом строятся и тело змеи, и привитый ей же хвост на чужом шасси
        bool organChain = organ != null && organ.visualSegments > 1;
        int links = organChain ? organ.visualSegments : Mathf.Max(1, socket.chain);
        float chainTaper = organChain ? (organ.visualTaper > 0f ? organ.visualTaper : 0.85f)
                                      : (socket.chainTaper > 0f ? socket.chainTaper : 0.94f);

        // ЗВЕНО ИЗ ДВУХ ЧИСЕЛ. Место задало толщину и длину В МЕТРАХ — форму (капсула + сустав-шар)
        // строим здесь, а не описываем долями в данных. Так исчезает целый класс ошибок: не надо помнить,
        // что длина капсулы это `baseSize.y × scale.y`, а доля Z уходит в высоту; длина ОДНА на всю цепь,
        // поэтому звенья равны по построению; `linkTaper` сужает ТОЛЬКО толщину, не укорачивая звено
        // ЦЕПЬ МЕСТА ГЛАВНЕЕ ФОРМЫ ОРГАНА — «носитель даёт калибр, донор даёт пропорции» (спека 4.4).
        // У змеи место «Хвост» описано цепью в метрах, а одноимённый орган несёт свою форму для ГРАФТА
        // на чужое шасси: без этого приоритета змея строила себе хвост донорскими долями (три звена
        // вместо четырёх), то есть носила аугумент вместо собственного тела. На волке место цепью не
        // описано — там орган рисует сам, как и задумано
        if (linkD > 0f && socket.linkLength > 0f && socket.chain > 0)
        {
            BuildLinks(parent, socket, side, pos, euler, Mathf.Max(1, socket.chain), made, linkD);
            return;
        }

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
                var b = sz;
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
            Vector3 canonBase = align ? Pick(sz, across, outward, along) : sz;
            Vector3 canonSize = align ? Pick(size, across, outward, along) : size;
            Quaternion place = socketRot * frameRot;

            Vector3 grow = ChainDir(socket, sz);
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
                    var made1 = Spawn(parent, socket.name,
                          linkPos + place * Vector3.Scale(pt.offset, canonBase * k),
                          (place * Quaternion.Euler(pt.euler)).eulerAngles,
                          Vector3.Scale(canonSize, pt.scale) * k, side, pt.shape, socket.solid);
                    // ПАСПОРТ ДЕТАЛИ: роль и своя окраска едут вместе с куском, а не списком имён в чужой
                    // системе. Вешаем только когда есть что сказать — обычный кусок остаётся чистым визуалом
                    if (pt.role != PartRole.None || pt.color.a > 0f)
                    {
                        var mark = made1.AddComponent<PartMark>();
                        mark.role = pt.role;
                        mark.own = pt.color;
                    }
                    made.Add(made1);
                }
            }
            return;
        }

        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }
        Quaternion rot = Quaternion.Euler(euler);

        // ЦЕПЬ БЕЗ ФОРМЫ — голые звенья (привитый хвост на чужом шасси: места своей формы не имеет)
        Vector3 dir = ChainDir(socket, sz);
        float axisLen = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))));

        float travel = 0f, prevLen = 0f;
        for (int i = 0; i < links; i++)
        {
            float k = Mathf.Pow(chainTaper, i);
            float len = axisLen * k;
            if (i > 0) travel += (prevLen + len) * 0.5f; // встык, с сужением
            prevLen = len;
            made.Add(Spawn(parent, socket.name, pos + rot * (dir * travel), euler, size * k, 1f, PartShape.Cube, socket.solid)); // зеркалирование уже учтено выше
        }
    }

    /// <summary>ЦЕПЬ ИЗ ДВУХ ЧИСЕЛ: N звеньев по `linkLength` в длину и `linkDiameter` в толщину.
    /// Форму строит КОД, а не доли в данных — поэтому длина звеньев равна ПО ПОСТРОЕНИЮ, а не подбором,
    /// и невозможно перепутать, какая доля куда идёт (вчерашний источник половины промахов).
    /// ЗВЕНО — ПУСТОЙ УЗЕЛ (масштаб 1), меш и сустав лежат ВНУТРИ него. Это не украшательство иерархии:
    /// узел без масштаба — единственное место, куда можно что-то подвесить без искажений. Пока звеном была
    /// сама капсула, её масштаб (d, len/2, d) НЕРАВНОМЕРЕН, и всё внутри него плющило: сустав выходил
    /// ДИСКОМ (те «проточки» на шее), а погремушка приехала бы овалом. Заодно доворот капсулы на 90°
    /// уехал внутрь узла — снаружи звено смотрит просто вдоль хребта, и `SnakeBodyChain` крутит его без
    /// поправок на форму примитива.</summary>
    static void BuildLinks(Transform parent, BodySocket s, float side, Vector3 pos, Vector3 euler, int links,
                           List<GameObject> made, float linkD)
    {
        Quaternion rot = Quaternion.Euler(euler);
        Vector3 grow = ChainDir(s, Vector3.zero);  // метрическая цепь: ось задана по смыслу, габарит не спрашиваем                  // у цепного места длина > толщины → ось Z
        float taper = s.linkTaper > 0f ? s.linkTaper : 1f;        // сужает ТОЛЬКО толщину
        float len = s.linkLength;

        for (int i = 0; i < links; i++)
        {
            float d = linkD * Mathf.Pow(taper, i);

            var node = new GameObject(s.name);       // ИМЯ = СОКЕТ и у узла, и у меша (контракт имён частей)
            node.transform.SetParent(parent, false);
            node.transform.localPosition = pos + rot * (grow * (len * (i + 0.5f)));
            node.transform.localRotation = rot;
            made.Add(node);

            // КАПСУЛА ВДОЛЬ ХРЕБТА: Unity вытягивает её по Y, поэтому кладём доворотом на 90°, а размеры
            // даём В МЕТРАХ напрямую (Spawn поделит Y пополам — длина выйдет ровно `len`)
            Spawn(node.transform, s.name, Vector3.zero, new Vector3(90f, 0f, 0f),
                  new Vector3(d, len, d), side, PartShape.Capsule, s.solid);

            // ВХОДНОЙ СУСТАВ у первого звена: цепь заканчивалась шаром, но не начиналась им, и на стыке с
            // родителем оставался голый торец капсулы — пережим до нуля, тот же дефект, что был на границах
            // мест. У родителя-цепи свой шар там уже стоит, и этот ложится ровно в него (диаметры совпадают
            // по наследованию), так что спецслучая «крепимся к голове» заводить не нужно
            if (i == 0)
                Spawn(node.transform, s.name + "~сустав", -grow * (len * 0.5f), Vector3.zero,
                      Vector3.one * d, side, PartShape.Sphere, s.solid);

            // СУСТАВ-ШАР на стыке — тоже внутри узла, поэтому его размер задаётся прямо в метрах: делить
            // на масштаб родителя больше не надо, а значит и перепутать нечего. Ставим его у КАЖДОГО звена,
            // включая последнее: торец капсулы — полусфера, сходящаяся В ТОЧКУ, поэтому две капсулы встык
            // касаются точкой и дают пережим до нуля. Внутри цепи его закрывал шар, а на ГРАНИЦЕ МЕСТ шара
            // не было — отсюда провалы на стыках шея/тело и тело/хвост. У последнего звена шар берёт СВОЙ
            // диаметр (не следующий по прогрессии): им же начинается цепь-наследник, и стык выходит гладким
            float dJoint = i < links - 1 ? linkD * Mathf.Pow(taper, i + 1) : d;
            Spawn(node.transform, s.name + "~сустав", grow * (len * 0.5f), Vector3.zero,
                  Vector3.one * dJoint, side, PartShape.Sphere, s.solid);
        }
    }

    /// <summary>КУДА РАСТЁТ ЦЕПЬ: по длинной оси места. Хвост/тело лежат вдоль Z → тянутся НАЗАД;
    /// рог вытянут по Y → растёт ВВЕРХ. Отдельного поля-направления не нужно — его говорит габарит.
    /// Считаем по РАЗРЕШЁННОМУ размеру (`SizeOf` — свой габарит либо доля родителя): это та же величина,
    /// что берёт `Place` для стыка детей, иначе хвост цеплялся бы к одному концу, а рос в другой.</summary>
    /// <summary>ДЛИННАЯ ОСЬ ГАБАРИТА — одно правило на весь билдер (вдоль неё считается `attach` детей и
    /// растёт цепь). Вынесено из `Place`, чтобы ось можно было ПРИКОЛОТЬ по чистому шасси: иначе смешанные
    /// пропорции переключают её молча и разворачивают ветку.</summary>
    static Vector3 LongAxis(Vector3 b) => b.z >= b.x && b.z >= b.y ? Vector3.forward
                                        : b.y >= b.x ? Vector3.up : Vector3.right;

    static Vector3 ChainDir(Vector3 b) => b.z >= b.x && b.z >= b.y ? Vector3.back
                                        : b.y >= b.x ? Vector3.up : Vector3.right;

    /// <summary>Ось цепи. У ЗВЕНА В МЕТРАХ она задана по смыслу — вдоль хребта, всегда Z: звено бывает
    /// КОРОЧЕ своего диаметра (кольцо погремушки 0.09 при 0.15), и вывод оси из габарита увёл бы цепь вбок.
    /// Это та же мина, что разворачивала тело башней вверх, — здесь её просто нет.</summary>
    static Vector3 ChainDir(BodySocket s, Vector3 size) => s.linkLength > 0f ? Vector3.back : ChainDir(size);

    /// <summary>Полная длина цепи — нужна `Place`, чтобы ребёнок сел на КОНЕЦ вереницы (хвост за туловищем).
    /// У ЗВЕНА В МЕТРАХ это просто длина × число: `linkTaper` сужает только толщину, длину не трогает —
    /// поэтому здесь ни прогрессий, ни подгонки. Старая ветка (доли + `chainTaper`) масштабировала звено
    /// целиком, и длину приходилось считать суммой ряда.</summary>
    static float ChainLength(BodySocket s, float linkLen)
    {
        int n = Mathf.Max(1, s.chain);
        if (s.linkLength > 0f) return s.linkLength * n;
        float t = s.chainTaper > 0f ? s.chainTaper : 0.94f;
        return Mathf.Approximately(t, 1f) ? linkLen * n : linkLen * (1f - Mathf.Pow(t, n)) / (1f - t);
    }

    /// <summary>ДИАМЕТР ПЕРВОГО ЗВЕНА. Задан у места — берём его; пуст — ПРОДОЛЖАЕМ РОДИТЕЛЬСКУЮ ЦЕПЬ с того
    /// диаметра, каким она кончилась (`d × taper^(n−1)`). Поэтому «первое звено хвоста толще тела» —
    /// состояние, которого больше нет: число живёт в ОДНОМ месте (у шеи) и выводится дальше по хребту,
    /// вместо того чтобы дублироваться в каждой веренице и разъезжаться при любой правке сужения.</summary>
    static float ChainDiameter(BodySocket s, Dictionary<string, BodySocket> byName, int depth)
    {
        if (s.linkDiameter > 0f) return s.linkDiameter;
        if (depth >= 16 || string.IsNullOrEmpty(s.parent)
                        || !byName.TryGetValue(s.parent, out var par) || par == s || par.linkLength <= 0f) return 0f;
        float t = par.linkTaper > 0f ? par.linkTaper : 1f;
        return ChainDiameter(par, byName, depth + 1) * Mathf.Pow(t, Mathf.Max(1, par.chain) - 1);
    }

    /// <summary>Снос, работающий И В РЕДАКТОРЕ. Вне Play  запрещён («may not be called from edit
    /// mode»), а карта тел собирает существо именно там — билдер должен уметь строиться без запущенной игры,
    /// иначе измерять нечем. В Play поведение прежнее: отложенный снос до конца кадра.</summary>
    static void Kill(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    static Material primMat;

    /// <summary>Материал по умолчанию — ТОТ ЖЕ, что у примитивов. Берём его с реального примитива, а не
    /// ищем шейдер по имени: имя зависит от рендер-пайплайна, и промах даёт розовую тушу вместо серой.
    /// Части-места получают его сами, а скин-мешу материал надо назначить руками.</summary>
    static Material PrimitiveMaterial()
    {
        if (primMat != null) return primMat;
        var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        primMat = probe.GetComponent<Renderer>().sharedMaterial;
        Kill(probe);
        return primMat;
    }

    /// <summary>ГАБАРИТ МЕСТА. Задан `sizeRel` — считаем ОТ РОДИТЕЛЯ (шасси говорит, как вставлять):
    /// голова доля корпуса, пасть доля головы, глаз доля головы. Тогда пропорция живёт в данных, и
    /// правка одного места тянет за собой всю ветку — вместо того чтобы молча с ней разъехаться.
    /// Пусто — прежний абсолютный `SizeForGraph` (корневые места и цепи в метрах).</summary>
    public static Vector3 SizeOf(BodySocket s, Dictionary<string, BodySocket> byName, int depth = 0)
    {
        if (s.sizeRel == Vector3.zero || depth >= 16 || string.IsNullOrEmpty(s.parent)
            || !byName.TryGetValue(s.parent, out var par) || par == s) return Raw(s, byName, depth);
        return Vector3.Scale(SizeOf(par, byName, depth + 1), s.sizeRel);
    }

    /// <summary>СОБСТВЕННЫЙ габарит места. У цепи он собран из звена — и толщину надо брать ту же, что
    /// возьмёт сборка: диаметр прописан ОДИН раз (у шеи) и дальше наследуется по хребту, поэтому сырое
    /// поле у тела и хвоста пустое. Читая его напрямую, мы получали габарит `0 × 0 × длина`, а на этот
    /// габарит умножается всё, что задано «в калибрах родителя»: боковой вынос привитых лап на змее
    /// молча обращался в ноль — лапы садились на осевую линию, и понять это по данным было нельзя.</summary>
    static Vector3 Raw(BodySocket s, Dictionary<string, BodySocket> byName, int depth)
    {
        if (s.linkLength <= 0f) return s.SizeForGraph;
        float d = ChainDiameter(s, byName, depth);
        return new Vector3(d, d, s.linkLength);
    }

    /// <summary>Части органа, предназначенные месту с такой ролью. `null`, если подходящих нет — тогда
    /// место рисует свою форму. Отдельного массива не плодим, когда фильтровать нечего.</summary>
    static OrganPart[] PickParts(OrganPart[] src, PartRole role)
    {
        if (src == null || src.Length == 0) return null;
        int n = 0;
        foreach (var p in src) if (p != null && p.role == role) n++;
        if (n == 0) return null;
        if (n == src.Length) return src;
        var picked = new OrganPart[n];
        int i = 0;
        foreach (var p in src) if (p != null && p.role == role) picked[i++] = p;
        return picked;
    }

    static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    static Vector3 Pick(Vector3 v, int a, int b, int c) => new(v[a], v[b], v[c]);          // взять компоненты в порядке осей формы

    // одна деталь. side = -1 зеркалит вынос по X и рыскание/крен (тангаж общий: левая лапа не «смотрит» иначе правой)
    static GameObject Spawn(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 size, float side,
                            PartShape shape = PartShape.Cube, bool solid = false)
    {
        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }

        // КАПСУЛА И ЦИЛИНДР у Unity ВДВОЕ ВЫШЕ куба при том же масштабе (высота примитива 2, диаметр 1).
        // Делим Y, чтобы `scale` во всех данных значил ОДНО И ТО ЖЕ — габарит куска, а не масштаб примитива
        if (shape == PartShape.Capsule || shape == PartShape.Cylinder) size.y *= 0.5f;

        var cube = GameObject.CreatePrimitive(shape == PartShape.Sphere ? PrimitiveType.Sphere
                                            : shape == PartShape.Capsule ? PrimitiveType.Capsule
                                            : shape == PartShape.Cylinder ? PrimitiveType.Cylinder
                                            : PrimitiveType.Cube);
        // [ANIM] ПЛОТНАЯ ЧАСТЬ оставляет коллайдер: кусок тела — препятствие для других и поверхность
        // попаданий (тело змеи плотное по всей длине). Обычная часть остаётся чистым визуалом:
        // физика носителя — его CharacterController, лишние коллайдеры мешали бы ему самому
        if (!solid && cube.TryGetComponent<Collider>(out var col)) Kill(col);
        // ИМЯ = СОКЕТ (стабильный словарь), не орган: по именам частей работают ПЕРВОЕ ЛИЦО (прячет свою
        // голову) и ЭМОЦ-ТИНТ (красит морду-градусник). Имя органа менялось бы от сборки и ломало обе системы
        cube.name = name;

        var t = cube.transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = size;
        return cube;
    }
}
