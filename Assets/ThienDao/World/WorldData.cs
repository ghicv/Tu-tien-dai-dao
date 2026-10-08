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
        public int Capital = -1; // kingdom id when this is a kingdom's capital
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
        public readonly byte[] Zone;          // ZoneFlags left by events (lôi địa, …)
        public readonly byte[] Scar;          // vết tích: ScarKind in the low 4 bits, strength 1..15 in the high 4 (ScarInfo)
        public readonly byte[] Region;        // RegionKind of each land cell (None at sea)
        public readonly ushort[] KingdomOf;   // kingdom id + 1, 0 = no kingdom (sea, ice, wilds between)
        public readonly WorldObjects Objects;
        public readonly List<VillageSite> VillageSites = new List<VillageSite>();
        public readonly List<RegionInfo> Regions = new List<RegionInfo>();
        public WorldLore Lore;                // the names this world drew from Resources/Lore (set by the generator)
        public float SeaLabelX = -1, SeaLabelY = -1; // where the map writes the sea's name
        public readonly List<Kingdom> Kingdoms = new List<Kingdom>();

        public RegionKind RegionAt(int i) => (RegionKind)Region[i];

        public Kingdom KingdomAt(int i) => KingdomOf[i] > 0 ? Kingdoms[KingdomOf[i] - 1] : null;

        public RegionInfo FindRegion(RegionKind k)
        {
            foreach (var r in Regions)
                if (r.Kind == k) return r;
            return null;
        }

        // Inclusive cell rect. Rendering subscribes; simulation never calls the renderer directly.
        public event Action<int, int, int, int> TerrainChanged;
        public event Action<int, int, int, int> QiCapChanged;
        public event Action<int, int, int, int> LookChanged; // only the look of the ground (scars, walls); no system needs to react
        public event Action<int, int, int, int> PathsChanged; // trails and roads appeared or faded (PathSystem): only the map redraws

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
            Zone = new byte[n];
            Scar = new byte[n];
            Region = new byte[n];
            KingdomOf = new ushort[n];
            Objects = new WorldObjects(this);
        }

        public int Idx(int x, int y) => y * W + x;
        public bool InBounds(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;

        public float Fertility(int i) =>
            UnityEngine.Mathf.Clamp01(TerrainInfo.BaseFertility[(int)Terrain[i]] * (0.55f + 0.6f * Moisture[i] / 255f) * ScarInfo.FertilityFactor(Scar[i]));

        public bool IsWalkable(float x, float y)
        {
            int cx = (int)x, cy = (int)y;
            return x >= 0f && y >= 0f && InBounds(cx, cy) && TerrainInfo.IsWalkable(Terrain[Idx(cx, cy)]);
        }

        public void NotifyTerrainChanged(int x0, int y0, int x1, int y1) => TerrainChanged?.Invoke(x0, y0, x1, y1);
        public void NotifyQiCapChanged(int x0, int y0, int x1, int y1) => QiCapChanged?.Invoke(x0, y0, x1, y1);
        public void NotifyLookChanged(int x0, int y0, int x1, int y1) => LookChanged?.Invoke(x0, y0, x1, y1);
        public void NotifyPathsChanged(int x0, int y0, int x1, int y1) => PathsChanged?.Invoke(x0, y0, x1, y1);
    }
}
