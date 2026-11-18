namespace Chimera.Laws
{
    /// <summary>
    /// Закон скорости хода (Ф-13): `moveSpeed` тела × ярость × разброс особи × замедление × вигор. Пять психик считали это
    /// произведение каждая своим кодом; теперь оно одно. Порядок множителей прежний — слева направо, бит в бит.
    /// </summary>
    public static class MoveLaw
    {
        /// <summary>Ярость ускоряет ВСЕХ (решение 18.11, спека симулятора §0 п.10: `Rage` = «+урон +скорость, −защита»; у змеи и
        /// химеры-альфы она раньше не входила — расхождение копий, не замысел). `slow` — любой множитель торможения (иглы,
        /// отдышка); `vigor` — истощение. Отсутствующий множитель — единица.</summary>
        public static float Speed(float baseSpeed, float rage, float variance, float slow = 1f, float vigor = 1f)
            => baseSpeed * rage * variance * slow * vigor;
    }
}
