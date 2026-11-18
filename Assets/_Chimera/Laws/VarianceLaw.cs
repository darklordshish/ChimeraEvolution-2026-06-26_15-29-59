namespace Chimera.Laws
{
    /// <summary>Множители особи: HP, урон, скорость.</summary>
    public readonly struct VarianceResult
    {
        public readonly float Hp, Damage, Speed;
        public VarianceResult(float hp, float damage, float speed) { Hp = hp; Damage = damage; Speed = speed; }
        public static readonly VarianceResult Average = new VarianceResult(1f, 1f, 1f);
    }

    /// <summary>Закон разброса особи (Ф-16): множитель = 1 + случайное из ±разброс. Порядок бросков — урон, скорость, HP
    /// (как катал `SpawnVariance`): расход случайности совпадает с прежним.</summary>
    public static class VarianceLaw
    {
        public static VarianceResult Roll(float hpSpread, float damageSpread, float speedSpread, ILawRandom rng)
        {
            float damage = Spread(damageSpread, rng);
            float speed = Spread(speedSpread, rng);
            float hp = Spread(hpSpread, rng);
            return new VarianceResult(hp, damage, speed);
        }

        static float Spread(float spread, ILawRandom rng) => 1f + rng.Range(-spread, spread);
    }
}
