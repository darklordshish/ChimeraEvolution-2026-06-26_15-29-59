namespace Chimera.Laws
{
    /// <summary>Детерминированный xorshift32: одна последовательность в игре, тестах и консольном симуляторе.</summary>
    public sealed class XorShiftRandom : ILawRandom
    {
        uint state;

        public XorShiftRandom(uint seed) { state = seed == 0 ? 0x9E3779B9u : seed; }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public float Next01() => (NextUInt() >> 8) * (1f / 16777216f);

        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));

        public float Range(float min, float max) => min + Next01() * (max - min);
    }
}
