using System.Collections.Generic;
using UnityEngine;

/// <summary>ПРАВИЛА ТЕЛА: суждения над данными и замерами. Отдельно от карты, потому что нужны бутстрапу
/// без всякого отчёта. Ругаются ТОЛЬКО на объективные поломки — анатомия сюда не входит: стилизация это
/// решение геймдизайнера, её место в карте справочной таблицей (спека 2026-08-10).
///
/// Пороги — ДОЛИ КАЛИБРА, а не метры: иначе мелкие виды всегда в допуске, а крупные всегда виноваты.</summary>
public static class BodyRules
{
    public const float GapWarn = 0.08f;      // щель больше 8% ТОЛЩИНЫ шва — предупреждение
    // КАСАНИЕ ВПРИТЫК против ПРОСВЕТА. Ноль в стыке недостижим: детали ставятся расчётом, и на сходящихся
    // поверхностях остаются доли миллиметра. Печатать им «не касается» — тот же шум, что ругань на
    // намеренное. Распределение по пяти видам разошлось надвое: 0.000–0.007 м (сходятся) и 0.011–0.015
    // (висят). По долям шва граница легла на 3%
    public const float GapTouch = 0.03f;
    // НАХЛЁСТ БОЛЬШЕ 75% ГЛУБИНЫ ДЕТАЛИ. Порог 0.40 был назначен до фактов, а первый же прогон по здоровым
    // телам дал распределение 40…87% и 21 «врастание» на анатомии, которую глаз принимает. Причина не в
    // телах: шарнирная кукла НАМЕРЕННО сажает детали глубоко друг в друга — иначе на торцах капсул
    // расходятся щели (торец сходится в точку). Глубокое вхождение здесь способ сборки, а не дефект,
    // поэтому осмысленное «врастание» — это «деталь утонула почти целиком», а не «вошла наполовину»
    public const float OverlapWarn = 0.75f;
    public const float DupError = 0.60f;     // пересечение объёмов больше 60% — дубль области
    public const float AxisMargin = 0.05f;   // запас длинной оси меньше 5% — ось вот-вот переключится

    public struct Issue
    {
        public string species, where, text;
        public bool error;
    }

    /// <summary>Проверки по ДАННЫМ вида: то, что видно без сборки.</summary>
    public static List<Issue> CheckData(SpeciesSO s)
    {
        var list = new List<Issue>();
        if (s == null || s.sockets == null) return list;

        var byName = new Dictionary<string, BodySocket>();
        foreach (var k in s.sockets)
            if (k != null && !string.IsNullOrEmpty(k.name)) byName[k.name] = k;

        // ── ПОКРЫТИЕ СЛОВАРЁМ СЛОТОВ (`BodySlots`) ────────────────────────────────────────────────
        // Имя слота — строка, и держит она сразу три вещи: что можно надеть (`Organ.slot`), где это
        // видно (`BodySocket.name`) и какие кости принадлежат части (`Bone.socket`). Опечатка в этой
        // строке не даёт НИ ОДНОЙ ошибки: орган просто не находит своё место и молча исчезает с тела,
        // а ловится это глазами на скриншоте. Словарь превращает молчание в строку отчёта
        foreach (var o in s.organs ?? System.Array.Empty<Organ>())
        {
            if (o == null || string.IsNullOrEmpty(o.slot)) continue;
            if (BodySlots.IsPlace(o.slot))
                list.Add(new Issue
                {
                    species = s.speciesName, where = o.organName, error = true,
                    text = $"орган сидит на ТЕЛЕСНОМ МЕСТЕ «{o.slot}» — надеть туда нечего, " +
                           $"такого слота в конструкторе нет"
                });
            else if (!BodySlots.IsSlot(o.slot))
                list.Add(new Issue
                {
                    species = s.speciesName, where = o.organName, error = true,
                    text = $"слот «{o.slot}» не в словаре `BodySlots` — опечатка либо новый слот, " +
                           $"который надо туда занести"
                });
        }

        foreach (var k in s.sockets)
        {
            if (k == null || string.IsNullOrEmpty(k.name)) continue;
            if (!BodySlots.IsKnown(k.name))
                list.Add(new Issue
                {
                    species = s.speciesName, where = k.name, error = true,
                    text = "имя места не в словаре `BodySlots` — опечатка либо новое место; " +
                           "новое надо занести в словарь, иначе следующая опечатка снова пройдёт молча"
                });
            // ГРАФТ — ФЛАГ СЛОТА, У АДРЕСА ОН БЕССМЫСЛЕН. Проверка симметрична той, что выше ловит
            // орган на телесном месте: там «надеть нечего», здесь «нечему прийти графтом». Телесное
            // место — только адрес и калибр для детей, органа у него нет по определению, поэтому
            // «закрыто до графта» ничего не значит: открывать нечем.
            //     Заведена 11.09 после того, как я предложил завести графтами `горб` и `ямки` —
            // ровно эту ошибку. Данные оказались чисты, правило же нигде не было записано: гайд
            // перечислял примеры (Хвост/Рога/Игломёт), а не говорил, что графт применим лишь к слоту.
            // Ошибка, которую поймал человек и не поймала машина, — заявка на проверку
            if (k.graft && BodySlots.IsPlace(k.name))
                list.Add(new Issue
                {
                    species = s.speciesName, where = k.name, error = true,
                    text = $"ТЕЛЕСНОЕ МЕСТО «{k.name}» помечено `graft` — флаг применим только к слоту: " +
                           $"органа у адреса нет, открывать графтом нечего"
                });
        }

        // ── КЛЕТКА: валидация топологии и Ratio (Ф1) ───────────────────────────────────
        list.AddRange(CheckCages(s));

        foreach (var k in s.sockets)
        {
            if (k == null || string.IsNullOrEmpty(k.name)) continue;

            // ЗАПАС ДЛИННОЙ ОСИ. Ось выбирается по максимальной стороне, и при близких числах молча
            // переключается, разворачивая ВСЮ ветку детей: у человека шея 0.128Y при 0.132Z уронила
            // рост с 1.85 до 1.76, и искали это долго — ошибок нет, просто «голова уехала вперёд».
            //
            // НО СПРАШИВАЕМ ТОЛЬКО ТАМ, ГДЕ ОСЬ НА ЧТО-ТО ВЛИЯЕТ: вдоль неё считается `attach` детей и
            // растёт цепь. У бездетного не-цепного места (глаз-шар, рога-калибр) переключись ось хоть
            // трижды — не сдвинется ничего, и ругань тут приучила бы игнорировать красное.
            // Изотропный калибр — тоже не поломка, а намерение: у шара длинной оси нет по определению
            //
            // СУДИМ ПО РАЗРЕШЁННОМУ РАЗМЕРУ, А НЕ ПО `baseSize`. Сторож стоял не у той двери: ось выбирает
            // сборка по `SizeOf` (свой габарит ЛИБО доля родителя), а правило читало сырое поле — у человека
            // это разные числа у 13 мест из 14. Правка глубины хребта переключила бы реальную ось шеи Y→Z
            // (та самая регрессия «рост 1.85 → 1.76»), а валидатор промолчал бы: он смотрит нетронутое поле.
            // Доказательство инертности рядом: у места «Чутьё» `baseSize` не задан вовсе — метровый куб,
            // и этого никто не замечал, пока размер приходил долей
            var b = MorphBuilder.SizeOf(k, byName);
            bool hasChildren = false;
            foreach (var other in s.sockets)
                if (other != null && other.parent == k.name) { hasChildren = true; break; }
            bool axisMatters = hasChildren || k.linkLength > 0f;

            if (axisMatters)
            {
                float max = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
                float min = Mathf.Min(b.x, Mathf.Min(b.y, b.z));
                float second = 0f;
                if (b.x < max && b.x > second) second = b.x;
                if (b.y < max && b.y > second) second = b.y;
                if (b.z < max && b.z > second) second = b.z;
                bool isotropic = max > 0f && (max / Mathf.Max(0.000001f, min) - 1f) < AxisMargin; // куб/шар
                if (!isotropic && max > 0f && second > 0f && (max / second - 1f) < AxisMargin)
                    list.Add(new Issue
                    {
                        species = s.speciesName, where = k.name, error = true,
                        text = $"запас длинной оси {(max / second - 1f) * 100f:F0}% — ось может молча переключиться " +
                               $"и развернуть ветку детей"
                    });
            }

            // НУЛЕВОЙ КАЛИБР. Деталь схлопнется в плоскость без единой ошибки в консоли
            if (k.linkLength <= 0f && (b.x <= 0f || b.y <= 0f || b.z <= 0f) && k.sizeRel == Vector3.zero)
                list.Add(new Issue
                {
                    species = s.speciesName, where = k.name, error = true,
                    text = $"нулевой калибр ({b.x:F3}, {b.y:F3}, {b.z:F3}) и нет доли родителя"
                });

            // ЦЕПЬ БЕЗ ДИАМЕТРА: ни своего, ни наследуемого от родительской цепи
            if (k.linkLength > 0f && k.linkDiameter <= 0f)
            {
                bool inherits = !string.IsNullOrEmpty(k.parent)
                                && byName.TryGetValue(k.parent, out var par) && par.linkLength > 0f;
                if (!inherits)
                    list.Add(new Issue
                    {
                        species = s.speciesName, where = k.name, error = true,
                        text = "цепь без диаметра: свой не задан, а родитель не цепь — наследовать не от кого"
                    });
            }

            // ПОКОМПОНЕНТНЫЙ НОЛЬ В ДОЛЕ: «оставить эту ось как есть» так не работает — выйдет плоская деталь
            if (k.sizeRel != Vector3.zero && (k.sizeRel.x <= 0f || k.sizeRel.y <= 0f || k.sizeRel.z <= 0f))
                list.Add(new Issue
                {
                    species = s.speciesName, where = k.name, error = true,
                    text = $"доля родителя с нулём по оси ({k.sizeRel.x:F2}, {k.sizeRel.y:F2}, {k.sizeRel.z:F2})"
                });

            // ЦИКЛ В ГРАФЕ. Билдер страхуется глубиной и молчит, но данные всё равно неверны
            var seen = new HashSet<string> { k.name };
            var cur = k;
            int guard = 0;
            while (!string.IsNullOrEmpty(cur.parent) && byName.TryGetValue(cur.parent, out var up) && guard++ < 32)
            {
                if (!seen.Add(up.name))
                {
                    list.Add(new Issue
                    {
                        species = s.speciesName, where = k.name, error = true,
                        text = $"цикл в графе мест через «{up.name}»"
                    });
                    break;
                }
                cur = up;
            }
        }
        return list;
    }

    /// <summary>Проверки по ЗАМЕРАМ: то, что видно только на построенном теле.
    ///
    /// СУДИМ О МЕСТАХ, А НЕ О ДЕТАЛЯХ. Прежняя версия складывала детали в словарь по имени — а имя у
    /// детали одно на всё место, и десять частей змеиной головы схлопывались в последнюю (левую ноздрю).
    /// Сравнение с «родителем» шло тогда с случайной деталью: на змее это давало полтора десятка ложных
    /// ошибок. Валидатор, кричащий на исправное, приучает игнорировать красное — поэтому объёмы мест
    /// считает `BodyProbe.Group`, общий с картой.</summary>
    public static List<Issue> CheckParts(SpeciesSO species, List<BodyProbe.Part> parts)
    {
        var list = new List<Issue>();
        if (species == null || parts == null) return list;

        var pl = BodyProbe.Group(species, parts);

        foreach (var kv in pl.whole)
        {
            string socket = pl.socketOf.TryGetValue(kv.Key, out var sn) ? sn : kv.Key;
            if (!pl.parentOf.TryGetValue(socket, out var rawParent) || string.IsNullOrEmpty(rawParent)) continue;

            string drawn = BodyProbe.DrawnParent(pl, socket);

            // МЕСТО ВИСИТ НА ПУСТОТЕ: по графу родитель есть, а нарисованного предка нет ни на одном
            // уровне вверх. Так жили шея, лапы и хвост, пока несущую анатомию рисовал ПОКРОВ, а хребет
            // числился служебным: примыкать было не к чему, и любой стык держался на совпадении чисел.
            // Теперь форму несущему даёт орган «Хребет» — правило сторожит возврат к прежнему
            if (string.IsNullOrEmpty(drawn))
            {
                // ...НО КОСТЬ — ЗАКОННЫЙ ПРЕДОК. Со скелетом место может висеть не на другом месте, а
                // прямо на кости: хвост сидит на «крестце», голова на «шее». Кость рисует не хуже места,
                // просто её нет в сокет-плане — и правило, не знающее о костях, ругалось на исправное.
                // Валидатор, кричащий на здоровое, приучает игнорировать красное
                if (MorphBuilder.IsBone(species, rawParent)) continue;

                list.Add(new Issue
                {
                    species = species.speciesName, where = kv.Key, error = true,
                    text = $"нет нарисованного предка: по графу висит на «{rawParent}», а тот ничего не рисует — " +
                           $"примыкать физически не к чему"
                });
                continue;
            }

            if (!pl.whole.TryGetValue(drawn, out var par)) continue;
            var me = kv.Value;

            // ВНУТРЕННЕЕ МЕСТО, ВЫЛЕЗШЕЕ ИЗ НОСИТЕЛЯ. Ровно так грудная клетка оказалась шире корпуса и
            // лепила «бочку», а причину мы искали в морде. Спрашиваем ТОЛЬКО с внутренних: голова
            // законно шире шеи, и требовать от неё «помещаться в родителя» значит ругаться на норму
            var inner = System.Array.Find(species.sockets, k => k != null && k.name == socket);
            if (inner == null || !inner.inner) continue;

            if (me.size.x > par.size.x && me.size.y > par.size.y && me.size.z > par.size.z)
                list.Add(new Issue
                {
                    species = species.speciesName, where = kv.Key, error = true,
                    text = $"внутреннее место больше носителя «{drawn}» по всем осям: " +
                           $"{me.size.x:F3}×{me.size.y:F3}×{me.size.z:F3} против {par.size.x:F3}×{par.size.y:F3}×{par.size.z:F3}"
                });
        }
        return list;
    }

    // ── КЛЕТКА: SameTopology + Ratio без метров (SPEC-kletka-tela.md §2, §4 И4) ──────────

    /// <summary>Одинакова ли топология клеток (M×N и число ландмарок) — условие покомпонентного среднего.</summary>
    public static bool SameTopology(CageTable a, CageTable b)
    {
        if (a == null || b == null) return false;
        // 0→дефолт: ненастроенная клетка не участвует в сравнении (фолбэк на кубы)
        if (!a.IsConfigured || !b.IsConfigured) return false;
        return a.SameTopology(b);
    }

    /// <summary>Кросс-видовая проверка: одноимённые слоты обязаны иметь одинаковую топологию M×N.</summary>
    public static List<Issue> CheckCages(SpeciesSO a, SpeciesSO b)
    {
        var list = new List<Issue>();
        if (a == null || b == null) return list;
        if (a.cages == null || b.cages == null) return list;
        var byA = new Dictionary<string, CageTable>();
        foreach (var c in a.cages) if (c != null && c.IsConfigured && !string.IsNullOrEmpty(c.slot)) byA[c.slot] = c;
        foreach (var c in b.cages)
        {
            if (c == null || !c.IsConfigured || string.IsNullOrEmpty(c.slot)) continue;
            if (!byA.TryGetValue(c.slot, out var ca)) continue;
            if (!SameTopology(ca, c))
                list.Add(new Issue
                {
                    species = $"{a.speciesName}↔{b.speciesName}", where = c.slot, error = true,
                    text = $"топология клетки слота «{c.slot}» разошлась: {a.speciesName} {ca.M}×{ca.N} vs {b.speciesName} {c.M}×{c.N} — химера невыразима покомпонентным средним (SPEC §2)"
                });
        }
        return list;
    }

    // ── БЮДЖЕТ Ф6: 324 квада ≈830 трис/сущ. (ADR-1 хребет 8×10, было 310/800), 25 в кадре ≈20.7k. Общая вершинная нагрузка
    public const int BudgetQuads = 324;
    public const int BudgetTrisPerCreature = 830;
    public const int BudgetTris25 = 20750;

    /// <summary>Бюджет клетки: сумма M×N по всем слотам должна укладываться в BudgetQuads (Ф6).</summary>
    public static List<Issue> CheckBudget(SpeciesSO s)
    {
        var list = new List<Issue>();
        if (s == null || s.cages == null) return list;
        int totalQuads = 0;
        foreach (var c in s.cages)
        {
            if (c == null || !c.IsConfigured) continue;
            // МЕЖДУ M СТАНЦИЯМИ ПРОЛЁТОВ M−1, А НЕ M. Квад натянут между СОСЕДНИМИ кольцами, поэтому
            // ряд из M колец даёт (M−1)·N квадов. Формула M·N завышала счёт у каждого слота и при этом
            // не совпадала ни с одной строкой таблицы SPEC §6: там 8×10 → 70, 4×8 → 24, 6×8 → 40.
            int quads = (c.M - 1) * c.N;
            // ПАРНЫЙ СЛОТ СТОИТ ВДВОЕ. Руки/Ноги/уши — ОДИН сокет с mirrorX, и клетка у него одна
            // (дубли запрещены проверкой ниже). Итог 324 в спеке — уже удвоенный, значит удваивать надо здесь,
            // иначе сумма выходит односторонней и показывает запас там, где бюджет уже выбран
            if (IsMirrored(s, c.slot)) quads *= 2;
            totalQuads += quads;
        }
        // пока клетки не у всех слотов — проверяем только заполненные, полный бюджет ждём волка целиком
        if (totalQuads > BudgetQuads)
            list.Add(new Issue { species = s.speciesName, where = "бюджет", error = true, text = $"бюджет клетки {totalQuads} квадов > {BudgetQuads} (на существо ≈{Mathf.RoundToInt(totalQuads * (BudgetTrisPerCreature / (float)BudgetQuads))} трис при бюджете {BudgetTrisPerCreature})" });
        return list;
    }

    /// <summary>Слот парный? Клетка у зеркального сокета одна, а в кадре его две — бюджет это учитывает.</summary>
    static bool IsMirrored(SpeciesSO s, string slot)
    {
        if (s.sockets == null || string.IsNullOrEmpty(slot)) return false;
        foreach (var k in s.sockets)
            if (k != null && k.name == slot) return k.mirrorX;
        return false;
    }

    /// <summary>Внутривидовая валидация клеток: корректность полей и отсутствие дублей.</summary>
    public static List<Issue> CheckCages(SpeciesSO s)
    {
        var list = new List<Issue>();
        if (s == null || s.cages == null) return list;
        var seen = new HashSet<string>();
        foreach (var c in s.cages)
        {
            if (c == null) continue;
            // пустая заглушка 0→дефолт — пропускаем (старый ассет или ненастроенный слот)
            if (!c.IsConfigured && c.M == 0 && c.N == 0 && (c.radii == null || c.radii.Length == 0)) continue;
            if (string.IsNullOrEmpty(c.slot))
            {
                list.Add(new Issue { species = s.speciesName, where = "(клетка)", error = true, text = "клетка без имени слота" });
                continue;
            }
            if (!BodySlots.IsKnown(c.slot))
                list.Add(new Issue { species = s.speciesName, where = c.slot, error = true, text = $"слот клетки «{c.slot}» не в словаре BodySlots" });
            if (!seen.Add(c.slot))
                list.Add(new Issue { species = s.speciesName, where = c.slot, error = true, text = $"дубль клетки слота «{c.slot}»" });
            if (c.M <= 0 || c.N <= 0)
                list.Add(new Issue { species = s.speciesName, where = c.slot, error = true, text = $"клетка «{c.slot}» с нулевым M×N ({c.M}×{c.N}) при заданных radii — размерность потеряна" });
            else if (c.radii == null || c.radii.Length != c.M * c.N)
                // ПРЕДУПРЕЖДЕНИЕ, А НЕ ОШИБКА: по SPEC §7 клетки заполняются ПО СЛОТУ на задачу, и пока
                // вид набирается, недобранный слот — норма. Красное на нормальном ходу работы приучает
                // не смотреть на красное вовсе, а это дороже пропущенной клетки
                list.Add(new Issue { species = s.speciesName, where = c.slot, error = false, text = $"клетка «{c.slot}» radii {c.radii?.Length ?? 0} ≠ M×N {c.M * c.N} — ещё не заполнена?" });
            else
            {
                // ОТРИЦАТЕЛЬНЫЙ РАДИУС — ошибка знака, и это ЖЁСТКО: наружу от оси нельзя на минус.
                //     А вот проверки «> 5 калибров = метры» здесь БОЛЬШЕ НЕТ, и вот почему. Она заводилась
                // ловить нарушение И4 (метры в данных донора), но метры у наших зверей лежат в 0.02…1.5 —
                // то есть ЦЕЛИКОМ внутри её же коридора [0..5]. Сработать она могла лишь на радиусе от пяти
                // метров, каких в игре нет: правило не способно поймать собственную мишень ни на одном
                // мыслимом входе. Настоящая защита И4 структурная — радиус умножается на калибр носителя
                // на выходе (CageTable.BlendWithCaliber), метру там просто негде появиться.
                //     Порог по величине вернётся, когда наберётся распределение реальных клеток: тогда его
                // выведут из фактов, как выведены GapWarn/OverlapWarn, а не назначат с потолка
                for (int i = 0; i < c.radii.Length; i++)
                    if (c.radii[i] < 0f)
                    {
                        list.Add(new Issue { species = s.speciesName, where = c.slot, error = true, text = $"клетка «{c.slot}» radii[{i}]={c.radii[i]:F2} — отрицательный радиус" });
                        break;
                    }
            }
        }
        return list;
    }
}
