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

            Run(control, SimClock.DaysPerYear * 100);
            Run(drained, SimClock.DaysPerYear * 100);
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
        public void CalendarRollsOver()
        {
            var clock = new SimClock();
            Assert.AreEqual((1, 1, 1, Season.Xuan), (clock.Year, clock.Month, clock.Day, clock.Season));
            for (int i = 0; i < SimClock.DaysPerSeason; i++) clock.Advance();
            Assert.AreEqual(Season.Ha, clock.Season);
            for (int i = SimClock.DaysPerSeason; i < SimClock.DaysPerYear; i++) clock.Advance();
            Assert.AreEqual((2, 1, 1, Season.Xuan), (clock.Year, clock.Month, clock.Day, clock.Season));
        }
    }
}
