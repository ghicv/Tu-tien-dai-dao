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
