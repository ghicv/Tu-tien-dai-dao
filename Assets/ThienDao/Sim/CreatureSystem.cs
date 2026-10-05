using System;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // Daily behaviour of every individual creature: animals graze, hunt, breed and die; migrant groups travel.
    public sealed class CreatureSystem
    {
        public const int BucketSize = 16;
        public const int MaxAnimals = 12000;
        const int MaxCandidates = 48; // herds can pile into one bucket; bound per-query work

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

        public int AnimalCount =>
            _e.AliveBySpecies[(int)Species.Deer] + _e.AliveBySpecies[(int)Species.Rabbit] + _e.AliveBySpecies[(int)Species.Wolf];

        // ---------------------------------------------------------------- spawning

        static float Habitat(Species s, Terrain t)
        {
            switch (s)
            {
                case Species.Deer:
                    return t == Terrain.Forest ? 0.8f : t == Terrain.Grass ? 0.5f : t == Terrain.Jungle || t == Terrain.Hills ? 0.4f :
                        t == Terrain.Tundra || t == Terrain.Savanna ? 0.3f : 0f;
                case Species.Rabbit:
                    return t == Terrain.Grass ? 0.8f : t == Terrain.Savanna ? 0.6f :
                        t == Terrain.Forest || t == Terrain.Tundra || t == Terrain.Hills ? 0.3f : t == Terrain.Beach ? 0.1f : 0f;
                case Species.Wolf:
                    return t == Terrain.Tundra ? 0.4f : t == Terrain.Forest || t == Terrain.Hills ? 0.3f :
                        t == Terrain.Snow ? 0.2f : t == Terrain.Grass ? 0.1f : 0f;
                default: return 0f;
            }
        }

        public void SpawnInitial()
        {
            var rng = new DetRandom(_w.Seed ^ 0xA11Au);
            var target = new int[(int)Species.Count];
            target[(int)Species.Deer] = 2200;
            target[(int)Species.Rabbit] = 3200;
            target[(int)Species.Wolf] = 260;
            var have = new int[(int)Species.Count];
            Species[] order = { Species.Deer, Species.Rabbit, Species.Wolf };

            for (int attempt = 0; attempt < 200000; attempt++)
            {
                var s = order[attempt % order.Length];
                if (have[(int)s] >= target[(int)s])
                {
                    if (have[1] >= target[1] && have[2] >= target[2] && have[3] >= target[3]) break;
                    continue;
                }
                int x = rng.Range(0, _w.W), y = rng.Range(0, _w.H);
                if (rng.NextFloat() >= Habitat(s, _w.Terrain[_w.Idx(x, y)])) continue;
                int life = Lifespan(s, ref rng);
                int id = _e.Spawn(s, x + 0.5f, y + 0.5f, -rng.Range(0, (int)(life * 0.6f)), life);
                _e.Hunger[id] = rng.Range(0f, 40f);
                have[(int)s]++;
            }
            RebuildBuckets();
        }

        static int Lifespan(Species s, ref DetRandom rng) => (int)(SpeciesInfo.LifespanDays[(int)s] * rng.Range(0.75f, 1.25f));

        public int SpawnAnimal(Species s, float x, float y, long tick, ref DetRandom rng)
        {
            if (AnimalCount >= MaxAnimals || !_w.IsWalkable(x, y)) return -1;
            int id = _e.Spawn(s, x, y, tick, Lifespan(s, ref rng));
            _e.Hunger[id] = 20f;
            return id;
        }

        // ---------------------------------------------------------------- spatial buckets

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

        public const int AnyMask = ~1;
        public const int HerbivoreMask = (1 << (int)Species.Deer) | (1 << (int)Species.Rabbit);

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
            {
                int seen = 0;
                for (int id = _head[by * _bw + bx]; id >= 0 && seen < MaxCandidates; id = _next[id])
                {
                    if (!_e.IsAlive(id) || (speciesMask & (1 << (int)_e.Species[id])) == 0) continue;
                    seen++;
                    float dx = _e.X[id] - x, dy = _e.Y[id] - y, d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = id; }
                }
            }
            return best;
        }

        int SameSpeciesInBucket(float x, float y, Species s, int cap)
        {
            int n = 0;
            for (int id = _head[Bucket(x, y)]; id >= 0 && n < cap; id = _next[id])
                if (_e.Species[id] == s) n++;
            return n;
        }

        // True when the bucket already holds more than `limit` of this species, counting the parent itself.
        bool BucketCrowded(float x, float y, Species s, int limit)
        {
            int n = 0;
            for (int id = _head[Bucket(x, y)]; id >= 0; id = _next[id])
                if (_e.Species[id] == s && ++n > limit) return true;
            return false;
        }

        // Villagers hunt herbivores near their village; returns the food gained.
        public float Hunt(int x, int y, int radius, int maxKills)
        {
            float food = 0f;
            int kills = 0;
            int r = Mathf.CeilToInt(radius / (float)BucketSize);
            int cbx = x / BucketSize, cby = y / BucketSize;
            for (int by = Mathf.Max(0, cby - r); by <= Mathf.Min(_bh - 1, cby + r) && kills < maxKills; by++)
            for (int bx = Mathf.Max(0, cbx - r); bx <= Mathf.Min(_bw - 1, cbx + r) && kills < maxKills; bx++)
            for (int id = _head[by * _bw + bx]; id >= 0 && kills < maxKills; id = _next[id])
            {
                if (!_e.IsAlive(id) || !SpeciesInfo.IsHerbivore(_e.Species[id])) continue;
                float dx = _e.X[id] - x, dy = _e.Y[id] - y;
                if (dx * dx + dy * dy > radius * radius) continue;
                food += SpeciesInfo.HuntFood[(int)_e.Species[id]];
                _e.Kill(id, DeathCause.Hunted);
                kills++;
            }
            return food;
        }

        // ---------------------------------------------------------------- daily tick

        public void Tick(long tick)
        {
            RebuildBuckets();
            var rng = new DetRandom(_w.Seed ^ Hash.U32((uint)tick * 2654435761u));
            int n = _e.Count;
            for (int id = 0; id < n; id++)
            {
                var s = _e.Species[id];
                if (s == Species.None) continue;
                _e.PrevX[id] = _e.X[id];
                _e.PrevY[id] = _e.Y[id];

                if (s == Species.Migrants)
                {
                    TickMigrants(id, tick);
                    continue;
                }

                long age = tick - _e.BirthTick[id];
                if (age >= _e.Lifespan[id]) { _e.Kill(id, DeathCause.Age); continue; }
                _e.Hunger[id] += SpeciesInfo.HungerPerDay[(int)s];
                if (_e.Hunger[id] >= 100f) { _e.Kill(id, DeathCause.Starvation); continue; }

                float speed = SpeciesInfo.Speed[(int)s];
                if (s == Species.Wolf) speed *= TickWolf(id, ref rng) ? 1.4f : 1f;
                else TickHerbivore(id, s, ref rng);

                Move(id, speed);

                if ((tick + id) % SimClock.DaysPerMonth == 0) TryBreed(id, s, age, tick, ref rng);
            }
        }

        void TickHerbivore(int id, Species s, ref DetRandom rng)
        {
            float x = _e.X[id], y = _e.Y[id];
            if (_e.Hunger[id] >= 25f) _e.Hunger[id] -= _sim.Forage.Eat(x, y, _e.Hunger[id]);
            if (!Arrived(id)) return;

            if (_e.Hunger[id] >= 25f)
            {
                // Head for the richest of a few nearby samples, looking further afield when starving.
                float reach = _e.Hunger[id] > 50f ? 16f : 7f;
                float bestF = -1f, bx = x, by = y;
                for (int k = 0; k < 5; k++)
                {
                    float tx = x + rng.Range(-reach, reach), ty = y + rng.Range(-reach, reach);
                    if (!_w.IsWalkable(tx, ty)) continue;
                    float f = _sim.Forage.At(tx, ty);
                    if (f > bestF) { bestF = f; bx = tx; by = ty; }
                }
                SetTarget(id, bx, by);
            }
            else
            {
                SetTarget(id, x + rng.Range(-4f, 4f), y + rng.Range(-4f, 4f));
            }
        }

        // Returns true while chasing prey.
        bool TickWolf(int id, ref DetRandom rng)
        {
            float x = _e.X[id], y = _e.Y[id];
            if (_e.Hunger[id] >= 25f)
            {
                int prey = _e.Prey[id];
                if (!_e.IsAlive(prey) || !SpeciesInfo.IsHerbivore(_e.Species[prey]))
                    prey = _e.Prey[id] = FindNearest(x, y, 14f, HerbivoreMask);
                if (prey >= 0)
                {
                    float dx = _e.X[prey] - x, dy = _e.Y[prey] - y;
                    if (dx * dx + dy * dy <= 0.8f * 0.8f)
                    {
                        var ps = _e.Species[prey];
                        _e.Prey[id] = -1;
                        // Sparse prey is wary and hard to corner: this feedback keeps predators from hunting it out.
                        float density = Mathf.Clamp(SameSpeciesInBucket(_e.X[prey], _e.Y[prey], ps, 8) / 3f, 0.35f, 1.3f);
                        if (rng.NextFloat() < SpeciesInfo.CatchChance[(int)ps] * density)
                        {
                            _e.Hunger[id] = Mathf.Max(0f, _e.Hunger[id] - SpeciesInfo.MeatValue[(int)ps]);
                            _e.Kill(prey, DeathCause.Predation);
                        }
                        else
                        {
                            SetTarget(id, x + rng.Range(-6f, 6f), y + rng.Range(-6f, 6f)); // the prey got away
                        }
                        return false;
                    }
                    SetTarget(id, _e.X[prey], _e.Y[prey]);
                    return true;
                }
            }
            if (Arrived(id)) SetTarget(id, x + rng.Range(-6f, 6f), y + rng.Range(-6f, 6f));
            return false;
        }

        void TryBreed(int id, Species s, long age, long tick, ref DetRandom rng)
        {
            if (age < SpeciesInfo.AdultDays[(int)s] || _e.Hunger[id] >= 25f) return;
            if (rng.NextFloat() >= SpeciesInfo.BirthChancePerMonth[(int)s]) return;
            if (BucketCrowded(_e.X[id], _e.Y[id], s, SpeciesInfo.CrowdLimit[(int)s])) return;
            int litter = rng.Range(1, SpeciesInfo.MaxLitter[(int)s] + 1);
            for (int k = 0; k < litter; k++)
                SpawnAnimal(s, _e.X[id] + rng.Range(-1f, 1f), _e.Y[id] + rng.Range(-1f, 1f), tick, ref rng);
        }

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

        void SetTarget(int id, float x, float y)
        {
            if (!_w.IsWalkable(x, y)) return;
            _e.TX[id] = x;
            _e.TY[id] = y;
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
            if (_w.IsWalkable(nx, ny))
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
                StateHash.Add(ref h, BitConverter.SingleToInt32Bits(_e.Hunger[id]));
                StateHash.Add(ref h, _e.BirthTick[id]);
            }
        }
    }
}
