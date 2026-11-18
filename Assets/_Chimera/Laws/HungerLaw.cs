using System;

namespace Chimera.Laws
{
    [Serializable]
    public struct HungerParams
    {
        public float HpRegen; public float StaminaRegen;
        public float PureMinutes; public float MediumMinutes; public float WeakMinutes; public float ChimeraMinutes;
        public float StartFullness; public float SatedThreshold; public float HungryThreshold; public float StarveThreshold; public float StarveVigor;

        public static HungerParams Default => new HungerParams
        {
            HpRegen = 4f, StaminaRegen = 14f,
            PureMinutes = 3f, MediumMinutes = 2f, WeakMinutes = 1.5f, ChimeraMinutes = 0.5f,
            StartFullness = 0.4f, SatedThreshold = 0.5f, HungryThreshold = 0.25f, StarveThreshold = 0.08f, StarveVigor = 0.65f,
        };
    }

    /// <summary>
    /// Закон сытости и регенерации (Ф-11, Ф-12): распад шкалы по ступеням однородности, пороги, вигор, накопление дробных HP.
    /// Времени закон не читает: шаг `dt` подаёт игра (`Time.deltaTime`) или симулятор.
    /// </summary>
    public static class HungerLaw
    {
        /// <summary>Распад шкалы в секунду до сборки тела — как у чистого вида.</summary>
        public static float DefaultDecayPerSec(in HungerParams p) => 1f / (p.PureMinutes * 60f);

        /// <summary>Распад по однородности тела (макс. идентичность 0..1): чистый держит дольше, химера сгорает.</summary>
        public static float DecayPerSec(float homogeneity, in HungerParams p)
        {
            float min = homogeneity >= 0.85f ? p.PureMinutes
                      : homogeneity >= 0.70f ? p.MediumMinutes
                      : homogeneity >= 0.55f ? p.WeakMinutes
                      : p.ChimeraMinutes;
            return 1f / (LawMath.Max(0.1f, min) * 60f);
        }

        public static float Decay(float fullness, float decayPerSec, float dt) => LawMath.Max(0f, fullness - decayPerSec * dt);
        public static float Feed(float fullness, float amount) => LawMath.Clamp01(fullness + LawMath.Max(0f, amount));

        public static bool IsSated(float fullness, in HungerParams p) => fullness >= p.SatedThreshold;
        public static bool IsHungry(float fullness, in HungerParams p) => fullness <= p.HungryThreshold;
        public static bool IsStarving(float fullness, in HungerParams p) => fullness <= p.StarveThreshold;
        public static float Vigor(float fullness, in HungerParams p) => IsStarving(fullness, p) ? p.StarveVigor : 1f;

        /// <summary>Скорость регена HP в секунду: постоянный + вне боя, умноженный на фактор подавления (яд, укус Пасти).</summary>
        public static float RegenRate(float perSecond, float outOfCombat, bool inCombat, float factor)
        {
            float rate = perSecond;
            if (!inCombat) rate += outOfCombat;
            return rate * factor;
        }

        /// <summary>Копит дробные HP: целая часть выходит наружу, остаток остаётся в `acc`. Скорость ≤ 0 — ничего не копит.</summary>
        public static int RegenTick(ref float acc, float rate, float dt)
        {
            if (rate <= 0f) return 0;
            acc += rate * dt;
            if (acc < 1f) return 0;
            int whole = (int)MathF.Floor(acc);
            acc -= whole;
            return whole;
        }
    }
}
