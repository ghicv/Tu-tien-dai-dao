using System;

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
        public readonly byte[] Temperature;   // yearly mean, 0 cold .. 255 hot
        public readonly byte[] Moisture;      // 0 dry .. 255 wet
        public readonly ushort[] QiBase;      // background qi (noise + phúc địa), independent of ley lines
        public readonly ushort[] QiCap;       // natural qi ceiling = base + ley line influence
        public readonly bool[] LeyLine;
        public readonly WorldObjects Objects;

        // Inclusive cell rect. Rendering subscribes; simulation never calls the renderer directly.
        public event Action<int, int, int, int> TerrainChanged;
        public event Action<int, int, int, int> QiCapChanged;

        public WorldData(uint seed, string seedText)
        {
            Seed = seed;
            SeedText = seedText;
            int n = W * H;
            Height = new float[n];
            Terrain = new Terrain[n];
            Temperature = new byte[n];
            Moisture = new byte[n];
            QiBase = new ushort[n];
            QiCap = new ushort[n];
            LeyLine = new bool[n];
            Objects = new WorldObjects(this);
        }

        public int Idx(int x, int y) => y * W + x;
        public bool InBounds(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;

        public void NotifyTerrainChanged(int x0, int y0, int x1, int y1) => TerrainChanged?.Invoke(x0, y0, x1, y1);
        public void NotifyQiCapChanged(int x0, int y0, int x1, int y1) => QiCapChanged?.Invoke(x0, y0, x1, y1);
    }
}
