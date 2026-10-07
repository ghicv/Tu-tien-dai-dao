namespace ThienDao.Sim
{
    public enum Realm : byte { Mortal, LuyenKhi, TrucCo, KetDan, NguyenAnh, HoaThan, Count }

    // Cultivation ladder after Phàm Nhân Tu Tiên. Values are game balance, not canon.
    public static class Realms
    {
        public static readonly string[] Names = { "Phàm nhân", "Luyện Khí", "Trúc Cơ", "Kết Đan", "Nguyên Anh", "Hóa Thần" };
        public static readonly string[] StageNames = { "sơ kỳ", "trung kỳ", "hậu kỳ", "đại viên mãn" };
        public static readonly string[] Titles = { "", "", "", "chân nhân", "lão tổ", "đại tu sĩ" };

        // Index by (int)Realm.
        public static readonly int[] Stages = { 1, 13, 4, 4, 4, 4 };
        public static readonly int[] LifespanYears = { 70, 120, 220, 500, 1000, 2000 };
        public static readonly float[] RequiredQi = { 0f, 1500f, 3000f, 5000f, 7000f, 9000f }; // local qi for full speed
        public static readonly float[] AbsorbPerMonth = { 0f, 8f, 25f, 60f, 150f, 300f };     // qi drawn from the surrounding blocks
        public static readonly float[] StageNeed = { 0f, 0f, 300f, 1200f, 4000f, 12000f };   // points per stage (LK uses LuyenKhiLayerNeed)
        // Chance to break INTO this realm from the peak of the one below. Tu tiên is meant to be brutally hard:
        // Trúc Cơ needs luck or a pill, Kết Đan makes an elder, Nguyên Anh an overlord, Hóa Thần a legend.
        public static readonly float[] BreakChance = { 0f, 1f, 0.1f, 0.03f, 0.012f, 0.003f };
        public const float HoaThanMinQi = 8000f; // only the richest phúc địa can carry a Nguyên Anh into Hóa Thần
        // Years to recover before the next attempt from this realm's peak.
        public static readonly int[] AttemptCooldownYears = { 1, 1, 3, 3, 3, 3 };
        public static readonly float[] Power = { 0f, 1f, 6f, 36f, 200f, 1200f };              // rough fighting strength per realm
        public static readonly float[] Hp = { 30f, 100f, 300f, 1000f, 3000f, 10000f };        // sinh lực (máu) per realm, +10% a stage

        public static float LuyenKhiLayerNeed(int layer) => 10f + layer * 4f; // layer 1..13

        public static float Need(Realm r, int stage) => r == Realm.LuyenKhi ? LuyenKhiLayerNeed(stage + 1) : StageNeed[(int)r];

        public static bool IsPeak(Realm r, int stage) => stage >= Stages[(int)r] - 1;

        public static string Describe(Realm r, int stage)
        {
            if (r == Realm.Mortal) return Names[0];
            if (r == Realm.LuyenKhi) return $"Luyện Khí tầng {stage + 1}";
            return $"{Names[(int)r]} {StageNames[stage]}";
        }
    }

    public static class SpiritRoots
    {
        public const int Kim = 1, Moc = 2, Thuy = 4, Hoa = 8, Tho = 16, Loi = 32, Phong = 64, Bang = 128;
        public const int Variant = Loi | Phong | Bang;
        public static readonly string[] ElementNames = { "Kim", "Mộc", "Thủy", "Hỏa", "Thổ", "Lôi", "Phong", "Băng" };

        public static int Count(int mask)
        {
            int n = 0;
            for (int m = mask; m != 0; m &= m - 1) n++;
            return n;
        }

        public static string Kind(int mask)
        {
            if ((mask & Variant) != 0) return "Dị linh căn";
            int n = Count(mask);
            return n == 1 ? "Thiên linh căn" : n <= 3 ? "Chân linh căn" : "Ngụy linh căn";
        }

        // Fewer elements mean a purer, faster root.
        public static float SpeedMultiplier(int mask)
        {
            if ((mask & Variant) != 0) return 5f;
            switch (Count(mask))
            {
                case 1: return 4f;
                case 2: return 2f;
                case 3: return 1f;
                case 4: return 0.5f;
                default: return 0.3f;
            }
        }

        public static string Elements(int mask)
        {
            var sb = new System.Text.StringBuilder();
            for (int b = 0; b < ElementNames.Length; b++)
            {
                if ((mask & (1 << b)) == 0) continue;
                if (sb.Length > 0) sb.Append('·');
                sb.Append(ElementNames[b]);
            }
            return sb.ToString();
        }
    }
}
