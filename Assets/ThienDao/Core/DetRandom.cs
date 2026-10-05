namespace ThienDao.Core
{
    public static class Hash
    {
        public static uint U32(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        public static uint U32(uint seed, int a, int b)
        {
            return U32(seed ^ U32((uint)a * 0x9E3779B1u ^ U32((uint)b * 0x85EBCA77u + 0x632BE5ABu)));
        }

        public static float Float01(uint seed, int a, int b)
        {
            return (U32(seed, a, b) >> 8) * (1f / 16777216f);
        }

        // Numeric text is used as-is so "12345" stays seed 12345; any other text is hashed (FNV-1a).
        public static uint FromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (uint.TryParse(s.Trim(), out var v)) return v;
            uint h = 2166136261u;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619u;
            }
            return h;
        }
    }

    public struct DetRandom
    {
        uint _state;

        public DetRandom(uint seed)
        {
            _state = Hash.U32(seed + 0x9E3779B9u);
            if (_state == 0) _state = 1;
        }

        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
