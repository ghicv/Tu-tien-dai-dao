namespace ThienDao.World
{
    public sealed class WorldData
    {
        public const int Size = 1024;
        public const float SeaLevel = 0.5f;
        public const ushort MaxQi = 10000;

        public readonly int W = Size;
        public readonly int H = Size;
        public readonly uint Seed;
        public readonly string SeedText;

        public readonly float[] Height;       // 0..1, SeaLevel = 0.5
        public readonly Terrain[] Terrain;
        public readonly byte[] Temperature;   // 0 cold .. 255 hot
        public readonly byte[] Moisture;      // 0 dry .. 255 wet
        public readonly ushort[] QiCap;       // natural qi ceiling from ley lines
        public readonly ushort[] Qi;          // current qi
        public readonly bool[] LeyLine;
        public readonly WorldObjects Objects;

        public WorldData(uint seed, string seedText)
        {
            Seed = seed;
            SeedText = seedText;
            int n = W * H;
            Height = new float[n];
            Terrain = new Terrain[n];
            Temperature = new byte[n];
            Moisture = new byte[n];
            QiCap = new ushort[n];
            Qi = new ushort[n];
            LeyLine = new bool[n];
            Objects = new WorldObjects(this);
        }

        public int Idx(int x, int y) => y * W + x;
        public bool InBounds(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;
    }
}
