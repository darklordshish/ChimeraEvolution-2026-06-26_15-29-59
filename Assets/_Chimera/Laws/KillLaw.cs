using System;
using System.Collections.Generic;

namespace Chimera.Laws
{
    /// <summary>Итог трапезы после убийства: кого и чем кормить (`CreatureBody.CreditKiller`).</summary>
    public readonly struct MealResult
    {
        /// <summary>Убийца ест мясо: ему положена трапеза.</summary>
        public readonly bool Fed;
        /// <summary>Доля шкалы сытости убийце (1 — на всю, у глотающего целиком).</summary>
        public readonly float Killer;
        /// <summary>Доля КАЖДОМУ сородичу стаи рядом; 0 — тушу не делят (искать стаю не нужно).</summary>
        public readonly float Pack;

        public MealResult(bool fed, float killer, float pack) { Fed = fed; Killer = killer; Pack = pack; }
    }

    /// <summary>
    /// Закон убийства (Ф-4): родство убийцы по составу трупа и сытость. Поиск стаи (`Physics.OverlapSphere`) остаётся в игре:
    /// закон говорит, нужна ли стая и сколько ей, а кто в неё входит — решает сцена. float и MathF, округление к чётному.
    /// </summary>
    public static class KillLaw
    {
        /// <summary>Прибавки родства убийце: +1 за шасси и за каждый надетый орган (по виду органа) × `perOrgan`, не меньше 1.
        /// Порядок — по первому появлению вида в составе. `organSpecies` — виды надетых органов (без пустых слотов).</summary>
        public static List<KeyValuePair<string, int>> Tally(string chassisSpecies, IReadOnlyList<string> organSpecies, float perOrgan)
        {
            var count = new Dictionary<string, int>();
            var order = new List<string>();
            if (chassisSpecies != null) { count[chassisSpecies] = 1; order.Add(chassisSpecies); }
            if (organSpecies != null)
                for (int i = 0; i < organSpecies.Count; i++)
                {
                    string sp = organSpecies[i];
                    if (sp == null) continue;
                    if (count.TryGetValue(sp, out int c)) count[sp] = c + 1;
                    else { count[sp] = 1; order.Add(sp); }
                }
            var gains = new List<KeyValuePair<string, int>>(order.Count);
            foreach (string sp in order)
                gains.Add(new KeyValuePair<string, int>(sp, Math.Max(1, (int)MathF.Round(count[sp] * perOrgan))));
            return gains;
        }

        /// <summary>Сытость: только хищнику. Глотающий целиком — полная шкала, прочие — `killMeal`; массивная туша кормит
        /// и стаю половиной трапезы.</summary>
        public static MealResult Meal(bool killerEatsMeat, bool digestsWhole, bool victimMassive, float killMeal)
        {
            if (!killerEatsMeat) return new MealResult(false, 0f, 0f);
            return new MealResult(true, digestsWhole ? 1f : killMeal, victimMassive ? killMeal * 0.5f : 0f);
        }
    }
}
