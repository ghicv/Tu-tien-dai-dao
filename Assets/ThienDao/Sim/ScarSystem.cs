using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // Vết tích trên mặt đất: the marks calamities, fires and wars leave (WorldData.Scar), and their slow healing.
    // Only the look of the land: nothing else in the simulation reads a scar, so marking one never moves history.
    public sealed class ScarSystem
    {
        const int Block = 32;     // healing is staggered by 32×32 blocks, so one year never repaints the whole map
        const int Stagger = 7;

        readonly WorldData _w;
        readonly int _bw, _bh;
        readonly int[] _blockMarks; // cells carrying a scar, per block

        public int MarkedCells { get; private set; }

        public ScarSystem(Simulation sim)
        {
            _w = sim.World;
            _bw = (_w.W + Block - 1) / Block;
            _bh = (_w.H + Block - 1) / Block;
            _blockMarks = new int[_bw * _bh];
        }

        public ScarKind KindAt(int i) => ScarInfo.Kind(_w.Scar[i]);
        public int StrengthAt(int i) => ScarInfo.Strength(_w.Scar[i]);

        // ---------------------------------------------------------------- marking

        // A ragged disc: `centre` strength in the middle falling to `edge` at radius r; the rim is eaten by noise.
        public void Disc(int cx, int cy, float r, ScarKind kind, int centre, int edge, uint salt)
        {
            int ir = Mathf.CeilToInt(r);
            int x0 = Mathf.Max(0, cx - ir), y0 = Mathf.Max(0, cy - ir);
            int x1 = Mathf.Min(_w.W - 1, cx + ir), y1 = Mathf.Min(_w.H - 1, cy + ir);
            if (x1 < x0 || y1 < y0) return;
            uint seed = _w.Seed ^ 0x5CA4u ^ salt;
            bool any = false;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float reach = r * (0.7f + 0.3f * Hash.Float01(seed, x >> 1, y >> 1));
                if (d > reach) continue;
                int s = Mathf.RoundToInt(Mathf.Lerp(centre, edge, r > 0f ? d / r : 0f));
                any |= Set(_w.Idx(x, y), kind, s);
            }
            if (any) _w.NotifyLookChanged(x0, y0, x1, y1);
        }

        // A crack or a trail wandering from (x, y): earthquake fissures, a storm's track.
        public void Trail(float x, float y, float angle, int length, float wobble, ScarKind kind, int strength, ref DetRandom rng)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int step = 0; step < length; step++)
            {
                angle += rng.Range(-wobble, wobble);
                x += Mathf.Cos(angle);
                y += Mathf.Sin(angle);
                int ix = (int)x, iy = (int)y;
                if (!_w.InBounds(ix, iy)) break;
                int s = strength - step * strength / (length * 2); // the crack narrows toward its end
                if (!Set(_w.Idx(ix, iy), kind, s)) continue;
                if (rng.NextFloat() < 0.35f)
                {
                    int sx = ix + (rng.NextFloat() < 0.5f ? 1 : -1);
                    if (_w.InBounds(sx, iy)) Set(_w.Idx(sx, iy), kind, s - 3);
                }
                x0 = Mathf.Min(x0, ix - 1);
                y0 = Mathf.Min(y0, iy);
                x1 = Mathf.Max(x1, ix + 1);
                y1 = Mathf.Max(y1, iy);
            }
            if (x1 >= x0) _w.NotifyLookChanged(Mathf.Max(0, x0), y0, Mathf.Min(_w.W - 1, x1), y1);
        }

        // One cell; the caller notifies. A stronger mark wins; the same kind keeps the deeper of the two.
        public bool Set(int i, ScarKind kind, int strength)
        {
            if (strength <= 0) return false;
            var t = _w.Terrain[i];
            if (!Suits(kind, t)) return false;
            byte old = _w.Scar[i];
            int oldStrength = ScarInfo.Strength(old);
            if (old != 0 && strength < oldStrength) return false;
            byte now = ScarInfo.Pack(kind, strength);
            if (now == old) return false;
            _w.Scar[i] = now;
            if (old == 0) Count(i, 1);
            return true;
        }

        // The sea keeps no scars and lava is its own mark; drought cracks and trampling only show where things grow.
        static bool Suits(ScarKind kind, Terrain t)
        {
            if (!TerrainInfo.IsLand(t) || t == Terrain.Lava) return false;
            switch (kind)
            {
                case ScarKind.Parched:
                case ScarKind.Trampled: return TerrainInfo.IsVegetated(t) || t == Terrain.Farmland;
                case ScarKind.Silt: return !TerrainInfo.IsHighland(t) && t != Terrain.Snow;
                case ScarKind.Scorch:
                case ScarKind.Battlefield: return t != Terrain.Snow && t != Terrain.Peak;
                default: return true;
            }
        }

        public void SetAndNotify(int i, ScarKind kind, int strength)
        {
            if (Set(i, kind, strength)) _w.NotifyLookChanged(i % _w.W, i / _w.W, i % _w.W, i / _w.W);
        }

        // Thiên Đạo reshapes a cell: whatever mark it carried is gone. The caller repaints.
        public void Clear(int i)
        {
            if (_w.Scar[i] == 0) return;
            _w.Scar[i] = 0;
            Count(i, -1);
        }

        public void Erase(int x0, int y0, int x1, int y1)
        {
            bool any = false;
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(_w.H - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_w.W - 1, x1); x++)
            {
                int i = _w.Idx(x, y);
                if (_w.Scar[i] == 0) continue;
                _w.Scar[i] = 0;
                Count(i, -1);
                any = true;
            }
            if (any) _w.NotifyLookChanged(Mathf.Max(0, x0), Mathf.Max(0, y0), Mathf.Min(_w.W - 1, x1), Mathf.Min(_w.H - 1, y1));
        }

        void Count(int i, int d)
        {
            _blockMarks[(i / _w.W / Block) * _bw + (i % _w.W) / Block] += d;
            MarkedCells += d;
        }

        // ---------------------------------------------------------------- healing

        // Each year a block's marks lose a step if their kind is due; the faint rims vanish first.
        public void YearlyStep(long tick)
        {
            if (MarkedCells == 0) return;
            int year = (int)(tick / SimClock.DaysPerYear);
            var scar = _w.Scar;
            for (int by = 0; by < _bh; by++)
            for (int bx = 0; bx < _bw; bx++)
            {
                int b = by * _bw + bx;
                if (_blockMarks[b] == 0) continue;
                int phase = year + b * Stagger;
                int x0 = bx * Block, y0 = by * Block;
                int x1 = Mathf.Min(_w.W, x0 + Block), y1 = Mathf.Min(_w.H, y0 + Block);
                bool changed = false;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = y * _w.W + x;
                    byte s = scar[i];
                    if (s == 0) continue;
                    var kind = ScarInfo.Kind(s);
                    if (phase % ScarInfo.YearsPerStep[(int)kind] != 0) continue;
                    scar[i] = ScarInfo.Pack(kind, ScarInfo.Strength(s) - 1);
                    if (scar[i] == 0) { _blockMarks[b]--; MarkedCells--; }
                    changed = true;
                }
                if (changed) _w.NotifyLookChanged(x0, y0, x1 - 1, y1 - 1);
            }
        }
    }
}
