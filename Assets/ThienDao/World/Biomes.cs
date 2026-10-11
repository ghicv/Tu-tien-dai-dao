using UnityEngine;

namespace ThienDao.World
{
    // Biome tu tiên giới (devlog 37): the spirit of a stretch of land, laid over its ground (WorldData.Biome). The
    // terrain says what the ground is (forest, swamp, peak); the biome says what heaven and earth made of it: a linh
    // sơn where qi gathers, a lôi trạch where thunder never stops, a ma vực of dark qi. Each has its own qi, its own
    // element (those with that root cultivate faster there), its own beasts and its own look on the map.
    public enum Biome : byte
    {
        None,      // đất thường: plains, ordinary woods and hills
        LinhSon,   // linh sơn: peaks where qi gathers, mist in the passes
        TienDao,   // tiên đảo: small isles out at sea, wind and cloud
        TrucLam,   // trúc lâm: groves of bamboo
        DaoHoa,    // đào hoa lâm: peach blossom, petals on the wind
        LinhHo,    // linh hồ: a lake fed by a ley line, and its shore
        HanBang,   // hàn băng nguyên: ice and frost
        HoaVuc,    // hỏa vực: fire earth, embers and ash
        LoiTrach,  // lôi trạch: thunder marsh, never a day without lightning
        MaVuc,     // ma vực: dark qi seeping from the earth; ma tu thrive, others wither
        DocChuong, // độc chướng lâm: poison jungle; rare herbs, and death
        CamDia,    // cấm địa: ground no one should walk; the richest qi, the greatest danger
        HoangMac,  // hoang mạc: sand and wind
        KimSon,    // kim thạch sơn: bare stone hills veined with metal
        Count
    }

    public static class BiomeInfo
    {
        public static readonly string[] Names =
        {
            "Đất thường", "Linh sơn", "Tiên đảo", "Trúc lâm", "Đào hoa lâm", "Linh hồ", "Hàn băng nguyên", "Hỏa vực", "Lôi trạch",
            "Ma vực", "Độc chướng lâm", "Cấm địa", "Hoang mạc", "Kim thạch sơn"
        };

        // What the land does to those who live on it, for the cell info.
        public static readonly string[] Effects =
        {
            "", "linh khí dồi dào, ai tu cũng nhanh", "gió mây tụ hội, hợp Phong linh căn", "hợp Mộc linh căn, nhiều linh thảo",
            "hợp Mộc linh căn, tâm cảnh dễ tĩnh", "hợp Thủy linh căn, linh mạch lộ thiên", "hợp Băng linh căn, kẻ khác khó sống",
            "hợp Hỏa linh căn, đất của luyện đan luyện khí", "hợp Lôi linh căn, sét đánh không ngớt", "ma khí: ma tu tu nhanh, chính đạo suy yếu",
            "linh thảo quý nhưng độc, dễ trúng độc mà chết", "linh khí cực thịnh, hung hiểm vô cùng", "hợp Thổ linh căn, khô cằn",
            "hợp Kim linh căn, đá núi ẩn kim khí"
        };

        // Element (index into SpiritRoots.ElementNames: Kim, Mộc, Thủy, Hỏa, Thổ, Lôi, Phong, Băng) whose roots cultivate
        // faster here; -1 for none.
        public static readonly sbyte[] Element = { -1, -1, 6, 1, 1, 2, 7, 3, 5, -1, 1, -1, 4, 0 };

        // Added to the cell's qi ceiling, as a share of MaxQi (QiCap.Recompute).
        public static readonly float[] QiBonus = { 0f, 0.25f, 0.22f, 0.08f, 0.06f, 0.2f, 0.04f, 0.1f, 0.15f, 0.1f, 0.12f, 0.35f, -0.03f, 0.05f };

        // The tint laid over the ground on the map (alpha = how strongly), and its overview colour.
        public static readonly Color32[] Tint =
        {
            new Color32(0, 0, 0, 0),
            new Color32(92, 210, 170, 70),   // linh sơn: jade
            new Color32(200, 240, 255, 60),  // tiên đảo: cloud white
            new Color32(60, 150, 70, 60),    // trúc lâm: deep green
            new Color32(255, 160, 200, 55),  // đào hoa: pink
            new Color32(90, 230, 230, 80),   // linh hồ: turquoise
            new Color32(190, 225, 255, 60),  // hàn băng: ice blue
            new Color32(230, 80, 30, 80),    // hỏa vực: ember red
            new Color32(150, 90, 220, 80),   // lôi trạch: violet
            new Color32(70, 20, 60, 110),    // ma vực: blood-dark purple
            new Color32(170, 190, 40, 80),   // độc chướng: sickly yellow-green
            new Color32(60, 50, 70, 120),    // cấm địa: grey-black
            new Color32(230, 200, 120, 40),  // hoang mạc: sand
            new Color32(200, 200, 215, 60),  // kim thạch: steel
        };

        public static bool IsDark(Biome b) => b == Biome.MaVuc || b == Biome.CamDia;

        // Lays a biome over a disc of land (a linh hồ over water too), with a ragged edge; the qi ceiling follows, and
        // the map redraws. Returns how many cells changed.
        public static int Paint(WorldData w, int cx, int cy, int r, Biome biome)
        {
            int changed = 0;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!w.InBounds(x, y)) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + (Core.Hash.Float01(w.Seed ^ 0xB10Bu, x, y) - 0.5f) * 2f;
                if (d > r) continue;
                int i = w.Idx(x, y);
                if (TerrainInfo.IsWater(w.Terrain[i]) && biome != Biome.LinhHo && biome != Biome.None) continue;
                if (w.Biome[i] == biome) continue;
                w.Biome[i] = biome;
                changed++;
            }
            if (changed == 0) return 0;
            QiCap.Recompute(w, cx - r, cy - r, cx + r, cy + r);
            w.NotifyQiCapChanged(cx - r, cy - r, cx + r, cy + r);
            w.NotifyLookChanged(cx - r, cy - r, cx + r, cy + r);
            return changed;
        }

        // How much faster (or slower) cultivation goes here for someone of these roots and this path.
        public static float CultivationFactor(Biome b, int roots, bool demonic)
        {
            if (b == Biome.MaVuc) return demonic ? 1.4f : 0.75f;
            float f = b == Biome.LinhSon || b == Biome.TienDao || b == Biome.CamDia ? 1.1f : 1f;
            int e = Element[(int)b];
            if (e >= 0 && (roots & (1 << e)) != 0) f *= 1.35f;
            return f;
        }

        // Names for the lands of each biome, "{word} {kind}": Thanh Vân Linh Sơn, Hắc Phong Ma Vực…
        public static readonly string[] PlaceKinds =
        {
            "", "Sơn", "Đảo", "Trúc Lâm", "Đào Hoa Cốc", "Hồ", "Băng Cốc", "Hỏa Vực", "Lôi Trạch", "Ma Vực", "Chướng Lâm", "Cấm Địa", "Sa Hải", "Kim Sơn"
        };

        public static readonly string[] PlaceWords =
        {
            "Thanh Vân", "Bích Lạc", "Tử Vi", "Huyền Minh", "Lạc Hà", "Vân Mộng", "Thiên Lam", "Ngọc Hư", "Kim Ô", "Hàn Nguyệt",
            "Xích Diễm", "Hắc Phong", "Bạch Lộc", "Thúy Trúc", "U Minh", "Phù Dung", "Linh Lung", "Côn Ngô", "Thái Nhạc", "Vạn Kiếm",
            "Lưu Vân", "Phi Tuyết", "Huyết Sắc", "Vô Tận", "Tịch Diệt", "Thiên Cơ", "Tử Kim", "Bích Thủy", "Cửu U", "Lạc Nhật",
        };
    }

    // A named stretch of one biome (WorldData.BiomePatches): for labels on the map and for telling where things are.
    public sealed class BiomePatch
    {
        public Biome Biome;
        public string Name;
        public int X, Y;     // its middle
        public int Cells;
    }
}
