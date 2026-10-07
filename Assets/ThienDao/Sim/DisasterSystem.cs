using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // What Thiên Đạo can send down on an area: calamities, the đại kiếp over the whole world, and the weather.
    public enum Calamity : byte { Earthquake, Eruption, Flood, Drought, Plague, BeastTide, GreatCalamity, Rain, Storm, Cold }

    // A place the land remembers: lôi địa where a tribulation fell, a volcano that rose from the plain.
    public sealed class Landmark
    {
        public const byte Thunder = 0, Volcano = 1, Battlefield = 2;

        public byte Kind;
        public string Name;
        public int X, Y, R;
        public long Tick;
        public long Until = long.MaxValue; // lôi khí fades over centuries; a volcano stays
        public string Origin;              // "thiên kiếp của Hàn Lập năm 203"
        public bool Alive = true;
        public int Toll;                   // chiến trường cổ: the fallen whose oán khí still hangs there
    }

    // Thiên tai (M6): calamities Thiên Đạo sends and those the world brings on itself, and the marks they leave.
    // Everything here is ordinary simulation state: deterministic, hashed, replayable from the command log.
    public sealed class DisasterSystem
    {
        // Yearly chance of each natural calamity somewhere in the world.
        const float QuakePerYear = 0.04f, EruptionPerYear = 0.008f, FloodPerYear = 0.05f, DroughtPerYear = 0.05f, TidePerYear = 0.03f;
        const float StormPerYear = 0.06f, ColdPerYear = 0.04f, GreatPerYear = 0.0015f; // đại kiếp: about once in seven centuries
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

        // A spell of weather over a region: rain (good harvests, ends droughts) or a cold snap (crops fail, the old freeze).
        sealed class Weather
        {
            public Calamity Kind;
            public int X, Y, R;
            public long Start, Until;
            public string Place;
            public int Dead;
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
        readonly List<Weather> _weather = new List<Weather>();
        long _greatStart, _greatUntil = -1;
        int _greatPop, _greatCultivators;
        readonly Dictionary<int, long> _immuneUntil = new Dictionary<int, long>(); // settlement id → no new epidemic before
        readonly List<int> _ids = new List<int>();
        readonly List<Settlement> _near = new List<Settlement>();
        readonly Lore.Picker _thunderNames;
        readonly Lore.Picker _volcanoNames;

        public readonly List<Landmark> Landmarks = new List<Landmark>();

        public DisasterSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _thunderNames = new Lore.Picker(_w.Lore.ThunderPlaces, _w.Lore.Syllables);
            _volcanoNames = new Lore.Picker(_w.Lore.Volcanoes, _w.Lore.Syllables);
        }

        public int DroughtCount => _droughts.Count;
        public int EpidemicCount => _epidemics.Count;
        public int FloodedCells => _flood.Count;
        public int LavaCells => _lava.Count;
        public int WeatherCount => _weather.Count;

        // Đại kiếp: while it lasts, the world's qi sinks to half.
        public bool GreatCalamityActive => _greatUntil >= 0;
        public float QiFactor => GreatCalamityActive ? 0.5f : 1f;
        public int GreatCalamityYearsLeft(long tick) => GreatCalamityActive ? (int)((_greatUntil - tick) / SimClock.DaysPerYear) : 0;

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0xCA1Au, (int)tick, salt));

        // Scars draw from their own stream, so marking the ground never changes what else a calamity rolls.
        DetRandom ScarRng(long tick, int x, int y) => new DetRandom(Hash.U32(_w.Seed ^ 0x5CA4u, (int)tick, x * 1031 + y));

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
                case Calamity.Rain: return Mathf.Clamp(size * 6, 40, 160);
                case Calamity.Storm: return Mathf.Clamp(size, 4, 16); // half-width of the storm's track
                case Calamity.Cold: return Mathf.Clamp(size * 5, 40, 120);
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
                case Calamity.GreatCalamity: StartGreatCalamity(tick, divine, ref rng); break;
                case Calamity.Rain: Rain(x, y, r, rng.Range(4, 9), tick, divine); break;
                case Calamity.Storm: Storm(x, y, r, tick, divine, ref rng); break;
                case Calamity.Cold: ColdSnap(x, y, r, rng.Range(3, 6), tick, divine); break;
            }
        }

        // ---------------------------------------------------------------- đại kiếp

        // Thiên địa đại kiếp: for years the world's qi runs thin and calamity follows calamity, until a new age begins.
        void StartGreatCalamity(long tick, bool divine, ref DetRandom rng)
        {
            if (GreatCalamityActive) return;
            _greatStart = tick;
            _greatUntil = tick + rng.Range(8, 16) * (long)SimClock.DaysPerYear;
            _greatPop = _sim.Settlements.TotalPopulation;
            _greatCultivators = _sim.Cultivation.AliveCount;
            _sim.Events.Add(tick, EventKind.Calamity, 3,
                $"{(divine ? "Thiên Đạo mở ra t" : "T")}hiên địa đại kiếp! Linh khí khô kiệt, tai ương liên miên khắp thiên hạ.");
        }

        void GreatCalamityYear(long tick, ref DetRandom rng)
        {
            if (!GreatCalamityActive) return;
            if (tick >= _greatUntil)
            {
                int years = (int)((tick - _greatStart) / SimClock.DaysPerYear);
                _sim.Events.Add(tick, EventKind.Calamity, 3,
                    $"Đại kiếp qua đi sau {years} năm: phàm nhân từ {_greatPop:N0} còn {_sim.Settlements.TotalPopulation:N0}, " +
                    $"tu sĩ từ {_greatCultivators} còn {_sim.Cultivation.AliveCount}. Thiên địa bước vào thời đại mới.");
                _greatUntil = -1;
                return;
            }
            int n = rng.Range(2, 6);
            for (int k = 0; k < n; k++)
            {
                var s = RandomVillage(false, ref rng);
                switch (rng.Range(0, 6))
                {
                    case 0:
                        if (RandomLand(false, ref rng, out int x, out int y)) Earthquake(x, y, rng.Range(20, 46), tick, false, ref rng);
                        break;
                    case 1:
                        if (s != null) StartDrought(s.X, s.Y, rng.Range(60, 141), rng.Range(12, 30), tick, false);
                        break;
                    case 2:
                        if (s != null) Flood(s.X + rng.Range(-8, 9), s.Y + rng.Range(-8, 9), rng.Range(14, 30), tick, false, ref rng);
                        break;
                    case 3:
                        if (s != null) StartEpidemic(s, tick, false);
                        break;
                    case 4:
                        if (s != null) BeastTide(s.X, s.Y, rng.Range(80, 200), tick, false, ref rng);
                        break;
                    default:
                        if (rng.NextFloat() < 0.3f && RandomLand(true, ref rng, out x, out y)) Eruption(x, y, tick, false, ref rng);
                        else if (s != null) ColdSnap(s.X, s.Y, rng.Range(50, 110), rng.Range(3, 6), tick, false);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- thời tiết

        // Mưa thuận gió hòa: a few months of good rain; any drought it falls on is broken.
        void Rain(int x, int y, int r, int months, long tick, bool divine)
        {
            bool broke = false;
            foreach (var d in _droughts)
            {
                float dist = Mathf.Sqrt((d.X - x) * (d.X - x) + (d.Y - y) * (d.Y - y));
                if (dist > r + d.R * 0.5f || d.Until <= tick) continue;
                d.Until = tick; // ends (with its toll told) at the next month
                broke = true;
            }
            _weather.Add(new Weather { Kind = Calamity.Rain, X = x, Y = y, R = r, Start = tick, Until = tick + months * (long)SimClock.DaysPerMonth, Place = PlaceName(x, y) });
            if (divine) _sim.Faith?.OnRain(x, y, r, tick); // the rain they prayed for
            _sim.Events.Add(tick, EventKind.Calamity, divine ? 2 : 1,
                $"{(divine ? "Thiên Đạo ban mưa, m" : "M")}ưa thuận gió hòa {PlaceName(x, y)}{(broke ? ", đại hạn chấm dứt" : "")}.", x + 0.5f, y + 0.5f, Fx.Rain);
        }

        // A cold snap: frost kills the crops, the old and the infants die of cold, grass withers.
        void ColdSnap(int x, int y, int r, int months, long tick, bool divine)
        {
            _weather.Add(new Weather { Kind = Calamity.Cold, X = x, Y = y, R = r, Start = tick, Until = tick + months * (long)SimClock.DaysPerMonth, Place = PlaceName(x, y) });
            _sim.Events.Add(tick, EventKind.Calamity, 2,
                $"{(divine ? "Thiên Đạo giáng hàn khí, r" : "R")}ét đậm rét hại {PlaceName(x, y)}: tuyết phủ ruộng đồng, mùa màng mất trắng.", x + 0.5f, y + 0.5f, Fx.Snow);
        }

        // A storm tears across the land along a track: trees go down, roofs come off, people are caught in the open.
        void Storm(int x, int y, int w, long tick, bool divine, ref DetRandom rng)
        {
            string where = PlaceName(x, y);
            var objs = _w.Objects;
            float a = rng.Range(0f, Mathf.PI * 2f);
            int len = rng.Range(60, 121), trees = 0, houses = 0, dead = 0;
            float fx = x + 0.5f, fy = y + 0.5f;
            _ids.Clear();
            var hit = new List<Settlement>();
            var harm = _sim.Harm;
            harm.Clear();
            var srng = ScarRng(tick, x, y);
            for (int step = 0; step < len; step += 2)
            {
                a += rng.Range(-0.25f, 0.25f);
                fx += Mathf.Cos(a) * 2f;
                fy += Mathf.Sin(a) * 2f;
                int cx = (int)fx, cy = (int)fy;
                if (!_w.InBounds(cx, cy)) break;
                if (step % 6 == 0) harm.Add(fx, fy, w + 1.5f); // the storm's track, for whoever is caught in it
                // Lightning out of the storm leaves burnt spots along its track.
                if (srng.NextFloat() < 0.08f)
                    _sim.Scars.Disc(cx + srng.Range(-w, w + 1), cy + srng.Range(-w, w + 1), srng.Range(1f, 2.5f), ScarKind.Scorch, 12, 6, (uint)step);
                for (int yy = cy - w; yy <= cy + w; yy++)
                for (int xx = cx - w; xx <= cx + w; xx++)
                {
                    if (!_w.InBounds(xx, yy) || (xx - cx) * (xx - cx) + (yy - cy) * (yy - cy) > w * w) continue;
                    int id = objs.CellObject[_w.Idx(xx, yy)];
                    if (id < 0) continue;
                    var t = objs.Get(id).Type;
                    if (IsPlant(t) && rng.NextFloat() < 0.08f)
                    {
                        objs.Remove(id);
                        trees++;
                    }
                    else if (t == ObjectType.House && !_ids.Contains(id)) _ids.Add(id);
                }
                Within(cx, cy, w + 4);
                foreach (var s in _near)
                    if (!hit.Contains(s)) hit.Add(s);
            }
            foreach (int id in _ids)
                if (objs.IsAlive(id) && rng.NextFloat() < 0.3f)
                {
                    objs.Remove(id);
                    houses++;
                }
            foreach (var s in hit) dead += _sim.Settlements.Kill(s, Stoch(s.Population * 0.03f, ref rng));
            string hurt = HarmSystem.Tail(harm.Strike(divine ? 250f : 120f, tick, "bị cuồng phong cuốn đi", false));
            _sim.Events.Add(tick, EventKind.Calamity, Mathf.Max(divine ? 2 : 1, dead >= 20 ? 2 : 1),
                $"{(divine ? "Thiên Đạo nổi cuồng phong, b" : "B")}ão lớn quét qua {where}: {houses} nhà tốc mái, {dead} người chết, {trees} cây đổ{hurt}.",
                x + 0.5f, y + 0.5f, Fx.Storm);
        }

        void WeatherStep(long tick)
        {
            var forage = _sim.Forage;
            for (int k = _weather.Count - 1; k >= 0; k--)
            {
                var w = _weather[k];
                if (tick >= w.Until)
                {
                    _weather.RemoveAt(k);
                    if (w.Kind == Calamity.Cold)
                        _sim.Events.Add(tick, EventKind.Calamity, w.Dead >= 30 ? 2 : 1, $"Đợt rét {w.Place} qua đi, {w.Dead} người chết cóng.", w.X + 0.5f, w.Y + 0.5f);
                    continue;
                }
                // Rain greens the grass; frost kills it.
                int b = ForageSystem.Block;
                int bx0 = Mathf.Max(0, (w.X - w.R) / b), bx1 = Mathf.Min(forage.BW - 1, (w.X + w.R) / b);
                int by0 = Mathf.Max(0, (w.Y - w.R) / b), by1 = Mathf.Min(forage.BH - 1, (w.Y + w.R) / b);
                float f = w.Kind == Calamity.Rain ? 1.15f : 0.5f;
                for (int by = by0; by <= by1; by++)
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    int dx = bx * b + b / 2 - w.X, dy = by * b + b / 2 - w.Y;
                    if (dx * dx + dy * dy <= w.R * w.R) forage.Scale(bx, by, 1, f);
                }
                if (w.Kind != Calamity.Cold) continue;
                var rng = RngFor(tick, 0x500000 + k);
                Within(w.X, w.Y, w.R);
                foreach (var s in _near) w.Dead += _sim.Settlements.Kill(s, Stoch(s.Population * 0.015f, ref rng));
            }
        }

        // ---------------------------------------------------------------- lôi địa from Thiên Đạo's wrath

        public void WrathScar(int x, int y, int r, long tick, string origin)
        {
            var rng = RngFor(tick, 0x600000 + x * 31 + y);
            Blasted(x, y, r + 3, tick);
            var mark = ThunderScar(x, y, r, tick, tick + rng.Range(300, 601) * (long)SimClock.DaysPerYear, origin, out bool merged, ref rng);
            if (!merged)
                _sim.Events.Add(tick, EventKind.Calamity, 2, $"Nơi thiên phạt giáng xuống hóa thành lôi địa, người đời gọi là {mark.Name}.", x + 0.5f, y + 0.5f);
        }

        // Where heaven's lightning came down: a crater in the middle, scorched ground all around.
        public void Blasted(int x, int y, int r, long tick)
        {
            uint salt = (uint)tick ^ (uint)(x * 31 + y);
            _sim.Scars.Disc(x, y, r, ScarKind.Scorch, 15, 6, salt);
            _sim.Scars.Disc(x, y, Mathf.Max(2f, r * 0.32f), ScarKind.Crater, 15, 9, salt + 7u);
            // Trees left standing are burnt to black stumps.
            var srng = ScarRng(tick, x, y);
            var objs = _w.Objects;
            for (int yy = y - r; yy <= y + r; yy++)
            for (int xx = x - r; xx <= x + r; xx++)
            {
                if (!_w.InBounds(xx, yy)) continue;
                float d = Mathf.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                if (d > r) continue;
                int id = objs.CellObject[_w.Idx(xx, yy)];
                if (id < 0) continue;
                var o = objs.Get(id);
                if (!IsPlant(o.Type) || o.Type == ObjectType.TreeDead || srng.NextFloat() >= (d < r * 0.75f ? 0.95f : 0.5f)) continue;
                objs.Remove(id);
                if (d > r * 0.32f) objs.Place(ObjectType.TreeDead, o.X, o.Y, (byte)srng.Range(0, 256));
            }
        }

        // What hangs over a cell right now, for the calamity overlay: 1 drought, 2 rain, 4 cold, 8 epidemic nearby.
        public int ClimateAt(int x, int y)
        {
            int f = 0;
            foreach (var d in _droughts)
                if ((d.X - x) * (d.X - x) + (d.Y - y) * (d.Y - y) <= d.R * d.R) f |= 1;
            foreach (var w in _weather)
                if ((w.X - x) * (w.X - x) + (w.Y - y) * (w.Y - y) <= w.R * w.R) f |= w.Kind == Calamity.Rain ? 2 : 4;
            var all = _sim.Settlements.All;
            foreach (var e in _epidemics)
            {
                var s = all[e.Settlement];
                if ((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) <= 14 * 14) f |= 8;
            }
            return f;
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
            // Whoever stands on the ground is thrown down and buried; those in the air ride it out.
            var harm = _sim.Harm;
            harm.Clear();
            harm.Add(cx + 0.5f, cy + 0.5f, r);
            string hurt = HarmSystem.Tail(harm.Strike(divine ? 400f : 200f, tick, "bị đất đá vùi lấp", true));
            if (ley > 0) RebuildQi(cx - r - QiCap.LeyReach, cy - r - QiCap.LeyReach, cx + r + QiCap.LeyReach, cy + r + QiCap.LeyReach);
            // The ground splits in long cracks running out from the epicentre.
            var srng = ScarRng(tick, cx, cy);
            int cracks = srng.Range(3, 5) + r / 20;
            for (int k = 0; k < cracks; k++)
                _sim.Scars.Trail(cx + 0.5f, cy + 0.5f, srng.Range(0f, Mathf.PI * 2f), srng.Range(r / 2, r + 1), 0.45f, ScarKind.Fissure, divine ? 15 : 13, ref srng);
            int imp = Mathf.Max(divine ? 2 : 1, dead >= 30 || ley >= 10 ? 2 : 1);
            _sim.Events.Add(tick, EventKind.Calamity, imp,
                $"{(divine ? "Thiên Đạo nổi giận, địa long" : "Địa long")} trở mình {where}: {houses} nhà sập, {dead} người chết" +
                (ley > 0 ? $", {ley} đoạn linh mạch đứt gãy" : "") + hurt + ".", cx + 0.5f, cy + 0.5f, Fx.Quake);
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

            // Grey ash over the country around, the burnt ring at the foot, the cone itself black rock.
            var scars = _sim.Scars;
            uint salt = (uint)tick ^ (uint)(cx * 31 + cy);
            scars.Disc(cx, cy, ash, ScarKind.Ash, 11, 2, salt);
            scars.Disc(cx, cy, burn, ScarKind.Scorch, 14, 5, salt + 1u);
            for (int y = cy - cone; y <= cy + cone; y++)
            for (int x = cx - cone; x <= cx + cone; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= cone * cone) scars.Set(_w.Idx(x, y), ScarKind.Basalt, 15);

            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            x1 = Mathf.Min(_w.W - 1, x1);
            y1 = Mathf.Min(_w.H - 1, y1);
            _w.NotifyTerrainChanged(x0, y0, x1, y1);
            RebuildQi(x0 - QiCap.LeyReach, y0 - QiCap.LeyReach, x1 + QiCap.LeyReach, y1 + QiCap.LeyReach);
            _sim.Wildlife.LandChanged(x0, y0, x1, y1);
            _sim.ResolveFlood(x0, y0, x1, y1); // whoever stands in the lava burns
            // Fire and falling rock scour the foot of the mountain; the living there burn or are wounded.
            var harm = _sim.Harm;
            harm.Clear();
            harm.Add(cx + 0.5f, cy + 0.5f, burn);
            string hurt = HarmSystem.Tail(harm.Strike(900f, tick, "bị dung nham và đá lửa thiêu chết", false));

            var volcano = new Landmark
            {
                Kind = Landmark.Volcano, Name = name, X = cx, Y = cy, R = cone + 2, Tick = tick,
                Origin = divine ? $"núi lửa Thiên Đạo gọi lên năm {Year(tick)}" : $"núi lửa phun trào năm {Year(tick)}"
            };
            Landmarks.Add(volcano);
            _sim.Relics?.OnLandmark(volcano, tick);
            _sim.Events.Add(tick, EventKind.Calamity, 3,
                $"Núi lửa phun trào {where}{(divine ? " theo ý Thiên Đạo" : "")}, {name} mọc lên giữa trời đất: {dead} người chết vì tro bụi" +
                (houses > 0 ? $", {houses} nhà bị thiêu rụi" : "") + hurt + ".", cx + 0.5f, cy + 0.5f, Fx.Eruption);
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
                _sim.Scars.Set(l.Cell, ScarKind.Basalt, 15); // the flow sets into dark rock
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
                _sim.Scars.Set(f.Cell, ScarKind.Silt, 12); // the water leaves its mud behind
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
            _sim.Scars.Disc(x, y, r * 0.8f, ScarKind.Parched, Mathf.Clamp(months / 3 + 3, 4, 9), 2, (uint)tick ^ (uint)(x * 31 + y));
            _sim.Events.Add(tick, EventKind.Calamity, 2,
                $"{(divine ? "Thiên Đạo khóa mây, đ" : "Đ")}ại hạn {d.Place}: trời không mưa, ruộng đồng nứt nẻ.", x + 0.5f, y + 0.5f, Fx.None);
        }

        // Share of the usual harvest a village at (x, y) brings in this month.
        public float HarvestFactor(int x, int y)
        {
            float f = 1f;
            foreach (var d in _droughts)
                if ((d.X - x) * (d.X - x) + (d.Y - y) * (d.Y - y) <= d.R * d.R) { f = 0.25f; break; }
            foreach (var w in _weather)
                if ((w.X - x) * (w.X - x) + (w.Y - y) * (w.Y - y) <= w.R * w.R) f *= w.Kind == Calamity.Rain ? 1.3f : 0.1f;
            return f;
        }

        // Months of a weather spell (rain or cold) left over (x, y), or -1.
        public int WeatherMonthsLeft(int x, int y, long tick, out Calamity kind)
        {
            kind = Calamity.Rain;
            int best = -1;
            foreach (var w in _weather)
                if ((w.X - x) * (w.X - x) + (w.Y - y) * (w.Y - y) <= w.R * w.R && (w.Until - tick) / SimClock.DaysPerMonth > best)
                {
                    best = (int)((w.Until - tick) / SimClock.DaysPerMonth);
                    kind = w.Kind;
                }
            return best;
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
                Settlement = s.Id, Until = tick + rng.Range(3, divine ? 9 : 7) * (long)SimClock.DaysPerMonth, Severity = (divine ? 0.06f : 0.04f) * (_sim.Settlements.HasCivic(s, ObjectType.Shrine) ? 0.7f : 1f) // the miếu calms and tends
            });
            _sim.Events.Add(tick, EventKind.Calamity, divine ? 2 : 1,
                $"{(divine ? "Thiên Đạo giáng ôn thần, ô" : "Ô")}n dịch bùng phát ở {s.Name}.", s.X + 0.5f, s.Y + 0.5f, Fx.Miasma);
        }

        // Thiên Đạo's blessing drives the sickness out of a village: it ends with this month's tally.
        public bool Cure(int settlement, long tick)
        {
            bool any = false;
            foreach (var e in _epidemics)
                if (e.Settlement == settlement && !e.Done && e.Until > tick) { e.Until = tick; any = true; }
            return any;
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

        // A yêu tộc led by its Yêu Vương comes down on the villages (BeastSystem).
        public void TideOf(string clan, int x, int y, int wolves, long tick)
        {
            var rng = RngFor(tick, 0x700000 + x * 31 + y);
            BeastTide(x, y, wolves, tick, false, ref rng, clan);
        }

        // Wolves pour out of the wild onto the villages; a sect guarding the land beats them back.
        void BeastTide(int cx, int cy, int wolves, long tick, bool divine, ref DetRandom rng, string clan = null)
        {
            string where = PlaceName(cx, cy);
            if (wolves > 0) _sim.Wildlife.Add(Species.Wolf, cx + 0.5f, cy + 0.5f, wolves);
            _sim.Scars.Disc(cx, cy, 16f, ScarKind.Trampled, 9, 3, (uint)tick ^ (uint)(cx * 31 + cy));
            int dead = 0;
            var guards = new List<string>();
            var walls = new List<string>();
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
                if (s.Walled)
                {
                    share *= 0.4f; // the gates shut
                    if (!walls.Contains(s.Name)) walls.Add(s.Name);
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
                $"Thú triều! {(clan != null ? $"Yêu thú {clan}" : wolves > 0 ? "Hàng trăm yêu lang" : "Bầy sói đói")} tràn xuống {where}: {dead} người bị cắn chết" +
                (guards.Count > 0 ? $"; {string.Join(", ", guards)} xuất thủ trấn áp" : "") +
                (walls.Count > 0 ? $"; {string.Join(", ", walls)} đóng cổng thành cố thủ" : "") + ".", cx + 0.5f, cy + 0.5f, Fx.Stampede);
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

            Blasted(cx, cy, r, tick);
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
                _sim.Relics?.OnLandmark(mark, tick);
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

        // ---------------------------------------------------------------- chiến trường cổ

        // Where cultivators fell in battle the land keeps their oán khí: a landmark that grows with every battle
        // fought on it, feeds ma tu, and in time condenses into yêu thú. It fades as the battlefield scar heals.
        public void MarkBattlefield(int x, int y, int r, int dead, long tick, string origin)
        {
            if (dead <= 0) return;
            Landmark field = null;
            foreach (var l in Landmarks)
                if (l.Alive && l.Kind == Landmark.Battlefield && (l.X - x) * (l.X - x) + (l.Y - y) * (l.Y - y) <= (l.R + r + 12) * (l.R + r + 12)) { field = l; break; } // one field for one contested land
            long until = tick + 120L * SimClock.DaysPerYear;
            if (field != null)
            {
                field.Toll += dead;
                field.Until = System.Math.Max(field.Until, until);
                field.R = Mathf.Max(field.R, r);
                if (field.Toll >= 10 && field.Toll - dead < 10)
                    _sim.Events.Add(tick, EventKind.Calamity, 2, $"Máu đổ lần nữa ở {field.Name}: oán khí nơi đây ngày càng nặng.", x + 0.5f, y + 0.5f);
                return;
            }
            var near = NearestSettlement(x, y, 80);
            field = new Landmark
            {
                Kind = Landmark.Battlefield, Name = $"Cổ chiến trường {(near != null ? near.BaseName : _volcanoNames.Next(ref _fieldRng))}",
                X = x, Y = y, R = r, Tick = tick, Until = until, Origin = origin, Toll = dead
            };
            Landmarks.Add(field);
            _sim.Relics?.OnLandmark(field, tick);
        }

        DetRandom _fieldRng = new DetRandom(0xF1E1Du);
        const int MaxGrudgeBeasts = 40; // with this many yêu thú abroad, oán khí lies dormant

        void BattlefieldsStep(long tick)
        {
            for (int k = 0, n = Landmarks.Count; k < n; k++)
            {
                var l = Landmarks[k];
                if (!l.Alive || l.Kind != Landmark.Battlefield) continue;
                if (tick >= l.Until)
                {
                    l.Alive = false;
                    _sim.Events.Add(tick, EventKind.Calamity, 1, $"Oán khí ở {l.Name} ({l.Origin}) đã tan, cỏ lại mọc xanh.", l.X + 0.5f, l.Y + 0.5f);
                    continue;
                }
                // The heavier the toll, the likelier the dead's resentment takes a shape.
                var rng = RngFor(tick, 0x800000 + k);
                if (l.Toll < 8 || _sim.Beasts.AliveCount >= MaxGrudgeBeasts || rng.NextFloat() >= Mathf.Min(0.06f, l.Toll * 0.002f)) continue;
                int grade = 1 + (l.Toll >= 12 ? 1 : 0) + (l.Toll >= 30 ? 1 : 0);
                float bx = l.X + 0.5f + rng.Range(-l.R, l.R + 1), by = l.Y + 0.5f + rng.Range(-l.R, l.R + 1);
                if (!_w.IsWalkable(bx, by)) continue;
                var beast = _sim.Beasts.Spawn(Species.Wolf, grade, bx, by, tick, ref rng);
                l.Toll = l.Toll * 2 / 3; // some of the resentment went into it
                _sim.Events.Add(tick, EventKind.Beast, 2, $"Oán khí ở {l.Name} ngưng tụ, hóa thành {beast.Name} ({beast.GradeText}).", bx, by, Fx.Miasma);
            }
        }

        public Landmark HeaviestBattlefield(float x, float y, float reach)
        {
            Landmark best = null;
            foreach (var l in Landmarks)
            {
                if (!l.Alive || l.Kind != Landmark.Battlefield || l.Toll < 5) continue;
                float dx = l.X + 0.5f - x, dy = l.Y + 0.5f - y;
                if (dx * dx + dy * dy > reach * reach) continue;
                if (best == null || l.Toll > best.Toll) best = l;
            }
            return best;
        }

        // The oán khí over (x, y): 0 none .. 1 thick (ma tu cultivate faster on it).
        public float GrudgeAt(float x, float y)
        {
            foreach (var l in Landmarks)
            {
                if (!l.Alive || l.Kind != Landmark.Battlefield) continue;
                float dx = l.X + 0.5f - x, dy = l.Y + 0.5f - y;
                if (dx * dx + dy * dy <= l.R * l.R) return Mathf.Min(1f, l.Toll / 20f);
            }
            return 0f;
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
            WeatherStep(tick);
            EpidemicStep(tick);
        }

        public void YearlyStep(long tick)
        {
            CoolLava(tick);
            BattlefieldsStep(tick);
            FadeScars(tick);

            var rng = RngFor(tick, 0x400000);
            float k = _sim.Rules[Rule.Calamities];
            if (rng.NextFloat() < QuakePerYear * k && RandomLand(false, ref rng, out int x, out int y))
                Earthquake(x, y, rng.Range(15, 36), tick, false, ref rng);
            if (rng.NextFloat() < EruptionPerYear * k && RandomLand(true, ref rng, out x, out y))
                Eruption(x, y, tick, false, ref rng);
            if (rng.NextFloat() < FloodPerYear * k)
            {
                var s = RandomVillage(true, ref rng);
                if (s != null) Flood(s.X + rng.Range(-8, 9), s.Y + rng.Range(-8, 9), rng.Range(12, 26), tick, false, ref rng);
            }
            if (rng.NextFloat() < DroughtPerYear * k)
            {
                var s = RandomVillage(false, ref rng);
                if (s != null) StartDrought(s.X, s.Y, rng.Range(60, 121), rng.Range(12, 25), tick, false);
            }
            if (rng.NextFloat() < TidePerYear * k) NaturalTide(tick, ref rng);
            // Weather the world makes for itself: storms and cold snaps now and then, and rain that may break a drought.
            if (rng.NextFloat() < StormPerYear * k)
            {
                var s = RandomVillage(false, ref rng);
                if (s != null) Storm(s.X + rng.Range(-40, 41), s.Y + rng.Range(-40, 41), rng.Range(4, 11), tick, false, ref rng);
            }
            if (rng.NextFloat() < ColdPerYear * k)
            {
                var s = RandomVillage(false, ref rng);
                if (s != null) ColdSnap(s.X, s.Y, rng.Range(50, 101), rng.Range(2, 5), tick, false);
            }
            if (_droughts.Count > 0 && rng.NextFloat() < 0.3f)
            {
                var d = _droughts[rng.Range(0, _droughts.Count)];
                Rain(d.X, d.Y, d.R, rng.Range(3, 7), tick, false);
            }
            if (!GreatCalamityActive && rng.NextFloat() < GreatPerYear * k) StartGreatCalamity(tick, false, ref rng);
            GreatCalamityYear(tick, ref rng);
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
            foreach (var w in _weather) StateHash.Add(ref h, (int)w.Kind | ((long)w.X << 8) | ((long)w.Y << 20) ^ (w.Until << 32) ^ w.Dead);
            StateHash.Add(ref h, _greatUntil);
        }
    }
}
