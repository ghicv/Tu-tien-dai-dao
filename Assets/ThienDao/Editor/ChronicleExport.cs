using System.Collections.Generic;
using System.IO;
using System.Text;
using ThienDao.Core;
using ThienDao.Sim;
using ThienDao.World;
using UnityEditor;
using UnityEngine;

namespace ThienDao.Editor
{
    // M5 check: run a world for a thousand years without rendering and write its chronicle to Docs/, so we can
    // read whether the history it tells holds together.
    public static class ChronicleExport
    {
        const int Years = 1000;

        [MenuItem("Thiên Đạo/Mô phỏng 1000 năm → Docs/Chronicle_ThienDao.md")]
        public static void Run() => Run("ThienDao", Years);

        public static string Run(string seed, int years)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sim = new Simulation(MapGenerator.Generate(seed));
            for (int y = 0; y < years; y++)
            {
                for (int d = 0; d < SimClock.DaysPerYear; d++) sim.Step();
                if (y % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Thiên Đạo", $"Năm {y} / {years}", y / (float)years)) break;
            }
            EditorUtility.ClearProgressBar();

            string md = Markdown(sim, seed, sw.Elapsed.TotalSeconds);
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", $"Chronicle_{seed}.md");
            File.WriteAllText(path, md, new UTF8Encoding(false));
            Debug.Log($"[ThienDao] Chronicle of '{seed}' ({sim.Clock.Year - 1} years, {sw.Elapsed.TotalSeconds:0} s) written to {path}");
            return path;
        }

        static string Markdown(Simulation sim, string seed, double seconds)
        {
            var h = sim.History;
            var sb = new StringBuilder();
            var cr = sim.Cultivation.CountByRealm;
            sb.AppendLine($"# Biên niên sử — thế giới \"{seed}\"");
            sb.AppendLine();
            sb.AppendLine($"Mô phỏng {sim.Clock.Year - 1} năm trong {seconds:0} giây. Cuối cùng: {sim.Settlements.TotalPopulation:N0} phàm nhân, " +
                          $"{sim.Cultivation.AliveCount} tu sĩ (Kết Đan {cr[3]}, Nguyên Anh {cr[4]}, Hóa Thần {cr[5]}), {sim.Factions.AliveCount} thế lực. " +
                          $"Sử sách ghi {h.All.Count:N0} sự kiện, {sim.Stories.All.Count} truyền kỳ.");
            sb.AppendLine();

            sb.AppendLine("## Truyền kỳ");
            sb.AppendLine();
            foreach (var s in sim.Stories.All) sb.AppendLine($"- **Năm {s.Year} — {s.Title}.** {s.Text}");
            sb.AppendLine();

            sb.AppendLine("## Danh nhân");
            sb.AppendLine();
            var legends = new List<Cultivator>();
            sim.Stories.Legends(legends);
            var recs = new List<HistoryRecord>();
            foreach (var c in legends)
            {
                string fate = c.Alive ? $"còn tại thế, {c.AgeYears(sim.Clock.Tick):0} tuổi"
                    : $"vẫn lạc năm {c.DeathTick / SimClock.DaysPerYear + 1}" + (c.KilledBy >= 0 ? $" dưới tay {sim.Cultivation.All[c.KilledBy].Name}" : "");
                sb.AppendLine($"- **{c.Epithet}** — {c.Name}, {c.RealmText}, {sim.Cultivation.SectName(c)}; {SpiritRoots.Kind(c.OriginRoots)}; {c.Kills} mạng; {fate}.");
            }
            sb.AppendLine();

            sb.AppendLine("## Biên niên theo thế kỷ");
            for (int century = 0; century < h.Centuries; century++)
            {
                sb.AppendLine();
                sb.AppendLine($"### Thế kỷ {century + 1}");
                sb.AppendLine();
                sb.AppendLine($"Lập tông {h.CountIn(century, EventKind.Founding)} · ly khai {h.CountIn(century, EventKind.Schism)} · trận đánh {h.CountIn(century, EventKind.Battle)} · " +
                              $"diệt môn {h.CountIn(century, EventKind.Destruction)} · đấu pháp {h.CountIn(century, EventKind.Duel)} · đột phá {h.CountIn(century, EventKind.Breakthrough)} · " +
                              $"thiên kiếp {h.CountIn(century, EventKind.Tribulation)}");
                sb.AppendLine();
                h.InCentury(century, 3, recs);
                foreach (var r in recs)
                    if (r.Kind != EventKind.Legend) sb.AppendLine($"- Năm {r.Year}: {r.Text}");
            }
            return sb.ToString();
        }
    }
}
