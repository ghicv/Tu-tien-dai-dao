using ThienDao.Core;
using ThienDao.Render;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Player
{
    public enum BrushTool
    {
        Inspect,
        Grass,
        Sand,
        Desert,
        Shallow,
        DeepWater,
        Hills,
        Mountain,
        Snow,
        Trees,
        House,
        SectHall,
        Erase,
        QiUp,
        QiDown
    }

    public sealed class WorldBrush
    {
        public static readonly string[] ToolNames =
        {
            "Xem", "Cỏ", "Cát", "Sa mạc", "Nước nông", "Biển sâu", "Đồi", "Núi", "Tuyết",
            "Trồng cây", "Nhà dân", "Tông môn", "Xóa vật", "Linh khí +", "Linh khí −"
        };

        readonly WorldData _world;
        readonly WorldRenderer _renderer;
        DetRandom _rng;
        float _cooldown;

        public BrushTool Tool = BrushTool.Inspect;
        public int Size = 6;

        public WorldBrush(WorldData world, WorldRenderer renderer)
        {
            _world = world;
            _renderer = renderer;
            _rng = new DetRandom(world.Seed ^ 0xB2B2u);
        }

        public static bool IsBuildingTool(BrushTool t) => t == BrushTool.House || t == BrushTool.SectHall;

        public static ObjectType BuildingFor(BrushTool t) => t == BrushTool.SectHall ? ObjectType.SectHall : ObjectType.House;

        public void Apply(int cx, int cy, bool pressedThisFrame, float dt)
        {
            if (Tool == BrushTool.Inspect || !_world.InBounds(cx, cy)) return;

            if (IsBuildingTool(Tool))
            {
                if (pressedThisFrame) PlaceBuilding(BuildingFor(Tool), cx, cy);
                return;
            }

            _cooldown -= dt;
            if (!pressedThisFrame && _cooldown > 0f) return;
            _cooldown = 1f / 30f;

            switch (Tool)
            {
                case BrushTool.Trees: PlantTrees(cx, cy); break;
                case BrushTool.Erase: ForEachCell(cx, cy, (x, y, i, k) => _world.Objects.RemoveAtCell(x, y)); break;
                case BrushTool.QiUp: AddQi(cx, cy, +1); break;
                case BrushTool.QiDown: AddQi(cx, cy, -1); break;
                default: PaintTerrain(cx, cy, TerrainFor(Tool)); break;
            }
        }

        static Terrain TerrainFor(BrushTool t)
        {
            switch (t)
            {
                case BrushTool.Sand: return Terrain.Beach;
                case BrushTool.Desert: return Terrain.Desert;
                case BrushTool.Shallow: return Terrain.Shallow;
                case BrushTool.DeepWater: return Terrain.DeepOcean;
                case BrushTool.Hills: return Terrain.Hills;
                case BrushTool.Mountain: return Terrain.Mountain;
                case BrushTool.Snow: return Terrain.Snow;
                default: return Terrain.Grass;
            }
        }

        delegate void CellAction(int x, int y, int i, float falloff);

        void ForEachCell(int cx, int cy, CellAction action)
        {
            float r = Size * 0.5f;
            int ir = Mathf.CeilToInt(r);
            for (int y = cy - ir; y <= cy + ir; y++)
            for (int x = cx - ir; x <= cx + ir; x++)
            {
                if (!_world.InBounds(x, y)) continue;
                float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 > r * r + r * 0.8f) continue;
                action(x, y, _world.Idx(x, y), 1f - Mathf.Sqrt(d2) / (r + 1f));
            }
        }

        void PaintTerrain(int cx, int cy, Terrain terrain)
        {
            bool any = false;
            ForEachCell(cx, cy, (x, y, i, k) =>
            {
                if (_world.Terrain[i] == terrain) return;
                _world.Terrain[i] = terrain;
                _world.Height[i] = TerrainInfo.NominalHeight[(int)terrain];
                int id = _world.Objects.CellObject[i];
                if (id >= 0 && !ObjectInfo.CanStandOn(_world.Objects.Get(id).Type, terrain)) _world.Objects.Remove(id);
                any = true;
            });
            if (any) _renderer.TerrainChanged(cx - Size, cy - Size, cx + Size, cy + Size);
        }

        void PlantTrees(int cx, int cy)
        {
            ForEachCell(cx, cy, (x, y, i, k) =>
            {
                if (_rng.NextFloat() > 0.08f) return;
                var type = MapGenerator.TreeFor(_world, i, _rng.NextUInt());
                if (type != ObjectType.None) _world.Objects.Place(type, x, y, (byte)_rng.Range(0, 256));
            });
        }

        void AddQi(int cx, int cy, int sign)
        {
            ForEachCell(cx, cy, (x, y, i, k) =>
            {
                int v = _world.QiCap[i] + sign * (int)(WorldData.MaxQi * 0.04f * k);
                ushort q = (ushort)Mathf.Clamp(v, 0, WorldData.MaxQi);
                _world.QiCap[i] = q;
                _world.Qi[i] = q;
            });
            _renderer.QiChanged();
        }

        // Footprint is centred on the cursor; trees in the way are cleared, other buildings block.
        public bool PlaceBuilding(ObjectType type, int cx, int cy)
        {
            int fw = ObjectInfo.FootprintW[(int)type], fh = ObjectInfo.FootprintH[(int)type];
            int ox = cx - fw / 2, oy = cy - fh / 2;
            for (int y = oy; y < oy + fh; y++)
            for (int x = ox; x < ox + fw; x++)
            {
                if (!_world.InBounds(x, y)) return false;
                int i = _world.Idx(x, y);
                if (!ObjectInfo.CanStandOn(type, _world.Terrain[i])) return false;
                int id = _world.Objects.CellObject[i];
                if (id >= 0 && ObjectInfo.IsBuilding(_world.Objects.Get(id).Type)) return false;
            }
            for (int y = oy; y < oy + fh; y++)
            for (int x = ox; x < ox + fw; x++)
                _world.Objects.RemoveAtCell(x, y);
            int variants = SpriteLibrary.VariantCount(type);
            return _world.Objects.Place(type, ox, oy, (byte)_rng.Range(0, variants)) >= 0;
        }
    }
}
