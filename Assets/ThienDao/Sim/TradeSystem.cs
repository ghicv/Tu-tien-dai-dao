using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public enum Good : byte { Food, Herb, Pill, Count }

    public sealed class Market
    {
        public float Herbs, Pills;                           // stock beyond what the village eats (food is Settlement.Food)
        public readonly float[] Price = { 1f, 10f, 40f };    // in linh thạch-equivalents
        public int CaravansSent, CaravansReceived;
    }

    public sealed class Caravan
    {
        public int Entity, From, To;
        public Good Good;
        public float Amount;
        public long Start;
    }

    // Kinh tế (GDD §7): every village has a market. Food beyond six months' stock is for sale; villages on rich qi
    // gather linh thảo; sects turn herbs into pills. Prices follow scarcity. Thương đội carry goods from where
    // they are cheap to where they are dear, so hungry villages are fed by full ones; and where caravans pass
    // often enough, a road appears on the map.
    public sealed class TradeSystem
    {
        public static readonly string[] GoodNames = { "lương thực", "linh thảo", "đan dược" };
        static readonly float[] BasePrice = { 1f, 10f, 40f };
        const int MaxCaravans = 60;
        const float Reach = 200f;

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly List<Market> _markets = new List<Market>();
        readonly List<Caravan> _caravans = new List<Caravan>();
        public int Roads => _sim.Paths?.RoadCells ?? 0; // cells of đường đất (PathSystem)
        public int CaravanCount => _caravans.Count;
        public long Delivered { get; private set; } // caravans that reached their market

        public TradeSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0x7EADu, (int)tick, salt));

        public Market MarketOf(Settlement s)
        {
            while (_markets.Count <= s.Id) _markets.Add(new Market());
            return _markets[s.Id];
        }

        public float PriceFactor(Settlement s, Good g) => MarketOf(s).Price[(int)g] / BasePrice[(int)g];

        public Caravan ForEntity(int entity)
        {
            foreach (var c in _caravans)
                if (c.Entity == entity) return c;
            return null;
        }

        // A sect's own alchemists supply a pill for a disciple's breakthrough, if they have one in store.
        public bool TakePill(int settlement)
        {
            if (settlement < 0) return false;
            var m = MarketOf(_sim.Settlements.All[settlement]);
            if (m.Pills < 1f) return false;
            m.Pills -= 1f;
            return true;
        }

        // ---------------------------------------------------------------- monthly: produce, price, send

        public void MonthlyStep(long tick)
        {
            var settlements = _sim.Settlements.All;
            foreach (var s in settlements)
            {
                if (!s.Alive) continue;
                var m = MarketOf(s);
                int pop = Mathf.Max(1, s.Population);
                // Herb gatherers work the rich-qi slopes around the village.
                float qi = _sim.Qi.SampleQi(s.X, s.Y);
                m.Herbs += s.Workers * 0.01f * Mathf.Clamp01((qi - 1500f) / 4000f);
                // A sect's luyện đan sư turn herbs into pills.
                int members = s.Sect ? (_sim.Factions.Get(s.Id)?.Members ?? 0) : 0;
                if (members > 0)
                {
                    float made = Mathf.Min(m.Herbs * 0.25f, members * 0.04f);
                    m.Herbs -= made * 2f;
                    m.Pills += made;
                }
                m.Herbs = Mathf.Min(m.Herbs, 400f);
                m.Pills = Mathf.Min(m.Pills, 60f);

                float foodSupply = Mathf.Max(0f, s.Food - pop * 6f), foodDemand = Mathf.Max(0f, pop * 4f - s.Food);
                float herbDemand = members * 0.5f + 1f, pillDemand = (members > 0 ? members * 0.05f : pop * 0.002f) + 0.3f;
                SetPrice(m, Good.Food, foodDemand / Mathf.Max(1f, pop * 0.5f), foodSupply / Mathf.Max(1f, pop * 0.5f));
                SetPrice(m, Good.Herb, herbDemand, m.Herbs);
                SetPrice(m, Good.Pill, pillDemand, m.Pills);
            }

            // Merchants set out where a good is cheap here and dear somewhere in reach.
            for (int k = 0; k < settlements.Count && _caravans.Count < MaxCaravans; k++)
            {
                var s = settlements[k];
                if (!s.Alive || s.Population < 40) continue;
                var rng = RngFor(tick, s.Id);
                if (rng.NextFloat() >= (_sim.Settlements.HasCivic(s, ObjectType.Market) ? 0.16f : 0.08f)) continue; // a chợ sends out twice the merchants
                TrySend(s, tick, ref rng);
            }
        }

        static void SetPrice(Market m, Good g, float demand, float supply)
        {
            float ratio = (demand + 1f) / (supply + 1f);
            m.Price[(int)g] = BasePrice[(int)g] * Mathf.Clamp(Mathf.Sqrt(ratio), 0.25f, 4f);
        }

        void TrySend(Settlement s, long tick, ref DetRandom rng)
        {
            var m = MarketOf(s);
            int pop = Mathf.Max(1, s.Population);
            Settlement best = null;
            Good bestGood = Good.Food;
            float bestGain = 0f;
            foreach (var p in _sim.Settlements.All)
            {
                if (!p.Alive || p == s) continue;
                float dx = p.X - s.X, dy = p.Y - s.Y, d2 = dx * dx + dy * dy;
                if (d2 > Reach * Reach || d2 < 20f * 20f) continue;
                var pm = MarketOf(p);
                float draw = _sim.Settlements.HasCivic(p, ObjectType.Market) ? 0.15f : 0f; // merchants like a town with a chợ
                for (int g = 0; g < (int)Good.Count; g++)
                {
                    float stock = g == 0 ? s.Food - pop * 6f : g == 1 ? m.Herbs : m.Pills;
                    if (stock < (g == 0 ? pop * 2f : g == 1 ? 5f : 2f)) continue;
                    // What moving it earns, less the road (farther is dearer).
                    float gain = (pm.Price[g] - m.Price[g]) / BasePrice[g] - Mathf.Sqrt(d2) / 400f + draw;
                    if (gain > bestGain) { bestGain = gain; best = p; bestGood = (Good)g; }
                }
            }
            if (best == null || bestGain < 0.3f || !PathClear(s.X, s.Y, best.X, best.Y)) return;
            float amount;
            switch (bestGood)
            {
                case Good.Food:
                    amount = Mathf.Min((s.Food - pop * 6f) * 0.5f, best.Population * 4f + 20f);
                    s.Food -= amount;
                    break;
                case Good.Herb:
                    amount = m.Herbs * 0.5f;
                    m.Herbs -= amount;
                    break;
                default:
                    amount = Mathf.Floor(m.Pills * 0.5f);
                    m.Pills -= amount;
                    break;
            }
            if (amount <= 0f) return;
            var e = _sim.Entities;
            int id = e.Spawn(Species.Caravan, s.X + 0.5f, s.Y + 0.5f, tick);
            e.TX[id] = best.X + 0.5f;
            e.TY[id] = best.Y + 0.5f;
            _caravans.Add(new Caravan { Entity = id, From = s.Id, To = best.Id, Good = bestGood, Amount = amount, Start = tick });
            m.CaravansSent++;
        }

        bool PathClear(int x0, int y0, int x1, int y1)
        {
            float d = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, (int)(d / 2f));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                if (!_w.IsWalkable(x0 + (x1 - x0) * t + 0.5f, y0 + (y1 - y0) * t + 0.5f)) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- daily: wear the road, arrive

        // Called by CreatureSystem after a caravan moved.
        public void Walked(int id, long tick)
        {
            var e = _sim.Entities; // the road it wears is PathSystem's (CreatureSystem treads it)
            bool arrived = (e.TX[id] - e.X[id]) * (e.TX[id] - e.X[id]) + (e.TY[id] - e.Y[id]) * (e.TY[id] - e.Y[id]) < 0.09f;
            bool stuck = _sim.Creatures.Stuck(id); // no road there on foot
            if (arrived || stuck || tick - e.BirthTick[id] > 320) Arrive(id, tick, arrived);
        }

        void Arrive(int id, long tick, bool arrived)
        {
            int k = _caravans.FindIndex(c => c.Entity == id);
            _sim.Entities.Kill(id, DeathCause.Settled);
            if (k < 0) return;
            var c = _caravans[k];
            _caravans.RemoveAt(k);
            var to = _sim.Settlements.All[c.To];
            if (!arrived || !to.Alive) return; // the goods are lost on the road
            var origin = _sim.Settlements.All[c.From];
            _sim.Knowledge?.Carry(origin.X + 0.5f, origin.Y + 0.5f, to.X + 0.5f, to.Y + 0.5f); // and the news from where it came
            var m = MarketOf(to);
            switch (c.Good)
            {
                case Good.Food: to.Food += c.Amount; break;
                case Good.Herb: m.Herbs += c.Amount; break;
                default: m.Pills += c.Amount; break;
            }
            m.CaravansReceived++;
            Delivered++;
            var from = _sim.Settlements.All[c.From];
            // The first time a route carries real weight, people start calling it a thương lộ.
            if (m.CaravansReceived == 5 && from.Alive)
                _sim.Events.Add(tick, EventKind.Patronage, 1, $"Thương lộ {from.Name} – {to.Name} hình thành, thương đội qua lại tấp nập.", to.X + 0.5f, to.Y + 0.5f);
        }

        // Water or lava covered the rect: caravans caught there are lost.
        public void Flood(int x0, int y0, int x1, int y1)
        {
            var e = _sim.Entities;
            for (int k = _caravans.Count - 1; k >= 0; k--)
            {
                int id = _caravans[k].Entity;
                float x = e.X[id], y = e.Y[id];
                if (x < x0 || x > x1 + 1 || y < y0 || y > y1 + 1 || _w.IsWalkable(x, y)) continue;
                _caravans.RemoveAt(k);
                e.Kill(id, DeathCause.Natural);
            }
        }

        // A beast caught this caravan on the road (BeastChase): the people scatter, the goods are lost.
        public bool Maul(int entity)
        {
            int k = _caravans.FindIndex(c => c.Entity == entity);
            if (k < 0) return false;
            _caravans.RemoveAt(k);
            _sim.Entities.Kill(entity, DeathCause.Natural);
            return true;
        }

        // A blow from heaven or earth (HarmSystem): a caravan that would lose half its people or more scatters, goods lost.
        public int Harm(HarmSystem harm, float damage)
        {
            var e = _sim.Entities;
            int lost = 0;
            for (int k = _caravans.Count - 1; k >= 0; k--)
            {
                int id = _caravans[k].Entity;
                if (HarmSystem.CrowdShare(damage * harm.Reach(e.X[id], e.Y[id]), SpeciesInfo.Hp[(int)Species.Caravan]) < 0.5f) continue;
                _caravans.RemoveAt(k);
                e.Kill(id, DeathCause.Natural);
                lost++;
            }
            return lost;
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, _caravans.Count | ((long)Roads << 20) | (Delivered << 40));
            foreach (var m in _markets)
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(m.Herbs) | ((long)System.BitConverter.SingleToInt32Bits(m.Pills) << 32));
        }
    }
}
