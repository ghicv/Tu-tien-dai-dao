using System.Collections.Generic;
using System.Diagnostics;
using ThienDao.Core;
using ThienDao.World;

namespace ThienDao.Sim
{
    public readonly struct LoggedCommand
    {
        public readonly long Tick;
        public readonly IWorldCommand Command;

        public LoggedCommand(long tick, IWorldCommand command)
        {
            Tick = tick;
            Command = command;
        }
    }

    // Pure simulation: no rendering, no Unity objects. Player intent enters only through commands,
    // which are applied between ticks and logged so a run can be replayed exactly from seed + log.
    public sealed class Simulation
    {
        public static readonly float[] SpeedDaysPerSecond = { 0f, 6f, 30f, 120f, 3600f };
        public static readonly string[] SpeedNames = { "Dừng", "x1", "x5", "x20", "Tua" };

        public readonly WorldData World;
        public readonly SimClock Clock = new SimClock();
        public readonly QiSystem Qi;
        public readonly ForageSystem Forage;
        public readonly NavSystem Nav;
        public readonly PathSystem Paths;
        public readonly EntityStore Entities = new EntityStore();
        public readonly CreatureSystem Creatures;
        public readonly WildlifeSystem Wildlife;
        public readonly SettlementSystem Settlements;
        public readonly CultivationSystem Cultivation;
        public readonly FactionSystem Factions;
        public readonly TechniqueSystem Techniques;
        public readonly CombatSystem Combat;
        public readonly HistoryLog History = new HistoryLog();
        public readonly StoryDetector Stories;
        public readonly ProtagonistAI Protagonists;
        public readonly ScarSystem Scars;
        public readonly DisasterSystem Disasters;
        public readonly BeastSystem Beasts;
        public readonly RelicSystem Relics;
        public readonly TradeSystem Trade;
        public readonly EraSystem Eras;
        public readonly HarmSystem Harm;
        public readonly FaithSystem Faith;
        public readonly DestinySystem Destiny;
        public readonly KnowledgeSystem Knowledge;
        public readonly ClanSystem Clans;
        public readonly PoliticsSystem Politics;
        public readonly WorldRules Rules = new WorldRules();
        public readonly EventLog Events = new EventLog();

        // Share of its natural ceiling the world's qi settles toward: the Quy luật slider, any đại kiếp, and the
        // great cycle of the ages (mạt pháp ↔ linh khí phục tô).
        public float QiScale => Rules[Rule.WorldQi] * Disasters.QiFactor * Eras.QiFactor(Clock.Tick);
        public readonly List<LoggedCommand> Log = new List<LoggedCommand>();

        readonly Queue<IWorldCommand> _pending = new Queue<IWorldCommand>();
        double _dayAccumulator;

        public int SpeedIndex = 1;
        public int TicksLastFrame { get; private set; }
        public bool Paused => SpeedIndex == 0;

        // Progress (0..1) toward the next tick; rendering uses it to interpolate movement.
        public float TickFraction => (float)System.Math.Min(1.0, _dayAccumulator);

        public Simulation(WorldData world)
        {
            World = world;
            Events.History = History;
            Qi = new QiSystem(world);
            Forage = new ForageSystem(world);
            world.TerrainChanged += Forage.RebuildCapBlocks; // must exist before settlements start clearing fields
            world.LookChanged += Forage.RebuildCapBlocks;    // scars change how much grass grows
            Paths = new PathSystem(this);  // trails and roads worn by walkers (before the routes that follow them)
            Nav = new NavSystem(this);     // routes for walkers, rebuilt where the land changes
            Creatures = new CreatureSystem(this);
            Settlements = new SettlementSystem(this);
            Wildlife = new WildlifeSystem(world, Forage); // after villages have cleared their first fields
            Cultivation = new CultivationSystem(this);
            Factions = new FactionSystem(this); // needs the sect members to weigh each sect's power
            Techniques = new TechniqueSystem(this); // công pháp for every sect and every one alive; before the relics that hold some
            Combat = new CombatSystem(this);
            Stories = new StoryDetector(this);
            Protagonists = new ProtagonistAI(this);
            Scars = new ScarSystem(this);
            Disasters = new DisasterSystem(this);
            Beasts = new BeastSystem(this);
            Relics = new RelicSystem(this);
            Trade = new TradeSystem(this);
            Eras = new EraSystem(this);
            Harm = new HarmSystem(this);
            Faith = new FaithSystem(this);
            Destiny = new DestinySystem(this);
            Knowledge = new KnowledgeSystem(this);
            Clans = new ClanSystem(this);
            Politics = new PoliticsSystem(this); // a king of a ruling house for every kingdom
        }

        public void Enqueue(IWorldCommand command) => _pending.Enqueue(command);

        public void ApplyPending()
        {
            while (_pending.Count > 0)
            {
                var c = _pending.Dequeue();
                Faith.Heaven = true; // whatever this act kills, heaven killed (FaithSystem)
                try { c.Apply(this); }
                finally { Faith.Heaven = false; }
                Log.Add(new LoggedCommand(Clock.Tick, c));
            }
        }

        // Per-system CPU time (ms, accumulated) for the Stats window and the perf probe; not part of the world.
        public static readonly string[] SystemNames =
        {
            "Di chuyển", "Linh khí", "Cỏ", "Thiên tai", "Thú hoang", "Làng", "Tu sĩ", "Đấu pháp", "Yêu thú", "Nhân vật chính",
            "Bí cảnh", "Thương mại", "Thế lực", "Thời đại", "Truyền kỳ", "Tín ngưỡng", "Tin đồn", "Chính trị"
        };
        [System.NonSerialized] public readonly double[] SystemMs = new double[SystemNames.Length];
        static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        void Mark(int system, ref long t0)
        {
            long now = Stopwatch.GetTimestamp();
            SystemMs[system] += (now - t0) * MsPerTick;
            t0 = now;
        }

        public void Step()
        {
            Clock.Advance();
            long tick = Clock.Tick;
            long t = Stopwatch.GetTimestamp();
            Creatures.Tick(tick); Mark(0, ref t);
            Cultivation.ErrandStep(tick); Mark(6, ref t); // errands: herbs, hunts, markets, patrols, day by day (Errands)
            Beasts.DailyStep(tick); Mark(8, ref t); // beasts and passers-by: chases, fights, flights (BeastChase)
            if (Clock.IsMonthStart)
            {
                Paths.MonthlyStep(tick); Mark(0, ref t); // villagers tread their roads; new trails reach the map and the routes
                Qi.MonthlyStep(RegenMultiplier(Clock.Season), QiScale); Mark(1, ref t);
                Forage.MonthlyStep(Clock.Season); Mark(2, ref t);
                Disasters.MonthlyStep(tick); Mark(3, ref t); // floods recede, droughts wither the grass, epidemics run their course
                Wildlife.MonthlyStep(Clock.Season, Rules[Rule.Births]); Mark(4, ref t);
                Settlements.MonthlyStep(tick); Mark(5, ref t);
                Cultivation.MonthlyStep(tick); Mark(6, ref t);
                Combat.MonthlyStep(tick); Mark(7, ref t);
                Beasts.MonthlyStep(tick); Mark(8, ref t);
                Protagonists.MonthlyStep(tick); Mark(9, ref t);
                Relics.MonthlyStep(tick); Mark(10, ref t);
                Trade.MonthlyStep(tick); Mark(11, ref t);
                Factions.MonthlyStep(tick); Mark(12, ref t);
                Faith.MonthlyStep(tick);
                Destiny.MonthlyStep(tick); Mark(15, ref t);
                Knowledge.MonthlyStep(tick); Mark(16, ref t);
            }
            if (Clock.IsYearStart)
            {
                Settlements.YearlyStep(tick); Mark(5, ref t);
                Cultivation.YearlyStep(tick);
                Techniques.YearlyStep(tick); Mark(6, ref t);
                Factions.YearlyStep(tick); Mark(12, ref t);
                Combat.YearlyStep(tick); Mark(7, ref t);
                Disasters.YearlyStep(tick);
                Scars.YearlyStep(tick); Mark(3, ref t); // the land heals its scars
                Paths.YearlyStep(); Mark(0, ref t); // grass takes back the trails no one walks
                Beasts.YearlyStep(tick); Mark(8, ref t);
                Relics.YearlyStep(tick); Mark(10, ref t);
                Eras.YearlyStep(tick); Mark(13, ref t);
                Stories.YearlyStep(tick); Mark(14, ref t);
                Faith.YearlyStep(tick); Mark(15, ref t);
                Clans.YearlyStep(tick);
                Politics.YearlyStep(tick); Mark(17, ref t);
            }
        }

        public void RunFrame(float realDeltaSeconds, double budgetMs)
        {
            ApplyPending();
            TicksLastFrame = 0;
            float speed = SpeedDaysPerSecond[SpeedIndex];
            if (speed <= 0f)
            {
                _dayAccumulator = 0;
                return;
            }
            // Cap the backlog so a slow frame never triggers a catch-up spiral.
            _dayAccumulator = System.Math.Min(_dayAccumulator + speed * realDeltaSeconds, System.Math.Max(1.0, speed * 0.25));
            var sw = Stopwatch.StartNew();
            while (_dayAccumulator >= 1.0 && sw.Elapsed.TotalMilliseconds < budgetMs)
            {
                Step();
                _dayAccumulator -= 1.0;
                TicksLastFrame++;
            }
        }

        // Land in the rect just became water or lava: whoever stood there falls in, right now.
        public void ResolveFlood(int x0, int y0, int x1, int y1)
        {
            long tick = Clock.Tick;
            Cultivation.Flood(x0, y0, x1, y1, tick);
            Settlements.Flood(x0, y0, x1, y1, tick);
            Beasts?.Flood(x0, y0, x1, y1, tick);
            Trade?.Flood(x0, y0, x1, y1);
        }

        public static float RegenMultiplier(Season s)
        {
            switch (s)
            {
                case Season.Xuan: return 1.25f;
                case Season.Ha: return 1f;
                case Season.Thu: return 0.85f;
                default: return 0.6f;
            }
        }

        public ulong ComputeStateHash()
        {
            ulong h = StateHash.Seed;
            StateHash.Add(ref h, Clock.Tick);
            var w = World;
            for (int i = 0; i < w.Terrain.Length; i++)
            {
                StateHash.Add(ref h, (int)w.Terrain[i] | (w.LeyLine[i] ? 256 : 0) | (w.QiCap[i] << 9) | ((long)w.Zone[i] << 24) | ((long)w.Owner[i] << 32) | ((long)w.Scar[i] << 48));
            }
            var objs = w.Objects;
            for (int id = 0; id < objs.Capacity; id++)
            {
                if (!objs.IsAlive(id)) continue;
                var o = objs.Get(id);
                StateHash.Add(ref h, id);
                StateHash.Add(ref h, (int)o.Type | (o.Variant << 8));
                StateHash.Add(ref h, o.X | (o.Y << 16));
            }
            Qi.HashInto(ref h);
            Forage.HashInto(ref h);
            Creatures.HashInto(ref h);
            Wildlife.HashInto(ref h);
            Cultivation.HashInto(ref h);
            Settlements.HashInto(ref h);
            Factions.HashInto(ref h);
            Combat.HashInto(ref h);
            History.HashInto(ref h);
            Stories.HashInto(ref h);
            Disasters.HashInto(ref h);
            Rules.HashInto(ref h);
            Beasts.HashInto(ref h);
            Relics.HashInto(ref h);
            Trade.HashInto(ref h);
            Eras.HashInto(ref h);
            Paths.HashInto(ref h);
            Techniques.HashInto(ref h);
            Faith.HashInto(ref h);
            Destiny.HashInto(ref h);
            Knowledge.HashInto(ref h);
            Clans.HashInto(ref h);
            Politics.HashInto(ref h);
            return h;
        }
    }

    public static class StateHash
    {
        public const ulong Seed = 14695981039346656037UL;

        public static void Add(ref ulong h, long v)
        {
            for (int b = 0; b < 8; b++)
            {
                h ^= (byte)(v >> (b * 8));
                h *= 1099511628211UL;
            }
        }
    }
}
