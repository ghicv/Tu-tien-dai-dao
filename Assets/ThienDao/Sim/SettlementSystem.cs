using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    public sealed class Settlement
    {
        public const int AgeGroups = 17; // 5-year groups; the last one is 80+

        public int Id;
        public string BaseName;
        public int X, Y;
        public byte Roof;
        public bool Sect;
        public bool Alive = true;
        public long FoundedTick;
        public int ParentId = -1;

        public readonly int[] Cohorts = new int[AgeGroups];
        public float Food;
        public readonly List<int> Houses = new List<int>();
        public readonly List<int> Farms = new List<int>(); // cell indices

        public float LastHarvest, LastHunt;
        public int BirthsLastYear, DeathsLastYear, StarvedThisYear;

        public int Population
        {
            get
            {
                int s = 0;
                foreach (int c in Cohorts) s += c;
                return s;
            }
        }

        // A sect town carries its sect's name; other places are styled Thôn / Trấn / Thành by size.
        public string Name => Sect ? BaseName : BaseName + " " + Lore.Tier(Population);

        public int Workers => Sum(3, 11);       // 15–59
        public int FertileAdults => Sum(3, 8);  // 15–44
        public int HousingCapacity => Houses.Count * SettlementSystem.PeoplePerHouse;

        int Sum(int from, int to)
        {
            int s = 0;
            for (int b = from; b <= to; b++) s += Cohorts[b];
            return s;
        }
    }

    // Villages as populations: food from farms and hunting, births and deaths by age group,
    // houses and farmland that grow or shrink with the people, and migrant groups that found new villages.
    public sealed class SettlementSystem
    {
        public const int PeoplePerHouse = 6;
        public const int MaxSettlements = 150;
        const int CrowdedPopulation = 800;     // births fade out as a village approaches this size
        const float PlagueChancePerYear = 0.015f;
        const float CellsPerWorker = 2.5f;
        const float YieldPerFertility = 1.4f;

        static readonly float[] Mortality =
        {
            0.05f, 0.008f, 0.006f, 0.006f, 0.006f, 0.006f, 0.006f, 0.007f, 0.008f,
            0.01f, 0.015f, 0.025f, 0.04f, 0.07f, 0.12f, 0.2f, 0.35f
        };

        // Starvation takes the youngest and oldest first.
        static readonly int[] FrailtyOrder = { 0, 16, 15, 14, 13, 1, 12, 11, 2, 10, 3, 9, 4, 8, 5, 7, 6 };

        readonly Lore.Picker _placeNames = new Lore.Picker(Lore.Places);
        readonly Lore.Picker _sectNames = new Lore.Picker(Lore.Sects);

        sealed class MigrantGroup
        {
            public int Entity;
            public int From;
            public byte Roof;
            public float Food;
            public readonly int[] Cohorts = new int[Settlement.AgeGroups];
        }

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly Dictionary<int, int> _houseOwner = new Dictionary<int, int>(); // object id → settlement id
        readonly List<MigrantGroup> _groups = new List<MigrantGroup>();

        public readonly List<Settlement> All = new List<Settlement>();
        public int AliveCount { get; private set; }
        public int MigrantGroups => _groups.Count;

        public SettlementSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _w.Objects.Removed += OnObjectRemoved;

            var rng = new DetRandom(_w.Seed ^ 0x5E771Eu);
            foreach (var site in _w.VillageSites)
            {
                var s = Create(site.X, site.Y, site.Roof, site.Sect, 0, ref rng);
                foreach (int h in site.Houses) AdoptHouse(s, h);
                int pop = site.Houses.Count * rng.Range(4, 6);
                DistributeInitial(s, pop, ref rng);
                s.Food = pop * 6f;
                ClaimFarmland(s, (int)(s.Workers * CellsPerWorker));
            }
        }

        public int TotalPopulation
        {
            get
            {
                int n = 0;
                foreach (var s in All)
                    if (s.Alive) n += s.Population;
                foreach (var g in _groups)
                    foreach (int c in g.Cohorts) n += c;
                return n;
            }
        }

        public Settlement Owning(int cell)
        {
            int o = _w.Owner[cell];
            return o > 0 && o <= All.Count ? All[o - 1] : null;
        }

        // The village a house or sect hall belongs to, if any.
        public Settlement OwnerOfObject(int objectId) =>
            _houseOwner.TryGetValue(objectId, out int id) && id >= 0 && id < All.Count ? All[id] : null;

        // Read-only view of a travelling migrant group, for the inspector.
        public bool MigrantInfo(int entity, out Settlement from, out int people, out float food)
        {
            from = null;
            people = 0;
            food = 0f;
            var g = _groups.Find(x => x.Entity == entity);
            if (g == null) return false;
            from = g.From >= 0 && g.From < All.Count ? All[g.From] : null;
            foreach (int c in g.Cohorts) people += c;
            food = g.Food;
            return true;
        }

        static int Stoch(float v, ref DetRandom rng)
        {
            int whole = (int)v;
            return whole + (rng.NextFloat() < v - whole ? 1 : 0);
        }

        static float HarvestFactor(Season s)
        {
            switch (s)
            {
                case Season.Xuan: return 0.6f;
                case Season.Ha: return 1f;
                case Season.Thu: return 1.5f;
                default: return 0.2f;
            }
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0x5E7u, (int)tick, salt));

        // ---------------------------------------------------------------- lifecycle

        Settlement Create(int x, int y, byte roof, bool sect, long tick, ref DetRandom rng)
        {
            var s = new Settlement
            {
                Id = All.Count,
                BaseName = sect ? _sectNames.Next(ref rng) : _placeNames.Next(ref rng),
                X = x,
                Y = y,
                Roof = roof,
                Sect = sect,
                FoundedTick = tick
            };
            All.Add(s);
            AliveCount++;
            return s;
        }

        static void DistributeInitial(Settlement s, int pop, ref DetRandom rng)
        {
            float total = 0f;
            var weight = new float[Settlement.AgeGroups];
            for (int b = 0; b < weight.Length; b++) total += weight[b] = Mathf.Exp(-0.11f * b);
            for (int p = 0; p < pop; p++)
            {
                float roll = rng.NextFloat() * total;
                int b = 0;
                while (b < weight.Length - 1 && roll > weight[b]) roll -= weight[b++];
                s.Cohorts[b]++;
            }
        }

        void Abandon(Settlement s)
        {
            s.Alive = false;
            AliveCount--;
            if (s.Sect) _sim.Cultivation.DisbandSect(s.Id, _sim.Clock.Tick); // its hall stays behind as a ruin
            for (int k = s.Houses.Count - 1; k >= 0; k--) _w.Objects.Remove(s.Houses[k]);
            ReleaseFarmland(s, s.Farms.Count);
        }

        public Settlement FoundVillage(int x, int y, byte roof, int people, long tick)
        {
            if (!_w.InBounds(x, y) || !TerrainInfo.IsWalkable(_w.Terrain[_w.Idx(x, y)]) || AliveCount >= MaxSettlements) return null;
            var rng = RngFor(tick, x * 4099 + y);
            var s = Create(x, y, roof, false, tick, ref rng);
            DistributeInitial(s, people, ref rng);
            s.Food = people * 6f;
            for (int k = 0; k < 3; k++) TryBuildHouse(s, ref rng);
            ClaimFarmland(s, (int)(s.Workers * CellsPerWorker));
            return s;
        }

        // ---------------------------------------------------------------- monthly / yearly

        public void MonthlyStep(long tick)
        {
            float season = HarvestFactor(_sim.Clock.Season);
            int count = All.Count;
            for (int k = 0; k < count; k++)
            {
                var s = All[k];
                if (!s.Alive) continue;
                var rng = RngFor(tick, s.Id);
                PruneFarms(s);
                int pop = s.Population;
                if (pop == 0)
                {
                    Abandon(s);
                    continue;
                }
                if (!CanSettleAt(s.X, s.Y))
                {
                    Displace(s, tick, ref rng);
                    if (!s.Alive) continue;
                }

                int workers = s.Workers;
                int tended = Mathf.Min(s.Farms.Count, (int)(workers * CellsPerWorker));
                float harvest = 0f;
                for (int f = 0; f < tended; f++) harvest += _w.Fertility(s.Farms[f]);
                harvest *= YieldPerFertility * season;
                float meat = _sim.Wildlife.Hunt(s.X, s.Y);
                s.LastHarvest = harvest;
                s.LastHunt = meat;

                s.Food = Mathf.Min(s.Food + harvest + meat - pop, pop * 12f);
                if (s.Food < 0f)
                {
                    float shortage = Mathf.Min(1f, -s.Food / pop);
                    int dead = Stoch(pop * 0.06f * shortage, ref rng);
                    RemovePeople(s, dead);
                    s.StarvedThisYear += dead;
                    s.Food = 0f;
                }

                int wantFarms = (int)(workers * CellsPerWorker);
                if (s.Farms.Count < wantFarms && s.Food < pop * 6f) ClaimFarmland(s, Mathf.Min(8, wantFarms - s.Farms.Count));
                else if (s.Farms.Count > wantFarms * 1.4f + 10) ReleaseFarmland(s, Mathf.Min(6, s.Farms.Count - wantFarms));
            }
        }

        public void YearlyStep(long tick)
        {
            int count = All.Count;
            for (int k = 0; k < count; k++)
            {
                var s = All[k];
                if (!s.Alive) continue;
                var rng = RngFor(tick, s.Id + 100000);

                for (int b = Settlement.AgeGroups - 2; b >= 0; b--)
                {
                    int moved = Stoch(s.Cohorts[b] / 5f, ref rng);
                    s.Cohorts[b] -= moved;
                    s.Cohorts[b + 1] += moved;
                }
                int deaths = 0;
                for (int b = 0; b < Settlement.AgeGroups; b++)
                {
                    int d = Mathf.Min(s.Cohorts[b], Stoch(s.Cohorts[b] * Mortality[b], ref rng));
                    s.Cohorts[b] -= d;
                    deaths += d;
                }

                int pop = s.Population;
                s.DeathsLastYear = deaths + s.StarvedThisYear;
                s.StarvedThisYear = 0;
                if (pop == 0)
                {
                    s.BirthsLastYear = 0;
                    Abandon(s);
                    continue;
                }

                if (rng.NextFloat() < PlagueChancePerYear && pop >= 20)
                {
                    int dead = Stoch(pop * rng.Range(0.1f, 0.25f), ref rng);
                    RemovePeople(s, dead);
                    s.DeathsLastYear += dead;
                    pop = s.Population;
                    _sim.Events.Add(tick, EventKind.Disaster, dead >= 50 ? 2 : 1, $"Ôn dịch hoành hành ở {s.Name}, {dead} người chết.");
                    if (pop == 0)
                    {
                        Abandon(s);
                        continue;
                    }
                }

                float foodFactor = Mathf.Clamp(s.Food / pop / 4f, 0.25f, 1.2f);
                float crowding = pop > s.HousingCapacity ? 0.4f : 1f;
                float saturation = Mathf.Clamp01(1f - pop / (float)CrowdedPopulation);
                int births = Stoch(s.FertileAdults * 0.5f * 0.3f * foodFactor * crowding * saturation, ref rng);
                s.Cohorts[0] += births;
                s.BirthsLastYear = births;
                pop += births;

                if (pop > s.HousingCapacity * 0.85f)
                {
                    TryBuildHouse(s, ref rng);
                    if (pop > s.HousingCapacity) TryBuildHouse(s, ref rng);
                }
                else if (pop < s.HousingCapacity * 0.35f && s.Houses.Count > 1)
                {
                    _w.Objects.Remove(s.Houses[s.Houses.Count - 1]);
                }

                if (pop >= 70 && (pop > s.HousingCapacity || s.Food < pop * 3f) && AliveCount < MaxSettlements && rng.NextFloat() < 0.35f)
                    Emigrate(s, tick, ref rng);
            }
        }

        // ---------------------------------------------------------------- flood: villages and migrant groups drown

        public void Flood(int x0, int y0, int x1, int y1, long tick)
        {
            foreach (var s in All)
            {
                if (!s.Alive || s.X < x0 || s.X > x1 || s.Y < y0 || s.Y > y1) continue;
                if (!TerrainInfo.IsWater(_w.Terrain[_w.Idx(s.X, s.Y)])) continue;
                int dead = s.Population;
                for (int b = 0; b < Settlement.AgeGroups; b++) s.Cohorts[b] = 0;
                _sim.Events.Add(tick, EventKind.Disaster, 2, $"{s.Name} bị nước nhấn chìm, {dead} người chết đuối.", s.X + 0.5f, s.Y + 0.5f, Fx.Splash);
                Abandon(s);
            }

            var e = _sim.Entities;
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                int id = _groups[g].Entity;
                float x = e.X[id], y = e.Y[id];
                if (x < x0 || x > x1 + 1 || y < y0 || y > y1 + 1 || _w.IsWalkable(x, y)) continue;
                int people = 0;
                foreach (int c in _groups[g].Cohorts) people += c;
                string from = All[_groups[g].From].Name;
                _groups.RemoveAt(g);
                e.Kill(id, DeathCause.Natural);
                _sim.Events.Add(tick, EventKind.Disaster, 1, $"Đoàn di dân từ {from} bị nước cuốn, {people} người chết đuối.", x, y, Fx.Splash);
            }
        }

        // A bolt of Thiên phạt landing near a village kills some of its people.
        public void Strike(int x, int y, long tick)
        {
            foreach (var s in All)
            {
                if (!s.Alive || (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) > 6 * 6) continue;
                var rng = RngFor(tick, 300000 + s.Id);
                int dead = Mathf.Min(s.Population, Stoch(s.Population * 0.1f + 1f, ref rng));
                RemovePeople(s, dead);
                s.DeathsLastYear += dead;
                _sim.Events.Add(tick, EventKind.Divine, 1, $"Thiên lôi đánh xuống {s.Name}, {dead} người thiệt mạng.");
            }
        }

        // ---------------------------------------------------------------- displacement (mountain raised, …)

        // A village needs ground it could build on at its centre.
        bool CanSettleAt(int x, int y)
        {
            if (!_w.InBounds(x, y)) return false;
            var t = _w.Terrain[_w.Idx(x, y)];
            return t == Terrain.Farmland || ObjectInfo.CanStandOn(ObjectType.House, t);
        }

        // The village (or sect) lost its land: move everyone to the nearest good ground, or scatter them.
        void Displace(Settlement s, long tick, ref DetRandom rng)
        {
            string oldName = s.Name;
            if (!FindRefuge(s, out int nx, out int ny))
            {
                var host = NearestAlive(s.X, s.Y, s);
                if (host != null)
                {
                    for (int b = 0; b < Settlement.AgeGroups; b++) host.Cohorts[b] += s.Cohorts[b];
                    host.Food += s.Food;
                }
                for (int b = 0; b < Settlement.AgeGroups; b++) s.Cohorts[b] = 0;
                _sim.Events.Add(tick, EventKind.Disaster, 2,
                    $"{oldName} mất hết đất sống, dân chúng ly tán{(host != null ? $" về {host.Name}" : "")}.");
                Abandon(s);
                return;
            }

            if (s.Sect)
            {
                int old = _w.Objects.CellObject[_w.Idx(s.X, s.Y)];
                if (old >= 0 && _w.Objects.Get(old).Type == ObjectType.SectHall) _w.Objects.Remove(old);
            }
            for (int k = s.Houses.Count - 1; k >= 0; k--) _w.Objects.Remove(s.Houses[k]);
            ReleaseFarmland(s, s.Farms.Count);

            s.X = nx;
            s.Y = ny;
            if (s.Sect)
            {
                PlaceSectHall(s, ref rng);
                _sim.Cultivation.RehomeSect(s.Id, nx, ny);
            }
            for (int k = 0; k < 3; k++) TryBuildHouse(s, ref rng);
            ClaimFarmland(s, (int)(s.Workers * CellsPerWorker));
            _sim.Events.Add(tick, EventKind.Disaster, s.Sect ? 2 : 1, $"{oldName} mất đất cũ, cả {(s.Sect ? "tông môn" : "làng")} dời đến nơi ở mới.");
        }

        // Nearest buildable, unclaimed ground within 60 cells; a sect prefers the richest qi it can find.
        bool FindRefuge(Settlement s, out int bx, out int by)
        {
            bx = by = -1;
            float bestQi = -1f;
            ushort owner = (ushort)(s.Id + 1);
            for (int r = 4; r <= 60; r += 2)
            {
                for (int dy = -r; dy <= r; dy += 2)
                for (int dx = -r; dx <= r; dx += 2)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = s.X + dx, y = s.Y + dy;
                    if (x < 8 || y < 8 || x >= _w.W - 8 || y >= _w.H - 8 || !CanSettleAt(x, y)) continue;
                    int i = _w.Idx(x, y);
                    if (_w.Terrain[i] == Terrain.Farmland || (_w.Owner[i] != 0 && _w.Owner[i] != owner)) continue;
                    if (TooCloseToOthers(x, y, s, 20)) continue;
                    if (!s.Sect)
                    {
                        bx = x;
                        by = y;
                        return true;
                    }
                    float qi = _w.QiCap[i];
                    if (qi > bestQi) { bestQi = qi; bx = x; by = y; }
                }
                if (s.Sect && bx >= 0 && r >= 20) return true; // searched a fair ring; take the best so far
            }
            return bx >= 0;
        }

        bool TooCloseToOthers(int x, int y, Settlement self, int dist)
        {
            foreach (var o in All)
                if (o != self && o.Alive && (o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y) < dist * dist) return true;
            return false;
        }

        Settlement NearestAlive(int x, int y, Settlement except)
        {
            Settlement best = null;
            int bestD = int.MaxValue;
            foreach (var o in All)
            {
                if (!o.Alive || o == except) continue;
                int d = (o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y);
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        void PlaceSectHall(Settlement s, ref DetRandom rng)
        {
            int ox = s.X - 2, oy = s.Y - 2;
            for (int y = oy; y < oy + 5; y++)
            for (int x = ox; x < ox + 5; x++)
            {
                if (!_w.InBounds(x, y)) return;
                int i = _w.Idx(x, y);
                if (!ObjectInfo.CanStandOn(ObjectType.SectHall, _w.Terrain[i])) return;
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0 && ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) return;
            }
            for (int y = oy; y < oy + 5; y++)
            for (int x = ox; x < ox + 5; x++)
                _w.Objects.RemoveAtCell(x, y);
            _w.Objects.Place(ObjectType.SectHall, ox, oy, (byte)rng.Range(0, 3));
        }

        // A child (preferably 10–14) leaves the village to walk the path of cultivation.
        public bool TakeChild(Settlement s)
        {
            for (int b = 2; b <= 3; b++)
            {
                if (s.Cohorts[b] == 0) continue;
                s.Cohorts[b]--;
                return true;
            }
            return false;
        }

        void RemovePeople(Settlement s, int n)
        {
            int guard = 0;
            while (n > 0 && guard++ < 10000)
            {
                bool any = false;
                foreach (int b in FrailtyOrder)
                {
                    if (n == 0) break;
                    if (s.Cohorts[b] == 0) continue;
                    s.Cohorts[b]--;
                    n--;
                    any = true;
                }
                if (!any) break;
            }
        }

        // ---------------------------------------------------------------- land and houses

        void PruneFarms(Settlement s)
        {
            ushort owner = (ushort)(s.Id + 1);
            for (int k = s.Farms.Count - 1; k >= 0; k--)
            {
                int i = s.Farms[k];
                if (_w.Terrain[i] == Terrain.Farmland && _w.Owner[i] == owner) continue;
                if (_w.Owner[i] == owner) _w.Owner[i] = 0;
                s.Farms.RemoveAt(k);
            }
        }

        void ClaimFarmland(Settlement s, int want)
        {
            if (want <= 0) return;
            ushort owner = (ushort)(s.Id + 1);
            int radius = Mathf.Clamp(6 + (int)(Mathf.Sqrt(s.Population) * 1.2f), 6, 30);
            int added = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int r = 2; r <= radius && added < want; r++)
            for (int dy = -r; dy <= r && added < want; dy++)
            for (int dx = -r; dx <= r && added < want; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                int x = s.X + dx, y = s.Y + dy;
                if (!_w.InBounds(x, y)) continue;
                int i = _w.Idx(x, y);
                if (_w.Owner[i] != 0 || !TerrainInfo.IsFarmable(_w.Terrain[i])) continue;
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0)
                {
                    if (ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) continue;
                    _w.Objects.Remove(obj); // clearing forest for fields
                }
                _w.Terrain[i] = Terrain.Farmland;
                _w.Owner[i] = owner;
                s.Farms.Add(i);
                added++;
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            if (added > 0) _w.NotifyTerrainChanged(x0, y0, x1, y1);
        }

        void ReleaseFarmland(Settlement s, int n)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int k = 0; k < n && s.Farms.Count > 0; k++)
            {
                int i = s.Farms[s.Farms.Count - 1];
                s.Farms.RemoveAt(s.Farms.Count - 1);
                if (_w.Terrain[i] == Terrain.Farmland) _w.Terrain[i] = Terrain.Grass;
                _w.Owner[i] = 0;
                int x = i % _w.W, y = i / _w.W;
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            if (x1 >= x0) _w.NotifyTerrainChanged(x0, y0, x1, y1);
        }

        void AdoptHouse(Settlement s, int objId)
        {
            s.Houses.Add(objId);
            _houseOwner[objId] = s.Id;
            SetFootprintOwner(_w.Objects.Get(objId), (ushort)(s.Id + 1));
        }

        void SetFootprintOwner(in WorldObject o, ushort owner)
        {
            int fw = ObjectInfo.FootprintW[(int)o.Type], fh = ObjectInfo.FootprintH[(int)o.Type];
            for (int y = o.Y; y < o.Y + fh; y++)
            for (int x = o.X; x < o.X + fw; x++)
                _w.Owner[_w.Idx(x, y)] = owner;
        }

        // Houses can disappear through the settlement itself or through Thiên Đạo; both land here.
        void OnObjectRemoved(int id, WorldObject o)
        {
            if (!_houseOwner.TryGetValue(id, out int sid)) return;
            _houseOwner.Remove(id);
            All[sid].Houses.Remove(id);
            SetFootprintOwner(o, 0);
        }

        bool TryBuildHouse(Settlement s, ref DetRandom rng)
        {
            ushort owner = (ushort)(s.Id + 1);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int ring = 1 + attempt / 6;
                int ox = s.X + rng.Range(-ring, ring + 1) * 4 + rng.Range(0, 2) - 1;
                int oy = s.Y + rng.Range(-ring, ring + 1) * 4 + rng.Range(0, 2) - 1;
                if (!HouseFits(ox, oy, owner)) continue;
                for (int y = oy; y < oy + 3; y++)
                for (int x = ox; x < ox + 3; x++)
                    _w.Objects.RemoveAtCell(x, y);
                int id = _w.Objects.Place(ObjectType.House, ox, oy, s.Roof);
                if (id < 0) continue;
                AdoptHouse(s, id);
                return true;
            }
            return false;
        }

        bool HouseFits(int ox, int oy, ushort owner)
        {
            for (int y = oy; y < oy + 3; y++)
            for (int x = ox; x < ox + 3; x++)
            {
                if (!_w.InBounds(x, y)) return false;
                int i = _w.Idx(x, y);
                if (_w.Owner[i] != 0 && _w.Owner[i] != owner) return false;
                if (!ObjectInfo.CanStandOn(ObjectType.House, _w.Terrain[i])) return false;
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0 && ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- migration

        void Emigrate(Settlement s, long tick, ref DetRandom rng)
        {
            if (!FindSite(s.X, s.Y, ref rng, out int tx, out int ty)) return;
            var g = new MigrantGroup { From = s.Id, Roof = s.Roof };
            float share = rng.Range(0.18f, 0.3f);
            int moved = 0;
            for (int b = 1; b <= 9; b++)
            {
                int m = Stoch(s.Cohorts[b] * share, ref rng);
                s.Cohorts[b] -= m;
                g.Cohorts[b] = m;
                moved += m;
            }
            if (moved < 8)
            {
                for (int b = 1; b <= 9; b++) s.Cohorts[b] += g.Cohorts[b];
                return;
            }
            g.Food = s.Food * share;
            s.Food -= g.Food;
            var e = _sim.Entities;
            g.Entity = e.Spawn(Species.Migrants, s.X + 0.5f, s.Y + 0.5f, tick);
            e.TX[g.Entity] = tx + 0.5f;
            e.TY[g.Entity] = ty + 0.5f;
            _groups.Add(g);
        }

        bool FindSite(int fromX, int fromY, ref DetRandom rng, out int tx, out int ty)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f), dist = rng.Range(30f, 110f);
                int x = fromX + (int)(Mathf.Cos(angle) * dist), y = fromY + (int)(Mathf.Sin(angle) * dist);
                if (!SiteOk(x, y, true) || !PathClear(fromX, fromY, x, y)) continue;
                tx = x;
                ty = y;
                return true;
            }
            tx = ty = 0;
            return false;
        }

        bool SiteOk(int x, int y, bool needWater)
        {
            if (x < 8 || y < 8 || x >= _w.W - 8 || y >= _w.H - 8) return false;
            int i = _w.Idx(x, y);
            var t = _w.Terrain[i];
            if (t != Terrain.Grass && t != Terrain.Savanna && t != Terrain.Forest) return false;
            if (needWater && (_w.WaterDist[i] < 2 || _w.WaterDist[i] > 14)) return false;
            for (int yy = y - 6; yy <= y + 6; yy++)
            for (int xx = x - 6; xx <= x + 6; xx++)
                if (_w.Owner[_w.Idx(xx, yy)] != 0) return false;
            foreach (var s in All)
                if (s.Alive && (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) < 36 * 36) return false;
            return true;
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

        public void MigrantsArrived(int entity, long tick)
        {
            int gi = _groups.FindIndex(g => g.Entity == entity);
            var e = _sim.Entities;
            int cx = (int)e.X[entity], cy = (int)e.Y[entity];
            e.Kill(entity, DeathCause.Settled);
            if (gi < 0) return;
            var group = _groups[gi];
            _groups.RemoveAt(gi);

            var rng = RngFor(tick, entity + 200000);
            for (int r = 0; r <= 8; r += 2)
            for (int dy = -r; dy <= r; dy += 2)
            for (int dx = -r; dx <= r; dx += 2)
            {
                if (!SiteOk(cx + dx, cy + dy, false) || AliveCount >= MaxSettlements) continue;
                var s = Create(cx + dx, cy + dy, group.Roof, false, tick, ref rng);
                s.ParentId = group.From;
                System.Array.Copy(group.Cohorts, s.Cohorts, Settlement.AgeGroups);
                s.Food = group.Food;
                TryBuildHouse(s, ref rng);
                TryBuildHouse(s, ref rng);
                ClaimFarmland(s, (int)(s.Workers * CellsPerWorker));
                return;
            }

            // No room here: rejoin the nearest living village, if any.
            Settlement best = null;
            int bestD = int.MaxValue;
            foreach (var s in All)
            {
                if (!s.Alive) continue;
                int d = (s.X - cx) * (s.X - cx) + (s.Y - cy) * (s.Y - cy);
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best == null) return;
            for (int b = 0; b < Settlement.AgeGroups; b++) best.Cohorts[b] += group.Cohorts[b];
            best.Food += group.Food;
        }

        public void HashInto(ref ulong h)
        {
            foreach (var s in All)
            {
                StateHash.Add(ref h, s.Alive ? s.Id : -s.Id - 1);
                foreach (int c in s.Cohorts) StateHash.Add(ref h, c);
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(s.Food));
                StateHash.Add(ref h, s.Houses.Count | ((long)s.Farms.Count << 32));
            }
            foreach (var g in _groups)
            {
                StateHash.Add(ref h, g.Entity);
                foreach (int c in g.Cohorts) StateHash.Add(ref h, c);
            }
        }
    }
}
