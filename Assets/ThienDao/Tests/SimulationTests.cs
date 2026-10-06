using NUnit.Framework;
using ThienDao.Core;
using ThienDao.Sim;
using ThienDao.World;

namespace ThienDao.Tests
{
    public class SimulationTests
    {
        static void Run(Simulation sim, int days)
        {
            sim.ApplyPending();
            for (int i = 0; i < days; i++) sim.Step();
        }

        static Simulation RunScripted(string seed)
        {
            var sim = new Simulation(MapGenerator.Generate(seed));
            Run(sim, 45);
            sim.Enqueue(new InfuseQiCommand(500, 500, 30, -0.5f));
            sim.Enqueue(new LeyLineCommand(300, 300, 3, true));
            sim.Enqueue(new PaintTerrainCommand(600, 400, 10, Terrain.Mountain));
            sim.Enqueue(new PlantTreesCommand(420, 420, 20, 1234u));
            sim.Enqueue(new SpawnCreaturesCommand(Species.Wolf, 450, 450, 6));
            var v = sim.Settlements.All[0];
            sim.Enqueue(new FoundVillageCommand(v.X + 40, v.Y, 24, 1));
            Run(sim, 400);
            sim.Enqueue(new InfuseQiCommand(200, 700, 20, 0.8f));
            Run(sim, 200);
            return sim;
        }

        [Test]
        public void SameSeedProducesSameWorld()
        {
            var a = new Simulation(MapGenerator.Generate("det"));
            var b = new Simulation(MapGenerator.Generate("det"));
            Assert.AreEqual(a.ComputeStateHash(), b.ComputeStateHash());
        }

        [Test]
        public void DifferentSeedsProduceDifferentWorlds()
        {
            var a = new Simulation(MapGenerator.Generate("det"));
            var b = new Simulation(MapGenerator.Generate("det2"));
            Assert.AreNotEqual(a.ComputeStateHash(), b.ComputeStateHash());
        }

        [Test]
        public void SameCommandsProduceSameState()
        {
            Assert.AreEqual(RunScripted("cmd").ComputeStateHash(), RunScripted("cmd").ComputeStateHash());
        }

        [Test]
        public void ReplayFromLogMatchesLiveRun()
        {
            var live = RunScripted("replay");
            var replay = new Simulation(MapGenerator.Generate("replay"));
            int next = 0;
            while (replay.Clock.Tick < live.Clock.Tick)
            {
                while (next < live.Log.Count && live.Log[next].Tick == replay.Clock.Tick)
                    replay.Enqueue(live.Log[next++].Command);
                replay.ApplyPending();
                replay.Step();
            }
            Assert.AreEqual(live.Log.Count, next);
            Assert.AreEqual(live.ComputeStateHash(), replay.ComputeStateHash());
        }

        [Test]
        public void DrainedQiRecoversToUndisturbedLevel()
        {
            const int bx = 64, by = 64;
            var control = new Simulation(MapGenerator.Generate("qi"));
            var drained = new Simulation(MapGenerator.Generate("qi"));

            drained.Enqueue(new InfuseQiCommand(bx * QiSystem.Block + 4, by * QiSystem.Block + 4, 40, -1f));
            drained.ApplyPending();
            Assert.Less(drained.Qi.BlockQi(bx, by), control.Qi.BlockQi(bx, by) * 0.5f + 1f, "drain should empty the block");

            Run(control, SimClock.DaysPerYear);
            Run(drained, SimClock.DaysPerYear);
            Assert.Less(drained.Qi.BlockQi(bx, by), control.Qi.BlockQi(bx, by), "one year is not enough to fully recover");

            Run(control, SimClock.DaysPerYear * 25);
            Run(drained, SimClock.DaysPerYear * 25);
            Assert.AreEqual(control.Qi.BlockQi(bx, by), drained.Qi.BlockQi(bx, by), 1f);
        }

        [Test]
        public void InfusedQiSpreadsToNeighbours()
        {
            const int bx = 40, by = 90;
            var sim = new Simulation(MapGenerator.Generate("spread"));
            float neighbourBefore = sim.Qi.BlockQi(bx + 3, by);
            sim.Enqueue(new InfuseQiCommand(bx * QiSystem.Block + 4, by * QiSystem.Block + 4, 8, 1.5f));
            Run(sim, SimClock.DaysPerMonth * 3);
            Assert.Greater(sim.Qi.BlockQi(bx + 3, by), neighbourBefore);
        }

        [Test]
        public void WorldStartsWithVillagesAndAnimals()
        {
            var sim = new Simulation(MapGenerator.Generate("life"));
            Assert.Greater(sim.Settlements.AliveCount, 10);
            Assert.Greater(sim.Settlements.TotalPopulation, 500);
            Assert.Greater(sim.Wildlife.Total(Species.Deer), 1000f);
            Assert.Greater(sim.Wildlife.Total(Species.Rabbit), 1000f);
            Assert.Greater(sim.Wildlife.Total(Species.Wolf), 20f);
            // A sect town perched on bare mountain may have no fields; ordinary villages must.
            int withFarms = 0;
            foreach (var s in sim.Settlements.All)
                if (s.Farms.Count > 0) withFarms++;
            Assert.GreaterOrEqual(withFarms, sim.Settlements.All.Count * 0.9f);
        }

        [Test]
        public void LifeContinuesForThirtyYears()
        {
            var sim = new Simulation(MapGenerator.Generate("life"));
            Run(sim, SimClock.DaysPerYear * 30);
            Assert.Greater(sim.Settlements.TotalPopulation, 500, "humans should not die out");
            var wild = sim.Wildlife;
            Assert.Greater(wild.Total(Species.Deer), 100f, "deer should not die out");
            Assert.Greater(wild.Total(Species.Rabbit), 200f, "rabbits should not die out");
            Assert.Greater(wild.Total(Species.Wolf), 5f, "wolves should not die out");
            Assert.Less(wild.Total(Species.Rabbit), 200000f, "populations should stay bounded");
        }

        [Test]
        public void VillageShrinksWhenItsLandTurnsToDesert()
        {
            var control = new Simulation(MapGenerator.Generate("famine"));
            var cursed = new Simulation(MapGenerator.Generate("famine"));
            var target = cursed.Settlements.All[0];
            for (int dy = -40; dy <= 40; dy += 20)
            for (int dx = -40; dx <= 40; dx += 20)
                cursed.Enqueue(new PaintTerrainCommand(target.X + dx, target.Y + dy, 24, Terrain.Desert));

            Run(control, SimClock.DaysPerYear * 10);
            Run(cursed, SimClock.DaysPerYear * 10);
            Assert.Less(cursed.Settlements.All[0].Population, control.Settlements.All[0].Population);
        }

        [Test]
        public void SectsStartWithCultivators()
        {
            var sim = new Simulation(MapGenerator.Generate("tutien"));
            int sects = 0;
            foreach (var s in sim.Settlements.All)
            {
                if (!s.Sect) continue;
                sects++;
                int members = 0;
                foreach (var c in sim.Cultivation.All)
                    if (c.SectId == s.Id) members++;
                Assert.GreaterOrEqual(members, 15, s.BaseName);
                var master = sim.Cultivation.MasterOf(s.Id);
                Assert.IsNotNull(master, s.BaseName);
                Assert.GreaterOrEqual((int)master.Realm, (int)Realm.KetDan, "a sect is led by at least a Kết Đan master");
            }
            Assert.Greater(sects, 0);
            Assert.LessOrEqual(sim.Cultivation.CountByRealm[(int)Realm.NguyenAnh], 1, "Nguyên Anh overlords are rare");
        }

        [Test]
        public void CultivatorsBreakThroughAgeAndAwaken()
        {
            var sim = new Simulation(MapGenerator.Generate("tutien"));
            Run(sim, SimClock.DaysPerYear * 60);
            var n = sim.Events.CountByKind;
            Assert.Greater(n[(int)EventKind.Breakthrough], 0, "someone should break through");
            Assert.Greater(n[(int)EventKind.Death], 0, "someone should die");
            Assert.Greater(n[(int)EventKind.Awakening], 0, "village children should awaken spirit roots");
            Assert.Greater(sim.Cultivation.AliveCount, 0);
        }

        [Test]
        public void CultivatorsDrawDownQiAroundTheirSect()
        {
            var sim = new Simulation(MapGenerator.Generate("tutien"));
            Run(sim, SimClock.DaysPerYear * 5);
            Settlement sect = null;
            foreach (var s in sim.Settlements.All)
                if (s.Sect) { sect = s; break; }
            int bx = sect.X / QiSystem.Block, by = sect.Y / QiSystem.Block;
            Assert.Less(sim.Qi.BlockQi(bx, by), sim.Qi.BlockCap(bx, by) * 0.98f);
        }

        // Thiên Đạo wrecks the world at random; whatever happens, the invariants must hold afterwards.
        static Simulation RunChaos(string seed, int rounds)
        {
            var sim = new Simulation(MapGenerator.Generate(seed));
            var rng = new DetRandom(Hash.FromString(seed) ^ 0xBADu);
            for (int round = 0; round < rounds; round++)
            {
                var alive = new System.Collections.Generic.List<Settlement>();
                foreach (var s in sim.Settlements.All)
                    if (s.Alive) alive.Add(s);
                var target = alive.Count > 0 ? alive[rng.Range(0, alive.Count)] : null;
                int tx = target?.X ?? rng.Range(50, 970), ty = target?.Y ?? rng.Range(50, 970);
                switch (rng.Range(0, 9))
                {
                    case 0: sim.Enqueue(new PaintTerrainCommand(tx, ty, rng.Range(10, 40), Terrain.Shallow)); break;
                    case 1: sim.Enqueue(new PaintTerrainCommand(tx, ty, rng.Range(10, 40), Terrain.DeepOcean)); break;
                    case 2: sim.Enqueue(new PaintTerrainCommand(tx, ty, rng.Range(6, 30), Terrain.Mountain)); break;
                    case 3: sim.Enqueue(new PaintTerrainCommand(tx, ty, rng.Range(10, 40), Terrain.Desert)); break;
                    case 4: sim.Enqueue(new EraseObjectsCommand(tx, ty, 20)); break;
                    case 5: sim.Enqueue(new DivineActCommand(DivineAct.Smite, tx, ty)); break;
                    case 6: sim.Enqueue(new SpawnCreaturesCommand(Species.Wolf, tx + 20, ty, 10)); break;
                    case 7: sim.Enqueue(new FoundVillageCommand(rng.Range(50, 970), rng.Range(50, 970), 24, 0)); break;
                    default: sim.Enqueue(new LeyLineCommand(tx, ty, 4, rng.NextFloat() < 0.5f)); break;
                }
                Run(sim, 60);
            }
            return sim;
        }

        [Test]
        public void InvariantsHoldUnderChaos()
        {
            var sim = RunChaos("chaos", 60);
            var errors = WorldInvariants.Check(sim);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void ChaosIsDeterministic()
        {
            Assert.AreEqual(RunChaos("chaos2", 15).ComputeStateHash(), RunChaos("chaos2", 15).ComputeStateHash());
        }

        [Test]
        public void FloodDrownsTheSectButFlyersEscape()
        {
            var sim = new Simulation(MapGenerator.Generate("flood"));
            Settlement sect = null;
            foreach (var s in sim.Settlements.All)
                if (s.Sect) { sect = s; break; }
            var members = new System.Collections.Generic.List<Cultivator>();
            foreach (var c in sim.Cultivation.All)
                if (c.SectId == sect.Id && sim.Cultivation.IsAtHome(c)) members.Add(c);

            sim.Enqueue(new PaintTerrainCommand(sect.X, sect.Y, 24, Terrain.Shallow));
            sim.ApplyPending(); // the flood resolves at once, before any tick

            Assert.IsFalse(sect.Alive, "a drowned town is gone");
            foreach (var c in members)
            {
                if (c.Realm == Realm.LuyenKhi) Assert.IsFalse(c.Alive, $"{c.Name} cannot fly and should drown");
                else
                {
                    Assert.IsTrue(c.Alive, $"{c.Name} can fly and should escape");
                    Assert.IsTrue(sim.World.IsWalkable(sim.Entities.X[c.Entity], sim.Entities.Y[c.Entity]), $"{c.Name} should be on dry land");
                }
            }
            Run(sim, SimClock.DaysPerMonth * 2);
            Assert.IsEmpty(WorldInvariants.Check(sim));
        }

        [Test]
        public void VillageUnderARaisedMountainMovesAway()
        {
            var sim = new Simulation(MapGenerator.Generate("flood"));
            Settlement village = null;
            foreach (var s in sim.Settlements.All)
                if (!s.Sect) { village = s; break; }
            int oldX = village.X, oldY = village.Y;
            sim.Enqueue(new PaintTerrainCommand(oldX, oldY, 10, Terrain.Mountain));
            Run(sim, SimClock.DaysPerMonth * 2);
            Assert.IsTrue(village.Alive);
            Assert.IsTrue(village.X != oldX || village.Y != oldY, "the village should have moved off the mountain");
            Assert.IsEmpty(WorldInvariants.Check(sim));
        }

        [Test]
        public void CalendarRollsOver()
        {
            var clock = new SimClock();
            Assert.AreEqual((1, 1, 1, Season.Xuan), (clock.Year, clock.Month, clock.Day, clock.Season));
            for (int i = 0; i < SimClock.DaysPerSeason; i++) clock.Advance();
            Assert.AreEqual(Season.Ha, clock.Season);
            for (int i = SimClock.DaysPerSeason; i < SimClock.DaysPerYear; i++) clock.Advance();
            Assert.AreEqual((2, 1, 1, Season.Xuan), (clock.Year, clock.Month, clock.Day, clock.Season));
        }

        [Test]
        public void GrantRootActsOnTheChosenOne()
        {
            var sim = new Simulation(MapGenerator.Generate("ThienDao"));
            // A cultivator with a mixed root is refined to a single-element Thiên linh căn, nobody new appears.
            Cultivator chosen = null;
            foreach (var c in sim.Cultivation.All)
                if (c.Alive && SpiritRoots.Count(c.Roots) >= 3 && (c.Roots & SpiritRoots.Variant) == 0) { chosen = c; break; }
            Assert.IsNotNull(chosen);
            int count = sim.Cultivation.All.Count;
            int oldRoots = chosen.Roots;
            sim.Enqueue(new DivineActCommand(DivineAct.GrantRoot, (int)chosen.HomeX, (int)chosen.HomeY, chosen.Index));
            sim.ApplyPending();
            Assert.AreEqual(count, sim.Cultivation.All.Count, "granting a root must not create a new person");
            Assert.AreEqual(1, SpiritRoots.Count(chosen.Roots));
            Assert.AreNotEqual(0, chosen.Roots & oldRoots, "the refined root keeps one of the person's own elements");

            // A mortal of the chosen village (an adult, not a newborn child) awakens.
            Settlement village = null;
            foreach (var s in sim.Settlements.All)
                if (s.Alive && !s.Sect && s.Population > 20) { village = s; break; }
            int pop = village.Population;
            sim.Enqueue(new DivineActCommand(DivineAct.GrantRoot, village.X, village.Y, -1, village.Id));
            sim.ApplyPending();
            Assert.AreEqual(count + 1, sim.Cultivation.All.Count);
            Assert.AreEqual(pop - 1, village.Population);
            var awakened = sim.Cultivation.All[count];
            Assert.GreaterOrEqual(awakened.AgeYears(sim.Clock.Tick), 15f);
            Assert.IsTrue(awakened.Travelling, "the awakened one sets out at once and shows on the map");

            // Granting on empty ground does nothing.
            sim.Enqueue(new DivineActCommand(DivineAct.GrantRoot, 5, 5));
            sim.ApplyPending();
            Assert.AreEqual(count + 1, sim.Cultivation.All.Count);
            CollectionAssert.IsEmpty(WorldInvariants.Check(sim));
        }

        // ---------------------------------------------------------------- M4: thế lực

        [Test]
        public void SectsStartAsFactionsHoldingLand()
        {
            var sim = new Simulation(MapGenerator.Generate("ThienDao"));
            var fs = sim.Factions;
            int sects = 0;
            foreach (var s in sim.Settlements.All)
            {
                if (!s.Sect) continue;
                sects++;
                var f = fs.Get(s.Id);
                Assert.IsNotNull(f, $"{s.Name} has no faction");
                Assert.AreEqual(f, fs.OwnerAt(s.X, s.Y), $"{s.Name} does not hold its own seat");
                Assert.GreaterOrEqual(f.Tiles, 3, $"{s.Name} holds too little land");
                Assert.Greater(f.Power, 0f);
            }
            Assert.AreEqual(sects, fs.AliveCount);
            CollectionAssert.IsEmpty(WorldInvariants.Check(sim));
        }

        [Test]
        public void NewSectsAreFoundedAndBreakAway()
        {
            var sim = new Simulation(MapGenerator.Generate("ThienDao"));
            int start = sim.Factions.All.Count;
            Run(sim, SimClock.DaysPerYear * 150);
            var k = sim.Events.CountByKind;
            Assert.Greater(k[(int)EventKind.Founding] + k[(int)EventKind.Schism], 0, "no sect was founded or split in 150 years");
            Assert.Greater(sim.Factions.All.Count, start, "no new faction appeared");
            CollectionAssert.IsEmpty(WorldInvariants.Check(sim));
        }

        [Test]
        public void WarIsFoughtOverLandAndCostsLives()
        {
            var sim = new Simulation(MapGenerator.Generate("ThienDao"));
            var fs = sim.Factions;
            // The two closest sects go to war.
            Faction a = null, b = null;
            int best = int.MaxValue;
            foreach (var x in fs.All)
            foreach (var y in fs.All)
            {
                if (x.Id >= y.Id) continue;
                var sx = sim.Settlements.All[x.Id];
                var sy = sim.Settlements.All[y.Id];
                int d = (sx.X - sy.X) * (sx.X - sy.X) + (sx.Y - sy.Y) * (sx.Y - sy.Y);
                if (d < best) { best = d; a = x; b = y; }
            }
            Assert.IsTrue(fs.DeclareWar(a.Id, b.Id, sim.Clock.Tick));
            Assert.AreEqual(Stance.War, fs.StanceBetween(a.Id, b.Id));
            Run(sim, SimClock.DaysPerYear * 3);
            Assert.Greater(a.BattlesWon + a.BattlesLost, 0, "no battle was fought in three years of war");
            CollectionAssert.IsEmpty(WorldInvariants.Check(sim));
        }

        [Test]
        public void DestroyedSectLeavesNoDanglingState()
        {
            var sim = new Simulation(MapGenerator.Generate("ThienDao"));
            var fs = sim.Factions;
            Settlement sect = null;
            foreach (var s in sim.Settlements.All)
                if (s.Sect) { sect = s; break; }
            // Drown the whole sect: the faction must go with it, land and quarrels included.
            sim.Enqueue(new PaintTerrainCommand(sect.X, sect.Y, 30, Terrain.DeepOcean));
            Run(sim, SimClock.DaysPerYear * 2);
            Assert.IsFalse(fs.Get(sect.Id).Alive);
            for (int t = 0; t < fs.TileOwner.Length; t++) Assert.AreNotEqual(sect.Id + 1, fs.TileOwner[t]);
            CollectionAssert.IsEmpty(WorldInvariants.Check(sim));
        }
    }
}
