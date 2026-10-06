using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    public enum Calamity : byte { Earthquake, Eruption, Flood, Drought, Plague, BeastTide }

    // A place the land remembers: lôi địa where a tribulation fell, a volcano that rose from the plain.
    public sealed class Landmark
    {
        public const byte Thunder = 0, Volcano = 1;

        public byte Kind;
        public string Name;
        public int X, Y, R;
        public long Tick;
        public long Until = long.MaxValue; // lôi khí fades over centuries; a volcano stays
        public string Origin;              // "thiên kiếp của Hàn Lập năm 203"
        public bool Alive = true;
    }

    // Thiên tai (M6): calamities Thiên Đạo sends and those the world brings on itself, and the marks they leave.
    // Everything here is ordinary simulation state: deterministic, hashed, replayable from the command log.
    public sealed class DisasterSystem
    {
        // Yearly chance of each natural calamity somewhere in the world.
        const float QuakePerYear = 0.04f, EruptionPerYear = 0.008f, FloodPerYear = 0.05f, DroughtPerYear = 0.05f, TidePerYear = 0.03f;
        const int TideReach = 50;
        const int SpreadReach = 70;
        const int MaxEpidemics = 16;

        struct FloodCell
        {
            public int Cell;
            public Terrain Was;
            public long Recede;
        }

        struct LavaCell
        {
            public int Cell;
            public Terrain Into;
            public long Cool;
        }

        sealed class Drought
        {
            public int X, Y, R;
            public long Start, Until;
            public string Place;
            public int StartPop;
        }

        sealed class Epidemic
        {
            public int Settlement;
            public long Until;
            public float Severity;
            public int Dead;
            public bool Done;
        }

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly List<FloodCell> _flood = new List<FloodCell>();
        readonly List<LavaCell> _lava = new List<LavaCell>();
        readonly List<Drought> _droughts = new List<Drought>();
        readonly List<Epidemic> _epidemics = new List<Epidemic>();
        readonly Dictionary<int, long> _immuneUntil = new Dictionary<int, long>(); // settlement id → no new epidemic before
        readonly List<int> _ids = new List<int>();
        readonly List<Settlement> _near = new List<Settlement>();
        readonly Lore.Picker _thunderNames = new Lore.Picker(Lore.ThunderPlaces);
        readonly Lore.Picker _volcanoNames = new Lore.Picker(Lore.Volcanoes);

        public readonly List<Landmark> Landmarks = new List<Landmark>();

        public DisasterSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
        }

        public int DroughtCount => _droughts.Count;
        public int EpidemicCount => _epidemics.Count;
        public int FloodedCells => _flood.Count;
        public int LavaCells => _lava.Count;

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0xCA1Au, (int)tick, salt));

        static int Stoch(float v, ref DetRandom rng)
        {
            int whole = (int)v;
            return whole + (rng.NextFloat() < v - whole ? 1 : 0);
        }

        static int Year(long tick) => (int)(tick / SimClock.DaysPerYear) + 1;

        // How far a calamity reaches for a given brush size (the cursor shows this).
        public static int Radius(Calamity kind, int size)
        {
            switch (kind)
            {
                case Calamity.Earthquake: return Mathf.Clamp(size * 2, 10, 60);
                case Calamity.Eruption: return 5;
                case Calamity.Flood: return Mathf.Clamp(size * 2, 8, 40);
                case Calamity.Drought: return Mathf.Clamp(size * 6, 40, 160);
                case Calamity.BeastTide: return TideReach;
                default: return 3;
            }
        }

        // ---------------------------------------------------------------- Thiên Đạo sends a calamity

        public void Unleash(Calamity kind, int x, int y, int size, long tick, bool divine)
        {
            if (!_w.InBounds(x, y)) return;
            var rng = RngFor(tick, 0x100000 + (int)kind * 7919 + x * 31 + y * 1031);
            int r = Radius(kind, size);
            switch (kind)
            {
                case Calamity.Earthquake: Earthquake(x, y, r, tick, divine, ref rng); break;
                case Calamity.Eruption: Eruption(x, y, tick, divine, ref rng); break;
                case Calamity.Flood: Flood(x, y, r, tick, divine, ref rng); break;
                case Calamity.Drought: StartDrought(x, y, r, rng.Range(12, 31), tick, divine); break;
                case Calamity.Plague: StartEpidemic(NearestSettlement(x, y, 24), tick, divine); break;
                case Calamity.BeastTide: BeastTide(x, y, rng.Range(150, 301), tick, divine, ref rng); break;
            }
        }

        // ---------------------------------------------------------------- động đất

        void Earthquake(int cx, int cy, int r, long tick, bool divine, ref DetRandom rng)
        {
            string where = PlaceName(cx, cy);
            var objs = _w.Objects;
            int houses = 0, ley = 0;
            _ids.Clear();
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!_w.InBounds(x, y)) continue;
                int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 > r * r) continue;
                int i = _w.Idx(x, y);
                float k = 1f - Mathf.Sqrt(d2) / r;
                int id = objs.CellObject[i];
                if (id >= 0 && objs.Get(id).Type == ObjectType.House && !_ids.Contains(id)) _ids.Add(id);
                // The earth splits the veins of qi beneath it.
                if (_w.LeyLine[i] && rng.NextFloat() < (divine ? 0.5f : 0.3f) * k)
                {
                    _w.LeyLine[i] = false;
                    ley++;
                }
                else if (id < 0 && TerrainInfo.IsHighland(_w.Terrain[i]) && rng.NextFloat() < 0.03f * k)
                    objs.Place(ObjectType.Rock, x, y, (byte)rng.Range(0, 256)); // landslide
            }
            foreach (int id in _ids)
            {
                var o = objs.Get(id);
                float dx = o.X + 1.5f - cx, dy = o.Y + 1.5f - cy;
                float k = 1f - Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (k > 0f && rng.NextFloat() < 0.7f * k)
                {
                    objs.Remove(id);
                    houses++;
                }
            }
            int dead = 0;
            Within(cx, cy, r);
            foreach (var s in _near)
            {
                float k = 1f - Dist(s, cx, cy) / r;
                dead += _sim.Settlements.Kill(s, Stoch(s.Population * (0.12f * k + 0.01f), ref rng));
            }
            if (ley > 0) RebuildQi(cx - r - QiCap.LeyReach, cy - r - QiCap.LeyReach, cx + r + QiCap.LeyReach, cy + r + QiCap.LeyReach);
            int imp = Mathf.Max(divine ? 2 : 1, dead >= 30 || ley >= 10 ? 2 : 1);
            _sim.Events.Add(tick, EventKind.Calamity, imp,
                $"{(divine ? "Thiên Đạo nổi giận, địa long" : "Địa long")} trở mình {where}: {houses} nhà sập, {dead} người chết" +
                (ley > 0 ? $", {ley} đoạn linh mạch đứt gãy" : "") + ".", cx + 0.5f, cy + 0.5f, Fx.Quake);
        }

        // ---------------------------------------------------------------- núi lửa

        void Eruption(int cx, int cy, long tick, bool divine, ref DetRandom rng)
        {
            const int crater = 2, cone = 5;
            if (cx < 16 || cy < 16 || cx >= _w.W - 16 || cy >= _w.H - 16) return;
            if (!TerrainInfo.IsLand(_w.Terrain[_w.Idx(cx, cy)]) || _w.Terrain[_w.Idx(cx, cy)] == Terrain.Lava) return; // the sea swallows it
            string where = PlaceName(cx, cy);
            string name = _volcanoNames.Next(ref rng);
            var objs = _w.Objects;
            int houses = 0;
            int x0 = cx - cone, y0 = cy - cone, x1 = cx + cone, y1 = cy + cone;

            // The cone rises; fire wells up in the crater and a new vein of địa hỏa opens beneath it.
            for (int y = cy - cone; y <= cy + cone; y++)
            for (int x = cx - cone; x <= cx + cone; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > cone + 0.3f) continue;
                int i = _w.Idx(x, y);
                houses += Clear(i);
                if (d <= crater)
                {
                    SetLava(i, Terrain.Mountain, tick + rng.Range(40, 91) * (long)SimClock.DaysPerYear);
                    if (d <= 1f) _w.LeyLine[i] = true;
                }
                else
                {
                    _w.Terrain[i] = Terrain.Mountain; // bare dark rock, not a snowy peak
                    _w.Height[i] = TerrainInfo.NominalHeight[(int)Terrain.Mountain];
                }
            }

            // Lava runs down the flanks in a few tongues until it cools or meets water.
            int streams = rng.Range(4, 8);
            for (int s = 0; s < streams; s++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float x = cx + 0.5f + Mathf.Cos(a) * crater, y = cy + 0.5f + Mathf.Sin(a) * crater;
                int len = rng.Range(10, 27);
                for (int step = 0; step < len; step++)
                {
                    a += rng.Range(-0.5f, 0.5f);
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a);
                    int ix = (int)x, iy = (int)y;
                    if (!_w.InBounds(ix, iy)) break;
                    if ((ix - cx) * (ix - cx) + (iy - cy) * (iy - cy) <= cone * cone) continue; // still on the cone
                    int i = _w.Idx(ix, iy);
                    if (TerrainInfo.IsWater(_w.Terrain[i])) break; // hisses into steam
                    long cool = tick + rng.Range(3, 13) * (long)SimClock.DaysPerYear;
                    houses += Clear(i);
                    SetLava(i, Terrain.Hills, cool);
                    int sx = ix + (rng.NextFloat() < 0.5f ? 1 : -1);
                    if (rng.NextFloat() < 0.5f && _w.InBounds(sx, iy) && !TerrainInfo.IsWater(_w.Terrain[_w.Idx(sx, iy)]))
                    {
                        houses += Clear(_w.Idx(sx, iy));
                        SetLava(_w.Idx(sx, iy), Terrain.Hills, cool);
                    }
                    x0 = Mathf.Min(x0, ix - 1);
                    y0 = Mathf.Min(y0, iy);
                    x1 = Mathf.Max(x1, ix + 1);
                    y1 = Mathf.Max(y1, iy);
                }
            }

            // Forest around the mountain burns.
            const int burn = cone + 8;
            for (int y = cy - burn; y <= cy + burn; y++)
            for (int x = cx - burn; x <= cx + burn; x++)
            {
                if (!_w.InBounds(x, y)) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > burn) continue;
                int id = objs.CellObject[_w.Idx(x, y)];
                if (id >= 0 && IsPlant(objs.Get(id).Type) && rng.NextFloat() < 0.6f * (1f - (d - cone) / (burn - cone))) objs.Remove(id);
            }

            // Ash falls on every village within a day's walk.
            const int ash = 40;
            int dead = 0;
            Within(cx, cy, ash);
            foreach (var v in _near)
            {
                float k = 1f - Dist(v, cx, cy) / ash;
                dead += _sim.Settlements.Kill(v, Stoch(v.Population * 0.15f * k, ref rng));
                v.Food *= 1f - 0.5f * k;
            }

            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            x1 = Mathf.Min(_w.W - 1, x1);
            y1 = Mathf.Min(_w.H - 1, y1);
            _w.NotifyTerrainChanged(x0, y0, x1, y1);
            RebuildQi(x0 - QiCap.LeyReach, y0 - QiCap.LeyReach, x1 + QiCap.LeyReach, y1 + QiCap.LeyReach);
            _sim.Wildlife.LandChanged(x0, y0, x1, y1);
            _sim.ResolveFlood(x0, y0, x1, y1); // whoever stands in the lava burns

            Landmarks.Add(new Landmark
            {
                Kind = Landmark.Volcano, Name = name, X = cx, Y = cy, R = cone + 2, Tick = tick,
                Origin = divine ? $"núi lửa Thiên Đạo gọi lên năm {Year(tick)}" : $"núi lửa phun trào năm {Year(tick)}"
            });
            _sim.Events.Add(tick, EventKind.Calamity, 3,
                $"Núi lửa phun trào {where}{(divine ? " theo ý Thiên Đạo" : "")}, {name} mọc lên giữa trời đất: {dead} người chết vì tro bụi" +
                (houses > 0 ? $", {houses} nhà bị thiêu rụi" : "") + ".", cx + 0.5f, cy + 0.5f, Fx.Eruption);
        }

        static bool IsPlant(ObjectType t) => t != ObjectType.None && t != ObjectType.Rock && !ObjectInfo.IsBuilding(t);

        // Whatever stands on the cell is destroyed and the field is lost; returns 1 if a house went with it.
        int Clear(int i)
        {
            int house = 0;
            int id = _w.Objects.CellObject[i];
            if (id >= 0)
            {
                if (_w.Objects.Get(id).Type == ObjectType.House) house = 1;
                _w.Objects.Remove(id);
            }
            if (_w.Terrain[i] == Terrain.Farmland) _sim.Settlements.LoseField(i);
            return house;
        }

        void SetLava(int i, Terrain into, long cool)
        {
            if (_w.Terrain[i] != Terrain.Lava) _lava.Add(new LavaCell { Cell = i, Into = into, Cool = cool });
            _w.Terrain[i] = Terrain.Lava;
            _w.Height[i] = Mathf.Max(_w.Height[i], 0.6f);
        }

        void CoolLava(long tick)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, kept = 0;
            for (int k = 0; k < _lava.Count; k++)
            {
                var l = _lava[k];
                if (tick < l.Cool)
                {
                    _lava[kept++] = l;
                    continue;
                }
                if (_w.Terrain[l.Cell] != Terrain.Lava) continue; // Thiên Đạo already reshaped it
                _w.Terrain[l.Cell] = l.Into;
                _w.Height[l.Cell] = TerrainInfo.NominalHeight[(int)l.Into];
                int x = l.Cell % _w.W, y = l.Cell / _w.W;
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            _lava.RemoveRange(kept, _lava.Count - kept);
            if (x1 < x0) return;
            _w.NotifyTerrainChanged(x0, y0, x1, y1);
            RebuildQi(x0, y0, x1, y1);
            _sim.Wildlife.LandChanged(x0, y0, x1, y1);
        }

        // ---------------------------------------------------------------- lũ lụt

        void Flood(int cx, int cy, int r, long tick, bool divine, ref DetRandom rng)
        {
            string where = PlaceName(cx, cy);
            long recede = tick + rng.Range(60, 121);
            var objs = _w.Objects;
            Within(cx, cy, r + 4);
            int cells = 0, fields = 0;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            uint shore = _w.Seed ^ 0xF100Du ^ (uint)tick;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!_w.InBounds(x, y)) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > r * (0.65f + 0.35f * Hash.Float01(shore, x, y))) continue; // a ragged waterline
                int i = _w.Idx(x, y);
                var t = _w.Terrain[i];
                // Only the lowlands drown; hills, villages' own ground and buildings stay above the water.
                if (!TerrainInfo.IsLand(t) || t == Terrain.Lava || TerrainInfo.IsHighland(t) || t == Terrain.Snow || _w.Height[i] > 0.6f) continue;
                if (NearCentre(x, y)) continue;
                int id = objs.CellObject[i];
                if (id >= 0 && ObjectInfo.IsBuilding(objs.Get(id).Type)) continue;
                if (_w.Owner[i] != 0 && t != Terrain.Farmland) continue;
                if (t == Terrain.Farmland)
                {
                    _sim.Settlements.LoseField(i);
                    fields++;
                }
                if (id >= 0) objs.Remove(id);
                _flood.Add(new FloodCell { Cell = i, Was = t, Recede = recede });
                _w.Terrain[i] = Terrain.Shallow;
                cells++;
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            if (cells == 0) return;
            _w.NotifyTerrainChanged(x0, y0, x1, y1);
            RebuildQi(x0, y0, x1, y1); // water damps qi
            _sim.Wildlife.LandChanged(x0, y0, x1, y1);

            int dead = 0;
            foreach (var s in _near)
            {
                float k = Mathf.Clamp01(1f - Dist(s, cx, cy) / r);
                if (k <= 0f) continue;
                dead += _sim.Settlements.Kill(s, Stoch(s.Population * (0.05f * k + 0.01f), ref rng));
                s.Food *= 0.7f; // stores soaked and rotting
            }
            _sim.ResolveFlood(x0, y0, x1, y1);
            int imp = Mathf.Max(divine ? 2 : 1, dead >= 20 || fields >= 60 ? 2 : 1);
            _sim.Events.Add(tick, EventKind.Calamity, imp,
                $"{(divine ? "Thiên Đạo giáng mưa lớn, l" : "L")}ũ dữ tràn {where}: {fields} ô ruộng ngập trắng, {dead} người chết đuối.", cx + 0.5f, cy + 0.5f, Fx.Splash);
        }

        bool NearCentre(int x, int y)
        {
            foreach (var s in _near)
                if (Mathf.Abs(s.X - x) <= 1 && Mathf.Abs(s.Y - y) <= 1) return true; // the village square; its fields start right outside
            return false;
        }

        // The water goes back where it came from; drowned fields come back as grass, to be tilled again.
        void Recede(long tick)
        {
            if (_flood.Count == 0) return;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, kept = 0;
            for (int k = 0; k < _flood.Count; k++)
            {
                var f = _flood[k];
                if (tick < f.Recede)
                {
                    _flood[kept++] = f;
                    continue;
                }
                if (_w.Terrain[f.Cell] != Terrain.Shallow) continue;
                _w.Terrain[f.Cell] = f.Was == Terrain.Farmland ? Terrain.Grass : f.Was;
                int x = f.Cell % _w.W, y = f.Cell / _w.W;
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            _flood.RemoveRange(kept, _flood.Count - kept);
            if (x1 < x0) return;
            _w.NotifyTerrainChanged(x0, y0, x1, y1);
            RebuildQi(x0, y0, x1, y1);
            _sim.Wildlife.LandChanged(x0, y0, x1, y1);
        }

        // ---------------------------------------------------------------- hạn hán

        public void StartDrought(int x, int y, int r, int months, long tick, bool divine)
        {
            var d = new Drought
            {
                X = x, Y = y, R = r, Start = tick, Until = tick + months * (long)SimClock.DaysPerMonth,
                Place = PlaceName(x, y), StartPop = PopulationWithin(x, y, r)
            };
            _droughts.Add(d);
            _sim.Events.Add(tick, EventKind.Calamity, 2,
                $"{(divine ? "Thiên Đạo khóa mây, đ" : "Đ")}ại hạn {d.Place}: trời không mưa, ruộng đồng nứt nẻ.", x + 0.5f, y + 0.5f, Fx.None);
        }

        // Share of the usual harvest a village at (x, y) brings in this month.
        public float HarvestFactor(int x, int y)
        {
            foreach (var d in _droughts)
                if ((d.X - x) * (d.X - x) + (d.Y - y) * (d.Y - y) <= d.R * d.R) return 0.25f;
            return 1f;
        }

        // Months of drought left over (x, y), or -1.
        public int DroughtMonthsLeft(int x, int y, long tick)
        {
            int best = -1;
            foreach (var d in _droughts)
                if ((d.X - x) * (d.X - x) + (d.Y - y) * (d.Y - y) <= d.R * d.R)
                    best = Mathf.Max(best, (int)((d.Until - tick) / SimClock.DaysPerMonth));
            return best;
        }

        void DroughtStep(long tick)
        {
            var forage = _sim.Forage;
            for (int k = _droughts.Count - 1; k >= 0; k--)
            {
                var d = _droughts[k];
                if (tick >= d.Until)
                {
                    _droughts.RemoveAt(k);
                    int now = PopulationWithin(d.X, d.Y, d.R);
                    int months = (int)((tick - d.Start) / SimClock.DaysPerMonth);
                    _sim.Events.Add(tick, EventKind.Calamity, now < d.StartPop * 0.85f ? 2 : 1,
                        $"Hạn hán {d.Place} chấm dứt sau {months} tháng; dân trong vùng từ {d.StartPop:N0} còn {now:N0} người.", d.X + 0.5f, d.Y + 0.5f);
                    continue;
                }
                // Grass withers: grazers go hungry, then the wolves.
                int b = ForageSystem.Block;
                int bx0 = Mathf.Max(0, (d.X - d.R) / b), bx1 = Mathf.Min(forage.BW - 1, (d.X + d.R) / b);
                int by0 = Mathf.Max(0, (d.Y - d.R) / b), by1 = Mathf.Min(forage.BH - 1, (d.Y + d.R) / b);
                for (int by = by0; by <= by1; by++)
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    int dx = bx * b + b / 2 - d.X, dy = by * b + b / 2 - d.Y;
                    if (dx * dx + dy * dy <= d.R * d.R) forage.Scale(bx, by, 1, 0.6f);
                }
            }
        }

        // ---------------------------------------------------------------- ôn dịch

        public bool IsInfected(int settlement)
        {
            foreach (var e in _epidemics)
                if (e.Settlement == settlement && !e.Done) return true;
            return false;
        }

        // An epidemic breaks out in the village; it lingers a few months and may spread to the villages around.
        public void StartEpidemic(Settlement s, long tick, bool divine)
        {
            if (s == null || !s.Alive || IsInfected(s.Id) || _epidemics.Count >= MaxEpidemics) return;
            if (_immuneUntil.TryGetValue(s.Id, out long immune) && tick < immune && !divine) return;
            var rng = RngFor(tick, 0x200000 + s.Id);
            _epidemics.Add(new Epidemic
            {
                Settlement = s.Id, Until = tick + rng.Range(3, divine ? 9 : 7) * (long)SimClock.DaysPerMonth, Severity = divine ? 0.06f : 0.04f
            });
            _sim.Events.Add(tick, EventKind.Calamity, divine ? 2 : 1,
                $"{(divine ? "Thiên Đạo giáng ôn thần, ô" : "Ô")}n dịch bùng phát ở {s.Name}.", s.X + 0.5f, s.Y + 0.5f, Fx.Miasma);
        }

        void EpidemicStep(long tick)
        {
            var all = _sim.Settlements.All;
            int n = _epidemics.Count; // those that spread this month start next month
            for (int k = 0; k < n; k++)
            {
                var ep = _epidemics[k];
                var s = all[ep.Settlement];
                if (!s.Alive) { ep.Done = true; continue; }
                if (tick >= ep.Until)
                {
                    ep.Done = true;
                    _immuneUntil[s.Id] = tick + 10L * SimClock.DaysPerYear;
                    _sim.Events.Add(tick, EventKind.Calamity, ep.Dead >= 50 ? 2 : 1, $"Ôn dịch ở {s.Name} chấm dứt, {ep.Dead} người đã chết.", s.X + 0.5f, s.Y + 0.5f);
                    continue;
                }
                var rng = RngFor(tick, 0x300000 + s.Id);
                ep.Dead += _sim.Settlements.Kill(s, Stoch(s.Population * ep.Severity, ref rng));
                // Traders and the fleeing carry it along the roads.
                Within(s.X, s.Y, SpreadReach);
                foreach (var o in _near)
                {
                    if (o == s || IsInfected(o.Id) || _epidemics.Count >= MaxEpidemics) continue;
                    if (_immuneUntil.TryGetValue(o.Id, out long immune) && tick < immune) continue;
                    if (rng.NextFloat() >= 0.05f * (1f - Dist(o, s.X, s.Y) / SpreadReach)) continue;
                    _epidemics.Add(new Epidemic
                    {
                        Settlement = o.Id, Until = tick + rng.Range(3, 8) * (long)SimClock.DaysPerMonth, Severity = ep.Severity * 0.9f
                    });
                    _sim.Events.Add(tick, EventKind.Calamity, 1, $"Ôn dịch từ {s.Name} lan sang {o.Name}.", o.X + 0.5f, o.Y + 0.5f, Fx.Miasma);
                }
            }
            _epidemics.RemoveAll(e => e.Done);
        }

        // ---------------------------------------------------------------- thú triều

        // Wolves pour out of the wild onto the villages; a sect guarding the land beats them back.
        void BeastTide(int cx, int cy, int wolves, long tick, bool divine, ref DetRandom rng)
        {
            string where = PlaceName(cx, cy);
            if (wolves > 0) _sim.Wildlife.Add(Species.Wolf, cx + 0.5f, cy + 0.5f, wolves);
            int dead = 0;
            var guards = new List<string>();
            Within(cx, cy, TideReach);
            foreach (var s in _near)
            {
                float k = 1f - Dist(s, cx, cy) / TideReach;
                float share = 0.12f * k + 0.02f;
                var guard = s.Sect ? s : _sim.Factions.ProtectorOf(s.X, s.Y);
                if (guard != null)
                {
                    share *= 0.35f;
                    if (!guards.Contains(guard.BaseName)) guards.Add(guard.BaseName);
                }
                dead += _sim.Settlements.Kill(s, Stoch(s.Population * share, ref rng));
                s.Food *= 0.85f; // the herds are gone too
            }
            // Outer disciples caught on the road.
            var e = _sim.Entities;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Realm != Realm.LuyenKhi || !_sim.Cultivation.IsShownOnMap(c)) continue;
                float dx = e.X[c.Entity] - cx, dy = e.Y[c.Entity] - cy;
                if (dx * dx + dy * dy > TideReach * TideReach || rng.NextFloat() >= 0.3f) continue;
                _sim.Cultivation.Perish(c, tick, $"{c.Name} ({_sim.Cultivation.SectName(c)}) bị bầy yêu lang vây xé giữa đường.", 1, Fx.Explosion);
            }
            int imp = Mathf.Max(divine ? 2 : 1, dead >= 40 ? 2 : 1);
            _sim.Events.Add(tick, EventKind.Calamity, imp,
                $"Thú triều! {(wolves > 0 ? "Hàng trăm yêu lang" : "Bầy sói đói")} tràn xuống {where}: {dead} người bị cắn chết" +
                (guards.Count > 0 ? $"; {string.Join(", ", guards)} xuất thủ trấn áp" : "") + ".", cx + 0.5f, cy + 0.5f, Fx.Stampede);
        }

        // ---------------------------------------------------------------- thiên kiếp: the land around it

        // Bolts of a tribulation rain on everything near the one facing it: trees burn, roofs cave in,
        // whoever stands too close is struck. Inside a sect at home, the hộ sơn đại trận shields the mortals.
        // Afterwards the ground stays charged with lôi khí for centuries (lôi địa).
        public void TribulationStrikes(Cultivator c, float px, float py, int r, bool divine, bool shielded, long tick, ref DetRandom rng)
        {
            int cx = (int)px, cy = (int)py;
            var objs = _w.Objects;
            int houses = 0, dead = 0;
            _ids.Clear();
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!_w.InBounds(x, y)) continue;
                int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 > r * r) continue;
                int id = objs.CellObject[_w.Idx(x, y)];
                if (id < 0) continue;
                var type = objs.Get(id).Type;
                float k = 1f - Mathf.Sqrt(d2) / r;
                if (IsPlant(type) && rng.NextFloat() < 0.35f * k) objs.Remove(id);
                else if (type == ObjectType.House && !shielded && !_ids.Contains(id)) _ids.Add(id);
            }
            foreach (int id in _ids)
            {
                var o = objs.Get(id);
                float dx = o.X + 1.5f - cx, dy = o.Y + 1.5f - cy;
                float k = 1f - Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (k > 0f && rng.NextFloat() < 0.3f * k)
                {
                    objs.Remove(id);
                    houses++;
                }
            }
            if (!shielded)
            {
                Within(cx, cy, r);
                foreach (var s in _near)
                    dead += _sim.Settlements.Kill(s, Stoch(s.Population * (0.06f * (1f - Dist(s, cx, cy) / r) + 0.01f), ref rng));
            }
            // Lesser cultivators who came to watch, or to rob the one in tribulation.
            var e = _sim.Entities;
            foreach (var o in _sim.Cultivation.All)
            {
                if (o == c || !o.Alive || o.Realm >= Realm.KetDan || !_sim.Cultivation.IsShownOnMap(o)) continue;
                float dx = e.X[o.Entity] - px, dy = e.Y[o.Entity] - py;
                float d2 = dx * dx + dy * dy;
                if (d2 > r * r || rng.NextFloat() >= 0.35f * (1f - Mathf.Sqrt(d2) / r)) continue;
                _sim.Cultivation.Perish(o, tick, $"{o.Name} ({_sim.Cultivation.SectName(o)}) đứng quá gần nơi {c.Name} độ kiếp, bị lôi kiếp đánh tan xác.", 1, Fx.Lightning);
            }
            if (houses + dead > 0)
                _sim.Events.Add(tick, EventKind.Calamity, 1, $"Lôi kiếp của {c.Name} đánh sập {houses} nhà, {dead} phàm nhân thiệt mạng.", px, py, Fx.None, c.Index);

            long years = divine ? rng.Range(300, 601) : rng.Range(200, 401);
            string origin = divine ? $"thiên kiếp Thiên Đạo giáng xuống {c.Name} năm {Year(tick)}" : $"thiên kiếp của {c.Name} năm {Year(tick)}";
            var mark = ThunderScar(cx, cy, Mathf.Max(3, r * 2 / 3), tick, tick + years * SimClock.DaysPerYear, origin, out bool merged, ref rng);
            _sim.Events.Add(tick, EventKind.Calamity, 1,
                merged ? $"Lôi khí ở {mark.Name} càng thêm dày đặc sau lần {c.Name} độ kiếp."
                       : $"Nơi {c.Name} độ kiếp hóa thành lôi địa, người đời gọi là {mark.Name}.", px, py, Fx.None, c.Index, -1, c.SectId);
        }

        Landmark ThunderScar(int cx, int cy, int r, long tick, long until, string origin, out bool merged, ref DetRandom rng)
        {
            Landmark mark = null;
            foreach (var l in Landmarks)
            {
                if (!l.Alive || l.Kind != Landmark.Thunder) continue;
                float d = Mathf.Sqrt((l.X - cx) * (l.X - cx) + (l.Y - cy) * (l.Y - cy));
                if (d > l.R + r * 0.5f) continue;
                mark = l;
                mark.Until = System.Math.Max(mark.Until, until);
                mark.R = Mathf.Max(mark.R, Mathf.CeilToInt(d + r)); // one circle covering both strikes
                break;
            }
            merged = mark != null;
            if (mark == null)
            {
                mark = new Landmark { Kind = Landmark.Thunder, Name = _thunderNames.Next(ref rng), X = cx, Y = cy, R = r, Tick = tick, Until = until, Origin = origin };
                Landmarks.Add(mark);
            }
            StampThunder(mark, true);
            RefreshZone(mark);
            return mark;
        }

        void StampThunder(Landmark l, bool on)
        {
            for (int y = l.Y - l.R; y <= l.Y + l.R; y++)
            for (int x = l.X - l.R; x <= l.X + l.R; x++)
            {
                if (!_w.InBounds(x, y) || (x - l.X) * (x - l.X) + (y - l.Y) * (y - l.Y) > l.R * l.R) continue;
                int i = _w.Idx(x, y);
                if (on) _w.Zone[i] |= ZoneFlags.Thunder;
                else _w.Zone[i] &= unchecked((byte)~ZoneFlags.Thunder);
            }
        }

        void RefreshZone(Landmark l)
        {
            int x0 = l.X - l.R, y0 = l.Y - l.R, x1 = l.X + l.R, y1 = l.Y + l.R;
            RebuildQi(x0, y0, x1, y1);
            _w.NotifyTerrainChanged(Mathf.Max(0, x0), Mathf.Max(0, y0), Mathf.Min(_w.W - 1, x1), Mathf.Min(_w.H - 1, y1)); // repaint the ground
        }

        // Lôi khí thins out over the centuries until the place is ordinary ground again.
        void FadeScars(long tick)
        {
            foreach (var l in Landmarks)
            {
                if (!l.Alive || l.Kind != Landmark.Thunder || tick < l.Until) continue;
                l.Alive = false;
                StampThunder(l, false);
                foreach (var o in Landmarks) // overlapping scars keep their own ground
                    if (o.Alive && o.Kind == Landmark.Thunder && Mathf.Abs(o.X - l.X) <= o.R + l.R && Mathf.Abs(o.Y - l.Y) <= o.R + l.R) StampThunder(o, true);
                RefreshZone(l);
                _sim.Events.Add(tick, EventKind.Calamity, 1,
                    $"Lôi khí ở {l.Name} ({l.Origin}) đã tan hết sau {(tick - l.Tick) / SimClock.DaysPerYear} năm.", l.X + 0.5f, l.Y + 0.5f);
            }
        }

        // The living landmark whose ground (x, y) is on, if any.
        public Landmark LandmarkAt(float x, float y)
        {
            foreach (var l in Landmarks)
                if (l.Alive && (l.X + 0.5f - x) * (l.X + 0.5f - x) + (l.Y + 0.5f - y) * (l.Y + 0.5f - y) <= l.R * l.R) return l;
            return null;
        }

        // ---------------------------------------------------------------- the world's own calamities

        public void MonthlyStep(long tick)
        {
            Recede(tick);
            DroughtStep(tick);
            EpidemicStep(tick);
        }

        public void YearlyStep(long tick)
        {
            CoolLava(tick);
            FadeScars(tick);

            var rng = RngFor(tick, 0x400000);
            if (rng.NextFloat() < QuakePerYear && RandomLand(false, ref rng, out int x, out int y))
                Earthquake(x, y, rng.Range(15, 36), tick, false, ref rng);
            if (rng.NextFloat() < EruptionPerYear && RandomLand(true, ref rng, out x, out y))
                Eruption(x, y, tick, false, ref rng);
            if (rng.NextFloat() < FloodPerYear)
            {
                var s = RandomVillage(true, ref rng);
                if (s != null) Flood(s.X + rng.Range(-8, 9), s.Y + rng.Range(-8, 9), rng.Range(12, 26), tick, false, ref rng);
            }
            if (rng.NextFloat() < DroughtPerYear)
            {
                var s = RandomVillage(false, ref rng);
                if (s != null) StartDrought(s.X, s.Y, rng.Range(60, 121), rng.Range(12, 25), tick, false);
            }
            if (rng.NextFloat() < TidePerYear) NaturalTide(tick, ref rng);
        }

        bool RandomLand(bool highland, ref DetRandom rng, out int x, out int y)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                x = rng.Range(20, _w.W - 20);
                y = rng.Range(20, _w.H - 20);
                var t = _w.Terrain[_w.Idx(x, y)];
                if (highland ? t == Terrain.Mountain || t == Terrain.Hills : TerrainInfo.IsWalkable(t)) return true;
            }
            x = y = 0;
            return false;
        }

        Settlement RandomVillage(bool riverside, ref DetRandom rng)
        {
            _near.Clear();
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && !s.Sect && (!riverside || _w.WaterDist[_w.Idx(s.X, s.Y)] <= 6)) _near.Add(s);
            return _near.Count > 0 ? _near[rng.Range(0, _near.Count)] : null;
        }

        // When the wolves of a region grow too many and hungry, they come down on the nearest village.
        void NaturalTide(long tick, ref DetRandom rng)
        {
            var wild = _sim.Wildlife;
            int best = -1;
            float most = 60f;
            for (int r = 0; r < wild.RW * wild.RH; r++)
            {
                float w = wild.At(Species.Wolf, r);
                if (w > most) { most = w; best = r; }
            }
            if (best < 0) return;
            int rx = (best % wild.RW) * WildlifeSystem.Region + WildlifeSystem.Region / 2;
            int ry = (best / wild.RW) * WildlifeSystem.Region + WildlifeSystem.Region / 2;
            var target = NearestSettlement(rx, ry, 90);
            if (target == null) return;
            BeastTide(target.X, target.Y, 0, tick, false, ref rng);
            wild.Cull(Species.Wolf, best, 0.6f); // many fall to hoe and sword
        }

        // ---------------------------------------------------------------- helpers

        static float Dist(Settlement s, int x, int y) => Mathf.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y));

        // Living settlements within r of (x, y), into _near, in id order.
        void Within(int x, int y, int r)
        {
            _near.Clear();
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) <= r * r) _near.Add(s);
        }

        int PopulationWithin(int x, int y, int r)
        {
            Within(x, y, r);
            int n = 0;
            foreach (var s in _near) n += s.Population;
            return n;
        }

        Settlement NearestSettlement(int x, int y, int reach)
        {
            Settlement best = null;
            int bestD = reach * reach;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                int d = (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y);
                if (d <= bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // "gần Thanh Ngưu Thôn", "quanh Lôi Cốc", or the wilds.
        string PlaceName(int x, int y)
        {
            var s = NearestSettlement(x, y, 60);
            if (s != null) return $"gần {s.Name}";
            var l = LandmarkAt(x, y);
            return l != null ? $"quanh {l.Name}" : "ở vùng hoang vu";
        }

        void RebuildQi(int x0, int y0, int x1, int y1)
        {
            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            x1 = Mathf.Min(_w.W - 1, x1);
            y1 = Mathf.Min(_w.H - 1, y1);
            QiCap.Recompute(_w, x0, y0, x1, y1);
            _sim.Qi.RebuildCapBlocks(x0, y0, x1, y1);
            _w.NotifyQiCapChanged(x0, y0, x1, y1);
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, _flood.Count | ((long)_lava.Count << 32));
            foreach (var l in _lava) StateHash.Add(ref h, l.Cell ^ (l.Cool << 24));
            foreach (var f in _flood) StateHash.Add(ref h, f.Cell ^ (f.Recede << 24));
            foreach (var d in _droughts) StateHash.Add(ref h, d.X | ((long)d.Y << 16) | ((long)d.R << 32) ^ (d.Until << 40));
            foreach (var e in _epidemics) StateHash.Add(ref h, e.Settlement | ((long)e.Dead << 20) ^ (e.Until << 36));
            foreach (var l in Landmarks) StateHash.Add(ref h, l.X | ((long)l.Y << 12) | ((long)l.R << 24) | (l.Alive ? 1L << 40 : 0) ^ (l.Until << 41));
        }
    }
}
