using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ПАМЯТЬ БОССА (спека вечной эволюции 2026-11-12, §5 п.1.6): при смерти или уходе босса записывается его состав —
/// тип, органы по слотам, родство; следующий спавн того же типа собирается ИЗ ЗАПИСИ, а не заново из доноров игрока.
/// Босс возвращается тем, во что эволюционировал. Статика на сессию (до выхода из игры), как `SuperBossReward.claimed`.
///     Состав пишется по индексам слотов: шасси босса всегда человек, слоты одного шасси идут в одном порядке, химерные —
/// в конце. Применяется тем же публичным API конструктора (`Install`, `GrantChimeraSlot`), своего пути сборки нет.
/// </summary>
public static class BossMemory
{
    [System.Serializable]
    public struct Entry
    {
        public int index;          // номер слота в теле
        public bool chimera;       // слот химерный (выдан сверх нормы)
        public string slot, species, organName;
    }

    public class Record
    {
        public string typeId;            // тип босса («Босс-химера» / «Босс-оборотень»)
        public string displayName;       // имя при рождении («Оборотень (Лось)»)
        public string wereSpecies;       // вид оборотня; пусто у случайной химеры
        public int chimeraSlots;         // сколько химерных слотов было
        public List<Entry> entries = new();
        public Dictionary<string, int> affinity = new();
        public int deaths;               // сколько раз тип уже записывали
        public double hoursAtRecord;     // игровые часы записи (WorldClock)
        public string Description;       // состав одной строкой — лог и отчёты
    }

    static readonly Dictionary<string, Record> records = new();

    public static IReadOnlyCollection<Record> All => records.Values;
    public static bool TryGet(string typeId, out Record record) => records.TryGetValue(typeId ?? "", out record);
    public static void Clear() => records.Clear(); // тестам; в игре память живёт до выхода

    /// <summary>Записать состав босса `body` под типом `typeId` (перезаписывает прежнюю запись типа: помнится последнее).</summary>
    public static Record Capture(string typeId, string displayName, string wereSpecies, CreatureBody body)
    {
        if (body == null || string.IsNullOrEmpty(typeId)) return null;
        var r = new Record
        {
            typeId = typeId, displayName = displayName, wereSpecies = wereSpecies,
            chimeraSlots = body.ChimeraSlots, hoursAtRecord = WorldClock.Now,
            Description = ChimeraFactory.Describe(body),
        };
        if (records.TryGetValue(typeId, out var old)) r.deaths = old.deaths;
        r.deaths++;
        for (int i = 0; i < body.SlotCount; i++)
        {
            var v = body.GetSlot(i);
            if (!v.installed) continue;
            r.entries.Add(new Entry { index = i, chimera = v.chimera, slot = v.slot, species = v.species, organName = v.organName });
        }
        void Aff(SpeciesSO s) { if (s != null) r.affinity[s.speciesName] = body.GetAffinity(s.speciesName); }
        Aff(body.Chassis);
        if (body.Donors != null) foreach (var d in body.Donors) Aff(d);
        records[typeId] = r;
        return r;
    }

    /// <summary>Применить запись к свежесобранному телу (шаг `compose` фабрики): химерные слоты, органы, родство.
    /// Возвращает, сколько записанных органов легло. Не легло — норма только если вариант пропал из данных.</summary>
    public static int Apply(CreatureBody body, Record r)
    {
        if (body == null || r == null) return 0;
        while (body.ChimeraSlots < r.chimeraSlots) body.GrantChimeraSlot();
        int placed = 0;
        foreach (var e in r.entries)
        {
            for (int guard = 0; e.index >= body.SlotCount && guard < 32; guard++) body.GrantChimeraSlot(); // запись длиннее тела — слотов не хватило
            var vars = body.GetVariants(e.index);
            for (int v = 0; v < vars.Count; v++)
            {
                if (vars[v].species != e.species || vars[v].organName != e.organName) continue;
                if (vars[v].worn || body.Install(e.index, v)) { placed++; break; }
            }
        }
        foreach (var kv in r.affinity) body.SetAffinity(kv.Key, kv.Value);
        return placed;
    }
}
