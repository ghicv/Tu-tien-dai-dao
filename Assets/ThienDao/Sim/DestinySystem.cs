using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    public enum DestinyKind : byte { AnswerPrayers, SlayHungThu, Ascend, ProtectSect, Population, FaithfulTowns }

    public sealed class Destiny
    {
        public DestinyKind Kind;
        public int Target = -1;   // a beast, a cultivator or a sect's settlement id, by kind
        public int Goal, Base;
        public long Start, Until;
        public int Reward;        // thiên uy
        public string Text;
        public bool Done, Failed;
        public long EndTick;
    }

    // Thiên mệnh (devlog 27): soft goals the world itself suggests, read off what is happening in it now: a
    // hung thú to be put down, a promising cultivator who could rise, a sect at war that may not last, villages
    // praying. There are always three on offer; the player may chase any, all or none, by any means they like.
    // Fulfilled ones are written into history and add to Thiên uy; missed ones are written down too.
    public sealed class DestinySystem
    {
        public const int Slots = 3;
        const int PastKept = 6;

        readonly Simulation _sim;
        public readonly List<Destiny> Active = new List<Destiny>();
        public readonly List<Destiny> Past = new List<Destiny>(); // newest first
        public int Completed, Failed, Merit;
        int _answered;             // prayers answered since the world began (FaithSystem tells it)

        public DestinySystem(Simulation sim)
        {
            _sim = sim;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_sim.World.Seed ^ 0xDE57u, (int)tick, salt));

        public void OnAnswered() => _answered++;

        // Progress toward the goal, for the panel ("2/3").
        public int Progress(Destiny d, long tick)
        {
            switch (d.Kind)
            {
                case DestinyKind.AnswerPrayers: return _answered - d.Base;
                case DestinyKind.SlayHungThu: return BeastOf(d) is Beast b ? b.Ravaged - d.Base : 0;
                case DestinyKind.Ascend: return CultivatorOf(d) is Cultivator c ? (int)c.Realm : 0;
                case DestinyKind.ProtectSect: return (int)((tick - d.Start) / SimClock.DaysPerYear);
                case DestinyKind.Population: return _sim.Settlements.TotalPopulation;
                default: return _sim.Faith.FaithfulCount(FaithSystem.ShrineFaith);
            }
        }

        public Beast BeastOf(Destiny d) => d.Kind == DestinyKind.SlayHungThu && d.Target >= 0 && d.Target < _sim.Beasts.All.Count ? _sim.Beasts.All[d.Target] : null;

        public Cultivator CultivatorOf(Destiny d) =>
            d.Kind == DestinyKind.Ascend && d.Target >= 0 && d.Target < _sim.Cultivation.All.Count ? _sim.Cultivation.All[d.Target] : null;

        public Settlement SectOf(Destiny d) =>
            d.Kind == DestinyKind.ProtectSect && d.Target >= 0 && d.Target < _sim.Settlements.All.Count ? _sim.Settlements.All[d.Target] : null;

        // Where on the map the destiny is about, if anywhere (the panel's rows fly there).
        public bool Where(Destiny d, out Vector2 at)
        {
            var e = _sim.Entities;
            at = default;
            if (BeastOf(d) is Beast b && b.Alive) { at = new Vector2(e.X[b.Entity], e.Y[b.Entity]); return true; }
            if (CultivatorOf(d) is Cultivator c && c.Alive)
            {
                at = _sim.Cultivation.IsShownOnMap(c) ? new Vector2(e.X[c.Entity], e.Y[c.Entity]) : new Vector2(c.HomeX, c.HomeY);
                return true;
            }
            if (SectOf(d) is Settlement s && s.Alive) { at = new Vector2(s.X + 0.5f, s.Y + 0.5f); return true; }
            return false;
        }

        // ---------------------------------------------------------------- monthly: judged; yearly: renewed

        public void MonthlyStep(long tick)
        {
            for (int k = Active.Count - 1; k >= 0; k--)
            {
                var d = Active[k];
                Judge(d, tick, out bool done, out bool failed);
                if (!done && !failed) continue;
                Active.RemoveAt(k);
                d.Done = done;
                d.Failed = failed;
                d.EndTick = tick;
                Past.Insert(0, d);
                if (Past.Count > PastKept) Past.RemoveAt(Past.Count - 1);
                Where(d, out var at);
                bool placed = at != default;
                if (done)
                {
                    Completed++;
                    Merit += d.Reward;
                    _sim.Events.Add(tick, EventKind.Destiny, 3, $"Thiên mệnh hoàn thành: {d.Text}. Thiên uy +{d.Reward}.",
                        placed ? at.x : -1f, placed ? at.y : -1f, placed ? Fx.LightPillar : Fx.None);
                }
                else
                {
                    Failed++;
                    _sim.Events.Add(tick, EventKind.Destiny, 2, $"Thiên mệnh không thành: {d.Text}.", placed ? at.x : -1f, placed ? at.y : -1f);
                }
            }
            if (Active.Count < Slots && (tick < SimClock.DaysPerMonth * 2 || tick % SimClock.DaysPerYear < SimClock.DaysPerMonth)) TopUp(tick);
        }

        void Judge(Destiny d, long tick, out bool done, out bool failed)
        {
            done = failed = false;
            int p = Progress(d, tick);
            switch (d.Kind)
            {
                case DestinyKind.SlayHungThu:
                    var b = BeastOf(d);
                    done = b == null || !b.Alive;
                    failed = !done && p >= d.Goal; // it ravaged that many more towns first
                    break;
                case DestinyKind.Ascend:
                    var c = CultivatorOf(d);
                    done = c != null && c.Alive && (int)c.Realm >= d.Goal;
                    failed = !done && (c == null || !c.Alive);
                    break;
                case DestinyKind.ProtectSect:
                    var s = SectOf(d);
                    failed = s == null || !s.Alive;
                    done = !failed && tick >= d.Until;
                    break;
                default:
                    done = p >= d.Goal;
                    break;
            }
            if (!done && !failed && tick >= d.Until) failed = true;
        }

        bool Has(DestinyKind kind)
        {
            foreach (var d in Active)
                if (d.Kind == kind) return true;
            return false;
        }

        // Fill the empty slots from what the world needs now; the urgent first.
        void TopUp(long tick)
        {
            var rng = RngFor(tick, Completed * 31 + Failed);
            for (int guard = 0; guard < 12 && Active.Count < Slots; guard++)
            {
                var d = guard == 0 ? HungThu(tick) : null;
                if (d == null)
                    switch (rng.Range(0, 5))
                    {
                        case 0: d = Prayers(tick); break;
                        case 1: d = Ascend(tick); break;
                        case 2: d = ProtectSect(tick, ref rng); break;
                        case 3: d = Population(tick); break;
                        default: d = Faithful(tick); break;
                    }
                if (d == null || Has(d.Kind)) continue;
                d.Start = tick;
                Active.Add(d);
                Where(d, out var at);
                bool placed = at != default;
                _sim.Events.Add(tick, EventKind.Destiny, 2, $"Thiên mệnh mới: {d.Text}.", placed ? at.x : -1f, placed ? at.y : -1f);
            }
        }

        Destiny HungThu(long tick)
        {
            foreach (var b in _sim.Beasts.All)
                if (b.Alive && b.Rampage)
                    return new Destiny
                    {
                        Kind = DestinyKind.SlayHungThu, Target = b.Index, Base = b.Ravaged, Goal = 3, Reward = 3,
                        Until = tick + 30L * SimClock.DaysPerYear, Text = $"Trừ khử {b.Title} ({b.GradeText}) trước khi nó tàn sát thêm 3 thành"
                    };
            return null;
        }

        Destiny Prayers(long tick) => new Destiny
        {
            Kind = DestinyKind.AnswerPrayers, Base = _answered, Goal = 3, Reward = 2, Until = tick + 40L * SimClock.DaysPerYear,
            Text = "Đáp lời 3 lời cầu nguyện của chúng sinh"
        };

        // The most gifted Trúc Cơ or Kết Đan alive, to be carried one realm higher.
        Destiny Ascend(long tick)
        {
            Cultivator best = null;
            float bestScore = -1f;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Realm < Realm.TrucCo || c.Realm > Realm.NguyenAnh) continue;
                float score = (int)c.Realm * 2f + c.Comprehension + c.Luck + c.DaoHeart;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best == null) return null;
            var next = best.Realm + 1;
            return new Destiny
            {
                Kind = DestinyKind.Ascend, Target = best.Index, Goal = (int)next, Reward = next >= Realm.HoaThan ? 6 : next == Realm.NguyenAnh ? 4 : 3,
                Until = tick + 150L * SimClock.DaysPerYear, Text = $"Đưa {best.Title} ({_sim.Cultivation.SectName(best)}) lên {Realms.Names[(int)next]}"
            };
        }

        // The weakest sect at war, to be kept standing for fifty years.
        Destiny ProtectSect(long tick, ref DetRandom rng)
        {
            var fs = _sim.Factions;
            Faction weakest = null;
            foreach (var r in fs.Relations)
            {
                if (r.Stance != Stance.War) continue;
                foreach (int id in new[] { r.A, r.B })
                {
                    var f = fs.Get(id);
                    if (f != null && f.Alive && (weakest == null || f.Power < weakest.Power)) weakest = f;
                }
            }
            if (weakest == null) return null;
            return new Destiny
            {
                Kind = DestinyKind.ProtectSect, Target = weakest.Id, Goal = 50, Reward = 3, Until = tick + 50L * SimClock.DaysPerYear,
                Text = $"Giữ {fs.NameOf(weakest.Id)} trụ vững 50 năm giữa chiến loạn"
            };
        }

        Destiny Population(long tick)
        {
            int goal = Mathf.CeilToInt(_sim.Settlements.TotalPopulation * 1.25f / 1000f) * 1000;
            return new Destiny
            {
                Kind = DestinyKind.Population, Goal = goal, Reward = 2, Until = tick + 100L * SimClock.DaysPerYear,
                Text = $"Phàm nhân thiên hạ đạt {goal:N0} người"
            };
        }

        Destiny Faithful(long tick)
        {
            int goal = Mathf.Max(3, _sim.Faith.FaithfulCount(FaithSystem.ShrineFaith) + 3);
            return new Destiny
            {
                Kind = DestinyKind.FaithfulTowns, Goal = goal, Reward = 2, Until = tick + 60L * SimClock.DaysPerYear,
                Text = $"{goal} nơi thành tâm thờ phụng Thiên Đạo (tín ngưỡng từ {FaithSystem.ShrineFaith:0})"
            };
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, Completed | ((long)Failed << 16) | ((long)Merit << 32) | ((long)_answered << 48));
            foreach (var d in Active) StateHash.Add(ref h, (int)d.Kind | ((long)d.Target << 8) | ((long)d.Goal << 32));
        }
    }
}
