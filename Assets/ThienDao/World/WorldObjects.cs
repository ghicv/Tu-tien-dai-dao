using System;
using System.Collections.Generic;

namespace ThienDao.World
{
    public struct WorldObject
    {
        public ObjectType Type; // None = free slot
        public int X, Y;        // bottom-left cell of the footprint
        public byte Variant;
    }

    public sealed class WorldObjects
    {
        readonly WorldData _world;
        WorldObject[] _items = new WorldObject[4096];
        int _count;
        readonly Stack<int> _free = new Stack<int>();

        public readonly int[] CellObject; // object id occupying the cell, -1 = empty

        public event Action<int> Added;
        public event Action<int, WorldObject> Removed;

        public int Capacity => _count;
        public int AliveCount { get; private set; }

        public WorldObjects(WorldData world)
        {
            _world = world;
            CellObject = new int[world.W * world.H];
            for (int i = 0; i < CellObject.Length; i++) CellObject[i] = -1;
        }

        public ref WorldObject Get(int id) => ref _items[id];
        public bool IsAlive(int id) => id >= 0 && id < _count && _items[id].Type != ObjectType.None;

        public bool CanPlace(ObjectType type, int x, int y)
        {
            int fw = ObjectInfo.FootprintW[(int)type];
            int fh = ObjectInfo.FootprintH[(int)type];
            for (int dy = 0; dy < fh; dy++)
            for (int dx = 0; dx < fw; dx++)
            {
                int cx = x + dx, cy = y + dy;
                if (!_world.InBounds(cx, cy)) return false;
                int i = _world.Idx(cx, cy);
                if (CellObject[i] >= 0) return false;
                if (!ObjectInfo.CanStandOn(type, _world.Terrain[i])) return false;
            }
            return true;
        }

        public int Place(ObjectType type, int x, int y, byte variant)
        {
            if (!CanPlace(type, x, y)) return -1;
            int id;
            if (_free.Count > 0) id = _free.Pop();
            else
            {
                if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
                id = _count++;
            }
            _items[id] = new WorldObject { Type = type, X = x, Y = y, Variant = variant };
            int fw = ObjectInfo.FootprintW[(int)type];
            int fh = ObjectInfo.FootprintH[(int)type];
            for (int dy = 0; dy < fh; dy++)
            for (int dx = 0; dx < fw; dx++)
                CellObject[_world.Idx(x + dx, y + dy)] = id;
            AliveCount++;
            Added?.Invoke(id);
            return id;
        }

        public void Remove(int id)
        {
            if (!IsAlive(id)) return;
            var obj = _items[id];
            int fw = ObjectInfo.FootprintW[(int)obj.Type];
            int fh = ObjectInfo.FootprintH[(int)obj.Type];
            for (int dy = 0; dy < fh; dy++)
            for (int dx = 0; dx < fw; dx++)
                CellObject[_world.Idx(obj.X + dx, obj.Y + dy)] = -1;
            _items[id].Type = ObjectType.None;
            _free.Push(id);
            AliveCount--;
            Removed?.Invoke(id, obj);
        }

        public void RemoveAtCell(int x, int y)
        {
            if (!_world.InBounds(x, y)) return;
            int id = CellObject[_world.Idx(x, y)];
            if (id >= 0) Remove(id);
        }
    }
}
