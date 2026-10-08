using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // What a yêu thú is: its kind follows the land it was born on (BeastSystem.KindFor) and sets its look, its
    // name and whether it flies.
    public enum BeastKind : byte { Wolf, Tiger, Bear, Serpent, Scorpion, Eagle, Fox, Ox, Ape, Dragon, Qilin, Bat, Turtle, Chaos, Count }

    // Hung thú (sự kiện thế giới): now and then a great beast of lục giai or more rises and goes from town to town
    // killing, and the sects around must band together (liên minh trảm yêu) to bring it down.
    public sealed partial class BeastSystem
    {
        public static readonly string[] KindNames =
        {
            "Yêu lang", "Yêu hổ", "Yêu hùng", "Cự mãng", "Độc hạt", "Yêu điêu", "Yêu hồ", "Ma ngưu", "Hung viên", "Phi long", "Hỏa kỳ lân", "Huyết bức",
            "Huyền quy", "Hỗn độn"
        };

        public static bool Flies(BeastKind k) => k == BeastKind.Eagle || k == BeastKind.Dragon || k == BeastKind.Bat || k == BeastKind.Chaos;

        // ---------------------------------------------------------------- kinds

        // The ground decides: tigers in the woods of the south, bears in the cold, scorpions in the sand, oxen and
        // bats in Ma Đạo, apes and eagles in the mountains, serpents in swamps, giao long by the sea.
        BeastKind KindFor(float x, float y, ref DetRandom rng)
        {
            int cx = Mathf.Clamp((int)x, 0, _w.W - 1), cy = Mathf.Clamp((int)y, 0, _w.H - 1);
            int i = _w.Idx(cx, cy);
            var t = _w.Terrain[i];
            var region = (RegionKind)_w.Region[i];
            bool coast = _w.WaterDist[i] <= 3;
            float r = rng.NextFloat();
            if (region == RegionKind.MaDao) return r < 0.45f ? BeastKind.Ox : r < 0.8f ? BeastKind.Bat : BeastKind.Wolf;
            if (coast && r < 0.3f) return r < 0.15f ? BeastKind.Turtle : BeastKind.Dragon; // the sea's own: huyền quy and long
            switch (t)
            {
                case Terrain.Swamp:
                case Terrain.Jungle: return r < 0.6f ? BeastKind.Serpent : BeastKind.Tiger;
                case Terrain.Forest:
                    return region == RegionKind.ThienDaoMinh || region == RegionKind.BangNguyen ? (r < 0.6f ? BeastKind.Bear : BeastKind.Wolf)
                        : r < 0.45f ? BeastKind.Tiger : r < 0.75f ? BeastKind.Fox : BeastKind.Bear;
                case Terrain.Desert:
                case Terrain.Badlands: return r < 0.7f ? BeastKind.Scorpion : BeastKind.Serpent;
                case Terrain.Ashland: return r < 0.6f ? BeastKind.Ox : BeastKind.Bat;
                case Terrain.Tundra:
                case Terrain.Snow: return r < 0.6f ? BeastKind.Bear : BeastKind.Wolf;
                case Terrain.Hills:
                case Terrain.Mountain:
                case Terrain.Peak: return r < 0.55f ? BeastKind.Ape : BeastKind.Eagle;
                default:
                    if (region == RegionKind.ThaoNguyen) return r < 0.5f ? BeastKind.Wolf : r < 0.8f ? BeastKind.Fox : BeastKind.Ox;
                    return r < 0.45f ? BeastKind.Wolf : r < 0.75f ? BeastKind.Fox : BeastKind.Ox;
            }
        }

        string KindName(BeastKind kind, ref DetRandom rng)
        {
            string key = kind.ToString();
            var pools = _w.Lore.BeastKinds;
            if (pools != null)
                foreach (var p in pools)
                    if (p.kind == key && p.names != null && p.names.Length > 0) return p.names[rng.Range(0, p.names.Length)];
            return KindNames[(int)kind];
        }

        // The beast in a fight at (x, y): one standing there now, or one that fell there (for the fight scene's look).
        public Beast LookNear(float x, float y)
        {
            Beast best = null;
            float bestD = 9f;
            foreach (var b in All)
            {
                float bx = b.Alive ? _e.X[b.Entity] : b.DeathX, by = b.Alive ? _e.Y[b.Entity] : b.DeathY;
                if (bx < 0f) continue;
                float d = (bx - x) * (bx - x) + (by - y) * (by - y);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        // ---------------------------------------------------------------- hung thú xuất thế

        const int MaxRampaging = 2;
        const int SlumberAfter = 8;   // massacres before a hung thú has had its fill and goes back to sleep
        const int SlumberYears = 15;

        int RampagingCount()
        {
            int n = 0;
            foreach (var b in All)
                if (b.Alive && b.Rampage) n++;
            return n;
        }

        // Once in a few decades: out of a volcano (hỏa kỳ lân), an old battlefield heavy with oán khí, or the deep
        // wilds, a hung thú rises. A beast that has grown to thất giai by itself may also turn to slaughter.
        void Emerge(long tick)
        {
            var rng = RngFor(tick, 3);
            int rampaging = RampagingCount();
            // Grown monsters that lose themselves.
            foreach (var b in All)
            {
                if (rampaging >= MaxRampaging) break;
                if (!b.Alive || b.Rampage || b.IsKing || b.Grade < 7 || tick < b.CalmUntil || rng.NextFloat() >= 0.1f) continue;
                Enrage(b, tick, $"{b.Name} ({b.GradeText}) tu luyện ngàn năm, sát tính bộc phát", ref rng);
                rampaging++;
            }
            if (rampaging >= MaxRampaging || rng.NextFloat() >= 0.02f * _sim.Rules[Rule.Calamities]) return;

            float x = -1f, y = -1f;
            BeastKind kind = BeastKind.Count;
            string origin;
            // A volcano first, then the heaviest battlefield, then the wilds.
            Landmark volcano = null, field = null;
            foreach (var l in _sim.Disasters.Landmarks)
            {
                if (!l.Alive) continue;
                if (l.Kind == Landmark.Volcano && (volcano == null || rng.NextFloat() < 0.5f)) volcano = l;
                if (l.Kind == Landmark.Battlefield && l.Toll >= 15 && (field == null || l.Toll > field.Toll)) field = l;
            }
            float pick = rng.NextFloat();
            if (volcano != null && pick < 0.35f)
            {
                x = volcano.X + 0.5f + rng.Range(-6f, 6f);
                y = volcano.Y + 0.5f + rng.Range(-6f, 6f);
                kind = BeastKind.Qilin;
                origin = $"Lòng núi lửa {volcano.Name} chấn động, một con {KindNames[(int)BeastKind.Qilin].ToLower()} phá đá chui ra";
            }
            else if (field != null && pick < 0.7f)
            {
                x = field.X + 0.5f;
                y = field.Y + 0.5f;
                origin = $"Oán khí của {field.Toll} người chết ở {field.Name} ngưng tụ thành hình";
            }
            else
            {
                for (int k = 0; k < 60 && x < 0f; k++)
                {
                    int cx = rng.Range(24, _w.W - 24), cy = rng.Range(24, _w.H - 24);
                    int i = _w.Idx(cx, cy);
                    if (!TerrainInfo.IsWalkable(_w.Terrain[i]) || _w.Owner[i] != 0 || NearPeople(cx, cy, 60f)) continue;
                    x = cx + 0.5f;
                    y = cy + 0.5f;
                }
                origin = "Từ chốn thâm sơn cùng cốc, một hung thú ngủ say vạn năm thức giấc";
            }
            if (x < 0f || !_w.IsWalkable(x, y)) return;
            // Heaven matches the age: in a world of Kết Đan a lục–thất giai beast; bát and cửu giai only once
            // Nguyên Anh and Hóa Thần walk the earth (two grades to a realm).
            var cr = _sim.Cultivation.CountByRealm;
            int top = cr[(int)Realm.HoaThan] > 0 ? 5 : cr[(int)Realm.NguyenAnh] > 0 ? 4 : 3;
            int grade = Mathf.Clamp(top * 2 - 1 + rng.Range(-1, 2), 6, 9);
            // Hỗn Độn, faceless, older than the world, only ever wakes as one of the greatest.
            if (kind == BeastKind.Count && grade >= 8 && rng.NextFloat() < 0.3f)
            {
                kind = BeastKind.Chaos;
                origin = "Trời đất tối sầm, Hỗn Độn từ thuở khai thiên lập địa thức tỉnh";
            }
            var beast = Spawn(Species.Wolf, grade, x, y, tick, ref rng, kind);
            Enrage(beast, tick, origin, ref rng);
        }

        // A hung thú set loose at (x, y) (Thiên Đạo's hand, or a test).
        public Beast RaiseHungThu(int grade, float x, float y, long tick, string origin)
        {
            var rng = RngFor(tick, 4 + (int)x * 31 + (int)y);
            var b = Spawn(Species.Wolf, grade, x, y, tick, ref rng);
            Enrage(b, tick, origin, ref rng);
            return b;
        }

        void Enrage(Beast b, long tick, string origin, ref DetRandom rng)
        {
            b.Rampage = true;
            b.RampageSince = tick;
            b.Ravaged = 0; // counted per rampage
            b.Prey = -1;
            b.RestUntil = tick + 15;
            if (b.Clan >= 0 && !b.IsKing) b.Clan = -1;
            var titles = _w.Lore.GreatTitles;
            if (titles != null && titles.Length > 0) b.Name = $"{titles[rng.Range(0, titles.Length)]} {KindName(b.Kind, ref rng)}";
            _sim.Events.Add(tick, EventKind.Beast, 3,
                $"Hung thú xuất thế! {origin}: {b.Name} ({b.GradeText}), yêu khí phủ kín trời, thiên hạ đại loạn.",
                _e.X[b.Entity], _e.Y[b.Entity], Fx.DemonBlast);
            _sim.Knowledge?.Spread(RumorKind.HungThu, b.Index, _e.X[b.Entity], _e.Y[b.Entity], 50f, tick, 50); // terror travels fast
        }

        // ---------------------------------------------------------------- tàn sát

        // From town to town: pick the nearest living one (not the last), go there, fall on it, gorge, go on.
        void RampageStep(Beast b, long tick, ref DetRandom rng)
        {
            float x = _e.X[b.Entity], y = _e.Y[b.Entity];
            // Gorged, or worn out: it goes back to sleep in the wilds, a lair beast again, for half a century.
            if (b.Ravaged >= SlumberAfter || tick - b.RampageSince > SlumberYears * (long)SimClock.DaysPerYear)
            {
                b.Rampage = false;
                b.Prey = -1;
                b.CalmUntil = tick + 50L * SimClock.DaysPerYear;
                if (_w.IsWalkable(x, y)) { b.HomeX = x; b.HomeY = y; }
                _sim.Events.Add(tick, EventKind.Beast, 2, $"Sau {b.Ravaged} lần tàn sát, hung thú {b.Name} ({b.GradeText}) no say, quay về chốn hoang sơn ngủ vùi.", x, y);
                return;
            }
            if (tick < b.RestUntil) return;
            var all = _sim.Settlements.All;
            if (b.Prey < 0 || !all[b.Prey].Alive)
            {
                Settlement best = null;
                float bestD = 420f * 420f;
                foreach (var s in all)
                {
                    if (!s.Alive || s.Id == b.LastPrey || s.Id == b.PreyBefore) continue;
                    float dx = s.X - x, dy = s.Y - y, d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = s; }
                }
                if (best == null) return;
                b.Prey = best.Id;
                _e.TX[b.Entity] = best.X + 0.5f;
                _e.TY[b.Entity] = best.Y + 0.5f;
            }
            var prey = all[b.Prey];
            // No road there on foot (across the sea, behind a range): give that town up, try another next month.
            if (!_e.Flying[b.Entity] && _sim.Creatures.Stuck(b.Entity))
            {
                b.PreyBefore = b.LastPrey;
                b.LastPrey = b.Prey;
                b.Prey = -1;
                return;
            }
            float px = prey.X + 0.5f - x, py = prey.Y + 0.5f - y;
            if (px * px + py * py > 25f)
            {
                _e.TX[b.Entity] = prey.X + 0.5f;
                _e.TY[b.Entity] = prey.Y + 0.5f;
                return;
            }
            Massacre(b, prey, tick, ref rng);
        }

        void Massacre(Beast b, Settlement s, long tick, ref DetRandom rng)
        {
            float share = 0.03f * (b.Grade - 4) * rng.Range(0.7f, 1.3f);
            if (s.Walled) share *= 0.4f;          // the gates hold, for a while
            if (_sim.Settlements.HasCivic(s, ObjectType.Shrine)) share *= 0.9f;
            int dead = _sim.Settlements.Kill(s, Mathf.Max(1, (int)(s.Population * share)));
            int houses = 0, wreck = Mathf.Max(0, b.Grade - 5 - (s.Walled ? 1 : 0));
            for (int k = 0; k < wreck && s.Houses.Count > 1; k++)
            {
                _w.Objects.Remove(s.Houses[rng.Range(0, s.Houses.Count)]);
                houses++;
            }
            b.Kills += dead;
            b.Ravaged++;
            _sim.Knowledge?.Spread(RumorKind.HungThu, b.Index, s.X + 0.5f, s.Y + 0.5f, 40f, tick, 50); // the survivors flee, telling it
            b.PreyBefore = b.LastPrey;
            b.LastPrey = s.Id;
            b.Prey = -1;
            b.HomeX = s.X + 0.5f;
            b.HomeY = s.Y + 0.5f;
            b.RestUntil = tick + rng.Range(60, 150);
            // The ground remembers: churned to mud, and burnt where a fire beast passed.
            var scar = b.Kind == BeastKind.Qilin ? ScarKind.Scorch : ScarKind.Trampled;
            _sim.Scars.Disc(s.X, s.Y, 6 + b.Grade, scar, 10 + b.Grade / 2, 3, (uint)tick ^ (uint)b.Index);
            _sim.Events.Add(tick, EventKind.Beast, 3,
                $"{b.Title} ({b.GradeText}) tàn sát {s.Name}: {dead} người chết" + (houses > 0 ? $", {houses} nhà sụp đổ" : "") +
                (s.Walled ? " dù tường thành cố thủ" : "") + ".", s.X + 0.5f, s.Y + 0.5f, Fx.Stampede);
            // Every massacre is a call to arms.
            if (CoalitionAgainst(b) == null) FormCoalition(b, tick, AttemptFor(b), ref rng);
        }

        // ---------------------------------------------------------------- liên minh trảm yêu

        sealed class Coalition
        {
            public int Beast;
            public long Deadline;
            public int Attempt;
            public readonly List<int> Who = new List<int>();
        }

        readonly List<Coalition> _coalitions = new List<Coalition>();
        readonly Dictionary<int, long> _nextCall = new Dictionary<int, long>(); // beast → no new coalition before

        Coalition CoalitionAgainst(Beast b)
        {
            foreach (var c in _coalitions)
                if (c.Beast == b.Index) return c;
            return null;
        }

        public int HuntersOf(Beast b)
        {
            var c = CoalitionAgainst(b);
            return c == null ? 0 : c.Who.Count;
        }

        // The sects within reach send their strongest, up to three each, of a realm that can stand against the beast;
        // a failed hunt calls on stronger people from farther away next time.
        void FormCoalition(Beast b, long tick, int attempt, ref DetRandom rng)
        {
            if (_nextCall.TryGetValue(b.Index, out long next) && tick < next) return;
            float bx = _e.X[b.Entity], by = _e.Y[b.Entity];
            float reach = 320f + 160f * attempt;
            var minRealm = b.Grade >= 9 ? Realm.NguyenAnh : b.Grade >= 7 ? Realm.KetDan : Realm.TrucCo;
            var coalition = new Coalition { Beast = b.Index, Attempt = attempt, Deadline = tick + 480 }; // walkers and swords are slow; time to gather
            var sects = new List<string>();
            float power = 0f;
            foreach (var f in _sim.Factions.All)
            {
                if (!f.Alive || f.Demonic && rng.NextFloat() < 0.6f) continue; // ma môn mostly watch others bleed
                var seat = _sim.Settlements.All[f.Id];
                if (!(_sim.Knowledge?.Knows(RumorKind.HungThu, b.Index, seat.X + 0.5f, seat.Y + 0.5f) ?? true)) continue; // only those who have heard what it did
                float dx = seat.X - bx, dy = seat.Y - by;
                if (dx * dx + dy * dy > reach * reach) continue;
                var picked = new List<Cultivator>();
                foreach (var c in _sim.Cultivation.All)
                {
                    if (!c.Alive || c.SectId != f.Id || c.Watched || c.AtWar || c.Realm < minRealm || !_sim.Cultivation.IsAtHome(c) || CombatSystem.Wounded(c, 0.6f)) continue;
                    picked.Add(c);
                }
                if (picked.Count == 0) continue;
                picked.Sort((p, q) => q.Rank.CompareTo(p.Rank));
                int n = Mathf.Min(3, picked.Count);
                for (int k = 0; k < n; k++)
                {
                    coalition.Who.Add(picked[k].Index);
                    power += CombatSystem.Strength(picked[k]);
                }
                sects.Add(_sim.Settlements.All[f.Id].BaseName);
            }
            _nextCall[b.Index] = tick + 2L * SimClock.DaysPerYear;
            if (coalition.Who.Count == 0 || power < Strength(b) * 0.8f) // too weak to dare
            {
                if (attempt == 0)
                    _sim.Events.Add(tick, EventKind.Beast, 2, $"Trước {b.Title} ({b.GradeText}), các tông môn quanh đó khiếp sợ, đóng chặt sơn môn không dám ra tay.",
                        bx, by);
                return;
            }
            foreach (int i in coalition.Who)
                _sim.Cultivation.SendToBattle(_sim.Cultivation.All[i], bx + rng.Range(-3f, 3f), by + rng.Range(-3f, 3f), coalition.Deadline);
            _coalitions.Add(coalition);
            _sim.Events.Add(tick, EventKind.Alliance, 3,
                $"Liên minh trảm yêu: {string.Join(", ", sects)} cùng cử {coalition.Who.Count} cao thủ truy sát {b.Title} ({b.GradeText}).", bx, by, Fx.LightPillar);
        }

        // Each month the hunters follow the beast; once most of them have caught up (or time runs out) they fight.
        void CoalitionStep(long tick)
        {
            for (int k = _coalitions.Count - 1; k >= 0; k--)
            {
                var co = _coalitions[k];
                var b = All[co.Beast];
                var all = _sim.Cultivation.All;
                co.Who.RemoveAll(i => !all[i].Alive || !all[i].AtWar);
                if (!b.Alive || co.Who.Count == 0)
                {
                    foreach (int i in co.Who) _sim.Cultivation.ReturnHome(all[i]);
                    _coalitions.RemoveAt(k);
                    continue;
                }
                float bx = _e.X[b.Entity], by = _e.Y[b.Entity];
                int near = 0;
                foreach (int i in co.Who)
                {
                    var c = all[i];
                    float dx = _e.X[c.Entity] - bx, dy = _e.Y[c.Entity] - by;
                    if (dx * dx + dy * dy <= 36f) near++;
                    else if (c.Travelling) _sim.Cultivation.Retarget(c, bx, by);
                    else _sim.Cultivation.SendToBattle(c, bx, by, co.Deadline); // the trail moved on
                }
                if (near * 2 < co.Who.Count && tick < co.Deadline) continue;
                _coalitions.RemoveAt(k);
                var rng = RngFor(tick, 900000 + b.Index);
                Hunt(b, co, tick, ref rng);
            }
        }

        // The great fight: the beast's hide against everyone's blows, round after round; each round it tears into
        // one of them. If it falls, the one who struck last takes its yêu đan, and the sects who fought side by side
        // remember it. If they break, it grows wilder, and the next coalition is called from farther away.
        void Hunt(Beast b, Coalition co, long tick, ref DetRandom rng)
        {
            var all = _sim.Cultivation.All;
            var fighters = new List<Cultivator>();
            foreach (int i in co.Who)
                if (all[i].Alive) fighters.Add(all[i]);
            float bx = _e.X[b.Entity], by = _e.Y[b.Entity];
            // Its blood against everyone's blows: a beast as strong as the whole band takes about three rounds to fell.
            float hide = HpOf(b), full = MaxHp(b), bs = Strength(b);
            int fallen = 0, fled = 0, round = 0, start = fighters.Count;
            Cultivator lastBlow = null;
            while (hide > 0f && fighters.Count > 0 && round < 12)
            {
                round++;
                foreach (var c in fighters)
                {
                    hide -= full * CombatSystem.Strength(c) / (3f * Mathf.Max(0.01f, bs)) * rng.Range(0.6f, 1.4f);
                    if (hide <= 0f) { lastBlow = c; break; }
                }
                if (hide <= 0f) break;
                var target = fighters[rng.Range(0, fighters.Count)];
                float odds = Mathf.Clamp(0.5f * Strength(b) / Mathf.Max(1f, CombatSystem.Strength(target)), 0.08f, 0.85f);
                if (rng.NextFloat() < odds)
                {
                    b.Kills++;
                    fallen++;
                    fighters.Remove(target);
                    _sim.Cultivation.Perish(target, tick, $"{target.Title} ({_sim.Cultivation.SectName(target)}) vây đánh {b.Title}, bị nó xé nát.",
                        target.Realm >= Realm.KetDan ? 2 : 1, Fx.BeastKill);
                }
                else if (Wound(target, rng.Range(0.3f, 0.6f)) || rng.NextFloat() < 0.15f) // torn open: too hurt to go on, or simply afraid
                {
                    fled++;
                    fighters.Remove(target);
                    _sim.Cultivation.ReturnHome(target);
                }
                // A third of them dead: the rest break and run.
                if (fallen * 3 >= start && fighters.Count > 0)
                {
                    foreach (var c in fighters) _sim.Cultivation.ReturnHome(c);
                    fled += fighters.Count;
                    fighters.Clear();
                }
            }
            // Where they fought, the land keeps the blood and the fear.
            _sim.Scars.Disc((int)bx, (int)by, 7, ScarKind.Battlefield, 12, 4, (uint)tick ^ 0xB3A57u);
            if (fallen > 0) _sim.Disasters.MarkBattlefield((int)bx, (int)by, 7, fallen, tick, $"trận trảm yêu {b.Name} năm {tick / SimClock.DaysPerYear + 1}");
            b.Hp = Mathf.Max(1f, hide); // what they took from it stays taken

            if (hide <= 0f && lastBlow != null)
            {
                // Yêu đan of a hung thú: a pháp bảo, a fortune, and a step on the road.
                lastBlow.Kills++;
                lastBlow.Treasures++;
                lastBlow.TreasureName = $"Yêu đan {b.Name}";
                lastBlow.Stones += 60f * b.Grade * b.Grade;
                lastBlow.Pills += 2;
                lastBlow.Progress += Realms.Need(lastBlow.Realm, lastBlow.Stage) * 0.6f;
                lastBlow.Fame += 6f;
                foreach (var c in fighters)
                {
                    if (c == lastBlow) continue;
                    c.Stones += 15f * b.Grade * b.Grade;
                    c.Progress += Realms.Need(c.Realm, c.Stage) * 0.2f;
                    c.Fame += 2f;
                }
                // Comrades in arms: the sects that fought together think better of each other.
                for (int i = 0; i < fighters.Count; i++)
                for (int j = i + 1; j < fighters.Count; j++)
                    if (fighters[i].SectId >= 0 && fighters[j].SectId >= 0 && fighters[i].SectId != fighters[j].SectId)
                        _sim.Factions.Grievance(fighters[i].SectId, fighters[j].SectId, -12f);
                Die(b, tick, $"Liên minh trảm yêu đại thắng: sau {round} hiệp, {lastBlow.Title} ({_sim.Cultivation.SectName(lastBlow)}) " +
                             $"tung đòn kết liễu {b.Title} ({b.GradeText}), đoạt yêu đan" + (fallen > 0 ? $"; {fallen} tu sĩ đã vẫn lạc." : "."), 3, lastBlow);
                foreach (var c in fighters)
                    if (c.Alive) _sim.Cultivation.ReturnHome(c);
                return;
            }
            foreach (var c in fighters)
                if (c.Alive) _sim.Cultivation.ReturnHome(c);
            b.Progress += 200f * b.Grade; // the taste of their blood
            _sim.Events.Add(tick, EventKind.Beast, 3,
                $"Liên minh trảm yêu thất bại: {fallen} tu sĩ vẫn lạc, {fled} người bỏ chạy, {b.Title} càng thêm hung hãn.", bx, by, Fx.Stampede);
            _nextCall[b.Index] = tick + SimClock.DaysPerYear;
            FormCoalitionLater(b, co.Attempt + 1);
        }

        // A blow that did not kill: true if it left them too torn up to keep fighting.
        static bool Wound(Cultivator c, float share)
        {
            CombatSystem.Hurt(c, share);
            return CombatSystem.Wounded(c, 0.3f);
        }

        // The next call comes after a year, from farther away (CoalitionStep re-forms it at the next massacre).
        void FormCoalitionLater(Beast b, int attempt) => _attempts[b.Index] = attempt;

        readonly Dictionary<int, int> _attempts = new Dictionary<int, int>();

        int AttemptFor(Beast b) => _attempts.TryGetValue(b.Index, out int a) ? a : 0;
    }
}
