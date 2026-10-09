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
        public int Kingdom = -1;   // the mortal kingdom whose land it stands on
        public bool Capital;       // that kingdom's capital

        public readonly int[] Cohorts = new int[AgeGroups];
        public float Food;
        public readonly List<int> Houses = new List<int>();
        public readonly List<int> Civic = new List<int>();  // công trình: giếng, miếu, chợ, tháp, hoàng cung (not homes)
        public readonly List<int> Farms = new List<int>(); // cell indices
        public int WallX0 = -1, WallY0, WallX1, WallY1;    // the ring of its walls, if it has any
        public long CivicRetry;                            // no room for its next công trình: look again from this tick
        public int ClaimFrom = 2;                          // rings inside this have no free farmland (until land is freed nearby)
        public float Faith = FaithSystem.StartFaith;       // tín ngưỡng 0..100: how much its people trust Thiên Đạo (FaithSystem)
        public long LastPrayer = -100000;                  // when it last prayed

        // Caches (exact: same result as recomputing): fields are re-checked only after the land nearby changed,
        // and the harvest's fertility sum only when the fields or the number tended changed.
        public bool FarmsCheck = true;
        public int FertilityTended = -1;
        public float FertilitySum;

        public float LastHarvest, LastHunt;
        public int BirthsLastYear, DeathsLastYear, StarvedThisYear, StarvedLastYear;
        public float Unrest = 20f;                         // bất mãn 0..100 of its people toward the crown (PoliticsSystem)

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
        // A capital that moved to an ordinary town is called that town's Thành.
        public string Name => Sect ? BaseName
            : Capital ? (BaseName.EndsWith(" Kinh") || BaseName.EndsWith(" Vương Đình") ? BaseName : BaseName + " Thành")
            : BaseName + " " + Lore.Tier(Population);

        public bool Walled => WallX0 >= 0;

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
        const float PlagueChancePerYear = 0.01f; // an outbreak now lingers and spreads, so it starts less often
        const float CellsPerWorker = 2.5f;
        const float YieldPerFertility = 1.4f;

        static readonly float[] Mortality =
        {
            0.05f, 0.008f, 0.006f, 0.006f, 0.006f, 0.006f, 0.006f, 0.007f, 0.008f,
            0.01f, 0.015f, 0.025f, 0.04f, 0.07f, 0.12f, 0.2f, 0.35f
        };

        // Starvation takes the youngest and oldest first.
        static readonly int[] FrailtyOrder = { 0, 16, 15, 14, 13, 1, 12, 11, 2, 10, 3, 9, 4, 8, 5, 7, 6 };

        readonly Lore.Picker _placeNames;
        readonly Lore.Picker _sectNames;
        readonly Lore.Picker _demonicSectNames;

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
            _w.TerrainChanged += HandleTerrainChanged;
            _w.LookChanged += HandleLookChanged;
            _placeNames = new Lore.Picker(_w.Lore.Places, _w.Lore.Syllables);
            _sectNames = new Lore.Picker(_w.Lore.RighteousSects, _w.Lore.Syllables);
            _demonicSectNames = new Lore.Picker(_w.Lore.DemonicSects, _w.Lore.Syllables);

            var rng = new DetRandom(_w.Seed ^ 0x5E771Eu);
            foreach (var site in _w.VillageSites)
            {
                // Ma Đạo's sects take demonic names; a capital takes its kingdom's.
                string name = site.Capital >= 0 ? _w.Kingdoms[site.Capital].CapitalName
                    : site.Sect && _w.RegionAt(_w.Idx(site.X, site.Y)) == RegionKind.MaDao ? _demonicSectNames.Next(ref rng) : null;
                var s = Create(site.X, site.Y, site.Roof, site.Sect, 0, ref rng, name);
                s.Capital = site.Capital >= 0;
                foreach (int h in site.Houses) AdoptHouse(s, h);
                int pop = site.Houses.Count * rng.Range(4, 6);
                DistributeInitial(s, pop, ref rng);
                s.Food = pop * 6f;
                Develop(s, true); // its công trình and house styles before the fields take the ground around
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
        public Settlement OwnerOfObject(int objectId)
        {
            if (_houseOwner.TryGetValue(objectId, out int id) && id >= 0 && id < All.Count) return All[id];
            if (!_w.Objects.IsAlive(objectId) || _w.Objects.Get(objectId).Type != ObjectType.SectHall) return null;
            foreach (var s in All) // a sect hall stands centred on its sect
                if (s.Alive && s.Sect && HallOf(s) == objectId) return s;
            return null;
        }

        public int HallOf(Settlement s)
        {
            if (!_w.InBounds(s.X, s.Y)) return -1;
            int obj = _w.Objects.CellObject[_w.Idx(s.X, s.Y)];
            return obj >= 0 && _w.Objects.Get(obj).Type == ObjectType.SectHall ? obj : -1;
        }

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

        Settlement Create(int x, int y, byte roof, bool sect, long tick, ref DetRandom rng, string name = null)
        {
            var s = new Settlement
            {
                Id = All.Count,
                BaseName = name ?? (sect ? _sectNames.Next(ref rng) : _placeNames.Next(ref rng)),
                X = x,
                Y = y,
                Roof = roof,
                Sect = sect,
                FoundedTick = tick,
                Kingdom = _w.InBounds(x, y) ? _w.KingdomOf[_w.Idx(x, y)] - 1 : -1
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
            string name = s.Name;
            s.Alive = false;
            AliveCount--;
            if (s.Capital) CapitalFalls(s, name, _sim.Clock.Tick);
            if (s.Sect)
            {
                _sim.Factions?.SectGone(s.Id, _sim.Clock.Tick);
                _sim.Cultivation.DisbandSect(s.Id, _sim.Clock.Tick); // its hall stays behind as a ruin
            }
            ClearDevelopment(s);
            for (int k = s.Houses.Count - 1; k >= 0; k--) _w.Objects.Remove(s.Houses[k]);
            ReleaseFarmland(s, s.Farms.Count);
        }

        // The capital is gone: the court moves to the largest town left in the kingdom (which then raises its
        // palace and walls), or, with no town left, the kingdom is no more.
        void CapitalFalls(Settlement old, string oldName, long tick)
        {
            old.Capital = false;
            var k = old.Kingdom >= 0 && old.Kingdom < _w.Kingdoms.Count ? _w.Kingdoms[old.Kingdom] : null;
            if (k == null) return;
            Settlement best = null;
            foreach (var s in All)
                if (s.Alive && !s.Sect && s.Kingdom == k.Id && (best == null || s.Population > best.Population)) best = s;
            if (best == null)
            {
                k.Fallen = true;
                _sim.Events.Add(tick, EventKind.Destruction, 3, $"Kinh đô {oldName} hoang phế, {k.Name} không còn một thành trì nào: {k.Name} diệt vong.",
                    old.X + 0.5f, old.Y + 0.5f);
                return;
            }
            best.Capital = true;
            best.CivicRetry = 0;
            k.CapitalX = best.X;
            k.CapitalY = best.Y;
            _sim.Events.Add(tick, EventKind.Succession, 2, $"{k.Name} mất kinh đô {oldName}, dời đô về {best.Name}.", best.X + 0.5f, best.Y + 0.5f);
        }

        // Settlers on the land of a fallen kingdom raise it again.
        void Restore(Settlement s, long tick)
        {
            var k = s.Kingdom >= 0 && s.Kingdom < _w.Kingdoms.Count ? _w.Kingdoms[s.Kingdom] : null;
            if (k == null || !k.Fallen || s.Sect) return;
            k.Fallen = false;
            s.Capital = true;
            k.CapitalX = s.X;
            k.CapitalY = s.Y;
            _sim.Events.Add(tick, EventKind.Founding, 2, $"Di dân dựng {s.Name} trên đất cũ của {k.Name}: {k.Name} phục quốc.", s.X + 0.5f, s.Y + 0.5f);
            _sim.Politics?.OnRestored(k, tick); // a new house to rule it
        }

        public Settlement FoundVillage(int x, int y, byte roof, int people, long tick)
        {
            if (!_w.InBounds(x, y) || !TerrainInfo.IsWalkable(_w.Terrain[_w.Idx(x, y)]) || AliveCount >= MaxSettlements) return null;
            var rng = RngFor(tick, x * 4099 + y);
            var s = Create(x, y, roof, false, tick, ref rng);
            DistributeInitial(s, people, ref rng);
            s.Food = people * 6f;
            Restore(s, tick);
            for (int k = 0; k < 3; k++) TryBuildHouse(s, ref rng);
            ClaimFarmland(s, (int)(s.Workers * CellsPerWorker));
            return s;
        }

        // ---------------------------------------------------------------- sects (thế lực)

        // Room for a sect: buildable, unclaimed ground for the 5×5 hall, clear of other settlements.
        public bool CanFoundSectAt(int x, int y)
        {
            if (x < 10 || y < 10 || x >= _w.W - 10 || y >= _w.H - 10 || !CanSettleAt(x, y)) return false;
            if (AliveCount >= MaxSettlements || TooCloseToOthers(x, y, null, 24)) return false;
            for (int yy = y - 2; yy <= y + 2; yy++)
            for (int xx = x - 2; xx <= x + 2; xx++)
            {
                int i = _w.Idx(xx, yy);
                if (_w.Owner[i] != 0 || !ObjectInfo.CanStandOn(ObjectType.SectHall, _w.Terrain[i])) return false;
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0 && ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) return false;
            }
            return true;
        }

        // A name no settlement has carried yet, true to the founder's path.
        public string NewSectName(bool demonic, ref DetRandom rng)
        {
            var free = new List<string>();
            foreach (var n in demonic ? _w.Lore.DemonicSects : _w.Lore.RighteousSects)
                if (!NameTaken(n)) free.Add(n);
            if (free.Count > 0) return free[rng.Range(0, free.Count)];
            for (int k = 0; k < 30; k++)
            {
                string g = Lore.GeneratedSectName(_w.Lore, ref rng);
                if (!NameTaken(g)) return g;
            }
            return Lore.GeneratedSectName(_w.Lore, ref rng) + " " + All.Count;
        }

        bool NameTaken(string name)
        {
            foreach (var s in All)
                if (s.BaseName == name) return true;
            return false;
        }

        // A new sect town: the hall, a couple of houses, and servants drawn from the nearest village.
        public Settlement FoundSect(int x, int y, string name, long tick)
        {
            if (!CanFoundSectAt(x, y)) return null;
            var rng = RngFor(tick, 400000 + x * 4099 + y);
            var s = Create(x, y, (byte)rng.Range(0, 4), true, tick, ref rng, name);
            PlaceSectHall(s, ref rng);
            var host = NearestAlive(x, y, s);
            int want = rng.Range(8, 16);
            if (host != null && !host.Sect && (host.X - x) * (host.X - x) + (host.Y - y) * (host.Y - y) < 140 * 140 && host.Population > want + 20)
            {
                for (int b = 3; b <= 8 && want > 0; b++)
                {
                    int n = Mathf.Min(want, host.Cohorts[b] / 3);
                    host.Cohorts[b] -= n;
                    s.Cohorts[b] += n;
                    want -= n;
                }
                s.ParentId = host.Id;
            }
            if (s.Population < 6) DistributeInitial(s, 6, ref rng);
            s.Food = s.Population * 6f;
            TryBuildHouse(s, ref rng);
            TryBuildHouse(s, ref rng);
            ClaimFarmland(s, (int)(s.Workers * CellsPerWorker));
            return s;
        }

        // Diệt môn: the hall burns, the cultivators are gone, the servants stay on as an ordinary village.
        public void ConvertSectToVillage(Settlement s, long tick)
        {
            if (!s.Sect) return;
            int hall = HallOf(s);
            if (hall >= 0) _w.Objects.Remove(hall);
            _sim.Cultivation.DisbandSect(s.Id, tick);
            s.Sect = false;
            var rng = RngFor(tick, 600000 + s.Id);
            s.BaseName = _placeNames.Next(ref rng);
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
                if (s.FarmsCheck)
                {
                    long p0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    PruneFarms(s);
                    Prof(0, p0);
                    s.FarmsCheck = false;
                    s.FertilityTended = -1;
                }
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
                if (s.FertilityTended != tended)
                {
                    float sum = 0f;
                    for (int f = 0; f < tended; f++) sum += _w.Fertility(s.Farms[f]);
                    s.FertilitySum = sum;
                    s.FertilityTended = tended;
                }
                float harvest = s.FertilitySum;
                harvest *= YieldPerFertility * season * _sim.Disasters.HarvestFactor(s.X, s.Y); // đại hạn
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
                long p1 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (s.Farms.Count < wantFarms && s.Food < pop * 6f) { ClaimFarmland(s, Mathf.Min(8, wantFarms - s.Farms.Count)); Prof(1, p1); }
                else if (s.Farms.Count > wantFarms * 1.4f + 10) { ReleaseFarmland(s, Mathf.Min(6, s.Farms.Count - wantFarms)); Prof(2, p1); }
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
                s.StarvedLastYear = s.StarvedThisYear; // for PoliticsSystem: hunger breeds unrest
                s.StarvedThisYear = 0;
                if (pop == 0)
                {
                    s.BirthsLastYear = 0;
                    Abandon(s);
                    continue;
                }

                // Ôn dịch lingers for months and can spread along the roads (DisasterSystem).
                if (rng.NextFloat() < PlagueChancePerYear * _sim.Rules[Rule.Calamities] && pop >= 20) _sim.Disasters.StartEpidemic(s, tick, false);

                float foodFactor = Mathf.Clamp(s.Food / pop / 4f, 0.25f, 1.2f);
                float crowding = pop > s.HousingCapacity ? 0.4f : 1f;
                float saturation = Mathf.Clamp01(1f - pop / (float)CrowdedPopulation);
                int births = Stoch(s.FertileAdults * 0.5f * 0.3f * foodFactor * crowding * saturation * _sim.Rules[Rule.Births], ref rng);
                s.Cohorts[0] += births;
                s.BirthsLastYear = births;
                pop += births;

                long p3 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (pop > s.HousingCapacity * 0.85f)
                {
                    TryBuildHouse(s, ref rng);
                    if (pop > s.HousingCapacity) TryBuildHouse(s, ref rng);
                }
                else if (pop < s.HousingCapacity * 0.35f && s.Houses.Count > 1)
                {
                    _w.Objects.Remove(s.Houses[s.Houses.Count - 1]);
                }
                Prof(3, p3);
                p3 = System.Diagnostics.Stopwatch.GetTimestamp();
                Develop(s, false);
                Prof(4, p3);

                p3 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (pop >= 70 && (pop > s.HousingCapacity || s.Food < pop * 3f) && AliveCount < MaxSettlements && rng.NextFloat() < 0.35f)
                    Emigrate(s, tick, ref rng);
                Prof(5, p3);
            }
        }

        // Where the villages' time goes, for the perf probe: not part of the world.
        public static readonly string[] ProfNames = { "Kiểm ruộng", "Mở ruộng", "Bỏ ruộng", "Xây / bỏ nhà", "Phát triển (công trình, tường)", "Di dân" };
        public static readonly double[] ProfMs = new double[6];
        static readonly double ProfScale = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        static void Prof(int slot, long t0) => ProfMs[slot] += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * ProfScale;

        // ---------------------------------------------------------------- flood / lava: villages and migrant groups engulfed

        public void Flood(int x0, int y0, int x1, int y1, long tick)
        {
            foreach (var s in All)
            {
                if (!s.Alive || s.X < x0 || s.X > x1 || s.Y < y0 || s.Y > y1) continue;
                var t = _w.Terrain[_w.Idx(s.X, s.Y)];
                if (!TerrainInfo.IsWater(t) && t != Terrain.Lava) continue;
                int dead = s.Population;
                for (int b = 0; b < Settlement.AgeGroups; b++) s.Cohorts[b] = 0;
                _sim.Events.Add(tick, EventKind.Disaster, 2,
                    t == Terrain.Lava ? $"{s.Name} bị dung nham nuốt chửng, {dead} người chết cháy." : $"{s.Name} bị nước nhấn chìm, {dead} người chết đuối.",
                    s.X + 0.5f, s.Y + 0.5f, t == Terrain.Lava ? Fx.Explosion : Fx.Splash);
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
                bool lava = _w.Terrain[_w.Idx((int)x, (int)y)] == Terrain.Lava;
                _groups.RemoveAt(g);
                e.Kill(id, DeathCause.Natural);
                _sim.Events.Add(tick, EventKind.Disaster, 1,
                    lava ? $"Đoàn di dân từ {from} bị dung nham vùi lấp, {people} người chết." : $"Đoàn di dân từ {from} bị nước cuốn, {people} người chết đuối.",
                    x, y, lava ? Fx.Explosion : Fx.Splash);
            }
        }

        // A blow from heaven or earth (HarmSystem) over migrants on the road: each group loses the share it would
        // kill; a group left with nobody is gone. Returns the dead.
        public int HarmMigrants(HarmSystem harm, float damage, long tick)
        {
            var e = _sim.Entities;
            int total = 0;
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                var group = _groups[g];
                float x = e.X[group.Entity], y = e.Y[group.Entity];
                float share = HarmSystem.CrowdShare(damage * harm.Reach(x, y), SpeciesInfo.Hp[(int)Species.Migrants]);
                if (share <= 0f) continue;
                int people = 0, dead = 0;
                for (int b = 0; b < Settlement.AgeGroups; b++)
                {
                    int lost = Mathf.CeilToInt(group.Cohorts[b] * share - 0.001f);
                    lost = Mathf.Min(lost, group.Cohorts[b]);
                    group.Cohorts[b] -= lost;
                    dead += lost;
                    people += group.Cohorts[b];
                }
                total += dead;
                if (people > 0) continue;
                _groups.RemoveAt(g);
                e.Kill(group.Entity, DeathCause.Natural);
                _sim.Events.Add(tick, EventKind.Disaster, 1, $"Đoàn di dân từ {All[group.From].Name} bị diệt sạch, {dead} người chết.", x, y);
            }
            return total;
        }

        // A beast caught these migrants on the road (BeastChase): `share` of them die; if none are left the band is gone.
        public int MaulMigrants(int entity, float share, long tick)
        {
            int gi = _groups.FindIndex(g => g.Entity == entity);
            if (gi < 0) return 0;
            var group = _groups[gi];
            int dead = 0, left = 0;
            for (int b = 0; b < Settlement.AgeGroups; b++)
            {
                int lost = Mathf.Min(group.Cohorts[b], Mathf.CeilToInt(group.Cohorts[b] * share - 0.001f));
                group.Cohorts[b] -= lost;
                dead += lost;
                left += group.Cohorts[b];
            }
            if (left == 0)
            {
                _groups.RemoveAt(gi);
                _sim.Entities.Kill(entity, DeathCause.Natural);
            }
            return dead;
        }

        // A calamity takes up to n people (the frail first); returns how many died.
        public int Kill(Settlement s, int n)
        {
            if (!s.Alive || n <= 0) return 0;
            int before = s.Population;
            RemovePeople(s, n);
            int dead = before - s.Population;
            s.DeathsLastYear += dead;
            return dead;
        }

        // The field at this cell is ruined (flooded, buried in lava): its village no longer owns or tends it.
        public void LoseField(int cell)
        {
            var s = Owning(cell);
            if (s != null) { s.Farms.Remove(cell); s.FertilityTended = -1; }
            Freed(cell % _w.W, cell / _w.W, cell % _w.W, cell / _w.W);
            _w.Owner[cell] = 0;
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
                var host = RefugeHost(s); // behind walls if any are in reach
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
            ClearDevelopment(s);
            for (int k = s.Houses.Count - 1; k >= 0; k--) _w.Objects.Remove(s.Houses[k]);
            ReleaseFarmland(s, s.Farms.Count);

            s.X = nx;
            s.ClaimFrom = 2;
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

        // Where the homeless go: the nearest walled town within 100 cells, else the nearest place at all.
        Settlement RefugeHost(Settlement s)
        {
            Settlement walled = null;
            int bestD = 100 * 100;
            foreach (var o in All)
            {
                if (!o.Alive || o == s || !o.Walled) continue;
                int d = (o.X - s.X) * (o.X - s.X) + (o.Y - s.Y) * (o.Y - s.Y);
                if (d < bestD) { bestD = d; walled = o; }
            }
            return walled ?? NearestAlive(s.X, s.Y, s);
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

        // One grown villager (15–39, else any child old enough) leaves; returns their age.
        public bool TakeAdult(Settlement s, out float ageYears)
        {
            int best = -1;
            for (int b = 3; b <= 7; b++)
                if (s.Cohorts[b] > 0 && (best < 0 || s.Cohorts[b] > s.Cohorts[best])) best = b;
            if (best < 0)
                for (int b = 2; b < Settlement.AgeGroups && best < 0; b++)
                    if (s.Cohorts[b] > 0) best = b;
            ageYears = 0f;
            if (best < 0) return false;
            s.Cohorts[best]--;
            ageYears = best * 5 + 2.5f;
            return true;
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

        // Land changed somewhere: villages whose fields could reach that far re-check them next month.
        void HandleTerrainChanged(int x0, int y0, int x1, int y1)
        {
            const int Reach = 40; // farmland is claimed within 30 cells of the centre
            foreach (var s in All)
            {
                if (!s.Alive) continue;
                if (s.X + Reach < x0 || s.X - Reach > x1 || s.Y + Reach < y0 || s.Y - Reach > y1) continue;
                s.FarmsCheck = true;
                if (!_claiming) s.ClaimFrom = 2; // the land may have opened up; a claim only ever takes land
            }
        }

        bool _claiming;

        // A scar came or faded near some villages: their fields yield differently now (Fertility reads scars).
        void HandleLookChanged(int x0, int y0, int x1, int y1)
        {
            const int Reach = 31;
            foreach (var s in All)
                if (s.Alive && s.X + Reach >= x0 && s.X - Reach <= x1 && s.Y + Reach >= y0 && s.Y - Reach <= y1) s.FertilityTended = -1;
        }

        // Land around (x0..x1, y0..y1) went back to nobody: villages near it must look at their inner rings again.
        void Freed(int x0, int y0, int x1, int y1)
        {
            const int Reach = 31;
            foreach (var s in All)
                if (s.Alive && s.X + Reach >= x0 && s.X - Reach <= x1 && s.Y + Reach >= y0 && s.Y - Reach <= y1) s.ClaimFrom = 2;
        }

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
            int added = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, lastRing = 2;
            // Rings inside ClaimFrom were found full last time and nothing has been freed near since: skip them.
            // Same fields as scanning from ring 2, without walking over every field the village already has.
            for (int r = Mathf.Max(2, s.ClaimFrom); r <= radius && added < want; r++)
            for (int dy = -r; dy <= r && added < want; dy++)
            for (int dx = -r; dx <= r && added < want; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                int x = s.X + dx, y = s.Y + dy;
                if (!_w.InBounds(x, y)) continue;
                int i = _w.Idx(x, y);
                if (_w.Owner[i] != 0 || !TerrainInfo.IsFarmable(_w.Terrain[i])) continue;
                if ((_w.Zone[i] & ZoneFlags.Thunder) != 0) continue; // nobody tills ground where lightning still crawls
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
                lastRing = Mathf.Max(dx < 0 ? -dx : dx, dy < 0 ? -dy : dy);
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            // Filled: the last ring may still have room. Not filled: every ring out to the radius is taken.
            s.ClaimFrom = added >= want ? lastRing : radius + 1;
            if (added > 0)
            {
                _claiming = true;
                _w.NotifyTerrainChanged(x0, y0, x1, y1);
                _claiming = false;
            }
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
            // Any building gone frees its ground for the fields around, owned or a ruin.
            if (ObjectInfo.IsBuilding(o.Type))
                Freed(o.X, o.Y, o.X + ObjectInfo.FootprintW[(int)o.Type] - 1, o.Y + ObjectInfo.FootprintH[(int)o.Type] - 1);
            if (!_houseOwner.TryGetValue(id, out int sid)) return;
            _houseOwner.Remove(id);
            if (!All[sid].Houses.Remove(id)) All[sid].Civic.Remove(id);
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
                int id = _w.Objects.Place(ObjectType.House, ox, oy, HouseVariant(s, ox, oy));
                if (id < 0) continue;
                AdoptHouse(s, id);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- standing: house styles, công trình, walls

        // 0 thôn (nhà tranh), 1 trấn (nhà ngói), 2 thành (nhà lầu), 3 kinh thành (phủ đệ).
        public static int Standing(Settlement s) => s.Capital ? 3 : s.Population >= 400 ? 2 : s.Population >= 150 ? 1 : 0;

        // What each standing builds, in the order it builds them. A sect town only digs its well.
        static readonly ObjectType[][] CivicFor =
        {
            new[] { ObjectType.Well },
            new[] { ObjectType.Well, ObjectType.Shrine, ObjectType.Market },
            new[] { ObjectType.Well, ObjectType.Shrine, ObjectType.Market, ObjectType.Pagoda },
            new[] { ObjectType.Palace, ObjectType.Well, ObjectType.Shrine, ObjectType.Market, ObjectType.Pagoda },
        };

        // House sprite: style × shape × roof colour. The heart of a place is one style grander than its edge.
        byte HouseVariant(Settlement s, int ox, int oy)
        {
            int standing = Standing(s);
            int d = Mathf.Max(Mathf.Abs(ox + 1 - s.X), Mathf.Abs(oy + 1 - s.Y));
            int style = d <= 7 ? standing : Mathf.Max(0, standing - 1);
            int shape = (int)(Hash.U32(_w.Seed ^ 0x405Eu, ox, oy) & 1);
            return (byte)(style * 8 + shape * 4 + (s.Roof & 3));
        }

        // Once a year a place grows into its standing: a new công trình, a few houses rebuilt, the walls moved out.
        // No dice: what gets built follows from the place itself.
        void Develop(Settlement s, bool initial)
        {
            long tick = _sim.Clock.Tick;
            if (initial || tick >= s.CivicRetry)
                if (!EnsureCivic(s, initial ? 8 : 1)) s.CivicRetry = tick + 10L * SimClock.DaysPerYear; // the search is costly; not every year
            Restyle(s, initial ? int.MaxValue : 3);
            UpdateWalls(s);
        }

        void Restyle(Settlement s, int max)
        {
            foreach (int id in s.Houses)
            {
                if (max <= 0) return;
                var o = _w.Objects.Get(id);
                byte v = HouseVariant(s, o.X, o.Y);
                if (o.Variant == v) continue;
                _w.Objects.SetVariant(id, v);
                max--;
            }
        }

        // False when something it needs found no ground.
        bool EnsureCivic(Settlement s, int max)
        {
            var need = s.Sect ? CivicFor[0] : CivicFor[Standing(s)];
            bool ok = true;
            foreach (var type in need)
            {
                if (max <= 0) return ok;
                if (HasCivic(s, type)) continue;
                if (type == ObjectType.Shrine && s.Faith < FaithSystem.ShrineFloor) continue; // a people who turned from heaven keep no miếu
                if (PlaceCivic(s, type, s.X, s.Y, 14, false)) max--;
                else ok = false;
            }
            return ok;
        }

        // Tín ngưỡng (FaithSystem): a faithful people raise a miếu to Thiên Đạo, even a hamlet; a faithless one lets theirs fall.
        public bool BuildShrine(Settlement s) => s.Alive && !HasCivic(s, ObjectType.Shrine) && PlaceCivic(s, ObjectType.Shrine, s.X, s.Y, 14, false);

        public bool AbandonShrine(Settlement s)
        {
            foreach (int id in s.Civic)
                if (_w.Objects.Get(id).Type == ObjectType.Shrine)
                {
                    _w.Objects.Remove(id); // OnObjectRemoved drops it from the village
                    return true;
                }
            return false;
        }

        public bool HasCivic(Settlement s, ObjectType type)
        {
            foreach (int id in s.Civic)
                if (_w.Objects.Get(id).Type == type) return true;
            return false;
        }

        // The nearest free ground to (cx, cy), spiralling out to `reach`; trees and rocks are cleared for it.
        bool PlaceCivic(Settlement s, ObjectType type, int cx, int cy, int reach, bool onWall)
        {
            int fw = ObjectInfo.FootprintW[(int)type], fh = ObjectInfo.FootprintH[(int)type];
            ushort owner = (ushort)(s.Id + 1);
            for (int r = 0; r <= reach; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                int ox = cx + dx - fw / 2, oy = cy + dy - fh / 2;
                if (!BuildingFits(ox, oy, fw, fh, owner, onWall)) continue;
                for (int y = oy; y < oy + fh; y++)
                for (int x = ox; x < ox + fw; x++)
                    _w.Objects.RemoveAtCell(x, y);
                int id = _w.Objects.Place(type, ox, oy, s.Roof);
                if (id < 0) continue;
                s.Civic.Add(id);
                _houseOwner[id] = s.Id;
                SetFootprintOwner(_w.Objects.Get(id), owner);
                return true;
            }
            return false;
        }

        bool BuildingFits(int ox, int oy, int fw, int fh, ushort owner, bool onWall)
        {
            for (int y = oy; y < oy + fh; y++)
            for (int x = ox; x < ox + fw; x++)
            {
                if (!_w.InBounds(x, y)) return false;
                int i = _w.Idx(x, y);
                if (_w.Owner[i] != 0 && _w.Owner[i] != owner) return false;
                if (!ObjectInfo.CanStandOn(ObjectType.House, _w.Terrain[i])) return false;
                if (!onWall && (_w.Zone[i] & ZoneFlags.Wall) != 0) return false;
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0 && ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) return false;
            }
            return true;
        }

        // Tường thành: a thành or a capital rings its houses with a wall, a gate in the middle of each side and
        // wherever a road comes in, a tháp canh at each corner. The ring moves out as the place grows.
        void UpdateWalls(Settlement s)
        {
            bool walled = !s.Sect && Standing(s) >= 2 && s.Houses.Count >= 6;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            if (walled)
            {
                foreach (int id in s.Houses) Extend(s, id, ref x0, ref y0, ref x1, ref y1);
                foreach (int id in s.Civic)
                    if (_w.Objects.Get(id).Type != ObjectType.Watchtower) Extend(s, id, ref x0, ref y0, ref x1, ref y1);
                x0 = Mathf.Max(1, x0 - 2);
                y0 = Mathf.Max(1, y0 - 2);
                x1 = Mathf.Min(_w.W - 2, x1 + 2);
                y1 = Mathf.Min(_w.H - 2, y1 + 2);
                if (x1 - x0 < 8 || y1 - y0 < 8) walled = false;
            }
            if (walled && s.WallX0 == x0 && s.WallY0 == y0 && s.WallX1 == x1 && s.WallY1 == y1) return;

            for (int k = s.Civic.Count - 1; k >= 0; k--)
                if (_w.Objects.Get(s.Civic[k]).Type == ObjectType.Watchtower) _w.Objects.Remove(s.Civic[k]);
            if (s.WallX0 >= 0) StampWall(s, false);
            s.WallX0 = -1;
            if (!walled) return;

            s.WallX0 = x0;
            s.WallY0 = y0;
            s.WallX1 = x1;
            s.WallY1 = y1;
            StampWall(s, true);
            PlaceCivic(s, ObjectType.Watchtower, x0 + 1, y0 + 1, 1, true);
            PlaceCivic(s, ObjectType.Watchtower, x1, y0 + 1, 1, true);
            PlaceCivic(s, ObjectType.Watchtower, x0 + 1, y1, 1, true);
            PlaceCivic(s, ObjectType.Watchtower, x1, y1, 1, true);
        }

        // Only what stands within the city proper (14 cells of its heart); stragglers beyond are its outskirts.
        void Extend(Settlement s, int id, ref int x0, ref int y0, ref int x1, ref int y1)
        {
            var o = _w.Objects.Get(id);
            int fw = ObjectInfo.FootprintW[(int)o.Type], fh = ObjectInfo.FootprintH[(int)o.Type];
            if (Mathf.Abs(o.X + fw / 2 - s.X) > 14 || Mathf.Abs(o.Y + fh / 2 - s.Y) > 14) return;
            x0 = Mathf.Min(x0, o.X);
            y0 = Mathf.Min(y0, o.Y);
            x1 = Mathf.Max(x1, o.X + fw - 1);
            y1 = Mathf.Max(y1, o.Y + fh - 1);
        }

        void StampWall(Settlement s, bool on)
        {
            int x0 = s.WallX0, y0 = s.WallY0, x1 = s.WallX1, y1 = s.WallY1;
            int mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (x != x0 && x != x1 && y != y0 && y != y1) continue;
                int i = _w.Idx(x, y);
                if (!on)
                {
                    _w.Zone[i] &= unchecked((byte)~ZoneFlags.Wall);
                    continue;
                }
                bool gate = ((y == y0 || y == y1) && (x == mx || x == mx + 1)) || ((x == x0 || x == x1) && (y == my || y == my + 1));
                if (gate || (_w.Zone[i] & ZoneFlags.Road) != 0) continue;
                var t = _w.Terrain[i];
                if (!TerrainInfo.IsWalkable(t) || t == Terrain.Peak) continue; // rivers and cliffs make their own wall
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0 && ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) continue;
                if (obj >= 0) _w.Objects.Remove(obj); // trees on the line are felled for the wall
                _w.Zone[i] |= ZoneFlags.Wall;
            }
            _w.NotifyLookChanged(x0, y0, x1, y1);
        }

        void ClearDevelopment(Settlement s)
        {
            if (s.WallX0 >= 0) StampWall(s, false);
            s.WallX0 = -1;
            for (int k = s.Civic.Count - 1; k >= 0; k--) _w.Objects.Remove(s.Civic[k]);
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
                if ((_w.Zone[i] & ZoneFlags.Wall) != 0) return false;
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
            // Settlers weigh a few good sites and take the richest soil: they find the silt a flood left and the
            // old ash of a volcano long before anyone tells them.
            tx = ty = 0;
            float best = -1f;
            int found = 0;
            for (int attempt = 0; attempt < 40 && found < 3; attempt++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f), dist = rng.Range(30f, 110f);
                int x = fromX + (int)(Mathf.Cos(angle) * dist), y = fromY + (int)(Mathf.Sin(angle) * dist);
                if (!SiteOk(x, y, true) || !PathClear(fromX, fromY, x, y)) continue;
                found++;
                float soil = SoilAround(x, y);
                if (soil <= best) continue;
                best = soil;
                tx = x;
                ty = y;
            }
            return found > 0;
        }

        float SoilAround(int x, int y)
        {
            float sum = 0f;
            for (int dy = -6; dy <= 6; dy += 3)
            for (int dx = -6; dx <= 6; dx += 3)
                if (_w.InBounds(x + dx, y + dy)) sum += _w.Fertility(_w.Idx(x + dx, y + dy));
            return sum;
        }

        bool SiteOk(int x, int y, bool needWater)
        {
            if (x < 8 || y < 8 || x >= _w.W - 8 || y >= _w.H - 8) return false;
            int i = _w.Idx(x, y);
            var t = _w.Terrain[i];
            if (!TerrainInfo.IsFarmable(t) || t == Terrain.Hills) return false;
            if ((_w.Zone[i] & ZoneFlags.Thunder) != 0) return false; // mortals give lôi địa a wide berth
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
            if (group.From >= 0 && group.From < All.Count) // migrants bring the news of the land they left
                _sim.Knowledge?.Carry(All[group.From].X + 0.5f, All[group.From].Y + 0.5f, cx + 0.5f, cy + 0.5f);

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
                Restore(s, tick);
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
