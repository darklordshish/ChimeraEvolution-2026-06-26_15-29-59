using System.Collections.Generic;
using UnityEngine;

// PARTIAL-SPLIT #2 (рефактор ядра): РОДСТВО (аффинити) вынесено сюда из CreatureBody.cs — тот же класс,
// НОЛЬ изменений поведения (partial делит поля/методы). Концерн: «привыкание к виду, копится убийствами» —
// хранилище (словарь per-тело) + кривая СКИДКИ (потребитель EffectiveCost остался в ядре, экономика) +
// кривая МОЩИ (BonusMultiplier→Power, потребитель Express остался в ядре, экспрессия). Родство ЛОКАЛЬНОЕ.
public partial class CreatureBody
{
    // РОДСТВО — ЕДИНАЯ РАВНОМЕРНАЯ КРИВАЯ 0→100 (решение пользователя): и скидка, и мощь растут ЛИНЕЙНО
    // на всём диапазоне. Прежде было ступенчато (скидка 0–80, мощь только 80–100) — и грайнд 0–80 не давал
    // силы вовсе, лишь дешевизну. Теперь КАЖДОЕ убийство делает орган и дешевле, И сильнее — прогресс ровный.
    // Концы те же: 0 родства = полная цена ×1 мощь, 100 = −80% цена ×2 мощь
    [SerializeField] float discountPerAffinity = 0.008f; // −0.8% за единицу → −80% на 100 родства
    [SerializeField] float maxDiscount = 0.8f;
    [SerializeField] float bonusStartAffinity = 0f;      // мощь копится С НУЛЯ (было 80 — «мёртвая зона» силы)
    [SerializeField] float bonusFullAffinity = 100f;
    [SerializeField] float maxBonusMult = 2f;            // звериная часть органа ×2 на 100 родства

    // НАБОР РОДСТВА: убийце капает +1 за шасси и КАЖДЫЙ орган трупа (по виду органа) × этот вес. Цель —
    // ~25 killов чистого вида до капа (пёстрый ~7-8 органов × 0.55 ≈ +4/килл, 100/4 = 25). Тюним под ощущение.
    // 0 = не настроено (гоча нового поля на старом компоненте) → дефолт свойством
    [SerializeField] float affinityPerOrgan = 0.55f;
    float AffinityPerOrgan => affinityPerOrgan > 0f ? affinityPerOrgan : 0.55f;

    public const int AffinityCap = 100; // потолок родства на вид (дальше некуда: скидка и мощь на полке)
    int lastAffinitySum = -1;

    // РОДСТВО — ЛОКАЛЬНОЕ, в теле КАЖДОГО существа (не в глобальном трекере: «ЦНС» игры не грузим).
    // Все звери мира — эксперименты с сывороткой (или съевшие их) → родство = база любого тела.
    // Задел эволюции химер-NPC: змея, съевшая волков, копит родство-волк (пока ничего с ним не делает).
    readonly Dictionary<string, int> affinity = new();

    /// <summary>МОЩЬ носителя — ось экспрессии как ЧИСЛО: у игрока множитель родства (1..2), у NPC —
    /// фикс. экспрессия вида (волк 0.45, вервольф 2). Общая ручка: на неё вешаются и масштабы (радиус
    /// голоса), и ПОРОГИ-ФИЧИ («что вообще открывается»). Переиспользуемо химерами и новыми видами.</summary>
    public float Power => move != null ? BonusMult : expression;

    public int GetAffinity(string species) { affinity.TryGetValue(species, out int v); return v; }
    public void AddAffinity(string species, int n) => affinity[species] = Mathf.Clamp(GetAffinity(species) + n, 0, AffinityCap);
    public void SetAffinity(string species, int v) => affinity[species] = Mathf.Clamp(v, 0, AffinityCap);
    public IEnumerable<KeyValuePair<string, int>> AllAffinity => affinity;

    public float BonusMult => donors != null && donors.Length > 0 && donors[0] != null ? BonusMultiplier(donors[0].speciesName) : 1f;

    // ЭКСПРЕССИЯ звериной части: у игрока РАВНОМЕРНО ×1 (родство 0) → maxBonusMult (родство 100), линейно
    // на всём диапазоне (мутаген раскрывает гены с каждым убийством). У NPC — фиксированная (поле expression).
    float BonusMultiplier(string species)
    {
        if (expression > 0f) return expression;
        float span = Mathf.Max(1f, bonusFullAffinity - bonusStartAffinity);
        float t = Mathf.Clamp01((GetAffinity(species) - bonusStartAffinity) / span);
        return Mathf.Lerp(1f, maxBonusMult, t);
    }

    // сумма родства по донорам — сторож пересчёта (Update): родство выросло → Recompute
    int AffinitySum()
    {
        int s = 0;
        if (donors != null) foreach (var d in donors) if (d != null) s += GetAffinity(d.speciesName);
        return s;
    }
}
