using ThienDao.Core;
using ThienDao.Render;
using ThienDao.Sim;
using ThienDao.World;
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
        LeyAdd,
        LeyErase,
        QiInfuse,
        QiDrain,
        SpawnDeer,
        SpawnRabbit,
        SpawnWolf,
        FoundVillage
    }

    // Turns mouse input into world commands; it never mutates the world itself.
    public sealed class WorldBrush
    {
        public static readonly string[] ToolNames =
        {
            "Xem", "Cỏ", "Cát", "Sa mạc", "Nước nông", "Biển sâu", "Đồi", "Núi", "Tuyết",
            "Trồng cây", "Nhà dân", "Tông môn", "Xóa vật", "Vẽ linh mạch", "Phá linh mạch",
            "Rót linh khí", "Hút linh khí", "Thả hươu", "Thả thỏ", "Thả sói", "Lập làng"
        };

        readonly Simulation _sim;
        DetRandom _rng;
        float _cooldown;

        public BrushTool Tool = BrushTool.Inspect;
        public int Size = 6;

        public WorldBrush(Simulation sim)
        {
            _sim = sim;
            _rng = new DetRandom(sim.World.Seed ^ 0xB2B2u);
        }

        public static bool IsBuildingTool(BrushTool t) => t == BrushTool.House || t == BrushTool.SectHall;

        public static ObjectType BuildingFor(BrushTool t) => t == BrushTool.SectHall ? ObjectType.SectHall : ObjectType.House;

        public void Apply(int cx, int cy, bool pressedThisFrame, float dt)
        {
            if (Tool == BrushTool.Inspect || !_sim.World.InBounds(cx, cy)) return;

            if (IsBuildingTool(Tool))
            {
                if (!pressedThisFrame) return;
                var type = BuildingFor(Tool);
                _sim.Enqueue(new PlaceBuildingCommand(type, cx, cy, (byte)_rng.Range(0, SpriteLibrary.VariantCount(type))));
                return;
            }
            if (Tool == BrushTool.FoundVillage)
            {
                if (pressedThisFrame) _sim.Enqueue(new FoundVillageCommand(cx, cy, 24, (byte)_rng.Range(0, 4)));
                return;
            }
            if (Tool == BrushTool.SpawnDeer || Tool == BrushTool.SpawnRabbit || Tool == BrushTool.SpawnWolf)
            {
                _cooldown -= dt;
                if (!pressedThisFrame && _cooldown > 0f) return;
                _cooldown = 0.15f;
                var species = Tool == BrushTool.SpawnDeer ? Species.Deer : Tool == BrushTool.SpawnRabbit ? Species.Rabbit : Species.Wolf;
                _sim.Enqueue(new SpawnCreaturesCommand(species, cx, cy, Size, System.Math.Max(1, Size / 2), _rng.NextUInt()));
                return;
            }

            _cooldown -= dt;
            if (!pressedThisFrame && _cooldown > 0f) return;
            _cooldown = 1f / 30f;

            switch (Tool)
            {
                case BrushTool.Trees: _sim.Enqueue(new PlantTreesCommand(cx, cy, Size, _rng.NextUInt())); break;
                case BrushTool.Erase: _sim.Enqueue(new EraseObjectsCommand(cx, cy, Size)); break;
                case BrushTool.LeyAdd: _sim.Enqueue(new LeyLineCommand(cx, cy, Size, true)); break;
                case BrushTool.LeyErase: _sim.Enqueue(new LeyLineCommand(cx, cy, Size, false)); break;
                case BrushTool.QiInfuse: _sim.Enqueue(new InfuseQiCommand(cx, cy, Size, 0.06f)); break;
                case BrushTool.QiDrain: _sim.Enqueue(new InfuseQiCommand(cx, cy, Size, -0.06f)); break;
                default: _sim.Enqueue(new PaintTerrainCommand(cx, cy, Size, TerrainFor(Tool))); break;
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
    }
}
