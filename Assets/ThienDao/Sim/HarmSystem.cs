using System.Collections.Generic;
using UnityEngine;

namespace ThienDao.Sim
{
    // Blows from heaven and earth (thiên lôi, the quaking ground, cuồng phong, the fire of a new volcano) land on
    // every living thing in reach, and are measured in sinh lực like any other wound: a blow heavier than what
    // is left of a body kills it, a lighter one wounds it (the wounded heal, or are finished by the next foe).
    // Herds, migrant crowds and caravans, which the sim counts rather than tracks, lose the share the blow would
    // kill. No randomness: the same blow on the same world always does the same.
    public sealed class HarmSystem
    {
        public const float Bolt = 4000f;        // one đạo thiên lôi: more than a Nguyên Anh has, a third of a Hóa Thần
        public const float MinBoltShare = 0.4f; // the one it is aimed at loses at least this share of their sinh lực
        const float CrowdHp = 70f;              // a crowd or herd falls as a body of this many beasts or people would

        public struct Report
        {
            public int Fallen, Wounded, BeastsSlain, BeastsWounded, People, Animals, Caravans;
        }

        readonly Simulation _sim;
        readonly List<Vector3> _reach = new List<Vector3>(); // x, y, radius
        float _x0, _y0, _x1, _y1;

        public HarmSystem(Simulation sim)
        {
            _sim = sim;
        }

        public void Clear()
        {
            _reach.Clear();
            _x0 = _y0 = float.MaxValue;
            _x1 = _y1 = float.MinValue;
        }

        public void Add(float x, float y, float r)
        {
            _reach.Add(new Vector3(x, y, r));
            _x0 = Mathf.Min(_x0, x - r);
            _y0 = Mathf.Min(_y0, y - r);
            _x1 = Mathf.Max(_x1, x + r);
            _y1 = Mathf.Max(_y1, y + r);
        }

        // How hard the blow lands here: 1 at a centre, 0.3 at the rim, 0 outside every circle.
        public float Reach(float x, float y)
        {
            if (x < _x0 || x > _x1 || y < _y0 || y > _y1) return 0f;
            float best = 0f;
            foreach (var c in _reach)
            {
                float dx = x - c.x, dy = y - c.y, d2 = dx * dx + dy * dy;
                if (d2 > c.z * c.z) continue;
                best = Mathf.Max(best, 1f - 0.7f * Mathf.Sqrt(d2) / Mathf.Max(0.01f, c.z));
            }
            return best;
        }

        // The share of a crowd (or herd) whose members each have hpEach that a blow of this weight kills.
        public static float CrowdShare(float damage, float hpEach) => hpEach <= 0f ? 0f : Mathf.Clamp01(damage / (hpEach * CrowdHp));

        // Strikes everything inside the circles added since Clear. verb finishes "<who> …" for those it kills.
        // groundOnly: a blow from below (an earthquake) that whoever is in the air rides out. skipC / skipB were
        // already struck on their own (the one a thiên phạt was aimed at).
        public Report Strike(float damage, long tick, string verb, bool groundOnly, Cultivator skipC = null, Beast skipB = null)
        {
            var r = new Report();
            if (_reach.Count == 0 || damage <= 0f) return r;
            var e = _sim.Entities;
            var cult = _sim.Cultivation;
            foreach (var c in cult.All)
            {
                if (!c.Alive || c == skipC) continue;
                if (groundOnly && c.Travelling && e.Flying[c.Entity]) continue;
                float k = Reach(e.X[c.Entity], e.Y[c.Entity]);
                if (k <= 0f) continue;
                if (Wound(c, damage * k))
                {
                    cult.Perish(c, tick, $"{c.Name} ({cult.SectName(c)}) {verb}.", c.Realm >= Realm.KetDan ? 2 : c.Realm >= Realm.TrucCo ? 1 : 0, Fx.None);
                    r.Fallen++;
                }
                else r.Wounded++;
            }
            var beasts = _sim.Beasts;
            for (int i = 0; i < beasts.All.Count; i++)
            {
                var b = beasts.All[i];
                if (!b.Alive || b == skipB) continue;
                if (groundOnly && BeastSystem.Flies(b.Kind)) continue;
                float k = Reach(e.X[b.Entity], e.Y[b.Entity]);
                if (k <= 0f) continue;
                if (Wound(b, damage * k))
                {
                    beasts.Perish(b, tick, $"{b.Title} ({b.GradeText}) {verb}.", b.Rampage || b.Grade >= 6 ? 2 : 0);
                    r.BeastsSlain++;
                }
                else r.BeastsWounded++;
            }
            r.People = _sim.Settlements.HarmMigrants(this, damage, tick);
            r.Caravans = _sim.Trade.Harm(this, damage);
            r.Animals = HarmHerds(damage);
            return r;
        }

        // True if the blow killed them (the caller tells the world); otherwise they keep what is left.
        public static bool Wound(Cultivator c, float damage)
        {
            float left = CombatSystem.HpOf(c) - damage;
            if (left <= 0f) return true;
            c.Hp = left;
            return false;
        }

        public static bool Wound(Beast b, float damage)
        {
            float left = BeastSystem.HpOf(b) - damage;
            if (left <= 0f) return true;
            b.Hp = left;
            return false;
        }

        static readonly Species[] Herds = { Species.Deer, Species.Rabbit, Species.Wolf };

        // Region herds: each circle covers part of its region and kills that part's share of each herd.
        int HarmHerds(float damage)
        {
            var wild = _sim.Wildlife;
            const float region = WildlifeSystem.Region;
            float dead = 0f;
            foreach (var c in _reach)
            {
                int reg = wild.RegionOf(c.x, c.y);
                float cover = Mathf.Min(1f, Mathf.PI * c.z * c.z / (region * region));
                foreach (var s in Herds)
                {
                    float share = CrowdShare(damage * 0.65f, SpeciesInfo.Hp[(int)s]) * cover; // 0.65: the blow's mean weight over a circle
                    if (share <= 0f) continue;
                    float before = wild.At(s, reg);
                    wild.Cull(s, reg, 1f - share);
                    dead += before - wild.At(s, reg);
                }
            }
            return Mathf.RoundToInt(dead);
        }

        // ", 3 tu sĩ vẫn lạc, 1 yêu thú bỏ mạng": what a calamity's message adds about the blow.
        public static string Tail(Report r)
        {
            var sb = new System.Text.StringBuilder();
            if (r.Fallen > 0) sb.Append($", {r.Fallen} tu sĩ vẫn lạc");
            if (r.Wounded > 0) sb.Append($", {r.Wounded} tu sĩ bị thương");
            if (r.BeastsSlain > 0) sb.Append($", {r.BeastsSlain} yêu thú bỏ mạng");
            if (r.BeastsWounded > 0) sb.Append($", {r.BeastsWounded} yêu thú bị thương");
            if (r.People > 0) sb.Append($", {r.People} di dân thiệt mạng");
            if (r.Caravans > 0) sb.Append($", {r.Caravans} thương đội tan tác");
            if (r.Animals >= 5) sb.Append($", {r.Animals:N0} con thú chết");
            return sb.ToString();
        }
    }
}
