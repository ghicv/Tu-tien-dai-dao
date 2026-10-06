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
            return errors;
        }
    }
}
