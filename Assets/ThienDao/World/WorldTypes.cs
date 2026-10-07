using UnityEngine;

namespace ThienDao.World
{
    public enum Terrain : byte
    {
        DeepOcean,
        Ocean,
        Shallow,
        River,
        Beach,
        Grass,
        Forest,
        Jungle,
        Savanna,
        Desert,
        Swamp,
        Tundra,
        Snow,
        Hills,
        Mountain,
        Peak,
        Farmland,
        Lava,      // dung nham from an eruption; cools to rock over the years
        Ashland,   // đất tro: the grey ash plains of Ma Đạo
        Badlands,  // hoàng thổ: red mesas and gullies at the edge of the desert
        Count
    }

    // Lasting marks events leave on the land (WorldData.Zone, one bit each).
    public static class ZoneFlags
    {
        public const byte Thunder = 1; // lôi địa: where heaven's tribulation fell; thick lôi khí, no fields, no trees
        public const byte Road = 2;    // thương lộ: worn into the ground by caravans
    }

    public static class TerrainInfo
    {
        public static readonly string[] Names =
        {
            "Biển sâu", "Biển", "Nước nông", "Sông", "Bãi cát", "Đồng cỏ", "Rừng", "Rừng rậm",
            "Thảo nguyên", "Sa mạc", "Đầm lầy", "Lãnh nguyên", "Tuyết", "Đồi", "Núi", "Đỉnh tuyết", "Ruộng", "Dung nham", "Đất tro", "Hoàng thổ"
        };

        public static readonly Color32[] Colors =
        {
            new Color32(30, 74, 160, 255),
            new Color32(42, 104, 194, 255),
            new Color32(66, 146, 218, 255),
            new Color32(62, 138, 212, 255),
            new Color32(234, 214, 152, 255),
            new Color32(102, 174, 66, 255),
            new Color32(76, 146, 54, 255),
            new Color32(48, 124, 52, 255),
            new Color32(180, 180, 82, 255),
            new Color32(228, 198, 122, 255),
            new Color32(86, 114, 72, 255),
            new Color32(150, 170, 140, 255),
            new Color32(238, 243, 248, 255),
            new Color32(124, 152, 74, 255),
            new Color32(132, 120, 110, 255),
            new Color32(222, 226, 234, 255),
            new Color32(126, 166, 66, 255),
            new Color32(236, 92, 30, 255),
            new Color32(104, 94, 92, 255),
            new Color32(198, 116, 72, 255),
        };

        // Elevation tier drives the cliff/bevel shading between neighbouring cells.
        public static readonly byte[] Tier = { 0, 0, 1, 1, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 5, 6, 3, 5, 3, 4 };

        // Representative normalized height (sea level = 0.5) used when the brush paints a terrain.
        public static readonly float[] NominalHeight =
        {
            0.25f, 0.38f, 0.47f, 0.495f, 0.505f, 0.56f, 0.58f, 0.56f, 0.57f, 0.58f, 0.53f, 0.6f, 0.62f, 0.74f, 0.85f, 0.95f, 0.56f, 0.8f, 0.58f, 0.72f
        };

        // How well crops grow, before the moisture factor (0..1).
        public static readonly float[] BaseFertility =
        {
            0f, 0f, 0f, 0f, 0.15f, 0.85f, 0.7f, 0.75f, 0.55f, 0.08f, 0.45f, 0.25f, 0.03f, 0.45f, 0.1f, 0f, 0.9f, 0f, 0.3f, 0.12f
        };

        // Wild forage a cell can hold for grazing animals (1 unit relieves 1 point of hunger).
        public static readonly float[] ForageCapacity =
        {
            0f, 0f, 0f, 0f, 1.5f, 18f, 12f, 15f, 12f, 1.2f, 9f, 6f, 0.6f, 9f, 1.5f, 0f, 0f, 0f, 4f, 3f
        };

        public static bool IsWater(Terrain t) => t <= Terrain.River;
        public static bool IsLand(Terrain t) => t > Terrain.River && t < Terrain.Count;
        public static bool IsHighland(Terrain t) => t == Terrain.Hills || t == Terrain.Mountain || t == Terrain.Peak;
        public static bool IsWalkable(Terrain t) => IsLand(t) && t != Terrain.Peak && t != Terrain.Lava;
        public static bool IsFarmable(Terrain t) =>
            t == Terrain.Grass || t == Terrain.Savanna || t == Terrain.Forest || t == Terrain.Jungle || t == Terrain.Hills || t == Terrain.Ashland;

        public static float PixelNoiseAmp(Terrain t)
        {
            switch (t)
            {
                case Terrain.DeepOcean:
                case Terrain.Ocean:
                case Terrain.Shallow:
                case Terrain.River: return 0.025f;
                case Terrain.Snow:
                case Terrain.Peak: return 0.02f;
                case Terrain.Mountain: return 0.08f;
                case Terrain.Lava: return 0.18f;
                case Terrain.Ashland: return 0.09f;
                case Terrain.Badlands: return 0.07f;
                case Terrain.Beach:
                case Terrain.Desert: return 0.035f;
                default: return 0.055f;
            }
        }

        public static bool IsVegetated(Terrain t) =>
            t == Terrain.Grass || t == Terrain.Forest || t == Terrain.Jungle || t == Terrain.Savanna ||
            t == Terrain.Swamp || t == Terrain.Hills || t == Terrain.Tundra;
    }

    public enum ObjectType : byte
    {
        None,
        TreeOak,
        TreeAutumn,
        TreeJungle,
        TreePine,
        TreeSnowPine,
        TreePalm,
        Cactus,
        Bush,
        Rock,
        House,
        SectHall,
        TreeBamboo, // trúc lâm
        TreeDead,   // cây khô of the ash plains
        TreePeach,  // đào hoa
        Count
    }

    public static class ObjectInfo
    {
        public static readonly string[] Names =
        {
            "", "Cây sồi", "Cây thu", "Cây rừng rậm", "Cây thông", "Thông tuyết", "Cây dừa", "Xương rồng",
            "Bụi cây", "Đá", "Nhà dân", "Tông môn", "Trúc", "Cây khô", "Đào hoa"
        };

        public static readonly byte[] FootprintW = { 0, 1, 1, 2, 1, 1, 1, 1, 1, 1, 3, 5, 1, 1, 1 };
        public static readonly byte[] FootprintH = { 0, 1, 1, 2, 1, 1, 1, 1, 1, 1, 3, 5, 1, 1, 1 };

        public static bool IsBuilding(ObjectType t) => t == ObjectType.House || t == ObjectType.SectHall;

        public static bool CanStandOn(ObjectType t, Terrain terrain)
        {
            if (!TerrainInfo.IsLand(terrain)) return false;
            if (terrain == Terrain.Farmland || terrain == Terrain.Lava) return false;
            if (IsBuilding(t)) return terrain != Terrain.Mountain && terrain != Terrain.Peak && terrain != Terrain.Swamp;
            if (t == ObjectType.Rock) return true;
            return terrain != Terrain.Peak;
        }
    }
}
