using System;
using System.Collections.Generic;

namespace ThienDao.World
{
    // A village laid out by the map generator; the settlement system turns these into living settlements.
    public sealed class VillageSite
    {
        public int X, Y;
        public byte Roof;
        public bool Sect;
        public readonly List<int> Houses = new List<int>();
    }

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
        public readonly byte[] WaterDist;     // cells to sea/lake at generation time; not refreshed by later edits
        public readonly ushort[] Owner;       // settlement id + 1, 0 = unclaimed
        public readonly WorldObjects Objects;
        public readonly List<VillageSite> VillageSites = new List<VillageSite>();

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
            WaterDist = new byte[n];
            Owner = new ushort[n];
            Objects = new WorldObjects(this);
        }

        public int Idx(int x, int y) => y * W + x;
        public bool InBounds(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;

        public float Fertility(int i) =>
            UnityEngine.Mathf.Clamp01(TerrainInfo.BaseFertility[(int)Terrain[i]] * (0.55f + 0.6f * Moisture[i] / 255f));

        public bool IsWalkable(float x, float y)
        {
            int cx = (int)x, cy = (int)y;
            return x >= 0f && y >= 0f && InBounds(cx, cy) && TerrainInfo.IsWalkable(Terrain[Idx(cx, cy)]);
        }

        public void NotifyTerrainChanged(int x0, int y0, int x1, int y1) => TerrainChanged?.Invoke(x0, y0, x1, y1);
        public void NotifyQiCapChanged(int x0, int y0, int x1, int y1) => QiCapChanged?.Invoke(x0, y0, x1, y1);
    }
}
