using System.Collections.Generic;
using UnityEngine;

// ЭВОЛЮЦИЯ (стохастическая химеризация): убийца с шансом = родство надевает орган жертвы. Родство — единая
// ось (donors=все уже стоят из InitEvolution). Зовётся из CreditKiller (this=жертва, killer=убийца). Partial-модуль.
public partial class CreatureBody
{
    // ПУЛ ХИМЕРЫ ДЛЯ NPC. Родные органы вида стоят дороже его же пула (волк: 26 при пуле 16, змея: 24 при 20) —
    // значит у ПРЕФАБНОГО зверя первый же грант отваливался по цене, и эволюция «включалась», но молчала.
    // Рождённое составом тело (`ChimeraFactory`) и раньше жило с расширенным пулом; теперь платформа эволюции
    // даёт тот же запас ВСЕМ, кого ведёт. Число — запас, а не баланс: психики и приёмы его не читают.
    const int EvolutionPoolBonus = 9999;

    bool evolutionReady; // платформа применена (Awake и Configure зовут её оба — второй заход не дублирует)
    bool awakeRan;       // компонент действительно жил в мире: в EditMode-стенде Awake не зовётся

    /// <summary>ПЛАТФОРМА ЭВОЛЮЦИИ NPC: все виды — доноры, стартовое родство, цвет по составу, слушатель
    /// метаморфозы. ОДИН вход на оба пути рождения: `Awake` (префаб — шасси уже сериализовано) и `Configure`
    /// (тело, рождённое составом: в Awake шасси ещё null, его отдаёт Configure следующей строкой). Без второго
    /// захода фабричный NPC оставался без `Metamorph` и без родства — психика не перевешивалась при сдвиге
    /// доминанты, а `Power` равнялся нулю.</summary>
    void InitEvolution()
    {
        if (chassis == null) return;

        // РОДНОЕ — НА КАП («я точно своего вида»): у префаба это делал Awake, фабричному телу шасси дают позже.
        // В EditMode-стенде (Awake не было) не трогаем: там Configure — сборка для замера, а не рождение,
        // и кап родства к шасси сломал бы экономику тестов.
        if (awakeRan) SetAffinity(chassis.speciesName, AffinityCap);
        if (move != null) return; // игрок собирается сборкой, эволюции не подлежит

        var cfg = EvolutionConfig.Instance;
        if (cfg == null || !cfg.EvolveNpc || cfg.AllSpecies == null || cfg.AllSpecies.Length == 0) return;

        // ДОНОРЫ ПЕРЕЖИВАЮТ ПОВТОРНЫЙ Configure: тело могли пересобрать с другим списком — эволюции нужен весь лес
        donors = cfg.AllSpecies; // все виды — потенциальные доноры (родные надеты → чистый вид)
        tintComposition = true;  // цвет по составу (развязка Telegraph.Rebase готова)

        if (evolutionReady) return; // остальное — ровно раз
        evolutionReady = true;
        poolBonus = Mathf.Max(poolBonus, EvolutionPoolBonus); // иначе родные органы вида съедают пул и грант молчит
        foreach (var sp in cfg.AllSpecies)
            if (sp != null && sp != chassis) SetAffinity(sp.speciesName, cfg.StartAffinity); // стартовое родство чужим
        if (GetComponent<Metamorph>() == null) gameObject.AddComponent<Metamorph>(); // ре-диспатч психики при сдвиге доминанты
    }

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
            if (!sl.Empty && sl.Pick != null && sl.Pick.species != null && sl.Worn != null && !sl.Worn.chassisOnly && !sl.Worn.nativeOnly)
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
                    if (killer.Install(s, v) && EvolutionConfig.Verbose) Debug.Log($"химеризация: {killer.name} надел {pick.organ} ({pick.species})");
                    return; // один орган за убийство
                }
        }
    }
}
