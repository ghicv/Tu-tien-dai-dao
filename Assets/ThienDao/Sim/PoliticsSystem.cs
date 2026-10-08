using System.Collections.Generic;
using System.Threading.Tasks;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public sealed class Rebellion
    {
        public int Kingdom;
        public int Seat;              // the town it rose in (settlement id)
        public string Leader;         // a mortal of the people, or of a gia tộc seated there
        public int LeaderClan = -1;
        public bool Usurper;          // a war for the throne after a king died with no heir, not a rising of the people
        public readonly List<int> Towns = new List<int>();
        public long Start, Until;
        public int Dead;
    }

    // Chính trị nước phàm nhân (devlog 30). Every kingdom has a king of a ruling house, benevolent or cruel, who
    // ages and dies. An heir of age takes the throne quietly; with none, the throne is fought over. Each town's
    // unrest follows what it lives through: hunger, death, drought and plague, a cruel king, a capital far away;
    // a sect's protection, a miếu, the capital's own walls calm it. A town pushed too far rises, the restless towns
    // around join it, and the kingdom is at civil war for some years. Then the crown puts it down, or the rebels
    // take the capital and found a new dynasty, or they hold their own land and found a kingdom of their own; and
    // the sects that stood behind each side carry the grudge. Mortals do not fight cultivators; but where a tu tiên
    // gia tộc is seated, the rising may be its, and the throne with it.
    public sealed class PoliticsSystem
    {
        const int MaxRebellions = 4;

        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly List<Rebellion> Rebellions = new List<Rebellion>();
        [System.NonSerialized] readonly List<Settlement> _towns = new List<Settlement>();

        public PoliticsSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            var rng = new DetRandom(_w.Seed ^ 0x9011Bu);
            foreach (var k in _w.Kingdoms)
            {
                k.Dynasty = _w.Lore.Surnames[rng.Range(0, _w.Lore.Surnames.Length)];
                k.Ruler = RulerName(k.Dynasty, ref rng);
                k.RulerBorn = -(long)(rng.Range(25f, 55f) * SimClock.DaysPerYear);
                k.Benevolence = rng.Range(0.2f, 0.9f);
                k.DynastySince = -(long)(rng.Range(40f, 300f) * SimClock.DaysPerYear);
            }
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0x9011Cu, (int)tick, salt));

        string RulerName(string house, ref DetRandom rng) => $"{house} {_w.Lore.GivenNames[rng.Range(0, _w.Lore.GivenNames.Length)]}";

        public static string Temper(Kingdom k) => k.Benevolence >= 0.65f ? "nhân từ" : k.Benevolence <= 0.35f ? "bạo ngược" : "bình thường";
        public static int RulerAge(Kingdom k, long tick) => (int)((tick - k.RulerBorn) / SimClock.DaysPerYear);
        public static int DynastyYears(Kingdom k, long tick) => (int)((tick - k.DynastySince) / SimClock.DaysPerYear);

        public Rebellion RebellionOf(int kingdom)
        {
            foreach (var r in Rebellions)
                if (r.Kingdom == kingdom) return r;
            return null;
        }

        public Rebellion RebellionAt(int settlement)
        {
            foreach (var r in Rebellions)
                if (r.Towns.Contains(settlement)) return r;
            return null;
        }

        void TownsOf(Kingdom k)
        {
            _towns.Clear();
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && !s.Sect && s.Kingdom == k.Id) _towns.Add(s);
        }

        Settlement CapitalOf(Kingdom k)
        {
            foreach (var s in _towns)
                if (s.Capital) return s;
            return null;
        }

        // ---------------------------------------------------------------- yearly

        public void YearlyStep(long tick)
        {
            foreach (var k in _w.Kingdoms)
            {
                if (k.Fallen) continue;
                if (k.Ruler == null) Crown(k, null, -1, tick); // a kingdom raised anew
                TownsOf(k);
                if (_towns.Count == 0) continue;
                var capital = CapitalOf(k);
                Unrest(k, capital, tick);
                Reign(k, capital, tick);
                if (RebellionOf(k.Id) == null && Rebellions.Count < MaxRebellions) Rise(k, capital, tick);
            }
            for (int i = Rebellions.Count - 1; i >= 0; i--) WarYear(Rebellions[i], i, tick);
            // A kingdom that broke away may be taken back: its parent strong and calm, itself weak and restless.
            for (int i = 0; i < _w.Kingdoms.Count; i++)
            {
                var k = _w.Kingdoms[i];
                if (k.Fallen || k.Parent < 0 || k.Parent >= _w.Kingdoms.Count) continue;
                var p = _w.Kingdoms[k.Parent];
                if (p.Fallen || p.Stability < 60f || k.Stability > 45f || RebellionOf(k.Id) != null || RebellionOf(p.Id) != null) continue;
                if (RngFor(tick, 600 + k.Id).NextFloat() >= 0.05f) continue;
                Reunite(p, k, tick);
            }
        }

        void Reunite(Kingdom p, Kingdom k, long tick)
        {
            ushort from = (ushort)(k.Id + 1), to = (ushort)(p.Id + 1);
            int n = _w.W, y0 = n, y1 = 0;
            for (int i = 0; i < _w.KingdomOf.Length; i++)
                if (_w.KingdomOf[i] == from)
                {
                    _w.KingdomOf[i] = to;
                    int y = i / n;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            p.LandCells += k.LandCells;
            k.LandCells = 0;
            k.Fallen = true;
            Settlement seat = null;
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && !s.Sect && s.Kingdom == k.Id)
                {
                    if (s.Capital) seat = s;
                    s.Kingdom = p.Id;
                    s.Capital = false;
                    s.Unrest = Mathf.Max(0f, s.Unrest - 15f);
                }
            if (k.RoyalClan >= 0 && k.RoyalClan < _sim.Clans.All.Count) _sim.Clans.All[k.RoyalClan].Royal = -1;
            if (y1 >= y0) _w.NotifyLookChanged(0, Mathf.Max(0, y0 - 1), n - 1, Mathf.Min(n - 1, y1 + 1));
            _sim.Events.Add(tick, EventKind.Founding, 3, $"{p.Name} thu phục {k.Name} sau {DynastyYears(k, tick)} năm cát cứ, giang sơn về một mối.",
                seat != null ? seat.X + 0.5f : k.CapitalX + 0.5f, seat != null ? seat.Y + 0.5f : k.CapitalY + 0.5f, Fx.LightPillar);
        }

        // Each town's bất mãn moves toward what its year made it; the kingdom's stability follows the people.
        void Unrest(Kingdom k, Settlement capital, long tick)
        {
            var rebellion = RebellionOf(k.Id);
            var royal = k.RoyalClan >= 0 && k.RoyalClan < _sim.Clans.All.Count ? _sim.Clans.All[k.RoyalClan] : null;
            float weighted = 0f, people = 0f;
            foreach (var s in _towns)
            {
                float t = 20f;
                if (s.StarvedLastYear > 0) t += 35f;
                if (s.Population > 30 && s.DeathsLastYear > s.BirthsLastYear * 1.5f) t += 10f;
                if (_sim.Disasters.IsInfected(s.Id) || _sim.Disasters.DroughtMonthsLeft(s.X, s.Y, tick) >= 0) t += 15f;
                t += (1f - k.Benevolence) * 35f - 10f;          // a cruel king +25, a kind one −10
                if (capital != null && Dist2(s, capital) > 180f * 180f) t += 10f; // the court is far, the officials greedy
                if (_sim.Factions.ProtectorOf(s.X, s.Y) != null) t -= 8f;       // a sect keeps the land in order
                if (s.Faith >= FaithSystem.ShrineFaith) t -= 5f;
                if (royal != null && royal.Members > 0 && royal.Prestige >= 36f) t -= 8f; // a gia tộc of cultivators on the throne awes the people
                if (s.Capital) t -= 30f;
                if (rebellion != null && !rebellion.Towns.Contains(s.Id)) t += 8f; // the war bleeds everyone
                s.Unrest += (Mathf.Clamp(t, 0f, 100f) - s.Unrest) * 0.35f;
                weighted += s.Unrest * s.Population;
                people += s.Population;
            }
            k.Stability = people > 0f ? 100f - weighted / people : 70f;
        }

        static float Dist2(Settlement a, Settlement b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

        // The king ages and dies; a cruel one in a restless land may not die of age.
        void Reign(Kingdom k, Settlement capital, long tick)
        {
            var rng = RngFor(tick, 100 + k.Id);
            int age = RulerAge(k, tick);
            float death = age < 40 ? 0.01f : 0.01f + (age - 40) * 0.005f;
            bool murdered = k.Stability < 30f && k.Benevolence < 0.4f && rng.NextFloat() < 0.06f;
            if (!murdered && rng.NextFloat() >= death) return;
            string old = k.Ruler;
            float x = capital != null ? capital.X + 0.5f : k.CapitalX + 0.5f, y = capital != null ? capital.Y + 0.5f : k.CapitalY + 0.5f;
            if (k.HasHeir && rng.NextFloat() < 0.85f)
            {
                k.Ruler = RulerName(k.Dynasty, ref rng);
                k.RulerBorn = tick - (long)(rng.Range(18f, 35f) * SimClock.DaysPerYear);
                k.Benevolence = Mathf.Clamp01(k.Benevolence + rng.Range(-0.35f, 0.35f));
                k.HasHeir = rng.NextFloat() < 0.8f;
                _sim.Events.Add(tick, EventKind.Succession, 2,
                    $"{(murdered ? $"Vua {old} của {k.Name} bị ám sát" : $"Vua {old} của {k.Name} băng hà")}, thái tử {k.Ruler} nối ngôi ({Temper(k)}).", x, y);
                return;
            }
            // No heir of age: a regent holds the throne, and an uncle in a great town reaches for it.
            k.Ruler = RulerName(k.Dynasty, ref rng);
            k.RulerBorn = tick - (long)(rng.Range(8f, 16f) * SimClock.DaysPerYear);
            k.HasHeir = false;
            k.Stability = Mathf.Max(0f, k.Stability - 20f);
            Settlement rival = null;
            foreach (var s in _towns)
                if (!s.Capital && s.Population >= 150 && (rival == null || s.Population > rival.Population)) rival = s;
            if (rival == null || rng.NextFloat() >= 0.35f || Rebellions.Count >= MaxRebellions || RebellionOf(k.Id) != null)
            {
                _sim.Events.Add(tick, EventKind.Succession, 2, $"Vua {old} của {k.Name} {(murdered ? "bị ám sát" : "băng hà")} không người nối dõi; ấu chúa {k.Ruler} lên ngôi, quyền thần nhiếp chính.", x, y);
                return;
            }
            var r = Start(k, rival, $"hoàng thúc {RulerName(k.Dynasty, ref rng)}", -1, true, tick, ref rng);
            _sim.Events.Add(tick, EventKind.War, 3,
                $"Vua {old} của {k.Name} {(murdered ? "bị ám sát" : "băng hà")} không người nối dõi: {r.Leader} ở {rival.Name} không phục ấu chúa {k.Ruler}, " +
                $"{r.Towns.Count} thành theo phò, {k.Name} rơi vào nội chiến tranh ngôi.", rival.X + 0.5f, rival.Y + 0.5f, Fx.Explosion);
        }

        // A town pushed too far rises; the restless around it join.
        void Rise(Kingdom k, Settlement capital, long tick)
        {
            var rng = RngFor(tick, 200 + k.Id);
            foreach (var s in _towns)
            {
                if (s.Capital || s.Population < 120 || s.Unrest < 78f || rng.NextFloat() >= 0.04f) continue;
                var clan = _sim.Clans.SeatedAt(s.Id);
                bool house = clan != null && clan.Members > 0;
                string leader = house ? $"{clan.Title}" : $"nghĩa quân thủ lĩnh {_w.Lore.PersonName(ref rng)}";
                var r = Start(k, s, leader, house ? clan.Index : -1, false, tick, ref rng);
                string why = s.StarvedLastYear > 0 ? "đói khổ" : k.Benevolence <= 0.35f ? $"vua {k.Ruler} bạo ngược" : "quan lại hà khắc";
                _sim.Events.Add(tick, EventKind.War, 3,
                    $"Dân {s.Name} không chịu nổi {why}: {leader} khởi nghĩa, {r.Towns.Count} thành hưởng ứng, {k.Name} rơi vào nội chiến.", s.X + 0.5f, s.Y + 0.5f, Fx.Explosion);
                return;
            }
        }

        Rebellion Start(Kingdom k, Settlement seat, string leader, int clan, bool usurper, long tick, ref DetRandom rng)
        {
            var r = new Rebellion { Kingdom = k.Id, Seat = seat.Id, Leader = leader, LeaderClan = clan, Usurper = usurper, Start = tick, Until = tick + rng.Range(2, 6) * (long)SimClock.DaysPerYear };
            foreach (var s in _towns)
                if (!s.Capital && (s == seat || (Dist2(s, seat) < 160f * 160f && s.Unrest >= (usurper ? 40f : 55f)))) r.Towns.Add(s.Id);
            Rebellions.Add(r);
            return r;
        }

        // A year of civil war: blood on both sides; at its end, the outcome.
        void WarYear(Rebellion r, int index, long tick)
        {
            var k = _w.Kingdoms[r.Kingdom];
            var settlements = _sim.Settlements;
            if (k.Fallen) { Rebellions.RemoveAt(index); return; }
            TownsOf(k);
            var rng = RngFor(tick, 300 + r.Kingdom);
            float rebels = 0f, crown = 0f, rebelPop = 0f, total = 0f;
            foreach (var s in _towns)
            {
                bool rebel = r.Towns.Contains(s.Id);
                r.Dead += settlements.Kill(s, Mathf.CeilToInt(s.Population * rng.Range(0.005f, 0.015f) * (rebel || s.Capital ? 1.5f : 0.5f)));
                total += s.Population;
                if (rebel) { rebelPop += s.Population; rebels += s.Population * (0.8f + s.Unrest / 100f); } // the desperate fight hard
                else crown += s.Population * (1f - s.Unrest / 100f) * (0.6f + 0.6f * k.Benevolence); // the restless fight half-heartedly; few die for a cruel king
            }
            var capital = CapitalOf(k);
            if (tick < r.Until && rebelPop > 0f && capital != null) return;
            Rebellions.RemoveAt(index);
            // Who stands behind whom: the sect guarding the capital backs the crown; a gia tộc its own rising.
            var crownSect = capital != null ? _sim.Factions.ProtectorOf(capital.X, capital.Y) : null;
            var seat = settlements.All[r.Seat];
            var rebelSect = seat.Alive ? _sim.Factions.ProtectorOf(seat.X, seat.Y) : null;
            if (crownSect != null && _sim.Factions.Get(crownSect.Id) is Faction cf) crown += cf.Power * 0.5f; // cultivators lend a hand, not an army
            if (r.LeaderClan >= 0) rebels += _sim.Clans.Power(_sim.Clans.All[r.LeaderClan]) * 0.5f;
            float roll = rng.Range(0.7f, 1.3f);
            // The crown's reach thins with distance: far from the capital it can hold the throne but not the land.
            float reach = capital != null ? Mathf.Clamp(1f - Mathf.Sqrt(Dist2(capital, seat)) / 400f, 0.3f, 1f) : 0.3f;
            bool takesThrone = rebels * roll > crown;
            bool holdsLand = rebels * roll > crown * reach;
            if (rebelPop <= 0f || !seat.Alive || !holdsLand)
            {
                foreach (int id in r.Towns)
                {
                    var s = settlements.All[id];
                    if (!s.Alive) continue;
                    r.Dead += settlements.Kill(s, Mathf.CeilToInt(s.Population * 0.05f)); // the purge
                    s.Unrest = 35f;
                }
                _sim.Events.Add(tick, EventKind.Destruction, r.Towns.Count >= 3 || r.Usurper ? 3 : 2,
                    $"{k.Name} dẹp yên cuộc {(r.Usurper ? "tranh ngôi" : "khởi nghĩa")} của {r.Leader} sau {(tick - r.Start) / SimClock.DaysPerYear} năm; {r.Dead:N0} người chết." +
                    (crownSect != null ? $" {crownSect.BaseName} đứng sau hoàng thất." : ""), seat.X + 0.5f, seat.Y + 0.5f);
                return;
            }
            if (crownSect != null && rebelSect != null && crownSect != rebelSect) _sim.Factions.Grievance(crownSect.Id, rebelSect.Id, 25f); // two sects behind two thrones
            string house = r.LeaderClan >= 0 ? _sim.Clans.All[r.LeaderClan].Name : null;
            if (takesThrone && (r.Usurper || capital == null || rebelPop >= total * 0.35f))
            {
                // The capital falls to them: a new dynasty.
                string old = k.Dynasty;
                int years = DynastyYears(k, tick);
                Crown(k, house, r.LeaderClan, tick);
                foreach (var s in _towns) s.Unrest = Mathf.Max(0f, s.Unrest - 30f);
                _sim.Events.Add(tick, EventKind.Founding, 3,
                    $"Triều {old} của {k.Name} sụp đổ sau {years} năm: {r.Leader} chiếm kinh đô, {k.Ruler} lên ngôi, lập triều {k.Dynasty}; {r.Dead:N0} người chết trong chiến loạn.",
                    capital != null ? capital.X + 0.5f : seat.X + 0.5f, capital != null ? capital.Y + 0.5f : seat.Y + 0.5f, Fx.LightPillar);
                return;
            }
            // They hold their own land: a new kingdom.
            var nk = Secede(k, r, house, tick);
            if (nk == null) return;
            _sim.Events.Add(tick, EventKind.Founding, 3,
                $"{r.Leader} cát cứ một phương, tách khỏi {k.Name} lập {nk.Name}, đóng đô ở {settlements.All[r.Seat].Name}; {r.Dead:N0} người chết trong chiến loạn.",
                seat.X + 0.5f, seat.Y + 0.5f, Fx.LightPillar);
        }

        // A new house on the throne: of a gia tộc if one led the rising, else a commoner who made himself king.
        void Crown(Kingdom k, string house, int clan, long tick)
        {
            var rng = RngFor(tick, 400 + k.Id);
            if (k.RoyalClan >= 0 && k.RoyalClan < _sim.Clans.All.Count && _sim.Clans.All[k.RoyalClan].Royal == k.Id) _sim.Clans.All[k.RoyalClan].Royal = -1;
            k.Dynasty = house ?? _w.Lore.Surnames[rng.Range(0, _w.Lore.Surnames.Length)];
            k.Ruler = RulerName(k.Dynasty, ref rng);
            k.RulerBorn = tick - (long)(rng.Range(28f, 50f) * SimClock.DaysPerYear);
            k.Benevolence = rng.Range(0.45f, 0.9f); // a new house begins by winning hearts
            k.HasHeir = true;
            k.DynastySince = tick;
            k.Stability = 60f;
            k.RoyalClan = clan;
            if (clan >= 0) _sim.Clans.All[clan].Royal = k.Id;
        }

        // The rebel towns become a kingdom of their own: its land is the part of the old one nearer its capital.
        Kingdom Secede(Kingdom old, Rebellion r, string house, long tick)
        {
            var settlements = _sim.Settlements.All;
            Settlement capital = null;
            foreach (int id in r.Towns)
                if (settlements[id].Alive && (capital == null || settlements[id].Population > capital.Population)) capital = settlements[id];
            if (capital == null || _w.Kingdoms.Count >= ushort.MaxValue - 1) return null;
            var nk = new Kingdom { Id = _w.Kingdoms.Count, Name = NewName(old, capital), Region = old.Region, CapitalX = capital.X, CapitalY = capital.Y, Parent = old.Id };
            _w.Kingdoms.Add(nk);
            Crown(nk, house, r.LeaderClan, tick);
            // Redraw the border: each cell of the old kingdom goes to whichever capital is nearer (warped like the original map).
            float ox = old.CapitalX, oy = old.CapitalY, nx = capital.X, ny = capital.Y;
            ushort from = (ushort)(old.Id + 1), to = (ushort)(nk.Id + 1);
            int n = _w.W;
            uint seed = _w.Seed;
            int moved = 0, y0 = n, y1 = 0;
            var counts = new int[n];
            Parallel.For(0, n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    if (_w.KingdomOf[i] != from) continue;
                    float px = x + Noise.Fbm(seed + 41u, x / 140f, y / 140f, 3) * 40f;
                    float py = y + Noise.Fbm(seed + 43u, x / 140f + 3.7f, y / 140f + 9.1f, 3) * 40f;
                    if ((px - nx) * (px - nx) + (py - ny) * (py - ny) < (px - ox) * (px - ox) + (py - oy) * (py - oy))
                    {
                        _w.KingdomOf[i] = to;
                        counts[y]++;
                    }
                }
            });
            for (int y = 0; y < n; y++)
            {
                if (counts[y] == 0) continue;
                moved += counts[y];
                y0 = Mathf.Min(y0, y);
                y1 = Mathf.Max(y1, y);
            }
            nk.LandCells = moved;
            old.LandCells -= moved;
            foreach (var s in settlements)
                if (s.Alive && !s.Sect && s.Kingdom == old.Id && _w.KingdomOf[_w.Idx(s.X, s.Y)] == to)
                {
                    s.Kingdom = nk.Id;
                    s.Capital = s == capital;
                }
            capital.Capital = true;
            capital.CivicRetry = 0; // a palace for the new court
            if (moved > 0) _w.NotifyLookChanged(0, Mathf.Max(0, y0 - 1), n - 1, Mathf.Min(n - 1, y1 + 1)); // the border on the map
            return nk;
        }

        // A name for a new kingdom: one of the region's that no kingdom holds, else the old one with a direction ("Tây Lương Quốc").
        string NewName(Kingdom old, Settlement capital)
        {
            foreach (var name in _w.Lore.Kingdoms[(int)old.Region])
            {
                bool taken = false;
                foreach (var k in _w.Kingdoms)
                    if (k.Name == name) { taken = true; break; }
                if (!taken) return name;
            }
            float dx = capital.X - old.CapitalX, dy = capital.Y - old.CapitalY;
            string dir = Mathf.Abs(dx) > Mathf.Abs(dy) ? (dx > 0 ? "Đông" : "Tây") : (dy > 0 ? "Bắc" : "Nam");
            return $"{dir} {old.Name}";
        }

        // Settlers raised a fallen kingdom again (SettlementSystem.Restore): a new house to rule it.
        public void OnRestored(Kingdom k, long tick)
        {
            Crown(k, null, -1, tick);
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, Rebellions.Count);
            foreach (var r in Rebellions) StateHash.Add(ref h, r.Kingdom | ((long)r.Seat << 16) | ((long)r.Towns.Count << 40));
            foreach (var k in _w.Kingdoms)
            {
                StateHash.Add(ref h, k.RulerBorn ^ ((long)k.Id << 50));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(k.Stability) | ((long)(k.RoyalClan + 1) << 32));
            }
        }
    }
}
