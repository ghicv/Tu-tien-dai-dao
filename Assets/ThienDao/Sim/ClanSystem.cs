using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public sealed class Clan
    {
        public int Index;
        public string Name;          // the surname: "Hàn" (shown as "Hàn gia", "Hàn thế gia" once great)
        public int Seat;             // its ancestral village (settlement id)
        public int Founder;          // the cultivator who raised it
        public long Founded;
        public float Prestige;       // uy danh: the strength of its living members, more for a royal house
        public bool Fallen;          // tuyệt tự: no one of it left for a generation
        public long LastAlive;       // when a member was last alive
        public int Royal = -1;       // the kingdom whose throne it holds (PoliticsSystem)
        public readonly List<int> Feuds = new List<int>(); // clans it is at blood feud with
        public int Members;          // living members (recounted yearly)

        public string Title => Royal >= 0 ? $"{Name} hoàng tộc" : Prestige >= 600f ? $"{Name} thế gia" : $"{Name} gia";
    }

    // Gia tộc (devlog 30). A Kết Đan who has made their name raises a tu tiên gia tộc on the land they were born
    // on. Children who show a spirit root there are mostly taken in, and carry its surname: generations of kin
    // under one name, beside (not instead of) the sects most of them still join. A gia tộc remembers: kill one of
    // its people and its strongest swear vengeance, and two houses with blood between them fight when their people
    // meet. A great house seated in a town that rises against the crown may lead the rising, and take the throne
    // (PoliticsSystem). A house with no one left for thirty years is gone.
    public sealed class ClanSystem
    {
        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly List<Clan> All = new List<Clan>();

        public ClanSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0xC1A11u, (int)tick, salt));

        public Clan Of(Cultivator c) => c != null && c.Clan >= 0 && c.Clan < All.Count ? All[c.Clan] : null;

        public Clan SeatedAt(int settlement)
        {
            foreach (var k in All)
                if (!k.Fallen && k.Seat == settlement) return k;
            return null;
        }

        public bool Feuding(int a, int b) => a >= 0 && b >= 0 && a < All.Count && All[a].Feuds.Contains(b);

        // The surname and the given name, by the world's surnames (some have two words: Nam Cung, Âu Dương).
        public (string surname, string given) Split(string name)
        {
            string best = null;
            foreach (var s in _w.Lore.Surnames)
                if (name.StartsWith(s + " ") && (best == null || s.Length > best.Length)) best = s;
            if (best == null)
            {
                int sp = name.IndexOf(' ');
                return sp > 0 ? (name.Substring(0, sp), name.Substring(sp + 1)) : (name, "");
            }
            return (best, name.Substring(best.Length + 1));
        }

        // A child awakened on a gia tộc's ancestral land: most are taken in and given its name.
        public void OnAwakened(Cultivator c, Settlement village)
        {
            var clan = SeatedAt(village.Id);
            if (clan == null || c.Clan >= 0) return;
            var rng = RngFor(c.BirthTick, 1000 + c.Index);
            if (rng.NextFloat() >= 0.7f) return;
            c.Clan = clan.Index;
            var (_, given) = Split(c.Name);
            if (given.Length > 0) c.Name = $"{clan.Name} {given}";
        }

        // Someone was killed: their kin swear vengeance (the strongest two who have no other blood debt), and if the
        // killer is of a house too, the two houses are at blood feud.
        public void OnKilled(Cultivator killer, Cultivator victim, long tick)
        {
            var clan = Of(victim);
            if (clan == null || killer.Clan == victim.Clan) return;
            var avengers = new List<Cultivator>();
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.Clan == clan.Index && c != victim && c.Nemesis < 0) avengers.Add(c);
            avengers.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            for (int k = 0; k < Mathf.Min(2, avengers.Count); k++)
            {
                avengers[k].Nemesis = killer.Index;
                avengers[k].NemesisFor = victim.Index;
                avengers[k].NemesisTick = tick;
            }
            var other = Of(killer);
            if (other != null && !clan.Feuds.Contains(other.Index))
            {
                clan.Feuds.Add(other.Index);
                other.Feuds.Add(clan.Index);
                _sim.Events.Add(tick, EventKind.Vendetta, 2, $"{killer.Title} giết {victim.Name} của {clan.Title}: {clan.Title} và {other.Title} từ nay thế thù.",
                    -1f, -1f, Fx.None, killer.Index, victim.Index);
            }
        }

        // The power of a house in the world: its living members, weighted by realm.
        public float Power(Clan clan)
        {
            float p = 0f;
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.Clan == clan.Index) p += Realms.Power[(int)c.Realm];
            return p;
        }

        // ---------------------------------------------------------------- yearly: houses raised, counted, ended

        public void YearlyStep(long tick)
        {
            var settlements = _sim.Settlements.All;
            foreach (var k in All) { k.Members = 0; k.Prestige = 0f; }
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Clan < 0) continue;
                var k = All[c.Clan];
                k.Members++;
                k.Prestige += Realms.Power[(int)c.Realm];
                k.LastAlive = tick;
            }
            foreach (var k in All)
            {
                if (k.Fallen) continue;
                if (k.Royal >= 0) k.Prestige *= 1.5f;
                bool seatGone = k.Seat < 0 || k.Seat >= settlements.Count || !settlements[k.Seat].Alive;
                if (k.Members == 0 && tick - k.LastAlive > 30L * SimClock.DaysPerYear)
                {
                    k.Fallen = true;
                    _sim.Events.Add(tick, EventKind.Death, 2, $"{k.Title} tuyệt tự sau {(tick - k.Founded) / SimClock.DaysPerYear} năm, không còn ai mang họ {k.Name} tu tiên.");
                }
                else if (seatGone) MoveSeat(k);
            }

            // A Kết Đan who has made their name raises a house on the land of their birth.
            var rng = RngFor(tick, 7);
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Clan >= 0 || c.Realm < Realm.KetDan || rng.NextFloat() >= 0.04f) continue;
                var home = c.Origin >= 0 && c.Origin < settlements.Count && settlements[c.Origin].Alive && !settlements[c.Origin].Sect
                    ? settlements[c.Origin] : NearestVillage(c.HomeX, c.HomeY);
                if (home == null || SeatedAt(home.Id) != null) continue;
                Found(c, home, tick);
            }
        }

        public Clan Found(Cultivator c, Settlement home, long tick)
        {
            var (surname, _) = Split(c.Name);
            var clan = new Clan { Index = All.Count, Name = surname, Seat = home.Id, Founder = c.Index, Founded = tick, LastAlive = tick, Members = 1 };
            All.Add(clan);
            c.Clan = clan.Index;
            clan.Prestige = Realms.Power[(int)c.Realm];
            _sim.Events.Add(tick, EventKind.Founding, 2,
                $"{c.Title} ({_sim.Cultivation.SectName(c)}) trở về {home.Name}, dựng {clan.Title}: con cháu họ {surname} ở đó từ nay có chỗ dựa tu tiên.",
                home.X + 0.5f, home.Y + 0.5f, Fx.Blessing, c.Index);
            return clan;
        }

        // The ancestral village is gone: the house moves to the nearest village where its people live on.
        void MoveSeat(Clan k)
        {
            Cultivator any = null;
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.Clan == k.Index) { any = c; break; }
            var v = any != null ? NearestVillage(any.HomeX, any.HomeY) : null;
            if (v != null && SeatedAt(v.Id) == null) k.Seat = v.Id;
        }

        Settlement NearestVillage(float x, float y)
        {
            Settlement best = null;
            float bestD = 120f * 120f;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive || s.Sect) continue;
                float dx = s.X - x, dy = s.Y - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, All.Count);
            foreach (var k in All) StateHash.Add(ref h, k.Seat | ((long)k.Feuds.Count << 24) | (k.Fallen ? 1L << 40 : 0) | ((long)(k.Royal + 1) << 44));
        }
    }
}
