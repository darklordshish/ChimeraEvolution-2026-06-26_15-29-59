using System;

namespace Chimera.Laws
{
    /// <summary>Когда запись рациона действует: всегда или только голодному (`HungerLaw.IsHungry`).</summary>
    public enum DietCondition { Always = 0, WhenHungry = 1 }

    /// <summary>Как хищник берёт эту добычу. `Prey` — обычная добыча, её психика выбирает целью сама (волк — змею и ежа, ёж —
    /// змею, лось — волка); `Shadow` — массивная туша, на которую стая идёт тенью-загоном (волк — лося). Два режима потому, что
    /// это две разные ветки психики с разным радиусом и условием стаи: один список «кто кого» их не склеивает.</summary>
    public enum DietMode { Prey = 0, Shadow = 1 }

    /// <summary>Запись рациона вида (план Ф0, С8): добыча — по ИМЕНИ ВИДА-ДОМИНАНТЫ цели (`CreatureBody.MostKin`, то же, по чему
    /// `PsycheDispatch` вешает психику), условие, режим и вес встречи (множитель в частоте столкновений симулятора; игра читает
    /// только «больше нуля»).</summary>
    [Serializable]
    public struct DietEntry
    {
        public string prey;
        public DietCondition condition;
        public DietMode mode;
        public float weight;

        public DietEntry(string prey, DietCondition condition = DietCondition.Always, DietMode mode = DietMode.Prey, float weight = 1f)
        { this.prey = prey; this.condition = condition; this.mode = mode; this.weight = weight; }
    }

    /// <summary>
    /// Закон рациона (Ф-14): «хочет ли хищник эту цель» — по доминанте цели и своему голоду, без движка. Пространственный
    /// поиск, камуфляж, насест и размер стаи остаются в психиках («где и видно ли», а не «кто кого»). Истинная химера
    /// (доминанты нет, `targetDominant == null`) по рациону ничья добыча.
    /// </summary>
    public static class DietLaw
    {
        /// <summary>Вес встречи; 0 — не хочет. Берётся наибольший из подходящих записей.</summary>
        public static float Wants(DietEntry[] diet, string targetDominant, bool hungry, DietMode mode)
            => Best(diet, targetDominant, hungry, mode, anyMode: false);

        /// <summary>То же без различения режима (для симулятора: добыча есть добыча).</summary>
        public static float Wants(DietEntry[] diet, string targetDominant, bool hungry)
            => Best(diet, targetDominant, hungry, DietMode.Prey, anyMode: true);

        /// <summary>Бежит ли вид от цели с этой доминантой (змея — от ежа). Пустой список или нет доминанты — нет.</summary>
        public static bool Flees(string[] fleesFrom, string targetDominant)
        {
            if (fleesFrom == null || targetDominant == null) return false;
            foreach (var f in fleesFrom) if (f == targetDominant) return true;
            return false;
        }

        static float Best(DietEntry[] diet, string dominant, bool hungry, DietMode mode, bool anyMode)
        {
            if (diet == null || dominant == null) return 0f;
            float best = 0f;
            foreach (var e in diet)
            {
                if (e.prey != dominant) continue;
                if (!anyMode && e.mode != mode) continue;
                if (e.condition == DietCondition.WhenHungry && !hungry) continue;
                if (e.weight > best) best = e.weight;
            }
            return best;
        }
    }
}
