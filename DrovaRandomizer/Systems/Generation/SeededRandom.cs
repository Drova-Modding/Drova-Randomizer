namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>
    /// SplitMix64. Own generator rather than System.Random, whose seeded sequence is not a
    /// documented contract across .NET versions. A run must regenerate identically from its save.
    /// </summary>
    public sealed class SeededRandom(ulong seed)
    {
        private ulong _state = seed;

        private ulong NextUInt64()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform in [0, max).</summary>
        public int Next(int max)
        {
            return (int)(((NextUInt64() >> 32) * (ulong)max) >> 32);
        }

        /// <summary>Uniform in [min, maxExclusive).</summary>
        public int Next(int min, int maxExclusive)
        {
            return min + Next(maxExclusive - min);
        }

        /// <summary>Fisher-Yates in place.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>k distinct random elements.</summary>
        public List<T> Sample<T>(IList<T> list, int k)
        {
            List<T> copy = [.. list];
            for (int i = 0; i < k; i++)
            {
                int j = i + Next(copy.Count - i);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy.GetRange(0, k);
        }

        /// <summary>Index picked with probability proportional to its weight.</summary>
        public int PickWeighted(float[] weights)
        {
            double total = weights.Sum(w => (double)w);
            double roll = (NextUInt64() >> 11) * (1.0 / (1UL << 53)) * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }
            return weights.Length - 1;
        }
    }
}
