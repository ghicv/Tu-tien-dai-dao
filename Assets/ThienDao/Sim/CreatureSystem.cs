using System;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    // Daily movement of the few individual creatures (migrant groups now; cultivators and awakened beasts later).
    // Ordinary wildlife lives in WildlifeSystem as region populations.
    public sealed class CreatureSystem
    {
        public const int BucketSize = 16;
        public const int AnyMask = ~1;

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly EntityStore _e;
        readonly int _bw, _bh;
        readonly int[] _head;
        int[] _next = new int[1024];
        bool _bucketsStale; // rebuilt on the first query of a tick, from where everyone stood when it began

        public CreatureSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _e = sim.Entities;
            _bw = _w.W / BucketSize;
            _bh = _w.H / BucketSize;
            _head = new int[_bw * _bh];
            RebuildBuckets();
        }

        int Bucket(float x, float y)
        {
            int bx = Mathf.Clamp((int)x / BucketSize, 0, _bw - 1), by = Mathf.Clamp((int)y / BucketSize, 0, _bh - 1);
            return by * _bw + bx;
        }

        // Buckets hold where entities stood at the start of the tick (PrevX/PrevY once Tick has run), exactly as
        // when they were rebuilt eagerly every day; most days nobody queries, so they are rebuilt only on demand.
        void RebuildBuckets(bool fromPrev = false)
        {
            if (_next.Length < _e.Species.Length) _next = new int[_e.Species.Length];
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            for (int id = 0; id < _e.Count; id++)
            {
                if (_e.Species[id] == Species.None) continue;
                int b = fromPrev ? Bucket(_e.PrevX[id], _e.PrevY[id]) : Bucket(_e.X[id], _e.Y[id]);
                _next[id] = _head[b];
                _head[b] = id;
            }
            _bucketsStale = false;
        }

        // Nearest living entity within radius whose species bit is in the mask, using this tick's buckets.
        public int FindNearest(float x, float y, float radius, int speciesMask)
        {
            if (_bucketsStale) RebuildBuckets(true);
            int best = -1;
            float bestD = radius * radius;
            int r = Mathf.CeilToInt(radius / BucketSize);
            int bx0 = Mathf.Clamp((int)x / BucketSize - r, 0, _bw - 1), bx1 = Mathf.Clamp((int)x / BucketSize + r, 0, _bw - 1);
            int by0 = Mathf.Clamp((int)y / BucketSize - r, 0, _bh - 1), by1 = Mathf.Clamp((int)y / BucketSize + r, 0, _bh - 1);
            for (int by = by0; by <= by1; by++)
            for (int bx = bx0; bx <= bx1; bx++)
            for (int id = _head[by * _bw + bx]; id >= 0; id = _next[id])
            {
                if (!_e.IsAlive(id) || (speciesMask & (1 << (int)_e.Species[id])) == 0) continue;
                float dx = _e.X[id] - x, dy = _e.Y[id] - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = id; }
            }
            return best;
        }

        public void Tick(long tick)
        {
            _bucketsStale = true;
            int n = _e.Count;
            for (int id = 0; id < n; id++)
            {
                var s = _e.Species[id];
                if (s == Species.None) continue;
                _e.PrevX[id] = _e.X[id];
                _e.PrevY[id] = _e.Y[id];
                if (s == Species.Migrants) TickMigrants(id, tick);
                else if (s == Species.Cultivator)
                {
                    if (_e.Flying[id]) Fly(id, FlightSpeed(id));
                    else Walk(id, SpeciesInfo.Speed[(int)s], tick);
                }
                else if (s == Species.Beast)
                {
                    // Great beasts stride faster; điêu, giao long and huyết bức fly.
                    var beast = _sim.Beasts.ForEntity(id);
                    int grade = beast != null ? beast.Grade : 1;
                    if (_e.Flying[id]) Fly(id, 6f + grade);
                    else Walk(id, SpeciesInfo.Speed[(int)s] * (1f + 0.08f * grade), tick);
                }
                else if (s == Species.Caravan)
                {
                    Walk(id, SpeciesInfo.Speed[(int)s], tick);
                    _sim.Trade.Walked(id, tick); // wears the road, delivers on arrival
                }
            }
        }

        // Ngự kiếm phi hành: the higher the realm, the faster the sword (Trúc Cơ 10 … Hóa Thần 30 cells a day).
        static readonly float[] FlightByRealm = { 10f, 10f, 10f, 14f, 20f, 30f, 30f };

        float FlightSpeed(int id)
        {
            int idx = _e.Payload[id];
            var all = _sim.Cultivation.All;
            return idx >= 0 && idx < all.Count ? FlightByRealm[(int)all[idx].Realm] : SpeciesInfo.FlyingSpeed;
        }

        // A walker gave up: its route found no way to the target (an island, a sealed valley).
        public bool Stuck(int id) => _sim.Nav.Failed(id);

        public bool HasArrived(int id) => Arrived(id);

        void TickMigrants(int id, long tick)
        {
            Walk(id, SpeciesInfo.Speed[(int)Species.Migrants], tick);
            if (Arrived(id) || Stuck(id) || tick - _e.BirthTick[id] > 360)
                _sim.Settlements.MigrantsArrived(id, tick);
        }

        bool Arrived(int id)
        {
            float dx = _e.TX[id] - _e.X[id], dy = _e.TY[id] - _e.Y[id];
            return dx * dx + dy * dy < 0.09f;
        }

        // Flyers go straight over anything, sea and peaks included.
        void Fly(int id, float speed)
        {
            float x = _e.X[id], y = _e.Y[id];
            float dx = _e.TX[id] - x, dy = _e.TY[id] - y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 1e-4f) return;
            float nx = d <= speed ? _e.TX[id] : x + dx / d * speed, ny = d <= speed ? _e.TY[id] : y + dy / d * speed;
            if (!_w.InBounds((int)nx, (int)ny) || nx < 0f || ny < 0f) return;
            _e.X[id] = nx;
            _e.Y[id] = ny;
        }

        static readonly float[] Detours = { 0.5f, -0.5f, 1f, -1f, 1.57f, -1.57f };

        // Walkers follow their route around sea, peaks and lava, at the pace the ground allows (a road is fast,
        // a swamp or a mountain slow); a step that runs into something tries to sidestep before replanning.
        void Walk(int id, float pace, long tick)
        {
            float x = _e.X[id], y = _e.Y[id];
            if (!_w.IsWalkable(x, y))
            {
                // Stranded on water or lava (the ground changed under them): wade to the nearest dry cell.
                for (int r = 1; r <= 4; r++)
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oy)) != r || !_w.IsWalkable(x + ox, y + oy)) continue;
                    float ddx = ox, ddy = oy, dd = Mathf.Sqrt(ddx * ddx + ddy * ddy), ss = Mathf.Min(pace * 0.5f, dd);
                    _e.X[id] = x + ddx / dd * ss;
                    _e.Y[id] = y + ddy / dd * ss;
                    return;
                }
                return;
            }
            float tdx = _e.TX[id] - x, tdy = _e.TY[id] - y;
            if (tdx * tdx + tdy * tdy < 1e-6f) return;
            var step = _sim.Nav.Steer(id, _e, tick, out float wx, out float wy);
            if (step == NavSystem.Step.Wait) return;
            if (step == NavSystem.Step.Failed)
            {
                // No way there on foot: give up on this target.
                _e.TX[id] = x;
                _e.TY[id] = y;
                return;
            }
            float speed = pace * Mathf.Max(0.2f, _sim.Nav.SpeedAt(x, y));
            float dx = wx - x, dy = wy - y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 1e-4f) return;
            float s = Mathf.Min(speed, d);
            float nx = x + dx / d * s, ny = y + dy / d * s;
            if (_w.IsWalkable(nx, ny))
            {
                _e.X[id] = nx;
                _e.Y[id] = ny;
                return;
            }
            float a = Mathf.Atan2(dy, dx);
            foreach (float turn in Detours)
            {
                float sx = x + Mathf.Cos(a + turn) * s, sy = y + Mathf.Sin(a + turn) * s;
                if (!_w.IsWalkable(sx, sy)) continue;
                _e.X[id] = sx;
                _e.Y[id] = sy;
                return;
            }
            _sim.Nav.Replan(id);
        }

        public void HashInto(ref ulong h)
        {
            for (int id = 0; id < _e.Count; id++)
            {
                if (_e.Species[id] == Species.None) continue;
                StateHash.Add(ref h, id | ((int)_e.Species[id] << 24));
                StateHash.Add(ref h, BitConverter.SingleToInt32Bits(_e.X[id]) | ((long)BitConverter.SingleToInt32Bits(_e.Y[id]) << 32));
                StateHash.Add(ref h, _e.BirthTick[id]);
            }
        }
    }
}
