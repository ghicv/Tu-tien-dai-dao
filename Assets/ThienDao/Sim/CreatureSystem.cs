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

        void RebuildBuckets()
        {
            if (_next.Length < _e.Species.Length) _next = new int[_e.Species.Length];
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            for (int id = 0; id < _e.Count; id++)
            {
                if (_e.Species[id] == Species.None) continue;
                int b = Bucket(_e.X[id], _e.Y[id]);
                _next[id] = _head[b];
                _head[b] = id;
            }
        }

        // Nearest living entity within radius whose species bit is in the mask, using this tick's buckets.
        public int FindNearest(float x, float y, float radius, int speciesMask)
        {
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
            RebuildBuckets();
            int n = _e.Count;
            for (int id = 0; id < n; id++)
            {
                var s = _e.Species[id];
                if (s == Species.None) continue;
                _e.PrevX[id] = _e.X[id];
                _e.PrevY[id] = _e.Y[id];
                if (s == Species.Migrants) TickMigrants(id, tick);
                else if (s == Species.Cultivator) Move(id, _e.Flying[id] ? SpeciesInfo.FlyingSpeed : SpeciesInfo.Speed[(int)s]);
                else if (s == Species.Beast) Move(id, SpeciesInfo.Speed[(int)s]);
                else if (s == Species.Caravan)
                {
                    Move(id, SpeciesInfo.Speed[(int)s]);
                    _sim.Trade.Walked(id, tick); // wears the road, delivers on arrival
                }
            }
        }

        public bool HasArrived(int id) => Arrived(id);

        void TickMigrants(int id, long tick)
        {
            Move(id, SpeciesInfo.Speed[(int)Species.Migrants]);
            bool stuck = _e.X[id] == _e.PrevX[id] && _e.Y[id] == _e.PrevY[id];
            if (Arrived(id) || stuck || tick - _e.BirthTick[id] > 240)
                _sim.Settlements.MigrantsArrived(id, tick);
        }

        bool Arrived(int id)
        {
            float dx = _e.TX[id] - _e.X[id], dy = _e.TY[id] - _e.Y[id];
            return dx * dx + dy * dy < 0.09f;
        }

        void Move(int id, float speed)
        {
            float x = _e.X[id], y = _e.Y[id];
            float dx = _e.TX[id] - x, dy = _e.TY[id] - y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 1e-4f) return;
            float nx, ny;
            if (d <= speed) { nx = _e.TX[id]; ny = _e.TY[id]; }
            else { nx = x + dx / d * speed; ny = y + dy / d * speed; }
            // Walkers stranded on water (the ground changed under them) may wade out.
            bool canPass = _e.Flying[id]
                ? _w.InBounds((int)nx, (int)ny) && nx >= 0f && ny >= 0f
                : _w.IsWalkable(nx, ny) || !_w.IsWalkable(x, y);
            if (canPass)
            {
                _e.X[id] = nx;
                _e.Y[id] = ny;
            }
            else
            {
                // Blocked by water or a peak: give up on this target.
                _e.TX[id] = x;
                _e.TY[id] = y;
            }
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
