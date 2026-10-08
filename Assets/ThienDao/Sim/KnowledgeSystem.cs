using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    public enum RumorKind : byte { Relic, HungThu, WeakSect }

    public sealed class RumorDisc
    {
        public float X, Y, R, Speed; // where the word is, how far it has gone, how fast it goes on (cells a month)
    }

    public sealed class Rumor
    {
        public RumorKind Kind;
        public int Subject;          // relic index, beast index, or the weakened sect's id
        public long Start, Until;
        public float Exaggeration = 1f; // told and retold: the farther it travels, the grander it grows
        public readonly List<RumorDisc> Discs = new List<RumorDisc>();
        public readonly List<int> Heard = new List<int>(); // sects whose mountain gate it has reached, in order
    }

    // Tri thức và tin đồn (devlog 29). No one in the world knows everything any more. Word of a bí cảnh, of a
    // linh vật's light, of a hung thú, of a sect that has just lost its master, starts where it happened and
    // spreads: outward a little every month (faster where many villages are), and in leaps along the roads,
    // carried by caravans and migrants to the towns they reach, growing in the telling. A sect acts only on what
    // has reached its mountain gate: it joins a contest late, or not at all; it answers a coalition only if it has
    // heard what the beast has done; it covets a neighbour only once it hears of its weakness.
    //
    // Cheap by design: a few dozen rumors of a few discs each, grown once a month; nothing per person.
    public sealed class KnowledgeSystem
    {
        const int MaxRumors = 48, MaxDiscs = 8;
        const float MaxRadius = 420f;

        readonly Simulation _sim;
        public readonly List<Rumor> All = new List<Rumor>();

        public KnowledgeSystem(Simulation sim)
        {
            _sim = sim;
        }

        public Rumor Of(RumorKind kind, int subject)
        {
            foreach (var r in All)
                if (r.Kind == kind && r.Subject == subject) return r;
            return null;
        }

        public static bool Knows(Rumor r, float x, float y)
        {
            if (r == null) return false;
            foreach (var d in r.Discs)
            {
                float dx = x - d.X, dy = y - d.Y;
                if (dx * dx + dy * dy <= d.R * d.R) return true;
            }
            return false;
        }

        public bool Knows(RumorKind kind, int subject, float x, float y) => Knows(Of(kind, subject), x, y);

        // Word starts (or starts again) at (x, y), already known within r0: a linh vật's light is seen from afar,
        // a cave found in the wilds is known only to the one who found it.
        public Rumor Spread(RumorKind kind, int subject, float x, float y, float r0, long tick, int years = 30)
        {
            var r = Of(kind, subject);
            if (r == null)
            {
                if (All.Count >= MaxRumors) All.RemoveAt(0); // the oldest is forgotten
                r = new Rumor { Kind = kind, Subject = subject, Start = tick, Until = tick + years * (long)SimClock.DaysPerYear };
                All.Add(r);
            }
            AddDisc(r, x, y, r0);
            return r;
        }

        void AddDisc(Rumor r, float x, float y, float r0)
        {
            foreach (var d in r.Discs)
            {
                float dx = x - d.X, dy = y - d.Y;
                if (dx * dx + dy * dy <= d.R * d.R) { d.R = Mathf.Max(d.R, r0); return; } // already known there
            }
            if (r.Discs.Count >= MaxDiscs) return;
            r.Discs.Add(new RumorDisc { X = x, Y = y, R = r0, Speed = SpeedAt(x, y) });
        }

        // Word walks with people: 10 cells a month in the wilds, up to 34 where villages crowd together.
        float SpeedAt(float x, float y)
        {
            int near = 0;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                float dx = s.X - x, dy = s.Y - y;
                if (dx * dx + dy * dy < 80f * 80f && ++near >= 12) break;
            }
            return 10f + 2f * near;
        }

        // A caravan or a band of migrants reached `to` from `from`: whatever was known at the one is now told at
        // the other, a little bigger than it was.
        public void Carry(float fromX, float fromY, float toX, float toY)
        {
            foreach (var r in All)
            {
                if (!Knows(r, fromX, fromY) || Knows(r, toX, toY)) continue;
                AddDisc(r, toX, toY, 6f);
                r.Exaggeration = Mathf.Min(2.5f, r.Exaggeration * 1.15f);
            }
        }

        // ---------------------------------------------------------------- monthly: the word goes on

        public void MonthlyStep(long tick)
        {
            for (int k = All.Count - 1; k >= 0; k--)
            {
                var r = All[k];
                if (tick >= r.Until || !SubjectStands(r)) { All.RemoveAt(k); continue; }
                foreach (var d in r.Discs) d.R = Mathf.Min(MaxRadius, d.R + d.Speed);
                // Mountain gates it reaches for the first time.
                foreach (var f in _sim.Factions.All)
                {
                    if (!f.Alive || r.Heard.Contains(f.Id)) continue;
                    var seat = _sim.Settlements.All[f.Id];
                    if (!Knows(r, seat.X + 0.5f, seat.Y + 0.5f)) continue;
                    r.Heard.Add(f.Id);
                    Heard(r, f, tick);
                }
            }
        }

        bool SubjectStands(Rumor r)
        {
            switch (r.Kind)
            {
                case RumorKind.Relic: return r.Subject < _sim.Relics.All.Count && _sim.Relics.All[r.Subject].Open;
                case RumorKind.HungThu: return r.Subject < _sim.Beasts.All.Count && _sim.Beasts.All[r.Subject].Alive && _sim.Beasts.All[r.Subject].Rampage;
                default: var f = _sim.Factions.Get(r.Subject); return f != null && f.Alive;
            }
        }

        // What a sect does when the word reaches it.
        void Heard(Rumor r, Faction f, long tick)
        {
            switch (r.Kind)
            {
                case RumorKind.Relic:
                    _sim.Relics.OnHeard(_sim.Relics.All[r.Subject], f, r.Exaggeration, tick); // join the contest late, or start one
                    break;
                case RumorKind.WeakSect:
                    // A stronger neighbour hears of the weakness and begins to covet.
                    var weak = _sim.Factions.Get(r.Subject);
                    if (weak == null || weak == f || f.Power <= weak.Power * 1.2f) break;
                    var a = _sim.Settlements.All[f.Id];
                    var b = _sim.Settlements.All[weak.Id];
                    if ((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) > 300 * 300 || _sim.Factions.StanceBetween(f.Id, weak.Id) == Stance.Allied) break;
                    _sim.Factions.Grievance(f.Id, weak.Id, 12f * r.Exaggeration);
                    _sim.Events.Add(tick, EventKind.War, 2, $"{_sim.Factions.NameOf(f.Id)} nghe tin {_sim.Factions.NameOf(weak.Id)} suy yếu, bắt đầu dòm ngó.",
                        -1f, -1f, Fx.None, -1, -1, f.Id, weak.Id);
                    break;
            }
        }

        // How many sects the word of this has reached, and the first few of them (the panels).
        public int HeardCount(Rumor r) => r?.Heard.Count ?? 0;

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, All.Count);
            foreach (var r in All)
            {
                StateHash.Add(ref h, (int)r.Kind | ((long)r.Subject << 8) | ((long)r.Heard.Count << 40));
                foreach (var d in r.Discs) StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(d.R) | ((long)(int)d.X << 32));
            }
        }
    }
}
