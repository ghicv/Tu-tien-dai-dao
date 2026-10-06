using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public enum RelicKind : byte { Cave, Ruins, Treasure } // động phủ of the dead, di tích of a fallen sect, thiên địa linh vật

    public sealed class Relic
    {
        public int Index;
        public RelicKind Kind;
        public string Name;          // "Động phủ của Hàn Lập"
        public int X, Y;
        public int Tier;             // 1..5, the realm it was made at: danger and reward
        public int Owner = -1;       // the cultivator who left it
        public int Sect = -1;        // the sect it came from
        public string Origin;        // "Kết Đan, vẫn lạc năm 312"
        public string Treasure;      // a named pháp bảo inside, if any
        public float Stones;
        public int Pills;
        public long Tick;
        public bool Discovered;
        public int DiscoveredBy = -1;
        public int Layers = 1;       // explorations left before it is empty
        public bool Open => Layers > 0;
    }

    // Bí cảnh (GDD §11): the past becomes the present. Strong cultivators who die leave their caves (and whatever
    // treasure was not taken from them); fallen sects leave ruins; the land itself hides wonders under its volcanoes
    // and lôi địa. These lie hidden until someone passing by finds them; then the bold go in, for the dead's
    // pháp bảo and truyền thừa, or never come out.
    public sealed class RelicSystem
    {
        const float FindReach = 25f;
        const int MaxOpen = 120;

        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly List<Relic> All = new List<Relic>();

        public RelicSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0x2E11Cu, (int)tick, salt));

        static int Year(long tick) => (int)(tick / SimClock.DaysPerYear) + 1;

        int OpenCount()
        {
            int n = 0;
            foreach (var r in All)
                if (r.Open) n++;
            return n;
        }

        Relic Add(RelicKind kind, string name, float x, float y, int tier, long tick)
        {
            if (OpenCount() >= MaxOpen || !_w.InBounds((int)x, (int)y)) return null;
            var r = new Relic { Index = All.Count, Kind = kind, Name = name, X = (int)x, Y = (int)y, Tier = Mathf.Clamp(tier, 1, 5), Tick = tick };
            All.Add(r);
            return r;
        }

        // ---------------------------------------------------------------- how relics come to be

        // A cultivator of Kết Đan and above died: their cave keeps what their killer did not take.
        public void OnDeath(Cultivator c, long tick)
        {
            if (c.Realm < Realm.KetDan) return;
            var rng = RngFor(tick, c.Index);
            float chance = c.Realm >= Realm.HoaThan ? 1f : c.Realm == Realm.NguyenAnh ? 0.7f : 0.35f;
            if (rng.NextFloat() >= chance) return;
            var r = Add(RelicKind.Cave, $"Động phủ của {c.Name}", c.HomeX, c.HomeY, (int)c.Realm, tick);
            if (r == null) return;
            r.Owner = c.Index;
            r.Sect = c.SectId;
            r.Origin = $"{Realms.Names[(int)c.Realm]}, vẫn lạc năm {Year(tick)}";
            r.Treasure = c.Treasures > 0 ? c.TreasureName : null;
            r.Stones = c.Stones * 0.8f + 50f * (int)c.Realm;
            r.Pills = c.Pills;
            r.Layers = (int)c.Realm >= (int)Realm.NguyenAnh ? 2 : 1;
        }

        // A sect was wiped out: its scripture hall lies in ruins on the old mountain gate.
        public void OnSectDestroyed(Settlement s, string name, long tick)
        {
            var rng = RngFor(tick, 100000 + s.Id);
            if (rng.NextFloat() >= 0.6f) return;
            var r = Add(RelicKind.Ruins, $"Di tích {name}", s.X + rng.Range(-6, 7), s.Y + rng.Range(-6, 7), 3, tick);
            if (r == null) return;
            r.Sect = s.Id;
            r.Origin = $"tông môn bị diệt năm {Year(tick)}";
            r.Treasure = rng.NextFloat() < 0.5f ? Lore.Treasures[rng.Range(0, Lore.Treasures.Length)] : null;
            r.Stones = rng.Range(200f, 800f);
            r.Pills = rng.Range(0, 3);
            r.Layers = 2;
        }

        // The land's own wonders, deep under a new volcano or a lôi địa.
        public void OnLandmark(Landmark l, long tick)
        {
            var rng = RngFor(tick, 200000 + l.X * 31 + l.Y);
            if (rng.NextFloat() >= 0.5f) return;
            string what = Lore.NaturalTreasures[rng.Range(0, Lore.NaturalTreasures.Length)];
            var r = Add(RelicKind.Treasure, $"{what} ở {l.Name}", l.X + rng.Range(-l.R, l.R + 1), l.Y + rng.Range(-l.R, l.R + 1), 2 + rng.Range(0, 3), tick);
            if (r == null) return;
            r.Origin = l.Origin;
            r.Treasure = what;
            r.Stones = rng.Range(100f, 400f);
        }

        // ---------------------------------------------------------------- finding and exploring

        public void MonthlyStep(long tick)
        {
            var e = _sim.Entities;
            foreach (var r in All)
            {
                if (r.Discovered || !r.Open) continue;
                int id = _sim.Creatures.FindNearest(r.X + 0.5f, r.Y + 0.5f, FindReach, 1 << (int)Species.Cultivator);
                var c = _sim.Cultivation.ForEntity(id);
                if (c == null || !_sim.Cultivation.IsShownOnMap(c) || c.AtWar) continue;
                var rng = RngFor(tick, 300000 + r.Index);
                // Fortune and insight find what the eyes pass over; the older and grander, the better hidden.
                float chance = 0.12f * (0.5f + c.Luck) * (0.7f + 0.6f * c.Comprehension) / r.Tier;
                if (rng.NextFloat() >= chance) continue;
                r.Discovered = true;
                r.DiscoveredBy = c.Index;
                _sim.Events.Add(tick, EventKind.Relic, 2, $"{c.Title} ({_sim.Cultivation.SectName(c)}) tình cờ phát hiện {r.Name} ({r.Origin}).",
                    r.X + 0.5f, r.Y + 0.5f, Fx.Blessing, c.Index, r.Owner, c.SectId, r.Sect);
                // They try it at once if they dare; otherwise word spreads and stronger people will come.
                if (DeathChance(r, c) < 0.35f) Explore(c, r, tick);
            }
        }

        public void YearlyStep(long tick)
        {
            // Word of a known bí cảnh draws the strong from far around.
            foreach (var r in All)
            {
                if (!r.Discovered || !r.Open) continue;
                var rng = RngFor(tick, 400000 + r.Index);
                if (rng.NextFloat() >= 0.3f) continue;
                Cultivator best = null;
                foreach (var c in _sim.Cultivation.All)
                {
                    if (!c.Alive || c.AtWar || c.HuntTarget >= 0 || !_sim.Cultivation.IsAtHome(c) || (int)c.Realm < r.Tier - 1) continue;
                    float dx = c.HomeX - r.X, dy = c.HomeY - r.Y;
                    if (dx * dx + dy * dy > 220f * 220f) continue;
                    if (best == null || c.Luck + c.Ambition > best.Luck + best.Ambition) best = c;
                }
                if (best == null) continue;
                _sim.Events.Add(tick, EventKind.Relic, 1, $"{best.Title} nghe tin {r.Name} xuất thế, lên đường thám hiểm.",
                    r.X + 0.5f, r.Y + 0.5f, Fx.None, best.Index, r.Owner, best.SectId, r.Sect);
                Explore(best, r, tick);
            }
        }

        public static float DeathChance(Relic r, Cultivator c) => Mathf.Clamp(0.08f + 0.18f * (r.Tier - (int)c.Realm), 0.02f, 0.85f);

        // The nearest known, unplundered bí cảnh within reach (nhân vật chính seek them out).
        public Relic NearestKnown(float x, float y, float reach)
        {
            Relic best = null;
            float bestD = reach * reach;
            foreach (var r in All)
            {
                if (!r.Discovered || !r.Open) continue;
                float dx = r.X - x, dy = r.Y - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        public Relic At(int x, int y, float reach)
        {
            foreach (var r in All)
                if (r.Open && (r.X - x) * (r.X - x) + (r.Y - y) * (r.Y - y) <= reach * reach) return r;
            return null;
        }

        // Going in: the guardians, traps and restrictions of the dead may kill; if not, the treasure, the stones,
        // and above all the truyền thừa (their understanding of the Dao) pass to the living.
        public void Explore(Cultivator c, Relic r, long tick)
        {
            if (!c.Alive || !r.Open) return;
            var rng = RngFor(tick, 500000 + r.Index * 7 + c.Index);
            string who = $"{c.Title} ({_sim.Cultivation.SectName(c)})";
            float px = r.X + 0.5f, py = r.Y + 0.5f;
            r.Discovered = true;
            if (rng.NextFloat() < DeathChance(r, c))
            {
                _sim.Cultivation.Perish(c, tick, $"{who} vào {r.Name}, vẫn lạc giữa cấm chế của người xưa.", r.Tier >= 3 ? 2 : 1, Fx.DemonBlast);
                return;
            }
            r.Layers--;
            var gains = new List<string>();
            if (r.Treasure != null && r.Kind != RelicKind.Treasure)
            {
                c.Treasures++;
                c.TreasureName = r.Treasure;
                gains.Add($"pháp bảo {r.Treasure}");
                r.Treasure = null;
            }
            float stones = r.Stones * (r.Layers > 0 ? 0.5f : 1f);
            if (stones > 0f)
            {
                c.Stones += stones;
                r.Stones -= stones;
                gains.Add($"{stones:0} linh thạch");
            }
            if (r.Pills > 0)
            {
                c.Pills += r.Pills;
                gains.Add($"{r.Pills} viên đan");
                r.Pills = 0;
            }
            // Truyền thừa: the Dao of the one who lived here; a natural wonder feeds the body instead.
            float insight = r.Kind == RelicKind.Treasure ? 0.6f : 0.4f * r.Tier;
            c.Progress += Realms.Need(c.Realm, c.Stage) * insight;
            if (r.Kind == RelicKind.Treasure)
            {
                gains.Add($"luyện hóa {r.Treasure}");
                c.BonusYears += 10 * r.Tier;
            }
            else
            {
                c.Comprehension = Mathf.Min(1f, c.Comprehension + 0.05f * r.Tier);
                gains.Add("truyền thừa của người xưa");
            }
            var owner = r.Owner >= 0 ? _sim.Cultivation.All[r.Owner] : null;
            bool heir = owner != null && IsHeir(c, owner);
            _sim.Events.Add(tick, EventKind.Relic, r.Tier >= 3 || r.Treasure != null ? 3 : 2,
                $"{who} {(heir ? "trở về" : "thám hiểm")} {r.Name}{(owner != null ? $" ({r.Origin})" : "")}, thu được {string.Join(", ", gains)}." +
                (r.Open ? "" : " Bí cảnh từ đây trống rỗng."), px, py, Fx.Blessing, c.Index, r.Owner, c.SectId, r.Sect);
            _sim.Stories?.OnRelic(c, r, owner, heir, tick);
        }

        // Lineage: the dead was their master, their master's master, or of their sect.
        bool IsHeir(Cultivator c, Cultivator owner)
        {
            var all = _sim.Cultivation.All;
            int m = c.MasterIdx;
            for (int depth = 0; m >= 0 && depth < 6; depth++, m = all[m].MasterIdx)
                if (m == owner.Index) return true;
            return false;
        }

        public void HashInto(ref ulong h)
        {
            foreach (var r in All)
                StateHash.Add(ref h, r.X | ((long)r.Y << 12) | ((long)r.Layers << 24) | (r.Discovered ? 1L << 30 : 0) | ((long)r.Tier << 32) ^ ((long)System.BitConverter.SingleToInt32Bits(r.Stones) << 36));
        }
    }
}
