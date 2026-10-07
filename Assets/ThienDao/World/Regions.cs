using UnityEngine;

namespace ThienDao.World
{
    // Đại vực: the great regions of the continent, after the map of Thiên Nam in Phàm Nhân Tu Tiên.
    // Each shapes its own land (relief, climate, plants) and holds a few mortal kingdoms.
    public enum RegionKind : byte
    {
        None,         // the sea
        ChinhDao,     // Chính Đạo: temperate hills, forests and rivers; the righteous sects
        MaDao,        // Ma Đạo: ash plains, red earth, dead woods and fire mountains; the demonic sects
        ThienDaoMinh, // Thiên Đạo Minh: the cold northern highlands of pine and stone
        CuuQuocMinh,  // Cửu Quốc Minh: the warm, fertile southern plains of many kingdoms
        SaMac,        // Sa Mạc Bạo Phong: the western storm desert and its red mesas
        ThaoNguyen,   // Mộ Lan Thảo Nguyên: the far-southern steppe of the Mộ Lan tribes
        BangNguyen,   // Cực Bắc Băng Nguyên: ice and tundra at the top of the world
        Count
    }

    public sealed class RegionInfo
    {
        public RegionKind Kind;
        public string Name;
        public float LabelX, LabelY; // centre of its land, where the map writes its name
        public int LandCells;
    }

    public sealed class Kingdom
    {
        public int Id;               // index in WorldData.Kingdoms; cells store Id + 1
        public string Name;
        public RegionKind Region;
        public int CapitalX, CapitalY;
        public bool Fallen;          // no town of it is left: the kingdom is gone, only its name on old maps
        public int LandCells;
        public string CapitalName => Name.EndsWith(" Bộ") ? Name.Substring(0, Name.Length - 3) + " Vương Đình" : Name.Replace(" Quốc", " Kinh");
    }

    public static class RegionLore
    {
        // A light grade over each region's land so the regions read apart at a glance.
        public static readonly Color32[] Tint =
        {
            new Color32(0, 0, 0, 0),
            new Color32(84, 176, 120, 255),
            new Color32(150, 54, 70, 255),
            new Color32(110, 140, 170, 255),
            new Color32(200, 190, 90, 255),
            new Color32(232, 140, 70, 255),
            new Color32(214, 196, 110, 255),
            new Color32(220, 236, 250, 255),
        };

        public static readonly float[] TintAmount = { 0f, 0.07f, 0.24f, 0.16f, 0.12f, 0.2f, 0.16f, 0.18f };
    }
}
