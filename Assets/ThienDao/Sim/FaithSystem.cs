using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public enum PrayerKind : byte { Rain, Cure, Harvest, Protection }

    public sealed class Prayer
    {
        public int Settlement;
        public PrayerKind Kind;
        public long Start, Until;
        public int Beast = -1; // Protection: the hung thú bearing down on them
    }

    // Tín ngưỡng & cầu nguyện (devlog 27). A village in trouble (drought, plague, an empty granary, a hung thú on
    // the road) may pray to Thiên Đạo. Answer it in any way that ends the trouble (rain, a blessing, a bolt on
    // the beast) and its people's faith grows, word spreads to the villages around, and a faithful people
    // raise a miếu, and now and then a child of heaven's favour rises from among them. Leave them to it and the
    // faith cools; a people who lose it all let the miếu fall and may turn to a tà giáo that breeds ma tu.
    // Trouble that passes on its own (or a cultivator who kills the beast first) owes heaven nothing.
    public sealed class FaithSystem
    {
        public const float StartFaith = 30f;
        public const float ShrineFloor = 15f;  // below this no miếu stands
        public const float ShrineFaith = 60f;  // from this a village raises one, a hamlet included
        const int MaxOpen = 8;
        const float HungryMonths = 1f;         // food for less than a month: the granary is empty

        public static readonly string[] KindNames = { "cầu mưa", "cầu trừ ôn dịch", "cầu mùa màng", "cầu trừ hung thú" };
        public static readonly string[] KindShort = { "Cầu mưa", "Cầu trừ dịch", "Cầu mùa", "Cầu trừ yêu" };

        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly List<Prayer> Open = new List<Prayer>();
        public int Raised, Answered, Ignored;
        // Set while a Thiên Đạo command runs (Simulation.ApplyPending): what dies then, heaven killed.
        [System.NonSerialized] public bool Heaven;

        public FaithSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0xFA17Bu, (int)tick, salt));

        public Prayer PrayerOf(int settlement)
        {
            foreach (var p in Open)
                if (p.Settlement == settlement) return p;
            return null;
        }

        static void Shift(Settlement s, float d) => s.Faith = Mathf.Clamp(s.Faith + d, 0f, 100f);

        // ---------------------------------------------------------------- monthly: prayers rise and lapse

        public void MonthlyStep(long tick)
        {
            Resolve(tick);
            Raise(tick);
        }

        void Resolve(long tick)
        {
            var all = _sim.Settlements.All;
            for (int k = Open.Count - 1; k >= 0; k--)
            {
                var p = Open[k];
                var s = all[p.Settlement];
                if (!s.Alive || !StillInNeed(p, s, tick))
                {
                    Open.RemoveAt(k); // gone, or the trouble passed by itself: no thanks owed, no grudge
                    continue;
                }
                if (tick < p.Until) continue;
                Open.RemoveAt(k);
                Ignored++;
                Shift(s, -15f);
                _sim.Events.Add(tick, EventKind.Faith, 1,
                    $"Lời {KindNames[(int)p.Kind]} của dân {s.Name} không được đáp lại; lòng người nguội lạnh với Thiên Đạo.", s.X + 0.5f, s.Y + 0.5f);
            }
        }

        bool StillInNeed(Prayer p, Settlement s, long tick)
        {
            switch (p.Kind)
            {
                case PrayerKind.Rain: return _sim.Disasters.DroughtMonthsLeft(s.X, s.Y, tick) >= 0;
                case PrayerKind.Cure: return _sim.Disasters.IsInfected(s.Id);
                case PrayerKind.Harvest: return s.Food < s.Population * HungryMonths;
                default:
                    var beasts = _sim.Beasts.All;
                    return p.Beast >= 0 && p.Beast < beasts.Count && beasts[p.Beast].Alive && beasts[p.Beast].Rampage;
            }
        }

        void Raise(long tick)
        {
            if (Open.Count >= MaxOpen) return;
            var rng = RngFor(tick, 1);
            // The towns a hung thú is bearing down on cry out first.
            foreach (var b in _sim.Beasts.All)
                if (b.Alive && b.Rampage && b.Prey >= 0 && b.Prey < _sim.Settlements.All.Count)
                    TryRaise(_sim.Settlements.All[b.Prey], PrayerKind.Protection, tick, ref rng, b);
            // A third of the villages each month (staggered: the checks are cheap, but there is no hurry).
            var all = _sim.Settlements.All;
            int month = (int)(tick / SimClock.DaysPerMonth);
            for (int i = month % 3; i < all.Count && Open.Count < MaxOpen; i += 3)
            {
                var s = all[i];
                if (!s.Alive || s.Sect || tick - s.LastPrayer < 2L * SimClock.DaysPerYear) continue;
                PrayerKind kind;
                if (_sim.Disasters.IsInfected(s.Id)) kind = PrayerKind.Cure;
                else if (_sim.Disasters.DroughtMonthsLeft(s.X, s.Y, tick) >= 2) kind = PrayerKind.Rain;
                else if (s.Population >= 20 && s.Food < s.Population * HungryMonths) kind = PrayerKind.Harvest;
                else continue;
                TryRaise(s, kind, tick, ref rng, null);
            }
        }

        void TryRaise(Settlement s, PrayerKind kind, long tick, ref DetRandom rng, Beast beast)
        {
            if (!s.Alive || s.Sect || Open.Count >= MaxOpen || PrayerOf(s.Id) != null) return;
            if (tick - s.LastPrayer < 2L * SimClock.DaysPerYear) return;
            // The faithful turn to heaven sooner; those who have given up on it hardly at all.
            if (rng.NextFloat() >= 0.1f + 0.5f * s.Faith / 100f) return;
            s.LastPrayer = tick;
            Raised++;
            Open.Add(new Prayer
            {
                Settlement = s.Id, Kind = kind, Start = tick, Beast = beast?.Index ?? -1,
                Until = tick + (kind == PrayerKind.Protection ? 4 : 6) * (long)SimClock.DaysPerMonth
            });
            string text;
            switch (kind)
            {
                case PrayerKind.Rain: text = $"Đại hạn kéo dài, dân {s.Name} lập đàn cầu mưa, khấu đầu xin Thiên Đạo rủ lòng thương."; break;
                case PrayerKind.Cure: text = $"Ôn dịch hoành hành, dân {s.Name} thắp hương cầu Thiên Đạo trừ ôn thần."; break;
                case PrayerKind.Harvest: text = $"Kho lẫm cạn kiệt, dân {s.Name} quỳ lạy cầu Thiên Đạo ban một mùa bội thu."; break;
                default: text = $"{beast.Title} đang kéo tới, dân {s.Name} khóc than cầu Thiên Đạo cứu mạng."; break;
            }
            _sim.Events.Add(tick, EventKind.Faith, 2, text, s.X + 0.5f, s.Y + 0.5f);
        }

        // ---------------------------------------------------------------- answers (hooked from the acts of Thiên Đạo)

        void Answer(Prayer p, Settlement s, long tick, string how)
        {
            Open.Remove(p);
            Answered++;
            Shift(s, 25f);
            // Word of it spreads to the villages around.
            foreach (var o in _sim.Settlements.All)
                if (o.Alive && o != s && !o.Sect && (o.X - s.X) * (o.X - s.X) + (o.Y - s.Y) * (o.Y - s.Y) < 40 * 40) Shift(o, 5f);
            _sim.Events.Add(tick, EventKind.Faith, 2,
                $"Thiên Đạo đáp lời {KindNames[(int)p.Kind]}: {how}, dân {s.Name} dập đầu tạ ơn, hương khói nghi ngút.", s.X + 0.5f, s.Y + 0.5f, Fx.Blessing);
            _sim.Destiny?.OnAnswered();
        }

        // Rain sent by Thiên Đạo: every drought prayer (and hungry granary) it falls on is answered.
        public void OnRain(int x, int y, int r, long tick)
        {
            var all = _sim.Settlements.All;
            for (int k = Open.Count - 1; k >= 0; k--)
            {
                var p = Open[k];
                if (p.Kind != PrayerKind.Rain && p.Kind != PrayerKind.Harvest) continue;
                var s = all[p.Settlement];
                if ((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) <= r * r) Answer(p, s, tick, "mưa rào trút xuống");
            }
        }

        // Ban phúc on a village: its granary fills and its sickness is driven out; whatever it prayed for, answered.
        public void OnBlessed(Settlement s, long tick)
        {
            if (s == null || !s.Alive) return;
            var p = PrayerOf(s.Id);
            if (p != null && p.Kind != PrayerKind.Protection) Answer(p, s, tick, p.Kind == PrayerKind.Cure ? "ôn thần lui đi" : "phúc lành giáng xuống");
            else Shift(s, 5f);
        }

        // Thiên phạt on a village: fear, and resentment.
        public void OnSmitten(Settlement s)
        {
            if (s != null && s.Alive) Shift(s, -10f);
        }

        // A hung thú died: if heaven struck it down, the towns that prayed against it are answered; if a
        // cultivator did, they thank the one who came, and heaven gets a little of the credit.
        public void OnBeastDied(Beast b, Cultivator killer, long tick)
        {
            var all = _sim.Settlements.All;
            for (int k = Open.Count - 1; k >= 0; k--)
            {
                var p = Open[k];
                if (p.Kind != PrayerKind.Protection || p.Beast != b.Index) continue;
                var s = all[p.Settlement];
                if (Heaven) Answer(p, s, tick, $"thiên lôi diệt {b.Title}");
                else
                {
                    Open.RemoveAt(k);
                    Shift(s, 3f);
                    if (killer != null)
                        _sim.Events.Add(tick, EventKind.Faith, 1, $"Dân {s.Name} tạ ơn {killer.Title} đã trừ {b.Title}, cho là Thiên Đạo phái người tới cứu.",
                            s.X + 0.5f, s.Y + 0.5f, Fx.None, killer.Index);
                }
            }
        }

        // ---------------------------------------------------------------- yearly: what faith (or its loss) brings

        public void YearlyStep(long tick)
        {
            var settlements = _sim.Settlements;
            foreach (var s in settlements.All)
            {
                if (!s.Alive || s.Sect) continue;
                bool shrine = settlements.HasCivic(s, ObjectType.Shrine);
                // Faith drifts back toward the ordinary; a miếu keeps it warm.
                float target = shrine ? StartFaith + 15f : StartFaith;
                Shift(s, Mathf.Clamp(target - s.Faith, -1f, 1f));

                if (s.Faith >= ShrineFaith && !shrine && settlements.BuildShrine(s))
                    _sim.Events.Add(tick, EventKind.Faith, 1, $"Dân {s.Name} dựng miếu thờ Thiên Đạo, hương khói quanh năm.", s.X + 0.5f, s.Y + 0.5f);
                else if (s.Faith < ShrineFloor && shrine && settlements.AbandonShrine(s))
                    _sim.Events.Add(tick, EventKind.Faith, 1, $"Miếu Thiên Đạo ở {s.Name} bị bỏ hoang, cỏ mọc lấp lối.", s.X + 0.5f, s.Y + 0.5f);

                if (s.Faith < 80f && s.Faith > 8f) continue;
                var rng = RngFor(tick, 100 + s.Id);
                if (s.Faith >= 80f && rng.NextFloat() < 0.03f)
                {
                    // Heaven remembers the faithful: one of them is born to the path.
                    var c = _sim.Cultivation.RiseFromVillage(s, tick, false, out float age);
                    if (c != null)
                        _sim.Events.Add(tick, EventKind.Faith, 2,
                            $"Hương khói ở {s.Name} chưa từng dứt, trời giáng phúc: {c.Name} ({age:0} tuổi) thức tỉnh {SpiritRoots.Kind(c.Roots)}.",
                            s.X + 0.5f, s.Y + 0.5f, Fx.Blessing, c.Index, -1, c.SectId);
                }
                else if (s.Faith <= 8f && _sim.Rules.DemonicAllowed && rng.NextFloat() < 0.015f)
                {
                    // A people who gave up on heaven listen to whoever promises them more.
                    var c = _sim.Cultivation.RiseFromVillage(s, tick, true, out float age);
                    if (c != null)
                        _sim.Events.Add(tick, EventKind.Faith, 2,
                            $"Dân {s.Name} quay lưng với Thiên Đạo, thờ phụng tà thần; {c.Name} ({age:0} tuổi) theo tà giáo, thành ma tu.",
                            s.X + 0.5f, s.Y + 0.5f, Fx.DemonBlast, c.Index);
                }
            }
        }

        public int FaithfulCount(float atLeast)
        {
            int n = 0;
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && !s.Sect && s.Faith >= atLeast) n++;
            return n;
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, Raised | ((long)Answered << 20) | ((long)Ignored << 40));
            foreach (var p in Open) StateHash.Add(ref h, p.Settlement | ((long)p.Kind << 24) | (p.Until << 28));
            foreach (var s in _sim.Settlements.All) StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(s.Faith));
        }
    }
}
