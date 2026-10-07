using System;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    public interface IWorldCommand
    {
        void Apply(Simulation sim);
    }

    public static class BrushShape
    {
        public static void ForEach(WorldData w, int cx, int cy, int size, Action<int, int, int, float> action)
        {
            float r = size * 0.5f;
            int ir = Mathf.CeilToInt(r);
            for (int y = cy - ir; y <= cy + ir; y++)
            for (int x = cx - ir; x <= cx + ir; x++)
            {
                if (!w.InBounds(x, y)) continue;
                float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 > r * r + r * 0.8f) continue;
                action(x, y, w.Idx(x, y), 1f - Mathf.Sqrt(d2) / (r + 1f));
            }
        }
    }

    public sealed class PaintTerrainCommand : IWorldCommand
    {
        public readonly int X, Y, Size;
        public readonly Terrain Terrain;

        public PaintTerrainCommand(int x, int y, int size, Terrain terrain)
        {
            X = x;
            Y = y;
            Size = size;
            Terrain = terrain;
        }

        public void Apply(Simulation sim)
        {
            var w = sim.World;
            bool any = false;
            BrushShape.ForEach(w, X, Y, Size, (x, y, i, k) =>
            {
                if (w.Terrain[i] == Terrain) return;
                w.Terrain[i] = Terrain;
                w.Height[i] = TerrainInfo.NominalHeight[(int)Terrain];
                sim.Scars.Clear(i); // new ground, no old marks
                int id = w.Objects.CellObject[i];
                if (id >= 0 && !ObjectInfo.CanStandOn(w.Objects.Get(id).Type, Terrain)) w.Objects.Remove(id);
                any = true;
            });
            if (!any) return;
            int x0 = X - Size, y0 = Y - Size, x1 = X + Size, y1 = Y + Size;
            // Water damps qi, so the cap under the stroke changes too.
            QiCap.Recompute(w, x0, y0, x1, y1);
            sim.Qi.RebuildCapBlocks(x0, y0, x1, y1);
            w.NotifyTerrainChanged(x0, y0, x1, y1);
            w.NotifyQiCapChanged(x0, y0, x1, y1);
            sim.Wildlife.LandChanged(x0, y0, x1, y1);
            if (TerrainInfo.IsWater(Terrain)) sim.ResolveFlood(x0, y0, x1, y1); // whoever stood there falls in
        }
    }

    public sealed class PlantTreesCommand : IWorldCommand
    {
        public readonly int X, Y, Size;
        public readonly uint Seed;

        public PlantTreesCommand(int x, int y, int size, uint seed)
        {
            X = x;
            Y = y;
            Size = size;
            Seed = seed;
        }

        public void Apply(Simulation sim)
        {
            var w = sim.World;
            var rng = new DetRandom(Seed);
            BrushShape.ForEach(w, X, Y, Size, (x, y, i, k) =>
            {
                if (rng.NextFloat() > 0.08f) return;
                var type = MapGenerator.TreeFor(w, i, rng.NextUInt());
                if (type != ObjectType.None) w.Objects.Place(type, x, y, (byte)rng.Range(0, 256));
            });
        }
    }

    public sealed class EraseObjectsCommand : IWorldCommand
    {
        public readonly int X, Y, Size;

        public EraseObjectsCommand(int x, int y, int size)
        {
            X = x;
            Y = y;
            Size = size;
        }

        public void Apply(Simulation sim)
        {
            var w = sim.World;
            BrushShape.ForEach(w, X, Y, Size, (x, y, i, k) => w.Objects.RemoveAtCell(x, y));
        }
    }

    // Footprint is centred on (X, Y); trees in the way are cleared, other buildings block.
    public sealed class PlaceBuildingCommand : IWorldCommand
    {
        public readonly ObjectType Type;
        public readonly int X, Y;
        public readonly byte Variant;

        public PlaceBuildingCommand(ObjectType type, int x, int y, byte variant)
        {
            Type = type;
            X = x;
            Y = y;
            Variant = variant;
        }

        public void Apply(Simulation sim)
        {
            var w = sim.World;
            int fw = ObjectInfo.FootprintW[(int)Type], fh = ObjectInfo.FootprintH[(int)Type];
            int ox = X - fw / 2, oy = Y - fh / 2;
            for (int y = oy; y < oy + fh; y++)
            for (int x = ox; x < ox + fw; x++)
            {
                if (!w.InBounds(x, y)) return;
                int i = w.Idx(x, y);
                if (!ObjectInfo.CanStandOn(Type, w.Terrain[i])) return;
                int id = w.Objects.CellObject[i];
                if (id >= 0 && ObjectInfo.IsBuilding(w.Objects.Get(id).Type)) return;
            }
            for (int y = oy; y < oy + fh; y++)
            for (int x = ox; x < ox + fw; x++)
                w.Objects.RemoveAtCell(x, y);
            w.Objects.Place(Type, ox, oy, Variant);
        }
    }

    // Drawing or breaking a ley line changes the cap; current qi then flows toward the new cap over months.
    public sealed class LeyLineCommand : IWorldCommand
    {
        public readonly int X, Y, Size;
        public readonly bool Add;

        public LeyLineCommand(int x, int y, int size, bool add)
        {
            X = x;
            Y = y;
            Size = size;
            Add = add;
        }

        public void Apply(Simulation sim)
        {
            var w = sim.World;
            bool any = false;
            BrushShape.ForEach(w, X, Y, Size, (x, y, i, k) =>
            {
                if (w.LeyLine[i] == Add) return;
                w.LeyLine[i] = Add;
                any = true;
            });
            if (!any) return;
            int r = Size + QiCap.LeyReach;
            QiCap.Recompute(w, X - r, Y - r, X + r, Y + r);
            sim.Qi.RebuildCapBlocks(X - r, Y - r, X + r, Y + r);
            w.NotifyQiCapChanged(X - r, Y - r, X + r, Y + r);
        }
    }

    // Adds animals to the population of the region under (X, Y).
    public sealed class SpawnCreaturesCommand : IWorldCommand
    {
        public readonly Species Species;
        public readonly int X, Y, Count;

        public SpawnCreaturesCommand(Species species, int x, int y, int count)
        {
            Species = species;
            X = x;
            Y = y;
            Count = count;
        }

        public void Apply(Simulation sim) => sim.Wildlife.Add(Species, X + 0.5f, Y + 0.5f, Count);
    }

    public sealed class FoundVillageCommand : IWorldCommand
    {
        public readonly int X, Y, People;
        public readonly byte Roof;

        public FoundVillageCommand(int x, int y, int people, byte roof)
        {
            X = x;
            Y = y;
            People = people;
            Roof = roof;
        }

        public void Apply(Simulation sim) => sim.Settlements.FoundVillage(X, Y, Roof, People, sim.Clock.Tick);
    }

    // Add a cultivator to (or drop them from) the player's watch list. Watched cultivators live by ProtagonistAI,
    // so this changes the simulation and goes through the command log like every other act of Thiên Đạo.
    public sealed class WatchCommand : IWorldCommand
    {
        public readonly int Target;
        public readonly bool On;

        public WatchCommand(int target, bool on)
        {
            Target = target;
            On = on;
        }

        public void Apply(Simulation sim)
        {
            var all = sim.Cultivation.All;
            if (Target < 0 || Target >= all.Count || (On && !all[Target].Alive)) return; // the fallen can still be dropped
            sim.Cultivation.SetWatched(all[Target], On, sim.Clock.Tick);
        }
    }

    public enum DivineAct : byte { GrantRoot, Bless, Smite, Tribulation, Revive, Annihilate }

    // Thiên Đạo acting on one being: the chosen cultivator (Target, an index into Cultivation.All) or a mortal of
    // the chosen village (Village, a settlement id). Without a target, Bless / Smite fall back to whoever is nearby.
    public sealed class DivineActCommand : IWorldCommand
    {
        const int CultivatorMask = 1 << (int)Species.Cultivator;
        public readonly DivineAct Act;
        public readonly int X, Y;
        public readonly int Target, Village;

        public DivineActCommand(DivineAct act, int x, int y, int target = -1, int village = -1)
        {
            Act = act;
            X = x;
            Y = y;
            Target = target;
            Village = village;
        }

        public void Apply(Simulation sim)
        {
            long tick = sim.Clock.Tick;
            var all = sim.Cultivation.All;
            if (Act == DivineAct.Revive) // the one act aimed at the dead
            {
                if (Target >= 0 && Target < all.Count) sim.Cultivation.Revive(all[Target], tick);
                return;
            }
            var chosen = Target >= 0 && Target < all.Count && all[Target].Alive ? all[Target] : null;
            var village = Village >= 0 && Village < sim.Settlements.All.Count && sim.Settlements.All[Village].Alive ? sim.Settlements.All[Village] : null;
            if (Act == DivineAct.GrantRoot)
            {
                if (chosen != null) sim.Cultivation.GrantRootTo(chosen, tick);
                else if (village != null) sim.Cultivation.AwakenMortal(village, tick);
                return; // a spirit root is given to someone, never to empty ground
            }
            if (Act == DivineAct.Annihilate)
            {
                if (village != null && village.Sect) sim.Factions.Annihilate(village.Id, tick); // only a sect can be wiped out
                return;
            }
            if (Act == DivineAct.Tribulation)
            {
                sim.Cultivation.CallTribulation(chosen, tick); // only ever on the chosen cultivator
                return;
            }
            if (Act == DivineAct.Bless && chosen == null && village != null)
            {
                village.Food += village.Population * 6f; // a good harvest for the chosen village
                sim.Events.Add(tick, EventKind.Divine, 1, $"Thiên Đạo ban phúc cho {village.Name}, mùa màng bội thu.", village.X + 0.5f, village.Y + 0.5f, Fx.Blessing);
                return;
            }
            // Prefer the chosen one; otherwise someone visible under the cursor, or whoever is meditating nearby.
            var c = chosen ?? (Target >= 0 ? null : sim.Cultivation.FindShownNear(X + 0.5f, Y + 0.5f, 3f) ??
                                                  sim.Cultivation.ForEntity(sim.Creatures.FindNearest(X + 0.5f, Y + 0.5f, 6f, CultivatorMask)));
            if (village != null && chosen == null) c = null; // a village was chosen: the bolt falls on it
            if (Act == DivineAct.Bless)
            {
                sim.Cultivation.Bless(c, tick);
                return;
            }
            if (c != null)
            {
                sim.Disasters.Blasted((int)sim.Entities.X[c.Entity], (int)sim.Entities.Y[c.Entity], 3, tick);
                sim.Cultivation.Smite(c, tick);
                return;
            }
            // Nobody to punish: the bolt still falls, scorching the ground and anyone living there.
            var w = sim.World;
            for (int y = Y - 2; y <= Y + 2; y++)
            for (int x = X - 2; x <= X + 2; x++)
            {
                if (!w.InBounds(x, y)) continue;
                int id = w.Objects.CellObject[w.Idx(x, y)];
                if (id >= 0 && !ObjectInfo.IsBuilding(w.Objects.Get(id).Type)) w.Objects.Remove(id);
            }
            sim.Settlements.Strike(X, Y, tick);
            sim.Disasters.Blasted(X, Y, 3, tick);
            sim.Events.Add(tick, EventKind.Divine, 0, "Thiên lôi giáng xuống.", X + 0.5f, Y + 0.5f, Fx.Lightning);
        }
    }

    // Thiên Đạo looses a yêu thú of the given grade at (X, Y).
    public sealed class SpawnBeastCommand : IWorldCommand
    {
        public readonly int X, Y, Grade;

        public SpawnBeastCommand(int x, int y, int grade)
        {
            X = x;
            Y = y;
            Grade = grade;
        }

        public void Apply(Simulation sim)
        {
            if (!sim.World.IsWalkable(X + 0.5f, Y + 0.5f)) return;
            var rng = new DetRandom(Hash.U32(sim.World.Seed ^ 0x5BEAu, (int)sim.Clock.Tick, X * 4099 + Y));
            var b = sim.Beasts.Spawn(Species.Wolf, Grade, X + 0.5f, Y + 0.5f, sim.Clock.Tick, ref rng);
            sim.Events.Add(sim.Clock.Tick, EventKind.Beast, 2, $"Thiên Đạo thả {b.Name} ({b.GradeText}) xuống nhân gian.", X + 0.5f, Y + 0.5f, Fx.Stampede);
        }
    }

    // Thiên tai sent by Thiên Đạo at (X, Y); Size is the brush size (the reach follows DisasterSystem.Radius).
    public sealed class CalamityCommand : IWorldCommand
    {
        public readonly Calamity Kind;
        public readonly int X, Y, Size;

        public CalamityCommand(Calamity kind, int x, int y, int size)
        {
            Kind = kind;
            X = x;
            Y = y;
            Size = size;
        }

        public void Apply(Simulation sim) => sim.Disasters.Unleash(Kind, X, Y, Size, sim.Clock.Tick, true);
    }

    // Positive amount pours qi in, negative drains it; amount is a fraction of MaxQi at the centre.
    public sealed class InfuseQiCommand : IWorldCommand
    {
        public readonly int X, Y, Size;
        public readonly float Amount;

        public InfuseQiCommand(int x, int y, int size, float amount)
        {
            X = x;
            Y = y;
            Size = size;
            Amount = amount;
        }

        public void Apply(Simulation sim) => sim.Qi.AddQi(X, Y, Mathf.Max(QiSystem.Block, Size / 2), Amount * WorldData.MaxQi);
    }
}
