using System;

namespace Chimera.Laws
{
    /// <summary>
    /// Закон родства (Ф-3, Ф-15): мощь органа 1..2 от родства, скидка на цену, цена в пуле, ёмкость пула. Числа кривых
    /// закон НЕ хранит — он получает их параметрами; игра подаёт из `[SerializeField]` на теле (префаб/сцена).
    /// float и MathF, порядок операций как в прежних формулах `CreatureBody`.
    /// </summary>
    public static class AffinityLaw
    {
        /// <summary>Множитель экспрессии звериной части: у NPC фикс `expression` (&gt;0), иначе кривая родства линейно
        /// от `startAffinity` (1) до `fullAffinity` (maxMult). Прежний `BonusMultiplier`.</summary>
        public static float Multiplier(float expression, int affinity, float startAffinity, float fullAffinity, float maxMult)
        {
            if (expression > 0f) return expression;
            float span = LawMath.Max(1f, fullAffinity - startAffinity);
            float t = LawMath.Clamp01((affinity - startAffinity) / span);
            return LawMath.Lerp(1f, maxMult, t);
        }

        /// <summary>МОЩЬ носителя (`CreatureBody.Power`): у игрока множитель родства, у NPC — фикс. экспрессия.</summary>
        public static float Power(bool isPlayer, float bonusMult, float expression) => isPlayer ? bonusMult : expression;

        /// <summary>Скидка на цену органа от родства с его видом.</summary>
        public static float Discount(int affinity, float perAffinity, float maxDiscount)
            => LawMath.Clamp(affinity * perAffinity, 0f, maxDiscount);

        /// <summary>Цена органа со скидкой, не меньше 1 (`EffectiveCost`).</summary>
        public static int EffectiveCost(int organCost, float discount)
            => Math.Max(1, (int)MathF.Ceiling(organCost * (1f - discount)));

        /// <summary>Цена варианта в слоте: химерный слот — «графт», дороже в `chimeraMult` раз (`CostOf`).</summary>
        public static int SlotCost(int effectiveCost, bool chimeraSlot, float chimeraMult)
            => (int)MathF.Ceiling(effectiveCost * (chimeraSlot ? chimeraMult : 1f));

        /// <summary>Ёмкость пула мутагена: пул шасси плюс рантайм-расширение наградами.</summary>
        public static int Pool(int chassisPool, int bonus) => chassisPool + bonus;
    }
}
