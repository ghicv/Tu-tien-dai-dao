using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ThienDao.Core;
using ThienDao.Sim;
using ThienDao.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ThienDao.Editor
{
    // How heavy is the simulation over a long run? CPU per simulated year and managed memory, century by century.
    public static class PerfProbe
    {
        [MenuItem("Thiên Đạo/Đo hiệu năng 1000 năm → Docs/Perf_ThienDao.md")]
        public static void Run() => EditorJob.Start(Probe(), "đo hiệu năng 1000 năm");

        static System.Collections.IEnumerator Probe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Năm | ms/năm (mô phỏng) | Bộ nhớ managed (MB) | Tu sĩ sống / tổng từng có | Dòng sử sách | Entity (slot) | Phàm nhân | Tệp lưu (KB) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            long baseMem = GC.GetTotalMemory(true);
            var genSw = Stopwatch.StartNew();
            var sim = new Simulation(MapGenerator.Generate("ThienDao"));
            double genMs = genSw.Elapsed.TotalMilliseconds;
            long afterGen = GC.GetTotalMemory(true);
            Row(sb, sim, 0, 0, baseMem);
            var breakdown = new StringBuilder();
            breakdown.AppendLine();
            breakdown.AppendLine("## ms/năm theo hệ thống (trung bình mỗi thế kỷ)");
            breakdown.AppendLine();
            breakdown.Append("| Hệ thống |");
            for (int c = 1; c <= 10; c++) breakdown.Append($" {c * 100} |");
            breakdown.AppendLine();
            breakdown.Append("|---|");
            for (int c = 1; c <= 10; c++) breakdown.Append("---|");
            breakdown.AppendLine();
            var perSystem = new double[Simulation.SystemNames.Length, 10];
            var perVillage = new double[SettlementSystem.ProfNames.Length, 10];
            Array.Clear(SettlementSystem.ProfMs, 0, SettlementSystem.ProfMs.Length);
            for (int century = 1; century <= 10; century++)
            {
                double simMs = 0;
                for (int y = 0; y < 100; y++)
                {
                    var yearSw = Stopwatch.StartNew();
                    for (int d = 0; d < SimClock.DaysPerYear; d++) sim.Step();
                    simMs += yearSw.Elapsed.TotalMilliseconds;
                    yield return null;
                }
                double msPerYear = simMs / 100.0;
                for (int s = 0; s < Simulation.SystemNames.Length; s++) { perSystem[s, century - 1] = sim.SystemMs[s] / 100.0; sim.SystemMs[s] = 0; }
                for (int s = 0; s < SettlementSystem.ProfNames.Length; s++) { perVillage[s, century - 1] = SettlementSystem.ProfMs[s] / 100.0; SettlementSystem.ProfMs[s] = 0; }
                Row(sb, sim, century * 100, msPerYear, baseMem);
                Debug.Log($"[ThienDao] perf: year {century * 100}, {msPerYear:0.0} ms/year");
            }

            for (int s = 0; s < Simulation.SystemNames.Length; s++)
            {
                breakdown.Append($"| {Simulation.SystemNames[s]} |");
                for (int c = 0; c < 10; c++) breakdown.Append($" {perSystem[s, c]:0.0} |");
                breakdown.AppendLine();
            }

            breakdown.AppendLine();
            breakdown.AppendLine("## Trong hệ Làng (ms/năm)");
            breakdown.AppendLine();
            breakdown.Append("| Phần |");
            for (int c = 1; c <= 10; c++) breakdown.Append($" {c * 100} |");
            breakdown.AppendLine();
            breakdown.Append("|---|");
            for (int c = 1; c <= 10; c++) breakdown.Append("---|");
            breakdown.AppendLine();
            for (int s = 0; s < SettlementSystem.ProfNames.Length; s++)
            {
                breakdown.Append($"| {SettlementSystem.ProfNames[s]} |");
                for (int c = 0; c < 10; c++) breakdown.Append($" {perVillage[s, c]:0.0} |");
                breakdown.AppendLine();
            }

            var head = new StringBuilder();
            head.AppendLine("# Hiệu năng mô phỏng — seed ThienDao, 1000 năm (Editor, không render)");
            head.AppendLine();
            head.AppendLine($"Sinh map + khởi tạo: {genMs:0} ms; bộ nhớ sau khởi tạo: {(afterGen - baseMem) / 1048576.0:0.0} MB.");
            head.AppendLine();
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Perf_ThienDao.md");
            File.WriteAllText(path, head.ToString() + sb + breakdown, new UTF8Encoding(false));
            Debug.Log("[ThienDao] Perf written to " + path);
        }

        static void Row(StringBuilder sb, Simulation sim, int year, double msPerYear, long baseMem)
        {
            long mem = GC.GetTotalMemory(true) - baseMem;
            int alive = sim.Cultivation.AliveCount, total = sim.Cultivation.All.Count;
            int save = SaveGame.SaveToBytes(sim, "ThienDao").Length / 1024;
            sb.AppendLine($"| {year} | {msPerYear:0.0} | {mem / 1048576.0:0.0} | {alive} / {total} | {sim.History.All.Count:N0} | {sim.Entities.Count:N0} | {sim.Settlements.TotalPopulation:N0} | {save:N0} |");
        }
    }
}
