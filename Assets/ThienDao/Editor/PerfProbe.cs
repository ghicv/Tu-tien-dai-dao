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
            sb.AppendLine("| Năm | ms/năm (mô phỏng) | Bộ nhớ managed (MB) | Tu sĩ sống / tổng từng có | Dòng sử sách | Entity (slot) | Phàm nhân | Tệp lưu (KB) | Yêu thú sống | Chiến trường cổ | Bí cảnh mở | Lời cầu (đã cầu / quá hạn) | Làng tín ngưỡng ≥ 60 / ≤ 8 | Ma tu sống | Trúc Cơ / Kết Đan / Nguyên Anh / Hóa Thần | Công pháp (có / thất truyền) | Tin đồn (đang lan / tới muộn / dòm ngó) | Nước (còn / khởi nghĩa / đổi triều / cát cứ / dẹp yên / thống nhất) | Gia tộc (còn / thế thù) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
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
            var perNav = new double[NavSystem.ProfNames.Length + 5, 10];
            Array.Clear(NavSystem.ProfMs, 0, NavSystem.ProfMs.Length);
            NavSystem.Expanded = NavSystem.Failures = NavSystem.Waits = 0;
            int plansBefore = sim.Nav.Planned;
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
                for (int s = 0; s < NavSystem.ProfNames.Length; s++) { perNav[s, century - 1] = NavSystem.ProfMs[s] / 100.0; NavSystem.ProfMs[s] = 0; }
                int np = NavSystem.ProfNames.Length;
                perNav[np, century - 1] = (sim.Nav.Planned - plansBefore) / 100.0; plansBefore = sim.Nav.Planned;
                perNav[np + 1, century - 1] = NavSystem.Failures / 100.0;
                perNav[np + 2, century - 1] = NavSystem.Expanded / 100.0;
                perNav[np + 3, century - 1] = NavSystem.Waits / 100.0;
                perNav[np + 4, century - 1] = NavSystem.ZoneRebuilds / 100.0;
                NavSystem.ZoneRebuilds = 0;
                NavSystem.Expanded = NavSystem.Failures = NavSystem.Waits = 0;
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

            breakdown.AppendLine();
            breakdown.AppendLine("## Trong hệ Di chuyển (mỗi năm)");
            breakdown.AppendLine();
            breakdown.Append("| Phần |");
            for (int c = 1; c <= 10; c++) breakdown.Append($" {c * 100} |");
            breakdown.AppendLine();
            breakdown.Append("|---|");
            for (int c = 1; c <= 10; c++) breakdown.Append("---|");
            breakdown.AppendLine();
            string[] navRows = { NavSystem.ProfNames[0] + " (ms)", NavSystem.ProfNames[1] + " (ms)", NavSystem.ProfNames[2] + " (ms)", "Số lần tìm đường", "Không có đường", "Nút A* đã mở", "Chờ lượt tìm", "Lần đánh số vùng" };
            for (int s = 0; s < navRows.Length; s++)
            {
                breakdown.Append($"| {navRows[s]} |");
                for (int c = 0; c < 10; c++) breakdown.Append($" {perNav[s, c]:0.#} |");
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
            int fields = 0, open = 0;
            foreach (var l in sim.Disasters.Landmarks) if (l.Alive && l.Kind == Landmark.Battlefield) fields++;
            foreach (var r in sim.Relics.All) if (r.Open) open++;
            var cr = sim.Cultivation.CountByRealm;
            int late = 0, covet = 0, risings = 0, dynasties = 0, splits = 0, crushed = 0, reunited = 0, kingdoms = 0, clans = 0, feuds = 0;
            foreach (var kg in sim.World.Kingdoms) if (!kg.Fallen) kingdoms++;
            foreach (var cl in sim.Clans.All) if (!cl.Fallen) { clans++; feuds += cl.Feuds.Count; }
            foreach (var e in sim.History.All)
            {
                if (e.Text.Contains("tranh đoạt muộn")) late++;
                else if (e.Text.Contains("bắt đầu dòm ngó")) covet++;
                else if (e.Text.Contains("rơi vào nội chiến")) risings++;
                else if (e.Text.Contains("lên ngôi, lập triều")) dynasties++;
                else if (e.Text.Contains(" cát cứ ")) splits++;
                else if (e.Text.Contains(" dẹp yên ")) crushed++;
                else if (e.Text.Contains("giang sơn về một mối")) reunited++;
            }
            int faithless = 0, demonic = 0;
            foreach (var s in sim.Settlements.All) if (s.Alive && !s.Sect && s.Faith <= 8f) faithless++;
            foreach (var c in sim.Cultivation.All) if (c.Alive && c.Demonic) demonic++;
            sb.AppendLine($"| {year} | {msPerYear:0.0} | {mem / 1048576.0:0.0} | {alive} / {total} | {sim.History.All.Count:N0} | {sim.Entities.Count:N0} | {sim.Settlements.TotalPopulation:N0} | {save:N0} | {sim.Beasts.AliveCount} | {fields} | {open} | {sim.Faith.Raised} / {sim.Faith.Ignored} | {sim.Faith.FaithfulCount(FaithSystem.ShrineFaith)} / {faithless} | {demonic} | {cr[2]} / {cr[3]} / {cr[4]} / {cr[5]} | {sim.Techniques.All.Count} / {sim.Techniques.LostCount()} | {sim.Knowledge.All.Count} / {late} / {covet} | {kingdoms} / {risings} / {dynasties} / {splits} / {crushed} / {reunited} | {clans} / {feuds / 2} |");
        }
    }
}
