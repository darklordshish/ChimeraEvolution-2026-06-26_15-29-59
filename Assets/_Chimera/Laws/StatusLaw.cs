using System;

namespace Chimera.Laws
{
    /// <summary>Накопительный статус (яд, кровь, замедление): стаки живут до `ExpireAt` — момента на часах мира. Часы подаёт
    /// вызывающий: игра — `Time.time`, симулятор — свой счётчик; закон времени не читает.</summary>
    [Serializable]
    public struct StackState
    {
        public int Stacks;
        public float ExpireAt;
        public float NextTick;
    }

    [Serializable]
    public struct VenomParams
    {
        public int MaxStacks; public float StackDuration; public float VulnerabilityMult; public int DotDamage; public float DotInterval;
        public static VenomParams Default => new VenomParams { MaxStacks = 3, StackDuration = 4f, VulnerabilityMult = 1.4f, DotDamage = 3, DotInterval = 0.6f };
    }

    [Serializable]
    public struct BleedParams
    {
        public int Threshold; public int MaxStacks; public float StackDuration; public float PctPerTick; public float TickInterval;
        public static BleedParams Default => new BleedParams { Threshold = 5, MaxStacks = 8, StackDuration = 3f, PctPerTick = 0.04f, TickInterval = 0.6f };
    }

    [Serializable]
    public struct RageParams
    {
        public float DamageMult; public float SpeedMult; public float IncomingMult; public float StaminaRegenMult;
        public static RageParams Default => new RageParams { DamageMult = 1.25f, SpeedMult = 1.15f, IncomingMult = 1.5f, StaminaRegenMult = 1.5f };
    }

    [Serializable]
    public struct SlowParams
    {
        public float PerStack; public float MaxSlow; public int MaxStacks; public float StackDuration;
        public static SlowParams Default => new SlowParams { PerStack = 0.14f, MaxSlow = 0.7f, MaxStacks = 6, StackDuration = 2.5f };
    }

    /// <summary>Иглы: доля сырого урона обратно 0.55 (0.35 не тянуло матч против змеи: укус 24 > ёжиный 14; теперь крупный укус
    /// возвращается ~13 — большой удар об иглы наказывается сильнее, ёж выигрывает размен).</summary>
    [Serializable]
    public struct ThornsParams
    {
        public float ReturnShare; public float Reach; public int BleedStacks; public int MinReturn;
        public static ThornsParams Default => new ThornsParams { ReturnShare = 0.55f, Reach = 2.5f, BleedStacks = 1, MinReturn = 1 };
    }

    /// <summary>
    /// Законы статусов (Ф-10): шаги яда и крови, замедление, ярость, иглы. Числа — в параметрах (`LawsConfig` в игре),
    /// формулы бит в бит с прежними компонентами: float, округление к чётному (как `Mathf.RoundToInt`).
    /// </summary>
    public static class StatusLaw
    {
        /// <summary>Яд: с этого числа стаков входящий урон растёт.</summary>
        public const int VenomVulnerableStacks = 2;
        /// <summary>Яд: с этого числа стаков идёт тик урона.</summary>
        public const int VenomDotStacks = 3;

        /// <summary>Живые стаки на момент `now`.</summary>
        public static int Stacks(in StackState s, float now) => now < s.ExpireAt ? s.Stacks : 0;

        /// <summary>Новый стак: счёт растёт до потолка, жизнь стака — `duration × durationMult` (стойкость носителя).</summary>
        public static void AddStack(ref StackState s, float now, int maxStacks, float duration, float durationMult)
        {
            s.Stacks = Math.Min(maxStacks, Stacks(s, now) + 1);
            s.ExpireAt = now + duration * durationMult;
        }

        public static float VenomIncoming(int stacks, in VenomParams p) => stacks >= VenomVulnerableStacks ? p.VulnerabilityMult : 1f;

        /// <summary>Шаг яда на момент `now`: урон тика (0 — тика нет). Тик идёт при 3 стаках и взводит следующий.</summary>
        public static int VenomStep(ref StackState s, float now, in VenomParams p)
        {
            int stacks = Stacks(s, now);
            if (stacks >= VenomDotStacks && now >= s.NextTick)
            {
                s.NextTick = now + p.DotInterval;
                return p.DotDamage;
            }
            return 0;
        }

        /// <summary>Шаг кровотечения на момент `now`: урон тика (0 — тика нет). Кровопотеря — за порогом, доля МАКС HP, не меньше 1.</summary>
        public static int BleedStep(ref StackState s, float now, in BleedParams p, int maxHealth)
        {
            if (Stacks(s, now) < p.Threshold) return 0;
            if (now >= s.NextTick)
            {
                s.NextTick = now + p.TickInterval;
                return Math.Max(1, (int)MathF.Round(maxHealth * p.PctPerTick));
            }
            return 0;
        }

        /// <summary>Множитель скорости от замедления: 1 — свободен.</summary>
        public static float SlowMult(int stacks, in SlowParams p) => 1f - LawMath.Min(p.MaxSlow, stacks * p.PerStack);

        /// <summary>Ответ игл: доля СЫРОГО урона, не меньше `minReturn`.</summary>
        public static int ThornsReturn(int rawDamage, float returnShare, int minReturn)
            => Math.Max(minReturn, (int)MathF.Round(rawDamage * returnShare));

        public static float RageDamage(in RageParams p, bool enraged, float boost) => enraged ? p.DamageMult * boost : 1f;
        public static float RageSpeed(in RageParams p, bool enraged) => enraged ? p.SpeedMult : 1f;
        public static float RageIncoming(in RageParams p, bool enraged) => enraged ? p.IncomingMult : 1f;
        public static float RageStamina(in RageParams p, bool enraged) => enraged ? p.StaminaRegenMult : 1f;
    }
}
