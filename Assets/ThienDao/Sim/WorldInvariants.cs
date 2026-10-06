using System.Collections.Generic;
using ThienDao.World;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // Things that must always hold, whatever Thiên Đạo does to the world. Used by tests; cheap enough for a debug key.
    public static class WorldInvariants
    {
        public static List<string> Check(Simulation sim)
        {
            var errors = new List<string>();
            var w = sim.World;
            var objects = w.Objects;
            var st = sim.Settlements;

            var ownedByHouse = new HashSet<int>();
            foreach (var s in st.All)
            {
                if (!s.Alive) continue;
                string who = $"{s.Name}#{s.Id}";
                ushort owner = (ushort)(s.Id + 1);
                var t = w.Terrain[w.Idx(s.X, s.Y)];
                if (t != Terrain.Farmland && !ObjectInfo.CanStandOn(ObjectType.House, t))
                    errors.Add($"{who} sits on unsettleable {t}");
                if (s.Food < 0f) errors.Add($"{who} has negative food");
                foreach (int c in s.Cohorts)
                    if (c < 0) errors.Add($"{who} has a negative cohort");
                foreach (int h in s.Houses)
                {
                    if (!objects.IsAlive(h) || objects.Get(h).Type != ObjectType.House)
                    {
                        errors.Add($"{who} lists a missing house {h}");
                        continue;
                    }
                    var o = objects.Get(h);
                    for (int y = o.Y; y < o.Y + 3; y++)
                    for (int x = o.X; x < o.X + 3; x++)
                    {
                        int i = w.Idx(x, y);
                        ownedByHouse.Add(i);
                        if (w.Owner[i] != owner) errors.Add($"{who} house cell ({x},{y}) not owned by it");
                    }
                }
                foreach (int i in s.Farms)
                    if (w.Terrain[i] != Terrain.Farmland || w.Owner[i] != owner) errors.Add($"{who} farm {i} is not its farmland");
            }

            for (int i = 0; i < w.Owner.Length && errors.Count < 50; i++)
            {
                int o = w.Owner[i];
                if (o == 0) continue;
                var s = o <= st.All.Count ? st.All[o - 1] : null;
                if (s == null || !s.Alive) errors.Add($"cell {i} owned by dead/unknown settlement {o - 1}");
                else if (w.Terrain[i] != Terrain.Farmland && !ownedByHouse.Contains(i)) errors.Add($"cell {i} owned by {s.Name} but is neither field nor house");
            }

            var cs = sim.Cultivation;
            var e = sim.Entities;
            int alive = 0;
            var byRealm = new int[(int)Realm.Count];
            foreach (var c in cs.All)
            {
                if (!c.Alive) continue;
                alive++;
                byRealm[(int)c.Realm]++;
                if (!e.IsAlive(c.Entity) || e.Species[c.Entity] != Species.Cultivator || e.Payload[c.Entity] != c.Index)
                    errors.Add($"cultivator {c.Name} has a broken entity link");
                if (cs.IsAtHome(c) && (!w.IsWalkable(c.HomeX, c.HomeY) || !w.IsWalkable(e.X[c.Entity], e.Y[c.Entity])))
                    errors.Add($"cultivator {c.Name} is at home on water/peak");
                if (c.SectId >= 0 && !st.All[c.SectId].Alive) errors.Add($"cultivator {c.Name} belongs to a dead sect");
            }
            if (alive != cs.AliveCount) errors.Add($"cultivator count {cs.AliveCount} != {alive}");
            for (int r = 0; r < byRealm.Length; r++)
                if (byRealm[r] != cs.CountByRealm[r]) errors.Add($"realm {r} count {cs.CountByRealm[r]} != {byRealm[r]}");

            for (int id = 0; id < e.Count; id++)
                if (e.Species[id] == Species.Migrants && sim.Clock.Tick - e.BirthTick[id] > 300) errors.Add($"migrant group {id} wandering forever");

            foreach (var kind in WildlifeSystem.Kinds)
            {
                float total = sim.Wildlife.Total(kind);
                if (float.IsNaN(total) || total < 0f) errors.Add($"wildlife {kind} total is {total}");
            }

            // Thế lực: every living sect is a living faction and the other way round; land and wars only among the living.
            var fs = sim.Factions;
            int aliveFactions = 0;
            foreach (var f in fs.All)
            {
                var s = st.All[f.Id];
                if (f.Alive)
                {
                    aliveFactions++;
                    if (!s.Alive || !s.Sect) errors.Add($"faction {f.Id} alive but its seat {s.Name} is not a living sect");
                    if (float.IsNaN(f.Treasury) || f.Treasury < 0f) errors.Add($"faction {s.BaseName} treasury {f.Treasury}");
                }
            }
            if (aliveFactions != fs.AliveCount) errors.Add($"faction count {fs.AliveCount} != {aliveFactions}");
            foreach (var s in st.All)
                if (s.Alive && s.Sect && (fs.Get(s.Id) == null || !fs.Get(s.Id).Alive)) errors.Add($"sect {s.Name} has no living faction");
            for (int t = 0; t < fs.TileOwner.Length; t++)
            {
                int o = fs.TileOwner[t];
                if (o != 0 && (fs.Get(o - 1) == null || !fs.Get(o - 1).Alive)) errors.Add($"territory tile {t} held by dead faction {o - 1}");
            }
            foreach (var r in fs.Relations)
            {
                if (fs.Get(r.A) == null || !fs.Get(r.A).Alive || fs.Get(r.B) == null || !fs.Get(r.B).Alive)
                    errors.Add($"relation {r.A}-{r.B} involves a dead faction");
                if (float.IsNaN(r.Opinion) || r.Opinion < -100f || r.Opinion > 100f) errors.Add($"relation {r.A}-{r.B} opinion {r.Opinion}");
            }
            foreach (var c in cs.All)
                if (c.Alive && c.SectId >= 0 && (fs.Get(c.SectId) == null || !fs.Get(c.SectId).Alive))
                    errors.Add($"cultivator {c.Name} belongs to a dead faction");

            // Lịch sử: links between people point at real people; pursuits only after the living.
            int n = cs.All.Count;
            foreach (var c in cs.All)
            {
                if (c.MasterIdx >= n || c.Nemesis >= n || c.NemesisFor >= n || c.KilledBy >= n || c.HuntTarget >= n)
                    errors.Add($"cultivator {c.Name} links to an unknown person");
                if (c.MasterIdx == c.Index || c.Nemesis == c.Index) errors.Add($"cultivator {c.Name} is their own master/nemesis");
                if (c.Alive && c.HuntTarget >= 0 && !cs.All[c.HuntTarget].Alive) errors.Add($"cultivator {c.Name} hunts the dead");
                if (!c.Alive && (c.AtWar || c.HuntTarget >= 0)) errors.Add($"dead cultivator {c.Name} still at war / hunting");
            }
            // Yêu thú: entity links, counts by grade, clans only around living kings.
            var bs = sim.Beasts;
            var byGrade = new int[10];
            int beasts = 0;
            foreach (var b in bs.All)
            {
                if (!b.Alive) continue;
                beasts++;
                byGrade[b.Grade]++;
                if (!e.IsAlive(b.Entity) || e.Species[b.Entity] != Species.Beast || e.Payload[b.Entity] != b.Index)
                    errors.Add($"beast {b.Name} has a broken entity link");
                if (b.Clan >= 0 && (!bs.All[b.Clan].Alive || !bs.All[b.Clan].IsKing)) errors.Add($"beast {b.Name} follows a dead or deposed king");
            }
            if (beasts != bs.AliveCount) errors.Add($"beast count {bs.AliveCount} != {beasts}");
            for (int g = 0; g < 10; g++)
                if (byGrade[g] != bs.CountByGrade[g]) errors.Add($"beast grade {g} count {bs.CountByGrade[g]} != {byGrade[g]}");

            long last = -1;
            foreach (var r in sim.History.All)
            {
                if (r.Tick < last) { errors.Add("history out of order"); break; }
                last = r.Tick;
            }
            return errors;
        }
    }
}
