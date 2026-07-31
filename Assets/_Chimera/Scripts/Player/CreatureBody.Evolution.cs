using System.Collections.Generic;
using UnityEngine;

// ЭВОЛЮЦИЯ (стохастическая химеризация): убийца с шансом = родство надевает орган жертвы. Родство — единая
// ось (donors=все уже стоят из Awake). Зовётся из CreditKiller (this=жертва, killer=убийца). Partial-модуль.
public partial class CreatureBody
{
    /// <summary>Стохастически надеть убийце орган ИЗ СОСТАВА жертвы (this). Шанс = родство убийцы к виду
    /// органа × конфиг. Влезет по пулу (Install гейтит) — надел. Grant по СЛУЧАЙНОМУ надетому органу жертвы.
    /// Ветка вида вне EvolutionConfig.AllSpecies недоступна убийце (нет донора) — грант просто не найдёт слот.</summary>
    public void TryChimerize(CreatureBody killer)
    {
        var cfg = EvolutionConfig.Instance;
        if (killer == null || slots == null || cfg == null || !cfg.EvolveNpc) return;

        // ВСЕ надетые органы жертвы (вид+орган): и РОДНЫЕ (native — волчий Коготь у чистого волка), и химерные.
        // Только Installed нельзя — чистая жертва (одни родные органы) дала бы пустой loot, и эволюция не стартовала.
        // chassisOnly (ходовая часть) исключаем — её аугументом не крадут (нет в вариантах убийцы).
        var loot = new List<(string species, string organ)>();
        foreach (var sl in slots)
            if (!sl.Empty && sl.Pick != null && sl.Pick.species != null && sl.Worn != null && !sl.Worn.chassisOnly)
                loot.Add((sl.Pick.species, sl.Worn.organName));
        if (loot.Count == 0) return;

        var pick = loot[Random.Range(0, loot.Count)];
        float chance = (killer.GetAffinity(pick.species) / 100f) * cfg.ChimerizeMultiplier; // РОДСТВО-ПРОЦЕНТ (0..100 → 0..1) × множитель конфига
        if (Random.value > chance) return; // не выпало

        // найти у убийцы слот+вариант этого органа (donors=все → вариант присутствует) и надеть
        for (int s = 0; s < killer.SlotCount; s++)
        {
            var vars = killer.GetVariants(s);
            for (int v = 0; v < vars.Count; v++)
                if (vars[v].species == pick.species && vars[v].organName == pick.organ)
                {
                    if (killer.Install(s, v)) Debug.Log($"химеризация: {killer.name} надел {pick.organ} ({pick.species})");
                    return; // один орган за убийство
                }
        }
    }
}
