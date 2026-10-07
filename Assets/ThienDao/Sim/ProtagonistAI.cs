using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    // Nhân vật chính: a cultivator the player watches lives like the hero of a xianxia novel instead of idling at
    // home. Each month, when free, they weigh their situation (danger, a blood debt, a bottleneck, poor qi, no
    // master, ambition) and pick a goal: flee, take revenge, buy a pill at the market, hunt for a cơ duyên in the
    // wilds, look for a better cave, go lịch luyện, seek a master, take a disciple, found a sect — or close the door
    // and bế quan for years. Everything they do is written into their biography.
    public sealed class ProtagonistAI
    {
        const float Reach = 300f;
        const string BigSeclusion = "đang bế quan khổ tu";

        static readonly string[] SeclusionWays =
        {
            "đóng cửa động phủ", "bày trận pháp quanh động phủ", "nuốt linh đan, luyện hóa linh thạch",
            "tham ngộ công pháp", "dựng cấm chế, ngồi tĩnh tọa"
        };

        readonly Simulation _sim;
        readonly WorldData _w;

        public ProtagonistAI(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0x9A0Bu, (int)tick, salt));

        CultivationSystem Cult => _sim.Cultivation;

        void Note(Cultivator c, long tick, int importance, string text, Fx fx = Fx.None)
        {
            var e = _sim.Entities;
            _sim.Events.Add(tick, EventKind.Fortune, importance, text, e.X[c.Entity], e.Y[c.Entity], fx, c.Index, -1, c.SectId);
        }

        public void MonthlyStep(long tick)
        {
            var all = Cult.All;
            for (int k = 0; k < all.Count; k++)
            {
                var c = all[k];
                if (!c.Alive || !c.Watched || c.AtWar || c.HuntTarget >= 0 || c.Travelling) continue;
                var rng = RngFor(tick, c.Index);
                if (c.Away)
                {
                    if (!c.GoalDone)
                    {
                        c.GoalDone = true;
                        AtDestination(c, tick, ref rng);
                    }
                    continue;
                }
                if (c.Goal == Goal.Seclusion && tick < c.GoalUntil) continue; // the door stays shut
                if (c.Goal == Goal.Seclusion && c.GoalText == BigSeclusion)
                    Note(c, tick, 1, Mark(c) == c.GoalMark
                        ? $"{c.Title} xuất quan, tu vi vẫn dừng ở {c.RealmText}{(Realms.IsPeak(c.Realm, c.Stage) ? ", bình cảnh khó phá" : "")}."
                        : $"{c.Title} xuất quan, tu vi tinh tiến tới {c.RealmText}.");
                if (c.Goal == Goal.Seclusion)
                {
                    c.Goal = Goal.None; // free again: whatever Decide picks (or nothing) starts from here
                    c.GoalText = null;
                }
                if (c.Goal != Goal.None && c.Goal != Goal.Seclusion)
                {
                    // Home from the last undertaking: settle what was gained before setting out again.
                    Begin(c, Goal.Seclusion, "đang tĩnh tu", tick);
                    c.GoalUntil = tick + rng.Range(180, 540);
                    continue;
                }
                Decide(c, tick, ref rng);
            }
        }

        // ---------------------------------------------------------------- choosing what to do

        void Decide(Cultivator c, long tick, ref DetRandom rng)
        {
            var all = Cult.All;
            var e = _sim.Entities;

            // 1. Someone far stronger is hunting them: run.
            foreach (var h in all)
            {
                if (!h.Alive || h.HuntTarget != c.Index || CombatSystem.Strength(h) <= CombatSystem.Strength(c) * 1.3f) continue;
                if (FindSpot(c.HomeX, c.HomeY, Reach, 200f, true, ref rng, out float fx, out float fy))
                {
                    Begin(c, Goal.Flee, "đang lẩn trốn kẻ thù", tick);
                    Cult.Travel(c, fx, fy, Trip.Relocate);
                    Note(c, tick, 2, $"{c.Title} biết {h.Title} đang truy sát mình, lặng lẽ bỏ động phủ trốn đi xa.");
                    return;
                }
            }

            // 2. A blood debt they can now settle.
            if (c.Nemesis >= 0 && all[c.Nemesis].Alive && CombatSystem.Strength(c) >= CombatSystem.Strength(all[c.Nemesis]) * 0.9f && c.Realm >= Realm.TrucCo)
            {
                _sim.Combat.StartHunt(c, tick);
                return;
            }

            // 3. A tán tu without a master looks for a sect to learn from.
            if (c.SectId < 0 && c.Realm <= Realm.TrucCo && rng.NextFloat() < 0.5f)
            {
                var sect = SectToJoin(c);
                if (sect != null)
                {
                    Begin(c, Goal.JoinSect, $"đang tìm đến {sect.BaseName} bái sư", tick);
                    Cult.JoinSectByChoice(c, sect, tick);
                    var master = Cult.MasterOfDisciple(c);
                    Note(c, tick, 2, $"{c.Title} tìm đến {sect.BaseName} bái sư học đạo{(master != null ? $", được {master.Title} thu làm đệ tử" : "")}.");
                    return;
                }
            }

            // 4. A strong tán tu with ambition founds their own sect.
            if (c.SectId < 0 && c.Realm >= Realm.KetDan && c.Ambition > 0.4f && rng.NextFloat() < 0.25f && _sim.Factions.TryFound(c, tick) != null)
                return;

            // 5. An elder takes a disciple now and then.
            if (c.SectId >= 0 && c.Realm >= Realm.KetDan && tick - c.LastDiscipleTick > 20L * SimClock.DaysPerYear && rng.NextFloat() < 0.3f)
            {
                Cultivator pick = null;
                foreach (var d in all)
                    if (d.Alive && d.SectId == c.SectId && d.Realm == Realm.LuyenKhi && d.MasterIdx != c.Index && (pick == null || d.Roots < pick.Roots || d.MasterIdx < 0))
                        pick = d;
                if (pick != null)
                {
                    pick.MasterIdx = c.Index;
                    c.LastDiscipleTick = tick;
                    Note(c, tick, 1, $"{c.Title} thu nhận {pick.Name} ({SpiritRoots.Kind(pick.Roots)}) làm đệ tử chân truyền.");
                }
            }

            // 6. At a bottleneck: a pill from the market, or a cơ duyên from the wilds.
            if (Realms.IsPeak(c.Realm, c.Stage) && c.Realm < Realm.HoaThan && c.Pills == 0)
            {
                var market = Market(c);
                float price = PillPrice(c.Realm + 1, market);
                if (c.Stones >= price && market != null)
                {
                    Begin(c, Goal.Market, $"đang đến phường thị {market.Name} mua {Lore.PillFor(c.Realm + 1)}", tick);
                    Cult.Travel(c, market.X + 0.5f, market.Y + 0.5f, Trip.Excursion);
                    return;
                }
                if (SeekFortune(c, tick, ref rng)) return;
            }

            // 6b. Linh thạch to spare: a pháp bảo from the market.
            if (c.Treasures < 3 && c.Stones >= TreasurePrice(c) && rng.NextFloat() < 0.4f)
            {
                var market = Market(c);
                if (market != null)
                {
                    Begin(c, Goal.Market, $"đang đến phường thị {market.Name} tìm pháp bảo", tick);
                    Cult.Travel(c, market.X + 0.5f, market.Y + 0.5f, Trip.Excursion);
                    return;
                }
            }

            // 7. Qi too thin for their realm: a better cave.
            float here = _sim.Qi.SampleQi((int)c.HomeX, (int)c.HomeY);
            if (here < Realms.RequiredQi[(int)c.Realm] * 0.8f && c.SectId < 0)
            {
                if (FindRichSpot(c, here * 1.3f, ref rng, out float qx, out float qy))
                {
                    Begin(c, Goal.SeekCave, "đang tìm động phủ mới", tick);
                    Cult.Travel(c, qx, qy, Trip.Relocate);
                    Note(c, tick, 1, $"{c.Title} rời nơi linh khí cạn kiệt, đi tìm động phủ mới.");
                    return;
                }
            }

            // 8. Otherwise: bế quan, lịch luyện, or a search for cơ duyên.
            float roll = rng.NextFloat();
            if (roll < 0.45f)
            {
                int years = rng.Range(1, 4);
                Begin(c, Goal.Seclusion, BigSeclusion, tick);
                c.GoalUntil = tick + years * SimClock.DaysPerYear;
                string how = SeclusionWays[rng.Range(0, SeclusionWays.Length)];
                Note(c, tick, 1, $"{c.Title} {how}, bế quan {years} năm.");
            }
            else if (roll < 0.75f)
            {
                if (FindSpot(c.HomeX, c.HomeY, Reach * 0.7f, 60f, false, ref rng, out float tx, out float ty))
                {
                    Begin(c, Goal.Training, "đang lịch luyện", tick);
                    c.Outing = Outing.Training;
                    Cult.Travel(c, tx, ty, Trip.Excursion);
                    Note(c, tick, 1, $"{c.Title} rời động phủ, một mình đi lịch luyện.");
                }
            }
            else SeekFortune(c, tick, ref rng);
        }

        void Begin(Cultivator c, Goal goal, string text, long tick)
        {
            c.Goal = goal;
            c.GoalText = text;
            c.GoalUntil = tick;
            c.GoalDone = false;
            c.GoalMark = Mark(c);
        }

        static int Mark(Cultivator c) => (int)c.Realm * 100 + c.Stage;

        static float TreasurePrice(Cultivator c) => 250f * (int)c.Realm * (1 + c.Treasures);

        static readonly string[] TrainingFinds =
        {
            "chém một con yêu lang, lấy được yêu đan", "luận đạo với một tán tu già, ngộ ra đôi điều", "ngộ ra một thức kiếm quyết mới",
            "giúp một thôn trang diệt yêu thú, được tạ lễ", "đối luyện với tu sĩ đồng giai, chiêu thức thuần thục hơn", "thu được vài khối khoáng thạch hiếm"
        };

        bool SeekFortune(Cultivator c, long tick, ref DetRandom rng)
        {
            // A bí cảnh people speak of, if one lies within reach and they think they can survive it.
            var known = _sim.Relics.NearestKnown(c.HomeX, c.HomeY, Reach);
            if (known != null && RelicSystem.DeathChance(known, c) < 0.4f && _w.IsWalkable(known.X + 0.5f, known.Y + 0.5f))
            {
                Begin(c, Goal.SeekFortune, $"đang đến {known.Name}", tick);
                c.Outing = Outing.HerbHunting;
                Cult.Travel(c, known.X + 0.5f, known.Y + 0.5f, Trip.Excursion);
                Note(c, tick, 1, $"{c.Title} nghe tin {known.Name} xuất thế, lên đường thám hiểm.");
                return true;
            }
            // Wild land with rich qi that no sect holds: where herbs and old caves are found.
            float best = 0f, bx = 0f, by = 0f;
            for (int k = 0; k < 20; k++)
            {
                float x = c.HomeX + rng.Range(-Reach, Reach), y = c.HomeY + rng.Range(-Reach, Reach);
                if (!_w.InBounds((int)x, (int)y) || !_w.IsWalkable(x, y) || _sim.Factions.OwnerAt(x, y) != null) continue;
                float q = _sim.Qi.SampleQi((int)x, (int)y);
                if (q > best) { best = q; bx = x; by = y; }
            }
            if (best <= 0f) return false;
            Begin(c, Goal.SeekFortune, "đang tìm kiếm cơ duyên", tick);
            c.Outing = Outing.HerbHunting;
            Cult.Travel(c, bx, by, Trip.Excursion);
            Note(c, tick, 1, $"{c.Title} nghe đồn nơi hoang sơn có linh vật xuất thế, lên đường tìm kiếm cơ duyên.");
            return true;
        }

        // ---------------------------------------------------------------- what they find when they get there

        void AtDestination(Cultivator c, long tick, ref DetRandom rng)
        {
            switch (c.Goal)
            {
                case Goal.Market:
                {
                    var next = c.Realm + 1;
                    float price = PillPrice(next, Market(c));
                    if (Realms.IsPeak(c.Realm, c.Stage) && c.Pills == 0 && c.Stones >= price)
                    {
                        c.Stones -= price;
                        c.Pills++;
                        Note(c, tick, 1, $"{c.Title} bỏ ra {price:0} linh thạch ở phường thị, mua được một viên {Lore.PillFor(next)}.");
                    }
                    else if (c.Stones >= TreasurePrice(c))
                    {
                        float cost = TreasurePrice(c);
                        c.Stones -= cost;
                        c.Treasures++;
                        c.TreasureName = _w.Lore.Treasures[rng.Range(0, _w.Lore.Treasures.Length)];
                        Note(c, tick, 1, $"{c.Title} dốc {cost:0} linh thạch, mua được pháp bảo {c.TreasureName}.");
                    }
                    else Note(c, tick, 0, $"{c.Title} dạo phường thị mà không đủ linh thạch mua thứ mình cần.");
                    break;
                }
                case Goal.Training:
                {
                    c.Comprehension = Mathf.Min(1f, c.Comprehension + 0.03f);
                    c.DaoHeart = Mathf.Min(1f, c.DaoHeart + 0.02f);
                    float danger = rng.NextFloat();
                    if (danger < 0.12f) Wounded(c, tick, ref rng, "gặp yêu thú cường đại trong lúc lịch luyện");
                    else
                    {
                        c.Stones += rng.Range(10f, 40f) * (int)c.Realm;
                        Note(c, tick, 1, $"{c.Title} trong chuyến lịch luyện {TrainingFinds[rng.Range(0, TrainingFinds.Length)]}.");
                    }
                    break;
                }
                case Goal.SeekFortune:
                {
                    // A real bí cảnh at the spot: explore it rather than roll for a find.
                    var e = _sim.Entities;
                    var relic = _sim.Relics.At((int)e.X[c.Entity], (int)e.Y[c.Entity], 8f);
                    if (relic != null)
                    {
                        _sim.Relics.Explore(c, relic, tick);
                        break;
                    }
                    float r = rng.NextFloat();
                    if (r < 0.08f)
                    {
                        c.Treasures++;
                        c.TreasureName = _w.Lore.Treasures[rng.Range(0, _w.Lore.Treasures.Length)];
                        Note(c, tick, 2, $"{c.Title} phát hiện động phủ của cổ tu sĩ, đoạt được pháp bảo {c.TreasureName}!", Fx.Blessing);
                    }
                    else if (r < 0.2f)
                    {
                        c.Pills++;
                        Note(c, tick, 2, $"{c.Title} tìm thấy một viên {Lore.PillFor(c.Realm + 1)} trong di tích cổ.", Fx.Blessing);
                    }
                    else if (r < 0.5f)
                    {
                        string herb = _w.Lore.Herbs[rng.Range(0, _w.Lore.Herbs.Length)];
                        c.Progress += Realms.Need(c.Realm, c.Stage) * 0.25f;
                        c.Stones += rng.Range(20f, 80f) * (int)c.Realm;
                        Note(c, tick, 1, $"{c.Title} hái được {herb}, luyện hóa một phần, phần còn lại đổi lấy linh thạch.");
                    }
                    else if (r < 0.62f) Wounded(c, tick, ref rng, "chạm phải cấm chế cổ xưa");
                    else Note(c, tick, 0, $"{c.Title} lùng sục nhiều ngày mà không thu hoạch được gì.");
                    break;
                }
            }
        }

        void Wounded(Cultivator c, long tick, ref DetRandom rng, string how)
        {
            // Luyện Khí die more easily; for everyone it costs cultivation.
            float death = c.Realm == Realm.LuyenKhi ? 0.15f : c.Realm == Realm.TrucCo ? 0.06f : 0.02f;
            if (rng.NextFloat() < death)
            {
                Cult.Slay(c, null, tick, $"{c.Title} ({Cult.SectName(c)}) {how}, vẫn lạc nơi đất khách.", 2);
                return;
            }
            c.Progress *= 0.75f;
            Note(c, tick, 1, $"{c.Title} {how}, trọng thương thoát chết, phải về động phủ tĩnh dưỡng.");
            Cult.BringHome(c);
        }

        // ---------------------------------------------------------------- helpers

        // What a pill costs at this market: the base price times how scarce pills are there (TradeSystem).
        float PillPrice(Realm next, Settlement market = null) =>
            40f * Mathf.Pow(3f, (int)next - (int)Realm.TrucCo) * (market != null ? _sim.Trade.PriceFactor(market, Good.Pill) : 1f);

        // The biggest town within reach: phường thị where pills are sold.
        Settlement Market(Cultivator c)
        {
            Settlement best = null;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                float dx = s.X - c.HomeX, dy = s.Y - c.HomeY;
                if (dx * dx + dy * dy > Reach * Reach) continue;
                if (best == null || s.Population > best.Population) best = s;
            }
            return best;
        }

        Settlement SectToJoin(Cultivator c)
        {
            Settlement best = null;
            float bestD = Reach * Reach;
            foreach (var f in _sim.Factions.All)
            {
                if (!f.Alive || f.Demonic != c.Demonic) continue;
                var s = _sim.Settlements.All[f.Id];
                float dx = s.X - c.HomeX, dy = s.Y - c.HomeY, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        bool FindSpot(float ox, float oy, float reach, float minDist, bool unclaimed, ref DetRandom rng, out float x, out float y)
        {
            for (int k = 0; k < 16; k++)
            {
                x = ox + rng.Range(-reach, reach);
                y = oy + rng.Range(-reach, reach);
                if (!_w.InBounds((int)x, (int)y) || !_w.IsWalkable(x, y)) continue;
                if ((x - ox) * (x - ox) + (y - oy) * (y - oy) < minDist * minDist) continue;
                if (unclaimed && _sim.Factions.OwnerAt(x, y) != null) continue;
                return true;
            }
            x = y = 0f;
            return false;
        }

        bool FindRichSpot(Cultivator c, float atLeast, ref DetRandom rng, out float bx, out float by)
        {
            bx = by = 0f;
            float best = atLeast;
            bool found = false;
            for (int k = 0; k < 16; k++)
            {
                float x = c.HomeX + rng.Range(-Reach, Reach), y = c.HomeY + rng.Range(-Reach, Reach);
                if (!_w.InBounds((int)x, (int)y) || !_w.IsWalkable(x, y)) continue;
                float q = _sim.Qi.SampleQi((int)x, (int)y);
                if (q > best) { best = q; bx = x; by = y; found = true; }
            }
            return found;
        }
    }
}
