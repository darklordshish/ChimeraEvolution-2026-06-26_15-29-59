using System;

namespace Chimera.Laws
{
    /// <summary>
    /// Закон урона (Ф-6, Ф-7, Ф-8). Числа бит в бит с прежними формулами игры: float и MathF, округление к чётному
    /// (как Mathf.RoundToInt), порядок умножений сохранён. Сбор множителей с компонентов остаётся в игре.
    /// </summary>
    public static class DamageLaw
    {
        /// <summary>Входящий урон после брони и множителя защиты (ярость × яд): `Health.TakeDamage`.</summary>
        public static int Incoming(int raw, float reduction, float incomingMult)
            => Math.Max(1, (int)MathF.Round(raw * (1f - reduction) * incomingMult));

        /// <summary>Множитель исходящего урона делателя; отсутствующий статус подаётся единицей.</summary>
        public static float OutgoingMult(float rage, float variance, float morale, float vigor, float bossness)
        {
            float m = 1f;
            m *= rage;
            m *= variance;
            m *= morale;
            m *= vigor;
            m *= bossness;
            return m;
        }

        /// <summary>Целый урон с множителем делателя: `OutgoingDamage.Apply`. Неположительный проходит как есть.</summary>
        public static int Outgoing(int damage, float mult)
            => damage <= 0 ? damage : Math.Max(1, (int)MathF.Round(damage * mult));

        /// <summary>Урон пайка `MeleeBlow` с динамической мощью.</summary>
        public static int Blow(int damage, float damageMult) => (int)MathF.Round(damage * damageMult);

        /// <summary>Урон тарана с разгоном (метры разбега — импульс туши): `ChargeAbility`.</summary>
        public static int Blow(int damage, float damagePerMeter, float run, float damageMult)
            => (int)MathF.Round((damage + damagePerMeter * run) * damageMult);
    }
}
