namespace Chimera.Laws
{
    /// <summary>
    /// Закон скорости хода (Ф-13): `moveSpeed` тела × ярость × разброс особи × замедление × вигор. Пять психик считали это
    /// произведение каждая своим кодом; теперь оно одно. Порядок множителей прежний — слева направо, бит в бит.
    /// </summary>
    public static class MoveLaw
    {
        /// <summary>`rageApplies = false` — ярость в скорость не входит. Так сегодня у змеи и химеры-альфы: паритет держим как
        /// есть, расхождение с волком, лосем и ежом записано в отчёте С5 (решение о выравнивании — за геймдизайнером).
        /// `slow` — любой множитель торможения (иглы, отдышка); `vigor` — истощение. Отсутствующий множитель — единица.</summary>
        public static float Speed(float baseSpeed, float rage, float variance, float slow = 1f, float vigor = 1f, bool rageApplies = true)
            => baseSpeed * (rageApplies ? rage : 1f) * variance * slow * vigor;
    }
}
