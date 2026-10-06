using System;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // Ordinary animals as populations per 64×64 region: they graze the forage grid, breed in Xuân/Hạ,
    // wolves hunt with diminishing returns, a share wanders into neighbouring regions, villages hunt a little.
    // The map shows only a few decorative tokens per region (see UnitRenderer).
    public sealed class WildlifeSystem
    {
        public const int Region = 64;
        public static readonly Species[] Kinds = { Species.Deer, Species.Rabbit, Species.Wolf };

        // Index by kind (0 deer, 1 rabbit, 2 wolf).
        static readonly float[] ForagePerMonth = { 90f, 36f, 0f };
        static readonly float[] BirthRate = { 0.08f, 0.25f, 0.08f };    // per breeding-season month, when fed
        static readonly float[] DeathRate = { 0.008f, 0.03f, 0.012f }; // natural, per month
        static readonly float[] StarveRate = { 0.25f, 0.35f, 0.12f };   // extra monthly death at zero food
        const float Wander = 0.04f;              // share of each population that drifts to neighbours monthly
        const float KillsPerWolf = 3f;           // per month when prey is plentiful
        const float KillsWolfNeeds = 2f;
        const float PreyHalfSaturation = 40f;

        readonly WorldData _w;
        readonly ForageSystem _forage;
        public readonly int RW, RH;
        float[][] _pop;
        float[][] _next;
        readonly bool[] _land;
        readonly float[] _deerShare; // share of a region's forage that is deer browse (woodland) vs rabbit grazing (open grass)
        readonly int[] _landCells;   // walkable cells per region, to know how much a flood took

        public WildlifeSystem(WorldData w, ForageSystem forage)
        {
            _w = w;
            _forage = forage;
            RW = w.W / Region;
            RH = w.H / Region;
            int n = RW * RH;
            _pop = new float[Kinds.Length][];
            _next = new float[Kinds.Length][];
            for (int k = 0; k < Kinds.Length; k++)
            {
                _pop[k] = new float[n];
                _next[k] = new float[n];
            }
            _land = new bool[n];
            _deerShare = new float[n];
            _landCells = new int[n];
            for (int r = 0; r < n; r++) _landCells[r] = CountLand(r);
            SeedInitial();
        }

        static int KindIndex(Species s) => s == Species.Deer ? 0 : s == Species.Rabbit ? 1 : s == Species.Wolf ? 2 : -1;

        static float Habitat(int kind, Terrain t)
        {
            switch (kind)
            {
                case 0:
                    return t == Terrain.Forest ? 0.8f : t == Terrain.Grass ? 0.5f : t == Terrain.Jungle || t == Terrain.Hills ? 0.4f :
                        t == Terrain.Tundra || t == Terrain.Savanna ? 0.3f : 0f;
                case 1:
                    return t == Terrain.Grass ? 0.8f : t == Terrain.Savanna ? 0.6f :
                        t == Terrain.Forest || t == Terrain.Tundra || t == Terrain.Hills ? 0.3f : t == Terrain.Beach ? 0.1f : 0f;
                default:
                    return t == Terrain.Tundra ? 0.4f : t == Terrain.Forest || t == Terrain.Hills ? 0.3f :
                        t == Terrain.Snow ? 0.2f : t == Terrain.Grass ? 0.1f : 0f;
            }
        }

        int RegionBlock0(int r, out int by0)
        {
            by0 = r / RW * (Region / ForageSystem.Block);
            return r % RW * (Region / ForageSystem.Block);
        }

        void SeedInitial()
        {
            const int blocks = Region / ForageSystem.Block;
            for (int r = 0; r < RW * RH; r++)
            {
                int rx = r % RW, ry = r / RW;
                var hab = new float[Kinds.Length];
                int samples = 0;
                for (int y = ry * Region; y < (ry + 1) * Region; y += 4)
                for (int x = rx * Region; x < (rx + 1) * Region; x += 4)
                {
                    var t = _w.Terrain[_w.Idx(x, y)];
                    for (int k = 0; k < Kinds.Length; k++) hab[k] += Habitat(k, t);
                    samples++;
                }
                for (int k = 0; k < Kinds.Length; k++) hab[k] /= samples;
                int bx0 = RegionBlock0(r, out int by0);
                float cap = _forage.CapSum(bx0, by0, blocks);
                _land[r] = cap > 0f;
                // Start at roughly a third of what average regrowth (~20%/month) could feed, split by habitat.
                float feedable = cap * 0.2f * 0.35f;
                float share = hab[0] + hab[1];
                _deerShare[r] = share > 0f ? hab[0] / share : 0.5f;
                if (share > 0f)
                {
                    _pop[0][r] = feedable * _deerShare[r] / ForagePerMonth[0];
                    _pop[1][r] = feedable * (1f - _deerShare[r]) / ForagePerMonth[1];
                }
                _pop[2][r] = (_pop[0][r] + _pop[1][r]) / 80f * Mathf.Clamp01(hab[2] * 3f);
            }
        }

        public float Total(Species s)
        {
            int k = KindIndex(s);
            if (k < 0) return 0f;
            double sum = 0;
            foreach (float p in _pop[k]) sum += p;
            return (float)sum;
        }

        public int RegionOf(float x, float y) =>
            Mathf.Clamp((int)y / Region, 0, RH - 1) * RW + Mathf.Clamp((int)x / Region, 0, RW - 1);

        public float At(Species s, int region)
        {
            int k = KindIndex(s);
            return k < 0 ? 0f : _pop[k][region];
        }

        public void Add(Species s, float x, float y, float amount)
        {
            int k = KindIndex(s);
            if (k < 0 || !_w.IsWalkable(x, y)) return; // animals dropped into the sea are lost
            _pop[k][RegionOf(x, y)] += amount;
        }

        // Keeps only a share of one kind in a region (a beast tide beaten back, …).
        public void Cull(Species s, int region, float keep)
        {
            int k = KindIndex(s);
            if (k >= 0) _pop[k][region] *= Mathf.Clamp01(keep);
        }

        // Call after any terrain edit: animals on land that became water drown (regions lose the flooded share).
        public void LandChanged(int x0, int y0, int x1, int y1)
        {
            int rx0 = Mathf.Clamp(x0 / Region, 0, RW - 1), rx1 = Mathf.Clamp(x1 / Region, 0, RW - 1);
            int ry0 = Mathf.Clamp(y0 / Region, 0, RH - 1), ry1 = Mathf.Clamp(y1 / Region, 0, RH - 1);
            for (int ry = ry0; ry <= ry1; ry++)
            for (int rx = rx0; rx <= rx1; rx++)
            {
                int r = ry * RW + rx;
                int before = _landCells[r];
                int after = CountLand(r);
                _landCells[r] = after;
                if (before <= 0 || after >= before) continue;
                float keep = after / (float)before;
                for (int k = 0; k < Kinds.Length; k++) _pop[k][r] *= keep;
            }
        }

        int CountLand(int r)
        {
            int rx = r % RW, ry = r / RW, n = 0;
            for (int y = ry * Region; y < (ry + 1) * Region; y++)
            for (int x = rx * Region; x < (rx + 1) * Region; x++)
                if (TerrainInfo.IsWalkable(_w.Terrain[y * _w.W + x])) n++;
            return n;
        }

        // Villagers take a small share of the local game; returns food in person-months.
        public float Hunt(int x, int y)
        {
            int r = RegionOf(x, y);
            float deer = Mathf.Min(0.2f, _pop[0][r] * 0.01f); // deer breed slowly; dozens of villages would hunt them out
            float rabbits = Mathf.Min(1f, _pop[1][r] * 0.03f);
            _pop[0][r] -= deer;
            _pop[1][r] -= rabbits;
            return deer * SpeciesInfo.HuntFood[(int)Species.Deer] + rabbits * SpeciesInfo.HuntFood[(int)Species.Rabbit];
        }

        public void MonthlyStep(Season season, float birthScale = 1f)
        {
            const int blocks = Region / ForageSystem.Block;
            bool breeding = season == Season.Xuan || season == Season.Ha;
            int n = RW * RH;
            for (int r = 0; r < n; r++)
            {
                // Land can be flooded or raised by Thiên Đạo; a region with no forage left stops taking in wanderers.
                int lbx = RegionBlock0(r, out int lby);
                _land[r] = _forage.CapSum(lbx, lby, blocks) > 0f;
                float deer = _pop[0][r], rabbits = _pop[1][r], wolves = _pop[2][r];

                // Deer browse and rabbits graze different parts of the same forage, so they compete only partly.
                float needDeer = deer * ForagePerMonth[0], needRabbit = rabbits * ForagePerMonth[1];
                int bx0 = RegionBlock0(r, out int by0);
                float fedDeer = 1f, fedRabbit = 1f;
                if (needDeer + needRabbit > 0f)
                {
                    float available = _forage.Sum(bx0, by0, blocks);
                    float eatDeer = Mathf.Min(available * _deerShare[r], needDeer);
                    float eatRabbit = Mathf.Min(available * (1f - _deerShare[r]), needRabbit);
                    if (available > 0f) _forage.Scale(bx0, by0, blocks, 1f - (eatDeer + eatRabbit) / available);
                    if (needDeer > 0f) fedDeer = eatDeer / needDeer;
                    if (needRabbit > 0f) fedRabbit = eatRabbit / needRabbit;
                }
                deer *= 1f - DeathRate[0] - StarveRate[0] * (1f - fedDeer);
                rabbits *= 1f - DeathRate[1] - StarveRate[1] * (1f - fedRabbit);
                if (breeding)
                {
                    deer += deer * BirthRate[0] * fedDeer * birthScale;
                    rabbits += rabbits * BirthRate[1] * fedRabbit * birthScale;
                }

                float prey = deer + rabbits;
                float kills = prey > 0f ? Mathf.Min(prey * 0.5f, wolves * KillsPerWolf * prey / (prey + PreyHalfSaturation)) : 0f;
                if (prey > 0f)
                {
                    deer -= kills * deer / prey;
                    rabbits -= kills * rabbits / prey;
                }
                float wolfFed = wolves > 0f ? Mathf.Clamp01(kills / (wolves * KillsWolfNeeds)) : 0f;
                wolves *= 1f - DeathRate[2] - StarveRate[2] * (1f - wolfFed);
                if (breeding) wolves += wolves * BirthRate[2] * wolfFed * birthScale;

                _pop[0][r] = deer < 0.3f ? 0f : deer;
                _pop[1][r] = rabbits < 0.3f ? 0f : rabbits;
                _pop[2][r] = wolves < 0.1f ? 0f : wolves;
            }
            Diffuse();
        }

        void Diffuse()
        {
            int n = RW * RH;
            for (int k = 0; k < Kinds.Length; k++)
            {
                var src = _pop[k];
                var dst = _next[k];
                Array.Copy(src, dst, n);
                for (int r = 0; r < n; r++)
                {
                    if (src[r] <= 0f) continue;
                    int rx = r % RW, ry = r / RW;
                    int l = rx > 0 && _land[r - 1] ? r - 1 : -1;
                    int rr = rx < RW - 1 && _land[r + 1] ? r + 1 : -1;
                    int d = ry > 0 && _land[r - RW] ? r - RW : -1;
                    int u = ry < RH - 1 && _land[r + RW] ? r + RW : -1;
                    float part = src[r] * Wander * 0.25f;
                    if (l >= 0) { dst[l] += part; dst[r] -= part; }
                    if (rr >= 0) { dst[rr] += part; dst[r] -= part; }
                    if (d >= 0) { dst[d] += part; dst[r] -= part; }
                    if (u >= 0) { dst[u] += part; dst[r] -= part; }
                }
                _next[k] = src;
                _pop[k] = dst;
            }
        }

        public void HashInto(ref ulong h)
        {
            for (int k = 0; k < Kinds.Length; k++)
                foreach (float p in _pop[k]) StateHash.Add(ref h, BitConverter.SingleToInt32Bits(p));
        }
    }
}
