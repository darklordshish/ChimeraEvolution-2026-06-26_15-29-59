namespace Chimera.Laws
{
    /// <summary>
    /// Логово в игровых часах (Ф-17). Игра тикает логово реальными секундами (`respawnSeconds`, `fearDecayPerSecond`);
    /// симулятор живёт в игровых часах. Перевод — здесь, один раз: `hoursPerSecond` отдаёт `WorldClock.HoursPerSecond`
    /// (закон на движок не ссылается). Темп рантайма не меняется — меняется только единица, в которой его читают.
    /// </summary>
    public static class LairLaw
    {
        /// <summary>Период рождения, игровых часов, из секунд.</summary>
        public static float RespawnHours(float respawnSeconds, float hoursPerSecond) => respawnSeconds * hoursPerSecond;

        /// <summary>Спад страха за игровой час из спада за секунду.</summary>
        public static float FearDecayPerHour(float decayPerSecond, float hoursPerSecond)
            => hoursPerSecond > 0f ? decayPerSecond / hoursPerSecond : 0f;

        /// <summary>Обратный перевод: реальных секунд на период в игровых часах.</summary>
        public static float RespawnSeconds(float respawnHours, float hoursPerSecond)
            => hoursPerSecond > 0f ? respawnHours / hoursPerSecond : 0f;
    }
}
