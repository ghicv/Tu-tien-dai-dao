using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    public sealed class Story
    {
        public long Tick;
        public string Title, Text;
        public int A = -1, B = -1, FA = -1, FB = -1;
        public float X = -1f, Y = -1f;
        public int Year => (int)(Tick / SimClock.DaysPerYear) + 1;
    }

    // Turns what happened into stories worth telling (GDD §12.3), and names the people history will remember.
    // Patterns are checked where they happen (a kill, a breakthrough, a sect falling, a war) and once a year.
    public sealed class StoryDetector
    {
        const float LegendFame = 100f;

        readonly Simulation _sim;
        public readonly List<Story> All = new List<Story>();
        readonly HashSet<string> _told = new HashSet<string>();

        public StoryDetector(Simulation sim)
        {
            _sim = sim;
            sim.History.Recorded += OnRecorded;
        }

        List<Cultivator> People => _sim.Cultivation.All;

        // Fame: what you take part in makes your name.
        void OnRecorded(HistoryRecord r)
        {
            float w = r.Importance * r.Importance;
            if (r.A >= 0 && r.A < People.Count) People[r.A].Fame += w;
            if (r.B >= 0 && r.B < People.Count && r.B != r.A) People[r.B].Fame += w * 0.5f;
        }

        void Tell(string key, long tick, string title, string text, int a = -1, int b = -1, int fa = -1, int fb = -1, float x = -1f, float y = -1f)
        {
            if (!_told.Add(key)) return;
            All.Add(new Story { Tick = tick, Title = title, Text = text, A = a, B = b, FA = fa, FB = fb, X = x, Y = y });
            _sim.Events.Add(tick, EventKind.Legend, 3, $"【{title}】 {text}", x, y, Fx.None, a, b, fa, fb);
        }

        string Sect(Cultivator c) => _sim.Cultivation.SectName(c);

        static int Years(long from, long to) => Mathf.Max(1, (int)((to - from) / SimClock.DaysPerYear));

        static int YearOf(long tick) => (int)(tick / SimClock.DaysPerYear) + 1;

        // ---------------------------------------------------------------- hooks

        public void OnKill(Cultivator killer, Cultivator victim, long tick)
        {
            var e = _sim.Entities;
            float x = e.X[victim.Entity], y = e.Y[victim.Entity];
            if (killer.Nemesis == victim.Index && killer.NemesisFor >= 0)
            {
                var lost = People[killer.NemesisFor];
                bool forMaster = lost.Index == killer.MasterIdx;
                string bond = forMaster ? "sư phụ" : "đệ tử";
                // A master settling scores for a disciple within a year or two is ordinary; a disciple avenging
                // their master, or a grudge nursed for years, is a story.
                if (forMaster || Years(killer.NemesisTick, tick) >= 5)
                Tell($"revenge:{killer.Index}:{victim.Index}", tick, "Báo thù rửa hận",
                    $"Năm {YearOf(killer.NemesisTick)}, {victim.Name} giết {lost.Name}, {bond} của {killer.Name}. " +
                    $"Ôm hận {Years(killer.NemesisTick, tick)} năm, nay {killer.Title} tự tay chém kẻ thù.",
                    killer.Index, victim.Index, killer.SectId, victim.SectId, x, y);
            }
            if (victim.Index == killer.MasterIdx)
                Tell($"betray:{killer.Index}", tick, "Khi sư diệt tổ",
                    $"{killer.Title} ra tay giết chính sư phụ mình là {victim.Title}.", killer.Index, victim.Index, killer.SectId, victim.SectId, x, y);
            if (killer.Realm < victim.Realm && victim.Realm >= Realm.KetDan)
                Tell($"upset:{killer.Index}:{victim.Index}", tick, "Vượt cấp trảm địch",
                    $"{killer.Title} ({Sect(killer)}) mới ở {Realms.Names[(int)killer.Realm]} mà chém được {victim.Title} ({Realms.Names[(int)victim.Realm]}).",
                    killer.Index, victim.Index, killer.SectId, victim.SectId, x, y);
            if (killer.Kills == 10)
                Tell($"butcher:{killer.Index}", tick, "Sát tinh giáng thế",
                    $"Trong tay {killer.Title} ({Sect(killer)}) đã có mười mạng tu sĩ; người đời nghe tên mà biến sắc.", killer.Index, -1, killer.SectId, -1, x, y);
        }

        public void OnBreakthrough(Cultivator c, long tick)
        {
            var e = _sim.Entities;
            float x = e.X[c.Entity], y = e.Y[c.Entity];
            bool wasTrash = (c.OriginRoots & SpiritRoots.Variant) == 0 && SpiritRoots.Count(c.OriginRoots) >= 4;
            // Ngũ linh căn reaching Kết Đan, or tứ linh căn reaching Nguyên Anh, against all odds.
            int elements = SpiritRoots.Count(c.OriginRoots);
            if (wasTrash && !c.Blessed && ((elements >= 5 && c.Realm >= Realm.KetDan) || c.Realm >= Realm.NguyenAnh))
                Tell($"trash:{c.Index}:{(int)c.Realm}", tick, "Phế vật nghịch thiên",
                    $"{c.Name} mang {SpiritRoots.Kind(c.OriginRoots)} ({SpiritRoots.Elements(c.OriginRoots)}), từng bị coi là phế vật, " +
                    $"nay đã đạt {Realms.Names[(int)c.Realm]} ở tuổi {c.AgeYears(tick):0}.", c.Index, -1, c.SectId, -1, x, y);
            if (c.Blessed && c.Realm >= Realm.NguyenAnh)
                Tell($"chosen:{c.Index}:{(int)c.Realm}", tick, "Thiên mệnh chi tử",
                    $"Người được Thiên Đạo điểm hóa, {c.Name}, nay đã thành {Realms.Names[(int)c.Realm]}.", c.Index, -1, c.SectId, -1, x, y);
            if (c.SectId < 0 && c.MasterIdx < 0 && c.Realm >= Realm.NguyenAnh) // never had a sư phụ
                Tell($"rogue:{c.Index}:{(int)c.Realm}", tick, "Tán tu nghịch tập",
                    $"Không tông môn, không sư thừa, tán tu {c.Name} một mình tu đến {Realms.Names[(int)c.Realm]}.", c.Index, -1, -1, -1, x, y);
            if (c.Realm == Realm.HoaThan)
                Tell($"hoathan:{c.Index}", tick, "Hóa Thần hiện thế",
                    $"{c.Title} ({Sect(c)}) đột phá Hóa Thần, cảnh giới vạn năm khó gặp; cả Thiên Nam chấn động.", c.Index, -1, c.SectId, -1, x, y);
        }

        public void OnFactionDestroyed(Faction winner, Faction loser, long tick)
        {
            var fs = _sim.Factions;
            var s = _sim.Settlements.All[loser.Id];
            float x = s.X + 0.5f, y = s.Y + 0.5f;
            if (winner != null && winner.ParentId == loser.Id)
                Tell($"surpass:{winner.Id}:{loser.Id}", tick, "Thanh xuất ư lam",
                    $"{fs.NameOf(winner.Id)} vốn tách ra từ {fs.NameOf(loser.Id)} năm {YearOf(winner.FoundedTick)}; nay quay về san bằng sơn môn cũ.",
                    -1, -1, winner.Id, loser.Id, x, y);
            else if (winner != null && loser.ParentId == winner.Id)
                Tell($"purge:{winner.Id}:{loser.Id}", tick, "Thanh lý môn hộ",
                    $"{fs.NameOf(winner.Id)} diệt phân tông phản đồ {fs.NameOf(loser.Id)}, rửa nhục năm xưa.", -1, -1, winner.Id, loser.Id, x, y);
            else if (Years(loser.FoundedTick, tick) >= 200)
                Tell($"ancient:{loser.Id}", tick, "Cổ tông diệt vong",
                    $"{fs.NameOf(loser.Id)} truyền thừa {Years(loser.FoundedTick, tick)} năm, nay {(winner != null ? $"bị {fs.NameOf(winner.Id)} diệt môn" : "tuyệt tự")}.",
                    -1, -1, winner?.Id ?? -1, loser.Id, x, y);
        }

        public void OnWarDeclared(Faction a, Faction b, Relation r, long tick)
        {
            var fs = _sim.Factions;
            if (r.Wars >= 4)
                Tell($"feud:{r.A}:{r.B}", tick, "Huyết hải thâm thù",
                    $"{fs.NameOf(a.Id)} và {fs.NameOf(b.Id)} đã {r.Wars} lần binh đao, mối thù truyền qua bao đời tông chủ.", -1, -1, a.Id, b.Id);
            var child = a.ParentId == b.Id ? a : b.ParentId == a.Id ? b : null;
            if (child != null && Years(child.FoundedTick, tick) >= 50) // old wounds, not the quarrel of the split itself
                Tell($"kin:{r.A}:{r.B}", tick, "Đồng môn tương tàn",
                    $"{fs.NameOf(a.Id)} và {fs.NameOf(b.Id)} vốn cùng một gốc, nay giương kiếm với nhau.", -1, -1, a.Id, b.Id);
        }

        // ---------------------------------------------------------------- yearly

        public void YearlyStep(long tick)
        {
            var fs = _sim.Factions;
            Faction top = null;
            foreach (var f in fs.All)
                if (f.Alive && (top == null || f.Power > top.Power)) top = f;
            if (top != null && top.FoundedTick > 0 && fs.AliveCount >= 4)
            {
                var s = _sim.Settlements.All[top.Id];
                Tell($"rise:{top.Id}", tick, "Tiểu tông thành bá chủ",
                    $"{fs.NameOf(top.Id)} do {top.FounderName ?? "vô danh"} lập năm {YearOf(top.FoundedTick)}, sau {Years(top.FoundedTick, tick)} năm đã đứng đầu thiên hạ.",
                    -1, -1, top.Id, -1, s.X + 0.5f, s.Y + 0.5f);
            }

            foreach (var c in People)
            {
                if (!c.Alive) continue;
                if (c.AgeYears(tick) >= 1000f)
                    Tell($"ancientone:{c.Index}", tick, "Lão quái ngàn năm",
                        $"{c.Title} ({Sect(c)}) đã sống qua một nghìn năm, chứng kiến bao tông môn hưng vong.", c.Index, -1, c.SectId);
                if (!c.Legend && c.Realm >= Realm.KetDan && c.Fame >= LegendFame)
                {
                    c.Legend = true;
                    string plain = c.Title;
                    c.Epithet = MakeEpithet(c);
                    Tell($"legend:{c.Index}", tick, "Danh chấn thiên hạ",
                        $"{plain} ({Sect(c)}) được người đời tôn xưng là \"{c.Epithet}\".", c.Index, -1, c.SectId);
                }
            }
        }

        readonly HashSet<string> _epithets = new HashSet<string>();

        // Danh hiệu from what they are and what they did, e.g. "Xích Viêm Ma Tôn", "Huyết Kiếm Chân Quân"; never repeated.
        string MakeEpithet(Cultivator c)
        {
            string[] prefixes = c.Kills >= 8 ? new[] { "Huyết Sát", "Nhân Đồ", "Tu La" }
                : c.Kills >= 4 ? new[] { "Huyết Kiếm", "Đoạt Mệnh", "Truy Hồn" }
                : ElementEpithets(c.Roots);
            string suffix;
            if (c.Demonic) suffix = c.Realm >= Realm.NguyenAnh ? "Ma Tổ" : "Ma Tôn";
            else if (FoundedASect(c)) suffix = "Tổ Sư";
            else if (c.SectId < 0) suffix = c.Realm >= Realm.NguyenAnh ? "Tán Tiên" : "Tán Nhân";
            else suffix = c.Realm >= Realm.NguyenAnh ? "Chân Quân" : "Chân Nhân";
            foreach (var p in prefixes)
                if (_epithets.Add($"{p} {suffix}")) return $"{p} {suffix}";
            for (int k = 2; ; k++) // all taken: the second, third… of that name
                if (_epithets.Add($"{prefixes[0]} {suffix} đời thứ {k}")) return $"{prefixes[0]} {suffix} đời thứ {k}";
        }

        bool FoundedASect(Cultivator c)
        {
            foreach (var f in _sim.Factions.All)
                if (f.FounderName == c.Name) return true;
            return false;
        }

        static string[] ElementEpithets(int roots)
        {
            if ((roots & SpiritRoots.Loi) != 0) return new[] { "Thiên Lôi", "Tử Điện", "Lôi Âm" };
            if ((roots & SpiritRoots.Phong) != 0) return new[] { "Cuồng Phong", "Thanh Phong", "Phong Hành" };
            if ((roots & SpiritRoots.Bang) != 0) return new[] { "Hàn Băng", "Băng Phách", "Huyền Băng" };
            if ((roots & SpiritRoots.Hoa) != 0) return new[] { "Xích Viêm", "Liệt Diễm", "Chu Tước" };
            if ((roots & SpiritRoots.Kim) != 0) return new[] { "Kim Kiếm", "Canh Kim", "Thiết Kiếm" };
            if ((roots & SpiritRoots.Thuy) != 0) return new[] { "Huyền Thủy", "Bích Ba", "Thương Lãng" };
            if ((roots & SpiritRoots.Moc) != 0) return new[] { "Thanh Mộc", "Trường Xuân", "Bích Đằng" };
            return new[] { "Hậu Thổ", "Huyền Nham", "Hoàng Sa" };
        }

        public void Legends(List<Cultivator> into)
        {
            into.Clear();
            foreach (var c in People)
                if (c.Legend) into.Add(c);
            into.Sort((a, b) => b.Fame != a.Fame ? b.Fame.CompareTo(a.Fame) : a.Index.CompareTo(b.Index));
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, All.Count);
            foreach (var c in People) StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(c.Fame) ^ (c.Legend ? 1L << 40 : 0));
        }
    }
}
