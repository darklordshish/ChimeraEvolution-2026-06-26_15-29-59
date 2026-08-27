using System.Collections.Generic;

/// <summary>СЛОВАРЬ СЛОТОВ — единственное место, где записано, какие слоты вообще бывают.
///
/// ЗАЧЕМ. Имя слота — это связка трёх вещей сразу: `Organ.slot` (что можно надеть), `BodySocket.name`
/// (где это видно) и `Bone.socket` (какие кости принадлежат части). Связка держится на СТРОКЕ, и до
/// сих пор эта строка жила по факту в данных каждого вида да ещё в восьми местах кода. Опечатка в ней
/// не даёт ни ошибки, ни предупреждения: орган просто не находит своё место и молча становится
/// невидимым, а найти это можно лишь глазами на скриншоте.
///
/// ЧЕГО ЭТОТ СЛОВАРЬ НЕ ДЕЛАЕТ — не пытается свести пять видов к одному плану тела. Универсален
/// СЛОВАРЬ и смысл слота, а граф остаётся видовым: у змеи нет хребта вовсе, корень её тела — голова,
/// а позвоночник и есть тело-цепь. «Человек минус ноги» из неё не получается, поэтому единая топология
/// с видовыми ограничениями была бы натягиванием одного графа на пять разных.</summary>
public static class BodySlots
{
    /// <summary>Чем слот является в конструкторе.</summary>
    public enum Kind
    {
        /// <summary>НЕСУЩЕЕ ШАССИ: аугументом не крадётся (закон «локомоция = свойство шасси»).
        /// В доноры и химерный слот не попадает — помечено `Organ.chassisOnly`.</summary>
        Chassis,
        /// <summary>БАЗОВЫЙ слот легконогого шасси: есть у большинства видов, обменивается.</summary>
        Base,
        /// <summary>ПРИДАТОК: аддитивный слот, дорастает. Игроку достаётся химерным слотом.</summary>
        Appendage,
    }

    // ── СЛОТЫ ─────────────────────────────────────────────────────────────────────────────────────
    // Константы, а не голые строки: имя слота ссылается из кода механик, и опечатка тут перестаёт
    // компилироваться вместо того, чтобы молча гасить часть тела
    public const string Spine = "хребет";       // несущая ось; форму даёт орган «Хребет»
    public const string Body = "Тело";          // ходовая безногого шасси (змея) — вместо Ног
    public const string Rattle = "Погремушка";  // хвостовой сигнал змеи
    public const string Maw = "Пасть";          // атака ртом: укус, вой, рёв, клич
    public const string Sense = "Чутьё";        // чувства: глаза, уши, нос, термоямки
    public const string Heart = "Сердце";       // витальность; внутреннее место
    public const string Hide = "Шкура";         // покров, броня, иглы
    public const string Arms = "Руки";          // передние конечности
    public const string Legs = "Ноги";          // задние/ходовые конечности
    public const string Tail = "Хвост";         // придаток: обхват
    public const string Horns = "Рога";         // придаток: фронтальный удар
    public const string Quiller = "Игломёт";    // придаток: дальний бой

    /// <summary>Все слоты и их род. Порядок — от несущего к придаткам, как в конструкторе.</summary>
    public static readonly IReadOnlyDictionary<string, Kind> All = new Dictionary<string, Kind>
    {
        { Spine,   Kind.Chassis },
        { Body,    Kind.Chassis },
        { Rattle,  Kind.Chassis },
        { Maw,     Kind.Base },
        { Sense,   Kind.Base },
        { Heart,   Kind.Base },
        { Hide,    Kind.Base },
        { Arms,    Kind.Base },
        { Legs,    Kind.Base },
        { Tail,    Kind.Appendage },
        { Horns,   Kind.Appendage },
        { Quiller, Kind.Appendage },
    };

    // ── ТЕЛЕСНЫЕ МЕСТА ────────────────────────────────────────────────────────────────────────────
    // Место, у которого НЕТ одноимённого слота: надеть туда нечего, оно только адрес и калибр для
    // детей. Список отдельный именно поэтому — смешать их со слотами значило бы разрешить органу
    // «сесть на шею», а такого слота в конструкторе нет
    public static readonly IReadOnlyList<string> Places = new[]
    {
        "голова", "шея", "глаза", "уши", "нос", "ямки", "горб",
    };

    public static bool IsSlot(string name) => !string.IsNullOrEmpty(name) && All.ContainsKey(name);

    public static bool IsPlace(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var p in Places) if (p == name) return true;
        return false;
    }

    /// <summary>Известно ли имя вообще — слот это или телесное место.</summary>
    public static bool IsKnown(string name) => IsSlot(name) || IsPlace(name);

    /// <summary>Род слота; для телесного места и неизвестного имени вернёт false.</summary>
    public static bool TryKind(string name, out Kind kind)
    {
        if (!string.IsNullOrEmpty(name) && All.TryGetValue(name, out kind)) return true;
        kind = Kind.Base;
        return false;
    }
}
