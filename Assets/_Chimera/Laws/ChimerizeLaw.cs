using System.Collections.Generic;

namespace Chimera.Laws
{
    /// <summary>Орган из состава жертвы: вид, имя и родство убийцы к виду (0..100) — игра считывает его с тела убийцы.</summary>
    public readonly struct LootEntry
    {
        public readonly string Species;
        public readonly string Organ;
        public readonly int KillerAffinity;

        public LootEntry(string species, string organ, int killerAffinity) { Species = species; Organ = organ; KillerAffinity = killerAffinity; }
    }

    /// <summary>
    /// Закон химеризации (Ф-5): какой орган жертвы пытается надеть убийца, шанс и влезание в пул. Порядок обращений к
    /// случайности — как в прежнем `TryChimerize`: сначала `Range(0, n)`, потом `Next01()`; пустой лут случайности не тратит.
    /// </summary>
    public static class ChimerizeLaw
    {
        /// <summary>Шанс надеть орган: родство-процент (0..100 → 0..1) × множитель конфига.</summary>
        public static float Chance(int killerAffinity, float multiplier) => (killerAffinity / 100f) * multiplier;

        /// <summary>Бросок: индекс выбранного органа в `loot` или -1 (лут пуст / не выпало).</summary>
        public static int Roll(IReadOnlyList<LootEntry> loot, float multiplier, ILawRandom rnd)
        {
            if (loot == null || loot.Count == 0) return -1;
            int pick = rnd.Range(0, loot.Count);
            float chance = Chance(loot[pick].KillerAffinity, multiplier);
            if (rnd.Next01() > chance) return -1;
            return pick;
        }

        /// <summary>Влезает ли вариант в пул: слот отдаёт цену текущего органа, берёт цену нового; один и тот же орган в
        /// двух слотах не носят (`wornElsewhere`).</summary>
        public static bool Fits(int poolUsed, int slotCost, int variantCost, int pool, bool wornElsewhere)
            => poolUsed - slotCost + variantCost <= pool && !wornElsewhere;
    }
}
