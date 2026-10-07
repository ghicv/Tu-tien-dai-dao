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
        FoundVillage,
        GrantRoot,
        Bless,
        Smite,
        Tribulation,
        Earthquake,
        Eruption,
        Flood,
        Drought,
        Plague,
        BeastTide,
        GreatCalamity, // the order from Earthquake to Cold follows the Calamity enum
        Rain,
        Storm,
        Cold,
        Annihilate,
        SpawnBeast,
        SpawnCultivator, // devlog 26: the powers that were missing
        SpawnHungThu,
        GrantTreasure,
        HeartDemon,
        Cripple,
        PlaceTreasure,
        OpenRealm
    }

    // Turns mouse input into world commands; it never mutates the world itself.
    public sealed class WorldBrush
    {
        public static readonly string[] ToolNames =
        {
            "Xem", "Cỏ", "Cát", "Sa mạc", "Nước nông", "Biển sâu", "Đồi", "Núi", "Tuyết",
            "Trồng cây", "Nhà dân", "Tông môn", "Xóa vật", "Vẽ linh mạch", "Phá linh mạch",
            "Rót linh khí", "Hút linh khí", "Thả hươu", "Thả thỏ", "Thả sói", "Lập làng",
            "Ban linh căn", "Ban cơ duyên", "Thiên phạt", "Thiên kiếp",
            "Động đất", "Núi lửa", "Lũ lụt", "Hạn hán", "Ôn dịch", "Thú triều", "Đại kiếp", "Mưa", "Bão", "Rét", "Diệt môn", "Thả yêu thú",
            "Thả tu sĩ", "Đánh thức hung thú", "Ban pháp bảo", "Giáng tâm ma", "Phế tu vi", "Thiên tài địa bảo", "Mở bí cảnh"
        };

        readonly Simulation _sim;
        DetRandom _rng;
        float _cooldown;

        public BrushTool Tool = BrushTool.Inspect;
        public int Size = 6;

        // Tools that send a being down choose how strong it is (the realm, the grade) instead of a brush size.
        readonly int[] _levels = new int[ToolNames.Length];

        public static bool HasLevel(BrushTool t) => t == BrushTool.SpawnCultivator || t == BrushTool.SpawnBeast || t == BrushTool.SpawnHungThu;
        public static int MinLevel(BrushTool t) => t == BrushTool.SpawnHungThu ? 5 : 1;
        public static int MaxLevel(BrushTool t) => t == BrushTool.SpawnCultivator ? (int)Realm.HoaThan : 9;
        static int DefaultLevel(BrushTool t) => t == BrushTool.SpawnCultivator ? (int)Realm.TrucCo : t == BrushTool.SpawnHungThu ? 7 : 3;

        public int LevelOf(BrushTool t) => _levels[(int)t] > 0 ? _levels[(int)t] : DefaultLevel(t);
        public int Level => LevelOf(Tool);
        public void StepLevel(int d) => _levels[(int)Tool] = UnityEngine.Mathf.Clamp(Level + d, MinLevel(Tool), MaxLevel(Tool));

        // "Kết Đan", "Lục giai".
        public static string LevelName(BrushTool t, int level) =>
            t == BrushTool.SpawnCultivator ? Realms.Names[UnityEngine.Mathf.Clamp(level, 1, (int)Realm.HoaThan)] : Beast.GradeNames[UnityEngine.Mathf.Clamp(level, 1, 9)];

        // Presentation hook: animals were just dropped at this spot (the sim only tracks region totals).
        public event System.Action<Species, float, float> Spawned;

        public WorldBrush(Simulation sim)
        {
            _sim = sim;
            _rng = new DetRandom(sim.World.Seed ^ 0xB2B2u);
        }

        public static bool IsBuildingTool(BrushTool t) => t == BrushTool.House || t == BrushTool.SectHall;

        public static bool IsDivineTool(BrushTool t) =>
            t == BrushTool.GrantRoot || t == BrushTool.Bless || t == BrushTool.Smite || t == BrushTool.Tribulation || t == BrushTool.Annihilate ||
            t == BrushTool.GrantTreasure || t == BrushTool.HeartDemon || t == BrushTool.Cripple;

        public static DivineAct ActFor(BrushTool t)
        {
            switch (t)
            {
                case BrushTool.GrantRoot: return DivineAct.GrantRoot;
                case BrushTool.Bless: return DivineAct.Bless;
                case BrushTool.Tribulation: return DivineAct.Tribulation;
                case BrushTool.Annihilate: return DivineAct.Annihilate;
                case BrushTool.GrantTreasure: return DivineAct.GrantTreasure;
                case BrushTool.HeartDemon: return DivineAct.HeartDemon;
                case BrushTool.Cripple: return DivineAct.Cripple;
                default: return DivineAct.Smite;
            }
        }

        public static bool IsCalamityTool(BrushTool t) => t >= BrushTool.Earthquake && t <= BrushTool.Cold;

        public static Calamity CalamityFor(BrushTool t) => (Calamity)(t - BrushTool.Earthquake);

        // Tools that act on one spot rather than painting an area.
        public static bool IsPointTool(BrushTool t) =>
            t == BrushTool.Inspect || t == BrushTool.FoundVillage || t == BrushTool.SpawnDeer || t == BrushTool.SpawnRabbit || t == BrushTool.SpawnWolf ||
            t == BrushTool.SpawnBeast || t == BrushTool.SpawnCultivator || t == BrushTool.SpawnHungThu || t == BrushTool.PlaceTreasure || t == BrushTool.OpenRealm;

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
            if (Tool == BrushTool.SpawnBeast || Tool == BrushTool.SpawnHungThu || Tool == BrushTool.SpawnCultivator ||
                Tool == BrushTool.PlaceTreasure || Tool == BrushTool.OpenRealm)
            {
                if (!pressedThisFrame) return; // one per click
                switch (Tool)
                {
                    case BrushTool.SpawnBeast: _sim.Enqueue(new SpawnBeastCommand(cx, cy, Level)); break;
                    case BrushTool.SpawnHungThu: _sim.Enqueue(new SpawnHungThuCommand(cx, cy, Level)); break;
                    case BrushTool.SpawnCultivator: _sim.Enqueue(new SpawnCultivatorCommand(cx, cy, (Realm)Level)); break;
                    default: _sim.Enqueue(new PlaceRelicCommand(cx, cy, Tool == BrushTool.OpenRealm)); break;
                }
                return;
            }
            if (Tool == BrushTool.FoundVillage)
            {
                if (pressedThisFrame) _sim.Enqueue(new FoundVillageCommand(cx, cy, 24, (byte)_rng.Range(0, 4)));
                return;
            }
            if (IsDivineTool(Tool)) return; // aimed at whoever is under the pointer: see WorldBootstrap.ActOn
            if (IsCalamityTool(Tool))
            {
                if (pressedThisFrame) _sim.Enqueue(new CalamityCommand(CalamityFor(Tool), cx, cy, Size)); // one calamity per click
                return;
            }
            if (Tool == BrushTool.SpawnDeer || Tool == BrushTool.SpawnRabbit || Tool == BrushTool.SpawnWolf)
            {
                _cooldown -= dt;
                if (!pressedThisFrame && _cooldown > 0f) return;
                _cooldown = 0.15f;
                var species = Tool == BrushTool.SpawnDeer ? Species.Deer : Tool == BrushTool.SpawnRabbit ? Species.Rabbit : Species.Wolf;
                _sim.Enqueue(new SpawnCreaturesCommand(species, cx, cy, species == Species.Wolf ? 2 : 10));
                Spawned?.Invoke(species, cx + 0.5f, cy + 0.5f);
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
