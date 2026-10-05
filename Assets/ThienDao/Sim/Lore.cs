using System.Collections.Generic;
using ThienDao.Core;

namespace ThienDao.Sim
{
    // Name pools in the style of Phàm Nhân Tu Tiên. Later milestones draw items, pills and beasts from here too.
    public static class Lore
    {
        public static readonly string[] Sects =
        {
            "Hoàng Phong Cốc", "Yểm Nguyệt Tông", "Thất Huyền Môn", "Linh Thú Sơn", "Thanh Hư Môn", "Cự Kiếm Môn",
            "Thiên Khuyết Bảo", "Hóa Đao Ổ", "Quỷ Linh Môn", "Hợp Hoan Tông", "Ngự Linh Tông", "Thiên Sát Tông",
            "Lạc Vân Tông", "Cổ Kiếm Môn", "Bách Xảo Viện", "Thái Nhất Môn", "Âm La Tông", "Tinh Cung"
        };

        // Bare place names; the settlement adds Thôn / Trấn / Thành by size.
        public static readonly string[] Places =
        {
            "Thanh Ngưu", "Gia Nguyên", "Lam Châu", "Thái Nam", "Bạch Thủy", "Thanh Dương", "Hắc Phong", "Lạc Hà",
            "Kim Sa", "Vân Mộng", "Ngọc Khê", "Thiết Lĩnh", "Bích Hồ", "Hồng Diệp", "Thạch Kiều", "Liễu Gia",
            "Tử Trúc", "Phù Vân", "Hoàng Sa", "Thanh Khâu", "Long Môn", "Bích Lạc", "Vọng Nguyệt", "Hàn Giang",
            "Mộc Lan", "Lưu Hoa", "Tùng Lâm", "Cửu Khúc", "Đào Nguyên", "Yên Ba", "Tây Lương", "Bắc Mạc",
            "Ngũ Liễu", "Song Phong", "Thất Lý", "Bách Hoa", "Lão Quân", "Thanh Thạch", "Kỳ Lân", "Hạc Minh"
        };

        static readonly string[] Syllables =
        {
            "Thanh", "Bạch", "Hắc", "Kim", "Ngọc", "Vân", "Phong", "Thủy", "Sơn", "Lâm", "Hà", "Khê", "Lĩnh", "Nguyên",
            "Dương", "Nguyệt", "Tinh", "Hoa", "Diệp", "Thạch", "Long", "Hạc", "Tùng", "Trúc", "Liễu", "Đào", "Mai", "Lan"
        };

        // Items, pills and beasts for upcoming milestones (cultivation, economy, yêu thú).
        public static readonly string[] Pills = { "Trúc Cơ Đan", "Hoàng Long Đan", "Định Nhan Đan", "Thanh Linh Tán", "Kết Kim Đan", "Bổ Thiên Đan" };
        public static readonly string[] SpiritStones = { "Hạ phẩm linh thạch", "Trung phẩm linh thạch", "Thượng phẩm linh thạch", "Cực phẩm linh thạch" };
        public static readonly string[] Beasts = { "Huyết Ngọc Tri Chu", "Phệ Kim Trùng", "Thiết Giáp Ngô Công", "Hỏa Lân Thú", "Băng Phượng", "Giao Long" };

        public static string Tier(int population) => population >= 400 ? "Thành" : population >= 150 ? "Trấn" : "Thôn";

        // Hands out each pool entry once per world before inventing two-syllable names.
        public sealed class Picker
        {
            readonly List<string> _left;
            readonly HashSet<string> _used = new HashSet<string>();

            public Picker(string[] pool) => _left = new List<string>(pool);

            public string Next(ref DetRandom rng)
            {
                if (_left.Count > 0)
                {
                    int k = rng.Range(0, _left.Count);
                    string name = _left[k];
                    _left.RemoveAt(k);
                    _used.Add(name);
                    return name;
                }
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    int a = rng.Range(0, Syllables.Length), b = rng.Range(0, Syllables.Length - 1);
                    if (b >= a) b++;
                    string name = Syllables[a] + " " + Syllables[b];
                    if (_used.Add(name)) return name;
                }
                return Syllables[rng.Range(0, Syllables.Length)] + " " + _used.Count;
            }
        }
    }
}
