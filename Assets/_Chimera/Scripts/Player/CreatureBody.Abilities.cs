using System;
using System.Collections.Generic;
using UnityEngine;

// СПОСОБНОСТИ ОРГАНОВ (спека `2026-09-12-dannye-v-organah.md`): тело раскрывает записи надетых органов тем же законом
// экспрессии, что и остальные числа органа (см. Express), сводит дубли супремумом и кормит носителей приёмов.
// Один путь для игрока и NPC — разница только в том, КТО исполняет запись: грань управления или доставка.
public partial class CreatureBody
{
    readonly Dictionary<Type, AbilityData> abilities = new(); // раскрытые записи этого тела по типу приёма

    /// <summary>Раскрытая запись приёма (после экспрессии и супремума); null — у тела этого приёма нет.</summary>
    public T Ability<T>() where T : AbilityData => abilities.TryGetValue(typeof(T), out var d) ? (T)d : null;

    void ProvisionAbilities()
    {
        abilities.Clear();
        foreach (var sl in slots)
        {
            if (sl.Empty || sl.Worn.abilities == null) continue;
            var pick = sl.Pick;
            float power = BonusMultiplier(pick.species);                                 // та же мощь, что у чисел органа
            Organ displaced = pick.native || sl.chimera ? null : ChassisOrgan(sl.name);   // вытесненный родной орган слота
            // ДОМ ОРГАНА (правило спеки 2a): родное шасси не задано — гейта нет; задано — дом, если совпало с шасси тела
            bool home = string.IsNullOrEmpty(sl.Worn.nativeChassis) || (chassis != null && sl.Worn.nativeChassis == chassis.speciesName);
            foreach (var record in sl.Worn.abilities)
            {
                if (record == null) continue;                                             // пустой элемент массива в инспекторе
                if (record.NativeOnly && !home) continue;                                 // ДОМАШНИЙ ПРИЁМ: только на родном шасси (см. AbilityData.NativeOnly)
                var type = record.GetType();
                var resolved = record.Resolve(RecordOf(displaced, type), pick.native, power);
                if (!home) resolved.OnForeignChassis();                                   // В ГОСТЯХ: приём режет себя сам (захват — кап стадии)
                abilities[type] = abilities.TryGetValue(type, out var prev) ? AbilityData.Sup(prev, resolved) : resolved;
            }
        }

        bool isPlayer = move != null;
        foreach (var kv in abilities)
        {
            var carrier = isPlayer ? kv.Value.PlayerCarrier : kv.Value.NpcCarrier;
            if (carrier == null) continue;                                                // у этой стороны приёма нет
            if (!TryGetComponent(carrier, out var comp)) comp = gameObject.AddComponent(carrier);
            ((IOrganAbility)comp).Configure(kv.Value);
        }

        // НЕТ ЗАПИСИ — НЕТ ПРИЁМА. Компонент не сносим, а гасим: на него держат ссылки психика и драйвер ввода,
        // а Destroy отложен до конца кадра (гоча проекта) — выключенный носитель надёжнее висячей ссылки
        foreach (var c in GetComponents<IOrganAbility>())
            if (!abilities.ContainsKey(c.DataType)) c.Configure(null);
    }

    static AbilityData RecordOf(Organ organ, Type type)
    {
        if (organ?.abilities == null) return null;
        foreach (var r in organ.abilities) if (r != null && r.GetType() == type) return r;
        return null;
    }
}
