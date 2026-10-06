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

        // Ma đạo: the six demonic sects of Thiên Nam plus a few others from the novel. A sect with one of these
        // names follows the demonic path; demonic founders pick from here first.
        public static readonly string[] DemonicSects =
        {
            "Quỷ Linh Môn", "Hợp Hoan Tông", "Ngự Linh Tông", "Thiên Sát Tông", "Âm La Tông", "Thiên Ma Tông",
            "Độc Thánh Môn", "Ma Diễm Môn", "Huyết Sát Tông", "Vạn Độc Môn"
        };

        // Righteous sects for sects founded during the simulation (the starting ones draw from Sects).
        public static readonly string[] RighteousSects =
        {
            "Diệu Âm Môn", "Vạn Pháp Môn", "Cửu Tiên Cung", "Thiên Lan Thánh Điện", "Huyền Thiên Tông", "Thanh Vân Môn",
            "Kim Cương Tự", "Thiên Kiếm Tông", "Tử Tiêu Cung", "Bích Vân Cốc"
        };

        public static bool IsDemonicSect(string name) => System.Array.IndexOf(DemonicSects, name) >= 0;

        static readonly string[] SectSuffixes = { "Tông", "Môn", "Cốc", "Phái", "Cung", "Các", "Sơn" };

        // When the named pools run out: two syllables and a sect suffix, e.g. "Thanh Hạc Tông".
        public static string GeneratedSectName(ref DetRandom rng)
        {
            int a = rng.Range(0, Syllables.Length), b = rng.Range(0, Syllables.Length - 1);
            if (b >= a) b++;
            return $"{Syllables[a]} {Syllables[b]} {SectSuffixes[rng.Range(0, SectSuffixes.Length)]}";
        }

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
        // The pill that helps through each gate (indexed by the realm being broken into).
        public static string PillFor(Realm next) =>
            next == Realm.TrucCo ? "Trúc Cơ Đan" : next == Realm.KetDan ? "Kết Kim Đan" : next == Realm.NguyenAnh ? "Bổ Thiên Đan" : "Hoàng Long Đan";

        // Pháp bảo found on the road, after the novel.
        public static readonly string[] Treasures =
        {
            "Thanh Trúc Phong Vân Kiếm", "Huyền Thiết Phi Thiên Thuẫn", "Kim Lôi Trúc phi kiếm", "Phong Lôi Sí", "Hư Thiên Đỉnh mảnh vỡ",
            "Ngũ Hành Hoàn", "Huyết Ngọc Châu", "Tử Mẫu Âm Dương Thoa", "Bát Quái Kính", "Hàn Băng Kiếm", "Liệt Hỏa Phiến", "Ngân Nguyệt Câu"
        };

        public static readonly string[] SpiritStones = { "Hạ phẩm linh thạch", "Trung phẩm linh thạch", "Thượng phẩm linh thạch", "Cực phẩm linh thạch" };
        public static readonly string[] Herbs =
        {
            "Ngọc Tủy Chi", "Thiên Linh Quả", "Huyết Sâm ngàn năm", "Băng Tâm Thảo", "Hỏa Linh Chi", "Kim Tủy Hoa",
            "Tử Hà Thảo", "Vạn Niên Linh Nhũ", "Thanh Linh Thảo", "Long Diên Hương"
        };

        public static readonly string[] Beasts = { "Huyết Ngọc Tri Chu", "Phệ Kim Trùng", "Thiết Giáp Ngô Công", "Hỏa Lân Thú", "Băng Phượng", "Giao Long" };

        // Cultivator names in the novel's style (surname + given name).
        public static readonly string[] Surnames =
        {
            "Hàn", "Lệ", "Nam Cung", "Mặc", "Lý", "Trương", "Vương", "Triệu", "Lăng", "Tần", "Đổng", "Âu Dương",
            "Mộ Dung", "Lục", "Tô", "Liễu", "Diệp", "Bạch", "Hạ Hầu", "Thạch", "Tề", "Ngô", "Chu", "Tôn", "Phùng",
            "Khúc", "Lạc", "Vạn", "Ôn", "Từ", "Cổ", "Thượng Quan", "Lâm", "Tiêu", "Hoàng Phủ", "Đoan Mộc"
        };

        public static readonly string[] GivenNames =
        {
            "Lập", "Phi Vũ", "Uyển", "Thiên Đô", "Ngọc", "Nguyệt", "Thanh Phong", "Vân", "Huyền", "Kiếm", "Tử Linh",
            "Mộng", "Hạo", "Thiên Nam", "Tuyết", "Băng", "Hồng Phất", "Dao", "Viêm", "Minh", "Tiêu Dao", "Nhược Hy",
            "Long", "Thần", "Bá", "Sương", "Diệu", "Trần", "Phong", "Lãnh", "Yên", "Khuyết", "Ly", "Tịch", "Vô Kỵ"
        };

        public static string Tier(int population) => population >= 400 ? "Thành" : population >= 150 ? "Trấn" : "Thôn";

        public static string PersonName(ref DetRandom rng) =>
            Surnames[rng.Range(0, Surnames.Length)] + " " + GivenNames[rng.Range(0, GivenNames.Length)];

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
