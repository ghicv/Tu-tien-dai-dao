using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public sealed class Technique
    {
        public int Index;
        public string Name;
        public int Element = -1;     // index into SpiritRoots.ElementNames; -1 = vạn năng (fits any root, no better, no worse)
        public int Grade = 1;        // 1 phàm phẩm … 5 cực phẩm
        public bool Demonic;         // ma công
        public bool Ancient;         // thượng cổ: older than any sect
        public int Sect = -1;        // the sect that keeps it as trấn phái công pháp, -1 if none does
        public int Creator = -1;     // the cultivator who wrote it, -1 for those of old or from heaven
        public long Tick;
        public bool Lost;            // thất truyền: no one alive knows it, no sect keeps it
        public int LostWith = -1;    // the sect it fell with (settlement id), -1 if never
        public int LostBy = -1;      // the faction that destroyed that sect, -1 if heaven did (or no one)

        public Realm Ceiling => TechniqueSystem.CeilingOf[Grade];
        public string GradeText => TechniqueSystem.GradeNames[Grade];
        public string ElementText => Element < 0 ? "vạn năng" : SpiritRoots.ElementNames[Element];
    }

    // Công pháp và truyền thừa (devlog 28). A cultivator climbs by a method, and a method only reaches so high:
    // phàm phẩm to Trúc Cơ, hạ phẩm to Kết Đan, trung phẩm to Nguyên Anh, thượng and cực phẩm to Hóa Thần. Speed
    // follows the grade and whether its element is one of the learner's roots. At the ceiling, no amount of
    // talent breaks through: a better method must be found.
    //
    // Where methods come from and go: a sect teaches its own (trấn phái công pháp); a master great enough writes
    // a new one; relics hold the ngọc giản of the dead, of fallen sects and of the thượng cổ; a killer takes
    // the victim's; a disciple who finds a better one offers it to the sect. When a sect is destroyed its method
    // falls with it (the victor may seize a copy); whoever recovers it from the ruins may raise the old sect
    // again under its old name, and the old grudge against those who destroyed it comes back with it.
    public sealed class TechniqueSystem
    {
        public static readonly string[] GradeNames = { "", "phàm phẩm", "hạ phẩm", "trung phẩm", "thượng phẩm", "cực phẩm" };
        public static readonly Realm[] CeilingOf = { Realm.Mortal, Realm.TrucCo, Realm.KetDan, Realm.NguyenAnh, Realm.HoaThan, Realm.HoaThan };
        static readonly float[] GradeSpeed = { 0f, 0.8f, 0.9f, 1f, 1.1f, 1.25f }; // about ×1 on average: methods shape who climbs, not how many
        const float Fits = 1.15f, Misfit = 0.8f; // the method's element among the roots, or not
        static readonly string[] Volumes = { "", "", "đệ nhị quyển", "đệ tam quyển", "đệ tứ quyển", "đệ ngũ quyển", "đệ lục quyển", "đệ thất quyển" };

        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly List<Technique> All = new List<Technique>();
        readonly Dictionary<int, int> _ofSect = new Dictionary<int, int>(); // faction id → technique index
        readonly Dictionary<string, int> _named = new Dictionary<string, int>(); // name → times used

        public TechniqueSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            var basic = new Technique { Index = 0, Name = _w.Lore.BasicTechnique, Grade = 1 }; // what every tán tu starts with
            All.Add(basic);
            _named[basic.Name] = 1;
            SeedInitial();
        }

        public Technique Basic => All[0];
        public Technique Of(Cultivator c) => c.Technique > 0 && c.Technique < All.Count ? All[c.Technique] : Basic;
        public Technique OfSect(int sect) => _ofSect.TryGetValue(sect, out int t) ? All[t] : null;

        public static float SpeedFor(Technique t, Cultivator c)
        {
            float s = GradeSpeed[t.Grade];
            if (t.Element >= 0) s *= (c.Roots & (1 << t.Element)) != 0 ? Fits : Misfit;
            return s;
        }

        public float Speed(Cultivator c) => SpeedFor(Of(c), c);
        public bool Allows(Cultivator c, Realm next) => next <= Of(c).Ceiling;
        // At the peak of their realm with a method that goes no higher: bình cảnh công pháp.
        public bool Capped(Cultivator c) => c.Realm < Realm.HoaThan && Realms.IsPeak(c.Realm, c.Stage) && !Allows(c, c.Realm + 1);

        // How much a method is worth to this cultivator: how high it reaches first, then how fast it goes.
        static float Value(Technique t, Cultivator c) => (int)t.Ceiling * 10f + SpeedFor(t, c);
        static float SectValue(Technique t) => (int)t.Ceiling * 10f + GradeSpeed[t.Grade];
        public bool Better(Technique t, Cultivator c) => t != null && Value(t, c) > Value(Of(c), c) + 0.01f;

        static int ElementOf(int roots)
        {
            for (int b = 0; b < 8; b++)
                if ((roots & (1 << b)) != 0) return b;
            return -1;
        }

        // ---------------------------------------------------------------- making methods

        // A new method. element -2: pick one (half are vạn năng). Names come from the lore, each used once; when a
        // pool runs out a later volume carries the name on ("Trường Xuân Công đệ nhị quyển").
        public Technique New(int grade, int element, bool demonic, int creator, long tick)
        {
            var rng = new DetRandom(Hash.U32(_w.Seed ^ 0x7EC4u, All.Count, (int)tick));
            if (demonic) element = -1;
            else if (element == -2) element = rng.NextFloat() < 0.5f ? -1 : rng.Range(0, 5);
            string[] pool = demonic ? _w.Lore.DemonicTechniques : null;
            if (pool == null)
                foreach (var p in _w.Lore.Techniques)
                    if (ElementIndex(p.element) == element) { pool = p.names; break; }
            if (pool == null || pool.Length == 0) pool = new[] { _w.Lore.BasicTechnique };
            string name = null;
            int start = rng.Range(0, pool.Length);
            for (int k = 0; k < pool.Length && name == null; k++)
                if (!_named.ContainsKey(pool[(start + k) % pool.Length])) name = pool[(start + k) % pool.Length];
            if (name == null)
            {
                string root = pool[start];
                int n = _named[root];
                name = n + 1 < Volumes.Length ? $"{root} {Volumes[n + 1]}" : $"{root} ({n + 1})";
                _named[root] = n + 1;
            }
            else _named[name] = 1;
            var t = new Technique { Index = All.Count, Name = name, Element = element, Grade = Mathf.Clamp(grade, 1, 5), Demonic = demonic, Creator = creator, Tick = tick };
            All.Add(t);
            return t;
        }

        static int ElementIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int k = 0; k < SpiritRoots.ElementNames.Length; k++)
                if (SpiritRoots.ElementNames[k] == name) return k;
            return -1;
        }

        void SeedInitial()
        {
            var rng = new DetRandom(_w.Seed ^ 0x7EC40u);
            foreach (var f in _sim.Factions.All)
            {
                if (!f.Alive) continue;
                float roll = rng.NextFloat();
                Adopt(f.Id, New(roll < 0.35f ? 2 : roll < 0.8f ? 3 : 4, -2, f.Demonic, -1, 0), 0, false);
            }
            // Tán tu keep the common method, unless they have already climbed past where it can carry anyone.
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.SectId < 0 && c.Realm >= Basic.Ceiling)
                    c.Technique = New(c.Realm >= Realm.KetDan ? 3 : 2, rng.NextFloat() < 0.5f ? ElementOf(c.Roots) : -1, c.Demonic, -1, 0).Index;
        }

        // ---------------------------------------------------------------- passing methods on

        // The sect takes t as its trấn phái công pháp: every member who would gain by it learns it.
        void Adopt(int sect, Technique t, long tick, bool announce)
        {
            _ofSect[sect] = t.Index;
            t.Sect = sect;
            t.Lost = false;
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.SectId == sect && Better(t, c)) c.Technique = t.Index; // a ma tông teaches its disciples ma công
        }

        // A new disciple learns the sect's method if it serves them better than their own.
        public void OnJoin(Cultivator c, int sect)
        {
            var t = OfSect(sect);
            if (t != null && Better(t, c)) c.Technique = t.Index;
        }

        // c comes upon t (a relic, a body, heaven): they take it up if it is better. A righteous cultivator turns
        // from ma công unless greed is stronger than conscience; then they fall to the demonic path with it.
        public bool Learn(Cultivator c, Technique t, long tick, out bool fell)
        {
            fell = false;
            if (c == null || !c.Alive || !Better(t, c)) return false;
            if (t.Demonic && !c.Demonic)
            {
                if (!_sim.Rules.DemonicAllowed || c.Ambition < 0.6f) return false;
                c.Demonic = true;
                fell = true;
            }
            c.Technique = t.Index;
            t.Lost = false;
            return true;
        }

        // A disciple brings a method home: if it is better than the sect's own, the sect makes it its trấn phái công pháp.
        public bool Offer(Cultivator c, Technique t, long tick)
        {
            if (c == null || c.SectId < 0 || t == null) return false;
            var f = _sim.Factions.Get(c.SectId);
            if (f == null || !f.Alive || t.Demonic != f.Demonic) return false;
            var cur = OfSect(c.SectId);
            if (cur != null && SectValue(t) <= SectValue(cur) + 0.01f) return false;
            if (cur != null && cur.Sect == c.SectId) cur.Sect = -1;
            Adopt(c.SectId, t, tick, true);
            return true;
        }

        // Giết người đoạt bảo: the storage bag holds their ngọc giản too.
        public void Plunder(Cultivator killer, Cultivator victim, long tick)
        {
            var t = Of(victim);
            if (t == Basic || !Learn(killer, t, tick, out bool fell)) return;
            _sim.Cultivation.ShowLoot(killer, Loot.Technique, tick); // the ngọc giản held up over the body
            _sim.Events.Add(tick, EventKind.Fortune, t.Grade >= 4 ? 2 : 1,
                $"{killer.Title} đoạt ngọc giản {t.Name} ({t.GradeText}) từ thi thể {victim.Name}" + (fell ? ", từ đó sa vào ma đạo." : "."),
                -1f, -1f, Fx.None, killer.Index, victim.Index, killer.SectId);
        }

        // Diệt môn: the victor may seize the scripture hall's method; otherwise it falls with the sect. Survivors
        // who knew it still do, but with no sect to keep it, it is lost to the world once they are gone.
        public void OnSectDestroyed(int loser, int winner, long tick)
        {
            var t = OfSect(loser);
            if (t == null) return;
            _ofSect.Remove(loser);
            t.Sect = -1;
            t.LostWith = loser;
            t.LostBy = winner;
            var wf = winner >= 0 ? _sim.Factions.Get(winner) : null;
            if (wf != null && wf.Alive && wf.Demonic == t.Demonic && (OfSect(winner) == null || SectValue(t) > SectValue(OfSect(winner)) + 0.01f))
            {
                Adopt(winner, t, tick, true);
                _sim.Events.Add(tick, EventKind.Fortune, 2, $"{_sim.Factions.NameOf(winner)} đoạt Tàng Kinh Các, lấy {t.Name} ({t.GradeText}) làm trấn phái công pháp.",
                    -1f, -1f, Fx.None, -1, -1, winner, loser);
                return;
            }
            t.Lost = !KnownByAnyone(t);
        }

        bool KnownByAnyone(Technique t)
        {
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.Technique == t.Index) return true;
            return false;
        }

        // A fallen sect whose method this cultivator now carries, if its name is free: founding a sect with it
        // raises the old one again (FactionSystem.Found).
        public Settlement RevivableSect(Cultivator c)
        {
            var t = Of(c);
            if (t.LostWith < 0 || t.LostWith >= _sim.Settlements.All.Count) return null;
            var old = _sim.Settlements.All[t.LostWith];
            if (old.Sect && old.Alive) return null;
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && s.Sect && s.BaseName == old.BaseName) return null;
            return old;
        }

        // A new sect keeps its founder's method; a founder with only the common method writes a hạ phẩm one of their own.
        public void OnFounded(Faction f, Cultivator founder, long tick)
        {
            var t = Of(founder);
            if (t.Grade < 2) t = New(2, ElementOf(founder.Roots), f.Demonic, founder.Index, tick);
            if (t.Demonic != f.Demonic) t = New(Mathf.Max(2, t.Grade - 1), ElementOf(founder.Roots), f.Demonic, founder.Index, tick);
            if (t.LostWith >= 0) t.LostWith = -1; // it is kept again
            Adopt(f.Id, t, tick, false);
            founder.Technique = t.Index;
        }

        // A sect that rose without a founder (a hall set down by Thiên Đạo): a hạ phẩm method of its own.
        public void EnsureSect(Faction f, long tick)
        {
            if (f != null && OfSect(f.Id) == null) Adopt(f.Id, New(2, -2, f.Demonic, -1, tick), tick, false);
        }

        // Thiên Đạo hands down a cực phẩm method made for this one's root.
        public Technique Bestow(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return null;
            var t = New(5, ElementOf(c.Roots), c.Demonic, -1, tick);
            c.Technique = t.Index;
            c.Blessed = true;
            _sim.Cultivation.ShowLoot(c, Loot.Technique, tick, 60);
            bool sect = Offer(c, t, tick);
            _sim.Events.Add(tick, EventKind.Divine, 3,
                $"Thiên Đạo truyền thụ {t.Name} ({t.GradeText}, {t.ElementText}) cho {c.Title}" + (sect ? $"; {_sim.Cultivation.SectName(c)} lập làm trấn phái công pháp." : "."),
                _sim.Entities.X[c.Entity], _sim.Entities.Y[c.Entity], Fx.Blessing, c.Index, -1, c.SectId);
            return t;
        }

        // ---------------------------------------------------------------- yearly: masters write new methods

        public void YearlyStep(long tick)
        {
            var rng = new DetRandom(Hash.U32(_w.Seed ^ 0x7EC41u, (int)(tick / SimClock.DaysPerYear), 0));
            // A sect master great enough, and wise enough, writes a method beyond the one the sect has.
            foreach (var f in _sim.Factions.All)
            {
                if (!f.Alive) continue;
                var m = _sim.Cultivation.MasterOf(f.Id);
                if (m == null || !m.Alive || m.Comprehension < 0.6f) continue;
                int grade = m.Realm >= Realm.HoaThan ? 5 : m.Realm == Realm.NguyenAnh ? 4 : m.Realm == Realm.KetDan ? 3 : 0;
                var cur = OfSect(f.Id);
                if (grade == 0 || (cur != null && cur.Grade >= grade) || rng.NextFloat() >= 0.03f * m.Comprehension) continue;
                var t = New(grade, rng.NextFloat() < 0.6f ? ElementOf(m.Roots) : -1, f.Demonic, m.Index, tick);
                if (cur != null && cur.Sect == f.Id) cur.Sect = -1;
                Adopt(f.Id, t, tick, true);
                if (Better(t, m)) m.Technique = t.Index;
                _sim.Events.Add(tick, EventKind.Fortune, grade >= 4 ? 3 : 2,
                    $"{m.Title} bế quan ngộ đạo, sáng tạo {t.Name} ({t.GradeText}, {t.ElementText}), lập làm trấn phái công pháp của {_sim.Factions.NameOf(f.Id)}.",
                    -1f, -1f, Fx.LightPillar, m.Index, -1, f.Id);
            }
            // A tán tu stuck at the ceiling of their method, if wise enough, breaks it by writing their own.
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.SectId >= 0 || c.Realm < Realm.TrucCo || c.Comprehension < 0.7f || !Capped(c)) continue;
                if (rng.NextFloat() >= 0.02f * c.Comprehension) continue;
                var t = New(c.Realm >= Realm.KetDan ? 4 : 3, ElementOf(c.Roots), c.Demonic, c.Index, tick);
                c.Technique = t.Index;
                _sim.Events.Add(tick, EventKind.Fortune, 2,
                    $"{c.Title} bị bình cảnh công pháp vây khốn nhiều năm, tự mình ngộ ra {t.Name} ({t.GradeText}).", -1f, -1f, Fx.LightPillar, c.Index);
            }
        }

        public int LostCount()
        {
            int n = 0;
            foreach (var t in All)
                if (t.Lost) n++;
            return n;
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, All.Count);
            foreach (var kv in _ofSect) StateHash.Add(ref h, kv.Key | ((long)kv.Value << 32));
            foreach (var t in All) StateHash.Add(ref h, t.Grade | (t.Lost ? 1 << 8 : 0) | ((long)t.Sect << 16) | ((long)t.LostWith << 40));
        }
    }
}
