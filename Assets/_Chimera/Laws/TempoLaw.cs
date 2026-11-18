namespace Chimera.Laws
{
    /// <summary>Числа приёма, какими их читает закон темпа: замах, перезарядка и цена стамины из записи органа (`AbilityData`).</summary>
    public struct TempoRecord
    {
        public float windup, cooldown, staminaCost;
    }

    /// <summary>
    /// Закон темпа приёма (Ф-9). Игра живёт на своих таймерах `Time.time` — закон считает ТОЛЬКО числа, а таймеры остаются у
    /// доставки: она зовёт `ReadyAt`/`WindupEnd` при запуске и `ReadyAfterCancel` при срыве замаха. Тем же законом
    /// симулятор получает период атаки, который в игре нигде не записан, а возникает из двух таймеров (перезарядка записи и
    /// ритм психики `attackCooldown`). Ритм психики — тактика психики, закон его не хранит, а получает аргументом.
    /// </summary>
    public static class TempoLaw
    {
        /// <summary>Сорванный замах стоит долю перезарядки: пауза видна, беспомощности нет (прежнее `cancelledCooldownShare`).</summary>
        public const float CancelledCooldownShare = 0.25f;

        /// <summary>Период атаки: не чаще перезарядки записи и не чаще ритма психики, плюс замах.</summary>
        public static float Period(in TempoRecord record, float psycheRhythm)
            => LawMath.Max(record.cooldown, psycheRhythm) + record.windup;

        /// <summary>Когда приём будет готов снова: перезарядка идёт с запуска.</summary>
        public static float ReadyAt(float now, float cooldown) => now + cooldown;

        /// <summary>Когда кончится замах.</summary>
        public static float WindupEnd(float now, float windup) => now + windup;

        /// <summary>Готовность после срыва замаха: не позже, чем доля перезарядки от сейчас (раньше — как было).</summary>
        public static float ReadyAfterCancel(float readyAt, float now, float cooldown, float share)
            => LawMath.Min(readyAt, now + cooldown * share);
    }
}
