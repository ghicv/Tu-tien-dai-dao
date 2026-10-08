using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // Đường mòn, đường đất (devlog 31): people make roads by walking. Every cell a mortal walker steps into (a
    // migrant crowd, a caravan's carts, a Luyện Khí on foot) is trodden a little; trodden often enough it becomes
    // a trail (đường mòn: the grass worn through), and a trail kept in use becomes a road of packed earth (đường
    // đất). Walking is faster on them, so routes come to follow them (NavSystem), and so they are trodden more:
    // the roads between towns draw themselves. Left alone, grass takes them back: each year the treading fades,
    // a road falls back to a trail, a trail to grass.
    //
    // Cheap by design: one counter per cell, touched only where someone walks; the yearly fade visits only the
    // cells that were ever trodden; the map and the routes are told of new trails once a month, chunk by chunk.
    public sealed class PathSystem
    {
        public const int TrailAt = 8, RoadAt = 30; // treads to wear a trail, and to pack it into a road
        const int Chunk = 32;

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly ushort[] _treads;
        readonly List<int> _trodden = new List<int>();   // cells with treads, for the yearly fade
        readonly List<int> _dirty = new List<int>();     // chunks whose trails changed since the last monthly telling
        readonly List<int> _dirtyNodes = new List<int>(); // route nodes (NavSystem.G cells square) whose trails changed
        public int TrailCells { get; private set; }
        public int RoadCells { get; private set; }

        public PathSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _treads = new ushort[_w.W * _w.H];
        }

        public int TreadsAt(int i) => _treads[i];

        static bool Wearable(Terrain t) => TerrainInfo.IsLand(t) && t != Terrain.Farmland && t != Terrain.Lava;

        // Someone stepped into cell (x, y): weight 1 for one walker, more for a crowd or carts.
        public void Tread(int x, int y, int weight)
        {
            if (!_w.InBounds(x, y)) return;
            int i = _w.Idx(x, y);
            if (!Wearable(_w.Terrain[i])) return;
            if (_treads[i] == 0) _trodden.Add(i);
            int t = _treads[i] + weight;
            _treads[i] = (ushort)(t > ushort.MaxValue ? ushort.MaxValue : t);
            byte z = _w.Zone[i];
            if ((z & ZoneFlags.Road) != 0) return;
            if (t >= RoadAt)
            {
                if ((z & ZoneFlags.Trail) != 0) TrailCells--;
                _w.Zone[i] = (byte)((z & ~ZoneFlags.Trail) | ZoneFlags.Road);
                RoadCells++;
                Dirty(x, y);
            }
            else if (t >= TrailAt && (z & ZoneFlags.Trail) == 0)
            {
                _w.Zone[i] = (byte)(z | ZoneFlags.Trail);
                TrailCells++;
                Dirty(x, y);
            }
        }

        void Dirty(int x, int y)
        {
            int c = (y / Chunk) * (_w.W / Chunk) + x / Chunk;
            if (!_dirty.Contains(c)) _dirty.Add(c);
            int node = (y / NavSystem.G) * (_w.W / NavSystem.G) + x / NavSystem.G; // the routes need only the 4×4 node it lies in
            if (!_dirtyNodes.Contains(node)) _dirtyNodes.Add(node);
        }

        // Everyday comings and goings, which the sim does not walk one by one: each year a village's people tread
        // the way to its two nearest neighbours, more the more of them there are; a town's also the way to its
        // kingdom's capital (quan lộ). A twelfth of the villages each month, so the work is spread thin.
        void VillageTraffic(long tick)
        {
            var all = _sim.Settlements.All;
            int month = (int)(tick / SimClock.DaysPerMonth % 12);
            _alive.Clear();
            foreach (var o in all) if (o.Alive) _alive.Add(o); // the long-dead villages are most of the list
            for (int k = month; k < all.Count; k += 12)
            {
                var s = all[k];
                if (!s.Alive || s.Population < 10) continue;
                int weight = UnityEngine.Mathf.Clamp(s.Population / 40, 1, 8);
                Settlement a = null, b = null, capital = null;
                float da = 70f * 70f, db = 70f * 70f;
                foreach (var o in _alive)
                {
                    if (o == s) continue;
                    if (o.Capital && o.Kingdom == s.Kingdom && s.Kingdom >= 0) capital = o;
                    float d = (o.X - s.X) * (o.X - s.X) + (o.Y - s.Y) * (o.Y - s.Y);
                    if (d < da) { db = da; b = a; da = d; a = o; }
                    else if (d < db) { db = d; b = o; }
                }
                if (a != null) Lay(s, a, weight);
                if (b != null) Lay(s, b, weight);
                if (capital != null && capital != a && capital != b && SettlementSystem.Standing(s) >= 1 &&
                    (capital.X - s.X) * (capital.X - s.X) + (capital.Y - s.Y) * (capital.Y - s.Y) < 220 * 220)
                    Lay(s, capital, weight + 2); // the officials' road
            }
        }

        // Tread the way from one village to another: straight where the land allows, else the route a walker would take.
        void Lay(Settlement from, Settlement to, int weight)
        {
            float x0 = from.X + 0.5f, y0 = from.Y + 0.5f, x1 = to.X + 0.5f, y1 = to.Y + 0.5f;
            var way = _sim.Nav.Way(x0, y0, x1, y1, _way);
            if (way == null) return;
            float ax = x0, ay = y0;
            foreach (int p in way)
            {
                float bx = (p & 0xFFFF) + 0.5f, by = (p >> 16) + 0.5f;
                TreadLine(ax, ay, bx, by, weight);
                ax = bx;
                ay = by;
            }
        }

        [System.NonSerialized] readonly List<int> _way = new List<int>();
        [System.NonSerialized] readonly List<Settlement> _alive = new List<Settlement>();

        void TreadLine(float x0, float y0, float x1, float y1, int weight)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = (int)UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(dx), UnityEngine.Mathf.Abs(dy));
            int lx = -1, ly = -1;
            for (int k = 1; k <= steps; k++) // the first cell is the village itself
            {
                int x = (int)(x0 + dx * k / steps), y = (int)(y0 + dy * k / steps);
                if (x == lx && y == ly) continue;
                lx = x;
                ly = y;
                Tread(x, y, weight);
            }
        }

        // Once a month: the map and the routes learn where trails appeared or faded.
        public void MonthlyStep(long tick)
        {
            VillageTraffic(tick);
            if (_dirty.Count == 0) return;
            int cw = _w.W / Chunk;
            foreach (int c in _dirty)
            {
                int x0 = c % cw * Chunk, y0 = c / cw * Chunk;
                _w.NotifyPathsChanged(x0, y0, x0 + Chunk - 1, y0 + Chunk - 1); // the map only: grass and fields do not care
            }
            _dirty.Clear();
            int nw = _w.W / NavSystem.G;
            foreach (int node in _dirtyNodes)
            {
                int nx = node % nw * NavSystem.G, ny = node / nw * NavSystem.G;
                _sim.Nav.RebuildArea(nx, ny, nx + NavSystem.G - 1, ny + NavSystem.G - 1);
            }
            _dirtyNodes.Clear();
        }

        // Once a year: what is no longer walked fades; grass takes back the trails no one keeps.
        public void YearlyStep()
        {
            for (int k = _trodden.Count - 1; k >= 0; k--)
            {
                int i = _trodden[k];
                int t = _treads[i] * 85 / 100; // a route walked a few times a year stays worn; one forgotten fades in a decade or two
                _treads[i] = (ushort)t;
                byte z = _w.Zone[i];
                if ((z & ZoneFlags.Road) != 0 && t < RoadAt / 4)
                {
                    _w.Zone[i] = (byte)((z & ~ZoneFlags.Road) | ZoneFlags.Trail); // the road falls back to a trail
                    RoadCells--;
                    TrailCells++;
                    Dirty(i % _w.W, i / _w.W);
                }
                else if ((z & ZoneFlags.Trail) != 0 && t < TrailAt / 3)
                {
                    _w.Zone[i] = (byte)(z & ~ZoneFlags.Trail); // grass again
                    TrailCells--;
                    Dirty(i % _w.W, i / _w.W);
                }
                if (t == 0)
                {
                    _trodden[k] = _trodden[_trodden.Count - 1];
                    _trodden.RemoveAt(_trodden.Count - 1);
                }
            }
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, TrailCells | ((long)RoadCells << 32));
            StateHash.Add(ref h, _trodden.Count);
        }
    }
}
