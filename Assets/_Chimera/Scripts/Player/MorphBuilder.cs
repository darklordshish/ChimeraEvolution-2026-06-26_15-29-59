using System.Collections.Generic;
using UnityEngine;

/// <summary>МОРФОЛОГИЯ (ось 2): собирает КУБ-МОДЕЛЬ тела из данных — ОДНА система (без статичного BuildBlocky,
/// потому нет дубля). База: куб на КАЖДОМ СОКЕТЕ шасси (голая тушка). Орган ЗАМЕНЯЕТ куб СВОЕГО сокета
/// деталью — место следует из `Organ.slot`, отдельного адреса нет (единый источник правды, спека сокет-плана).
/// Пересобирается при смене состава. Эмерджентность: волчья Пасть на ЧЕЛОВЕЧЬЕМ сокете «Пасть» = морда
/// оборотня-волка. Кубы грубо, без подгонки.</summary>
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
        // КОСТИ КАК ТРАНСФОРМЫ — чтобы деталь гнезда стала ребёнком своей кости (П7): её повезёт анимация
        var boneXf = new Dictionary<string, Transform>();
        var skeleton = container.transform.Find("Skeleton");
        if (skeleton != null)
            foreach (var t in skeleton.GetComponentsInChildren<Transform>(true))
                if (t != skeleton && !boneXf.ContainsKey(t.name)) boneXf[t.name] = t;

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
            organBySocket.TryGetValue(socket.name, out var organ);

            // ГНЕЗДО (спека 26.09): аугмент, адресованный гнёздами, встаёт в гнездо ШАССИ — в его кадре и единицах,
            // погашено место или нет (П1: «погашено» отнимает у места форму куба, но не гнездо). Куски без роли — в
            // своё место; куски с ролью (уши, глаза, нос, ямки Чутья) — в место, которое берёт эту роль (`formFrom`)
            bool nested = false;
            if (Nested(organ))
            {
                NestParts(container.transform, chassis, socket, PartsFor(organ, PartRole.None), byBone, bonePos, boneXf, organBySocket);
                nested = true;
            }
            if (!string.IsNullOrEmpty(socket.formFrom) && organBySocket.TryGetValue(socket.formFrom, out var nestSrc) && Nested(nestSrc))
            {
                NestParts(container.transform, chassis, socket, PartsFor(nestSrc, socket.formRole), byBone, bonePos, boneXf, organBySocket);
                nested = true;
            }
            if (nested) continue;

            if (boneSockets.Contains(socket.name))
            {
                // ФОРМУ ЭТОГО МЕСТА СТРОИТ ГРАФ — но куски органа, прикреплённые к УЗЛУ, рисуются и здесь. Ноги
                // волка погашены (тушу и бёдра даёт поле), а пясть и лапа — ригблоки на конце `предплечье`:
                // погаси их вместе с местом, и зверь остался бы без лап ниже запястья. Куски без узла на
                // погашенном месте по-прежнему не рисуются — их форму заменило поле
                if (HasNodeParts(organ))
                {
                    var (hp, hr) = Place(socket, byName, placed, 0, axisOf, byBone, bonePos, boneSockets);
                    NodeParts(container.transform, socket, organ, hp, hr, SizeOf(socket, byName, 0), axisOf, byBone, bonePos);
                }
                continue;
            }
            // ЦЕПЬ ЗМЕИ НЕ ПРОПУСКАЕМ: морф СТРОИТ ФОРМУ и ставит звенья в стартовую позу, а дальше
            // позицию каждый кадр перезаписывает своя система (SnakeBodyChain ведёт цепь по пути головы).
            // Раньше здесь стоял continue — отсюда невидимая змея: место есть, а рисовать его было некому
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

            var (pos, rot) = Place(socket, byName, placed, 0, axisOf, byBone, bonePos, boneSockets);
            var made = new List<GameObject>();
            float linkD = ChainDiameter(socket, byName, 0);
            Vector3 sz = SizeOf(socket, byName, 0);   // габарит: свой или доля родителя

            // КУСКИ НА УЗЛАХ рисуются отдельно — у них свой кадр (узел или, если узла у носителя нет, само
            // место). Орган, у которого ВСЕ куски на узлах, место кубом не рисует: волчьи ноги на человеке —
            // это пясть и лапа, а не пясть, лапа и ещё голый куб места поверх
            bool onlyNodeParts = false;
            if (HasNodeParts(organ))
            {
                NodeParts(container.transform, socket, organ, pos, rot, sz, axisOf, byBone, bonePos);
                var rest = PartsWithoutNode(organ);
                if (rest == null) onlyNodeParts = true;
                else if (fromOther == null) fromOther = rest;
            }
            if (!onlyNodeParts)
            {
                Piece(container.transform, socket, organ, +1f, pos, rot, made, linkD, fromOther, sz);
                if (socket.mirrorX) Piece(container.transform, socket, organ, -1f, pos, rot, made, linkD, fromOther, sz);
            }

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
                                       Dictionary<string, (Vector3, Quaternion)> bonePos = null,
                                       HashSet<string> hides = null)
    {
        if (placed.TryGetValue(s.name, out var done)) return done;

        var rot = Quaternion.Euler(s.baseEuler);
        var pos = s.localPos;

        // ПОГАШЕННОЕ МЕСТО СТОИТ ПО УЗЛУ ГРАФА С ТЕМ ЖЕ ИМЕНЕМ (17.09, письмо модельной линии OTVET §4.1).
        // Тело рисует граф, а детали головы (Пасть, глаза, уши, нос) считаются от места `голова`. Пока место
        // стояло по своему плану, детали садились туда, где голова была БЫ по цепочке мест, — у горла и в
        // воздухе перед мордой, при любом качестве графа (п. 0 паспорта волка, отложенный «до клетки» 01.09,
        // а клетку отменили). Двигать план мест под граф руками — второй источник формы, разошлись бы на
        // первой же правке.
        //     ПОЗА ОТ УЗЛА, КАЛИБР ОТ МЕСТА. Начало места совпадает с началом узла, длинная ось места ложится
        // вдоль узла, а длина остаётся своей. Растянуть место до длины узла нельзя: узел `голова` — череп
        // БЕЗ морды (0.20 м), место — череп С мордой (0.47 м), и доли всех деталей сжались бы вдвое. Лишняя
        // длина места и есть зона морды: её заполнит ригблок Пасти.
        //     Поворот узла собственный, `baseEuler` места не добавляется: наклон уже несёт граф, второй раз
        // голова «кивнула» бы на угол, заданный под старый план.
        if (hides != null && hides.Contains(s.name) && byBone != null && bonePos != null
            && byBone.TryGetValue(s.name, out var node))
        {
            var (np, nr) = SkeletonBuilder.Place(node, byBone, bonePos);
            Vector3 size = SizeOf(s, byName, 0);
            Vector3 axis = axisOf != null && axisOf.TryGetValue(s.name, out var pinnedOwn) ? pinnedOwn : LongAxis(size);
            float length = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));
            // кость растёт по своему +Y — разворачиваем так, чтобы по нему легла ДЛИННАЯ ось места
            var frame = nr * Quaternion.FromToRotation(axis, Vector3.up);
            pos = np + frame * (axis * (length * 0.5f));
            placed[s.name] = (pos, frame);
            return (pos, frame);
        }

        // МЕСТО НА КОСТИ. Скелет забирает несущее (позвоночник, рёбра, конечности), а голова, хвост и
        // закрытые места остаются сокетами — и родителя им надо где-то взять. Берут его на КОСТИ: место
        // садится в точку `attach` вдоль неё и наследует её поворот, ровно как кость садится на кость.
        // Это и есть будущий адрес аугумента: привитый рог сядет на череп, а не «рядом с местом головы».
        //     СМЕЩЕНИЕ ЗДЕСЬ В МЕТРАХ, а не в калибрах родителя. У кости нет габаритной коробки, делить
        // не на что — зато вся кость и так задана метрами, поэтому смешения единиц не возникает
        //     ИМЯ-КОЛЛИЗИЯ РЕШАЕТСЯ В ПОЛЬЗУ МЕСТА (08.09). У четырёх видов есть и СОКЕТ «шея», и КОСТЬ
        // «шея»: пока костей не было, ссылка `parent = "шея"` однозначно значила место, и все смещения
        // писались под его поворот. Перенос скелетов создал двойника, кость выигрывала — и место
        // наследовало ориентацию звена, которое после разворота смотрит от черепа ВНИЗ. Координаты при
        // этом оставались верными, разворачивались СМЕЩЕНИЯ: плечи человека уезжали на два метра назад.
        //     Поэтому кость берётся, только когда одноимённого места нет: `Хвост → крестец` и
        // `Игломёт → грудной` у волка — настоящие адреса на кости, и они работают по-прежнему.
        if (depth < 16 && !string.IsNullOrEmpty(s.parent) && byBone != null && bonePos != null
                       && !byName.ContainsKey(s.parent)
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
            var (ppos, prot) = Place(par, byName, placed, depth + 1, axisOf, byBone, bonePos, hides);
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
                Vector3 grow = ChainDir(par, b, s.chainForward);   // с какого конца цепи садится РЕБЁНОК
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

    // ── ГНЁЗДА (спека `2026-09-26-adresaciya-detaley-himery.md`) ─────────────────────────────────────────
    static readonly SortedSet<string> missingNests = new();

    /// <summary>ГНЁЗДА, КОТОРЫХ НЕ НАШЛОСЬ («Вид.место»): аугмент адресован гнездом, а у шасси его нет. Кусок не
    /// рисуется — ставить его наугад значит снова повесить морду в воздух, — и промах виден машине.</summary>
    public static IEnumerable<string> MissingNests => missingNests;

    static bool Nested(Organ organ)
    {
        if (organ == null || organ.visualParts == null) return false;
        foreach (var p in organ.visualParts) if (p != null && p.nest) return true;
        return false;
    }

    static List<OrganPart> PartsFor(Organ organ, PartRole role)
    {
        var res = new List<OrganPart>();
        foreach (var p in organ.visualParts) if (p != null && p.role == role) res.Add(p);
        return res;
    }

    /// <summary>КУСКИ В ГНЕЗДЕ ШАССИ. Кадр — гнездо (поза кости-хозяина × кадр гнезда в ней), единица — его `unit`:
    /// `offset` и `scale` умножаются на неё, `euler` — доворот в кадре гнезда. Форма донора, калибр носителя (П3).
    /// Деталь строится в контейнере (там живут зеркало и все замеры) и вешается на кость с сохранением мирового
    /// положения: правая — на `host`, зеркальная — на `host.L`, если кость парная.</summary>
    static void NestParts(Transform container, SpeciesSO chassis, BodySocket socket, List<OrganPart> parts,
                          Dictionary<string, Bone> byBone, Dictionary<string, (Vector3, Quaternion)> bonePos,
                          Dictionary<string, Transform> boneXf, Dictionary<string, Organ> worn)
    {
        if (parts.Count == 0) return;
        if (!ResolveNest(chassis, socket.name, byBone, bonePos, worn, out var np, out var nr, out float unit, out bool pair, out string hostName))
        {
            missingNests.Add(chassis.speciesName + "." + socket.name);
            return;
        }
        boneXf.TryGetValue(hostName, out var right);
        if (!boneXf.TryGetValue(hostName + ".L", out var left)) left = right;

        // ОПОРНЫЙ КОНЕЦ ДОТЯГИВАЕТСЯ ДО ЗЕМЛИ (П4): высота запястья — калибр шасси, поэтому тянется шток аугмента
        var shape = System.Array.IndexOf(chassis.stanceLimbs ?? new string[0], socket.name) >= 0
                  ? ReachGround(parts, np, nr, unit) : null;

        foreach (var pt in parts)
        {
            var (off, scl) = shape != null ? shape[pt] : (pt.offset, pt.scale);
            Vector3 pos = np + nr * (off * unit);
            Vector3 euler = (nr * Quaternion.Euler(pt.euler)).eulerAngles;
            Vector3 size = scl * unit;
            Hang(Mark(Spawn(container, socket.name, pos, euler, size, +1f, pt.shape, socket.solid, pt.block), pt), right);
            if (pair)
                Hang(Mark(Spawn(container, socket.name, pos, euler, size, -1f, pt.shape, socket.solid, pt.block), pt), left);
        }
    }

    // ── ДОТЯЖКА ОПОРНОГО КОНЦА (П4) ────────────────────────────────────────────────────────────────
    // Тянется ОДИН шток — голень конца (пясть, колонна копыта), всё ниже него переезжает целиком, выше — остаётся. Так и
    // в анатомии: чужая лапа на высоком шасси — это длинная пясть, а не растянутые когти. Первая версия (26.09) тянула
    // позиции всех кусков и разводила короткую ежиную лапу на волке щелями по 10–23 см — матрица химер поймала это как И2.
    // Нет штока (ни помеченного, ни куска вдоль гнезда) — конец не тянется, и детектор честно покажет «в воздухе»

    static bool Aligned(OrganPart pt) => Quaternion.Angle(Quaternion.identity, Quaternion.Euler(pt.euler)) < 15f;

    /// <summary>ФОРМА КОНЦА, ВСТАВШЕГО НА ЗЕМЛЮ: для каждого куска — смещение и габарит (в единицах гнезда) после
    /// растяжения штока. Растяжение вдоль +Z гнезда кусочно-линейно: выше штока — как было, внутри — пропорционально,
    /// ниже — сдвиг на добавку. Добавка подбирается по реальному низу повёрнутых кусков за несколько шагов.</summary>
    static Dictionary<OrganPart, (Vector3, Vector3)> ReachGround(List<OrganPart> parts, Vector3 np, Quaternion nr, float unit)
    {
        var shank = parts.FindAll(p => p.stretch);
        if (shank.Count == 0)
        {
            OrganPart best = null;
            foreach (var p in parts) if (Aligned(p) && (best == null || p.scale.z > best.scale.z)) best = p;
            if (best != null) shank.Add(best);
        }
        float down = -(nr * Vector3.forward).y;               // сколько вниз даёт единица вдоль гнезда
        if (shank.Count == 0 || down < 0.2f || unit <= 0f) return null;

        float top = float.MaxValue, bottom = float.MinValue;
        foreach (var p in shank) { top = Mathf.Min(top, p.offset.z - p.scale.z * 0.5f); bottom = Mathf.Max(bottom, p.offset.z + p.scale.z * 0.5f); }
        float length = bottom - top;
        if (length < 1e-4f) return null;

        float add = 0f;
        Dictionary<OrganPart, (Vector3, Vector3)> shape = null;
        for (int i = 0; i < 4; i++)
        {
            float k = Mathf.Max(0.05f, (length + add) / length);
            shape = new Dictionary<OrganPart, (Vector3, Vector3)>();
            foreach (var p in parts)
            {
                float z = p.offset.z;
                float z2 = z <= top ? z : z <= bottom ? top + (z - top) * k : z + add;
                var s = p.scale;
                if (shank.Contains(p)) s.z *= k;
                shape[p] = (new Vector3(p.offset.x, p.offset.y, z2), s);
            }
            float low = LowestY(parts, shape, np, nr, unit);
            if (Mathf.Abs(low) < 0.002f) break;
            add += low / down / unit;
        }
        return shape;
    }

    static float LowestY(List<OrganPart> parts, Dictionary<OrganPart, (Vector3, Vector3)> shape, Vector3 np, Quaternion nr, float unit)
    {
        float low = float.MaxValue;
        foreach (var pt in parts)
        {
            var (off, scl) = shape[pt];
            Vector3 c = np + nr * (off * unit);
            var rot = nr * Quaternion.Euler(pt.euler);
            Vector3 half = scl * (unit * 0.5f);
            // полувысота повёрнутой коробки: проекции её полуосей на вертикаль
            float ext = Mathf.Abs((rot * Vector3.right).y) * half.x + Mathf.Abs((rot * Vector3.up).y) * half.y
                      + Mathf.Abs((rot * Vector3.forward).y) * half.z;
            low = Mathf.Min(low, c.y - ext);
        }
        return low;
    }

    /// <summary>КАДР ГНЕЗДА МЕСТА (в системе контейнера) и единица. Сначала — гнездо, которое ПРИНОСИТ надетый аугмент
    /// (`Organ.carries`: нос на кончике чужой морды), в кадре гнезда этого аугмента; нет — гнездо шасси. Кость-хозяин —
    /// кость гнезда-опоры: нос на морде едет с головой.</summary>
    static bool ResolveNest(SpeciesSO chassis, string place, Dictionary<string, Bone> byBone,
                            Dictionary<string, (Vector3, Quaternion)> bonePos, Dictionary<string, Organ> worn,
                            out Vector3 pos, out Quaternion rot, out float unit, out bool pair, out string host)
    {
        pos = Vector3.zero; rot = Quaternion.identity; unit = 1f; pair = false; host = null;
        if (worn != null)
            foreach (var kv in worn)
            {
                var o = kv.Value;
                if (o == null || o.carries == null || kv.Key == place) continue;
                foreach (var c in o.carries)
                {
                    if (c == null || c.name != place) continue;
                    if (!ResolveNest(chassis, kv.Key, byBone, bonePos, null, out var bp, out var br, out float bu, out _, out host)) continue;
                    pos = bp + br * (c.localPos * bu);
                    rot = br * c.localRot;
                    unit = bu * c.unit;
                    pair = c.mirror;
                    return true;
                }
            }

        PlaceNest nest = null;
        if (chassis.nests != null) foreach (var n in chassis.nests) if (n != null && n.name == place) { nest = n; break; }
        if (nest == null || string.IsNullOrEmpty(nest.host) || !byBone.TryGetValue(nest.host, out var hb)) return false;
        var (hp, hr) = SkeletonBuilder.Place(hb, byBone, bonePos);
        pos = hp + hr * nest.localPos;
        rot = hr * nest.localRot;
        unit = nest.unit;
        pair = nest.mirror;
        host = nest.host;
        return true;
    }

    static void Hang(GameObject go, Transform bone)
    {
        if (go != null && bone != null) go.transform.SetParent(bone, true);
    }

    static bool HasNodeParts(Organ organ)
    {
        if (organ == null || organ.visualParts == null) return false;
        foreach (var p in organ.visualParts) if (p != null && !string.IsNullOrEmpty(p.node)) return true;
        return false;
    }

    static OrganPart[] PartsWithoutNode(Organ organ)
    {
        var rest = new List<OrganPart>();
        foreach (var p in organ.visualParts) if (p != null && string.IsNullOrEmpty(p.node)) rest.Add(p);
        return rest.Count > 0 ? rest.ToArray() : null;
    }

    /// <summary>КУСОК НА УЗЛЕ ГРАФА (решение 17.09: нижние ноги — ригблоки, письмо FEEDBACK-2026-09-17c §4).
    ///
    /// КАДР: начало в КОНЦЕ узла, поворот узла × Euler(−90, 0, 0) — тот же, которым место садится на кость:
    /// +Z вдоль узла, +Y — перёд узла, +X вбок. ЕДИНИЦЫ — доли узла, а не метры: `offset` и `scale` по X и Y в
    /// ДИАМЕТРАХ конца узла (2·r1), по Z — в ДЛИНАХ узла; `euler` — поза сегмента в этом кадре. Метров в
    /// данных донора быть не может (закон идентичности), поэтому один брусок годится любому калибру.
    ///
    /// УЗЛА У НОСИТЕЛЯ НЕТ (человек на чистом листе с волчьими ногами) — кадр даёт само место по той же
    /// формуле: длина — длинная сторона места, диаметр — меньшая поперечная, начало — дальний конец места.
    /// Спрятать кусок было бы проще, но тогда игрок, приживший волчьи ноги, не увидел бы лап: игра соврала бы
    /// ему о его же теле. Особого случая при этом нет — меняется только поставщик кадра.</summary>
    static void NodeParts(Transform parent, BodySocket socket, Organ organ, Vector3 placePos, Quaternion placeRot,
                          Vector3 placeSize, Dictionary<string, Vector3> axisOf,
                          Dictionary<string, Bone> byBone, Dictionary<string, (Vector3, Quaternion)> bonePos)
    {
        foreach (var pt in organ.visualParts)
        {
            if (pt == null || string.IsNullOrEmpty(pt.node)) continue;

            Vector3 end;
            Quaternion frame;
            float diameter, length;
            bool mirror;
            if (byBone != null && bonePos != null && byBone.TryGetValue(pt.node, out var node))
            {
                var (np, nr) = SkeletonBuilder.Place(node, byBone, bonePos);
                length = node.length;
                diameter = 2f * node.r1;
                end = np + nr * (Vector3.up * length);
                frame = nr * Quaternion.Euler(-90f, 0f, 0f);
                mirror = node.mirrorX;          // пара узлов → пара кусков, как пара ног
            }
            else
            {
                Vector3 axis = axisOf != null && axisOf.TryGetValue(socket.name, out var pinned) ? pinned : LongAxis(placeSize);
                Vector3 across = new Vector3(1f - Mathf.Abs(axis.x), 1f - Mathf.Abs(axis.y), 1f - Mathf.Abs(axis.z));
                length = Mathf.Abs(Vector3.Dot(placeSize, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));
                float a = across.x > 0f ? placeSize.x : float.MaxValue;
                float b = across.y > 0f ? placeSize.y : float.MaxValue;
                float c = across.z > 0f ? placeSize.z : float.MaxValue;
                diameter = Mathf.Min(a, Mathf.Min(b, c));
                end = placePos + placeRot * (axis * (length * 0.5f));
                frame = placeRot * Quaternion.FromToRotation(Vector3.forward, axis);
                mirror = socket.mirrorX;
            }

            var unit = new Vector3(diameter, diameter, length);
            Vector3 pos = end + frame * Vector3.Scale(pt.offset, unit);
            Vector3 euler = (frame * Quaternion.Euler(pt.euler)).eulerAngles;
            Vector3 size = Vector3.Scale(pt.scale, unit);

            Mark(Spawn(parent, socket.name, pos, euler, size, +1f, pt.shape, socket.solid, pt.block), pt);
            if (mirror) Mark(Spawn(parent, socket.name, pos, euler, size, -1f, pt.shape, socket.solid, pt.block), pt);
        }
    }

    static GameObject Mark(GameObject go, OrganPart pt)
    {
        if (pt.role == PartRole.None && pt.color.a <= 0f) return go;
        var mark = go.AddComponent<PartMark>();
        mark.role = pt.role;
        mark.own = pt.color;
        return go;
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
                          Vector3.Scale(canonSize, pt.scale) * k, side, pt.shape, socket.solid, pt.block);
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
    const string LinkBlock = "звено";
    const float LinkOverlap = 1.12f;   // звено длиннее шага: торцы заходят в соседей (замер модельной линии, поставка 7 §4)

    static void BuildLinks(Transform parent, BodySocket s, float side, Vector3 pos, Vector3 euler, int links,
                           List<GameObject> made, float linkD)
    {
        Quaternion rot = Quaternion.Euler(euler);
        Vector3 grow = ChainDir(s, Vector3.zero);  // метрическая цепь: ось задана по смыслу, габарит не спрашиваем                  // у цепного места длина > толщины → ось Z
        float taper = s.linkTaper > 0f ? s.linkTaper : 1f;        // сужает ТОЛЬКО толщину
        float len = s.linkLength;

        // ЗВЕНО — РИГБЛОК, ЕСЛИ ОН ЕСТЬ В БИБЛИОТЕКЕ (поставка 7 §4). Пара «капсула + шар» давала змее почти всю её цену —
        // ~27 тыс. треугольников из 27.6 на клетке 0.042. Блок `звено` не сходится торцом в точку, как капсула, поэтому
        // шар на стыке не нужен; длина с запасом 12 % — торцы прячутся в соседей, пережима нет. Узел звена тот же:
        // его двигает `SnakeBodyChain`, меш едет внутри. Нет блока — прежние примитивы
        bool linkBlock = BlockMesh(LinkBlock) != null;

        for (int i = 0; i < links; i++)
        {
            float d = linkD * Mathf.Pow(taper, i);

            var node = new GameObject(s.name);       // ИМЯ = СОКЕТ и у узла, и у меша (контракт имён частей)
            node.transform.SetParent(parent, false);
            node.transform.localPosition = pos + rot * (grow * (len * (i + 0.5f)));
            node.transform.localRotation = rot;
            made.Add(node);

            if (linkBlock)
            {
                Spawn(node.transform, s.name, Vector3.zero, Vector3.zero, new Vector3(d, d, len * LinkOverlap),
                      side, PartShape.Cube, s.solid, LinkBlock);
                continue;
            }

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
    static Vector3 ChainDir(BodySocket s, Vector3 size, bool forward = false) => s.linkLength > 0f
                                                        ? (forward ? Vector3.forward : Vector3.back)
                                                        : ChainDir(size);

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
    // ── БИБЛИОТЕКА ФОРМ ──────────────────────────────────────────────────────────────────
    // Блок ищется ПО ИМЕНИ: кусок вида говорит «клин», каталог отвечает мешем. Каталога нет или имени в
    // нём нет — кусок рисуется примитивом, как раньше, а имя копится в `MissingBlocks`. Тихий откат без
    // следа был бы миной того же рода, что «новое поле приходит нулём»: опечатка в имени не даёт ни
    // ошибки, ни заметной разницы на мелкой детали
    static ShapeCatalog catalog;
    static bool catalogAsked;
    static readonly SortedSet<string> missingBlocks = new();

    /// <summary>Действующий каталог форм: поданный `SetCatalog` либо поднятый из `Resources`.</summary>
    public static ShapeCatalog Catalog
    {
        get
        {
            if (catalog == null && !catalogAsked)
            {
                catalogAsked = true;
                catalog = Resources.Load<ShapeCatalog>(ShapeCatalog.ResourceName);
            }
            return catalog;
        }
    }

    /// <summary>Подать каталог вручную (бутстрап, тест). `null` — работать на голых примитивах.</summary>
    public static void SetCatalog(ShapeCatalog c)
    {
        catalog = c;
        catalogAsked = true;
        missingBlocks.Clear();
    }

    /// <summary>Вернуть каталог по умолчанию: следующий запрос снова поднимет его из `Resources`. Тестам, подававшим свой
    /// каталог или `null`, звать в конце — иначе «пустой» каталог переживёт тест до перезагрузки домена, и всё, что
    /// строится в редакторе после прогона (виды, карта тел, кадры), нарисует ригблоки кубами без единой ошибки.</summary>
    public static void ResetCatalog()
    {
        catalog = null;
        catalogAsked = false;
        missingBlocks.Clear();
        missingNests.Clear();
    }

    /// <summary>ИМЕНА БЛОКОВ, КОТОРЫХ НЕ НАШЛОСЬ, — долг библиотеки форм, видимый машине.</summary>
    public static IEnumerable<string> MissingBlocks => missingBlocks;

    static Mesh BlockMesh(string blockName)
    {
        if (string.IsNullOrEmpty(blockName)) return null;
        var mesh = Catalog != null ? Catalog.Find(blockName) : null;
        if (mesh == null) missingBlocks.Add(blockName);
        return mesh;
    }

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
    /// <summary>ЕСТЬ ЛИ У ВИДА ТАКОЙ РОДИТЕЛЬ — местом ЛИБО КОСТЬЮ.
    ///
    /// Со скелетом место садится не только на другое место, но и прямо на кость: хвост волка сидит на
    /// «крестце», игломёт на «грудном» (`Place` ищет родителя среди костей и ставит место на сустав).
    /// Знание об этом выводилось заново в каждом инструменте — и каждый по очереди объявлял исправное
    /// сломанным: сперва `BodyRules` ругался «нет нарисованного предка», потом аудит данных, потом
    /// генератор схем писал «родитель НЕ НАЙДЕН, место встанет в корень». Три ложные тревоги об одном
    /// и том же — признак, что факт живёт не там, где его спрашивают.</summary>
    /// <summary>ЭТО ИМЯ — КОСТЬ? Отдельный вопрос от `ParentExists`, и путать их нельзя: правило «место
    /// висит на пустоте» обходится ТОЛЬКО для кости. Спроси там «существует ли родитель вообще» — и
    /// правило замолчит навсегда, потому что родитель-место существует всегда, вопрос лишь в том,
    /// рисует ли он что-нибудь.</summary>
    public static bool IsBone(SpeciesSO species, string name)
    {
        if (string.IsNullOrEmpty(name) || species == null || species.bones == null) return false;
        foreach (var b in species.bones)
            if (b != null && b.name == name) return true;
        return false;
    }

    public static bool ParentExists(SpeciesSO species, string parent)
    {
        if (string.IsNullOrEmpty(parent) || species == null) return false;
        if (species.sockets != null)
            foreach (var k in species.sockets)
                if (k != null && k.name == parent) return true;
        if (species.bones != null)
            foreach (var b in species.bones)
                if (b != null && b.name == parent) return true;
        return false;
    }

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
                            PartShape shape = PartShape.Cube, bool solid = false, string block = null)
    {
        if (side < 0f) { pos.x = -pos.x; euler.y = -euler.y; euler.z = -euler.z; }

        GameObject cube;
        var blockMesh = BlockMesh(block);
        if (blockMesh != null)
        {
            // РИГБЛОК ПОДМЕНЯЕТ КУБ ОДИН В ОДИН: габарит блока 1×1×1 с центром в нуле (требование
            // контракта), поэтому `size` значит ровно то же, что у куба, и делить Y не нужно.
            // Плотному куску коллайдер даёт КОРОБКА, а не меш: у блока она точна по построению, а
            // выпуклый MeshCollider стоил бы пересчёта на каждой пересборке тела
            cube = new GameObject("блок", typeof(MeshFilter), typeof(MeshRenderer));
            cube.GetComponent<MeshFilter>().sharedMesh = blockMesh;
            cube.GetComponent<MeshRenderer>().sharedMaterial = PrimitiveMaterial();
            if (solid) cube.AddComponent<BoxCollider>();
        }
        else
        {
            // КАПСУЛА И ЦИЛИНДР у Unity ВДВОЕ ВЫШЕ куба при том же масштабе (высота примитива 2, диаметр 1).
            // Делим Y, чтобы `scale` во всех данных значил ОДНО И ТО ЖЕ — габарит куска, а не масштаб примитива
            if (shape == PartShape.Capsule || shape == PartShape.Cylinder) size.y *= 0.5f;

            cube = GameObject.CreatePrimitive(shape == PartShape.Sphere ? PrimitiveType.Sphere
                                            : shape == PartShape.Capsule ? PrimitiveType.Capsule
                                            : shape == PartShape.Cylinder ? PrimitiveType.Cylinder
                                            : PrimitiveType.Cube);
        }
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
