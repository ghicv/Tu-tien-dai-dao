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
        public readonly EntityStore Entities = new EntityStore();
        public readonly CreatureSystem Creatures;
        public readonly WildlifeSystem Wildlife;
        public readonly SettlementSystem Settlements;
        public readonly CultivationSystem Cultivation;
        public readonly FactionSystem Factions;
        public readonly CombatSystem Combat;
        public readonly HistoryLog History = new HistoryLog();
        public readonly StoryDetector Stories;
        public readonly ProtagonistAI Protagonists;
        public readonly DisasterSystem Disasters;
        public readonly WorldRules Rules = new WorldRules();
        public readonly EventLog Events = new EventLog();

        // Share of its natural ceiling the world's qi settles toward: the Quy luật slider times any đại kiếp.
        public float QiScale => Rules[Rule.WorldQi] * Disasters.QiFactor;
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
            Creatures = new CreatureSystem(this);
            Settlements = new SettlementSystem(this);
            Wildlife = new WildlifeSystem(world, Forage); // after villages have cleared their first fields
            Cultivation = new CultivationSystem(this);
            Factions = new FactionSystem(this); // needs the sect members to weigh each sect's power
            Combat = new CombatSystem(this);
            Stories = new StoryDetector(this);
            Protagonists = new ProtagonistAI(this);
            Disasters = new DisasterSystem(this);
        }

        public void Enqueue(IWorldCommand command) => _pending.Enqueue(command);

        public void ApplyPending()
        {
            while (_pending.Count > 0)
            {
                var c = _pending.Dequeue();
                c.Apply(this);
                Log.Add(new LoggedCommand(Clock.Tick, c));
            }
        }

        public void Step()
        {
            Clock.Advance();
            long tick = Clock.Tick;
            Creatures.Tick(tick);
            if (Clock.IsMonthStart)
            {
                Qi.MonthlyStep(RegenMultiplier(Clock.Season), QiScale);
                Forage.MonthlyStep(Clock.Season);
                Disasters.MonthlyStep(tick); // floods recede, droughts wither the grass, epidemics run their course
                Wildlife.MonthlyStep(Clock.Season, Rules[Rule.Births]);
                Settlements.MonthlyStep(tick);
                Cultivation.MonthlyStep(tick);
                Combat.MonthlyStep(tick);
                Protagonists.MonthlyStep(tick);
                Factions.MonthlyStep(tick);
            }
            if (Clock.IsYearStart)
            {
                Settlements.YearlyStep(tick);
                Cultivation.YearlyStep(tick);
                Factions.YearlyStep(tick);
                Combat.YearlyStep(tick);
                Disasters.YearlyStep(tick);
                Stories.YearlyStep(tick);
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
                StateHash.Add(ref h, (int)w.Terrain[i] | (w.LeyLine[i] ? 256 : 0) | (w.QiCap[i] << 9) | ((long)w.Zone[i] << 24) | ((long)w.Owner[i] << 32));
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
