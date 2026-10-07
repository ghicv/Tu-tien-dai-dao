using System;
using System.Collections.Generic;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // Đường bộ: how walkers (Luyện Khí, migrants, caravans, yêu thú) find their way around sea, peaks and lava.
    // Routes are planned with A* on a coarse grid (4×4 cells a node) whose cost is the walking time over the
    // ground (roads fast, swamps and mountains slow), then smoothed so people cut straight across open land.
    // Flyers never ask. Plans are budgeted per day, so a crowd setting out at once costs a few days, not a frame.
    public sealed class NavSystem
    {
        public const int G = 4;
        public const float RoadBonus = 1.6f;
        const int PlanBudget = 32;      // A* searches per day
        const int NodeLimit = 6000;     // nodes one search may expand
        const float DirectReach = 40f;  // short trips in plain sight need no search
        const float Drift = 8f;         // a target moving less than this keeps its route
        const int MaxBlocks = 3;

        public enum Step : byte { Go, Wait, Failed }

        public sealed class Route
        {
            public float TX, TY;                          // the target this route was planned for
            public readonly List<int> Points = new List<int>(); // waypoints, packed x | y << 16
            public int Next;
            public bool Failed;
            public bool Stale;                            // blocked on the way: plan again
            public int Blocks;                            // times this trip was blocked; past MaxBlocks it is given up
        }

        readonly WorldData _w;
        readonly int _nw, _nh;
        readonly byte[] _cost;         // per node: 0 impassable, else time to cross (10 = open grass)
        readonly byte[] _door;         // per node: the walkable cell nearest its centre (x + y * G), where routes pass
        readonly bool[] _full;         // per node: every cell walkable (straight lines across it need no per-cell check)
        readonly Dictionary<int, Route> _routes = new Dictionary<int, Route>();
        int _plansToday;
        long _planDay = -1;

        // Search scratch, rebuilt on demand (not part of the world).
        [NonSerialized] float[] _g;
        [NonSerialized] int[] _from, _seen, _heap;
        [NonSerialized] float[] _heapF;
        [NonSerialized] int _stamp;
        // Connected land: nodes that can reach each other on foot share a number. A trip to another zone fails at
        // once instead of searching the whole continent first. Renumbered only when a node opens or closes.
        [NonSerialized] int[] _zone;
        [NonSerialized] bool _zonesDirty = true;

        public int Planned { get; private set; }

        // Where walking time goes, for the perf probe: not part of the world.
        public static readonly string[] ProfNames = { "Tìm đường A*", "Kiểm tra đi thẳng", "Đánh số vùng liên thông" };
        public static readonly double[] ProfMs = new double[3];
        public static long Expanded, Failures, Waits, ZoneRebuilds;
        static readonly double ProfScale = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        static void Prof(int slot, long t0) => ProfMs[slot] += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * ProfScale;

        public NavSystem(Simulation sim)
        {
            _w = sim.World;
            _nw = _w.W / G;
            _nh = _w.H / G;
            _cost = new byte[_nw * _nh];
            _door = new byte[_nw * _nh];
            _full = new bool[_nw * _nh];
            Rebuild(0, 0, _w.W - 1, _w.H - 1);
            _w.TerrainChanged += Rebuild;
            sim.Entities.Died += (id, s, cause) => _routes.Remove(id);
        }

        public float SpeedAt(float x, float y)
        {
            int cx = (int)x, cy = (int)y;
            if (!_w.InBounds(cx, cy)) return 0f;
            int i = _w.Idx(cx, cy);
            float s = TerrainInfo.WalkSpeed[(int)_w.Terrain[i]];
            return (_w.Zone[i] & ZoneFlags.Road) != 0 ? s * RoadBonus : s;
        }

        void Rebuild(int x0, int y0, int x1, int y1)
        {
            int nx0 = Mathf.Clamp(x0 / G, 0, _nw - 1), nx1 = Mathf.Clamp(x1 / G, 0, _nw - 1);
            int ny0 = Mathf.Clamp(y0 / G, 0, _nh - 1), ny1 = Mathf.Clamp(y1 / G, 0, _nh - 1);
            for (int ny = ny0; ny <= ny1; ny++)
            for (int nx = nx0; nx <= nx1; nx++)
            {
                int open = 0, door = 0;
                float time = 0f, doorD = float.MaxValue;
                for (int y = ny * G; y < ny * G + G; y++)
                for (int x = nx * G; x < nx * G + G; x++)
                {
                    float s = SpeedAt(x + 0.5f, y + 0.5f);
                    if (s <= 0f) continue;
                    open++;
                    time += 10f / s;
                    // A route through this node passes its most central dry cell, never a wet one.
                    float cdx = x - nx * G - (G - 1) * 0.5f, cdy = y - ny * G - (G - 1) * 0.5f, cd = cdx * cdx + cdy * cdy;
                    if (cd < doorD) { doorD = cd; door = (x - nx * G) + (y - ny * G) * G; }
                }
                _door[ny * _nw + nx] = (byte)door;
                _full[ny * _nw + nx] = open == G * G;
                // Mostly water or rock: not a way through (a single walkable cell would leave walkers stuck on it).
                byte c = open < 6 ? (byte)0 : (byte)Mathf.Clamp(time / open, 1f, 255f);
                int node = ny * _nw + nx;
                if ((c == 0) != (_cost[node] == 0)) _zonesDirty = true;
                _cost[node] = c;
            }
        }

        // Where a walker should head now, toward its entity target (TX, TY).
        public Step Steer(int id, EntityStore e, long tick, out float wx, out float wy)
        {
            wx = e.TX[id];
            wy = e.TY[id];
            _routes.TryGetValue(id, out var route);
            // The target crept a little (a beast chasing its prey): keep the road, move only its end.
            if (route != null && !route.Failed && route.Points.Count > 0 && (route.TX != e.TX[id] || route.TY != e.TY[id]) &&
                (route.TX - e.TX[id]) * (route.TX - e.TX[id]) + (route.TY - e.TY[id]) * (route.TY - e.TY[id]) < Drift * Drift)
            {
                route.Points[route.Points.Count - 1] = Pack(e.TX[id], e.TY[id]);
                route.TX = e.TX[id];
                route.TY = e.TY[id];
            }
            bool newTarget = route == null || route.TX != e.TX[id] || route.TY != e.TY[id];
            if (newTarget || route.Stale)
            {
                if (tick != _planDay) { _planDay = tick; _plansToday = 0; }
                if (route == null) { route = new Route(); _routes[id] = route; }
                if (newTarget) route.Blocks = 0;
                route.Stale = false;
                // Close by and in plain sight: walk straight there, no search.
                float dx = e.TX[id] - e.X[id], dy = e.TY[id] - e.Y[id];
                long p0 = System.Diagnostics.Stopwatch.GetTimestamp();
                bool direct = dx * dx + dy * dy <= DirectReach * DirectReach && LineClear(e.X[id], e.Y[id], e.TX[id], e.TY[id]);
                Prof(1, p0);
                if (direct)
                    Direct(route, e.TX[id], e.TY[id]);
                else
                {
                    if (_plansToday >= PlanBudget) { Waits++; return Step.Wait; }
                    _plansToday++;
                    long p1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    Plan(route, e.X[id], e.Y[id], e.TX[id], e.TY[id]);
                    Prof(0, p1);
                    if (route.Failed) Failures++;
                }
                route.TX = e.TX[id];
                route.TY = e.TY[id];
            }
            if (route.Failed) return Step.Failed;
            while (route.Next < route.Points.Count)
            {
                int p = route.Points[route.Next];
                float px = (p & 0xFFFF) + 0.5f, py = (p >> 16) + 0.5f;
                float ddx = px - e.X[id], ddy = py - e.Y[id];
                if (ddx * ddx + ddy * ddy > 0.8f || route.Next == route.Points.Count - 1) break;
                route.Next++;
            }
            if (route.Next < route.Points.Count - 1)
            {
                int p = route.Points[route.Next];
                wx = (p & 0xFFFF) + 0.5f;
                wy = (p >> 16) + 0.5f;
            }
            return Step.Go;
        }

        // Something blocked the way (the land changed): plan again next time.
        public void Replan(int id)
        {
            if (!_routes.TryGetValue(id, out var route)) return;
            // The same trip blocked again and again: there is no way through after all.
            if (++route.Blocks > MaxBlocks) route.Failed = true;
            else route.Stale = true;
        }

        public bool Failed(int id) => _routes.TryGetValue(id, out var r) && r.Failed;

        static void Direct(Route route, float tx, float ty)
        {
            route.Points.Clear();
            route.Points.Add(Pack(tx, ty));
            route.Next = 0;
            route.Failed = false;
        }

        static int Pack(float x, float y) => Mathf.Clamp((int)x, 0, 0xFFFF) | (Mathf.Clamp((int)y, 0, 0x7FFF) << 16);

        // ---------------------------------------------------------------- A*

        void Plan(Route route, float sx, float sy, float tx, float ty)
        {
            Planned++;
            route.Points.Clear();
            route.Next = 0;
            route.Failed = false;
            int start = Nearest((int)sx / G, (int)sy / G), goal = Nearest((int)tx / G, (int)ty / G);
            if (start < 0 || goal < 0) { route.Failed = true; return; }
            if (start == goal) { route.Points.Add(Pack(tx, ty)); return; }
            if (_zonesDirty)
            {
                long pz = System.Diagnostics.Stopwatch.GetTimestamp();
                NumberZones();
                Prof(2, pz);
                ZoneRebuilds++;
            }
            if (_zone[start] != _zone[goal]) { route.Failed = true; return; }

            int n = _nw * _nh;
            if (_g == null || _g.Length != n)
            {
                _g = new float[n];
                _from = new int[n];
                _seen = new int[n];
                _heap = new int[n];
                _heapF = new float[n];
                _stamp = 0;
            }
            _stamp++;
            int heapCount = 0;
            int gx = goal % _nw, gy = goal / _nw;
            _seen[start] = _stamp;
            _g[start] = 0f;
            _from[start] = -1;
            Push(start, H(start, gx, gy), ref heapCount);
            int expanded = 0;
            bool found = false;
            while (heapCount > 0 && expanded < NodeLimit)
            {
                int cur = Pop(ref heapCount);
                if (cur == goal) { found = true; break; }
                expanded++;
                Expanded++;
                int cx = cur % _nw, cy = cur / _nw;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if ((uint)nx >= (uint)_nw || (uint)ny >= (uint)_nh) continue;
                    int nb = ny * _nw + nx;
                    byte c = _cost[nb];
                    if (c == 0) continue;
                    // No cutting corners between two impassable nodes.
                    if (dx != 0 && dy != 0 && (_cost[cy * _nw + nx] == 0 || _cost[ny * _nw + cx] == 0)) continue;
                    float step = (c + _cost[cur]) * 0.5f * (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    float ng = _g[cur] + step;
                    if (_seen[nb] == _stamp && ng >= _g[nb]) continue;
                    _seen[nb] = _stamp;
                    _g[nb] = ng;
                    _from[nb] = cur;
                    Push(nb, ng + H(nb, gx, gy), ref heapCount);
                }
            }
            if (!found) { route.Failed = true; return; }

            // Node centres from start to goal, then the exact target.
            var nodes = new List<int>();
            for (int k = goal; k >= 0; k = _from[k]) nodes.Add(k);
            nodes.Reverse();
            float ax = sx, ay = sy;
            for (int k = 1; k < nodes.Count; k++)
            {
                // String pulling: skip a waypoint while the next one is in a straight, walkable line.
                if (k + 1 < nodes.Count && LineClear(ax, ay, Cx(nodes[k + 1]), Cy(nodes[k + 1]))) continue;
                route.Points.Add(Pack(Cx(nodes[k]), Cy(nodes[k])));
                ax = Cx(nodes[k]);
                ay = Cy(nodes[k]);
            }
            route.Points.Add(Pack(tx, ty));
        }

        // Flood fill with the same moves A* allows (eight ways, no corner cutting).
        void NumberZones()
        {
            int n = _nw * _nh;
            if (_zone == null || _zone.Length != n) _zone = new int[n];
            System.Array.Clear(_zone, 0, n);
            var stack = new Stack<int>();
            int next = 0;
            for (int s = 0; s < n; s++)
            {
                if (_cost[s] == 0 || _zone[s] != 0) continue;
                next++;
                _zone[s] = next;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    int cx = cur % _nw, cy = cur / _nw;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if ((dx == 0 && dy == 0) || (uint)nx >= (uint)_nw || (uint)ny >= (uint)_nh) continue;
                        int nb = ny * _nw + nx;
                        if (_cost[nb] == 0 || _zone[nb] != 0) continue;
                        if (dx != 0 && dy != 0 && (_cost[cy * _nw + nx] == 0 || _cost[ny * _nw + cx] == 0)) continue;
                        _zone[nb] = next;
                        stack.Push(nb);
                    }
                }
            }
            _zonesDirty = false;
        }

        float Cx(int node) => (node % _nw) * G + (_door[node] % G) + 0.5f;
        float Cy(int node) => (node / _nw) * G + (_door[node] / G) + 0.5f;

        // Weighted toward the goal (open grass costs 10 a node): a near-best road for a fraction of the search.
        float H(int node, int gx, int gy)
        {
            int dx = Mathf.Abs(node % _nw - gx), dy = Mathf.Abs(node / _nw - gy);
            return 10f * (Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy));
        }

        // The open node nearest to (nx, ny) within a few nodes, -1 if none (a walker on an islet or in the sea).
        int Nearest(int nx, int ny)
        {
            for (int r = 0; r <= 3; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                int x = nx + dx, y = ny + dy;
                if ((uint)x < (uint)_nw && (uint)y < (uint)_nh && _cost[y * _nw + x] != 0) return y * _nw + x;
            }
            return -1;
        }

        // The straight line crosses only open nodes: two samples a node and a byte each, against two a cell for
        // LineClear; small obstacles inside a node are left to the walker's sidestep.
        bool CoarseLineClear(float x0, float y0, float x1, float y1)
        {
            float dx = (x1 - x0) / G, dy = (y1 - y0) / G;
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 2f);
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                int nx = (int)((x0 + (x1 - x0) * t) / G), ny = (int)((y0 + (y1 - y0) * t) / G);
                if ((uint)nx >= (uint)_nw || (uint)ny >= (uint)_nh || _cost[ny * _nw + nx] == 0) return false;
            }
            return true;
        }

        // Every cell on the straight line can be walked. Inside a node whose every cell is dry the per-cell test is
        // skipped (one byte instead of a terrain lookup), which is most of the open country.
        public bool LineClear(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 2f);
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                float x = x0 + dx * t, y = y0 + dy * t;
                if (x < 0f || y < 0f) return false;
                int nx = (int)x / G, ny = (int)y / G;
                if ((uint)nx >= (uint)_nw || (uint)ny >= (uint)_nh) return false;
                if (_full[ny * _nw + nx]) continue;
                if (!_w.IsWalkable(x, y)) return false;
            }
            return true;
        }

        void Push(int node, float f, ref int count)
        {
            int i = count++;
            _heap[i] = node;
            _heapF[i] = f;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (Less(p, i)) break;
                Swap(i, p);
                i = p;
            }
        }

        int Pop(ref int count)
        {
            int top = _heap[0];
            count--;
            _heap[0] = _heap[count];
            _heapF[0] = _heapF[count];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < count && Less(l, m)) m = l;
                if (r < count && Less(r, m)) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        // Ties broken by node index, so the same search always takes the same path.
        bool Less(int a, int b) => _heapF[a] < _heapF[b] || (_heapF[a] == _heapF[b] && _heap[a] < _heap[b]);

        void Swap(int a, int b)
        {
            (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
            (_heapF[a], _heapF[b]) = (_heapF[b], _heapF[a]);
        }
    }
}
